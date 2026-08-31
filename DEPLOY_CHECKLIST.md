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
# Em outro serviço/processo:
npm run unity-webgl-auth
```

No servidor, preencha `HTTPS_*`, `UNITY_WEBGL_HTTPS_*`, `UNITY_WEBGL_BUILD_DIR`, `API_PUBLIC_URL`, `WS_PUBLIC_URL` e `CORS_ORIGIN` com os caminhos e origens reais antes de iniciar os processos.
