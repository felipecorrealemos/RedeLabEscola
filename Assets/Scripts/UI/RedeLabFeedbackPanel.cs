using System;
using System.Collections;
using System.Globalization;
using System.Text;
using RedeLabEscola.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RedeLabEscola.UI
{
    [DisallowMultipleComponent]
    public sealed class RedeLabFeedbackPanel : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.075f, 0.09f, 0.11f, 0.98f);
        private static readonly Color PrimaryColor = new Color(0.12f, 0.58f, 0.82f, 1f);
        private static readonly Color SelectedColor = new Color(0.10f, 0.50f, 0.72f, 1f);
        private static readonly Color IdleColor = new Color(0.22f, 0.25f, 0.29f, 1f);

        private readonly RedeLabFeedbackDraft draft = new RedeLabFeedbackDraft();
        private TMP_InputField commentInput;
        private TMP_Text characterCounter;
        private TMP_Text statusLabel;
        private TMP_Text historyText;
        private TMP_Text historyStatus;
        private Button sendButton;
        private Button historyButton;
        private Button suggestionButton;
        private Button bugButton;
        private Button commentButton;
        private GameObject historyPanel;
        private Coroutine activeRequest;
        private RedeLabAuthManager subscribedAuth;
        private bool built;

        public RedeLabFeedbackUiState State { get; private set; } = RedeLabFeedbackUiState.Ready;
        public static string CurrentGameVersion => Application.version;
        public TMP_InputField CommentInput => commentInput;
        public Button SendButton => sendButton;
        public GameObject HistoryPanel => historyPanel;

        public void Build()
        {
            if (built) return;
            built = true;

            RectTransform root = CreateRect(transform, "Feedback", new Vector2(0.5f, 0f), Vector2.one);
            root.offsetMin = new Vector2(18f, 24f);
            root.offsetMax = new Vector2(-24f, -24f);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.08f, 0.10f, 0.12f, 0.78f);

            TMP_Text title = CreateText(root, "Title", "Ajude a melhorar o RedeLab", 27f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -62f), new Vector2(-24f, -16f));
            title.alignment = TextAlignmentOptions.Center;

            TMP_Text prompt = CreateText(root, "Prompt", "Encontrou algum problema ou tem uma sugestão? Conte para a gente.", 17f);
            SetRect(prompt.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -98f), new Vector2(-24f, -66f));
            prompt.alignment = TextAlignmentOptions.Center;

            suggestionButton = CreateButton(root, "Suggestion", "Sugestão", new Vector2(24f, -146f), new Vector2(166f, -108f));
            bugButton = CreateButton(root, "Bug", "Bug / Problema", new Vector2(178f, -146f), new Vector2(320f, -108f));
            commentButton = CreateButton(root, "Comment", "Comentário geral", new Vector2(332f, -146f), new Vector2(-24f, -108f), true);
            suggestionButton.onClick.AddListener(() => SelectType(RedeLabFeedbackValidation.SuggestionType));
            bugButton.onClick.AddListener(() => SelectType(RedeLabFeedbackValidation.BugType));
            commentButton.onClick.AddListener(() => SelectType(RedeLabFeedbackValidation.CommentType));

            commentInput = CreateInput(root);
            commentInput.onValueChanged.AddListener(OnCommentChanged);

            characterCounter = CreateText(root, "CharacterCounter", "0 / 1000", 15f);
            SetRect(characterCounter.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -398f), new Vector2(-24f, -370f));
            characterCounter.alignment = TextAlignmentOptions.Right;

            statusLabel = CreateText(root, "Status", string.Empty, 15f);
            SetRect(statusLabel.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -433f), new Vector2(-24f, -398f));
            statusLabel.alignment = TextAlignmentOptions.Left;

            sendButton = CreateButton(root, "Send", "Enviar feedback", new Vector2(24f, 24f), new Vector2(245f, 76f), false, false);
            historyButton = CreateButton(root, "History", "Meus comentários", new Vector2(260f, 24f), new Vector2(-24f, 76f), true, false);
            sendButton.onClick.AddListener(Submit);
            historyButton.onClick.AddListener(OpenHistory);

            BuildHistory(root);
            SelectType(RedeLabFeedbackValidation.SuggestionType);
            SubscribeToAuth();
            RefreshAvailability();
        }

        private void OnEnable()
        {
            if (!built) return;
            SubscribeToAuth();
            RefreshAvailability();
        }

        private void OnDisable()
        {
            UnsubscribeFromAuth();
        }

        private void OnDestroy()
        {
            UnsubscribeFromAuth();
        }

        private void SubscribeToAuth()
        {
            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            if (subscribedAuth == auth) return;
            UnsubscribeFromAuth();
            subscribedAuth = auth;
            if (subscribedAuth == null) return;
            subscribedAuth.OnAuthSuccess += OnAuthenticated;
            subscribedAuth.OnLogout += OnUnauthenticated;
            subscribedAuth.OnSessionRenewalRequired += OnSessionRenewalRequired;
        }

        private void UnsubscribeFromAuth()
        {
            if (subscribedAuth == null) return;
            subscribedAuth.OnAuthSuccess -= OnAuthenticated;
            subscribedAuth.OnLogout -= OnUnauthenticated;
            subscribedAuth.OnSessionRenewalRequired -= OnSessionRenewalRequired;
            subscribedAuth = null;
        }

        private void OnAuthenticated(RedeLabUser user)
        {
            State = RedeLabFeedbackUiState.Ready;
            SetStatus(string.Empty);
            RefreshAvailability();
        }

        private void OnUnauthenticated()
        {
            SetUnauthenticated("Entre novamente para enviar ou consultar seus comentários.");
        }

        private void OnSessionRenewalRequired(string message)
        {
            SetUnauthenticated(string.IsNullOrWhiteSpace(message)
                ? "Sua sessão expirou. Entre novamente para continuar."
                : message);
        }

        private void SelectType(string type)
        {
            if (!draft.SetType(type)) return;
            suggestionButton.image.color = type == RedeLabFeedbackValidation.SuggestionType ? SelectedColor : IdleColor;
            bugButton.image.color = type == RedeLabFeedbackValidation.BugType ? SelectedColor : IdleColor;
            commentButton.image.color = type == RedeLabFeedbackValidation.CommentType ? SelectedColor : IdleColor;
            RefreshAvailability();
        }

        private void OnCommentChanged(string value)
        {
            draft.SetComment(value);
            characterCounter.text = string.Format(CultureInfo.InvariantCulture, "{0} / {1}", value.Length, RedeLabFeedbackValidation.MaximumCommentLength);
            if (State == RedeLabFeedbackUiState.Error || State == RedeLabFeedbackUiState.Sent)
            {
                State = RedeLabFeedbackUiState.Ready;
                SetStatus(string.Empty);
            }
            RefreshAvailability();
        }

        private void Submit()
        {
            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                SetUnauthenticated("Entre novamente para enviar seu feedback.");
                return;
            }

            if (!draft.TryBeginSubmission(out string trimmedComment))
            {
                if (string.IsNullOrWhiteSpace(draft.Comment)) SetStatus("Escreva um comentário antes de enviar.", true);
                RefreshAvailability();
                return;
            }

            State = RedeLabFeedbackUiState.Sending;
            SetStatus("Enviando feedback...");
            RefreshAvailability();
            activeRequest = StartCoroutine(auth.SubmitFeedback(
                draft.Type,
                trimmedComment,
                CurrentGameVersion,
                OnSubmissionSucceeded,
                OnSubmissionFailed));
        }

        private void OnSubmissionSucceeded(RedeLabFeedback feedback)
        {
            activeRequest = null;
            draft.CompleteSuccess();
            commentInput.SetTextWithoutNotify(string.Empty);
            characterCounter.text = "0 / 1000";
            State = RedeLabFeedbackUiState.Sent;
            SetStatus("Feedback enviado. Obrigado!", false, true);
            RefreshAvailability();
        }

        private void OnSubmissionFailed(string error)
        {
            activeRequest = null;
            draft.CompleteFailure();
            State = RedeLabFeedbackUiState.Error;
            SetStatus(FriendlyError(error, "Não foi possível enviar. Seu texto foi mantido para tentar novamente."), true);
            RefreshAvailability();
        }

        private void OpenHistory()
        {
            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                SetUnauthenticated("Entre novamente para consultar seus comentários.");
                return;
            }

            if (State == RedeLabFeedbackUiState.LoadingHistory || draft.IsSending) return;
            historyPanel.SetActive(true);
            historyText.text = string.Empty;
            historyStatus.text = "Carregando seus comentários...";
            State = RedeLabFeedbackUiState.LoadingHistory;
            RefreshAvailability();
            activeRequest = StartCoroutine(auth.GetMyFeedback(OnHistoryLoaded, OnHistoryFailed));
        }

        private void OnHistoryLoaded(RedeLabFeedbackHistory history)
        {
            activeRequest = null;
            RedeLabFeedback[] feedbacks = RedeLabFeedbackValidation.NewestFirst(history != null ? history.feedbacks : null);
            if (feedbacks.Length == 0)
            {
                State = RedeLabFeedbackUiState.HistoryEmpty;
                historyStatus.text = "Você ainda não enviou nenhum feedback.";
                historyText.text = string.Empty;
            }
            else
            {
                State = RedeLabFeedbackUiState.HistoryLoaded;
                historyStatus.text = string.Empty;
                historyText.text = FormatHistory(feedbacks);
            }
            RefreshAvailability();
        }

        private void OnHistoryFailed(string error)
        {
            activeRequest = null;
            State = RedeLabFeedbackUiState.Error;
            historyStatus.text = FriendlyError(error, "Não foi possível carregar seus comentários. Tente novamente.");
            historyText.text = string.Empty;
            RefreshAvailability();
        }

        private void CloseHistory()
        {
            if (State == RedeLabFeedbackUiState.LoadingHistory && activeRequest != null)
            {
                StopCoroutine(activeRequest);
                activeRequest = null;
            }
            historyPanel.SetActive(false);
            State = RedeLabFeedbackUiState.Ready;
            RefreshAvailability();
        }

        private void SetUnauthenticated(string message)
        {
            State = RedeLabFeedbackUiState.Unauthenticated;
            SetStatus(message, true);
            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            if (!built) return;
            bool authenticated = RedeLabAuthManager.Instance != null && RedeLabAuthManager.Instance.IsAuthenticated;
            bool idle = !draft.IsSending && State != RedeLabFeedbackUiState.LoadingHistory;
            sendButton.interactable = authenticated && idle && draft.CanSubmit;
            historyButton.interactable = authenticated && idle;
            suggestionButton.interactable = idle;
            bugButton.interactable = idle;
            commentButton.interactable = idle;
            commentInput.interactable = idle;
        }

        private void SetStatus(string message, bool error = false, bool success = false)
        {
            statusLabel.text = message ?? string.Empty;
            statusLabel.color = error
                ? new Color(1f, 0.52f, 0.48f)
                : success ? new Color(0.46f, 0.92f, 0.61f) : new Color(0.78f, 0.84f, 0.88f);
        }

        private void BuildHistory(RectTransform parent)
        {
            RectTransform modal = CreateRect(parent, "FeedbackHistory", Vector2.zero, Vector2.one);
            modal.offsetMin = Vector2.zero;
            modal.offsetMax = Vector2.zero;
            historyPanel = modal.gameObject;
            Image modalImage = modal.gameObject.AddComponent<Image>();
            modalImage.color = PanelColor;

            TMP_Text title = CreateText(modal, "Title", "Meus comentários", 28f, FontStyles.Bold);
            SetRect(title.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -66f), new Vector2(-92f, -18f));
            title.alignment = TextAlignmentOptions.Left;

            Button close = CreateButton(modal, "Close", "Fechar", new Vector2(-90f, -66f), new Vector2(-20f, -20f), true);
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = Vector2.one;
            closeRect.anchorMax = Vector2.one;
            closeRect.offsetMin = new Vector2(-90f, -66f);
            closeRect.offsetMax = new Vector2(-20f, -20f);
            close.onClick.AddListener(CloseHistory);

            historyStatus = CreateText(modal, "Status", string.Empty, 18f);
            SetRect(historyStatus.rectTransform, Vector2.zero, Vector2.one, new Vector2(28f, 28f), new Vector2(-28f, -76f));
            historyStatus.alignment = TextAlignmentOptions.Center;

            RectTransform scrollRoot = CreateRect(modal, "HistoryScroll", Vector2.zero, Vector2.one);
            scrollRoot.offsetMin = new Vector2(24f, 24f);
            scrollRoot.offsetMax = new Vector2(-24f, -82f);
            Image scrollImage = scrollRoot.gameObject.AddComponent<Image>();
            scrollImage.color = new Color(0.04f, 0.05f, 0.065f, 0.8f);
            ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            RectTransform viewport = CreateRect(scrollRoot, "Viewport", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(12f, 12f);
            viewport.offsetMax = new Vector2(-12f, -12f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            historyText = CreateText(viewport, "Content", string.Empty, 16f);
            RectTransform content = historyText.rectTransform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            historyText.alignment = TextAlignmentOptions.TopLeft;
            historyText.enableWordWrapping = true;
            ContentSizeFitter fitter = historyText.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            historyPanel.SetActive(false);
        }

        private static TMP_InputField CreateInput(RectTransform parent)
        {
            RectTransform root = CreateRect(parent, "CommentInput", new Vector2(0f, 1f), Vector2.one);
            root.offsetMin = new Vector2(24f, -368f);
            root.offsetMax = new Vector2(-24f, -158f);
            Image background = root.gameObject.AddComponent<Image>();
            background.color = new Color(0.035f, 0.045f, 0.055f, 1f);
            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.characterLimit = RedeLabFeedbackValidation.MaximumCommentLength;
            input.lineType = TMP_InputField.LineType.MultiLineNewline;
            input.richText = false;

            RectTransform viewport = CreateRect(root, "Text Area", Vector2.zero, Vector2.one);
            viewport.offsetMin = new Vector2(14f, 12f);
            viewport.offsetMax = new Vector2(-14f, -12f);
            viewport.gameObject.AddComponent<RectMask2D>();

            TMP_Text placeholder = CreateText(viewport, "Placeholder", "Escreva aqui seu comentário...", 17f, FontStyles.Italic);
            placeholder.color = new Color(0.58f, 0.64f, 0.68f, 1f);
            placeholder.alignment = TextAlignmentOptions.TopLeft;
            Stretch(placeholder.rectTransform);

            TMP_Text text = CreateText(viewport, "Text", string.Empty, 17f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.enableWordWrapping = true;
            Stretch(text.rectTransform);

            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            return input;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 offsetMin,
            Vector2 offsetMax,
            bool rightAnchored = false,
            bool topAnchored = true)
        {
            Vector2 min = topAnchored ? new Vector2(0f, 1f) : Vector2.zero;
            Vector2 max = topAnchored ? new Vector2(rightAnchored ? 1f : 0f, 1f) : new Vector2(rightAnchored ? 1f : 0f, 0f);
            RectTransform rect = CreateRect(parent, name, min, max);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = IdleColor;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            TMP_Text text = CreateText(rect, "Text", label, topAnchored ? 16f : 18f, FontStyles.Bold);
            Stretch(text.rectTransform);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, float size, FontStyles style = FontStyles.Normal)
        {
            RectTransform rect = CreateRect(parent, name, Vector2.zero, Vector2.one);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static string FormatHistory(RedeLabFeedback[] feedbacks)
        {
            StringBuilder builder = new StringBuilder();
            foreach (RedeLabFeedback feedback in feedbacks)
            {
                if (feedback == null) continue;
                DateTime date = RedeLabFeedbackValidation.ParseDate(feedback.data_envio);
                string dateLabel = date == DateTime.MinValue
                    ? "Data não informada"
                    : date.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
                builder.Append("<b>").Append(RedeLabFeedbackValidation.DisplayLabel(feedback.tipo)).Append("</b>  •  ")
                    .Append(dateLabel);
                if (!string.IsNullOrWhiteSpace(feedback.versao_jogo))
                {
                    builder.Append("  •  Versão ").Append(Escape(feedback.versao_jogo));
                }
                builder.AppendLine().Append(Escape(feedback.comentario)).AppendLine().AppendLine();
            }
            return builder.ToString().TrimEnd();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty).Replace("<", "‹").Replace(">", "›");
        }

        private static string FriendlyError(string apiMessage, string fallback)
        {
            if (string.IsNullOrWhiteSpace(apiMessage)) return fallback;
            if (apiMessage.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0
                || apiMessage.IndexOf("sess", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Sua sessão expirou. Entre novamente para continuar.";
            }
            return fallback;
        }
    }
}
