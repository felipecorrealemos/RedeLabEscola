const express = require('express');

function criarDevPresencaRoutes(presenca) {
  const router = express.Router();
  router.get('/presenca', (req, res) => {
    res.json(presenca.resumo());
  });
  return router;
}

module.exports = { criarDevPresencaRoutes };
