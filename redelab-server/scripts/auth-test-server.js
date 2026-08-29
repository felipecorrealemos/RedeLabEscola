const path = require('node:path');
const express = require('express');

const AUTH_TEST_PORT = 8080;
const CLIENT_DIRECTORY = path.join(__dirname, '..', 'test-client');
const AUTH0_SDK_FILE = path.join(
  __dirname,
  '..',
  'node_modules',
  '@auth0',
  'auth0-spa-js',
  'dist',
  'auth0-spa-js.production.js'
);

function addDevelopmentHeaders(req, res, next) {
  res.set({
    'Cache-Control': 'no-store',
    'Content-Security-Policy': [
      "default-src 'self'",
      "script-src 'self'",
      "style-src 'self'",
      "img-src 'self' data:",
      "connect-src 'self' http://localhost:3000 ws://localhost:3000 https://dev-ldgwwvi01va0qxzx.us.auth0.com",
      "frame-src https://dev-ldgwwvi01va0qxzx.us.auth0.com",
      "base-uri 'none'",
      "frame-ancestors 'none'",
      "form-action 'none'",
    ].join('; '),
    'Referrer-Policy': 'no-referrer',
    'X-Content-Type-Options': 'nosniff',
  });
  next();
}

function criarAuthTestApp() {
  const app = express();
  app.disable('x-powered-by');
  app.use(addDevelopmentHeaders);

  app.get('/vendor/auth0-spa-js.production.js', (req, res) => {
    res.sendFile(AUTH0_SDK_FILE);
  });
  app.get('/healthz', (req, res) => {
    res.json({ status: 'ok', tool: 'redelab-auth-test' });
  });
  app.use(express.static(CLIENT_DIRECTORY, { index: 'index.html', fallthrough: false }));

  app.use((error, req, res, next) => {
    if (res.headersSent) {
      return next(error);
    }
    return res.status(error.status === 404 ? 404 : 500).json({
      error: error.status === 404 ? 'Arquivo não encontrado.' : 'Erro no servidor de teste.',
    });
  });

  return app;
}

function iniciar() {
  const server = criarAuthTestApp().listen(AUTH_TEST_PORT, '127.0.0.1', () => {
    console.log(`Teste temporário Auth0 disponível em http://localhost:${AUTH_TEST_PORT}`);
    console.log('Use somente em desenvolvimento. Pressione Ctrl+C para encerrar.');
  });

  function encerrar() {
    server.close(() => process.exit(0));
  }
  process.once('SIGINT', encerrar);
  process.once('SIGTERM', encerrar);
  return server;
}

if (require.main === module) {
  iniciar();
}

module.exports = { AUTH_TEST_PORT, criarAuthTestApp, iniciar };
