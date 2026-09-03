require('dotenv').config({ quiet: true });

const fs = require('node:fs');
const http = require('node:http');
const https = require('node:https');
const path = require('node:path');
const express = require('express');
const { carregarOpcoesHttps } = require('../src/config/https');

const DEFAULT_UNITY_WEBGL_PORT = 8081;
const DEFAULT_UNITY_WEBGL_HOST = '127.0.0.1';
const DEFAULT_API_PUBLIC_URL = 'http://localhost:3000';
const DEFAULT_WS_PUBLIC_URL = 'ws://localhost:3000';
const CERTIFICATE_PAGE_FILE = path.join(
  __dirname,
  '..',
  'public',
  'certificado',
  'index.html'
);
const PUBLIC_CERTIFICATE_RELATIVE_PATH = path.join('downloads', 'redelab.crt');
const AUTH0_SDK_FILE = path.join(
  __dirname,
  '..',
  'node_modules',
  '@auth0',
  'auth0-spa-js',
  'dist',
  'auth0-spa-js.production.js'
);

function parsePort(value, fallback = DEFAULT_UNITY_WEBGL_PORT) {
  const port = Number(value || fallback);
  if (!Number.isInteger(port) || port <= 0 || port > 65535) {
    throw new Error('UNITY_WEBGL_PORT deve ser um número entre 1 e 65535.');
  }
  return port;
}

function normalizarOrigem(value, label, allowedProtocols) {
  let parsed;
  try {
    parsed = new URL(String(value || '').trim());
  } catch {
    throw new Error(`${label} deve ser uma URL absoluta válida.`);
  }
  if (!allowedProtocols.includes(parsed.protocol)) {
    throw new Error(`${label} deve usar ${allowedProtocols.join(' ou ')}.`);
  }
  return parsed.origin;
}

function normalizarOrigemAuth0(domain) {
  const value = String(domain || '').trim();
  if (!value) {
    throw new Error('AUTH0_DOMAIN é obrigatório para montar a CSP do servidor WebGL.');
  }
  const url = /^https?:\/\//i.test(value) ? value : `https://${value}`;
  return normalizarOrigem(url, 'AUTH0_DOMAIN', ['https:']);
}

function resolverBuildDirectory(configuredPath, existsSync = fs.existsSync) {
  if (String(configuredPath || '').trim()) {
    const directory = path.resolve(String(configuredPath).trim());
    return { directory, searched: [directory] };
  }

  const candidates = [
    path.resolve(__dirname, '..', '..', 'Build_WebGL'),
    path.resolve(__dirname, '..', 'Build_WebGL'),
    path.resolve(process.cwd(), 'Build_WebGL'),
  ].filter((candidate, index, all) => all.indexOf(candidate) === index);
  const directory =
    candidates.find((candidate) => existsSync(path.join(candidate, 'index.html'))) ||
    candidates[0];
  return { directory, searched: candidates };
}

function createUnityWebGLConfig(env = process.env) {
  const build = resolverBuildDirectory(env.UNITY_WEBGL_BUILD_DIR);
  return {
    port: parsePort(env.UNITY_WEBGL_PORT),
    host: String(env.UNITY_WEBGL_HOST || DEFAULT_UNITY_WEBGL_HOST).trim(),
    httpsEnabled: env.UNITY_WEBGL_HTTPS_ENABLED,
    httpsKeyPath: env.UNITY_WEBGL_HTTPS_KEY_PATH,
    httpsCertPath: env.UNITY_WEBGL_HTTPS_CERT_PATH,
    buildDirectory: build.directory,
    searchedBuildDirectories: build.searched,
    apiPublicUrl: normalizarOrigem(
      env.API_PUBLIC_URL || DEFAULT_API_PUBLIC_URL,
      'API_PUBLIC_URL',
      ['http:', 'https:']
    ),
    wsPublicUrl: normalizarOrigem(
      env.WS_PUBLIC_URL || DEFAULT_WS_PUBLIC_URL,
      'WS_PUBLIC_URL',
      ['ws:', 'wss:']
    ),
    auth0Origin: normalizarOrigemAuth0(env.AUTH0_DOMAIN),
  };
}

function securityHeaders({ apiPublicUrl, wsPublicUrl, auth0Origin }) {
  return (req, res, next) => {
    res.set({
      'Cache-Control': 'no-store',
      'Content-Security-Policy': [
        "default-src 'self'",
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' 'wasm-unsafe-eval'",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data: blob:",
        "media-src 'self' blob: data:",
        `connect-src 'self' ${apiPublicUrl} ${wsPublicUrl} ${auth0Origin}`,
        `frame-src ${auth0Origin}`,
        "worker-src 'self' blob:",
        "base-uri 'self'",
      ].join('; '),
      'Referrer-Policy': 'no-referrer',
      'X-Content-Type-Options': 'nosniff',
    });
    next();
  };
}

function setUnityContentHeaders(res, filePath) {
  if (!filePath.endsWith('.gz')) return;
  res.setHeader('Content-Encoding', 'gzip');
  if (filePath.endsWith('.js.gz')) res.setHeader('Content-Type', 'application/javascript');
  if (filePath.endsWith('.wasm.gz')) res.setHeader('Content-Type', 'application/wasm');
  if (filePath.endsWith('.data.gz')) res.setHeader('Content-Type', 'application/octet-stream');
}

function createUnityWebGLApp({
  buildDirectory,
  apiPublicUrl,
  wsPublicUrl,
  auth0Origin,
} = createUnityWebGLConfig()) {
  const app = express();
  app.disable('x-powered-by');
  app.use(securityHeaders({ apiPublicUrl, wsPublicUrl, auth0Origin }));

  app.get('/vendor/auth0-spa-js.production.js', (req, res) => {
    res.sendFile(AUTH0_SDK_FILE);
  });
  app.get('/healthz', (req, res) => {
    res.json({ status: 'ok', tool: 'redelab-unity-webgl-auth' });
  });
  app.get('/certificado', (req, res) => {
    res.sendFile(CERTIFICATE_PAGE_FILE);
  });
  app.get('/downloads/redelab.crt', (req, res) => {
    res.set({
      'Content-Disposition': 'attachment; filename="redelab.crt"',
      'Content-Type': 'application/x-x509-ca-cert',
    });
    res.sendFile(path.join(buildDirectory, PUBLIC_CERTIFICATE_RELATIVE_PATH));
  });
  app.use(
    express.static(buildDirectory, {
      index: 'index.html',
      fallthrough: false,
      setHeaders: setUnityContentHeaders,
    })
  );

  app.use((error, req, res, next) => {
    if (res.headersSent) return next(error);
    return res.status(error.status === 404 ? 404 : 500).json({
      error: error.status === 404 ? 'Arquivo do build nao encontrado.' : 'Erro no servidor WebGL.',
    });
  });
  return app;
}

function validarArquivosWebGL(config) {
  const indexPath = path.join(config.buildDirectory, 'index.html');
  if (!fs.existsSync(indexPath)) {
    const searched = config.searchedBuildDirectories.join(', ');
    throw new Error(
      `Build WebGL não encontrado. Esperado index.html em ${config.buildDirectory}. ` +
        `Caminhos verificados: ${searched}. Configure UNITY_WEBGL_BUILD_DIR se necessário.`
    );
  }
  if (!fs.existsSync(AUTH0_SDK_FILE)) {
    throw new Error('SDK Auth0 local não encontrado. Execute npm ci em redelab-server.');
  }
}

function createUnityWebGLServer(config = createUnityWebGLConfig()) {
  validarArquivosWebGL(config);
  const tlsOptions = carregarOpcoesHttps({
    enabled: config.httpsEnabled,
    keyPath: config.httpsKeyPath,
    certPath: config.httpsCertPath,
    label: 'HTTPS do servidor WebGL',
  });
  const app = createUnityWebGLApp(config);
  const server = tlsOptions
    ? https.createServer(tlsOptions, app)
    : http.createServer(app);
  return { server, protocol: tlsOptions ? 'https' : 'http', config };
}

function start() {
  const { server, protocol, config } = createUnityWebGLServer();
  server.listen(config.port, config.host, () => {
    console.log(
      `RedeLab Unity WebGL disponível em ${protocol}://${config.host}:${config.port}`
    );
    console.log(`Build WebGL: ${config.buildDirectory}`);
    console.log('Pressione Ctrl+C para encerrar.');
  });

  function stop() {
    server.close(() => process.exit(0));
  }
  process.once('SIGINT', stop);
  process.once('SIGTERM', stop);
  return server;
}

if (require.main === module) {
  try {
    start();
  } catch (error) {
    console.error(`Não foi possível iniciar o servidor WebGL: ${error.message}`);
    process.exitCode = 1;
  }
}

module.exports = {
  AUTH0_SDK_FILE,
  CERTIFICATE_PAGE_FILE,
  DEFAULT_API_PUBLIC_URL,
  DEFAULT_UNITY_WEBGL_HOST,
  DEFAULT_UNITY_WEBGL_PORT,
  DEFAULT_WS_PUBLIC_URL,
  PUBLIC_CERTIFICATE_RELATIVE_PATH,
  createUnityWebGLApp,
  createUnityWebGLConfig,
  createUnityWebGLServer,
  normalizarOrigem,
  resolverBuildDirectory,
  setUnityContentHeaders,
  start,
  validarArquivosWebGL,
};
