const express = require('express');
const { sincronizarUsuario } = require('../controllers/authController');
const { validarAccessToken } = require('../middleware/auth');

const router = express.Router();

router.post('/sync', validarAccessToken, sincronizarUsuario);

module.exports = router;
