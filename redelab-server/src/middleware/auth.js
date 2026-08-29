const { auth } = require('express-oauth2-jwt-bearer');
const { issuerBaseURL, audience } = require('../config/auth0');

const validarAccessToken = auth({
  issuerBaseURL,
  audience,
  tokenSigningAlg: 'RS256',
});

async function validarAccessTokenSocket(accessToken) {
  const headers = {
    authorization: `Bearer ${accessToken}`,
    host: 'localhost',
  };
  const requisicao = {
    app: { get: () => undefined },
    body: {},
    headers,
    method: 'GET',
    originalUrl: '/ws',
    protocol: 'http',
    query: {},
    socket: { remoteAddress: '127.0.0.1' },
    get(nome) {
      return headers[String(nome).toLowerCase()];
    },
    is: () => false,
  };

  await new Promise((resolve, reject) => {
    Promise.resolve(
      validarAccessToken(requisicao, {}, (error) => (error ? reject(error) : resolve()))
    ).catch(reject);
  });

  return requisicao.auth.payload;
}

function tratarErroAutenticacao(error, req, res, next) {
  const status = error.statusCode || error.status;

  if (status === 401) {
    return res.status(401).json({ error: 'Não autorizado.' });
  }

  if (status === 403) {
    return res.status(403).json({ error: 'Acesso proibido.' });
  }

  return next(error);
}

module.exports = { validarAccessToken, validarAccessTokenSocket, tratarErroAutenticacao };
