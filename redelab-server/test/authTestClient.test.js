const { after, before, test } = require('node:test');
const assert = require('node:assert/strict');
const { criarAuthTestApp } = require('../scripts/auth-test-server');

let server;
let baseUrl;

before(async () => {
  server = criarAuthTestApp().listen(0, '127.0.0.1');
  await new Promise((resolve, reject) => {
    server.once('listening', resolve);
    server.once('error', reject);
  });
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});

after(async () => {
  await new Promise((resolve, reject) =>
    server.close((error) => (error ? reject(error) : resolve()))
  );
});

test('página e recursos locais carregam sem CDN', async () => {
  const paths = ['/', '/style.css', '/config.js', '/app.js', '/vendor/auth0-spa-js.production.js'];
  for (const path of paths) {
    const response = await fetch(`${baseUrl}${path}`);
    assert.equal(response.status, 200, path);
    assert.match(response.headers.get('cache-control'), /no-store/);
  }
});

test('configuração usa domínio, audience e Client ID válido fornecidos', async () => {
  const config = await (await fetch(`${baseUrl}/config.js`)).text();
  assert.match(config, /dev-ldgwwvi01va0qxzx\.us\.auth0\.com/);
  assert.match(config, /https:\/\/api\.redelab\.local/);
  assert.match(config, /Ai8Q8DjlvFJqmcwkcedu5Spdu7XGkrmd/);
  assert.doesNotMatch(config, /AUTH0_CLIENT_ID_AQUI/);
  assert.doesNotMatch(config, /clientSecret/i);
});

test('bundle local é UMD para navegador e expõe createAuth0Client', async () => {
  const sdk = await (await fetch(`${baseUrl}/vendor/auth0-spa-js.production.js`)).text();
  assert.match(sdk, /\.auth0=\{\}/);
  assert.match(sdk, /\.createAuth0Client=/);
});

test('scripts carregam na ordem config, SDK UMD e app', async () => {
  const html = await (await fetch(`${baseUrl}/`)).text();
  const configIndex = html.indexOf('/config.js');
  const sdkIndex = html.indexOf('/vendor/auth0-spa-js.production.js');
  const appIndex = html.indexOf('/app.js');
  assert.ok(configIndex >= 0 && configIndex < sdkIndex && sdkIndex < appIndex);
});

test('cliente implementa SDK, callback, audience, API autenticada e logout', async () => {
  const app = await (await fetch(`${baseUrl}/app.js`)).text();
  for (const expected of [
    'createAuth0Client',
    'loginWithRedirect',
    'handleRedirectCallback',
    'getTokenSilently',
    "scope: AUTH_SCOPE",
    "connection: 'google-oauth2'",
    "logoutParams: { returnTo: window.location.origin }",
    "headers.set('Authorization', `Bearer ${token}`)",
    'clique login recebido',
    'executando loginWithRedirect',
    'redirecionando navegador',
    "new WebSocket(getWebSocketUrl())",
    "JSON.stringify({ type: 'auth', accessToken: token })",
    "message.type === 'auth_ok'",
  ]) {
    assert.ok(app.includes(expected), expected);
  }
  assert.doesNotMatch(app, /localStorage|sessionStorage|console\.log\(.*token/i);
});

test('ações da API começam desabilitadas até autenticação', async () => {
  const html = await (await fetch(`${baseUrl}/`)).text();
  const protectedButtons = [...html.matchAll(/<button[^>]*data-requires-auth[^>]*>/g)];
  assert.equal(protectedButtons.length, 8);
  protectedButtons.forEach(([button]) => assert.match(button, /disabled/));
});

test('CSP permite scripts locais, API, tenant Auth0 e iframe de silent auth', async () => {
  const response = await fetch(`${baseUrl}/`);
  const csp = response.headers.get('content-security-policy');
  assert.match(csp, /script-src 'self'/);
  assert.match(
    csp,
    /connect-src 'self' http:\/\/localhost:3000 ws:\/\/localhost:3000 https:\/\/dev-ldgwwvi01va0qxzx\.us\.auth0\.com/
  );
  assert.match(csp, /frame-src https:\/\/dev-ldgwwvi01va0qxzx\.us\.auth0\.com/);
});
