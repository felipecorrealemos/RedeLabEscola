const express = require('express');
const {
  listarMissoes,
  listarMissoesPorFase,
  buscarMissaoPorCodigo,
} = require('../controllers/missaoController');

const router = express.Router();

router.get('/', listarMissoes);
router.get('/fase/:id_fase', listarMissoesPorFase);
router.get('/codigo/:codigo', buscarMissaoPorCodigo);

module.exports = router;
