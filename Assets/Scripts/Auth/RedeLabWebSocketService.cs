using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RedeLabEscola.Auth
{
    public enum RedeLabWebSocketState
    {
        Disconnected,
        Connecting,
        Authenticating,
        Connected,
        Reconnecting,
        AuthenticationFailed
    }

    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class RedeLabWebSocketService : MonoBehaviour
    {
        [Serializable]
        private sealed class ServerMessage
        {
            public string type;
            public int id_usuario;
            public string error;
        }

        private const string WebSocketPath = "/ws";
        private static readonly float[] DefaultReconnectDelays = { 2f, 5f, 10f, 20f };

        private static RedeLabWebSocketService instance;

        [Header("Configuracao opcional")]
        [Tooltip("Deixe vazio para derivar automaticamente da URL da API (http->ws, https->wss).")]
        [SerializeField] private string manualWebSocketUrl;
        [SerializeField, Min(1f)] private float maximumReconnectDelay = 20f;
        [Tooltip("Logs temporarios de diagnostico. Nunca imprime o Access Token.")]
        [SerializeField] private bool enableDiagnosticLogs = true;

        private RedeLabAuthManager auth;
        private Coroutine bindRoutine;
        private Coroutine reconnectRoutine;
        private RedeLabWebSocketState state = RedeLabWebSocketState.Disconnected;
        private bool reconnectAllowed;
        private bool intentionalDisconnect;
        private int reconnectAttempt;

        public static RedeLabWebSocketService Instance => instance;
        public RedeLabWebSocketState State => state;
        public bool IsConnected => state == RedeLabWebSocketState.Connected;
        public string CurrentWebSocketUrl => ResolveWebSocketUrl(
            auth != null ? auth.ApiBaseUrl : RedeLabAuthManager.DefaultApiBaseUrl,
            manualWebSocketUrl);

        public event Action<RedeLabWebSocketState> OnStateChanged;
        public event Action<string> OnServerMessage;

        public static bool IsRuntimeWebSocketPlatform
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static RedeLabWebSocketService EnsureInstance()
        {
            if (instance != null) return instance;
            RedeLabWebSocketService existing = FindObjectOfType<RedeLabWebSocketService>();
            if (existing != null) return existing;

            GameObject serviceObject = new GameObject("RedeLab WebSocket Service");
            return serviceObject.AddComponent<RedeLabWebSocketService>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            DiagnosticLog("Servico criado e marcado como DontDestroyOnLoad.");
            DiagnosticLog("Plataforma WebGL real: " + (IsRuntimeWebSocketPlatform ? "sim" : "nao"));
        }

        private void Start()
        {
            if (!TryBindToAuthManager())
            {
                bindRoutine = StartCoroutine(WaitForAuthManager());
            }
        }

        private void OnDestroy()
        {
            DetachFromAuthManager();
            CancelReconnect();
            if (instance == this)
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                RedeLabWebSocket_Disconnect();
#endif
                instance = null;
            }
        }

        public void ConfigureWebSocketUrl(string url)
        {
            string normalized = string.IsNullOrWhiteSpace(url) ? string.Empty : url.Trim();
            if (string.Equals(manualWebSocketUrl, normalized, StringComparison.OrdinalIgnoreCase)) return;

            manualWebSocketUrl = normalized;
            RestartForConfigurationChange();
        }

        public void ConnectIfAuthenticated()
        {
            TryConnect(false);
        }

        public void Disconnect()
        {
            DisconnectInternal(false);
        }

        // Callback da bridge WebGL.
        public void OnWebSocketOpened(string ignored)
        {
            if (!reconnectAllowed || intentionalDisconnect) return;
            DiagnosticLog("Bridge informou onopen; aguardando autenticacao do servidor.");
            SetState(RedeLabWebSocketState.Authenticating);
        }

        // Callback da bridge WebGL. O payload nunca e registrado para evitar dados futuros sensiveis.
        public void OnWebSocketMessage(string json)
        {
            ServerMessage message;
            try
            {
                message = JsonUtility.FromJson<ServerMessage>(json);
            }
            catch (Exception)
            {
                return;
            }

            if (message == null || string.IsNullOrWhiteSpace(message.type)) return;
            if (message.type == "auth_ok")
            {
                if (auth == null || !auth.IsAuthenticated ||
                    (auth.IdUsuario > 0 && message.id_usuario != auth.IdUsuario))
                {
                    HandleAuthenticationFailure();
                    return;
                }

                reconnectAttempt = 0;
                intentionalDisconnect = false;
                reconnectAllowed = true;
                SetState(RedeLabWebSocketState.Connected);
                DiagnosticLog("Resposta auth_ok recebida. WebSocket autenticado.");
                return;
            }

            if (message.type == "auth_error")
            {
                DiagnosticWarning("Resposta auth_error recebida: " + SafeAuthError(message.error));
                HandleAuthenticationFailure();
                return;
            }

            OnServerMessage?.Invoke(json);
        }

        // Callback da bridge WebGL.
        public void OnWebSocketClosed(string ignored)
        {
            bool shouldReconnect = reconnectAllowed
                && !intentionalDisconnect
                && HasAuthenticatedSession();

            DiagnosticWarning("Bridge informou onclose. Reconectar: " + (shouldReconnect ? "sim" : "nao"));
            intentionalDisconnect = false;
            if (state == RedeLabWebSocketState.AuthenticationFailed) return;
            if (shouldReconnect)
            {
                ScheduleReconnect();
            }
            else
            {
                SetState(RedeLabWebSocketState.Disconnected);
            }
        }

        // Callback da bridge WebGL. O fechamento subsequente controla a reconexao.
        public void OnWebSocketError(string ignored)
        {
            DiagnosticWarning("Bridge informou onerror de transporte.");
        }

        public static string ResolveWebSocketUrl(string apiBaseUrl, string manualUrl = null)
        {
            string candidate = string.IsNullOrWhiteSpace(manualUrl) ? null : manualUrl.Trim();
            if (candidate != null)
            {
                return IsValidWebSocketUrl(candidate) ? candidate : string.Empty;
            }

            if (!Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out Uri apiUri)) return string.Empty;
            string socketScheme;
            if (string.Equals(apiUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                socketScheme = "wss";
            }
            else if (string.Equals(apiUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                socketScheme = "ws";
            }
            else
            {
                return string.Empty;
            }

            try
            {
                UriBuilder builder = new UriBuilder(apiUri)
                {
                    Scheme = socketScheme,
                    Port = apiUri.IsDefaultPort ? -1 : apiUri.Port,
                    Path = WebSocketPath,
                    Query = string.Empty,
                    Fragment = string.Empty
                };
                return builder.Uri.AbsoluteUri;
            }
            catch (UriFormatException)
            {
                return string.Empty;
            }
        }

        private IEnumerator WaitForAuthManager()
        {
            while (!TryBindToAuthManager())
            {
                yield return null;
            }

            bindRoutine = null;
        }

        private bool TryBindToAuthManager()
        {
            if (auth != null) return true;
            auth = RedeLabAuthManager.Instance;
            if (auth == null) return false;

            auth.OnAuthSuccess += HandleAuthSuccess;
            auth.OnAuthFailed += HandleAuthFailed;
            auth.OnAuthReady += HandleAuthReady;
            auth.OnLogout += HandleLogout;
            auth.OnApiBaseUrlChanged += HandleApiBaseUrlChanged;

            DiagnosticLog("RedeLabAuthManager encontrado. Autenticado: "
                + (auth.IsAuthenticated ? "sim" : "nao")
                + "; Access Token disponivel: "
                + (!string.IsNullOrWhiteSpace(auth.AccessToken) ? "sim" : "nao"));

            if (auth.IsAuthenticated) TryConnect(false);
            return true;
        }

        private void DetachFromAuthManager()
        {
            if (bindRoutine != null)
            {
                StopCoroutine(bindRoutine);
                bindRoutine = null;
            }
            if (auth == null) return;

            auth.OnAuthSuccess -= HandleAuthSuccess;
            auth.OnAuthFailed -= HandleAuthFailed;
            auth.OnAuthReady -= HandleAuthReady;
            auth.OnLogout -= HandleLogout;
            auth.OnApiBaseUrlChanged -= HandleApiBaseUrlChanged;
            auth = null;
        }

        private void HandleAuthSuccess(RedeLabUser user)
        {
            DiagnosticLog("Evento de autenticacao detectado. Access Token disponivel: "
                + (auth != null && !string.IsNullOrWhiteSpace(auth.AccessToken) ? "sim" : "nao"));
            if (state == RedeLabWebSocketState.AuthenticationFailed)
            {
                SetState(RedeLabWebSocketState.Disconnected);
            }
            TryConnect(false);
        }

        private void HandleAuthFailed(string ignored)
        {
            DiagnosticWarning("Autenticacao Auth0 indisponivel; WebSocket sera desconectado.");
            DisconnectInternal(false);
        }

        private void HandleAuthReady()
        {
            DiagnosticLog("Inicializacao Auth0 concluida. Autenticado: "
                + (auth != null && auth.IsAuthenticated ? "sim" : "nao")
                + "; Access Token disponivel: "
                + (auth != null && !string.IsNullOrWhiteSpace(auth.AccessToken) ? "sim" : "nao"));
            if (HasAuthenticatedSession())
            {
                TryConnect(false);
            }
            else
            {
                DisconnectInternal(false);
            }
        }

        private void HandleLogout()
        {
            DiagnosticLog("Logout detectado; encerrando WebSocket.");
            DisconnectInternal(false);
        }

        private void HandleApiBaseUrlChanged(string ignored)
        {
            RestartForConfigurationChange();
        }

        private void RestartForConfigurationChange()
        {
            if (!HasAuthenticatedSession()) return;
            DisconnectInternal(true);
            TryConnect(false);
        }

        private void TryConnect(bool reconnecting)
        {
            if (!IsRuntimeWebSocketPlatform)
            {
                DiagnosticLog("Conexao ignorada: execucao nao e WebGL real.");
                return;
            }
            if (!HasAuthenticatedSession())
            {
                DiagnosticLog("Conexao aguardando sessao autenticada e Access Token.");
                return;
            }
            if (state == RedeLabWebSocketState.Connecting
                || state == RedeLabWebSocketState.Authenticating
                || state == RedeLabWebSocketState.Connected)
            {
                return;
            }

            string url = CurrentWebSocketUrl;
            DiagnosticLog("URL WebSocket calculada: " + url);
            if (string.IsNullOrWhiteSpace(url))
            {
                reconnectAllowed = false;
                SetState(RedeLabWebSocketState.Disconnected);
                Debug.LogWarning("A URL do WebSocket RedeLab e invalida. Revise a URL da API ou a configuracao manual.", this);
                return;
            }

            CancelReconnect();
            intentionalDisconnect = false;
            reconnectAllowed = true;
            SetState(reconnecting ? RedeLabWebSocketState.Reconnecting : RedeLabWebSocketState.Connecting);
            DiagnosticLog(reconnecting ? "Solicitando reconexao pela bridge JavaScript." : "Solicitando conexao pela bridge JavaScript.");

#if UNITY_WEBGL && !UNITY_EDITOR
            RedeLabWebSocket_Connect(gameObject.name, url, auth.AccessToken);
#endif
        }

        private void DisconnectInternal(bool reconnectAfterClose)
        {
            CancelReconnect();
            reconnectAllowed = reconnectAfterClose;
            intentionalDisconnect = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            RedeLabWebSocket_Disconnect();
#endif
            SetState(RedeLabWebSocketState.Disconnected);
            intentionalDisconnect = false;
            if (!reconnectAfterClose) reconnectAttempt = 0;
        }

        private void HandleAuthenticationFailure()
        {
            CancelReconnect();
            reconnectAllowed = false;
            intentionalDisconnect = true;
            SetState(RedeLabWebSocketState.AuthenticationFailed);
#if UNITY_WEBGL && !UNITY_EDITOR
            RedeLabWebSocket_Disconnect();
#endif
            intentionalDisconnect = false;
            DiagnosticWarning("Autenticacao do WebSocket rejeitada; reconexao automatica suspensa ate nova autenticacao.");
        }

        private void ScheduleReconnect()
        {
            if (reconnectRoutine != null || !reconnectAllowed || !HasAuthenticatedSession()) return;
            reconnectRoutine = StartCoroutine(ReconnectAfterDelay());
        }

        private IEnumerator ReconnectAfterDelay()
        {
            SetState(RedeLabWebSocketState.Reconnecting);
            int delayIndex = Mathf.Clamp(reconnectAttempt, 0, DefaultReconnectDelays.Length - 1);
            float delay = Mathf.Min(maximumReconnectDelay, DefaultReconnectDelays[delayIndex]);
            reconnectAttempt++;
            DiagnosticLog("Nova tentativa agendada em " + delay.ToString("0") + " segundo(s).");
            yield return new WaitForSecondsRealtime(Mathf.Max(1f, delay));
            reconnectRoutine = null;

            if (reconnectAllowed && HasAuthenticatedSession())
            {
                TryConnect(true);
            }
        }

        private void CancelReconnect()
        {
            if (reconnectRoutine == null) return;
            StopCoroutine(reconnectRoutine);
            reconnectRoutine = null;
        }

        private bool HasAuthenticatedSession()
        {
            return auth != null
                && auth.IsAuthenticated
                && !string.IsNullOrWhiteSpace(auth.AccessToken);
        }

        private void DiagnosticLog(string message)
        {
            if (enableDiagnosticLogs) Debug.Log("[RedeLab WS] " + message, this);
        }

        private void DiagnosticWarning(string message)
        {
            if (enableDiagnosticLogs) Debug.LogWarning("[RedeLab WS] " + message, this);
        }

        private static string SafeAuthError(string error)
        {
            if (string.IsNullOrWhiteSpace(error)) return "sem codigo";
            if (error == "invalid_token" || error == "auth_required" || error == "user_not_synced")
            {
                return error;
            }
            return "erro nao identificado";
        }

        private void SetState(RedeLabWebSocketState value)
        {
            if (state == value) return;
            state = value;
            OnStateChanged?.Invoke(state);
        }

        private static bool IsValidWebSocketUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri parsed)
                && (string.Equals(parsed.Scheme, "ws", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parsed.Scheme, "wss", StringComparison.OrdinalIgnoreCase));
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RedeLabWebSocket_Connect(string receiver, string url, string accessToken);

        [DllImport("__Internal")]
        private static extern void RedeLabWebSocket_Disconnect();
#endif
    }
}
