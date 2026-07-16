using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[ExecuteAlways]
public class NotebookWorldStatusUI : MonoBehaviour
{
    public enum PreviewMode
    {
        Runtime,
        WiFiOff,
        Searching,
        Connected
    }

    public enum PresentationMode
    {
        WorldSpace,
        ScreenSpaceProjected
    }

    private enum VisualState
    {
        Unknown,
        WiFiOff,
        Searching,
        Connected
    }

    [Header("Behavior")]
    [SerializeField] private bool enableWorldStatusUI = true;
    [SerializeField] private PresentationMode presentationMode = PresentationMode.ScreenSpaceProjected;
    [SerializeField, Min(0.05f)] private float statusPollInterval = 0.25f;
    [SerializeField, Min(0.1f)] private float appearanceDistance = 2.2f;
    [SerializeField] private Transform playerReference;
    [SerializeField, Min(0.1f)] private float playerSearchInterval = 1f;
    [SerializeField] private Vector2 screenSpaceOffset = new Vector2(0f, 48f);
    [SerializeField] private int screenSpaceSortingOrder = 50;
    [SerializeField, Min(0.2f)] private float maxVisibleDuration = 8f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.22f;
    [SerializeField, Min(0.05f)] private float reappearPlayerMoveDistance = 0.35f;
    [SerializeField, Min(1f)] private float reappearScreenMoveDistance = 28f;

    [Header("Preview")]
    [SerializeField] private PreviewMode previewMode = PreviewMode.Runtime;
    [SerializeField] private bool showPreviewInEditMode;
    [SerializeField] private bool showTitle;
    [SerializeField] private bool showStatusText;

    [Header("Wi-Fi State Source")]
    [SerializeField] private ComputerInteractable computerInteractable;
    [SerializeField] private WiFiDevice wiFiDevice;
    [SerializeField] private bool showConnectedNetworkName;

    [Header("World Space References")]
    [SerializeField] private Transform worldStatusAnchor;
    [SerializeField] private Canvas statusCanvas;
    [SerializeField] private RectTransform visualPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, InspectorName("Background Preto Semitransparente")] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;

    [Header("Icons")]
    [SerializeField] private GameObject iconWifiOff;
    [SerializeField] private GameObject iconWifiSearching;
    [SerializeField] private GameObject iconWifiConnected;
    [SerializeField] private Image iconWifiOffImage;
    [SerializeField] private Image iconWifiSearchingImage;
    [SerializeField] private Image iconWifiConnectedImage;
    [SerializeField] private Sprite wifiOffSprite;
    [SerializeField] private Sprite wifiSearchingSprite;
    [SerializeField] private Sprite wifiConnectedSprite;

    [Header("Colors")]
    [SerializeField] private Color wifiOffColor = new Color(0.8f, 0.22f, 0.2f, 1f);
    [SerializeField] private Color wifiSearchingColor = new Color(1f, 0.76f, 0.18f, 1f);
    [SerializeField] private Color wifiConnectedColor = new Color(0.16f, 0.72f, 0.34f, 1f);
    [SerializeField, InspectorName("Cor do Fundo Preto")] private Color backgroundColor = new Color(0.03f, 0.035f, 0.04f, 0.62f);
    [SerializeField] private Color textColor = Color.white;

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool rotateOnlyOnY;
    [SerializeField] private Camera cameraReference;

    private VisualState currentVisualState = VisualState.Unknown;
    private bool currentNearPlayer;
    private bool currentProjectedOnScreen = true;
    private bool previousProjectedOnScreen = true;
    private bool targetVisible;
    private bool runtimeStateInitialized;
    private bool stateTransitionPending;
    private bool hiddenByDuration;
    private bool hasLastShownPlayerPosition;
    private bool hasLastShownScreenPosition;
    private bool hasCurrentScreenPosition;
    private VisualState pendingVisualState = VisualState.Unknown;
    private string pendingStatusText;
    private Vector3 lastShownPlayerPosition;
    private Vector2 lastShownScreenPosition;
    private Vector2 currentScreenPosition;
    private float visibleUntilTime;
    private float nextStatusPollTime;
    private float nextPlayerSearchTime;
    private string currentStatusText;

    private void Reset()
    {
        CacheLocalReferences();
    }

    private void Awake()
    {
        CacheLocalReferences();
        ConfigureCanvasGroup();
        CacheCamera();
        CachePlayer();
        ForceRefresh();
    }

    private void OnEnable()
    {
        CacheLocalReferences();
        ConfigureCanvasGroup();
        ForceRefresh();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreview();
            return;
        }

        if (!enableWorldStatusUI)
        {
            SetVisible(false);
            return;
        }

        UpdateProjectedScreenPosition();

        if (Time.time >= nextStatusPollTime)
        {
            nextStatusPollTime = Time.time + Mathf.Max(statusPollInterval, 0.05f);
            RefreshStateIfNeeded();
            RefreshVisibilityIfNeeded();
        }

        UpdateTimedVisibility();
        UpdateCanvasAlpha();

        if (presentationMode == PresentationMode.WorldSpace)
        {
            ApplyBillboard();
        }
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && presentationMode == PresentationMode.WorldSpace)
        {
            ApplyBillboard();
        }
    }

    private void OnValidate()
    {
        statusPollInterval = Mathf.Max(statusPollInterval, 0.05f);
        appearanceDistance = Mathf.Max(appearanceDistance, 0.1f);
        playerSearchInterval = Mathf.Max(playerSearchInterval, 0.1f);
        CacheLocalReferences();
        ConfigureCanvasGroup();

        if (!Application.isPlaying)
        {
            ApplyEditorPreview();
        }
    }

    [ContextMenu("Refresh World Status Preview")]
    public void ForceRefresh()
    {
        RefreshStateIfNeeded(true);
        RefreshVisibilityIfNeeded(true);
    }

    public void AssignReferences(
        Transform anchor,
        RectTransform panel,
        CanvasGroup group,
        Image backgroundImage,
        Image borderImage,
        TMP_Text title,
        TMP_Text status,
        GameObject offIcon,
        GameObject searchingIcon,
        GameObject connectedIcon,
        Image offIconImage,
        Image searchingIconImage,
        Image connectedIconImage,
        ComputerInteractable computer,
        WiFiDevice wifi)
    {
        worldStatusAnchor = anchor;
        statusCanvas = group != null ? group.GetComponent<Canvas>() : statusCanvas;
        visualPanel = panel;
        canvasGroup = group;
        background = backgroundImage;
        border = borderImage;
        titleText = title;
        statusText = status;
        iconWifiOff = offIcon;
        iconWifiSearching = searchingIcon;
        iconWifiConnected = connectedIcon;
        iconWifiOffImage = offIconImage;
        iconWifiSearchingImage = searchingIconImage;
        iconWifiConnectedImage = connectedIconImage;
        computerInteractable = computer;
        wiFiDevice = wifi;
        ConfigureCanvasGroup();
        ForceRefresh();
    }

    public void ConfigureBillboardDefaults(bool shouldFaceCamera, bool shouldRotateOnlyOnY)
    {
        faceCamera = shouldFaceCamera;
        rotateOnlyOnY = shouldRotateOnlyOnY;
    }

    public void ConfigurePresentationDefaults(PresentationMode mode, Vector2 offset)
    {
        presentationMode = mode;
        screenSpaceOffset = offset;
    }

    public void ConfigureTimingDefaults(float visibleDuration, float fadeSeconds)
    {
        maxVisibleDuration = Mathf.Max(visibleDuration, 0.2f);
        fadeDuration = Mathf.Max(fadeSeconds, 0.01f);
    }

    public void ConfigureSpriteDefaults(Sprite offSprite, Sprite searchingSprite, Sprite connectedSprite, bool imageOnly)
    {
        wifiOffSprite = offSprite;
        wifiSearchingSprite = searchingSprite;
        wifiConnectedSprite = connectedSprite;
        showTitle = false;
        showStatusText = !imageOnly;
    }

    private void CacheLocalReferences()
    {
        if (computerInteractable == null)
        {
            computerInteractable = GetComponent<ComputerInteractable>();
        }

        if (wiFiDevice == null)
        {
            wiFiDevice = GetComponent<WiFiDevice>();
        }

        if (worldStatusAnchor == null)
        {
            Transform anchor = transform.Find("WorldStatusAnchor");
            if (anchor != null)
            {
                worldStatusAnchor = anchor;
            }
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        if (statusCanvas == null && canvasGroup != null)
        {
            statusCanvas = canvasGroup.GetComponent<Canvas>();
        }

        if (statusCanvas == null)
        {
            Transform canvasTransform = worldStatusAnchor != null ? worldStatusAnchor.Find("NotebookWorldStatusCanvas") : transform.Find("WorldStatusAnchor/NotebookWorldStatusCanvas");
            if (canvasTransform != null)
            {
                statusCanvas = canvasTransform.GetComponent<Canvas>();
            }
        }
    }

    private void ConfigureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (!Application.isPlaying)
        {
            canvasGroup.alpha = enableWorldStatusUI && showPreviewInEditMode ? 1f : 0f;
        }
    }

    private void RefreshStateIfNeeded(bool force = false)
    {
        VisualState nextState = ResolveVisualState();
        string nextStatusText = ResolveStatusText(nextState);

        if (stateTransitionPending && pendingVisualState == nextState && pendingStatusText == nextStatusText)
        {
            return;
        }

        if (!force && currentVisualState == nextState && currentStatusText == nextStatusText)
        {
            return;
        }

        bool changedRuntimeState = Application.isPlaying
            && runtimeStateInitialized
            && currentVisualState != VisualState.Unknown
            && currentVisualState != nextState;

        if (changedRuntimeState && currentNearPlayer && canvasGroup != null && canvasGroup.alpha > 0.01f)
        {
            pendingVisualState = nextState;
            pendingStatusText = nextStatusText;
            stateTransitionPending = true;
            SetVisible(false);
            return;
        }

        currentVisualState = nextState;
        currentStatusText = nextStatusText;
        ApplyVisualState(nextState, nextStatusText);

        if (Application.isPlaying)
        {
            runtimeStateInitialized = true;
            if (changedRuntimeState && currentNearPlayer)
            {
                ShowForLimitedTime();
            }
        }
    }

    private VisualState ResolveVisualState()
    {
        if (!Application.isPlaying && previewMode != PreviewMode.Runtime)
        {
            return PreviewToVisualState(previewMode);
        }

        if (computerInteractable == null || !computerInteractable.HasWiFiInterface || !computerInteractable.IsNotebookWiFiEnabled)
        {
            return VisualState.WiFiOff;
        }

        return computerInteractable.IsConnectedByWiFi ? VisualState.Connected : VisualState.Searching;
    }

    private string ResolveStatusText(VisualState state)
    {
        switch (state)
        {
            case VisualState.WiFiOff:
                return "Wi-Fi desligado";
            case VisualState.Connected:
                return showConnectedNetworkName ? ResolveConnectedNetworkLabel() : "Wi-Fi conectado";
            case VisualState.Searching:
                return "Sem rede conectada";
            default:
                return string.Empty;
        }
    }

    private string ResolveConnectedNetworkLabel()
    {
        NetworkScope activeScope = computerInteractable != null ? computerInteractable.ActiveNetworkScope : null;
        if (activeScope == null || string.IsNullOrWhiteSpace(activeScope.NetworkPrefix))
        {
            return "Wi-Fi conectado";
        }

        return "Conectado: " + activeScope.NetworkPrefix.TrimEnd('.');
    }

    private static VisualState PreviewToVisualState(PreviewMode mode)
    {
        switch (mode)
        {
            case PreviewMode.WiFiOff:
                return VisualState.WiFiOff;
            case PreviewMode.Connected:
                return VisualState.Connected;
            case PreviewMode.Searching:
            default:
                return VisualState.Searching;
        }
    }

    private void ApplyVisualState(VisualState state, string status)
    {
        Color stateColor = GetStateColor(state);

        if (titleText != null)
        {
            titleText.gameObject.SetActive(showTitle);
            titleText.text = showTitle ? "NOTEBOOK" : string.Empty;
            titleText.color = textColor;
        }

        if (statusText != null)
        {
            statusText.gameObject.SetActive(showStatusText);
            statusText.text = status;
            statusText.color = textColor;
            statusText.enableAutoSizing = true;
            statusText.fontSizeMin = 11f;
            statusText.fontSizeMax = 16f;
        }

        if (background != null)
        {
            background.color = backgroundColor;
        }

        if (border != null)
        {
            border.color = border.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
        }

        ApplyIconState(state, stateColor);
    }

    private Color GetStateColor(VisualState state)
    {
        switch (state)
        {
            case VisualState.WiFiOff:
                return wifiOffColor;
            case VisualState.Connected:
                return wifiConnectedColor;
            case VisualState.Searching:
            default:
                return wifiSearchingColor;
        }
    }

    private void ApplyIconState(VisualState state, Color stateColor)
    {
        SetIconActive(iconWifiOff, state == VisualState.WiFiOff);
        SetIconActive(iconWifiSearching, state == VisualState.Searching);
        SetIconActive(iconWifiConnected, state == VisualState.Connected);

        ApplyIconImage(iconWifiOffImage, wifiOffSprite, wifiOffColor);
        ApplyIconImage(iconWifiSearchingImage, wifiSearchingSprite, wifiSearchingColor);
        ApplyIconImage(iconWifiConnectedImage, wifiConnectedSprite, wifiConnectedColor);

    }

    private static void SetIconActive(GameObject iconObject, bool active)
    {
        if (iconObject != null && iconObject.activeSelf != active)
        {
            iconObject.SetActive(active);
        }
    }

    private static void ApplyIconImage(Image iconImage, Sprite sprite, Color color)
    {
        if (iconImage == null)
        {
            return;
        }

        iconImage.sprite = sprite;
        iconImage.color = sprite != null ? Color.white : color;
        iconImage.enabled = sprite != null;
    }

    private void RefreshVisibilityIfNeeded(bool force = false)
    {
        bool nearPlayer = !Application.isPlaying || (currentProjectedOnScreen && IsPlayerNear());
        bool cameBackToScreen = Application.isPlaying && !previousProjectedOnScreen && currentProjectedOnScreen;
        previousProjectedOnScreen = currentProjectedOnScreen;

        if (!force && currentNearPlayer == nearPlayer)
        {
            if (cameBackToScreen && nearPlayer)
            {
                ShowForLimitedTime();
            }
            else if (nearPlayer && ShouldReappearInSameNearSession())
            {
                ShowForLimitedTime();
            }

            return;
        }

        currentNearPlayer = nearPlayer;
        if (enableWorldStatusUI && nearPlayer)
        {
            ShowForLimitedTime();
        }
        else
        {
            SetVisible(false);
            if (force && Application.isPlaying && canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
        }
    }

    private bool IsPlayerNear()
    {
        CachePlayer();
        if (playerReference == null)
        {
            return false;
        }

        Vector3 playerPosition = playerReference.position;
        Vector3 statusPosition = worldStatusAnchor != null ? worldStatusAnchor.position : transform.position;
        playerPosition.y = statusPosition.y;
        return Vector3.SqrMagnitude(playerPosition - statusPosition) <= appearanceDistance * appearanceDistance;
    }

    private void CachePlayer()
    {
        if (!Application.isPlaying || playerReference != null || Time.time < nextPlayerSearchTime)
        {
            return;
        }

        nextPlayerSearchTime = Time.time + Mathf.Max(playerSearchInterval, 0.1f);
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerObject = GameObject.Find("Player");
        }

        if (playerObject != null)
        {
            playerReference = playerObject.transform;
        }
    }

    private void ShowForLimitedTime()
    {
        visibleUntilTime = Time.time + Mathf.Max(maxVisibleDuration, 0.2f);
        hiddenByDuration = false;
        CaptureReappearReferencePositions();
        SetVisible(true);
    }

    private void UpdateTimedVisibility()
    {
        if (!targetVisible || maxVisibleDuration <= 0f)
        {
            return;
        }

        if (Time.time >= visibleUntilTime)
        {
            hiddenByDuration = true;
            SetVisible(false);
        }
    }

    private bool ShouldReappearInSameNearSession()
    {
        if (!hiddenByDuration || targetVisible || stateTransitionPending)
        {
            return false;
        }

        if (PlayerMovedEnoughSinceLastShow())
        {
            return true;
        }

        return ScreenPositionMovedEnoughSinceLastShow();
    }

    private bool PlayerMovedEnoughSinceLastShow()
    {
        CachePlayer();
        if (!hasLastShownPlayerPosition || playerReference == null)
        {
            return false;
        }

        Vector3 currentPlayerPosition = playerReference.position;
        currentPlayerPosition.y = lastShownPlayerPosition.y;
        float threshold = Mathf.Max(reappearPlayerMoveDistance, 0.05f);
        return Vector3.SqrMagnitude(currentPlayerPosition - lastShownPlayerPosition) >= threshold * threshold;
    }

    private bool ScreenPositionMovedEnoughSinceLastShow()
    {
        if (!hasLastShownScreenPosition || !hasCurrentScreenPosition)
        {
            return false;
        }

        float threshold = Mathf.Max(reappearScreenMoveDistance, 1f);
        return (currentScreenPosition - lastShownScreenPosition).sqrMagnitude >= threshold * threshold;
    }

    private void CaptureReappearReferencePositions()
    {
        CachePlayer();
        if (playerReference != null)
        {
            lastShownPlayerPosition = playerReference.position;
            hasLastShownPlayerPosition = true;
        }

        if (hasCurrentScreenPosition)
        {
            lastShownScreenPosition = currentScreenPosition;
            hasLastShownScreenPosition = true;
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        targetVisible = visible;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void UpdateCanvasAlpha()
    {
        if (canvasGroup == null)
        {
            return;
        }

        float targetAlpha = targetVisible ? 1f : 0f;
        float duration = Mathf.Max(fadeDuration, 0.01f);
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime / duration);

        if (stateTransitionPending && canvasGroup.alpha <= 0.001f)
        {
            ApplyPendingStateTransition();
        }
    }

    private void ApplyPendingStateTransition()
    {
        stateTransitionPending = false;
        currentVisualState = pendingVisualState;
        currentStatusText = pendingStatusText;
        ApplyVisualState(currentVisualState, currentStatusText);
        ShowForLimitedTime();
    }

    private void ApplyBillboard()
    {
        Transform billboardTransform = ResolveBillboardTransform();
        if (!faceCamera || billboardTransform == null)
        {
            return;
        }

        CacheCamera();
        if (cameraReference == null)
        {
            return;
        }

        Vector3 direction = billboardTransform.position - cameraReference.transform.position;
        if (rotateOnlyOnY)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 upDirection = rotateOnlyOnY ? Vector3.up : cameraReference.transform.up;
        billboardTransform.rotation = Quaternion.LookRotation(direction.normalized, upDirection);
    }

    private void UpdateProjectedScreenPosition()
    {
        if (presentationMode != PresentationMode.ScreenSpaceProjected || visualPanel == null)
        {
            currentProjectedOnScreen = true;
            return;
        }

        CacheCamera();
        if (cameraReference == null)
        {
            currentProjectedOnScreen = false;
            return;
        }

        ConfigureCanvasForProjection();

        Vector3 worldPosition = worldStatusAnchor != null ? worldStatusAnchor.position : transform.position;
        Vector3 screenPoint = cameraReference.WorldToScreenPoint(worldPosition);
        currentProjectedOnScreen = screenPoint.z > 0f
            && screenPoint.x >= 0f
            && screenPoint.x <= Screen.width
            && screenPoint.y >= 0f
            && screenPoint.y <= Screen.height;

        if (!currentProjectedOnScreen || statusCanvas == null)
        {
            hasCurrentScreenPosition = false;
            return;
        }

        RectTransform canvasRect = statusCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
        currentScreenPosition = localPoint;
        hasCurrentScreenPosition = true;
        visualPanel.anchoredPosition = localPoint + screenSpaceOffset;
        visualPanel.localRotation = Quaternion.identity;
    }

    private void ConfigureCanvasForProjection()
    {
        if (statusCanvas == null)
        {
            return;
        }

        if (presentationMode == PresentationMode.ScreenSpaceProjected)
        {
            statusCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            statusCanvas.sortingOrder = screenSpaceSortingOrder;
        }
    }

    private Transform ResolveBillboardTransform()
    {
        if (canvasGroup != null)
        {
            return canvasGroup.transform;
        }

        return visualPanel != null ? visualPanel.transform : null;
    }

    private void CacheCamera()
    {
        if (cameraReference != null || !Application.isPlaying)
        {
            return;
        }

        cameraReference = Camera.main;
    }

    private void ApplyEditorPreview()
    {
        RefreshStateIfNeeded(true);
        SetVisible(enableWorldStatusUI && showPreviewInEditMode);
        if (canvasGroup != null)
        {
            canvasGroup.alpha = enableWorldStatusUI && showPreviewInEditMode ? 1f : 0f;
        }
    }
}
