require('dotenv').config({ quiet: true });

const fs = require('node:fs/promises');
const path = require('node:path');
const { pool } = require('../src/config/database');

const migrationsDirectory = path.join(__dirname, '..', 'database', 'migrations');
const lockName = 'redelab_escola_migrations';

async function executarMigrations() {
  const connection = await pool.getConnection();
  let lockObtido = false;
  try {
    const [[lock]] = await connection.query('SELECT GET_LOCK(?, 30) AS obtido', [lockName]);
    lockObtido = Number(lock.obtido) === 1;
    if (!lockObtido) {
      throw new Error('Não foi possível obter o lock das migrations.');
    }

    await connection.query(
      `CREATE TABLE IF NOT EXISTS schema_migration (
         id_migration varchar(255) NOT NULL,
         aplicada_em datetime(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
         PRIMARY KEY (id_migration)
       ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci`
    );

    const arquivos = (await fs.readdir(migrationsDirectory))
      .filter((arquivo) => arquivo.endsWith('.sql'))
      .sort((a, b) => a.localeCompare(b));

    for (const arquivo of arquivos) {
      const [aplicadas] = await connection.query(
        'SELECT 1 FROM schema_migration WHERE id_migration = ?',
        [arquivo]
      );
      if (aplicadas.length > 0) {
        console.log(`Migration já aplicada: ${arquivo}`);
        continue;
      }

      const sql = (await fs.readFile(path.join(migrationsDirectory, arquivo), 'utf8')).trim();
      if (!sql) {
        throw new Error(`Migration vazia: ${arquivo}`);
      }
      await connection.query(sql);
      await connection.execute(
        'INSERT INTO schema_migration (id_migration) VALUES (?)',
        [arquivo]
      );
      console.log(`Migration aplicada: ${arquivo}`);
    }
  } finally {
    if (lockObtido) {
      await connection.query('SELECT RELEASE_LOCK(?)', [lockName]);
    }
    connection.release();
  }
}

if (require.main === module) {
  executarMigrations()
    .catch((error) => {
      console.error('Falha ao aplicar migrations:', error.message);
      process.exitCode = 1;
    })
    .finally(() => pool.end());
}

module.exports = { executarMigrations, migrationsDirectory };
