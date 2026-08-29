const express = require('express');
const { listarFases, buscarFasePorId } = require('../controllers/faseController');

const router = express.Router();

router.get('/', listarFases);
router.get('/:id', buscarFasePorId);

module.exports = router;
