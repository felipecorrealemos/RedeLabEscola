const { pool } = require('../config/database');
const { obterPerfilAuth0 } = require('../services/auth0UserInfo');
const { notificarMonitor } = require('../services/monitorUpdates');

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const CAMPOS_RETORNO = 'id_usuario, nome, email, id_personagem';

async function buscarUsuarioPorSub(sub) {
  const [usuarios] = await pool.query(
    `SELECT ${CAMPOS_RETORNO} FROM usuario WHERE auth0_id = ?`,
    [sub]
  );
  return usuarios[0] || null;
}

async function registrarAcesso(idUsuario) {
  await pool.execute('UPDATE usuario SET ultimo_acesso = CURRENT_TIMESTAMP WHERE id_usuario = ?', [
    idUsuario,
  ]);
}

async function sincronizarUsuario(req, res, next) {
  const sub = req.auth.payload.sub;

  try {
    const existente = await buscarUsuarioPorSub(sub);
    if (existente) {
      await registrarAcesso(existente.id_usuario);
      return res.json({ ...existente, novo_usuario: false });
    }

    const perfil = await obterPerfilAuth0(req.auth.token);
    const nome = typeof perfil.name === 'string' ? perfil.name.trim() : '';
    const email = typeof perfil.email === 'string' ? perfil.email.trim() : '';

    if (perfil.sub !== sub) {
      return res.status(401).json({ error: 'Não autorizado.' });
    }

    if (!nome || nome.length > 150 || !EMAIL_PATTERN.test(email) || email.length > 255) {
      return res.status(400).json({
        error:
          'O perfil Auth0 não contém nome e email válidos. Solicite os escopos openid profile email.',
      });
    }

    try {
      const [resultado] = await pool.execute(
        `INSERT INTO usuario (auth0_id, nome, email, ultimo_acesso)
         VALUES (?, ?, ?, CURRENT_TIMESTAMP)`,
        [sub, nome, email]
      );
      const [usuarios] = await pool.query(
        `SELECT ${CAMPOS_RETORNO} FROM usuario WHERE id_usuario = ?`,
        [resultado.insertId]
      );
      notificarMonitor('cadastro', usuarios[0].id_usuario);
      return res.status(201).json({ ...usuarios[0], novo_usuario: true });
    } catch (error) {
      if (error.code !== 'ER_DUP_ENTRY') {
        throw error;
      }

      // Uma sincronização simultânea do mesmo sub pode vencer o INSERT.
      const criadoEmParalelo = await buscarUsuarioPorSub(sub);
      if (criadoEmParalelo) {
        await registrarAcesso(criadoEmParalelo.id_usuario);
        return res.json({ ...criadoEmParalelo, novo_usuario: false });
      }

      return res.status(409).json({
        error: 'O email já pertence a outro usuário interno.',
      });
    }
  } catch (error) {
    return next(error);
  }
}

module.exports = { sincronizarUsuario };
