mergeInto(LibraryManager.library, {
  RedeLabAuth_Initialize: function (receiverPtr, domainPtr, clientIdPtr, audiencePtr) {
    var receiver = UTF8ToString(receiverPtr);
    var domain = UTF8ToString(domainPtr);
    var clientId = UTF8ToString(clientIdPtr);
    var audience = UTF8ToString(audiencePtr);

    function send(method, payload) {
      SendMessage(receiver, method, payload || "");
    }

    function friendlyError(error) {
      if (!error) return "Falha desconhecida na autenticacao.";
      if (error.error === "access_denied") return "O login foi cancelado ou negado.";
      return error.message || error.error_description || String(error);
    }

    function loadSdk() {
      if (window.auth0 && window.auth0.createAuth0Client) return Promise.resolve();
      if (window.RedeLabAuthSdkPromise) return window.RedeLabAuthSdkPromise;

      window.RedeLabAuthSdkPromise = new Promise(function (resolve, reject) {
        var script = document.createElement("script");
        script.src = "/vendor/auth0-spa-js.production.js";
        script.async = true;
        script.onload = resolve;
        script.onerror = function () {
          reject(new Error("Nao foi possivel carregar o SDK Auth0 local."));
        };
        document.head.appendChild(script);
      });
      return window.RedeLabAuthSdkPromise;
    }

    window.RedeLabAuthBridge = {
      receiver: receiver,
      client: null,
      domain: domain,
      clientId: clientId,
      audience: audience,
      redirectUri: window.location.origin,
      send: send,
      friendlyError: friendlyError
    };

    loadSdk()
      .then(function () {
        return window.auth0.createAuth0Client({
          domain: domain,
          clientId: clientId,
          cacheLocation: "localstorage",
          useRefreshTokens: false,
          authorizationParams: {
            audience: audience,
            redirect_uri: window.location.origin,
            scope: "openid profile email"
          }
        });
      })
      .then(function (client) {
        window.RedeLabAuthBridge.client = client;
        var query = window.location.search;
        if (query.indexOf("code=") !== -1 || query.indexOf("error=") !== -1) {
          return client.handleRedirectCallback().then(function () {
            window.history.replaceState({}, document.title, window.location.pathname + window.location.hash);
          });
        }
        return client.checkSession().catch(function (error) {
          if (error && (error.error === "login_required" || error.error === "consent_required")) return;
          throw error;
        });
      })
      .then(function () {
        return window.RedeLabAuthBridge.client.isAuthenticated();
      })
      .then(function (authenticated) {
        if (!authenticated) return null;
        return window.RedeLabAuthBridge.client.getTokenSilently();
      })
      .then(function (token) {
        if (token) send("OnWebGLAuthToken", token);
        else send("OnWebGLAuthReady", "");
      })
      .catch(function (error) {
        send("OnWebGLAuthFailed", friendlyError(error));
      });
  },

  RedeLabAuth_LoginWithGoogle: function () {
    var bridge = window.RedeLabAuthBridge;
    if (!bridge || !bridge.client) {
      if (bridge) bridge.send("OnWebGLAuthFailed", "O cliente Auth0 ainda esta inicializando.");
      return;
    }

    bridge.client.loginWithPopup({
      authorizationParams: {
        audience: bridge.audience,
        redirect_uri: bridge.redirectUri,
        scope: "openid profile email",
        connection: "google-oauth2"
      }
    }).then(function () {
      return bridge.client.getTokenSilently();
    }).then(function (token) {
      if (!token) throw new Error("O Auth0 nao retornou um Access Token valido.");
      bridge.send("OnWebGLAuthToken", token);
    }).catch(function (error) {
      bridge.send("OnWebGLAuthFailed", error.message || String(error));
    });
  },

  RedeLabAuth_Logout: function () {
    var bridge = window.RedeLabAuthBridge;
    if (!bridge || !bridge.client) return;
    bridge.client.logout({
      logoutParams: { returnTo: window.location.origin }
    });
  },

  RedeLabAuth_RenewTokenSilently: function () {
    var bridge = window.RedeLabAuthBridge;
    if (!bridge || !bridge.client) return;

    bridge.client.getTokenSilently({ cacheMode: "off" })
      .then(function (token) {
        if (!token) throw new Error("O Auth0 nao retornou um Access Token valido.");
        bridge.send("OnWebGLAuthToken", token);
      })
      .catch(function (error) {
        bridge.send(
          "OnWebGLSilentRenewalFailed",
          bridge.friendlyError ? bridge.friendlyError(error) : "Sua sessao precisa ser renovada."
        );
      });
  }
});
