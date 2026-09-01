# Checklist de implantação no Ubuntu

## Antes de sair do computador de desenvolvimento

- [ ] Gerar e testar uma nova `Build_WebGL/` depois da resolução dinâmica da API e substituir todos os arquivos da build anterior.
- [ ] Confirmar no Auth0 as URLs de callback, logout, origem Web e CORS usadas na escola.
- [ ] Confirmar que a origem servida aos navegadores coincide exatamente com a origem cadastrada no Auth0; para uso real, planejar HTTPS em vez de `localhost` ou HTTP improvisado.
- [ ] Levar `redelab-server/`, incluindo `package.json`, `package-lock.json`, `src/`, `public/` e `database/`.
- [ ] Levar a pasta `Build_WebGL/` completa, preferencialmente em um ZIP no pendrive para preservar todos os arquivos.
- [ ] Levar `redelab-server/.env.example`, mas nunca o `.env` real.
- [ ] Não levar `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` nem `node_modules/`.

## Requisitos no Ubuntu

- Node.js 18 ou superior e npm.
- MySQL 8 local.
- Certificado e chave legíveis pelo usuário que executará o Node, válidos para o host acessado pelos navegadores.
- O script `npm run unity-webgl-auth` configurado para enviar os arquivos `.gz` com `Content-Encoding: gzip` e os tipos MIME corretos.
- Acesso à internet para Auth0 durante o login.
- Unity não é necessária e nenhuma build será gerada no Ubuntu.

## Primeira instalação manual

A primeira instalação não é responsabilidade do script de atualização. Prepare os diretórios, banco, `.env`, certificados e serviços systemd manualmente:

```bash
cd redelab-server
npm ci
cp .env.example .env
# Preencher .env com os valores do servidor e manter ENABLE_DEV_ROUTES=false.

mysql -u root -p < database/01_schema.sql
mysql -u root -p < database/02_seed_personagens.sql
mysql -u root -p < database/03_seed_fases.sql
mysql -u root -p < database/04_seed_missoes.sql

npm test
npm start
# Em outro serviço/processo:
npm run unity-webgl-auth
```

No servidor, preencha `HTTPS_*`, `UNITY_WEBGL_HTTPS_*`, `UNITY_WEBGL_BUILD_DIR`, `API_PUBLIC_URL`, `WS_PUBLIC_URL` e `CORS_ORIGIN` com os caminhos e origens reais antes de iniciar os processos.

## Atualizações depois da primeira instalação

O script `scripts/deploy-school.sh` atualiza o clone, backend, migrations e Build WebGL de forma seletiva. Os defaults correspondem à instalação da escola:

```text
REPO_DIR=/var/www/RedeLabEscola
BACKEND_DIR=/var/www/redelab-server
WEBGL_DIR=/var/www/redelab-webgl
BACKUP_DIR=/var/backups/redelab
API_SERVICE=redelab
WEBGL_SERVICE=redelab-webgl
DB_NAME=redelab_escola
```

Todos podem ser sobrescritos por variáveis de ambiente. `API_HEALTH_URL` e `WEBGL_HEALTH_URL` também podem ser definidos explicitamente; quando ausentes, o script deriva protocolo e porta das opções `HTTPS_ENABLED`, `PORT`, `UNITY_WEBGL_HTTPS_ENABLED` e `UNITY_WEBGL_PORT` do `.env` do backend.

Antes de uma atualização real, execute:

```bash
cd /var/www/RedeLabEscola
sudo ./scripts/deploy-school.sh --check
sudo ./scripts/deploy-school.sh
```

O arquivo deve ser versionado com modo executável (`100755`). Se o clone existente tiver perdido essa permissão, corrija uma única vez com `sudo chmod +x scripts/deploy-school.sh`.

O modo `--check` faz `git fetch` para consultar `origin/main`, mas não executa pull, cópias, `npm ci`, testes, backup, migrations ou restart. Ele mantém `HEAD` e os diretórios publicados intactos e informa:

- branch e commits local/remoto;
- arquivos que seriam atualizados;
- presença de alterações no backend, migrations e Build WebGL;
- serviços que seriam reiniciados.

O script aborta se a working tree tiver modificações ou arquivos não rastreados, se a branch não for `main` ou se o avanço não puder ser feito por fast-forward. Não usa `reset`, `clean`, pull forçado, seeds ou rollback SQL automático.

### Backend e `.env`

Quando há mudança executável em `redelab-server/`, o backend publicado permanece intacto enquanto um diretório irmão temporário, como `redelab-server.deploy.XXXXXX`, é preparado no mesmo filesystem. O código é copiado para esse staging com `rsync`, sem `--delete`, excluindo `.env`, `node_modules`, logs, cobertura e diretórios temporários. O `.env` real é copiado com seus metadados somente para o staging e comparado antes e depois dos testes.

Dentro do staging, o script executa:

```bash
npm ci
npm test
```

Se `npm ci` ou `npm test` falhar, o staging é removido, o backend publicado permanece byte a byte intacto e nenhum serviço é reiniciado. Mudanças apenas em Markdown/README/Docs não criam staging nem reiniciam serviços.

### Backup e migrations

Quando algum arquivo de `redelab-server/database/migrations/` mudou, o script cria primeiro um dump comprimido em:

```text
/var/backups/redelab/redelab_escola_AAAAMMDD_HHMMSS.sql.gz
```

As credenciais são lidas do `.env` existente e passadas ao `mysqldump` por um arquivo temporário com permissão `0600`; a senha não aparece na linha de comando nem no log. O arquivo parcial precisa ser gerado e não pode estar vazio. Somente depois de `npm ci` e `npm test` passarem no staging e o backup ser validado, o script executa `npm run migrate` a partir do próprio staging validado. Backups antigos não são removidos automaticamente.

### Promoção e recuperação do backend

Depois dos testes, backup e migrations, o script preserva o diretório publicado como `redelab-server.previous.AAAAMMDD_HHMMSS` e promove o staging com `mv` no mesmo filesystem. O proprietário e as permissões do diretório e do `.env` são copiados da publicação anterior. Se a segunda parte da promoção falhar, o diretório anterior é recolocado imediatamente.

Se o restart ou health check da API falhar e nenhuma migration tiver sido executada, o backend novo é preservado como `redelab-server.failed.AAAAMMDD_HHMMSS`, o anterior é restaurado e o script tenta reiniciá-lo. O deploy ainda termina com erro para exigir investigação.

Se `npm run migrate` tiver sido executado, não há rollback automático do código nem do banco: a versão anterior, a nova versão e o backup permanecem disponíveis e seus caminhos aparecem no erro. Essa escolha evita executar código antigo contra um esquema possivelmente novo.

### Build WebGL e serviços

Uma mudança em `Build_WebGL/` exige `index.html`, `Build/` não vazio e `TemplateData/`. A nova build é copiada para um diretório temporário ao lado do destino, validada e promovida por rename. A publicação anterior é preservada como `redelab-webgl.previous.AAAAMMDD_HHMMSS` para rollback manual.

Regras de restart:

- `redelab`: alterações executáveis no backend ou migrations;
- `redelab-webgl`: alterações na Build WebGL ou no runtime que a serve;
- mudanças em `unity-webgl-auth-server.js`, `src/config/https.js`, `package.json` ou `package-lock.json` são consideradas alterações do runtime WebGL;
- documentação isolada não reinicia serviços.

Após o restart, o script exige `systemctl is-active --quiet` e consulta:

- API: `/api/health`;
- WebGL: `/healthz`.

TLS local pode usar `curl -k` por padrão. Para exigir validação completa do certificado, execute com `HEALTH_TLS_INSECURE=false`.

### Requisitos e log

Execute o deploy real com `sudo`. São necessários:

- `git`;
- Node.js/npm;
- `rsync`;
- `mysqldump`;
- `gzip`;
- `curl`;
- `systemctl`.

Cada deploy real é registrado em `/var/log/redelab-deploy.log`, incluindo commits, categorias, backup, migrations, serviços, health checks e falhas. O modo `--check` escreve apenas no terminal.

Para validar o script fora do servidor, sem acessar `/var/www` ou systemd:

```bash
bash -n scripts/deploy-school.sh
bash -n scripts/test-deploy-school.sh
bash scripts/test-deploy-school.sh
```
