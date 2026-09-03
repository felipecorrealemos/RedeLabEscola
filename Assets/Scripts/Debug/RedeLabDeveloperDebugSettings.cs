using System;
using System.Collections.Generic;
using RedeLabEscola.Auth;
using UnityEngine;

namespace RedeLabEscola.Debugging
{
    [CreateAssetMenu(
        fileName = "RedeLabDeveloperDebugSettings",
        menuName = "RedeLab Escola/Developer Debug Settings")]
    public sealed class RedeLabDeveloperDebugSettings : ScriptableObject
    {
        [Header("Disponibilidade")]
        [Tooltip("Desative antes de gerar uma build destinada aos alunos.")]
        [SerializeField] private bool allowDeveloperDebugPanel = true;

        [Header("Contas autorizadas")]
        [Tooltip("E-mails vindos da conta autenticada e sincronizada pela API. Não use nome de exibição.")]
        [SerializeField] private List<string> authorizedEmails = new List<string>();

        public bool AllowDeveloperDebugPanel => allowDeveloperDebugPanel;

        public bool IsAuthorized(RedeLabAuthManager auth)
        {
            return allowDeveloperDebugPanel
                && auth != null
                && auth.IsAuthenticated
                && IsAuthorizedEmail(auth.Email);
        }

        public bool IsAuthorizedEmail(string authenticatedEmail)
        {
            if (!allowDeveloperDebugPanel || string.IsNullOrWhiteSpace(authenticatedEmail)) return false;

            foreach (string allowedEmail in authorizedEmails)
            {
                if (string.Equals(
                    allowedEmail?.Trim(),
                    authenticatedEmail.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
