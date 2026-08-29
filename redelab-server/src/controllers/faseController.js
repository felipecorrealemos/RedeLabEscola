const { pool } = require('../config/database');

async function listarFases(req, res, next) {
  try {
    const [fases] = await pool.query(
      'SELECT id_fase, nome, ativa FROM fase ORDER BY id_fase ASC'
    );
    res.json(fases);
  } catch (error) {
    next(error);
  }
}

async function buscarFasePorId(req, res, next) {
  const idFase = Number(req.params.id);
  if (!Number.isInteger(idFase) || idFase <= 0) {
    return res.status(400).json({ error: 'ID de fase inválido.' });
  }

  try {
    const [fases] = await pool.query(
      'SELECT id_fase, nome, ativa FROM fase WHERE id_fase = ?',
      [idFase]
    );

    if (fases.length === 0) {
      return res.status(404).json({ error: 'Fase não encontrada.' });
    }

    return res.json(fases[0]);
  } catch (error) {
    return next(error);
  }
}

module.exports = { listarFases, buscarFasePorId };
