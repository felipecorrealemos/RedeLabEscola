const assert = require('node:assert/strict');
const { once } = require('node:events');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const {
  createUnityWebGLApp,
  createUnityWebGLConfig,
  resolverBuildDirectory,
  validarArquivosWebGL,
} = require('../scripts/unity-webgl-auth-server');

const buildDirectory = path.resolve(__dirname, '..', '..', 'Build_WebGL');
const webGlConfig = {
  buildDirectory,
  apiPublicUrl: 'https://jogo.exemplo:3001',
  wsPublicUrl: 'wss://jogo.exemplo:3001',
  auth0Origin: 'https://tenant-de-teste.auth0.com',
};

async function withServer(run) {
  const server = createUnityWebGLApp(webGlConfig).listen(0, '127.0.0.1');
  await once(server, 'listening');
  try {
    const { port } = server.address();
    await run(`http://127.0.0.1:${port}`);
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
}

test('servidor Unity entrega pagina, SDK Auth0 local e headers WebGL', async () => {
  await withServer(async (baseUrl) => {
    const page = await fetch(baseUrl);
    assert.equal(page.status, 200);
    const html = await page.text();
    assert.match(html, /<title>Rede Lab\. Escola<\/title>/);
    assert.doesNotMatch(html, /Unity WebGL Player/);
    assert.match(html, /<link rel="icon" href="data:,">/);
    const contentSecurityPolicy = page.headers.get('content-security-policy');
    assert.match(contentSecurityPolicy, /https:\/\/jogo\.exemplo:3001/);
    assert.match(contentSecurityPolicy, /wss:\/\/jogo\.exemplo:3001/);
    assert.match(contentSecurityPolicy, /https:\/\/tenant-de-teste\.auth0\.com/);
    assert.match(contentSecurityPolicy, /media-src 'self' blob: data:/);

    const sdk = await fetch(`${baseUrl}/vendor/auth0-spa-js.production.js`);
    assert.equal(sdk.status, 200);
    assert.match(await sdk.text(), /createAuth0Client/);

    const wasm = await fetch(`${baseUrl}/Build/Build_WebGL.wasm.gz`);
    assert.equal(wasm.status, 200);
    assert.equal(wasm.headers.get('content-encoding'), 'gzip');
    assert.match(wasm.headers.get('content-type'), /application\/wasm/);
  });
});

test('página de certificado orienta instalação e entrega somente o certificado público', async (t) => {
  const temporaryBuild = fs.mkdtempSync(path.join(os.tmpdir(), 'redelab-webgl-cert-'));
  t.after(() => fs.rmSync(temporaryBuild, { recursive: true, force: true }));
  fs.mkdirSync(path.join(temporaryBuild, 'downloads'));
  fs.writeFileSync(
    path.join(temporaryBuild, 'downloads', 'redelab.crt'),
    'CERTIFICADO PUBLICO DE TESTE'
  );

  const config = { ...webGlConfig, buildDirectory: temporaryBuild };
  const server = createUnityWebGLApp(config).listen(0, '127.0.0.1');
  await once(server, 'listening');
  try {
    const { port } = server.address();
    const baseUrl = `http://127.0.0.1:${port}`;
    const page = await fetch(`${baseUrl}/certificado`);
    assert.equal(page.status, 200);
    const html = await page.text();
    assert.match(html, /<title>Certificado de segurança RedeLab<\/title>/);
    assert.match(html, /Baixar certificado RedeLab/);
    assert.match(html, /Autoridades de Certificação Raiz Confiáveis/);
    assert.match(html, /href="\/downloads\/redelab\.crt"/);

    const download = await fetch(`${baseUrl}/downloads/redelab.crt`);
    assert.equal(download.status, 200);
    assert.match(download.headers.get('content-disposition'), /attachment; filename="redelab\.crt"/);
    assert.match(download.headers.get('content-type'), /application\/x-x509-ca-cert/);
    assert.equal(await download.text(), 'CERTIFICADO PUBLICO DE TESTE');
  } finally {
    await new Promise((resolve) => server.close(resolve));
  }
});

test('configuração WebGL usa ambiente para HTTPS, host, portas e CSP', () => {
  const config = createUnityWebGLConfig({
    UNITY_WEBGL_PORT: '8081',
    UNITY_WEBGL_HOST: '0.0.0.0',
    UNITY_WEBGL_HTTPS_ENABLED: 'true',
    UNITY_WEBGL_HTTPS_KEY_PATH: '/etc/redelab/ssl/redelab.key',
    UNITY_WEBGL_HTTPS_CERT_PATH: '/etc/redelab/ssl/redelab.crt',
    UNITY_WEBGL_BUILD_DIR: buildDirectory,
    API_PUBLIC_URL: 'https://jogo.exemplo:3001',
    WS_PUBLIC_URL: 'wss://jogo.exemplo:3001',
    AUTH0_DOMAIN: 'tenant-de-teste.auth0.com',
  });

  assert.equal(config.port, 8081);
  assert.equal(config.host, '0.0.0.0');
  assert.equal(config.httpsEnabled, 'true');
  assert.equal(config.buildDirectory, buildDirectory);
  assert.equal(config.apiPublicUrl, 'https://jogo.exemplo:3001');
  assert.equal(config.wsPublicUrl, 'wss://jogo.exemplo:3001');
  assert.equal(config.auth0Origin, 'https://tenant-de-teste.auth0.com');
});

test('caminho explícito da Build WebGL tem prioridade e erro mostra o local procurado', () => {
  const configuredDirectory = path.resolve('deploy', 'webgl');
  assert.deepEqual(resolverBuildDirectory(configuredDirectory), {
    directory: configuredDirectory,
    searched: [configuredDirectory],
  });

  assert.throws(
    () =>
      validarArquivosWebGL({
        buildDirectory: configuredDirectory,
        searchedBuildDirectories: [configuredDirectory],
      }),
    new RegExp(`Build WebGL não encontrado.*${configuredDirectory.replace(/[\\/]/g, '[\\\\/]')}`)
  );
});
