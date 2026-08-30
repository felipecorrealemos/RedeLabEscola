const { WebSocket, WebSocketServer } = require('ws');
const { pool } = require('../config/database');
const { validarAccessTokenSocket } = require('../middleware/auth');
const { Presenca } = require('./presenca');
const { monitorUpdates } = require('../services/monitorUpdates');

const WS_PATH = '/ws';
const MONITOR_WS_PATH = '/ws/monitor';
const AUTH_TIMEOUT_MS = 8000;
const HEARTBEAT_INTERVAL_MS = 30000;
const MAX_PAYLOAD_BYTES = 16 * 1024;
const MAX_MESSAGES_BEFORE_AUTH = 3;

async function buscarUsuarioPorSub(sub) {
  const [usuarios] = await pool.query(
    'SELECT id_usuario FROM usuario WHERE auth0_id = ?',
    [sub]
  );
  return usuarios[0] || null;
}

function enviar(socket, mensagem) {
  if (socket.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(mensagem));
  }
}

function enviarErroEFechar(socket, codigo, razao) {
  enviar(socket, { type: 'auth_error', error: razao });
  if (socket.readyState === WebSocket.OPEN) {
    socket.close(codigo, razao);
  }
}

function origemPermitida(origem) {
  const configuracao = String(process.env.CORS_ORIGIN || '*').trim();
  if (!origem || configuracao === '*') {
    return true;
  }
  return configuracao.split(',').map((item) => item.trim()).includes(origem);
}

function criarServidorWebSocket({
  server,
  presenca = new Presenca(),
  validarToken = validarAccessTokenSocket,
  buscarUsuario = buscarUsuarioPorSub,
  authTimeoutMs = AUTH_TIMEOUT_MS,
  heartbeatIntervalMs = HEARTBEAT_INTERVAL_MS,
  maxPayload = MAX_PAYLOAD_BYTES,
} = {}) {
  if (!server) {
    throw new Error('Uma instância de servidor HTTP é obrigatória para o WebSocket.');
  }

  const estados = new WeakMap();
  const estadosMonitor = new WeakMap();
  const wss = new WebSocketServer({ noServer: true, maxPayload });
  const monitorWss = new WebSocketServer({ noServer: true, maxPayload: 1024 });

  function rejeitarUpgrade(socket, status, mensagem) {
    socket.write(
      `HTTP/1.1 ${status}\r\nConnection: close\r\nContent-Type: text/plain\r\n\r\n${mensagem}`
    );
    socket.destroy();
  }

  function tratarUpgrade(request, socket, head) {
    if (!origemPermitida(request.headers.origin)) {
      rejeitarUpgrade(socket, '403 Forbidden', 'Origem não permitida');
      return;
    }

    let pathname;
    try {
      pathname = new URL(request.url, 'http://localhost').pathname;
    } catch {
      rejeitarUpgrade(socket, '400 Bad Request', 'URL inválida');
      return;
    }

    const destino = pathname === WS_PATH ? wss : pathname === MONITOR_WS_PATH ? monitorWss : null;
    if (!destino) {
      rejeitarUpgrade(socket, '404 Not Found', 'WebSocket não encontrado');
      return;
    }

    destino.handleUpgrade(request, socket, head, (websocket) => {
      destino.emit('connection', websocket, request);
    });
  }

  server.on('upgrade', tratarUpgrade);

  function removerDaPresenca(socket) {
    const estado = estados.get(socket);
    if (!estado || !estado.idUsuario) {
      return;
    }

    const conexoesRestantes = presenca.remover(estado.idUsuario, socket);
    console.log(`Usuário ${estado.idUsuario} desconectado`);
    if (conexoesRestantes === 0) {
      console.log(`Usuário ${estado.idUsuario} offline`);
    }
    estado.idUsuario = null;
  }

  wss.on('connection', (socket) => {
    console.log('WebSocket conectado');
    const estado = {
      autenticado: false,
      autenticando: false,
      idUsuario: null,
      respondeuHeartbeat: true,
      mensagensAntesDaAutenticacao: 0,
      authTimer: null,
    };
    estados.set(socket, estado);

    estado.authTimer = setTimeout(() => {
      if (!estado.autenticado) {
        enviarErroEFechar(socket, 1008, 'auth_timeout');
      }
    }, authTimeoutMs);
    estado.authTimer.unref?.();

    socket.on('pong', () => {
      estado.respondeuHeartbeat = true;
    });

    socket.on('message', async (dados, isBinary) => {
      if (estado.autenticado) {
        enviar(socket, { type: 'error', error: 'unsupported_message' });
        return;
      }

      estado.mensagensAntesDaAutenticacao += 1;
      if (estado.mensagensAntesDaAutenticacao > MAX_MESSAGES_BEFORE_AUTH) {
        enviarErroEFechar(socket, 1008, 'message_limit');
        return;
      }
      if (estado.autenticando) {
        enviarErroEFechar(socket, 1008, 'auth_in_progress');
        return;
      }
      if (isBinary) {
        enviarErroEFechar(socket, 1003, 'text_messages_only');
        return;
      }

      let mensagem;
      try {
        mensagem = JSON.parse(dados.toString('utf8'));
      } catch {
        enviarErroEFechar(socket, 1008, 'invalid_message');
        return;
      }

      if (
        !mensagem ||
        mensagem.type !== 'auth' ||
        typeof mensagem.accessToken !== 'string' ||
        !mensagem.accessToken.trim()
      ) {
        enviarErroEFechar(socket, 1008, 'auth_required');
        return;
      }

      estado.autenticando = true;
      try {
        const payload = await validarToken(mensagem.accessToken);
        const sub = payload && payload.sub;
        if (typeof sub !== 'string' || !sub.trim()) {
          throw new Error('Token sem subject válido.');
        }

        const usuario = await buscarUsuario(sub);
        if (!usuario) {
          enviarErroEFechar(socket, 1008, 'user_not_synced');
          return;
        }
        if (socket.readyState !== WebSocket.OPEN) {
          return;
        }

        estado.autenticado = true;
        estado.idUsuario = Number(usuario.id_usuario);
        clearTimeout(estado.authTimer);
        presenca.adicionar(estado.idUsuario, socket);
        enviar(socket, { type: 'auth_ok', id_usuario: estado.idUsuario });
        console.log(`Usuário ${estado.idUsuario} autenticado`);
      } catch (error) {
        console.warn('Falha na autenticação do WebSocket:', error.message || error.code || 'erro');
        enviarErroEFechar(socket, 1008, 'invalid_token');
      } finally {
        estado.autenticando = false;
      }
    });

    socket.on('close', () => {
      clearTimeout(estado.authTimer);
      removerDaPresenca(socket);
    });

    socket.on('error', (error) => {
      console.warn('Erro de conexão WebSocket:', error.message);
    });
  });

  // Canal temporariamente público e estritamente somente leitura para o monitor.
  // Ele não autentica jogadores, não entra na presença e não aceita comandos.
  monitorWss.on('connection', (socket) => {
    estadosMonitor.set(socket, { respondeuHeartbeat: true });
    enviar(socket, { type: 'monitor_ready' });
    socket.on('pong', () => {
      const estado = estadosMonitor.get(socket);
      if (estado) {
        estado.respondeuHeartbeat = true;
      }
    });
    socket.on('message', () => {
      enviar(socket, { type: 'error', error: 'read_only' });
      socket.close(1008, 'read_only');
    });
    socket.on('error', (error) => {
      console.warn('Erro no WebSocket do monitor:', error.message);
    });
  });

  const aoMudarPresenca = (evento) => {
    for (const socket of monitorWss.clients) {
      enviar(socket, {
        type: 'monitor_update',
        reason: evento.online ? 'usuario_online' : 'usuario_offline',
        id_usuario: Number(evento.id_usuario),
      });
    }
  };
  const aoAtualizarDados = (evento) => {
    const motivo = typeof evento === 'string' ? evento : evento.motivo;
    const idUsuario = typeof evento === 'object' ? evento.id_usuario : null;
    for (const socket of monitorWss.clients) {
      enviar(socket, {
        type: 'monitor_update',
        reason: motivo,
        ...(idUsuario ? { id_usuario: Number(idUsuario) } : {}),
      });
    }
  };
  presenca.on('change', aoMudarPresenca);
  monitorUpdates.on('update', aoAtualizarDados);

  const heartbeatTimer = setInterval(() => {
    for (const socket of wss.clients) {
      const estado = estados.get(socket);
      if (!estado) {
        continue;
      }
      if (!estado.respondeuHeartbeat) {
        socket.terminate();
        continue;
      }
      estado.respondeuHeartbeat = false;
      if (socket.readyState === WebSocket.OPEN) {
        socket.ping();
      }
    }
    for (const socket of monitorWss.clients) {
      const estado = estadosMonitor.get(socket);
      if (!estado) {
        continue;
      }
      if (!estado.respondeuHeartbeat) {
        socket.terminate();
        continue;
      }
      estado.respondeuHeartbeat = false;
      if (socket.readyState === WebSocket.OPEN) {
        socket.ping();
      }
    }
  }, heartbeatIntervalMs);
  heartbeatTimer.unref?.();

  async function encerrar() {
    clearInterval(heartbeatTimer);
    server.off('upgrade', tratarUpgrade);
    presenca.off('change', aoMudarPresenca);
    monitorUpdates.off('update', aoAtualizarDados);
    for (const servidorWebSocket of [wss, monitorWss]) {
      for (const socket of servidorWebSocket.clients) {
        socket.terminate();
      }
    }
    await Promise.all(
      [wss, monitorWss].map(
        (servidorWebSocket) =>
          new Promise((resolve) => servidorWebSocket.close(() => resolve()))
      )
    );
  }

  return { wss, monitorWss, presenca, encerrar };
}

module.exports = {
  AUTH_TIMEOUT_MS,
  HEARTBEAT_INTERVAL_MS,
  MAX_PAYLOAD_BYTES,
  MONITOR_WS_PATH,
  WS_PATH,
  criarServidorWebSocket,
};
