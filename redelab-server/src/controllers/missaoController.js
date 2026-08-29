const { pool } = require('../config/database');

const CAMPOS_MISSAO =
  'id_missao, id_fase, codigo, numero_missao, nome, descricao, ativa';

async function listarMissoes(req, res, next) {
  try {
    const [missoes] = await pool.query(
      `SELECT ${CAMPOS_MISSAO} FROM missao ORDER BY id_fase ASC, numero_missao ASC`
    );
    res.json(missoes);
  } catch (error) {
    next(error);
  }
}

async function listarMissoesPorFase(req, res, next) {
  const idFase = Number(req.params.id_fase);
  if (!Number.isInteger(idFase) || idFase <= 0) {
    return res.status(400).json({ error: 'ID de fase inválido.' });
  }

  try {
    const [fases] = await pool.query('SELECT id_fase FROM fase WHERE id_fase = ?', [idFase]);
    if (fases.length === 0) {
      return res.status(404).json({ error: 'Fase não encontrada.' });
    }

    const [missoes] = await pool.query(
      `SELECT ${CAMPOS_MISSAO} FROM missao WHERE id_fase = ? ORDER BY numero_missao ASC`,
      [idFase]
    );
    return res.json(missoes);
  } catch (error) {
    return next(error);
  }
}

async function buscarMissaoPorCodigo(req, res, next) {
  const codigo = String(req.params.codigo || '').trim();
  if (!codigo) {
    return res.status(400).json({ error: 'Código da missão inválido.' });
  }

  try {
    const [missoes] = await pool.query(
      `SELECT ${CAMPOS_MISSAO} FROM missao WHERE codigo = ?`,
      [codigo]
    );

    if (missoes.length === 0) {
      return res.status(404).json({ error: 'Missão não encontrada.' });
    }

    return res.json(missoes[0]);
  } catch (error) {
    return next(error);
  }
}

module.exports = { listarMissoes, listarMissoesPorFase, buscarMissaoPorCodigo };
