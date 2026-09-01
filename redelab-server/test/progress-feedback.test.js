const { after, test } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

process.env.AUTH0_DOMAIN ||= 'tenant-de-teste.auth0.com';
process.env.AUTH0_AUDIENCE ||= 'https://api.redelab.test';

const { pool } = require('../src/config/database');
const {
  buscarMeuProgresso,
  concluirMissao,
  desfazerMissao,
} = require('../src/controllers/progressoController');
const {
  criarFeedback,
  listarMeusFeedbacks,
  validarFeedback,
} = require('../src/controllers/feedbackController');
const { reiniciarMeuJogo } = require('../src/controllers/meController');
const { criarMonitorController } = require('../src/controllers/monitorController');
const { Presenca } = require('../src/websocket/presenca');

after(async () => {
  await pool.end();
});

function criarResposta() {
  return {
    statusCode: 200,
    headers: {},
    body: undefined,
    status(code) {
      this.statusCode = code;
      return this;
    },
    set(name, value) {
      this.headers[String(name).toLowerCase()] = value;
      return this;
    },
    json(value) {
      this.body = value;
      return this;
    },
  };
}

async function executar(handler, req) {
  const res = criarResposta();
  let nextError = null;
  await handler(req, res, (error) => {
    nextError = error;
  });
  if (nextError) throw nextError;
  return res;
}

test('concluir, desfazer e reler missão são idempotentes e recalculam o monitor', async () => {
  const originalQuery = pool.query;
  const originalExecute = pool.execute;
  const completed = new Set();
  const mission = {
    id_missao: 1,
    id_fase: 1,
    codigo: 'sala1_colocar_gabinete',
    numero_missao: 1,
    nome: 'Colocar gabinete',
  };

  pool.query = async (sql, params = []) => {
    if (sql.includes('SELECT id_usuario FROM usuario')) return [[{ id_usuario: 7 }]];
    if (sql.includes('FROM missao WHERE codigo')) return [[mission]];
    if (sql.includes('SELECT 1 FROM missao_concluida')) {
      return [[completed.has(params.join(':')) ? { found: 1 } : undefined].filter(Boolean)];
    }
    if (sql.includes('FROM missao_concluida mc') && sql.includes('mc.data_conclusao')) {
      return [[...completed].length ? [{ ...mission, data_conclusao: new Date() }] : []];
    }
    if (sql.includes('SELECT id_usuario, nome, ultimo_acesso')) {
      return [[{ id_usuario: 7, nome: 'Aluno', ultimo_acesso: null }]];
    }
    if (sql.includes('FROM fase') && sql.includes('WHERE ativa = 1')) {
      return [[{ id_fase: 1, nome: 'Escritório' }]];
    }
    if (sql.includes('FROM missao') && sql.includes('WHERE ativa = 1')) return [[mission]];
    if (sql.includes('SELECT mc.id_usuario')) {
      return [[...completed].length ? [{ id_usuario: 7, id_fase: 1, id_missao: 1 }] : []];
    }
    throw new Error(`Query inesperada no teste: ${sql}`);
  };
  pool.execute = async (sql, params) => {
    const key = params.join(':');
    if (sql.includes('INSERT INTO missao_concluida')) {
      const existed = completed.has(key);
      completed.add(key);
      return [{ affectedRows: existed ? 0 : 1 }];
    }
    if (sql.includes('DELETE FROM missao_concluida')) {
      const existed = completed.delete(key);
      return [{ affectedRows: existed ? 1 : 0 }];
    }
    throw new Error(`Execute inesperado no teste: ${sql}`);
  };

  try {
    const req = { usuario: { id_usuario: 7 }, body: { codigo_missao: mission.codigo } };
    const primeiraConclusao = await executar(concluirMissao, req);
    assert.equal(primeiraConclusao.statusCode, 201);
    assert.equal(primeiraConclusao.body.alreadyCompleted, false);

    const conclusaoRepetida = await executar(concluirMissao, req);
    assert.equal(conclusaoRepetida.statusCode, 200);
    assert.equal(conclusaoRepetida.body.alreadyCompleted, true);

    const progressoConcluido = await executar(buscarMeuProgresso, {
      usuario: { id_usuario: 7 },
    });
    assert.equal(progressoConcluido.body.missoes_concluidas.length, 1);

    const reversao = await executar(desfazerMissao, req);
    assert.equal(reversao.statusCode, 200);
    assert.equal(reversao.body.alreadyPending, false);

    const reversaoRepetida = await executar(desfazerMissao, req);
    assert.equal(reversaoRepetida.body.alreadyPending, true);

    const progressoReaberto = await executar(buscarMeuProgresso, {
      usuario: { id_usuario: 7 },
    });
    assert.deepEqual(progressoReaberto.body.missoes_concluidas, []);

    const monitor = criarMonitorController(new Presenca());
    const respostaMonitor = await executar(monitor.listarAlunos, { query: {} });
    assert.equal(respostaMonitor.body.alunos[0].percentual, 0);
    assert.equal(respostaMonitor.body.alunos[0].missoes[0].concluida, false);
  } finally {
    pool.query = originalQuery;
    pool.execute = originalExecute;
  }
});

test('validação de feedback rejeita tipo, vazio e excesso sem alterar conteúdo válido', () => {
  assert.match(validarFeedback({ tipo: 'outro', comentario: 'x' }).error, /Tipo/);
  assert.match(validarFeedback({ tipo: 'bug', comentario: '   ' }).error, /obrigatório/);
  assert.match(
    validarFeedback({ tipo: 'bug', comentario: 'x'.repeat(1001) }).error,
    /1000/
  );
  assert.match(
    validarFeedback({ tipo: 'bug', comentario: 'x', versao_jogo: 'v'.repeat(51) }).error,
    /50/
  );
  assert.deepEqual(
    validarFeedback({ tipo: ' Sugestao ', comentario: '  Ajuda  ', versao_jogo: ' 1.2.3 ' }),
    { tipo: 'sugestao', comentario: 'Ajuda', versaoJogo: '1.2.3' }
  );
});

test('jogador cria vários feedbacks e /me retorna somente os seus em ordem decrescente', async () => {
  const originalQuery = pool.query;
  const originalExecute = pool.execute;
  const feedbacks = [];
  let nextId = 1;

  pool.execute = async (sql, params) => {
    assert.match(sql, /INSERT INTO feedback_usuario/);
    const item = {
      id_feedback: nextId++,
      id_usuario: params[0],
      tipo: params[1],
      comentario: params[2],
      versao_jogo: params[3],
      data_envio: new Date(Date.now() + nextId * 1000),
    };
    feedbacks.push(item);
    return [{ insertId: item.id_feedback, affectedRows: 1 }];
  };
  pool.query = async (sql, params) => {
    if (sql.includes('WHERE id_feedback')) {
      return [[feedbacks.find(
        (item) => item.id_feedback === params[0] && item.id_usuario === params[1]
      )]];
    }
    if (sql.includes('WHERE id_usuario')) {
      return [[...feedbacks]
        .filter((item) => item.id_usuario === params[0])
        .sort((a, b) => b.data_envio - a.data_envio || b.id_feedback - a.id_feedback)];
    }
    throw new Error(`Query inesperada no teste: ${sql}`);
  };

  try {
    for (const comentario of ['Primeiro', 'Segundo']) {
      const resposta = await executar(criarFeedback, {
        usuario: { id_usuario: 7 },
        body: { tipo: 'comentario', comentario, versao_jogo: '1.0.0' },
      });
      assert.equal(resposta.statusCode, 201);
    }
    feedbacks.push({
      id_feedback: nextId++,
      id_usuario: 8,
      tipo: 'bug',
      comentario: 'De outro jogador',
      versao_jogo: null,
      data_envio: new Date(),
    });

    const historico = await executar(listarMeusFeedbacks, {
      usuario: { id_usuario: 7 },
    });
    assert.deepEqual(historico.body.feedbacks.map((item) => item.comentario), [
      'Segundo',
      'Primeiro',
    ]);
    assert.ok(historico.body.feedbacks.every((item) => item.id_usuario === 7));
  } finally {
    pool.query = originalQuery;
    pool.execute = originalExecute;
  }
});

test('reset do jogo não consulta nem remove feedback_usuario', async () => {
  const originalGetConnection = pool.getConnection;
  const statements = [];
  const feedbacks = [{ id_feedback: 1, id_usuario: 7 }];
  const connection = {
    beginTransaction: async () => {},
    commit: async () => {},
    rollback: async () => {},
    release: () => {},
    execute: async (sql) => {
      statements.push(sql);
      return [{ affectedRows: sql.includes('missao_concluida') ? 3 : 1 }];
    },
  };
  pool.getConnection = async () => connection;
  try {
    const resposta = await executar(reiniciarMeuJogo, { usuario: { id_usuario: 7 } });
    assert.equal(resposta.body.registrosRemovidos, 3);
    assert.equal(feedbacks.length, 1);
    assert.ok(statements.some((sql) => sql.includes('missao_concluida')));
    assert.ok(statements.some((sql) => sql.includes('UPDATE usuario')));
    assert.ok(statements.every((sql) => !sql.includes('feedback_usuario')));
  } finally {
    pool.getConnection = originalGetConnection;
  }
});

test('feedback é append-only para o jogador e migration é incremental e não destrutiva', () => {
  const routes = fs.readFileSync(
    path.join(__dirname, '..', 'src', 'routes', 'feedbackRoutes.js'),
    'utf8'
  );
  assert.match(routes, /router\.post\('\/'/);
  assert.match(routes, /router\.get\('\/me'/);
  assert.doesNotMatch(routes, /router\.(put|patch|delete)/i);

  const migration = fs.readFileSync(
    path.join(
      __dirname,
      '..',
      'database',
      'migrations',
      '20260901_001_create_feedback_usuario.sql'
    ),
    'utf8'
  );
  assert.match(migration, /CREATE TABLE IF NOT EXISTS `feedback_usuario`/);
  assert.match(migration, /ON DELETE RESTRICT/);
  assert.doesNotMatch(migration, /DROP|TRUNCATE|DELETE FROM|INSERT INTO `usuario`/i);
});
