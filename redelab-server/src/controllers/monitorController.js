const { pool } = require('../config/database');

function agruparPor(lista, campo) {
  const grupos = new Map();
  for (const item of lista) {
    const chave = Number(item[campo]);
    const grupo = grupos.get(chave) || [];
    grupo.push(item);
    grupos.set(chave, grupo);
  }
  return grupos;
}

function criarMonitorController(presenca) {
  async function listarAlunos(req, res, next) {
    try {
      const [[usuarios], [fases], [missoes], [conclusoes]] = await Promise.all([
        pool.query(
          `SELECT id_usuario, nome, ultimo_acesso
             FROM usuario
            ORDER BY nome ASC, id_usuario ASC`
        ),
        pool.query(
          `SELECT id_fase, nome
             FROM fase
            WHERE ativa = 1
            ORDER BY id_fase ASC`
        ),
        pool.query(
          `SELECT id_missao, id_fase, numero_missao, nome
             FROM missao
            WHERE ativa = 1
            ORDER BY id_fase ASC, numero_missao ASC`
        ),
        pool.query(
          `SELECT mc.id_usuario, mc.id_fase, mc.id_missao
             FROM missao_concluida mc
             JOIN fase f ON f.id_fase = mc.id_fase AND f.ativa = 1
             JOIN missao m
               ON m.id_fase = mc.id_fase
              AND m.id_missao = mc.id_missao
              AND m.ativa = 1`
        ),
      ]);

      const onlineIds = new Set(
        presenca.resumo().usuarios.map((item) => Number(item.id_usuario))
      );
      const missoesPorFase = agruparPor(missoes, 'id_fase');
      const conclusoesPorUsuario = agruparPor(conclusoes, 'id_usuario');

      const alunos = usuarios.map((usuario) => {
        const concluidas = conclusoesPorUsuario.get(Number(usuario.id_usuario)) || [];
        const idsConcluidos = new Set(concluidas.map((item) => Number(item.id_missao)));
        const concluidasPorFase = new Map();
        for (const conclusao of concluidas) {
          const idFase = Number(conclusao.id_fase);
          concluidasPorFase.set(idFase, (concluidasPorFase.get(idFase) || 0) + 1);
        }

        const fasesComMissoes = fases.filter(
          (fase) => (missoesPorFase.get(Number(fase.id_fase)) || []).length > 0
        );
        const faseAtual =
          fasesComMissoes.find((fase) => {
            const idFase = Number(fase.id_fase);
            return (concluidasPorFase.get(idFase) || 0) < missoesPorFase.get(idFase).length;
          }) || fasesComMissoes.at(-1) || null;

        const missoesDaFase = faseAtual
          ? missoesPorFase.get(Number(faseAtual.id_fase)) || []
          : [];
        const quantidadeConcluida = missoesDaFase.reduce(
          (total, missao) => total + (idsConcluidos.has(Number(missao.id_missao)) ? 1 : 0),
          0
        );
        const totalMissoes = missoesDaFase.length;
        const percentual = totalMissoes
          ? Math.round((quantidadeConcluida / totalMissoes) * 100)
          : 0;

        return {
          id_usuario: Number(usuario.id_usuario),
          nome: usuario.nome,
          online: onlineIds.has(Number(usuario.id_usuario)),
          ultimo_acesso: usuario.ultimo_acesso,
          fase_atual: faseAtual
            ? { id_fase: Number(faseAtual.id_fase), nome: faseAtual.nome }
            : null,
          missoes_concluidas: quantidadeConcluida,
          total_missoes: totalMissoes,
          percentual,
          total_missoes_concluidas: concluidas.length,
          missoes: missoesDaFase.map((missao) => ({
            id_missao: Number(missao.id_missao),
            numero_missao: Number(missao.numero_missao),
            nome: missao.nome,
            concluida: idsConcluidos.has(Number(missao.id_missao)),
          })),
        };
      });

      alunos.sort(
        (a, b) =>
          Number(b.online) - Number(a.online) ||
          a.nome.localeCompare(b.nome, 'pt-BR', { sensitivity: 'base' })
      );

      const online = alunos.filter((aluno) => aluno.online).length;
      res.set('Cache-Control', 'no-store');
      return res.json({
        atualizado_em: new Date().toISOString(),
        resumo: {
          alunos_cadastrados: alunos.length,
          online,
          offline: alunos.length - online,
          missoes_concluidas: conclusoes.length,
        },
        alunos,
      });
    } catch (error) {
      return next(error);
    }
  }

  async function listarFeedbacks(req, res, next) {
    const tipo = typeof req.query.tipo === 'string' ? req.query.tipo.trim().toLowerCase() : '';
    const tiposPermitidos = new Set(['sugestao', 'bug', 'comentario']);
    if (tipo && !tiposPermitidos.has(tipo)) {
      return res.status(400).json({ error: 'Tipo de feedback inválido.' });
    }

    const idUsuarioInformado = req.query.id_usuario;
    const idUsuario = idUsuarioInformado === undefined ? null : Number(idUsuarioInformado);
    if (idUsuario !== null && (!Number.isInteger(idUsuario) || idUsuario <= 0)) {
      return res.status(400).json({ error: 'ID de usuário inválido.' });
    }

    const filtros = [];
    const parametros = [];
    if (tipo) {
      filtros.push('fu.tipo = ?');
      parametros.push(tipo);
    }
    if (idUsuario !== null) {
      filtros.push('fu.id_usuario = ?');
      parametros.push(idUsuario);
    }
    const where = filtros.length ? `WHERE ${filtros.join(' AND ')}` : '';

    try {
      const [[feedbacks], [jogadores]] = await Promise.all([
        pool.query(
          `SELECT fu.id_feedback, fu.id_usuario, u.nome AS jogador, fu.tipo,
                  fu.comentario, fu.versao_jogo, fu.data_envio
             FROM feedback_usuario fu
             JOIN usuario u ON u.id_usuario = fu.id_usuario
             ${where}
            ORDER BY fu.data_envio DESC, fu.id_feedback DESC`,
          parametros
        ),
        pool.query(
          `SELECT DISTINCT u.id_usuario, u.nome
             FROM feedback_usuario fu
             JOIN usuario u ON u.id_usuario = fu.id_usuario
            ORDER BY u.nome ASC, u.id_usuario ASC`
        ),
      ]);

      res.set('Cache-Control', 'no-store');
      return res.json({
        atualizado_em: new Date().toISOString(),
        filtros: { tipo: tipo || null, id_usuario: idUsuario },
        jogadores: jogadores.map((item) => ({
          id_usuario: Number(item.id_usuario),
          nome: item.nome,
        })),
        feedbacks: feedbacks.map((item) => ({
          ...item,
          id_feedback: Number(item.id_feedback),
          id_usuario: Number(item.id_usuario),
        })),
      });
    } catch (error) {
      return next(error);
    }
  }

  return { listarAlunos, listarFeedbacks };
}

module.exports = { criarMonitorController };
