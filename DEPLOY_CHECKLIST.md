# Checklist de implantação no Ubuntu

## Antes de sair do computador de desenvolvimento

- [ ] Gerar e testar uma nova `Build_WebGL/` configurada com a URL real do servidor. A build atual contém `http://localhost:3000` e não serve para clientes em outros computadores.
- [ ] Confirmar no Auth0 as URLs de callback, logout, origem Web e CORS usadas na escola.
- [ ] Confirmar que a origem servida aos navegadores coincide exatamente com a origem cadastrada no Auth0; para uso real, planejar HTTPS em vez de `localhost` ou HTTP improvisado.
- [ ] Levar `redelab-server/`, incluindo `package.json`, `package-lock.json`, `src/`, `public/` e `database/`.
- [ ] Levar a pasta `Build_WebGL/` completa, preferencialmente em um ZIP no pendrive para preservar todos os arquivos.
- [ ] Levar `redelab-server/.env.example`, mas nunca o `.env` real.
- [ ] Não levar `Library/`, `Temp/`, `Logs/`, `obj/`, `UserSettings/` nem `node_modules/`.

## Requisitos no Ubuntu

- Node.js 18 ou superior e npm.
- MySQL 8 local.
- Um servidor HTTP estático configurado para enviar os arquivos `.gz` da Unity com `Content-Encoding: gzip` e os tipos MIME corretos.
- Acesso à internet para Auth0 durante o login.
- Unity não é necessária e nenhuma build será gerada no Ubuntu.

## Instalação manual

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
```

O script `npm run unity-webgl-auth` é uma ferramenta local de desenvolvimento: usa porta e origens fixas e escuta apenas em `127.0.0.1`. Não o trate como o servidor WebGL definitivo da escola.
