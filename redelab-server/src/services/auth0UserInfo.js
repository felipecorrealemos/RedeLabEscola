const { issuerBaseURL } = require('../config/auth0');

async function obterPerfilAuth0(accessToken) {
  let resposta;

  try {
    resposta = await fetch(`${issuerBaseURL}/userinfo`, {
      headers: { Authorization: `Bearer ${accessToken}` },
      signal: AbortSignal.timeout(5000),
    });
  } catch (error) {
    const falha = new Error('Não foi possível consultar o perfil no Auth0.');
    falha.cause = error;
    throw falha;
  }

  if (!resposta.ok) {
    const falha = new Error(`Auth0 UserInfo respondeu com status ${resposta.status}.`);
    falha.code = 'AUTH0_USERINFO_ERROR';
    throw falha;
  }

  return resposta.json();
}

module.exports = { obterPerfilAuth0 };
