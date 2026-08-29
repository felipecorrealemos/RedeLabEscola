const { pool } = require('../config/database');

function obterMe(req, res) {
  const { id_usuario, nome, email, id_personagem } = req.usuario;
  return res.json({ id_usuario, nome, email, id_personagem });
}

async function selecionarMeuPersonagem(req, res, next) {
  const idPersonagem = Number(req.body.id_personagem);
  if (!Number.isInteger(idPersonagem) || idPersonagem <= 0) {
    return res.status(400).json({ error: 'ID de personagem inválido.' });
  }

  try {
    const [personagens] = await pool.query(
      'SELECT id_personagem FROM personagem WHERE id_personagem = ?',
      [idPersonagem]
    );
    if (personagens.length === 0) {
      return res.status(404).json({ error: 'Personagem não encontrado.' });
    }

    await pool.execute('UPDATE usuario SET id_personagem = ? WHERE id_usuario = ?', [
      idPersonagem,
      req.usuario.id_usuario,
    ]);
    return res.json({
      success: true,
      id_usuario: req.usuario.id_usuario,
      id_personagem: idPersonagem,
    });
  } catch (error) {
    return next(error);
  }
}

async function removerMeuPersonagem(req, res, next) {
  try {
    await pool.execute('UPDATE usuario SET id_personagem = NULL WHERE id_usuario = ?', [
      req.usuario.id_usuario,
    ]);
    return res.json({ success: true, id_usuario: req.usuario.id_usuario });
  } catch (error) {
    return next(error);
  }
}

async function reiniciarMeuJogo(req, res, next) {
  const connection = await pool.getConnection();
  try {
    await connection.beginTransaction();
    const [resultado] = await connection.execute(
      'DELETE FROM missao_concluida WHERE id_usuario = ?',
      [req.usuario.id_usuario]
    );
    await connection.execute('UPDATE usuario SET id_personagem = NULL WHERE id_usuario = ?', [
      req.usuario.id_usuario,
    ]);
    await connection.commit();
    return res.json({
      success: true,
      id_usuario: req.usuario.id_usuario,
      registrosRemovidos: resultado.affectedRows,
    });
  } catch (error) {
    await connection.rollback();
    return next(error);
  } finally {
    connection.release();
  }
}

module.exports = {
  obterMe,
  selecionarMeuPersonagem,
  removerMeuPersonagem,
  reiniciarMeuJogo,
};
