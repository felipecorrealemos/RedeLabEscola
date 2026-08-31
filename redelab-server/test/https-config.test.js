const assert = require('node:assert/strict');
const path = require('node:path');
const test = require('node:test');
const { carregarOpcoesHttps, configuracaoAtivada } = require('../src/config/https');

test('HTTPS permanece desabilitado por padrão sem tentar ler certificados', () => {
  let leituras = 0;
  const options = carregarOpcoesHttps({
    enabled: undefined,
    readFile: () => {
      leituras += 1;
    },
  });

  assert.equal(options, null);
  assert.equal(leituras, 0);
  assert.equal(configuracaoAtivada('false'), false);
  assert.equal(configuracaoAtivada('TRUE'), true);
});

test('HTTPS habilitado carrega chave e certificado dos caminhos configurados', () => {
  const keyPath = path.resolve('certificados', 'redelab.key');
  const certPath = path.resolve('certificados', 'redelab.crt');
  const requested = [];
  const options = carregarOpcoesHttps({
    enabled: 'true',
    keyPath,
    certPath,
    label: 'HTTPS de teste',
    readFile(filePath) {
      requested.push(filePath);
      return Buffer.from(filePath === keyPath ? 'chave-teste' : 'certificado-teste');
    },
  });

  assert.deepEqual(requested, [keyPath, certPath]);
  assert.equal(options.key.toString(), 'chave-teste');
  assert.equal(options.cert.toString(), 'certificado-teste');
});

test('HTTPS habilitado falha claramente sem caminhos ou com arquivo ilegível', () => {
  assert.throws(
    () => carregarOpcoesHttps({ enabled: 'true', label: 'HTTPS da API' }),
    /HTTPS da API habilitado.*caminhos da chave e do certificado/
  );

  assert.throws(
    () =>
      carregarOpcoesHttps({
        enabled: 'true',
        keyPath: 'ausente.key',
        certPath: 'ausente.crt',
        label: 'HTTPS da API',
        readFile() {
          throw new Error('arquivo ausente');
        },
      }),
    /HTTPS da API habilitado.*não foi possível ler a chave.*arquivo ausente/
  );
});
