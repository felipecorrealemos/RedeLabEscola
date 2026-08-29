const { after, before, test } = require('node:test');
const assert = require('node:assert/strict');

process.env.AUTH0_DOMAIN ||= 'tenant-de-teste.auth0.com';
process.env.AUTH0_AUDIENCE ||= 'https://api.redelab.test';
process.env.ENABLE_DEV_ROUTES = 'false';

const { criarApp } = require('../src/server');
const { pool } = require('../src/config/database');

let server;
let baseUrl;

before(async () => {
  server = criarApp({ habilitarRotasDev: false }).listen(0, '127.0.0.1');
  await new Promise((resolve, reject) => {
    server.once('listening', resolve);
    server.once('error', reject);
  });
  baseUrl = `http://127.0.0.1:${server.address().port}`;
});

after(async () => {
  if (server) {
    await new Promise((resolve, reject) =>
      server.close((error) => (error ? reject(error) : resolve()))
    );
  }
  await pool.end();
});

test('rotas públicas continuam acessíveis com o MySQL real', async () => {
  const [health, fases, missoes] = await Promise.all([
    fetch(`${baseUrl}/api/health`),
    fetch(`${baseUrl}/api/fases`),
    fetch(`${baseUrl}/api/missoes`),
  ]);

  assert.equal(health.status, 200);
  assert.equal(fases.status, 200);
  assert.equal(missoes.status, 200);
});

test('rotas protegidas sem Bearer token retornam 401 padronizado', async () => {
  const chamadas = [
    ['POST', '/api/auth/sync'],
    ['GET', '/api/me'],
    ['PUT', '/api/me/personagem'],
    ['DELETE', '/api/me/personagem'],
    ['DELETE', '/api/me/novo-jogo'],
    ['GET', '/api/progresso/me'],
    ['POST', '/api/progresso/concluir'],
    ['DELETE', '/api/progresso/me'],
  ];

  for (const [method, path] of chamadas) {
    const resposta = await fetch(`${baseUrl}${path}`, {
      method,
      headers: method === 'GET' ? undefined : { 'Content-Type': 'application/json' },
      body: method === 'GET' ? undefined : '{}',
    });
    assert.equal(resposta.status, 401, `${method} ${path}`);
    assert.deepEqual(await resposta.json(), { error: 'Não autorizado.' });
  }
});

test('token malformado retorna 401 sem expor detalhes do JWT', async () => {
  const resposta = await fetch(`${baseUrl}/api/me`, {
    headers: { Authorization: 'Bearer token-invalido' },
  });

  assert.equal(resposta.status, 401);
  assert.deepEqual(await resposta.json(), { error: 'Não autorizado.' });
});

test('rotas DEV não são registradas quando a flag está desabilitada', async () => {
  const chamadas = [
    ['POST', '/api/usuarios'],
    ['GET', '/api/usuarios/id-invalido'],
    ['PUT', '/api/usuarios/id-invalido/personagem'],
    ['GET', '/api/progresso/id-invalido'],
    ['DELETE', '/api/progresso/id-invalido'],
    ['GET', '/api/dev/presenca'],
  ];

  for (const [method, path] of chamadas) {
    const resposta = await fetch(`${baseUrl}${path}`, { method });
    assert.equal(resposta.status, 404, `${method} ${path}`);
    assert.deepEqual(await resposta.json(), { error: 'Endpoint não encontrado.' });
  }
});

test('rotas DEV são registradas somente quando habilitadas explicitamente', async () => {
  const devServer = criarApp({ habilitarRotasDev: true }).listen(0, '127.0.0.1');
  await new Promise((resolve, reject) => {
    devServer.once('listening', resolve);
    devServer.once('error', reject);
  });

  try {
    const url = `http://127.0.0.1:${devServer.address().port}`;
    const [usuario, progresso, presenca] = await Promise.all([
      fetch(`${url}/api/usuarios/id-invalido`),
      fetch(`${url}/api/progresso/id-invalido`),
      fetch(`${url}/api/dev/presenca`),
    ]);
    assert.equal(usuario.status, 400);
    assert.deepEqual(await usuario.json(), { error: 'ID de usuário inválido.' });
    assert.equal(progresso.status, 400);
    assert.deepEqual(await progresso.json(), { error: 'ID de usuário inválido.' });
    assert.equal(presenca.status, 200);
    assert.deepEqual(await presenca.json(), { online: 0, usuarios: [] });
  } finally {
    await new Promise((resolve, reject) =>
      devServer.close((error) => (error ? reject(error) : resolve()))
    );
  }
});
