const express = require('express');
const {
  criarFeedback,
  listarMeusFeedbacks,
} = require('../controllers/feedbackController');
const { validarAccessToken } = require('../middleware/auth');
const { carregarUsuarioAutenticado } = require('../middleware/usuarioAutenticado');

const router = express.Router();

router.use(validarAccessToken, carregarUsuarioAutenticado);
router.post('/', criarFeedback);
router.get('/me', listarMeusFeedbacks);

module.exports = router;
