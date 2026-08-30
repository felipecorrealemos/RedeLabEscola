const { after, before, test } = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const express = require('express');
const { WebSocket } = require('ws');

process.env.AUTH0_DOMAIN ||= 'tenant-de-teste.auth0.com';
process.env.AUTH0_AUDIENCE ||= 'https://api.redelab.test';
process.env.CORS_ORIGIN = '*';

const { validarAccessTokenSocket } = require('../src/middleware/auth');
const { Presenca } = require('../src/websocket/presenca');
const { criarServidorWebSocket } = require('../src/websocket/webSocketServer');
const { notificarMonitor } = require('../src/services/monitorUpdates');

let server;
let service;
let presenca;
let wsUrl;
let monitorWsUrl;
let httpUrl;

function aguardarEvento(emissor, evento) {
  return new Promise((resolve, reject) => {
    emissor.once(evento, (...args) => resolve(args));
    emissor.once('error', reject);
  });
}

async function abrirSocket() {
  const socket = new WebSocket(wsUrl);
  await aguardarEvento(socket, 'open');
  return socket;
}

async function abrirSocketMonitor() {
  const socket = new WebSocket(monitorWsUrl);
  const mensagem = proximaMensagem(socket);
  await aguardarEvento(socket, 'open');
  assert.deepEqual(await mensagem, { type: 'monitor_ready' });
  return socket;
}

function proximaMensagem(socket) {
  return new Promise((resolve, reject) => {
    socket.once('message', (dados) => resolve(JSON.parse(dados.toString('utf8'))));
    socket.once('error', reject);
  });
}

async function autenticarSocket(token = 'token-valido') {
  const socket = await abrirSocket();
  const mensagem = proximaMensagem(socket);
  socket.send(JSON.stringify({ type: 'auth', accessToken: token }));
  assert.deepEqual(await mensagem, { type: 'auth_ok', id_usuario: 77 });
  return socket;
}

async function fecharSocket(socket) {
  if (socket.readyState === WebSocket.CLOSED) {
    return;
  }
  const fechamento = aguardarEvento(socket, 'close');
  socket.close(1000, 'teste_concluido');
  await fechamento;
}

async function aguardarCondicao(condicao, timeoutMs = 1000) {
  const limite = Date.now() + timeoutMs;
  while (!condicao()) {
    if (Date.now() >= limite) {
      throw new Error('Tempo esgotado aguardando condição do teste.');
    }
    await new Promise((resolve) => setTimeout(resolve, 10));
  }
}

before(async () => {
  const app = express();
  app.get('/rest-ok', (req, res) => res.json({ status: 'ok' }));
  server = http.createServer(app);
  presenca = new Presenca();
  service = criarServidorWebSocket({
    server,
    presenca,
    authTimeoutMs: 80,
    heartbeatIntervalMs: 40,
    validarToken: async (token) => {
      if (token === 'token-sem-sync') {
        return { sub: 'auth0|nao-sincronizado' };
      }
      if (token !== 'token-valido') {
        throw new Error('Token inválido para o teste.');
      }
      return { sub: 'auth0|usuario-teste' };
    },
    buscarUsuario: async (sub) =>
      sub === 'auth0|usuario-teste' ? { id_usuario: 77 } : null,
  });

  server.listen(0, '127.0.0.1');
  await aguardarEvento(server, 'listening');
  const porta = server.address().port;
  wsUrl = `ws://127.0.0.1:${porta}/ws`;
  monitorWsUrl = `ws://127.0.0.1:${porta}/ws/monitor`;
  httpUrl = `http://127.0.0.1:${porta}`;
});

after(async () => {
  await service.encerrar();
  await new Promise((resolve, reject) =>
    server.close((error) => (error ? reject(error) : resolve()))
  );
});

test('REST e WebSocket compartilham o mesmo servidor HTTP', async () => {
  const resposta = await fetch(`${httpUrl}/rest-ok`);
  assert.equal(resposta.status, 200);
  assert.deepEqual(await resposta.json(), { status: 'ok' });

  const socket = await autenticarSocket();
  await fecharSocket(socket);
  await aguardarCondicao(() => presenca.resumo().online === 0);
});

test('canal do monitor é somente leitura e recebe mudanças da presença existente', async () => {
  const monitor = await abrirSocketMonitor();
  assert.equal(presenca.resumo().online, 0);

  const entrada = proximaMensagem(monitor);
  const jogo = await autenticarSocket();
  assert.deepEqual(await entrada, {
    type: 'monitor_update',
    reason: 'usuario_online',
    id_usuario: 77,
  });
  assert.equal(presenca.resumo().online, 1);

  const saida = proximaMensagem(monitor);
  await fecharSocket(jogo);
  assert.deepEqual(await saida, {
    type: 'monitor_update',
    reason: 'usuario_offline',
    id_usuario: 77,
  });
  await fecharSocket(monitor);
});

test('canal do monitor identifica o usuário em atualizações de progresso e reset', async () => {
  const monitor = await abrirSocketMonitor();
  const progresso = proximaMensagem(monitor);
  notificarMonitor('missao_concluida', 77);
  assert.deepEqual(await progresso, {
    type: 'monitor_update',
    reason: 'missao_concluida',
    id_usuario: 77,
  });

  const reset = proximaMensagem(monitor);
  notificarMonitor('progresso_reset', 77);
  assert.deepEqual(await reset, {
    type: 'monitor_update',
    reason: 'progresso_reset',
    id_usuario: 77,
  });
  await fecharSocket(monitor);
});

test('canal do monitor rejeita comandos sem afetar o WebSocket autenticado', async () => {
  const monitor = await abrirSocketMonitor();
  const mensagem = proximaMensagem(monitor);
  const fechamento = aguardarEvento(monitor, 'close');
  monitor.send(JSON.stringify({ type: 'auth', accessToken: 'nao-deve-ser-usado' }));
  assert.deepEqual(await mensagem, { type: 'error', error: 'read_only' });
  const [codigo, razao] = await fechamento;
  assert.equal(codigo, 1008);
  assert.equal(razao.toString(), 'read_only');
  assert.equal(presenca.resumo().online, 0);
});

test('heartbeat mantém o canal do monitor responsivo sem contar presença', async () => {
  const monitor = await abrirSocketMonitor();
  await new Promise((resolve) => setTimeout(resolve, 130));
  assert.equal(monitor.readyState, WebSocket.OPEN);
  assert.equal(presenca.resumo().online, 0);
  await fecharSocket(monitor);
});

test('socket sem autenticação é fechado após o timeout', async () => {
  const socket = await abrirSocket();
  const [codigo, razao] = await aguardarEvento(socket, 'close');
  assert.equal(codigo, 1008);
  assert.equal(razao.toString(), 'auth_timeout');
});

test('token inválido recebe auth_error e fecha sem entrar na presença', async () => {
  const socket = await abrirSocket();
  const mensagem = proximaMensagem(socket);
  const fechamento = aguardarEvento(socket, 'close');
  socket.send(JSON.stringify({ type: 'auth', accessToken: 'token-invalido' }));
  assert.deepEqual(await mensagem, { type: 'auth_error', error: 'invalid_token' });
  const [codigo] = await fechamento;
  assert.equal(codigo, 1008);
  assert.equal(presenca.resumo().online, 0);
});

test('mensagem comum antes da autenticação é rejeitada', async () => {
  const socket = await abrirSocket();
  const mensagem = proximaMensagem(socket);
  const fechamento = aguardarEvento(socket, 'close');
  socket.send(JSON.stringify({ type: 'gameplay', id_usuario: 999 }));
  assert.deepEqual(await mensagem, { type: 'auth_error', error: 'auth_required' });
  const [codigo] = await fechamento;
  assert.equal(codigo, 1008);
});

test('token válido sem usuário sincronizado não entra na presença', async () => {
  const socket = await abrirSocket();
  const mensagem = proximaMensagem(socket);
  const fechamento = aguardarEvento(socket, 'close');
  socket.send(JSON.stringify({ type: 'auth', accessToken: 'token-sem-sync' }));
  assert.deepEqual(await mensagem, { type: 'auth_error', error: 'user_not_synced' });
  const [codigo] = await fechamento;
  assert.equal(codigo, 1008);
  assert.equal(presenca.resumo().online, 0);
});

test('payload acima do limite encerra a conexão', async () => {
  const socket = await abrirSocket();
  const fechamento = aguardarEvento(socket, 'close');
  socket.send(JSON.stringify({ type: 'auth', accessToken: 'x'.repeat(20 * 1024) }));
  const [codigo] = await fechamento;
  assert.equal(codigo, 1009);
  assert.equal(presenca.resumo().online, 0);
});

test('duas abas contam duas conexões e usuário só fica offline após ambas fecharem', async () => {
  const primeira = await autenticarSocket();
  const segunda = await autenticarSocket();
  assert.deepEqual(presenca.resumo(), {
    online: 1,
    usuarios: [{ id_usuario: 77, conexoes: 2 }],
  });

  await fecharSocket(primeira);
  await aguardarCondicao(() => presenca.resumo().usuarios[0]?.conexoes === 1);
  assert.equal(presenca.resumo().online, 1);

  await fecharSocket(segunda);
  await aguardarCondicao(() => presenca.resumo().online === 0);
  assert.deepEqual(presenca.resumo(), { online: 0, usuarios: [] });
});

test('heartbeat mantém cliente responsivo conectado', async () => {
  const socket = await autenticarSocket();
  await new Promise((resolve) => setTimeout(resolve, 130));
  assert.equal(socket.readyState, WebSocket.OPEN);
  assert.equal(presenca.resumo().online, 1);
  await fecharSocket(socket);
  await aguardarCondicao(() => presenca.resumo().online === 0);
});

test('heartbeat termina conexão que deixa de responder', async () => {
  const socket = await autenticarSocket();
  socket._socket.pause();
  await aguardarCondicao(() => presenca.resumo().online === 0, 500);
  socket._socket.destroy();
});

test('validador real rejeita JWT malformado sem fazer apenas decode', async () => {
  await assert.rejects(validarAccessTokenSocket('token-invalido'));
});
