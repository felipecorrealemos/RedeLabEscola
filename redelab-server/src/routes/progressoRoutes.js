const express = require('express');
const {
  concluirMissao,
  buscarMeuProgresso,
  apagarMeuProgresso,
} = require('../controllers/progressoController');
const { validarAccessToken } = require('../middleware/auth');
const { carregarUsuarioAutenticado } = require('../middleware/usuarioAutenticado');

const router = express.Router();
const protegerUsuario = [validarAccessToken, carregarUsuarioAutenticado];

router.get('/me', protegerUsuario, buscarMeuProgresso);
router.post('/concluir', protegerUsuario, concluirMissao);
router.delete('/me', protegerUsuario, apagarMeuProgresso);

module.exports = router;
