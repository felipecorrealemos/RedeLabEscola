const { pool } = require('../config/database');

const CAMPOS_USUARIO =
  'id_usuario, nome, email, id_personagem, data_cadastro, ultimo_acesso';

async function carregarUsuarioAutenticado(req, res, next) {
  const sub = req.auth && req.auth.payload && req.auth.payload.sub;
  if (typeof sub !== 'string' || !sub.trim()) {
    return res.status(401).json({ error: 'Não autorizado.' });
  }

  try {
    const [usuarios] = await pool.query(
      `SELECT ${CAMPOS_USUARIO} FROM usuario WHERE auth0_id = ?`,
      [sub]
    );

    if (usuarios.length === 0) {
      return res.status(404).json({
        error: 'Usuário autenticado ainda não sincronizado. Use POST /api/auth/sync.',
      });
    }

    req.usuario = usuarios[0];
    return next();
  } catch (error) {
    return next(error);
  }
}

module.exports = { carregarUsuarioAutenticado };
