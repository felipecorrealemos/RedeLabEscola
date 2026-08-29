using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace RedeLabEscola.Auth
{
    [DisallowMultipleComponent]
    public sealed class RedeLabAuthManager : MonoBehaviour
    {
        public const string Auth0Domain = "dev-ldgwwvi01va0qxzx.us.auth0.com";
        public const string Auth0ClientId = "Ai8Q8DjlvFJqmcwkcedu5Spdu7XGkrmd";
        public const string Auth0Audience = "https://api.redelab.local";
        public const string DefaultApiBaseUrl = "http://localhost:3000";

        private static RedeLabAuthManager instance;
        private RedeLabApiClient apiClient;
        private Coroutine profileRoutine;
        private bool silentRenewalInProgress;

        public static RedeLabAuthManager Instance => instance;
        public string AccessToken { get; private set; }
        public RedeLabUser User { get; private set; }
        public bool IsAuthenticated => !SessionRenewalRequired && !string.IsNullOrEmpty(AccessToken) && User != null;
        public bool IsBusy { get; private set; }
        public bool SessionRenewalRequired { get; private set; }
        public string ApiBaseUrl => apiClient != null ? apiClient.BaseUrl : DefaultApiBaseUrl;
        public int IdUsuario => User != null ? User.id_usuario : 0;
        public string Nome => User != null ? User.nome : string.Empty;
        public string Email => User != null ? User.email : string.Empty;
        public int IdPersonagem => User != null ? User.id_personagem : 0;

        public event Action OnAuthStarted;
        public event Action<RedeLabUser> OnAuthSuccess;
        public event Action<string> OnAuthFailed;
        public event Action OnLogout;
        public event Action OnAuthReady;
        public event Action<string> OnSessionRenewalRequired;
        public event Action<string> OnApiBaseUrlChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<RedeLabAuthManager>() != null)
            {
                return;
            }

            GameObject managerObject = new GameObject("RedeLab Auth Manager");
            managerObject.AddComponent<RedeLabAuthManager>();
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
            ConfigureApiClient(DefaultApiBaseUrl);
        }

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetBusy(true);
            OnAuthStarted?.Invoke();
            RedeLabAuth_Initialize(gameObject.name, Auth0Domain, Auth0ClientId, Auth0Audience);
#endif
        }

        public void LoginWithGoogle()
        {
            if (IsBusy || IsAuthenticated)
            {
                return;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            SetBusy(true);
            OnAuthStarted?.Invoke();
            RedeLabAuth_LoginWithGoogle();
#else
            Fail("O login Auth0 esta disponivel somente no build WebGL servido em http://localhost:8081.");
#endif
        }

        public void Logout()
        {
            // Fecha a presenca antes de limpar a sessao e antes do logout Auth0.
            RedeLabWebSocketService.Instance?.Disconnect();
            RedeLabProgressService.Instance?.ResetSession();
            ClearSession();
            OnLogout?.Invoke();
#if UNITY_WEBGL && !UNITY_EDITOR
            RedeLabAuth_Logout();
#endif
        }

        public void ConfigureApiBaseUrl(string baseUrl)
        {
            if (IsBusy)
            {
                throw new InvalidOperationException("Nao altere a URL da API durante uma autenticacao.");
            }

            string previousUrl = ApiBaseUrl;
            ConfigureApiClient(baseUrl);
            if (!string.Equals(previousUrl, ApiBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                OnApiBaseUrlChanged?.Invoke(ApiBaseUrl);
            }
        }

        public IEnumerator RefreshUser(Action<RedeLabUser> onSuccess, Action<string> onError)
        {
            RedeLabUser loadedUser = null;
            string error = null;
            yield return apiClient.GetMe(value => loadedUser = value, value => error = value);
            if (!string.IsNullOrEmpty(error) || loadedUser == null || loadedUser.id_usuario <= 0)
            {
                onError?.Invoke(string.IsNullOrEmpty(error)
                    ? "A API nao retornou o usuario autenticado."
                    : error);
                yield break;
            }

            User = loadedUser;
            onSuccess?.Invoke(User);
        }

        public IEnumerator SetCharacter(int characterId, Action onSuccess, Action<string> onError)
        {
            RedeLabSetCharacterResponse response = null;
            string error = null;
            yield return apiClient.SetCharacter(characterId, value => response = value, value => error = value);
            if (!string.IsNullOrEmpty(error) || response == null || !response.success)
            {
                onError?.Invoke(string.IsNullOrEmpty(error)
                    ? "A API nao confirmou a escolha do personagem."
                    : error);
                yield break;
            }

            if (User != null) User.id_personagem = response.id_personagem;
            onSuccess?.Invoke();
        }

        public IEnumerator GetProgress(Action<RedeLabProgress> onSuccess, Action<string> onError)
        {
            return apiClient.GetProgress(onSuccess, onError);
        }

        public IEnumerator CompleteMission(
            string missionCode,
            Action<RedeLabCompleteMissionResponse> onSuccess,
            Action<string> onError)
        {
            return apiClient.CompleteMission(missionCode, onSuccess, onError);
        }

        public IEnumerator DeleteProgress(Action onSuccess, Action<string> onError)
        {
            return RunOperation(apiClient.DeleteProgress, onSuccess, onError, false);
        }

        public IEnumerator ClearCharacter(Action onSuccess, Action<string> onError)
        {
            return RunOperation(apiClient.ClearCharacter, onSuccess, onError, true);
        }

        public IEnumerator ResetNewGame(Action onSuccess, Action<string> onError)
        {
            return RunOperation(apiClient.ResetNewGame, onSuccess, onError, true);
        }

        private IEnumerator RunOperation(
            Func<Action<RedeLabOperationResponse>, Action<string>, IEnumerator> operation,
            Action onSuccess,
            Action<string> onError,
            bool clearCharacter)
        {
            RedeLabOperationResponse response = null;
            string error = null;
            yield return operation(value => response = value, value => error = value);
            if (!string.IsNullOrEmpty(error) || response == null || !response.success)
            {
                onError?.Invoke(string.IsNullOrEmpty(error) ? "A API nao confirmou a operacao." : error);
                yield break;
            }

            if (clearCharacter && User != null) User.id_personagem = 0;
            onSuccess?.Invoke();
        }

        public IEnumerator GetMissionsForPhase(
            int phaseId,
            Action<RedeLabMission[]> onSuccess,
            Action<string> onError)
        {
            return apiClient.GetMissionsForPhase(phaseId, onSuccess, onError);
        }

        // Chamado pelo bridge JavaScript. Nunca registrar o parametro: ele contem o Access Token.
        public void OnWebGLAuthToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                Fail("O Auth0 nao devolveu um Access Token valido.");
                return;
            }

            SetBusy(true);
            SessionRenewalRequired = false;
            silentRenewalInProgress = false;
            AccessToken = accessToken;
            OnAuthStarted?.Invoke();
            if (profileRoutine != null)
            {
                StopCoroutine(profileRoutine);
            }
            profileRoutine = StartCoroutine(SyncAndLoadProfile());
        }

        public void OnWebGLAuthFailed(string message)
        {
            Fail(string.IsNullOrWhiteSpace(message) ? "Falha ao autenticar com o Auth0." : message);
        }

        public void OnWebGLLoggedOut(string ignored)
        {
            ClearSession();
            OnLogout?.Invoke();
        }

        public void OnWebGLAuthReady(string ignored)
        {
            silentRenewalInProgress = false;
            SetBusy(false);
            OnAuthReady?.Invoke();
        }

        public void OnWebGLSilentRenewalFailed(string message)
        {
            silentRenewalInProgress = false;
            SessionRenewalRequired = true;
            SetBusy(false);
            OnSessionRenewalRequired?.Invoke(
                string.IsNullOrWhiteSpace(message)
                    ? "Sua sessao precisa ser renovada."
                    : message);
        }

        private IEnumerator SyncAndLoadProfile()
        {
            string error = null;
            yield return apiClient.SyncUser(_ => { }, value => error = value);
            if (!string.IsNullOrEmpty(error))
            {
                if (SessionRenewalRequired)
                {
                    profileRoutine = null;
                    SetBusy(false);
                    yield break;
                }
                Fail(error);
                yield break;
            }

            RedeLabUser loadedUser = null;
            yield return apiClient.GetMe(value => loadedUser = value, value => error = value);
            if (!string.IsNullOrEmpty(error) || loadedUser == null)
            {
                if (SessionRenewalRequired)
                {
                    profileRoutine = null;
                    SetBusy(false);
                    yield break;
                }
                Fail(string.IsNullOrEmpty(error) ? "A API nao retornou o usuario autenticado." : error);
                yield break;
            }

            User = loadedUser;
            profileRoutine = null;
            SetBusy(false);
            OnAuthSuccess?.Invoke(User);
        }

        private void Fail(string message)
        {
            ClearSession();
            OnAuthFailed?.Invoke(message);
        }

        private void ClearSession()
        {
            if (profileRoutine != null)
            {
                StopCoroutine(profileRoutine);
                profileRoutine = null;
            }
            AccessToken = null;
            User = null;
            SessionRenewalRequired = false;
            silentRenewalInProgress = false;
            SetBusy(false);
        }

        private void ConfigureApiClient(string baseUrl)
        {
            if (apiClient != null) apiClient.OnUnauthorized -= HandleApiUnauthorized;
            apiClient = new RedeLabApiClient(baseUrl, () => AccessToken);
            apiClient.OnUnauthorized += HandleApiUnauthorized;
        }

        private void HandleApiUnauthorized()
        {
            SessionRenewalRequired = true;
            OnSessionRenewalRequired?.Invoke("Sua sessao precisa ser renovada.");
            if (silentRenewalInProgress) return;

#if UNITY_WEBGL && !UNITY_EDITOR
            silentRenewalInProgress = true;
            RedeLabAuth_RenewTokenSilently();
#endif
        }

        private void SetBusy(bool value)
        {
            IsBusy = value;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RedeLabAuth_Initialize(string receiver, string domain, string clientId, string audience);

        [DllImport("__Internal")]
        private static extern void RedeLabAuth_LoginWithGoogle();

        [DllImport("__Internal")]
        private static extern void RedeLabAuth_Logout();

        [DllImport("__Internal")]
        private static extern void RedeLabAuth_RenewTokenSilently();
#endif
    }
}
