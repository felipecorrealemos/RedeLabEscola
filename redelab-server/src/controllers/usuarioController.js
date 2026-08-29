const { pool } = require('../config/database');

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const CAMPOS_USUARIO =
  'id_usuario, auth0_id, nome, email, id_personagem, data_cadastro, ultimo_acesso';

async function criarUsuario(req, res, next) {
  // Temporário para desenvolvimento. Proteger com Auth0 em uma etapa posterior.
  const auth0Id = typeof req.body.auth0_id === 'string' ? req.body.auth0_id.trim() : '';
  const nome = typeof req.body.nome === 'string' ? req.body.nome.trim() : '';
  const email = typeof req.body.email === 'string' ? req.body.email.trim() : '';

  if (
    !auth0Id ||
    auth0Id.length > 255 ||
    !nome ||
    nome.length > 150 ||
    !EMAIL_PATTERN.test(email) ||
    email.length > 255
  ) {
    return res.status(400).json({
      error: 'Informe auth0_id, nome e um email válido.',
    });
  }

  try {
    const [resultado] = await pool.execute(
      'INSERT INTO usuario (auth0_id, nome, email) VALUES (?, ?, ?)',
      [auth0Id, nome, email]
    );
    const [usuarios] = await pool.query(
      `SELECT ${CAMPOS_USUARIO} FROM usuario WHERE id_usuario = ?`,
      [resultado.insertId]
    );
    return res.status(201).json(usuarios[0]);
  } catch (error) {
    if (error.code === 'ER_DUP_ENTRY') {
      return res.status(409).json({ error: 'auth0_id ou email já cadastrado.' });
    }
    return next(error);
  }
}

async function buscarUsuarioPorId(req, res, next) {
  const idUsuario = Number(req.params.id_usuario);
  if (!Number.isInteger(idUsuario) || idUsuario <= 0) {
    return res.status(400).json({ error: 'ID de usuário inválido.' });
  }

  try {
    const [usuarios] = await pool.query(
      `SELECT ${CAMPOS_USUARIO} FROM usuario WHERE id_usuario = ?`,
      [idUsuario]
    );
    if (usuarios.length === 0) {
      return res.status(404).json({ error: 'Usuário não encontrado.' });
    }
    return res.json(usuarios[0]);
  } catch (error) {
    return next(error);
  }
}

async function selecionarPersonagem(req, res, next) {
  // Proteger com Auth0 e impedir alteração de outro usuário em uma etapa posterior.
  const idUsuario = Number(req.params.id_usuario);
  const idPersonagem = Number(req.body.id_personagem);
  if (!Number.isInteger(idUsuario) || idUsuario <= 0 ||
      !Number.isInteger(idPersonagem) || idPersonagem <= 0) {
    return res.status(400).json({ error: 'IDs de usuário e personagem devem ser válidos.' });
  }

  try {
    const [[usuarios], [personagens]] = await Promise.all([
      pool.query('SELECT id_usuario FROM usuario WHERE id_usuario = ?', [idUsuario]),
      pool.query('SELECT id_personagem FROM personagem WHERE id_personagem = ?', [idPersonagem]),
    ]);

    if (usuarios.length === 0) {
      return res.status(404).json({ error: 'Usuário não encontrado.' });
    }
    if (personagens.length === 0) {
      return res.status(404).json({ error: 'Personagem não encontrado.' });
    }

    await pool.execute('UPDATE usuario SET id_personagem = ? WHERE id_usuario = ?', [
      idPersonagem,
      idUsuario,
    ]);
    return res.json({ success: true, id_usuario: idUsuario, id_personagem: idPersonagem });
  } catch (error) {
    return next(error);
  }
}

module.exports = { criarUsuario, buscarUsuarioPorId, selecionarPersonagem };
