using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ProfessorSpeechBubbleUI : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text speechText;
    [SerializeField] private Transform worldAnchor;

    [Header("Conteúdo")]
    [SerializeField] private string receivedDocumentMessage = "Obrigado!";
    [SerializeField, Min(0f)] private float speechDuration = 3f;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;

    [Header("Posicionamento na tela")]
    [SerializeField] private Vector2 screenOffset = Vector2.zero;

    private Camera worldCamera;
    private float shownAt;
    private bool hasShown;
    private bool isVisible;

#if UNITY_EDITOR
    public void ConfigureEditor(Canvas targetCanvas, RectTransform targetPanel, CanvasGroup targetGroup,
        TMP_Text targetText, Transform targetWorldAnchor)
    {
        canvas = targetCanvas;
        panel = targetPanel;
        group = targetGroup;
        speechText = targetText;
        worldAnchor = targetWorldAnchor;
        receivedDocumentMessage = "Obrigado!";
        speechDuration = 3f;
        fadeDuration = 0.5f;
        screenOffset = Vector2.zero;
        if (speechText != null)
        {
            speechText.text = receivedDocumentMessage;
        }

        HideImmediately();
    }
#endif

    private void Awake()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
        }

        if (panel == null)
        {
            panel = transform as RectTransform;
        }

        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
        }

        if (speechText == null)
        {
            speechText = GetComponentInChildren<TMP_Text>(true);
        }

        if (canvas == null || panel == null || group == null || speechText == null || worldAnchor == null)
        {
            Debug.LogError("ProfessorSpeechBubbleUI está incompleto na cena O_escritorio.", this);
            enabled = false;
            return;
        }

        worldCamera = Camera.main;
        HideImmediately();
    }

    private void OnValidate()
    {
        speechDuration = Mathf.Max(0f, speechDuration);
        fadeDuration = Mathf.Max(0f, fadeDuration);
    }

    private void Update()
    {
        if (!isVisible)
        {
            return;
        }

        float now = Time.unscaledTime;
        float fade = Mathf.Max(0f, fadeDuration);
        float fadeInEndsAt = shownAt + fade;
        float fadeOutStartsAt = fadeInEndsAt + speechDuration;
        float hideAt = fadeOutStartsAt + fade;
        if (now >= hideAt)
        {
            isVisible = false;
            HideImmediately();
            return;
        }

        if (!UpdateProjectedPosition())
        {
            return;
        }

        if (fade <= 0f)
        {
            group.alpha = 1f;
        }
        else if (now < fadeInEndsAt)
        {
            group.alpha = Mathf.InverseLerp(shownAt, fadeInEndsAt, now);
        }
        else if (now < fadeOutStartsAt)
        {
            group.alpha = 1f;
        }
        else
        {
            group.alpha = 1f - Mathf.InverseLerp(fadeOutStartsAt, hideAt, now);
        }
    }

    private void OnDisable()
    {
        HideImmediately();
    }

    public void ShowOnce()
    {
        if (hasShown || !enabled)
        {
            return;
        }

        hasShown = true;
        speechText.text = receivedDocumentMessage;
        shownAt = Time.unscaledTime;
        isVisible = true;
        group.alpha = 0f;
        UpdateProjectedPosition();
    }

    private bool UpdateProjectedPosition()
    {
        if (worldCamera == null || !worldCamera.isActiveAndEnabled)
        {
            worldCamera = Camera.main;
        }

        if (worldCamera == null || canvas == null || panel == null || worldAnchor == null)
        {
            HideImmediately();
            return false;
        }

        Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldAnchor.position);
        bool onScreen = screenPoint.z > 0f
            && screenPoint.x >= 0f && screenPoint.x <= Screen.width
            && screenPoint.y >= 0f && screenPoint.y <= Screen.height;
        if (!onScreen)
        {
            group.alpha = 0f;
            return false;
        }

        RectTransform canvasRect = canvas.transform as RectTransform;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (canvasRect != null
            && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            panel.anchoredPosition = localPoint + screenOffset;
            return true;
        }

        group.alpha = 0f;
        return false;
    }

    private void HideImmediately()
    {
        if (group == null)
        {
            return;
        }

        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }
}
