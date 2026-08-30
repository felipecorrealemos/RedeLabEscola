const { pool } = require('../config/database');
const { notificarMonitor } = require('../services/monitorUpdates');

function parseIdUsuario(valor) {
  const idUsuario = Number(valor);
  return Number.isInteger(idUsuario) && idUsuario > 0 ? idUsuario : null;
}

async function buscarProgresso(req, res, next) {
  // Proteger com Auth0 e permitir apenas o próprio progresso em uma etapa posterior.
  const idUsuario = parseIdUsuario(req.params.id_usuario);
  if (!idUsuario) {
    return res.status(400).json({ error: 'ID de usuário inválido.' });
  }

  try {
    const [usuarios] = await pool.query('SELECT id_usuario FROM usuario WHERE id_usuario = ?', [
      idUsuario,
    ]);
    if (usuarios.length === 0) {
      return res.status(404).json({ error: 'Usuário não encontrado.' });
    }

    const [missoes] = await pool.query(
      `SELECT mc.id_fase, mc.id_missao, m.codigo, m.numero_missao,
              m.nome, f.nome AS fase_nome, mc.data_conclusao
         FROM missao_concluida mc
         JOIN missao m
           ON m.id_fase = mc.id_fase AND m.id_missao = mc.id_missao
         JOIN fase f ON f.id_fase = mc.id_fase
        WHERE mc.id_usuario = ?
        ORDER BY mc.id_fase ASC, m.numero_missao ASC`,
      [idUsuario]
    );

    return res.json({ id_usuario: idUsuario, missoes_concluidas: missoes });
  } catch (error) {
    return next(error);
  }
}

async function concluirMissao(req, res, next) {
  const idUsuario = req.usuario.id_usuario;
  const codigoMissao =
    typeof req.body.codigo_missao === 'string' ? req.body.codigo_missao.trim() : '';

  if (!codigoMissao) {
    return res.status(400).json({ error: 'Informe codigo_missao válido.' });
  }

  try {
    const [[usuarios], [missoes]] = await Promise.all([
      pool.query('SELECT id_usuario FROM usuario WHERE id_usuario = ?', [idUsuario]),
      pool.query('SELECT id_missao, id_fase, codigo FROM missao WHERE codigo = ?', [
        codigoMissao,
      ]),
    ]);

    if (usuarios.length === 0) {
      return res.status(404).json({ error: 'Usuário não encontrado.' });
    }
    if (missoes.length === 0) {
      return res.status(404).json({ error: 'Missão não encontrada.' });
    }

    const missao = missoes[0];
    const parametrosConclusao = [idUsuario, missao.id_fase, missao.id_missao];
    const [conclusoes] = await pool.query(
      `SELECT 1 FROM missao_concluida
        WHERE id_usuario = ? AND id_fase = ? AND id_missao = ?`,
      parametrosConclusao
    );

    let alreadyCompleted = conclusoes.length > 0;
    if (!alreadyCompleted) {
      try {
        await pool.execute(
          `INSERT INTO missao_concluida (id_usuario, id_fase, id_missao)
           VALUES (?, ?, ?)`,
          parametrosConclusao
        );
      } catch (error) {
        // Outra chamada simultânea pode inserir entre o SELECT e o INSERT.
        if (error.code === 'ER_DUP_ENTRY') {
          alreadyCompleted = true;
        } else {
          throw error;
        }
      }
    }

    if (!alreadyCompleted) {
      notificarMonitor('missao_concluida', idUsuario);
    }

    return res.status(alreadyCompleted ? 200 : 201).json({
      success: true,
      alreadyCompleted,
      id_usuario: idUsuario,
      id_fase: missao.id_fase,
      id_missao: missao.id_missao,
      codigo_missao: missao.codigo,
    });
  } catch (error) {
    return next(error);
  }
}

async function buscarMeuProgresso(req, res, next) {
  const idUsuario = req.usuario.id_usuario;

  try {
    const [missoes] = await pool.query(
      `SELECT mc.id_fase, mc.id_missao, m.codigo, m.numero_missao,
              m.nome, f.nome AS fase_nome, mc.data_conclusao
         FROM missao_concluida mc
         JOIN missao m
           ON m.id_fase = mc.id_fase AND m.id_missao = mc.id_missao
         JOIN fase f ON f.id_fase = mc.id_fase
        WHERE mc.id_usuario = ?
        ORDER BY mc.id_fase ASC, m.numero_missao ASC`,
      [idUsuario]
    );

    return res.json({ id_usuario: idUsuario, missoes_concluidas: missoes });
  } catch (error) {
    return next(error);
  }
}

async function apagarMeuProgresso(req, res, next) {
  const idUsuario = req.usuario.id_usuario;

  try {
    const [resultado] = await pool.execute(
      'DELETE FROM missao_concluida WHERE id_usuario = ?',
      [idUsuario]
    );
    if (resultado.affectedRows > 0) {
      notificarMonitor('progresso_reset', idUsuario);
    }
    return res.json({
      success: true,
      id_usuario: idUsuario,
      registrosRemovidos: resultado.affectedRows,
    });
  } catch (error) {
    return next(error);
  }
}

async function apagarProgresso(req, res, next) {
  // Proteger com Auth0 e permitir apenas o próprio progresso em uma etapa posterior.
  const idUsuario = parseIdUsuario(req.params.id_usuario);
  if (!idUsuario) {
    return res.status(400).json({ error: 'ID de usuário inválido.' });
  }

  try {
    const [usuarios] = await pool.query('SELECT id_usuario FROM usuario WHERE id_usuario = ?', [
      idUsuario,
    ]);
    if (usuarios.length === 0) {
      return res.status(404).json({ error: 'Usuário não encontrado.' });
    }

    const [resultado] = await pool.execute(
      'DELETE FROM missao_concluida WHERE id_usuario = ?',
      [idUsuario]
    );
    if (resultado.affectedRows > 0) {
      notificarMonitor('progresso_reset', idUsuario);
    }
    return res.json({
      success: true,
      id_usuario: idUsuario,
      registrosRemovidos: resultado.affectedRows,
    });
  } catch (error) {
    return next(error);
  }
}

module.exports = {
  buscarProgresso,
  concluirMissao,
  apagarProgresso,
  buscarMeuProgresso,
  apagarMeuProgresso,
};
