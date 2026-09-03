using RedeLabEscola.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RedeLabEscola.Debugging
{
    [DisallowMultipleComponent]
    public sealed class RedeLabDeveloperDebugPanel : MonoBehaviour
    {
        private const string PrefabResourcePath = "RedeLabDeveloperDebugPanel";
        private static RedeLabDeveloperDebugPanel instance;

        [Header("Autorização")]
        [SerializeField] private RedeLabDeveloperDebugSettings settings;

        [Header("UI serializada")]
        [SerializeField] private Canvas panelCanvas;
        [SerializeField] private GameObject debugButtonRoot;
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button openButton;
        [SerializeField] private Button finishStage1Button;
        [SerializeField] private Button finishStage2Button;
        [SerializeField] private Button closeButton;
        [SerializeField] private Text statusLabel;

        private RedeLabAuthManager subscribedAuth;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (instance != null || FindObjectOfType<RedeLabDeveloperDebugPanel>() != null) return;

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab != null) Instantiate(prefab);
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
            if (panelCanvas == null) panelCanvas = GetComponent<Canvas>();
            BindButtons();
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SubscribeToAuth();
            RefreshAvailability();
        }

        private void Start()
        {
            SubscribeToAuth();
            RefreshAvailability();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeFromAuth();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void BindButtons()
        {
            if (openButton != null) openButton.onClick.AddListener(OpenPanel);
            if (finishStage1Button != null) finishStage1Button.onClick.AddListener(FinishStage1);
            if (finishStage2Button != null) finishStage2Button.onClick.AddListener(FinishStage2);
            if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);
        }

        private void SubscribeToAuth()
        {
            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            if (subscribedAuth == auth) return;

            UnsubscribeFromAuth();
            subscribedAuth = auth;
            if (subscribedAuth == null) return;

            subscribedAuth.OnAuthSuccess += HandleAuthenticationChanged;
            subscribedAuth.OnLogout += HandleAuthenticationCleared;
            subscribedAuth.OnSessionRenewalRequired += HandleSessionRenewalRequired;
        }

        private void UnsubscribeFromAuth()
        {
            if (subscribedAuth == null) return;
            subscribedAuth.OnAuthSuccess -= HandleAuthenticationChanged;
            subscribedAuth.OnLogout -= HandleAuthenticationCleared;
            subscribedAuth.OnSessionRenewalRequired -= HandleSessionRenewalRequired;
            subscribedAuth = null;
        }

        private void HandleAuthenticationChanged(RedeLabUser ignored)
        {
            RefreshAvailability();
        }

        private void HandleAuthenticationCleared()
        {
            RefreshAvailability();
        }

        private void HandleSessionRenewalRequired(string ignored)
        {
            RefreshAvailability();
        }

        private void HandleSceneLoaded(Scene ignoredScene, LoadSceneMode ignoredMode)
        {
            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            bool authorized = settings != null && settings.IsAuthorized(RedeLabAuthManager.Instance);
            string sceneName = SceneManager.GetActiveScene().name;
            bool isOffice = sceneName == SceneNames.Office;
            bool isFactory = sceneName == SceneNames.Factory;
            bool visible = authorized && (isOffice || isFactory);

            // A transição de fase desativa todos os outros Canvas para esconder a UI
            // durante o fade. Como este objeto é persistente, é necessário restaurar
            // explicitamente o Canvas quando a cena seguinte terminar de carregar.
            if (panelCanvas != null) panelCanvas.enabled = visible;
            if (debugButtonRoot != null) debugButtonRoot.SetActive(visible);
            if (!visible && panelRoot != null) panelRoot.SetActive(false);
            if (finishStage1Button != null) finishStage1Button.interactable = visible && isOffice;
            if (finishStage2Button != null) finishStage2Button.interactable = visible && isFactory;
            SetStatus(string.Empty);
        }

        private void OpenPanel()
        {
            if (!CanExecuteInCurrentScene()) return;
            panelRoot.SetActive(true);
            RefreshAvailability();
        }

        private void ClosePanel()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void FinishStage1()
        {
            TryFinishStage(SceneNames.Office);
        }

        private void FinishStage2()
        {
            TryFinishStage(SceneNames.Factory);
        }

        private void TryFinishStage(string requiredScene)
        {
            if (!CanExecuteInCurrentScene() || SceneManager.GetActiveScene().name != requiredScene)
            {
                SetStatus("Ação indisponível nesta fase.");
                return;
            }

            StageTransitionUI transition = FindObjectOfType<StageTransitionUI>(true);
            if (transition == null)
            {
                SetStatus("Transição da fase não encontrada.");
                return;
            }

            if (!transition.TryCompleteStage(true))
            {
                SetStatus("A conclusão da fase já foi iniciada.");
                return;
            }

            panelRoot.SetActive(false);
        }

        private bool CanExecuteInCurrentScene()
        {
            bool authorized = settings != null && settings.IsAuthorized(RedeLabAuthManager.Instance);
            string sceneName = SceneManager.GetActiveScene().name;
            bool supportedScene = sceneName == SceneNames.Office || sceneName == SceneNames.Factory;
            if (authorized && supportedScene) return true;

            if (panelRoot != null) panelRoot.SetActive(false);
            return false;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message ?? string.Empty;
        }
    }
}
