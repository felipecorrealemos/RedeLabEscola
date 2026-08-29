const fs = require('node:fs');
const path = require('node:path');
const express = require('express');

const UNITY_WEBGL_PORT = 8081;
const BUILD_DIRECTORY = path.join(__dirname, '..', '..', 'Build_WebGL');
const AUTH0_SDK_FILE = path.join(
  __dirname,
  '..',
  'node_modules',
  '@auth0',
  'auth0-spa-js',
  'dist',
  'auth0-spa-js.production.js'
);

function developmentHeaders(req, res, next) {
  res.set({
    'Cache-Control': 'no-store',
    'Content-Security-Policy': [
      "default-src 'self'",
      "script-src 'self' 'unsafe-inline' 'unsafe-eval' 'wasm-unsafe-eval'",
      "style-src 'self' 'unsafe-inline'",
      "img-src 'self' data: blob:",
      "connect-src 'self' http://localhost:3000 ws://localhost:3000 https://dev-ldgwwvi01va0qxzx.us.auth0.com",
      "frame-src https://dev-ldgwwvi01va0qxzx.us.auth0.com",
      "worker-src 'self' blob:",
      "base-uri 'self'",
    ].join('; '),
    'Referrer-Policy': 'no-referrer',
    'X-Content-Type-Options': 'nosniff',
  });
  next();
}

function setUnityContentHeaders(res, filePath) {
  if (!filePath.endsWith('.gz')) return;
  res.setHeader('Content-Encoding', 'gzip');
  if (filePath.endsWith('.js.gz')) res.setHeader('Content-Type', 'application/javascript');
  if (filePath.endsWith('.wasm.gz')) res.setHeader('Content-Type', 'application/wasm');
  if (filePath.endsWith('.data.gz')) res.setHeader('Content-Type', 'application/octet-stream');
}

function createUnityWebGLApp() {
  const app = express();
  app.disable('x-powered-by');
  app.use(developmentHeaders);

  app.get('/vendor/auth0-spa-js.production.js', (req, res) => {
    res.sendFile(AUTH0_SDK_FILE);
  });
  app.get('/healthz', (req, res) => {
    res.json({ status: 'ok', tool: 'redelab-unity-webgl-auth' });
  });
  app.use(express.static(BUILD_DIRECTORY, {
    index: 'index.html',
    fallthrough: false,
    setHeaders: setUnityContentHeaders,
  }));

  app.use((error, req, res, next) => {
    if (res.headersSent) return next(error);
    return res.status(error.status === 404 ? 404 : 500).json({
      error: error.status === 404 ? 'Arquivo do build nao encontrado.' : 'Erro no servidor WebGL.',
    });
  });
  return app;
}

function start() {
  if (!fs.existsSync(path.join(BUILD_DIRECTORY, 'index.html'))) {
    throw new Error(`Build WebGL nao encontrado em ${BUILD_DIRECTORY}`);
  }
  if (!fs.existsSync(AUTH0_SDK_FILE)) {
    throw new Error('SDK Auth0 local nao encontrado. Execute npm install em redelab-server.');
  }

  const server = createUnityWebGLApp().listen(UNITY_WEBGL_PORT, '127.0.0.1', () => {
    console.log(`RedeLab Unity WebGL disponivel em http://localhost:${UNITY_WEBGL_PORT}`);
    console.log('Use somente em desenvolvimento. Pressione Ctrl+C para encerrar.');
  });

  function stop() {
    server.close(() => process.exit(0));
  }
  process.once('SIGINT', stop);
  process.once('SIGTERM', stop);
  return server;
}

if (require.main === module) start();

module.exports = { UNITY_WEBGL_PORT, BUILD_DIRECTORY, createUnityWebGLApp, start };
