function normalizarDominioAuth0(valor) {
  const dominio = String(valor || '').trim();
  if (!dominio) {
    throw new Error('AUTH0_DOMAIN não foi configurado.');
  }

  const url = new URL(/^https?:\/\//i.test(dominio) ? dominio : `https://${dominio}`);
  if (url.protocol !== 'https:' || url.pathname !== '/' || url.search || url.hash) {
    throw new Error('AUTH0_DOMAIN deve conter apenas o domínio HTTPS do tenant Auth0.');
  }

  return url.origin;
}

function carregarConfiguracaoAuth0() {
  const issuerBaseURL = normalizarDominioAuth0(process.env.AUTH0_DOMAIN);
  const audience = String(process.env.AUTH0_AUDIENCE || '').trim();

  if (!audience) {
    throw new Error('AUTH0_AUDIENCE não foi configurado.');
  }

  return { issuerBaseURL, audience };
}

module.exports = carregarConfiguracaoAuth0();
