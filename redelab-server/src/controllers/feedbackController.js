const { pool } = require('../config/database');
const { notificarMonitor } = require('../services/monitorUpdates');

const TIPOS_FEEDBACK = new Set(['sugestao', 'bug', 'comentario']);
const TAMANHO_MAXIMO_COMENTARIO = 1000;
const TAMANHO_MAXIMO_VERSAO = 50;

function validarFeedback(body = {}) {
  const tipo = typeof body.tipo === 'string' ? body.tipo.trim().toLowerCase() : '';
  const comentario = typeof body.comentario === 'string' ? body.comentario.trim() : '';
  const versaoJogo =
    typeof body.versao_jogo === 'string' ? body.versao_jogo.trim() : '';

  if (!TIPOS_FEEDBACK.has(tipo)) {
    return { error: 'Tipo de feedback inválido.' };
  }
  if (!comentario) {
    return { error: 'O comentário é obrigatório.' };
  }
  if (comentario.length > TAMANHO_MAXIMO_COMENTARIO) {
    return {
      error: `O comentário deve ter no máximo ${TAMANHO_MAXIMO_COMENTARIO} caracteres.`,
    };
  }
  if (versaoJogo.length > TAMANHO_MAXIMO_VERSAO) {
    return {
      error: `A versão do jogo deve ter no máximo ${TAMANHO_MAXIMO_VERSAO} caracteres.`,
    };
  }

  return { tipo, comentario, versaoJogo: versaoJogo || null };
}

async function criarFeedback(req, res, next) {
  const validacao = validarFeedback(req.body);
  if (validacao.error) {
    return res.status(400).json({ error: validacao.error });
  }

  const idUsuario = req.usuario.id_usuario;
  try {
    const [resultado] = await pool.execute(
      `INSERT INTO feedback_usuario (id_usuario, tipo, comentario, versao_jogo)
       VALUES (?, ?, ?, ?)`,
      [idUsuario, validacao.tipo, validacao.comentario, validacao.versaoJogo]
    );
    const [feedbacks] = await pool.query(
      `SELECT id_feedback, tipo, comentario, versao_jogo, data_envio
         FROM feedback_usuario
        WHERE id_feedback = ? AND id_usuario = ?`,
      [resultado.insertId, idUsuario]
    );

    notificarMonitor('feedback_criado', idUsuario);
    return res.status(201).json(feedbacks[0]);
  } catch (error) {
    return next(error);
  }
}

async function listarMeusFeedbacks(req, res, next) {
  try {
    const [feedbacks] = await pool.query(
      `SELECT id_feedback, tipo, comentario, versao_jogo, data_envio
         FROM feedback_usuario
        WHERE id_usuario = ?
        ORDER BY data_envio DESC, id_feedback DESC`,
      [req.usuario.id_usuario]
    );
    res.set('Cache-Control', 'no-store');
    return res.json({ id_usuario: req.usuario.id_usuario, feedbacks });
  } catch (error) {
    return next(error);
  }
}

module.exports = {
  TAMANHO_MAXIMO_COMENTARIO,
  TAMANHO_MAXIMO_VERSAO,
  TIPOS_FEEDBACK,
  criarFeedback,
  listarMeusFeedbacks,
  validarFeedback,
};
