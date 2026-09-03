# RedeLab Server

API REST e presença WebSocket em Node.js para o RedeLab Escola. O catálogo de fases e missões é público; dados pessoais, progresso, feedback e presença usam Access Tokens emitidos pelo Auth0. O MySQL deve existir previamente. A inicialização comum não altera o esquema; mudanças versionadas são aplicadas explicitamente com `npm run migrate`.

REST e WebSocket compartilham um único processo e a mesma porta. Não há senha local, autenticação própria nem alterações na Unity.

## Monitor de Turma

Com a API em execução, acesse:

```text
http://localhost:3000/monitor
```

O painel usa HTML, CSS e JavaScript puros, com Bootstrap 5 e Bootstrap Icons instalados pelo npm e servidos localmente. Ele não depende de CDN ou internet externa para carregar a interface.

Na aba **Alunos**, o filtro da tabela começa em **Todos os alunos** e também oferece **Apenas online**. Os cards permanecem globais. No filtro online, entradas usam fade/zoom de 360 ms e saídas usam fade/zoom de 320 ms, implementados no CSS local sem Animate.css. O último aluno só é removido depois da animação; então aparece o estado “Nenhum aluno online”.

A aba **Feedback** lista os envios permanentes em ordem decrescente de data, com jogador, tipo, comentário, versão do jogo e horário. É possível filtrar por tipo e por jogador. `GET /api/monitor/feedback` aceita os parâmetros opcionais `tipo` e `id_usuario`; as consultas usam parâmetros do driver e não interpolam esses valores no SQL.

`GET /api/monitor/alunos` monta uma visão agregada somente de leitura com nome, presença, `ultimo_acesso`, fase, progresso e missões. A presença vem da mesma instância `Presenca` alimentada pelo `/ws` autenticado do jogo. Como o banco não possui uma coluna de fase atual, o monitor considera a primeira fase ativa ainda incompleta; quando todas estão concluídas, mostra a última fase ativa. Fases e missões sem registros ativos não são inventadas.

O navegador do monitor se conecta a `ws://HOST/ws/monitor` (ou `wss://` em HTTPS). Esse canal envia apenas `monitor_ready` e `monitor_update`, não recebe comandos, não entra na contagem de presença e não altera o protocolo `/ws` do jogo. Os eventos identificam `id_usuario` e usam os motivos `usuario_online`, `usuario_offline`, `cadastro`, `missao_concluida`, `missao_revertida`, `feedback_criado` e `progresso_reset`. Conclusões, reversões, feedbacks, exclusões de progresso e `DELETE /api/me/novo-jogo` publicam a notificação somente depois da gravação/commit. A página reconsulta somente a visão afetada, atualiza apenas a área de dados — nunca recarrega a página — e usa o ID para animar a linha afetada. Eventos próximos são consolidados por um debounce curto de 120 ms. A reconexão usa atrasos de 1, 2, 5 e 10 segundos, limitados em 10 segundos.

O espaço de marca do cabeçalho possui `#brandLogo` e o fallback Bootstrap atual. O caminho e os dois ajustes de classe para instalar o logo oficial estão documentados em `public/monitor/img/README.md`.

### Segurança temporária do monitor

Nesta primeira versão, conforme o requisito da sala de aula, `/monitor`, `GET /api/monitor/alunos`, `GET /api/monitor/feedback` e `/ws/monitor` abrem sem login. Isso expõe nomes, progresso e comentários a quem puder alcançar o servidor, portanto restrinja a aplicação à rede confiável da escola até implementar a autorização administrativa.

A próxima etapa de produção deve proteger **no servidor** tanto o endpoint REST quanto o handshake do canal do monitor:

```text
/monitor -> Auth0 -> Google -> validação do token no Node -> verificação de administrador -> painel autorizado
```

Uma lista de emails no JavaScript não é controle de acesso. A verificação administrativa deve usar identidade validada e uma política no backend. O `/ws` do jogo permanece inalterado, exigindo a mensagem inicial `auth` com Access Token válido.

## Requisitos

- Node.js 18 ou superior e npm
- MySQL 8 em execução
- Banco `redelab_escola` com as tabelas existentes
- Tenant, aplicação e API configurados no Auth0

## Instalação

```bash
cd redelab-server
npm ci
cp .env.example .env
npm run migrate
```

A autenticação usa o SDK oficial `express-oauth2-jwt-bearer`; o WebSocket usa a biblioteca leve `ws`. Não existe Client Secret do Google nesta API.

## Variáveis de ambiente

```dotenv
DB_HOST=localhost
DB_PORT=3306
DB_USER=root
DB_PASSWORD=sua_senha
DB_NAME=redelab_escola
PORT=3000
HTTPS_ENABLED=false
HTTPS_KEY_PATH=
HTTPS_CERT_PATH=
CORS_ORIGIN=http://localhost:8081

AUTH0_DOMAIN=seu-tenant.REGIAO.auth0.com
AUTH0_AUDIENCE=https://identificador-da-api-redelab

ENABLE_DEV_ROUTES=false

UNITY_WEBGL_PORT=8081
UNITY_WEBGL_HOST=127.0.0.1
UNITY_WEBGL_HTTPS_ENABLED=false
UNITY_WEBGL_HTTPS_KEY_PATH=
UNITY_WEBGL_HTTPS_CERT_PATH=
UNITY_WEBGL_BUILD_DIR=
API_PUBLIC_URL=http://localhost:3000
WS_PUBLIC_URL=ws://localhost:3000
```

- `AUTH0_DOMAIN`: domínio do tenant, sem caminhos. A API monta o issuer HTTPS e valida o claim `iss`.
- `AUTH0_AUDIENCE`: Identifier da API cadastrada no Auth0; deve coincidir com o claim `aud`.
- `ENABLE_DEV_ROUTES`: habilita rotas antigas que aceitam `id_usuario`. Use `true` somente no computador de desenvolvimento e `false` em qualquer servidor publicado.
- `CORS_ORIGIN`: aceita `*` ou uma lista separada por vírgulas. Em produção na escola, autorize o jogo e o monitor com `https://redelab.escola:8081,https://redelab.escola:3001`; o monitor tem origem própria na porta 3001.
- `HTTPS_ENABLED`: quando `true`, a API lê `HTTPS_KEY_PATH` e `HTTPS_CERT_PATH` e publica REST e WebSocket no mesmo servidor TLS. Arquivo ausente encerra a inicialização; não há fallback para HTTP.
- `UNITY_WEBGL_*`: configura porta, interface, TLS e caminho da build servida por `npm run unity-webgl-auth`.
- `API_PUBLIC_URL` e `WS_PUBLIC_URL`: origens inseridas na CSP da página WebGL. Use respectivamente `https://...` e `wss://...` quando o servidor WebGL estiver em HTTPS.

A aplicação falha ao iniciar se `AUTH0_DOMAIN` ou `AUTH0_AUDIENCE` estiverem vazios. Isso evita publicar uma API aparentemente protegida sem validação configurada.

### HTTPS opcional sem proxy

Em desenvolvimento, os defaults continuam sendo `http://localhost:3000`, `ws://localhost:3000/ws` e WebGL em `http://127.0.0.1:8081`.

Para TLS direto no Node, configure a API e o servidor WebGL separadamente. Os dois podem apontar para o mesmo par de certificado/chave, desde que o certificado seja válido para o host usado pelos navegadores:

```dotenv
PORT=3001
HTTPS_ENABLED=true
HTTPS_KEY_PATH=/etc/redelab/ssl/redelab.key
HTTPS_CERT_PATH=/etc/redelab/ssl/redelab.crt

UNITY_WEBGL_PORT=8081
UNITY_WEBGL_HOST=0.0.0.0
UNITY_WEBGL_HTTPS_ENABLED=true
UNITY_WEBGL_HTTPS_KEY_PATH=/etc/redelab/ssl/redelab.key
UNITY_WEBGL_HTTPS_CERT_PATH=/etc/redelab/ssl/redelab.crt
UNITY_WEBGL_BUILD_DIR=/var/www/redelab-webgl

API_PUBLIC_URL=https://redelab.escola:3001
WS_PUBLIC_URL=wss://redelab.escola:3001
CORS_ORIGIN=https://redelab.escola:8081,https://redelab.escola:3001
```

`UNITY_WEBGL_BUILD_DIR` é opcional no layout normal do repositório. Quando backend e build forem copiados para diretórios independentes no servidor, defina-o com o caminho absoluto que contém `index.html`.

### Certificado HTTPS da escola

A produção usa um certificado autoassinado com CN/SAN `redelab.escola` e SAN IP `192.168.1.59`. Os arquivos privados da instalação ficam somente no servidor:

- certificado público: `/etc/redelab/ssl/redelab.crt`;
- chave privada: `/etc/redelab/ssl/redelab.key`.

O navegador pode mostrar “Não seguro” até que o certificado público seja instalado em **Usuário Atual > Autoridades de Certificação Raiz Confiáveis**. O servidor WebGL publica `GET /certificado`, com instruções para Windows, e entrega `GET /downloads/redelab.crt` a partir de `UNITY_WEBGL_BUILD_DIR/downloads/redelab.crt`.

Somente o certificado público pode ser copiado para a pasta WebGL. Nunca copie, distribua ou versione `redelab.key`, nem exponha `/etc/redelab/ssl` como diretório estático. Como a atualização da Build WebGL substitui o diretório publicado, repita a cópia segura do `.crt` depois de cada atualização da build conforme o `DEPLOY_CHECKLIST.md`.

### DNS do laboratório

O servidor Ubuntu foi configurado manualmente para responder por `redelab.escola` em `192.168.1.59:53`, usando `/etc/systemd/resolved.conf.d/redelab-dns.conf`. A consulta local do nome RedeLab foi validada, mas a resolução de domínios externos pelo servidor ainda está **pendente de validação no ambiente da escola** com `resolvectl query google.com` e `dig @192.168.1.59 google.com`. Somente depois será decidido se a infraestrutura continuará com `systemd-resolved` ou usará `dnsmasq`; o projeto não altera DHCP ou DNS.

## Execução e testes

```bash
npm start
npm run dev
npm run migrate
npm test
npm run unity-webgl-auth
```

`npm run migrate` cria a tabela de controle `schema_migration` e aplica, em ordem, os arquivos ainda não registrados de `database/migrations/`. O comando usa um lock nomeado do MySQL para impedir duas execuções concorrentes e pode ser repetido sem reaplicar migrações concluídas. Execute-o antes de iniciar uma versão que contenha novas migrações.

O teste automatizado usa o MySQL configurado no `.env`, verifica as rotas públicas, confirma `401` para rotas protegidas sem token, testa o protocolo WebSocket, timeout, múltiplas abas, heartbeat e comprova que as rotas DEV aparecem somente quando habilitadas. Um token Auth0 real não é fabricado pelos testes.

## Página temporária de teste Auth0

A pasta `test-client/` contém uma ferramenta de desenvolvimento em HTML, CSS e JavaScript puro para validar o login Google antes da integração com a Unity. Ela usa o SDK oficial `@auth0/auth0-spa-js`, servido localmente pelo próprio Express, sem CDN e sem implementação manual de OAuth/PKCE.

Antes de executar, abra `test-client/config.js` e substitua somente:

```javascript
clientId: 'AUTH0_CLIENT_ID_AQUI'
```

pelo **Client ID** público da aplicação SPA `RedeLab WebGL`. Não use nem procure Client Secret para esse cliente navegador. O domínio `dev-ldgwwvi01va0qxzx.us.auth0.com`, a audience `https://api.redelab.local` e a API local já estão declarados no mesmo arquivo.

No Auth0 Dashboard, adicione `http://localhost:8080` sem remover valores existentes em:

- Allowed Callback URLs
- Allowed Logout URLs
- Allowed Web Origins
- Allowed Origins (CORS), quando esse campo estiver disponível para a aplicação

Inicie a API em um terminal e a página em outro:

```powershell
npm start
npm run auth-test
```

Acesse `http://localhost:8080`. A página permite login Google, callback Auth0, obtenção de Access Token para a audience da API, sync do usuário, consulta de `/api/me`, seleção dos personagens 1 e 2, consulta/conclusão/exclusão de progresso, conexão WebSocket e logout.

O Access Token fica somente no cache em memória mantido pelo SDK, nunca é salvo em arquivo, `localStorage` ou `sessionStorage`, e não é mostrado por completo na página ou no console. As ações da API começam desabilitadas e só são liberadas depois que `isAuthenticated()` e `getTokenSilently()` confirmam a sessão.

Esta ferramenta é temporária. O servidor usa a porta fixa `8080`, escuta apenas em `127.0.0.1`, desabilita cache e envia uma Content Security Policy restrita ao tenant Auth0 e à API local. Encerre-o com `Ctrl+C` após os testes.

### Autorização da SPA para a API

Além das URLs da aplicação, a SPA precisa estar autorizada a solicitar tokens para a audience `https://api.redelab.local`. Se o callback mostrar `Client is not authorized to access resource server`, abra no Dashboard:

1. **Applications > APIs** e selecione a API RedeLab com Identifier `https://api.redelab.local`.
2. Em **Settings > Application Access Policy**, confira a política de **User-Delegated Access**.
3. Se estiver configurada como autorização por aplicação/client grant, abra **Application Access**, localize `RedeLab WebGL`, escolha **Edit** e conceda **User-Delegated Access**.
4. Salve e tente o login novamente. Não é necessário conceder Client Access/Machine-to-Machine para este fluxo SPA.

O console mostra eventos de diagnóstico prefixados por `[RedeLab Auth Test]`: carregamento de `app.js`, disponibilidade do SDK, criação do cliente, registro/clique do botão, callback e início de `loginWithRedirect`. Erros aparecem no console e na área **Resposta**, sem Access Token ou stack trace.

## Como a autenticação funciona

O cliente envia:

```http
Authorization: Bearer ACCESS_TOKEN
```

O middleware valida o JWT com as chaves públicas do tenant Auth0, incluindo issuer, audience, assinatura RS256 e expiração. Erros de token são reduzidos a uma resposta `401` genérica; o token e o header `Authorization` não são registrados.

O vínculo interno é sempre:

```text
Access Token.sub -> usuario.auth0_id -> usuario.id_usuario
```

Email não é chave de autenticação. Nenhuma rota de produção aceita `id_usuario` do cliente para decidir qual jogador consultar ou alterar.

## WebSocket e presença

O WebSocket usa o mesmo servidor HTTP e a mesma porta da API:

```text
Desenvolvimento: ws://localhost:3000/ws
Produção:        wss://DOMINIO/ws
```

O navegador não permite definir livremente um header `Authorization` no handshake WebSocket. Por isso, imediatamente após abrir a conexão, o cliente envia a única mensagem aceita antes da autenticação:

```json
{"type":"auth","accessToken":"ACCESS_TOKEN"}
```

O token não vai na URL, não é registrado e não é persistido. O servidor o entrega internamente ao mesmo middleware `express-oauth2-jwt-bearer` usado pelo REST, que valida assinatura RS256 pelas chaves JWKS do Auth0, issuer, audience e expiração. Depois lê `sub`, consulta `usuario.auth0_id` e associa somente o `id_usuario` encontrado no banco. Valores de identidade enviados pelo cliente são ignorados.

Sucesso:

```json
{"type":"auth_ok","id_usuario":3}
```

Falha:

```json
{"type":"auth_error","error":"invalid_token"}
```

O socket tem 8 segundos para autenticar. Mensagens binárias, mensagens comuns antes da autenticação, autenticações concorrentes e payloads acima de 16 KiB são rejeitados. Nesta etapa, mensagens posteriores de gameplay não são implementadas.

A presença fica somente na memória do processo:

```text
Map<id_usuario, Set<WebSocket>>
```

Assim, duas abas do mesmo usuário geram duas conexões, mas apenas um usuário online. Ele só fica offline quando a última conexão fecha. Reiniciar o processo zera a presença, intencionalmente; nenhum campo `online`, heartbeat ou última atividade é gravado no MySQL.

O servidor envia `ping` a cada 30 segundos. Clientes WebSocket respondem `pong` automaticamente. Se uma conexão não responder até a verificação seguinte, ela é terminada e removida da presença, cobrindo perda de Wi-Fi, navegador encerrado e desligamento abrupto.

### Teste manual do WebSocket

1. Execute `npm start` e `npm run auth-test` em terminais separados.
2. Abra `http://localhost:8080`, entre com Google e sincronize o usuário.
3. Clique em **Conectar WebSocket**; o status deve mudar para **Autenticado**.
4. Com `ENABLE_DEV_ROUTES=true`, consulte `http://localhost:3000/api/dev/presenca`.
5. Abra uma segunda aba e conecte: `conexoes` deve mudar para `2`.
6. Desconecte/feche cada aba e confirme a redução para `1` e depois a remoção do usuário.

### Primeiro acesso e perfil

Chame `POST /api/auth/sync` depois do login. Se o `sub` já estiver cadastrado, a API atualiza somente `ultimo_acesso`. No primeiro acesso, ela consulta `https://AUTH0_DOMAIN/userinfo` com o Access Token validado, compara o `sub` retornado e usa os campos `name` e `email` fornecidos pelo Auth0.

O cliente deve solicitar o Access Token para `AUTH0_AUDIENCE` com os escopos:

```text
openid profile email
```

Access Tokens de uma API customizada não carregam nome/email por padrão. Por isso a API não lê esses valores do body e não presume que estejam no token. O endpoint `/userinfo` é a solução OIDC usada nesta versão. Como alternativa futura, uma Auth0 Post Login Action pode adicionar claims próprios com namespace controlado, mas isso exige configuração explícita e não foi presumido no código.

Se o perfil não tiver nome/email, o sync retorna `400` com instrução sobre os escopos. Se o mesmo email já pertencer a outro `auth0_id`, retorna `409`; a API não vincula contas automaticamente por email.

Exemplo:

```powershell
$headers = @{ Authorization = "Bearer $accessToken" }

Invoke-RestMethod -Method Post `
  -Uri http://localhost:3000/api/auth/sync `
  -Headers $headers

Invoke-RestMethod `
  -Uri http://localhost:3000/api/progresso/me `
  -Headers $headers
```

## Endpoints públicos

| Método | Endpoint | Finalidade |
| --- | --- | --- |
| GET | `/api/health` | Testar API e conexão MySQL |
| GET | `/api/fases` | Listar fases |
| GET | `/api/fases/:id` | Buscar fase |
| GET | `/api/missoes` | Listar missões |
| GET | `/api/missoes/fase/:id_fase` | Listar missões da fase |
| GET | `/api/missoes/codigo/:codigo` | Buscar missão pelo código da Unity |
| GET | `/api/monitor/alunos` | Visão agregada temporariamente pública do Monitor de Turma |
| GET | `/api/monitor/feedback` | Feedbacks e jogadores para filtros do monitor temporariamente público |

## Endpoints autenticados

| Método | Endpoint | Body | Finalidade |
| --- | --- | --- | --- |
| POST | `/api/auth/sync` | vazio | Criar ou sincronizar o usuário pelo Auth0 `sub` |
| GET | `/api/me` | vazio | Retornar o usuário interno autenticado |
| PUT | `/api/me/personagem` | `{"id_personagem":1}` | Selecionar personagem do próprio usuário |
| GET | `/api/progresso/me` | vazio | Consultar o próprio progresso |
| POST | `/api/progresso/concluir` | `{"codigo_missao":"codigo"}` | Concluir missão para o próprio usuário |
| DELETE | `/api/progresso/concluir` | `{"codigo_missao":"codigo"}` | Reverter a conclusão da missão para o próprio usuário |
| DELETE | `/api/progresso/me` | vazio | Apagar apenas o próprio progresso |
| POST | `/api/feedback` | `{"tipo":"sugestao","comentario":"texto","versao_jogo":"1.0.0"}` | Registrar feedback permanente do próprio usuário |
| GET | `/api/feedback/me` | vazio | Consultar o histórico de feedback do próprio usuário |

`GET /api/me`, as rotas de progresso e as rotas de feedback retornam `404` com orientação para chamar `/api/auth/sync` quando o token é válido, mas o `sub` ainda não existe no MySQL. Concluir e reverter são idempotentes: repetir a mesma operação mantém o estado desejado sem duplicar registros nem transformar ausência em erro.

O feedback é append-only: não há rota para editar ou apagar envios. `tipo` aceita somente `sugestao`, `bug` ou `comentario`; `comentario` é obrigatório e limitado a 1000 caracteres, e `versao_jogo` é opcional e limitada a 50. A API sempre deriva o usuário do token e a data do servidor. O reset de progresso e `DELETE /api/me/novo-jogo` não acessam `feedback_usuario`, portanto o histórico permanece depois de um novo jogo.

## Rotas DEV/LEGACY

As seguintes rotas só são registradas com `ENABLE_DEV_ROUTES=true`:

| Método | Endpoint | Risco/uso |
| --- | --- | --- |
| POST | `/api/usuarios` | Criação manual temporária |
| GET | `/api/usuarios/:id_usuario` | Consulta por ID informado pelo cliente |
| PUT | `/api/usuarios/:id_usuario/personagem` | Alteração por ID informado pelo cliente |
| GET | `/api/progresso/:id_usuario` | Consulta por ID informado pelo cliente |
| DELETE | `/api/progresso/:id_usuario` | Exclusão de progresso por ID informado pelo cliente |
| GET | `/api/dev/presenca` | Contagem transitória de usuários e conexões WebSocket |

Com a flag ausente ou diferente de `true`, essas URLs respondem `404`. Em produção e no servidor da escola, configure obrigatoriamente:

```dotenv
ENABLE_DEV_ROUTES=false
```

Não existe uma rota DEV de conclusão de missão. Todo `INSERT` de progresso passa por `POST /api/progresso/concluir` e exige um Access Token válido. Assim, testes futuros da Unity com recursos como `allowIncompleteMissionForDebug` não devem chamar a rota autenticada nem gravar progresso real.

`GET /api/dev/presenca` retorna somente `id_usuario` e número de conexões; nunca email, `auth0_id` ou token. Quando `ENABLE_DEV_ROUTES=false`, a rota não é registrada e responde `404`.

## Respostas de erro

- `401`: token ausente, inválido ou expirado
- `403`: autenticado sem autorização, quando uma política de autorização for adicionada
- `404`: recurso ou usuário interno sincronizado não encontrado
- `409`: conflito de cadastro, sem associação automática por email
- `500`: falha interna sem stack trace na resposta

## Configuração manual no Auth0

Os nomes exatos dos menus podem mudar, mas as entidades e valores necessários são:

1. Crie ou escolha um tenant Auth0.
2. Crie uma aplicação adequada ao cliente WebGL, normalmente uma **Single Page Application**, usando Authorization Code Flow com PKCE. Não use Client Secret embutido no WebGL.
3. Crie uma **API** no Auth0. Escolha um Identifier placeholder estável, por exemplo `https://api.redelab.exemplo`, e copie exatamente esse valor para `AUTH0_AUDIENCE`. Mantenha assinatura RS256.
4. Configure o cliente para solicitar essa audience e os escopos `openid profile email`.
5. Em **Authentication > Social**, habilite a conexão Google e autorize a aplicação criada.
6. Enquanto não houver URL final, use placeholders/documente os valores previstos. Quando o WebGL estiver hospedado, cadastre exatamente suas Allowed Callback URLs, Allowed Logout URLs e Allowed Web Origins.
7. Para testes iniciais, as Google Development Keys fornecidas pelo Auth0 podem ser suficientes. Elas são compartilhadas, têm limitações e não são a configuração final. Antes da produção, crie credenciais OAuth próprias no Google Cloud, configure a tela de consentimento e informe Client ID/Client Secret apenas na conexão Google dentro do painel Auth0.
8. Obtenha um token real pelo fluxo do cliente e execute `POST /api/auth/sync`; depois teste `/api/me` e as rotas de progresso.

Consulte a documentação oficial sobre [`express-oauth2-jwt-bearer`](https://github.com/auth0/node-oauth2-jwt-bearer/tree/main/packages/express-oauth2-jwt-bearer), [`/userinfo`](https://auth0.com/docs/api/authentication/user-profile/get-user-info) e [Google Social Connection](https://auth0.com/docs/authenticate/identity-providers/social-identity-providers/google).

## Publicação futura no servidor da escola

1. Instalar uma versão suportada do Node.js e executar `npm ci --omit=dev` dentro de `redelab-server`. O `ws` será instalado pelo `package-lock.json`.
2. Copiar o código sem `node_modules` e sem o `.env` local.
3. Criar o `.env` do servidor com credenciais próprias do MySQL, configurações HTTPS, `AUTH0_DOMAIN`, `AUTH0_AUDIENCE`, `ENABLE_DEV_ROUTES=false`, `CORS_ORIGIN=https://redelab.escola:8081,https://redelab.escola:3001` e o caminho real de `UNITY_WEBGL_BUILD_DIR`.
4. Garantir acesso HTTPS à API e à WebGL e manter o MySQL protegido da internet pública. Uma página WebGL em HTTPS deve usar `wss://`; navegadores bloqueiam `ws://` como conteúdo misto.
5. Executar `npm run migrate`, iniciar a API com um gerenciador de processos/serviço e conferir `/api/health`.
6. Reproduzir no Auth0 as URLs reais de callback, logout e origem do WebGL.
7. Liberar no firewall somente a porta pública usada pelo HTTP/HTTPS. REST e WebSocket usam o mesmo processo e a mesma porta; não é necessária uma segunda porta para `/ws`.
8. Se houver Nginx, Apache, IIS ou outro reverse proxy, encaminhar `/ws` com HTTP/1.1 e preservar os headers `Upgrade` e `Connection`. Configurar timeouts do proxy acima do intervalo de heartbeat.
9. Testar com um Access Token real, confirmar que uma requisição REST sem token recebe `401` e que um socket sem autenticação fecha após o timeout.

Autorização administrativa do monitor, formulário de feedback dentro do jogo, mensagens entre jogadores, gameplay via socket, multiplayer, posição do jogador e histórico de sessão permanecem para etapas posteriores.
