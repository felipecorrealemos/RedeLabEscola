const express = require('express');
const { criarMonitorController } = require('../controllers/monitorController');

function criarMonitorRoutes(presenca) {
  const router = express.Router();
  const { listarAlunos, listarFeedbacks } = criarMonitorController(presenca);

  // Temporariamente público: a próxima etapa deve adicionar Auth0 e autorização
  // administrativa no servidor, nunca apenas uma verificação no navegador.
  router.get('/alunos', listarAlunos);
  router.get('/feedback', listarFeedbacks);

  return router;
}

module.exports = { criarMonitorRoutes };
