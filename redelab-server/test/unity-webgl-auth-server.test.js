const assert = require('node:assert/strict');
const { once } = require('node:events');
const test = require('node:test');
const { createUnityWebGLApp } = require('../scripts/unity-webgl-auth-server');

async function withServer(run) {
  const server = createUnityWebGLApp().listen(0, '127.0.0.1');
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
    assert.match(contentSecurityPolicy, /localhost:3000/);
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
