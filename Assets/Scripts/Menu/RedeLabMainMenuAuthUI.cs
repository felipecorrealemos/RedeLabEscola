using RedeLabEscola.Auth;
using UnityEngine;
using UnityEngine.UI;

namespace RedeLabEscola.Menu
{
    [DisallowMultipleComponent]
    public sealed class RedeLabMainMenuAuthUI : MonoBehaviour
    {
        [Header("Area de autenticacao (editavel na cena)")]
        [SerializeField] private GameObject authenticationPanel;
        [SerializeField] private Button authButton;
        [SerializeField] private Image googleIcon;
        [SerializeField] private Text authButtonLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Text greetingLabel;

        [Header("Botoes protegidos por login")]
        [SerializeField] private Button[] protectedButtons;

        private RedeLabAuthManager auth;
        private bool operationBusy;

        private void Start()
        {
            auth = RedeLabAuthManager.Instance;
            if (!HasRequiredSceneReferences())
            {
                Debug.LogError(
                    "Auth UI: configure o Authentication Panel e suas referencias serializadas na cena MainMenu.",
                    this);
                return;
            }

            authButton.onClick.AddListener(HandleAuthButton);
            Subscribe();
            Refresh();
        }

        private void OnDestroy()
        {
            if (authButton != null) authButton.onClick.RemoveListener(HandleAuthButton);
            Unsubscribe();
        }

        private bool HasRequiredSceneReferences()
        {
            return authenticationPanel != null
                && authButton != null
                && googleIcon != null
                && authButtonLabel != null
                && statusLabel != null
                && greetingLabel != null;
        }

        private void Subscribe()
        {
            if (auth == null) return;
            auth.OnAuthStarted += HandleAuthenticationStarted;
            auth.OnAuthSuccess += HandleAuthenticationSucceeded;
            auth.OnAuthFailed += HandleAuthenticationFailed;
            auth.OnLogout += HandleLoggedOut;
            auth.OnAuthReady += HandleAuthReady;
        }

        private void Unsubscribe()
        {
            if (auth == null) return;
            auth.OnAuthStarted -= HandleAuthenticationStarted;
            auth.OnAuthSuccess -= HandleAuthenticationSucceeded;
            auth.OnAuthFailed -= HandleAuthenticationFailed;
            auth.OnLogout -= HandleLoggedOut;
            auth.OnAuthReady -= HandleAuthReady;
        }

        private void HandleAuthButton()
        {
            AudioManager.ResumeAfterUserInteraction();
            if (auth == null) return;
            if (auth.IsAuthenticated) auth.Logout();
            else auth.LoginWithGoogle();
        }

        private void HandleAuthenticationStarted()
        {
            SetStatus("Autenticando...", string.Empty, false);
        }

        private void HandleAuthenticationSucceeded(RedeLabUser user)
        {
            Refresh();
        }

        private void HandleAuthenticationFailed(string message)
        {
            SetStatus("Falha na autenticacao: " + message, string.Empty, true);
        }

        private void HandleLoggedOut()
        {
            Refresh();
        }

        private void HandleAuthReady()
        {
            Refresh();
        }

        public void ShowOperationStatus(string message, bool busy)
        {
            operationBusy = busy;
            SetStatus(
                message,
                auth != null && auth.IsAuthenticated ? "Ola, " + auth.Nome : string.Empty,
                true);
        }

        private void Refresh()
        {
            if (auth == null)
            {
                SetStatus("Servico de autenticacao indisponivel.", string.Empty, true);
                return;
            }

            if (auth.IsBusy)
            {
                SetStatus("Autenticando...", string.Empty, false);
                return;
            }

            if (auth.IsAuthenticated)
            {
                SetStatus("Autenticado", "Ola, " + auth.User.nome, true);
                if (authButtonLabel != null) authButtonLabel.text = "Sair da conta";
            }
            else
            {
                SetStatus("Nao autenticado - entre para continuar", string.Empty, true);
                if (authButtonLabel != null) authButtonLabel.text = "Entrar com Google";
            }
        }

        private void SetStatus(string status, string greeting, bool allowAuthAction)
        {
            bool authenticated = auth != null && auth.IsAuthenticated && !operationBusy;
            if (statusLabel != null) statusLabel.text = status;
            if (greetingLabel != null) greetingLabel.text = greeting;
            if (authButton != null) authButton.interactable = allowAuthAction;

            if (protectedButtons == null) return;
            foreach (Button button in protectedButtons)
            {
                if (button != null) button.interactable = authenticated;
            }
        }
    }
}
