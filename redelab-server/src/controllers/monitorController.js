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

  return { listarAlunos };
}

module.exports = { criarMonitorController };
