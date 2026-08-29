const express = require('express');
const { buscarProgresso, apagarProgresso } = require('../controllers/progressoController');

const router = express.Router();

// DEV/LEGACY: estas rotas confiam no id_usuario da URL.
router.get('/:id_usuario', buscarProgresso);
router.delete('/:id_usuario', apagarProgresso);

module.exports = router;
