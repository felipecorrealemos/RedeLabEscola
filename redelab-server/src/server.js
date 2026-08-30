require('dotenv').config({ quiet: true });

const express = require('express');
const cors = require('cors');
const http = require('node:http');
const path = require('node:path');
const { pool, testDatabaseConnection } = require('./config/database');
const faseRoutes = require('./routes/faseRoutes');
const missaoRoutes = require('./routes/missaoRoutes');
const usuarioRoutes = require('./routes/usuarioRoutes');
const progressoRoutes = require('./routes/progressoRoutes');
const devProgressoRoutes = require('./routes/devProgressoRoutes');
const authRoutes = require('./routes/authRoutes');
const meRoutes = require('./routes/meRoutes');
const { tratarErroAutenticacao } = require('./middleware/auth');
const { criarDevPresencaRoutes } = require('./routes/devPresencaRoutes');
const { Presenca } = require('./websocket/presenca');
const { criarServidorWebSocket } = require('./websocket/webSocketServer');
const { criarMonitorRoutes } = require('./routes/monitorRoutes');

const port = Number(process.env.PORT || 3000);
const devRoutesHabilitadas = String(process.env.ENABLE_DEV_ROUTES || '').toLowerCase() === 'true';
const monitorPublicDirectory = path.join(__dirname, '..', 'public', 'monitor');
const bootstrapDirectory = path.join(__dirname, '..', 'node_modules', 'bootstrap', 'dist');
const bootstrapIconsDirectory = path.join(
  __dirname,
  '..',
  'node_modules',
  'bootstrap-icons',
  'font'
);

function detalheTecnico(error) {
  if (error.message) {
    return error.message;
  }
  if (Array.isArray(error.errors) && error.errors.length > 0) {
    return error.errors
      .map((item) => item.message || item.code)
      .filter(Boolean)
      .join('; ');
  }
  return error.code || String(error);
}

function criarOpcoesCors() {
  const configuracao = process.env.CORS_ORIGIN || '*';
  if (configuracao === '*') {
    return {};
  }

  const origensPermitidas = configuracao.split(',').map((origem) => origem.trim());
  return {
    origin(origem, callback) {
      if (!origem || origensPermitidas.includes(origem)) {
        return callback(null, true);
      }
      return callback(new Error('Origem não permitida pelo CORS.'));
    },
  };
}

function criarApp({
  habilitarRotasDev = devRoutesHabilitadas,
  presenca = new Presenca(),
} = {}) {
  const app = express();

  app.use(cors(criarOpcoesCors()));
  app.use(express.json({ limit: '100kb' }));

  app.get('/monitor', (req, res) => res.sendFile(path.join(monitorPublicDirectory, 'index.html')));
  app.use('/monitor', express.static(monitorPublicDirectory, { index: false }));
  app.use('/monitor/vendor/bootstrap', express.static(bootstrapDirectory, { index: false }));
  app.use(
    '/monitor/vendor/bootstrap-icons',
    express.static(bootstrapIconsDirectory, { index: false })
  );

  app.get('/api/health', async (req, res) => {
    try {
      await testDatabaseConnection();
      res.json({ status: 'ok', database: 'connected' });
    } catch (error) {
      console.error('Falha no health check do banco:', detalheTecnico(error));
      res.status(503).json({ status: 'error', database: 'unavailable' });
    }
  });

  app.use('/api/fases', faseRoutes);
  app.use('/api/missoes', missaoRoutes);
  app.use('/api/auth', authRoutes);
  app.use('/api/me', meRoutes);
  app.use('/api/progresso', progressoRoutes);
  app.use('/api/monitor', criarMonitorRoutes(presenca));

  if (habilitarRotasDev) {
    app.use('/api/usuarios', usuarioRoutes);
    app.use('/api/progresso', devProgressoRoutes);
    app.use('/api/dev', criarDevPresencaRoutes(presenca));
  }

  app.use((req, res) => {
    res.status(404).json({ error: 'Endpoint não encontrado.' });
  });

  app.use(tratarErroAutenticacao);

  app.use((error, req, res, next) => {
    if (error.type === 'entity.parse.failed') {
      return res.status(400).json({ error: 'JSON inválido.' });
    }

    console.error('Erro interno da API:', error);
    if (res.headersSent) {
      return next(error);
    }
    return res.status(500).json({ error: 'Erro interno do servidor.' });
  });

  return app;
}

const presenca = new Presenca();
const app = criarApp({ presenca });

function criarServidorHttp({
  aplicacao = app,
  presencaWebSocket = presenca,
  opcoesWebSocket,
} = {}) {
  const server = http.createServer(aplicacao);
  const websocket = criarServidorWebSocket({
    server,
    presenca: presencaWebSocket,
    ...opcoesWebSocket,
  });
  return { server, websocket, presenca: presencaWebSocket };
}

async function iniciarServidor() {
  if (!Number.isInteger(port) || port <= 0 || port > 65535) {
    console.error('PORT deve ser um número entre 1 e 65535.');
    process.exitCode = 1;
    return;
  }

  try {
    await testDatabaseConnection();
    console.log(`MySQL conectado: ${process.env.DB_HOST}:${process.env.DB_PORT}/${process.env.DB_NAME}`);
  } catch (error) {
    console.error('Não foi possível conectar ao MySQL. Verifique o .env e o serviço do banco.');
    console.error(`Detalhe técnico: ${detalheTecnico(error)}`);
    process.exitCode = 1;
    await pool.end();
    return;
  }

  const { server, websocket } = criarServidorHttp();
  server.listen(port, () => {
    console.log(`API RedeLab Escola disponível em http://localhost:${port}`);
    console.log(`WebSocket disponível em ws://localhost:${port}/ws`);
    console.log(`Monitor disponível em http://localhost:${port}/monitor`);
    console.log(`Rotas DEV/LEGACY: ${devRoutesHabilitadas ? 'habilitadas' : 'desabilitadas'}`);
  });

  let encerrando = false;
  async function encerrar(sinal) {
    if (encerrando) {
      return;
    }
    encerrando = true;
    console.log(`\n${sinal} recebido. Encerrando a API...`);
    await websocket.encerrar();
    server.close(async () => {
      await pool.end();
      process.exit(0);
    });
  }

  process.on('SIGINT', () => encerrar('SIGINT'));
  process.on('SIGTERM', () => encerrar('SIGTERM'));
}

if (require.main === module) {
  iniciarServidor();
}

module.exports = { app, criarApp, criarServidorHttp, iniciarServidor, presenca };
