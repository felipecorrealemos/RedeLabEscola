const fs = require('node:fs');
const path = require('node:path');

function configuracaoAtivada(value) {
  return String(value || '').trim().toLowerCase() === 'true';
}

function carregarOpcoesHttps({
  enabled,
  keyPath,
  certPath,
  label = 'HTTPS',
  readFile = fs.readFileSync,
} = {}) {
  if (!configuracaoAtivada(enabled)) {
    return null;
  }

  const keyValue = String(keyPath || '').trim();
  const certValue = String(certPath || '').trim();
  if (!keyValue || !certValue) {
    throw new Error(
      `${label} habilitado, mas os caminhos da chave e do certificado não foram informados.`
    );
  }

  const resolvedKeyPath = path.resolve(keyValue);
  const resolvedCertPath = path.resolve(certValue);

  let key;
  try {
    key = readFile(resolvedKeyPath);
  } catch (error) {
    throw new Error(
      `${label} habilitado, mas não foi possível ler a chave em ${resolvedKeyPath}: ${error.message}`
    );
  }

  let cert;
  try {
    cert = readFile(resolvedCertPath);
  } catch (error) {
    throw new Error(
      `${label} habilitado, mas não foi possível ler o certificado em ${resolvedCertPath}: ${error.message}`
    );
  }

  return { key, cert };
}

module.exports = { carregarOpcoesHttps, configuracaoAtivada };
