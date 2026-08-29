(() => {
  'use strict';

  const config = window.REDELAB_AUTH_CONFIG;
  const AUTH_SCOPE = 'openid profile email';
  const CLIENT_ID_PLACEHOLDER = 'AUTH0_CLIENT_ID_AQUI';
  const LOG_PREFIX = '[RedeLab Auth Test]';

  console.info(`${LOG_PREFIX} app.js carregado`);

  const elements = {
    authStatus: document.querySelector('#auth-status'),
    tokenStatus: document.querySelector('#token-status'),
    output: document.querySelector('#response-output'),
    login: document.querySelector('#login-button'),
    logout: document.querySelector('#logout-button'),
    sync: document.querySelector('#sync-button'),
    me: document.querySelector('#me-button'),
    boy: document.querySelector('#boy-button'),
    girl: document.querySelector('#girl-button'),
    progress: document.querySelector('#progress-button'),
    deleteProgress: document.querySelector('#delete-progress-button'),
    missionCode: document.querySelector('#mission-code'),
    complete: document.querySelector('#complete-button'),
    websocketStatus: document.querySelector('#websocket-status'),
    websocketConnect: document.querySelector('#websocket-connect-button'),
    websocketDisconnect: document.querySelector('#websocket-disconnect-button'),
    clear: document.querySelector('#clear-button'),
    protectedActions: document.querySelectorAll('[data-requires-auth]'),
  };

  console.info(`${LOG_PREFIX} Auth0 SDK disponível`, {
    globalAuth0: typeof window.auth0,
    createAuth0Client: typeof window.auth0?.createAuth0Client,
  });
  console.info(`${LOG_PREFIX} botão login encontrado`, Boolean(elements.login));

  let auth0Client = null;
  let authenticated = false;
  let webSocket = null;

  function setStatus(message, state) {
    elements.authStatus.textContent = message;
    elements.authStatus.dataset.state = state;
  }

  function setOutput(title, value, isError = false) {
    const payload = value && typeof value === 'object' ? value : { message: String(value) };
    elements.output.textContent = JSON.stringify({ title, error: isError, ...payload }, null, 2);
  }

  function setAuthenticatedActions(enabled) {
    elements.protectedActions.forEach((button) => {
      button.disabled = !enabled;
    });
    elements.login.disabled = enabled;
    elements.logout.disabled = !enabled;
    if (!enabled) {
      disconnectWebSocket();
    }
  }

  function setWebSocketStatus(message, state) {
    elements.websocketStatus.textContent = message;
    elements.websocketStatus.dataset.state = state;
  }

  function disconnectWebSocket() {
    const socket = webSocket;
    webSocket = null;
    if (socket && socket.readyState < WebSocket.CLOSING) {
      socket.close(1000, 'client_disconnect');
    }
    setWebSocketStatus('Desconectado', 'disconnected');
    elements.websocketDisconnect.disabled = true;
    elements.websocketConnect.disabled = !authenticated;
  }

  function getWebSocketUrl() {
    const url = new URL(config.apiBaseUrl);
    url.protocol = url.protocol === 'https:' ? 'wss:' : 'ws:';
    url.pathname = '/ws';
    url.search = '';
    url.hash = '';
    return url.toString();
  }

  function configurationIsValid() {
    return Boolean(
      config &&
        config.domain &&
        config.audience &&
        config.apiBaseUrl &&
        config.clientId &&
        config.clientId !== CLIENT_ID_PLACEHOLDER
    );
  }

  function safeError(error) {
    if (error instanceof ApiError) {
      return {
        type: 'api_error',
        status: error.status,
        message: error.message,
        response: error.payload,
      };
    }

    if (
      error instanceof TypeError &&
      /failed to fetch|fetch failed|networkerror|network request failed/i.test(error.message)
    ) {
      return { type: 'network_error', message: 'Falha de rede ao acessar Auth0 ou a API.' };
    }

    return {
      type: error && error.error ? error.error : 'application_error',
      message: error && error.message ? error.message : 'Erro inesperado.',
    };
  }

  function showError(context, error) {
    const safe = safeError(error);
    console.error(`${LOG_PREFIX} ${context}`, safe);
    setOutput(context, safe, true);
  }

  class ApiError extends Error {
    constructor(status, payload) {
      const apiMessage = payload && payload.error ? payload.error : `A API respondeu com status ${status}.`;
      super(apiMessage);
      this.name = 'ApiError';
      this.status = status;
      this.payload = payload;
    }
  }

  async function getAccessToken() {
    if (!auth0Client || !authenticated) {
      throw new Error('Faça login antes de chamar a API.');
    }

    const token = await auth0Client.getTokenSilently({
      authorizationParams: {
        audience: config.audience,
        scope: AUTH_SCOPE,
      },
    });

    if (!token) {
      throw new Error('O Auth0 não retornou um Access Token para a RedeLab API.');
    }

    elements.tokenStatus.textContent = 'Access Token válido disponível';
    return token;
  }

  async function callApi(path, options = {}) {
    const token = await getAccessToken();
    const headers = new Headers(options.headers || {});
    headers.set('Authorization', `Bearer ${token}`);
    if (options.body) {
      headers.set('Content-Type', 'application/json');
    }

    const response = await fetch(`${config.apiBaseUrl}${path}`, { ...options, headers });
    const contentType = response.headers.get('content-type') || '';
    const payload = contentType.includes('application/json')
      ? await response.json()
      : { message: await response.text() };

    if (!response.ok) {
      throw new ApiError(response.status, payload);
    }

    return payload;
  }

  async function runApiAction(title, path, options) {
    try {
      const result = await callApi(path, options);
      setOutput(title, result);
    } catch (error) {
      showError(title, error);
    }
  }

  async function updateAuthenticationState() {
    authenticated = await auth0Client.isAuthenticated();
    setAuthenticatedActions(authenticated);

    if (!authenticated) {
      setStatus('Não autenticado', 'unauthenticated');
      elements.tokenStatus.textContent = 'Token não disponível';
      console.info(`${LOG_PREFIX} usuário não autenticado`);
      return;
    }

    const user = await auth0Client.getUser();
    await getAccessToken();
    setStatus('Autenticado', 'authenticated');
    setOutput('Login Auth0 concluído', {
      sub: user && user.sub,
      nome: user && user.name,
      email: user && user.email,
      access_token: 'presente, oculto por segurança',
    });
  }

  async function initialize() {
    setAuthenticatedActions(false);
    setStatus('Não autenticado', 'unauthenticated');
    elements.tokenStatus.textContent = 'Token não disponível';

    if (!configurationIsValid()) {
      setStatus('Configuração pendente', 'error');
      elements.tokenStatus.textContent = 'Client ID não configurado';
      setOutput(
        'Configuração necessária',
        {
          message: 'Substitua AUTH0_CLIENT_ID_AQUI em test-client/config.js pelo Client ID da aplicação RedeLab WebGL.',
        },
        true
      );
      return;
    }

    try {
      if (!window.auth0 || typeof window.auth0.createAuth0Client !== 'function') {
        throw new Error(
          'O bundle browser/UMD do Auth0 SPA SDK não foi carregado. Verifique /vendor/auth0-spa-js.production.js.'
        );
      }

      console.info(`${LOG_PREFIX} criando Auth0 client`);
      auth0Client = await window.auth0.createAuth0Client({
        domain: config.domain,
        clientId: config.clientId,
        cacheLocation: 'memory',
        useRefreshTokens: false,
        authorizationParams: {
          redirect_uri: window.location.origin,
          audience: config.audience,
          scope: AUTH_SCOPE,
        },
      });
      console.info(`${LOG_PREFIX} Auth0 client criado`);

      const query = new URLSearchParams(window.location.search);
      if ((query.has('code') || query.has('error')) && query.has('state')) {
        console.info(`${LOG_PREFIX} callback Auth0 detectado`);
        try {
          await auth0Client.handleRedirectCallback();
        } finally {
          window.history.replaceState({}, document.title, window.location.pathname);
        }
      }

      await updateAuthenticationState();
    } catch (error) {
      authenticated = false;
      setAuthenticatedActions(false);
      setStatus('Falha na autenticação', 'error');
      elements.tokenStatus.textContent = 'Token não disponível';
      showError('Inicialização ou callback Auth0', error);
    }
  }

  elements.login.addEventListener('click', async () => {
    console.info(`${LOG_PREFIX} clique login recebido`);

    if (!configurationIsValid()) {
      showError('Entrar com Google', new Error('Configure o Client ID em test-client/config.js e recarregue a página.'));
      return;
    }

    if (!auth0Client) {
      showError(
        'Entrar com Google',
        new Error('O cliente Auth0 não foi inicializado. Recarregue a página e consulte o erro de inicialização.')
      );
      return;
    }

    try {
      setOutput('Entrar com Google', { message: 'Redirecionando para o Auth0...' });
      console.info(`${LOG_PREFIX} executando loginWithRedirect`);
      await auth0Client.loginWithRedirect({
        async openUrl(url) {
          // Registra somente a origem, nunca state, nonce, code_challenge ou token.
          console.info(`${LOG_PREFIX} redirecionando navegador`, new URL(url).origin);
          window.location.assign(url);
        },
        authorizationParams: {
          connection: 'google-oauth2',
          audience: config.audience,
          scope: AUTH_SCOPE,
          redirect_uri: window.location.origin,
        },
      });
    } catch (error) {
      showError('Entrar com Google', error);
    }
  });
  console.info(`${LOG_PREFIX} listener do botão login registrado`);

  elements.logout.addEventListener('click', async () => {
    try {
      disconnectWebSocket();
      await auth0Client.logout({
        logoutParams: { returnTo: window.location.origin },
      });
    } catch (error) {
      showError('Sair', error);
    }
  });

  elements.sync.addEventListener('click', () =>
    runApiAction('Sincronizar usuário', '/api/auth/sync', { method: 'POST' })
  );
  elements.me.addEventListener('click', () =>
    runApiAction('Consultar meu usuário', '/api/me')
  );
  elements.boy.addEventListener('click', () =>
    runApiAction('Selecionar Menino', '/api/me/personagem', {
      method: 'PUT',
      body: JSON.stringify({ id_personagem: 1 }),
    })
  );
  elements.girl.addEventListener('click', () =>
    runApiAction('Selecionar Menina', '/api/me/personagem', {
      method: 'PUT',
      body: JSON.stringify({ id_personagem: 2 }),
    })
  );
  elements.progress.addEventListener('click', () =>
    runApiAction('Consultar progresso', '/api/progresso/me')
  );
  elements.complete.addEventListener('click', () => {
    const codigoMissao = elements.missionCode.value.trim();
    if (!codigoMissao) {
      showError('Concluir missão', new Error('Informe o código da missão.'));
      return;
    }
    runApiAction('Concluir missão', '/api/progresso/concluir', {
      method: 'POST',
      body: JSON.stringify({ codigo_missao: codigoMissao }),
    });
  });
  elements.deleteProgress.addEventListener('click', () => {
    if (!window.confirm('Apagar todo o progresso do usuário autenticado?')) {
      return;
    }
    runApiAction('Apagar progresso', '/api/progresso/me', { method: 'DELETE' });
  });
  elements.websocketConnect.addEventListener('click', async () => {
    if (webSocket && webSocket.readyState < WebSocket.CLOSING) {
      return;
    }

    try {
      setWebSocketStatus('Conectando', 'connecting');
      elements.websocketConnect.disabled = true;
      const token = await getAccessToken();
      const socket = new WebSocket(getWebSocketUrl());
      webSocket = socket;

      socket.addEventListener('open', () => {
        socket.send(JSON.stringify({ type: 'auth', accessToken: token }));
      }, { once: true });
      socket.addEventListener('message', (event) => {
        let message;
        try {
          message = JSON.parse(event.data);
        } catch {
          showError('WebSocket', new Error('Mensagem inválida recebida do servidor.'));
          return;
        }

        if (message.type === 'auth_ok') {
          setWebSocketStatus('Autenticado', 'authenticated');
          elements.websocketDisconnect.disabled = false;
          setOutput('WebSocket autenticado', { id_usuario: message.id_usuario });
        } else if (message.type === 'auth_error') {
          setWebSocketStatus('Falha na autenticação', 'error');
          setOutput('WebSocket', { error: message.error }, true);
        }
      });
      socket.addEventListener('close', () => {
        if (webSocket === socket) {
          webSocket = null;
          setWebSocketStatus('Desconectado', 'disconnected');
          elements.websocketDisconnect.disabled = true;
          elements.websocketConnect.disabled = !authenticated;
        }
      });
      socket.addEventListener('error', () => {
        setWebSocketStatus('Erro de conexão', 'error');
        setOutput('WebSocket', { message: 'Não foi possível conectar ao WebSocket.' }, true);
      });
    } catch (error) {
      disconnectWebSocket();
      showError('Conectar WebSocket', error);
    }
  });
  elements.websocketDisconnect.addEventListener('click', disconnectWebSocket);
  elements.clear.addEventListener('click', () => {
    elements.output.textContent = 'Aguardando uma ação.';
  });

  initialize();
})();
