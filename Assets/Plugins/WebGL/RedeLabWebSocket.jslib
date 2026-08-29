mergeInto(LibraryManager.library, {
  RedeLabWebSocket_Connect: function (receiverPtr, urlPtr, accessTokenPtr) {
    var receiver = UTF8ToString(receiverPtr);
    var url = UTF8ToString(urlPtr);
    var accessToken = UTF8ToString(accessTokenPtr);
    var bridge = window.RedeLabUnityWebSocketBridge;

    console.log("[RedeLab WS] Bridge recebeu solicitacao de conexao. URL: " + url + "; token disponivel: " + (accessToken ? "sim" : "nao"));

    if (!bridge) {
      bridge = {
        socket: null,
        generation: 0,
        unloadInstalled: false
      };
      window.RedeLabUnityWebSocketBridge = bridge;
    }

    bridge.generation += 1;
    var generation = bridge.generation;
    var previous = bridge.socket;
    bridge.socket = null;
    if (previous && previous.readyState < WebSocket.CLOSING) {
      previous.close(1000, "connection_replaced");
    }

    function isCurrent(socket) {
      return bridge.generation === generation && bridge.socket === socket;
    }

    function sendToUnity(method, payload) {
      if (bridge.generation !== generation) return;
      SendMessage(receiver, method, payload || "");
    }

    var socket;
    try {
      console.log("[RedeLab WS] Executando new WebSocket(" + url + ").");
      socket = new WebSocket(url);
    } catch (error) {
      accessToken = "";
      console.error("[RedeLab WS] new WebSocket falhou antes de abrir a conexao.");
      sendToUnity("OnWebSocketError", "connection_failed");
      sendToUnity("OnWebSocketClosed", JSON.stringify({ code: 0, wasClean: false }));
      return;
    }

    bridge.socket = socket;
    socket.addEventListener("open", function () {
      if (!isCurrent(socket)) return;
      console.log("[RedeLab WS] onopen recebido.");
      sendToUnity("OnWebSocketOpened", "");
      try {
        socket.send(JSON.stringify({ type: "auth", accessToken: accessToken }));
        console.log("[RedeLab WS] Mensagem auth enviada (Access Token oculto).");
      } catch (error) {
        console.error("[RedeLab WS] Falha ao enviar a mensagem auth.");
        sendToUnity("OnWebSocketError", "auth_send_failed");
        socket.close(1000, "auth_send_failed");
      } finally {
        accessToken = "";
      }
    }, { once: true });

    socket.addEventListener("message", function (event) {
      if (!isCurrent(socket) || typeof event.data !== "string") return;
      var safeType = "mensagem_nao_identificada";
      try {
        var parsed = JSON.parse(event.data);
        if (parsed && (parsed.type === "auth_ok" || parsed.type === "auth_error")) {
          safeType = parsed.type;
        }
      } catch (error) {
      }
      console.log("[RedeLab WS] onmessage recebido. Tipo seguro: " + safeType + ".");
      sendToUnity("OnWebSocketMessage", event.data);
    });

    socket.addEventListener("error", function () {
      if (!isCurrent(socket)) return;
      console.error("[RedeLab WS] onerror recebido.");
      sendToUnity("OnWebSocketError", "connection_error");
    });

    socket.addEventListener("close", function (event) {
      accessToken = "";
      if (!isCurrent(socket)) return;
      bridge.socket = null;
      console.warn("[RedeLab WS] onclose recebido. Codigo: " + event.code + "; fechamento limpo: " + event.wasClean + ".");
      sendToUnity("OnWebSocketClosed", JSON.stringify({
        code: event.code,
        wasClean: event.wasClean
      }));
    });

    if (!bridge.unloadInstalled) {
      bridge.unloadInstalled = true;
      window.addEventListener("beforeunload", function () {
        var activeBridge = window.RedeLabUnityWebSocketBridge;
        var activeSocket = activeBridge && activeBridge.socket;
        if (activeSocket && activeSocket.readyState < WebSocket.CLOSING) {
          activeSocket.close(1000, "page_unload");
        }
      });
    }
  },

  RedeLabWebSocket_Disconnect: function () {
    var bridge = window.RedeLabUnityWebSocketBridge;
    if (!bridge) return;

    bridge.generation += 1;
    var socket = bridge.socket;
    bridge.socket = null;
    console.log("[RedeLab WS] Desconexao solicitada pela Unity.");
    if (socket && socket.readyState < WebSocket.CLOSING) {
      socket.close(1000, "client_disconnect");
    }
  }
});
