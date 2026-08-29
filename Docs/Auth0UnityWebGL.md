# Auth0 no Unity WebGL

Esta etapa integra somente login Google, sessao em memoria e as chamadas autenticadas
`POST /api/auth/sync` e `GET /api/me`. Ela nao implementa progresso, personagem, WebSocket
ou modo offline.

## Configuracao usada

- Auth0 Domain: `dev-ldgwwvi01va0qxzx.us.auth0.com`
- Auth0 Client ID: `Ai8Q8DjlvFJqmcwkcedu5Spdu7XGkrmd`
- Audience: `https://api.redelab.local`
- API local: `http://localhost:3000`
- pagina WebGL local: `http://localhost:8081`

O Client ID acima usa a letra `l` minuscula depois de `Dj`. A variante com o numero `1`
foi rejeitada pelo Auth0 com HTTP 400 durante a validacao anterior.

Na aplicacao SPA **RedeLab WebGL** do Auth0 Dashboard, configure exatamente:

- Allowed Callback URLs: `http://localhost:8081`
- Allowed Logout URLs: `http://localhost:8081`
- Allowed Web Origins: `http://localhost:8081`
- Allowed Origins (CORS): `http://localhost:8081`

Habilite tambem a conexao Google para essa aplicacao. A API Auth0 de identifier
`https://api.redelab.local` precisa autorizar o cliente SPA. Nao existe Client Secret no
Unity e o fluxo nao deve receber um.

Se `CORS_ORIGIN` da API nao estiver como `*` no ambiente local, inclua
`http://localhost:8081` na lista.

## Como executar

> Nao use `File > Build And Run` para testar autenticacao. Esse comando inicia o
> `SimpleWebServer` temporario do Unity em uma porta dinamica, que nao corresponde ao
> callback local cadastrado no Auth0. O projeto bloqueia esse caminho para builds WebGL.

1. Inicie MySQL e a API em `redelab-server` com `npm start`.
2. Gere o build pelo menu Unity `RedeLab > Build > WebGL Auth Test`, ou por linha de comando:

   ```powershell
   & 'C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe' `
     -batchmode -quit -projectPath 'D:\Projetos unity\RedeLabEscola' `
     -executeMethod RedeLabWebGLAuthBuild.Build -logFile 'webgl-auth-build.log'
   ```

3. Em outro terminal, dentro de `redelab-server`, execute `npm run unity-webgl-auth`.
4. Abra `http://localhost:8081`. Nao abra o `index.html` diretamente com `file://`.

O servidor de teste entrega o SDK oficial `@auth0/auth0-spa-js` instalado localmente e
tambem aplica os headers corretos aos artefatos `.gz` do Unity. Nenhum CDN e usado.

## Arquitetura e seguranca

`RedeLabAuthManager` e criado antes da primeira cena e preservado com
`DontDestroyOnLoad`. O Access Token e o perfil ficam apenas em campos de memoria; nao sao
gravados em `PlayerPrefs`, arquivo, log ou cena. O callback passa por
`Assets/Plugins/WebGL/RedeLabAuth0.jslib`, que usa o SDK oficial com cache
`localstorage`. O Unity nao grava nem recupera o token por conta propria: toda a
persistencia e renovacao pertencem ao Auth0 SPA SDK.

Depois de obter o token, o manager chama primeiro `/api/auth/sync` e depois `/api/me` por
meio de `RedeLabApiClient`. O menu so libera os botoes de inicio, carregar e sala quando o
perfil interno foi carregado com sucesso. No Unity Editor, o OAuth e a API ficam
desativados e o menu informa que o teste precisa de um build WebGL.

O logout da conta limpa imediatamente os campos em memoria e pede ao Auth0 que retorne
para a URL atual. Recarregar a pagina preserva o cache gerenciado pelo SDK: ao iniciar,
ele verifica a sessao, obtem o token silenciosamente e o Unity sincroniza novamente o
usuario. Nenhum token e salvo em PlayerPrefs, arquivos ou codigo proprio.

## Duracao do Access Token da API

A duracao nao e configurada no Unity nem no Node. No Auth0 Dashboard, abra:

1. `Applications > APIs`;
2. selecione a API cujo Identifier e `https://api.redelab.local`;
3. na aba `Settings`, localize `Token Settings > Maximum Access Token Lifetime (Seconds)`;
4. informe `10800` e salve.

O RedeLab usa Authorization Code com PKCE pelo Auth0 SPA SDK. Portanto, a configuracao
principal aplicavel e `Maximum Access Token Lifetime`, e nao apenas o campo de fluxo
Implicit/Hybrid. Se o Dashboard exigir ambos os valores, mantenha tambem
`Implicit / Hybrid Flow Access Token Lifetime (Seconds)` em no maximo `10800`.

O backend continua validando assinatura RS256, issuer, audience e expiracao. Alterar o
tempo no Dashboard nao exige e nao deve causar flexibilizacao do middleware Node.

Quando uma API devolve 401 durante o gameplay, o Unity mantem a missao na fila local e o
bridge tenta `getTokenSilently` sem popup. Se a renovacao silenciosa nao for possivel, uma
mensagem discreta e serializada na cena informa que a sessao precisa ser renovada.

## Sair do jogo e sair da aplicacao

Nas cenas jogaveis, `Sair do jogo` retorna ao `MainMenu` sem logout, sem apagar personagem
e sem apagar progresso. O prefab editavel e `Assets/Prefabs/UI/GameplayExitUI.prefab`.

Somente o botao `Sair` do MainMenu tenta encerrar a aplicacao. No WebGL ele chama
`window.close()`; quando o navegador impede o fechamento, o texto serializado
`QuitFallbackMessage` orienta o usuario a fechar a aba manualmente.

## Navegacao e recarga

O login iniciado pelo menu usa `loginWithPopup` do SDK oficial. Esse fluxo continua usando
Authorization Code com PKCE, mas preserva a instancia Unity/WebAssembly e devolve o token
ao MainMenu sem trocar ou recarregar a cena. O bridge ainda processa callback por redirect
ao iniciar, tanto para compatibilidade quanto para recuperar um fluxo ja iniciado.

O logout central do Auth0 continua redirecionando o documento para o tenant e de volta a
`http://localhost:8081`. Esse reload completo e esperado, pois o navegador abandona a
pagina WebGL; a nova instancia inicia no MainMenu, mostra `Autenticando...`, verifica a
sessao silenciosamente e conclui como nao autenticada. Nao ha transicao de gameplay nesse
processo.
