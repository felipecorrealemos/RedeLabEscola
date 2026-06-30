using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RouterInteractable : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private string promptText = "Aperte F para interagir";
    [SerializeField] private NetworkScope networkScope;
    [SerializeField] private string networkPrefix = "192.168.0.";
    [SerializeField] private int routerAddress = 1;
    [SerializeField] private int firstDeviceAddress = 2;
    [SerializeField] private int availableAddressCount = 4;

    [Header("Panel")]
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.52f, 0.14f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.96f, 0.86f);
    [SerializeField] private float panelOpacity = 0.88f;
    [SerializeField] private float scrollSensitivity = 40f;

    [Header("Text")]
    [SerializeField] private int titleFontSize = 24;
    [SerializeField] private int hintFontSize = 16;
    [SerializeField] private int rangeFontSize = 14;
    [SerializeField] private int rowFontSize = 16;
    [SerializeField] private int statusFontSize = 14;

    [Header("Rows")]
    [SerializeField] private float rowHeight = 38f;
    [SerializeField] private float statusColumnWidth = 92f;
    [SerializeField] private float rowHorizontalPadding = 10f;

    [Header("Status Lights")]
    [SerializeField] private float greenLightBlinkFrequency = 4f;

    [Header("Editable UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject panelObject;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;
    [SerializeField] private Text titleLabel;
    [SerializeField] private Text closeHintLabel;
    [SerializeField] private Text rangeLabel;
    [SerializeField] private ScrollRect ipScrollRect;

    private bool isOpen;

    public event Action OnIpPoolChanged;
    public bool IsOpen => isOpen;
    public IReadOnlyList<NetworkScope.IpLease> Leases => ActiveNetworkScope != null ? ActiveNetworkScope.Leases : Array.Empty<NetworkScope.IpLease>();
    public NetworkScope ActiveNetworkScope => ResolveNetworkScope(false);
    public string RouterIpAddress => ActiveNetworkScope != null ? ActiveNetworkScope.RouterIpAddress : networkPrefix + routerAddress;

    private void Awake()
    {
        SetNetworkScope(ResolveNetworkScope(true));
        ResetRuntimePanel();
        isOpen = false;
        EnsureUi();
        RefreshIpRows();
        ForceUiClosed();
        EnsureGreenLightBlinkers();
    }

    private void Start()
    {
        ForceUiClosed();
    }

    private void OnValidate()
    {
        ResolveNetworkScope(false);
        ApplyUiSettings();
        RefreshIpRows();
    }

    private void OnDestroy()
    {
        if (networkScope != null)
        {
            networkScope.OnIpPoolChanged -= NotifyPoolChanged;
        }
    }

    [ContextMenu("Create Editable Router UI")]
    private void CreateEditableRouterUi()
    {
        EnsureUi();
        SetPromptVisible(false);
        SetPanelVisible(false);
        ApplyUiSettings();
        RefreshIpRows();
    }

    public void SetPromptVisible(bool visible)
    {
        EnsureUi();
        if (promptObject != null)
        {
            promptObject.SetActive(visible && !isOpen);
        }
    }

    public void Toggle(PlayerTopDownController player)
    {
        if (isOpen)
        {
            Close(player);
        }
        else
        {
            Open(player);
        }
    }

    public void Open(PlayerTopDownController player)
    {
        EnsureUi();
        RefreshIpRows();
        isOpen = true;
        SetPromptVisible(false);
        SetPanelVisible(true);
        player?.SetMovementLocked(true);
    }

    public void Close(PlayerTopDownController player)
    {
        isOpen = false;
        SetPanelVisible(false);
        player?.SetMovementLocked(false);
    }

    public bool TryAssignIp(ComputerInteractable computer, string address)
    {
        return TryAssignIp(computer, address, string.Empty);
    }

    public bool TryAssignIp(ComputerInteractable computer, string address, string reservedDeviceName)
    {
        if (computer == null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        NetworkScope scope = ResolveNetworkScope(true);
        if (scope == null)
        {
            return false;
        }

        return scope.TryAssignIp(computer, address, reservedDeviceName);
    }

    public void ReleaseIp(ComputerInteractable computer)
    {
        NetworkScope scope = ResolveNetworkScope(false);
        if (scope != null)
        {
            scope.ReleaseIp(computer);
        }
    }

    public RouterInteractable FindRouterForDevice(MovableDevice device)
    {
        return this;
    }

    private void NotifyPoolChanged()
    {
        RefreshIpRows();
        OnIpPoolChanged?.Invoke();
    }

    private NetworkScope ResolveNetworkScope(bool createIfMissing)
    {
        if (networkScope != null)
        {
            networkScope.SetOwnerRouter(this);
            return networkScope;
        }

        networkScope = GetComponentInParent<NetworkScope>();
        if (networkScope != null)
        {
            networkScope.SetOwnerRouter(this);
            return networkScope;
        }

        networkScope = FindExistingNetworkScopeForThisRouter();
        if (networkScope != null)
        {
            networkScope.Configure(networkPrefix, routerAddress, firstDeviceAddress, availableAddressCount, this);
            return networkScope;
        }

        if (!createIfMissing)
        {
            return null;
        }

        GameObject scopeObject = new GameObject("Network_" + networkPrefix.TrimEnd('.') + "_" + name.Replace(" ", "_"));
        scopeObject.transform.position = transform.position;
        scopeObject.transform.SetParent(GetNetworkScopeRoot(), true);
        networkScope = scopeObject.AddComponent<NetworkScope>();
        networkScope.Configure(networkPrefix, routerAddress, firstDeviceAddress, availableAddressCount, this);
        return networkScope;
    }

    private NetworkScope FindExistingNetworkScopeForThisRouter()
    {
        NetworkScope prefixMatchWithoutOwner = null;
        NetworkScope[] scopes = FindObjectsOfType<NetworkScope>(true);

        foreach (NetworkScope scope in scopes)
        {
            if (scope == null)
            {
                continue;
            }

            if (scope.OwnerRouter == this)
            {
                return scope;
            }

            if (prefixMatchWithoutOwner == null
                && scope.OwnerRouter == null
                && scope.NetworkPrefix == networkPrefix)
            {
                prefixMatchWithoutOwner = scope;
            }
        }

        return prefixMatchWithoutOwner;
    }

    private Transform GetNetworkScopeRoot()
    {
        GameObject root = GameObject.Find("Networks");
        if (root == null)
        {
            root = new GameObject("Networks");
        }

        return root.transform;
    }

    private void SetNetworkScope(NetworkScope scope)
    {
        if (networkScope == scope)
        {
            if (networkScope != null)
            {
                networkScope.OnIpPoolChanged -= NotifyPoolChanged;
                networkScope.OnIpPoolChanged += NotifyPoolChanged;
            }

            return;
        }

        if (networkScope != null)
        {
            networkScope.OnIpPoolChanged -= NotifyPoolChanged;
        }

        networkScope = scope;

        if (networkScope != null)
        {
            networkScope.OnIpPoolChanged -= NotifyPoolChanged;
            networkScope.OnIpPoolChanged += NotifyPoolChanged;
        }
    }

    private void EnsureUi()
    {
        if (canvas == null)
        {
            canvas = FindCanvasByName("InteractionCanvas");
        }

        if (canvas == null)
        {
            GameObject canvasObject = CreateUiObject("InteractionCanvas", null);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        EnsureEventSystem();
        EnsurePrompt();
        EnsurePanel();
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = CreateUiObject("EventSystem", null);
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private void EnsurePrompt()
    {
        if (promptObject == null && canvas != null)
        {
            Transform existingPrompt = canvas.transform.Find("RouterInteractionPrompt");
            if (existingPrompt != null)
            {
                promptObject = existingPrompt.gameObject;
                promptLabel = existingPrompt.GetComponentInChildren<Text>(true);
            }
        }

        if (promptObject == null)
        {
            promptObject = CreateUiObject("RouterInteractionPrompt", canvas.transform);
            promptObject.SetActive(false);
            RectTransform promptRect = promptObject.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 72f);
            promptRect.sizeDelta = new Vector2(360f, 48f);

            Image promptBackground = promptObject.AddComponent<Image>();
            promptBackground.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject labelObject = CreateUiObject("Text", promptObject.transform);
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            promptLabel = labelObject.AddComponent<Text>();
        }

        ApplyPromptSettings();
    }

    private void EnsurePanel()
    {
        if (panelObject == null && canvas != null)
        {
            Transform existingPanel = canvas.transform.Find("RouterIpPanel");
            if (existingPanel != null)
            {
                panelObject = existingPanel.gameObject;
            }
        }

        if (panelObject == null)
        {
            panelObject = CreateUiObject("RouterIpPanel", canvas.transform);
            panelObject.SetActive(false);
            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = panelAnchorMin;
            panelRect.anchorMax = panelAnchorMax;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(1f, 1f, 1f, panelOpacity);

        CreateHeader(panelObject.transform, "Roteador", "Esc ou F para fechar");
            CreateScrollList(panelObject.transform);
        }

        CachePanelReferences();
        ApplyPanelSettings();
        SetPanelVisible(isOpen);
    }

    private void CreateHeader(Transform parent, string title, string hint)
    {
        GameObject titleObject = CreateUiObject("Title", parent);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -18f);
        titleRect.sizeDelta = new Vector2(-48f, 34f);

        titleLabel = titleObject.AddComponent<Text>();
        titleLabel.text = title;
        titleLabel.fontStyle = FontStyle.Bold;

        GameObject rangeObject = CreateUiObject("Range", parent);
        RectTransform rangeRect = rangeObject.AddComponent<RectTransform>();
        rangeRect.anchorMin = new Vector2(0f, 1f);
        rangeRect.anchorMax = new Vector2(1f, 1f);
        rangeRect.pivot = new Vector2(0.5f, 1f);
        rangeRect.anchoredPosition = new Vector2(0f, -52f);
        rangeRect.sizeDelta = new Vector2(-48f, 24f);

        rangeLabel = rangeObject.AddComponent<Text>();

        CreateFooter(parent, hint);
    }

    private void CreateFooter(Transform parent, string hint)
    {
        GameObject footerObject = CreateUiObject("CloseHint", parent);
        RectTransform footerRect = footerObject.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 10f);
        footerRect.sizeDelta = new Vector2(-48f, 28f);

        closeHintLabel = footerObject.AddComponent<Text>();
        closeHintLabel.text = hint;
    }

    private void CreateScrollList(Transform parent)
    {
        GameObject scrollObject = CreateUiObject("IpScrollView", parent);
        RectTransform scrollRect = scrollObject.AddComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(24f, 50f);
        scrollRect.offsetMax = new Vector2(-24f, -88f);

        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.94f, 0.94f, 0.94f, 0.84f);

        ipScrollRect = scrollObject.AddComponent<ScrollRect>();
        ipScrollRect.horizontal = false;

        GameObject viewportObject = CreateUiObject("Viewport", scrollObject.transform);
        RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportObject.AddComponent<RectMask2D>();

        GameObject contentObject = CreateUiObject("Content", viewportObject.transform);
        RectTransform contentRect = contentObject.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(50, 50, 12, 12);
        layout.spacing = 8f;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ipScrollRect.viewport = viewportRect;
        ipScrollRect.content = contentRect;
    }

    private void RefreshIpRows()
    {
        if (ipScrollRect == null || ipScrollRect.content == null)
        {
            return;
        }

        ApplyContentPadding();

        for (int i = ipScrollRect.content.childCount - 1; i >= 0; i--)
        {
            DestroyImmediateSafe(ipScrollRect.content.GetChild(i).gameObject);
        }

        foreach (NetworkScope.IpLease lease in Leases)
        {
            string status = GetLeaseStatus(lease);
            bool available = lease.IsAvailable;
            CreateIpRow(ipScrollRect.content, lease.Address, status, available);
        }
    }

    private string GetLeaseStatus(NetworkScope.IpLease lease)
    {
        if (lease.IsRouter)
        {
            return "Roteador";
        }

        return lease.IsAvailable ? "Disponivel" : "Em uso";
    }

    private void CreateIpRow(Transform parent, string ipAddress, string status, bool available)
    {
        GameObject rowObject = CreateUiObject("IP_" + ipAddress, parent);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = available ? new Color(1f, 1f, 1f, 0.92f) : new Color(0.86f, 0.86f, 0.86f, 0.92f);

        HorizontalLayoutGroup rowLayoutGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowLayoutGroup.padding = new RectOffset(Mathf.RoundToInt(rowHorizontalPadding), Mathf.RoundToInt(rowHorizontalPadding), 0, 0);
        rowLayoutGroup.spacing = 8f;
        rowLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
        rowLayoutGroup.childForceExpandHeight = true;
        rowLayoutGroup.childForceExpandWidth = false;

        GameObject textObject = CreateUiObject("Text", rowObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement textLayout = textObject.AddComponent<LayoutElement>();
        textLayout.flexibleWidth = 1f;
        textLayout.minWidth = 120f;
        textLayout.preferredHeight = rowHeight;

        Text ipText = textObject.AddComponent<Text>();
        ipText.text = ipAddress;
        ipText.alignment = TextAnchor.MiddleCenter;
        ipText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
        ipText.font = GetDefaultFont();
        ipText.fontSize = rowFontSize;

        GameObject statusObject = CreateUiObject("Status", rowObject.transform);
        RectTransform statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.sizeDelta = new Vector2(statusColumnWidth, rowHeight);

        LayoutElement statusLayout = statusObject.AddComponent<LayoutElement>();
        statusLayout.minWidth = statusColumnWidth;
        statusLayout.preferredWidth = statusColumnWidth;
        statusLayout.preferredHeight = rowHeight;

        Text statusText = statusObject.AddComponent<Text>();
        statusText.text = status;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = available ? new Color(0.16f, 0.45f, 0.2f, 1f) : new Color(0.28f, 0.28f, 0.28f, 1f);
        statusText.font = GetDefaultFont();
        statusText.fontSize = statusFontSize;
    }

    private int GetLastDeviceAddress()
    {
        return ActiveNetworkScope != null ? ActiveNetworkScope.LastDeviceAddress : firstDeviceAddress + Mathf.Max(availableAddressCount, 1) - 1;
    }

    private void ApplyUiSettings()
    {
        ApplyPromptSettings();
        ApplyPanelSettings();
    }

    private void ApplyPromptSettings()
    {
        if (promptLabel == null && promptObject != null)
        {
            promptLabel = promptObject.GetComponentInChildren<Text>(true);
        }

        if (promptLabel == null)
        {
            return;
        }

        promptLabel.text = "Aperte F para interagir";
        promptLabel.alignment = TextAnchor.MiddleCenter;
        promptLabel.color = Color.white;
        promptLabel.font = GetDefaultFont();
        promptLabel.fontSize = 22;
    }

    private void ApplyPanelSettings()
    {
        if (panelObject == null)
        {
            return;
        }

        CachePanelReferences();

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = panelAnchorMin;
            panelRect.anchorMax = panelAnchorMax;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        Image panelImage = panelObject.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(1f, 1f, 1f, panelOpacity);
        }

        if (titleLabel != null)
        {
            titleLabel.text = "Roteador";
            titleLabel.alignment = TextAnchor.MiddleLeft;
            titleLabel.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            titleLabel.font = GetDefaultFont();
            titleLabel.fontSize = titleFontSize;
            titleLabel.fontStyle = FontStyle.Bold;
        }

        if (rangeLabel != null)
        {
            string prefix = ActiveNetworkScope != null ? ActiveNetworkScope.NetworkPrefix : networkPrefix;
            rangeLabel.text = "Range: " + RouterIpAddress + " - " + prefix + GetLastDeviceAddress();
            rangeLabel.alignment = TextAnchor.MiddleLeft;
            rangeLabel.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            rangeLabel.font = GetDefaultFont();
            rangeLabel.fontSize = rangeFontSize;
        }

        ApplyCloseHintLabel();

        if (ipScrollRect != null)
        {
            ipScrollRect.horizontal = false;
            ipScrollRect.scrollSensitivity = scrollSensitivity;
            ApplyContentPadding();
        }
    }

    private void ApplyContentPadding()
    {
        if (ipScrollRect == null || ipScrollRect.content == null)
        {
            return;
        }

        VerticalLayoutGroup layout = ipScrollRect.content.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            return;
        }

        layout.padding = new RectOffset(50, 50, 12, 12);
    }

    private void ApplyCloseHintLabel()
    {
        if (closeHintLabel == null)
        {
            return;
        }

        closeHintLabel.text = "Esc ou F para fechar";
        closeHintLabel.alignment = TextAnchor.MiddleLeft;
        closeHintLabel.color = new Color(0.35f, 0.35f, 0.35f, 1f);
        closeHintLabel.font = GetDefaultFont();
        closeHintLabel.fontSize = hintFontSize;
    }

    private void EnsureGreenLightBlinkers()
    {
        NetworkStatusLightBlinker.EnsureOnGreenLightRenderers(transform, greenLightBlinkFrequency);
        NetworkStatusLightBlinker.EnsureOnSceneGreenLightRenderers(greenLightBlinkFrequency);
    }

    private void CachePanelReferences()
    {
        if (panelObject == null)
        {
            return;
        }

        Transform title = panelObject.transform.Find("Title");
        if (title != null)
        {
            titleLabel = title.GetComponent<Text>();
        }

        Transform range = panelObject.transform.Find("Range");
        if (range != null)
        {
            rangeLabel = range.GetComponent<Text>();
        }

        Transform closeHint = panelObject.transform.Find("CloseHint");
        if (closeHint != null)
        {
            closeHintLabel = closeHint.GetComponent<Text>();
        }

        Transform scrollView = panelObject.transform.Find("IpScrollView");
        if (scrollView != null)
        {
            ipScrollRect = scrollView.GetComponent<ScrollRect>();
        }
    }

    private void ResetRuntimePanel()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Canvas targetCanvas = canvas != null ? canvas : FindCanvasByName("InteractionCanvas");
        if (targetCanvas == null)
        {
            return;
        }

        Transform existingPanel = targetCanvas.transform.Find("RouterIpPanel");
        if (existingPanel != null)
        {
            DestroyImmediateSafe(existingPanel.gameObject);
        }

        panelObject = null;
        titleLabel = null;
        closeHintLabel = null;
        rangeLabel = null;
        ipScrollRect = null;
    }

    private Canvas FindCanvasByName(string canvasName)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == canvasName)
            {
                return canvases[i];
            }
        }

        return FindObjectOfType<Canvas>();
    }

    private GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName);
        if (parent != null)
        {
            uiObject.transform.SetParent(parent, false);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RegisterCreatedObjectUndo(uiObject, "Create " + objectName);
            UnityEditor.EditorUtility.SetDirty(uiObject);
        }
#endif

        return uiObject;
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelObject != null)
        {
            panelObject.SetActive(visible);
        }
    }

    private void ForceUiClosed()
    {
        isOpen = false;
        SetPromptVisible(false);
        SetPanelVisible(false);
    }

    private void DestroyImmediateSafe(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
