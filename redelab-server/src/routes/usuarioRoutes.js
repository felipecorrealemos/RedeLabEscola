const express = require('express');
const {
  criarUsuario,
  buscarUsuarioPorId,
  selecionarPersonagem,
} = require('../controllers/usuarioController');

const router = express.Router();

// DEV/LEGACY: o servidor só registra este router com ENABLE_DEV_ROUTES=true.
router.post('/', criarUsuario);
router.get('/:id_usuario', buscarUsuarioPorId);
router.put('/:id_usuario/personagem', selecionarPersonagem);

module.exports = router;
