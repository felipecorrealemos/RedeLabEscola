const express = require('express');
const {
  obterMe,
  selecionarMeuPersonagem,
  removerMeuPersonagem,
  reiniciarMeuJogo,
} = require('../controllers/meController');
const { validarAccessToken } = require('../middleware/auth');
const { carregarUsuarioAutenticado } = require('../middleware/usuarioAutenticado');

const router = express.Router();

router.use(validarAccessToken, carregarUsuarioAutenticado);
router.get('/', obterMe);
router.put('/personagem', selecionarMeuPersonagem);
router.delete('/personagem', removerMeuPersonagem);
router.delete('/novo-jogo', reiniciarMeuJogo);

module.exports = router;
