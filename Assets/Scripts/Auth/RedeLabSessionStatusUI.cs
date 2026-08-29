using UnityEngine;
using UnityEngine.UI;

namespace RedeLabEscola.Auth
{
    [DisallowMultipleComponent]
    public sealed class RedeLabSessionStatusUI : MonoBehaviour
    {
        [SerializeField] private GameObject messageRoot;
        [SerializeField] private Text messageLabel;
        [SerializeField, TextArea] private string renewalRequiredMessage =
            "Sua sessao precisa ser renovada. O progresso sera sincronizado quando o acesso voltar.";

        private RedeLabAuthManager auth;

        private void Awake()
        {
            DisableRaycastTargets();
        }

        private void Start()
        {
            auth = RedeLabAuthManager.Instance;
            if (auth != null)
            {
                auth.OnSessionRenewalRequired += ShowRenewalRequired;
                auth.OnAuthSuccess += HandleAuthSuccess;
            }

            if (auth != null && auth.SessionRenewalRequired) ShowRenewalRequired(renewalRequiredMessage);
            else Hide();
        }

        private void OnDestroy()
        {
            if (auth == null) return;
            auth.OnSessionRenewalRequired -= ShowRenewalRequired;
            auth.OnAuthSuccess -= HandleAuthSuccess;
        }

        private void OnValidate()
        {
            DisableRaycastTargets();
            if (messageLabel != null && !Application.isPlaying) messageLabel.text = renewalRequiredMessage;
        }

        private void ShowRenewalRequired(string ignoredDetail)
        {
            if (messageLabel != null) messageLabel.text = renewalRequiredMessage;
            if (messageRoot != null) messageRoot.SetActive(true);
        }

        private void HandleAuthSuccess(RedeLabUser ignored)
        {
            Hide();
        }

        private void Hide()
        {
            if (messageRoot != null) messageRoot.SetActive(false);
        }

        private void DisableRaycastTargets()
        {
            if (messageRoot == null) return;
            foreach (Graphic graphic in messageRoot.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null) graphic.raycastTarget = false;
            }
        }
    }
}
