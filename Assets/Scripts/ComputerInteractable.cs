using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ComputerInteractable : MonoBehaviour
{
    private const string WhiteMaterialPath = "Assets/Prefabs/materiais/branco.mat";
    private const string GrayMaterialPath = "Assets/Prefabs/materiais/cinza.mat";
    private const string RedMaterialPath = "Assets/Prefabs/materiais/vermelho.mat";
    private const string GreenMaterialPath = "Assets/Prefabs/materiais/verde.mat";
    private const string PanelBaseName = "ComputerIpPanel";

    [Header("Interaction")]
    [SerializeField] private string carryPromptText = "E pegar computador";
    [SerializeField] private string networkPromptText = "F configurar rede";
    [SerializeField] private string useComputerPromptText = "F usar computador";
    [SerializeField] private string deviceTitle = "Computador";
    [SerializeField] private bool stationaryNetworkDevice;
    [SerializeField] private string preferredIpAddress;
    [SerializeField] private string reservedDeviceName;
    [SerializeField] private Transform usePoint;
    [SerializeField] private float usePointRadius = 1.2f;
    [SerializeField] private Vector3 generatedUseColliderSize = new Vector3(1.2f, 0.35f, 0.85f);
    [SerializeField] private Vector2 terminalIndicatorSize = new Vector2(0.75f, 0.45f);
    [SerializeField] private Color terminalIndicatorColor = new Color(1f, 0.85f, 0.15f, 0.45f);
    [SerializeField] private float terminalIndicatorHeight = 0.03f;
    [SerializeField] private float terminalIndicatorPulseAmount = 0.12f;
    [SerializeField] private float terminalIndicatorPulseSpeed = 5f;

    [Header("Panel")]
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.64f, 0.12f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.98f, 0.9f);
    [SerializeField] private float panelOpacity = 0.9f;
    [SerializeField] private float scrollSensitivity = 40f;

    [Header("Rows")]
    [SerializeField] private float rowHeight = 40f;
    [SerializeField] private float statusColumnWidth = 88f;
    [SerializeField] private float rowHorizontalPadding = 8f;

    [Header("Status Light")]
    [SerializeField] private Renderer statusLightRenderer;
    [SerializeField] private Material offMaterial;
    [SerializeField] private Material noIpMaterial;
    [SerializeField] private Material connectedMaterial;
    [SerializeField] private float statusLightBlinkFrequency = 4f;
    [SerializeField] private Vector3 generatedLightLocalPosition = new Vector3(0.28f, 0.16f, -0.26f);
    [SerializeField] private Vector3 generatedLightLocalScale = new Vector3(0.12f, 0.08f, 0.04f);

    [Header("Monitor Screen")]
    [SerializeField] private Renderer monitorScreenRenderer;
    [SerializeField] private Material screenOffMaterial;
    [SerializeField] private Material screenOnMaterial;

    [Header("Editable UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject panelObject;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;
    [SerializeField] private Text titleLabel;
    [SerializeField] private Text closeHintLabel;
    [SerializeField] private Text selectedIpLabel;
    [SerializeField] private Button removeIpButton;
    [SerializeField] private Text removeIpButtonLabel;
    [SerializeField] private ScrollRect ipScrollRect;

    private MovableDevice movableDevice;
    private RouterInteractable router;
    private NetworkJackConnectionPoint connectedJack;
    private string assignedIp;
    private bool isOpen;
    private bool showingTerminalPanel;
    private Collider terminalInteractionCollider;
    private Transform terminalIndicator;
    private Renderer terminalIndicatorRenderer;
    private Material terminalIndicatorMaterial;
    private Vector3 terminalIndicatorBaseScale;

    public bool IsOpen => isOpen;
    public string AssignedIp => assignedIp;
    public bool IsConnectedToNetworkJack => stationaryNetworkDevice || (connectedJack != null && connectedJack.IsConnected(this));
    public bool CanInteract => stationaryNetworkDevice || (movableDevice != null && movableDevice.IsPlaced && IsConnectedToNetworkJack);
    public bool IsNetworkOperational => CanInteract && !string.IsNullOrWhiteSpace(assignedIp);
    public bool CanBePickedUp => movableDevice != null && !stationaryNetworkDevice && !IsNetworkOperational;
    public bool CanShowPrompt => stationaryNetworkDevice || (movableDevice != null && !movableDevice.IsCarried);

    private void Update()
    {
    }

    private void Awake()
    {
        movableDevice = GetComponent<MovableDevice>();
        ResolveMaterials();
        ResetRuntimePanel();
        EnsureNetworkJackPoints();
        EnsureUsePoint();
        EnsureNetworkDoorDevices();
        EnsureStatusLight();
        EnsureMonitorScreen();
        EnsureUi();
        SetPromptVisible(false);
        SetPanelVisible(false);
        UpdateStatusLight();
    }

    private void Start()
    {
        TryAssignPreferredIp();
    }

    public void ConfigureAsStationaryNetworkDevice(string title, string preferredIp, string reservedName)
    {
        stationaryNetworkDevice = true;
        usePoint = null;
        deviceTitle = string.IsNullOrWhiteSpace(title) ? deviceTitle : title;
        preferredIpAddress = preferredIp;
        reservedDeviceName = reservedName;
        ApplyUiSettings();
        UpdateStatusLight();
    }

    private void OnDestroy()
    {
        if (router != null)
        {
            router.OnIpPoolChanged -= RefreshIpRows;
            router.ReleaseIp(this);
        }
    }

    private void OnValidate()
    {
        ResolveMaterials();
        ApplyUiSettings();
        UpdateStatusLight();
    }

    public void HandlePlaced(DeviceDropZone dropZone)
    {
        EnsureNetworkJackPoints();
        EnsureNetworkDoorDevices();
        EnsureRouter();
        UpdateStatusLight();
        RefreshIpRows();
    }

    public void HandlePickedUp()
    {
        ReleaseCurrentIp();
        SetNetworkJack(null);
        SetPromptVisible(false);
        Close(null);
        UpdateStatusLight();
    }

    public void SetNetworkJack(NetworkJackConnectionPoint jack)
    {
        if (connectedJack == jack)
        {
            return;
        }

        if (connectedJack != null && jack == null)
        {
            ReleaseCurrentIp();
            Close(null);
        }

        connectedJack = jack;
        UpdateStatusLight();
        RefreshIpRows();
        ApplyPromptSettings();
    }

    public void SetPromptVisible(bool visible)
    {
        EnsureUi();
        ApplyPromptSettings();
        if (promptObject != null)
        {
            promptObject.SetActive(visible && CanShowPrompt && !isOpen);
        }
    }

    public void SetTerminalPromptVisible(bool visible)
    {
        EnsureUi();
        if (promptLabel != null)
        {
            promptLabel.text = useComputerPromptText;
            promptLabel.alignment = TextAnchor.MiddleCenter;
            promptLabel.color = Color.white;
            promptLabel.font = GetDefaultFont();
            promptLabel.fontSize = 18;
        }

        if (promptObject != null)
        {
            promptObject.SetActive(visible && IsNetworkOperational && !isOpen);
        }
    }

    public void Open(PlayerTopDownController player)
    {
        if (!CanInteract)
        {
            return;
        }

        EnsureRouter();
        EnsureNetworkDoorDevices();
        EnsureUi();
        showingTerminalPanel = false;
        RefreshIpRows();
        isOpen = true;
        SetPromptVisible(false);
        SetPanelVisible(true);
        player?.SetMovementLocked(true);
    }

    public void OpenTerminal(PlayerTopDownController player)
    {
        if (!IsNetworkOperational)
        {
            return;
        }

        EnsureNetworkDoorDevices();
        EnsureUi();
        showingTerminalPanel = true;
        RefreshIpRows();
        isOpen = true;
        SetTerminalPromptVisible(false);
        SetPanelVisible(true);
        player?.SetMovementLocked(true);
    }

    public void Close(PlayerTopDownController player)
    {
        isOpen = false;
        showingTerminalPanel = false;
        SetPanelVisible(false);
        player?.SetMovementLocked(false);
    }

    private void SelectIp(string ipAddress)
    {
        EnsureRouter();
        if (router == null || !router.TryAssignIp(this, ipAddress, reservedDeviceName))
        {
            return;
        }

        assignedIp = ipAddress;
        UpdateStatusLight();
        RefreshIpRows();
    }

    private void ReleaseCurrentIp()
    {
        if (router != null)
        {
            router.ReleaseIp(this);
        }

        assignedIp = string.Empty;
    }

    private void RemoveSelectedIp()
    {
        ReleaseCurrentIp();
        UpdateStatusLight();
        RefreshIpRows();
    }

    private void TryAssignPreferredIp()
    {
        if (!stationaryNetworkDevice || !string.IsNullOrWhiteSpace(assignedIp) || string.IsNullOrWhiteSpace(preferredIpAddress))
        {
            return;
        }

        SelectIp(preferredIpAddress);
    }

    private void EnsureRouter()
    {
        if (router != null)
        {
            return;
        }

        router = FindObjectOfType<RouterInteractable>();
        if (router != null)
        {
            router.OnIpPoolChanged -= RefreshIpRows;
            router.OnIpPoolChanged += RefreshIpRows;
        }
    }

    public bool IsPlayerNearUsePoint(Vector3 playerPosition)
    {
        EnsureUsePoint();
        if (!IsNetworkOperational || usePoint == null)
        {
            return true;
        }

        Vector3 usePosition = usePoint.position;
        usePosition.y = playerPosition.y;
        return Vector3.SqrMagnitude(usePosition - playerPosition) <= usePointRadius * usePointRadius;
    }

    public Vector3 GetInteractionPosition()
    {
        EnsureUsePoint();
        return !stationaryNetworkDevice && IsNetworkOperational && usePoint != null ? usePoint.position : transform.position;
    }

    public bool IsTerminalCollider(Collider candidate)
    {
        EnsureUsePoint();
        if (!IsNetworkOperational || usePoint == null || candidate == null)
        {
            return false;
        }

        KeyboardTerminalInteractable terminal = usePoint.GetComponent<KeyboardTerminalInteractable>();
        if (terminal != null)
        {
            return terminal.ContainsCollider(candidate);
        }

        return terminalInteractionCollider != null && candidate == terminalInteractionCollider;
    }

    private void EnsureUsePoint()
    {
        if (stationaryNetworkDevice)
        {
            usePoint = null;
            return;
        }

        if (usePoint != null)
        {
            return;
        }

        usePoint = FindKeyboardUsePoint();
        if (usePoint != null && usePoint.GetComponent<KeyboardTerminalInteractable>() == null)
        {
            usePoint.gameObject.AddComponent<KeyboardTerminalInteractable>();
        }
    }

    private Transform FindKeyboardUsePoint()
    {
        Transform keyboard = FindChildByName(transform, "Keyboard");
        if (keyboard != null)
        {
            return keyboard;
        }

        if (transform.parent != null)
        {
            keyboard = FindChildByName(transform.parent, "Keyboard");
            if (keyboard != null)
            {
                return keyboard;
            }
        }

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && candidate.name == "Keyboard")
            {
                return candidate;
            }
        }

        return null;
    }

    private void EnsureTerminalInteractionCollider()
    {
        if (usePoint == null || terminalInteractionCollider != null)
        {
            return;
        }

        Collider existingCollider = usePoint.GetComponent<Collider>();
        if (existingCollider != null)
        {
            terminalInteractionCollider = existingCollider;
            return;
        }

        BoxCollider generatedCollider = usePoint.gameObject.AddComponent<BoxCollider>();
        generatedCollider.isTrigger = true;
        generatedCollider.size = generatedUseColliderSize;
        terminalInteractionCollider = generatedCollider;
    }

    private void EnsureStatusLight()
    {
        if (statusLightRenderer == null)
        {
            Transform existingLight = transform.Find("Network_Status_Light");
            if (existingLight == null)
            {
                existingLight = transform.Find("Status_Light");
            }

            if (existingLight == null)
            {
                existingLight = transform.Find("Light");
            }

            if (existingLight != null)
            {
                statusLightRenderer = existingLight.GetComponent<Renderer>();
            }
        }

        if (statusLightRenderer == null)
        {
            statusLightRenderer = FindExistingStatusLightRenderer();
        }

        if (statusLightRenderer != null)
        {
            return;
        }

        GameObject lightObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        lightObject.name = "Network_Status_Light";
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = generatedLightLocalPosition;
        lightObject.transform.localScale = generatedLightLocalScale;
        Destroy(lightObject.GetComponent<Collider>());
        statusLightRenderer = lightObject.GetComponent<Renderer>();
    }

    private void UpdateStatusLight()
    {
        EnsureStatusLight();
        if (statusLightRenderer == null)
        {
            return;
        }

        Material targetMaterial = offMaterial;
        if (CanInteract)
        {
            targetMaterial = string.IsNullOrWhiteSpace(assignedIp) ? noIpMaterial : connectedMaterial;
        }

        if (targetMaterial != null)
        {
            statusLightRenderer.sharedMaterial = targetMaterial;
        }

        NetworkStatusLightBlinker.Ensure(statusLightRenderer, statusLightBlinkFrequency);
        UpdateMonitorScreen();
    }

    private void EnsureMonitorScreen()
    {
        if (stationaryNetworkDevice)
        {
            monitorScreenRenderer = null;
            return;
        }

        if (monitorScreenRenderer != null)
        {
            return;
        }

        Transform screen = FindChildByName(transform, "Monitor_Screen");
        if (screen == null && movableDevice != null && movableDevice.IsComputerDevice() && transform.parent != null)
        {
            screen = FindChildByName(transform.parent, "Monitor_Screen");
        }

        if (screen != null)
        {
            monitorScreenRenderer = screen.GetComponent<Renderer>();
        }
    }

    private void UpdateMonitorScreen()
    {
        if (stationaryNetworkDevice)
        {
            return;
        }

        EnsureMonitorScreen();
        if (monitorScreenRenderer == null)
        {
            return;
        }

        if (screenOffMaterial == null && monitorScreenRenderer.sharedMaterial != screenOnMaterial)
        {
            screenOffMaterial = monitorScreenRenderer.sharedMaterial;
        }

        Material targetMaterial = IsNetworkOperational ? screenOnMaterial : screenOffMaterial;
        if (targetMaterial != null)
        {
            monitorScreenRenderer.sharedMaterial = targetMaterial;
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
            Transform existingPrompt = canvas.transform.Find("ComputerInteractionPrompt");
            if (existingPrompt != null)
            {
                promptObject = existingPrompt.gameObject;
                promptLabel = existingPrompt.GetComponentInChildren<Text>(true);
            }
        }

        if (promptObject == null)
        {
            promptObject = CreateUiObject("ComputerInteractionPrompt", canvas.transform);
            RectTransform promptRect = promptObject.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 112f);
            promptRect.sizeDelta = new Vector2(460f, 58f);

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
            Transform existingPanel = canvas.transform.Find(GetPanelName());
            if (existingPanel != null)
            {
                panelObject = existingPanel.gameObject;
            }
        }

        if (panelObject == null)
        {
            panelObject = CreateUiObject(GetPanelName(), canvas.transform);
            RectTransform panelRect = panelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = panelAnchorMin;
            panelRect.anchorMax = panelAnchorMax;
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.color = new Color(1f, 1f, 1f, panelOpacity);

            CreateHeader(panelObject.transform);
            CreateScrollList(panelObject.transform);
        }

        CachePanelReferences();
        ApplyUiSettings();
        SetPanelVisible(isOpen);
    }

    private void CreateHeader(Transform parent)
    {
        GameObject titleObject = CreateUiObject("Title", parent);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -18f);
        titleRect.sizeDelta = new Vector2(-48f, 34f);

        titleLabel = titleObject.AddComponent<Text>();
        titleLabel.fontStyle = FontStyle.Bold;

        GameObject selectedObject = CreateUiObject("SelectedIp", parent);
        RectTransform selectedRect = selectedObject.AddComponent<RectTransform>();
        selectedRect.anchorMin = new Vector2(0f, 1f);
        selectedRect.anchorMax = new Vector2(1f, 1f);
        selectedRect.pivot = new Vector2(0.5f, 1f);
        selectedRect.anchoredPosition = new Vector2(0f, -52f);
        selectedRect.sizeDelta = new Vector2(-48f, 24f);

        selectedIpLabel = selectedObject.AddComponent<Text>();

        GameObject removeButtonObject = CreateUiObject("RemoveIpButton", parent);
        RectTransform removeButtonRect = removeButtonObject.AddComponent<RectTransform>();
        removeButtonRect.anchorMin = new Vector2(0f, 1f);
        removeButtonRect.anchorMax = new Vector2(1f, 1f);
        removeButtonRect.pivot = new Vector2(0.5f, 1f);
        removeButtonRect.anchoredPosition = new Vector2(0f, -80f);
        removeButtonRect.sizeDelta = new Vector2(-48f, 30f);

        Image removeButtonImage = removeButtonObject.AddComponent<Image>();
        removeButtonImage.color = new Color(0.9f, 0.24f, 0.18f, 0.92f);

        removeIpButton = removeButtonObject.AddComponent<Button>();
        removeIpButton.targetGraphic = removeButtonImage;
        removeIpButton.onClick.AddListener(RemoveSelectedIp);

        GameObject removeButtonLabelObject = CreateUiObject("Text", removeButtonObject.transform);
        RectTransform removeButtonLabelRect = removeButtonLabelObject.AddComponent<RectTransform>();
        removeButtonLabelRect.anchorMin = Vector2.zero;
        removeButtonLabelRect.anchorMax = Vector2.one;
        removeButtonLabelRect.offsetMin = Vector2.zero;
        removeButtonLabelRect.offsetMax = Vector2.zero;

        removeIpButtonLabel = removeButtonLabelObject.AddComponent<Text>();

        CreateFooter(parent);
    }

    private void CreateFooter(Transform parent)
    {
        GameObject footerObject = CreateUiObject("CloseHint", parent);
        RectTransform footerRect = footerObject.AddComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.anchoredPosition = new Vector2(0f, 10f);
        footerRect.sizeDelta = new Vector2(-48f, 28f);

        closeHintLabel = footerObject.AddComponent<Text>();
    }

    private void CreateScrollList(Transform parent)
    {
        GameObject scrollObject = CreateUiObject("IpScrollView", parent);
        RectTransform scrollRect = scrollObject.AddComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(20f, 50f);
        scrollRect.offsetMax = new Vector2(-20f, -124f);

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

        EnsureRouter();
        EnsureNetworkDoorDevices();
        ApplyContentPadding();

        for (int i = ipScrollRect.content.childCount - 1; i >= 0; i--)
        {
            DestroyImmediateSafe(ipScrollRect.content.GetChild(i).gameObject);
        }

        if (showingTerminalPanel)
        {
            CreateNetworkDeviceRows();
            ApplySelectedIpLabel();
            ApplyRemoveIpButton();
            return;
        }

        if (router == null)
        {
            CreateMessageRow("Nenhum roteador encontrado");
            return;
        }

        foreach (RouterInteractable.IpLease lease in router.Leases)
        {
            bool isSelected = lease.AssignedComputer == this;
            bool canSelect = !lease.IsRouter && (lease.IsAvailable || isSelected);
            string status = lease.IsRouter ? "Roteador" : (isSelected ? "Selecionado" : (lease.IsAvailable ? "Livre" : "Em uso"));
            CreateIpButtonRow(lease.Address, status, canSelect, isSelected);
        }

        ApplySelectedIpLabel();
        ApplyRemoveIpButton();
    }

    private void CreateNetworkDeviceRows()
    {
        Transform areaRoot = FindAreaRoot(transform);
        DualNetworkDoorController[] dualDoors = FindObjectsOfType<DualNetworkDoorController>(true);
        HashSet<NetworkDoorDevice> devicesControlledByDualDoors = new HashSet<NetworkDoorDevice>();
        NetworkDoorDevice[] doorDevices = FindObjectsOfType<NetworkDoorDevice>(true);
        List<NetworkDoorDevice> visibleDoorDevices = new List<NetworkDoorDevice>();
        bool createdAny = false;
        bool createdDualDoor = false;

        foreach (DualNetworkDoorController dualDoor in dualDoors)
        {
            if (dualDoor == null || !dualDoor.isActiveAndEnabled || !IsInSameArea(dualDoor.transform, areaRoot))
            {
                continue;
            }

            CreateDualNetworkDoorRow(dualDoor);
            createdAny = true;
            createdDualDoor = true;

            if (dualDoor.FirstDevice != null)
            {
                devicesControlledByDualDoors.Add(dualDoor.FirstDevice);
            }

            if (dualDoor.SecondDevice != null)
            {
                devicesControlledByDualDoors.Add(dualDoor.SecondDevice);
            }
        }

        foreach (NetworkDoorDevice device in doorDevices)
        {
            if (device == null || !device.isActiveAndEnabled || devicesControlledByDualDoors.Contains(device) || !IsInSameArea(device.transform, areaRoot))
            {
                continue;
            }

            visibleDoorDevices.Add(device);
        }

        if (!createdDualDoor && visibleDoorDevices.Count == 2)
        {
            CreateImplicitDualNetworkDoorRow(visibleDoorDevices[0], visibleDoorDevices[1]);
            createdAny = true;
            return;
        }

        foreach (NetworkDoorDevice device in visibleDoorDevices)
        {
            CreateNetworkDeviceRow(device);
            createdAny = true;
        }

        if (!createdAny)
        {
            CreateMessageRow("Nenhum dispositivo de rede encontrado");
        }
    }

    private void CreateImplicitDualNetworkDoorRow(NetworkDoorDevice firstDevice, NetworkDoorDevice secondDevice)
    {
        if (firstDevice == null || secondDevice == null)
        {
            return;
        }

        firstDevice.SetControlledByAccessGroup(true);
        secondDevice.SetControlledByAccessGroup(true);

        bool canOperate = firstDevice.CanOperate && secondDevice.CanOperate;
        bool isOpen = firstDevice.IsOpen && secondDevice.IsOpen;
        int connectedCount = GetConnectedDoorDeviceCount(firstDevice, secondDevice);

        GameObject rowObject = CreateUiObject("DualDoor_PortaDupla", ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = canOperate ? new Color(1f, 1f, 1f, 0.94f) : new Color(0.82f, 0.82f, 0.82f, 0.92f);

        GameObject textObject = CreateUiObject("Text", rowObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(rowHorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-92f, 0f);

        Text deviceText = textObject.AddComponent<Text>();
        deviceText.text = "Porta dupla - " + connectedCount + "/2" + (canOperate ? " - " + (isOpen ? "Aberta" : "Fechada") : " - Aguardando IP");
        deviceText.alignment = TextAnchor.MiddleLeft;
        deviceText.color = GetDoorProgressColor(connectedCount, 2);
        deviceText.font = GetDefaultFont();
        deviceText.fontSize = 14;
        deviceText.horizontalOverflow = HorizontalWrapMode.Wrap;
        deviceText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject buttonObject = CreateUiObject("Action", rowObject.transform);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-rowHorizontalPadding, 0f);
        buttonRect.sizeDelta = new Vector2(76f, -8f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = canOperate ? new Color(0.16f, 0.45f, 0.92f, 0.92f) : new Color(0.62f, 0.62f, 0.62f, 0.72f);

        Button button = buttonObject.AddComponent<Button>();
        button.interactable = canOperate;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(() =>
        {
            bool targetOpen = !(firstDevice.IsOpen && secondDevice.IsOpen);
            firstDevice.SetOpenFromAccessGroup(targetOpen);
            secondDevice.SetOpenFromAccessGroup(targetOpen);
            RefreshIpRows();
        });

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text buttonText = labelObject.AddComponent<Text>();
        buttonText.text = canOperate ? (isOpen ? "Fechar" : "Abrir") : "Sem IP";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.font = GetDefaultFont();
        buttonText.fontSize = 13;
        buttonText.fontStyle = FontStyle.Bold;
    }

    private void CreateDualNetworkDoorRow(DualNetworkDoorController dualDoor)
    {
        GameObject rowObject = CreateUiObject("DualDoor_" + dualDoor.DoorLabel, ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        bool canOperate = dualDoor.CanOperate;
        int connectedCount = GetConnectedDoorDeviceCount(dualDoor.FirstDevice, dualDoor.SecondDevice);
        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = canOperate ? new Color(1f, 1f, 1f, 0.94f) : new Color(0.82f, 0.82f, 0.82f, 0.92f);

        GameObject textObject = CreateUiObject("Text", rowObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(rowHorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-92f, 0f);

        Text deviceText = textObject.AddComponent<Text>();
        deviceText.text = dualDoor.DoorLabel + " - " + connectedCount + "/2" + (canOperate ? " - " + dualDoor.StateLabel : " - Aguardando IP");
        deviceText.alignment = TextAnchor.MiddleLeft;
        deviceText.color = GetDoorProgressColor(connectedCount, 2);
        deviceText.font = GetDefaultFont();
        deviceText.fontSize = 14;
        deviceText.horizontalOverflow = HorizontalWrapMode.Wrap;
        deviceText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject buttonObject = CreateUiObject("Action", rowObject.transform);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-rowHorizontalPadding, 0f);
        buttonRect.sizeDelta = new Vector2(76f, -8f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = canOperate ? new Color(0.16f, 0.45f, 0.92f, 0.92f) : new Color(0.62f, 0.62f, 0.62f, 0.72f);

        Button button = buttonObject.AddComponent<Button>();
        button.interactable = canOperate;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(() =>
        {
            dualDoor.Toggle();
            RefreshIpRows();
        });

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text buttonText = labelObject.AddComponent<Text>();
        buttonText.text = canOperate ? dualDoor.ActionLabel : "Sem IP";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.font = GetDefaultFont();
        buttonText.fontSize = 13;
        buttonText.fontStyle = FontStyle.Bold;
    }

    private int GetConnectedDoorDeviceCount(NetworkDoorDevice firstDevice, NetworkDoorDevice secondDevice)
    {
        int connectedCount = 0;
        if (firstDevice != null && firstDevice.CanOperate)
        {
            connectedCount++;
        }

        if (secondDevice != null && secondDevice.CanOperate)
        {
            connectedCount++;
        }

        return connectedCount;
    }

    private Color GetDoorProgressColor(int connectedCount, int requiredCount)
    {
        return connectedCount >= requiredCount
            ? new Color(0.05f, 0.45f, 0.16f, 1f)
            : new Color(0.65f, 0.12f, 0.1f, 1f);
    }

    private void CreateMessageRow(string message)
    {
        GameObject rowObject = CreateUiObject("Message", ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        Text text = rowObject.AddComponent<Text>();
        text.text = message;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        text.font = GetDefaultFont();
        text.fontSize = 16;
    }

    private void CreateIpButtonRow(string ipAddress, string status, bool canSelect, bool isSelected)
    {
        GameObject rowObject = CreateUiObject("IP_" + ipAddress, ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = isSelected ? new Color(0.8f, 1f, 0.84f, 0.96f) : (canSelect ? new Color(1f, 1f, 1f, 0.94f) : new Color(0.82f, 0.82f, 0.82f, 0.92f));

        Button button = rowObject.AddComponent<Button>();
        button.interactable = canSelect && !isSelected;
        button.targetGraphic = rowImage;
        if (button.interactable)
        {
            string selectedAddress = ipAddress;
            button.onClick.AddListener(() => SelectIp(selectedAddress));
        }

        GameObject textObject = CreateUiObject("Text", rowObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(rowHorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-(statusColumnWidth + rowHorizontalPadding), 0f);

        Text ipText = textObject.AddComponent<Text>();
        ipText.text = ipAddress;
        ipText.alignment = TextAnchor.MiddleCenter;
        ipText.color = canSelect ? new Color(0.1f, 0.1f, 0.1f, 1f) : new Color(0.42f, 0.42f, 0.42f, 1f);
        ipText.font = GetDefaultFont();
        ipText.fontSize = 14;
        ipText.horizontalOverflow = HorizontalWrapMode.Overflow;
        ipText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject statusObject = CreateUiObject("Status", rowObject.transform);
        RectTransform statusRect = statusObject.AddComponent<RectTransform>();
        statusRect.anchorMin = new Vector2(1f, 0f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(1f, 0.5f);
        statusRect.anchoredPosition = new Vector2(-rowHorizontalPadding, 0f);
        statusRect.sizeDelta = new Vector2(statusColumnWidth, 0f);

        Text statusText = statusObject.AddComponent<Text>();
        statusText.text = status;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = isSelected ? new Color(0.05f, 0.55f, 0.18f, 1f) : (canSelect ? new Color(0.16f, 0.45f, 0.2f, 1f) : new Color(0.38f, 0.38f, 0.38f, 1f));
        statusText.font = GetDefaultFont();
        statusText.fontSize = 12;
        statusText.fontStyle = isSelected ? FontStyle.Bold : FontStyle.Normal;
        statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
        statusText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void CreateNetworkDeviceRow(NetworkDoorDevice device)
    {
        GameObject rowObject = CreateUiObject("Device_" + device.DeviceLabel, ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = device.CanOperate ? new Color(1f, 1f, 1f, 0.94f) : new Color(0.82f, 0.82f, 0.82f, 0.92f);

        GameObject textObject = CreateUiObject("Text", rowObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(rowHorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-92f, 0f);

        Text deviceText = textObject.AddComponent<Text>();
        deviceText.text = device.DeviceLabel + " - " + (device.CanOperate ? device.StateLabel : "Sem IP");
        deviceText.alignment = TextAnchor.MiddleLeft;
        deviceText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        deviceText.font = GetDefaultFont();
        deviceText.fontSize = 14;
        deviceText.horizontalOverflow = HorizontalWrapMode.Wrap;
        deviceText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject buttonObject = CreateUiObject("Action", rowObject.transform);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-rowHorizontalPadding, 0f);
        buttonRect.sizeDelta = new Vector2(76f, -8f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = device.CanOperate ? new Color(0.16f, 0.45f, 0.92f, 0.92f) : new Color(0.62f, 0.62f, 0.62f, 0.72f);

        Button button = buttonObject.AddComponent<Button>();
        button.interactable = device.CanOperate;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(() =>
        {
            device.Toggle();
            RefreshIpRows();
        });

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text buttonText = labelObject.AddComponent<Text>();
        buttonText.text = device.CanOperate ? device.ActionLabel : "Sem IP";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.font = GetDefaultFont();
        buttonText.fontSize = 13;
        buttonText.fontStyle = FontStyle.Bold;
    }

    private void ApplyUiSettings()
    {
        ApplyPromptSettings();

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
            titleLabel.text = deviceTitle;
            titleLabel.alignment = TextAnchor.MiddleLeft;
            titleLabel.color = new Color(0.08f, 0.08f, 0.08f, 1f);
            titleLabel.font = GetDefaultFont();
            titleLabel.fontSize = 24;
            titleLabel.fontStyle = FontStyle.Bold;
        }

        ApplyCloseHintLabel();

        ApplySelectedIpLabel();
        ApplyRemoveIpButton();

        if (ipScrollRect != null)
        {
            ipScrollRect.horizontal = false;
            ipScrollRect.scrollSensitivity = scrollSensitivity;
        }

        ApplyContentPadding();
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

        if (CanInteract)
        {
            promptLabel.text = CanBePickedUp ? carryPromptText + "  |  " + networkPromptText : networkPromptText;
        }
        else
        {
            promptLabel.text = carryPromptText;
        }
        promptLabel.alignment = TextAnchor.MiddleCenter;
        promptLabel.color = Color.white;
        promptLabel.font = GetDefaultFont();
        promptLabel.fontSize = 18;
    }

    private void ApplySelectedIpLabel()
    {
        if (selectedIpLabel == null)
        {
            return;
        }

        selectedIpLabel.text = showingTerminalPanel ? "Dispositivos de rede" : (string.IsNullOrWhiteSpace(assignedIp) ? "IP selecionado: nenhum" : "IP selecionado: " + assignedIp);
        selectedIpLabel.alignment = TextAnchor.MiddleLeft;
        selectedIpLabel.color = string.IsNullOrWhiteSpace(assignedIp) ? new Color(0.55f, 0.16f, 0.12f, 1f) : new Color(0.05f, 0.45f, 0.16f, 1f);
        selectedIpLabel.font = GetDefaultFont();
        selectedIpLabel.fontSize = 14;
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
        closeHintLabel.fontSize = 14;
    }

    private void ApplyRemoveIpButton()
    {
        if (removeIpButton == null)
        {
            return;
        }

        bool hasSelectedIp = !string.IsNullOrWhiteSpace(assignedIp);
        removeIpButton.gameObject.SetActive(!showingTerminalPanel);
        removeIpButton.interactable = hasSelectedIp && !showingTerminalPanel;

        Image buttonImage = removeIpButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = hasSelectedIp ? new Color(0.9f, 0.24f, 0.18f, 0.92f) : new Color(0.62f, 0.62f, 0.62f, 0.72f);
        }

        if (removeIpButtonLabel == null)
        {
            removeIpButtonLabel = removeIpButton.GetComponentInChildren<Text>(true);
        }

        if (removeIpButtonLabel != null)
        {
            removeIpButtonLabel.text = "Remover IP";
            removeIpButtonLabel.alignment = TextAnchor.MiddleCenter;
            removeIpButtonLabel.color = Color.white;
            removeIpButtonLabel.font = GetDefaultFont();
            removeIpButtonLabel.fontSize = 14;
            removeIpButtonLabel.fontStyle = FontStyle.Bold;
        }
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

        Transform selectedIp = panelObject.transform.Find("SelectedIp");
        if (selectedIp != null)
        {
            selectedIpLabel = selectedIp.GetComponent<Text>();
        }

        Transform closeHint = panelObject.transform.Find("CloseHint");
        if (closeHint != null)
        {
            closeHintLabel = closeHint.GetComponent<Text>();
        }

        Transform removeButton = panelObject.transform.Find("RemoveIpButton");
        if (removeButton != null)
        {
            removeIpButton = removeButton.GetComponent<Button>();
            removeIpButtonLabel = removeButton.GetComponentInChildren<Text>(true);
        }

        Transform scrollView = panelObject.transform.Find("IpScrollView");
        if (scrollView != null)
        {
            ipScrollRect = scrollView.GetComponent<ScrollRect>();
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

        Transform existingPanel = targetCanvas.transform.Find(GetPanelName());
        if (existingPanel != null)
        {
            DestroyImmediateSafe(existingPanel.gameObject);
        }

        Transform legacySharedPanel = targetCanvas.transform.Find(PanelBaseName);
        if (legacySharedPanel != null)
        {
            DestroyImmediateSafe(legacySharedPanel.gameObject);
        }

        panelObject = null;
        titleLabel = null;
        selectedIpLabel = null;
        closeHintLabel = null;
        removeIpButton = null;
        removeIpButtonLabel = null;
        ipScrollRect = null;
    }

    private string GetPanelName()
    {
        return PanelBaseName + "_" + GetInstanceID();
    }

    private Renderer FindExistingStatusLightRenderer()
    {
        Renderer renderer = FindExistingStatusLightRenderer(transform);
        if (renderer != null)
        {
            return renderer;
        }

        if (transform.parent != null)
        {
            renderer = FindExistingStatusLightRenderer(transform.parent);
        }

        return renderer;
    }

    private Renderer FindExistingStatusLightRenderer(Transform searchRoot)
    {
        Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);
        Renderer firstMaterialMatch = null;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.transform == transform)
            {
                continue;
            }

            string lowerName = renderer.name.ToLowerInvariant();
            if (lowerName.Contains("light") || lowerName.Contains("luz") || lowerName.Contains("lamp") || lowerName.Contains("status"))
            {
                return renderer;
            }

            Material material = renderer.sharedMaterial;
            if (firstMaterialMatch == null && material != null)
            {
                string lowerMaterialName = material.name.ToLowerInvariant();
                if (lowerMaterialName.Contains("branco") || lowerMaterialName.Contains("vermelho") || lowerMaterialName.Contains("verde"))
                {
                    firstMaterialMatch = renderer;
                }
            }
        }

        return firstMaterialMatch;
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

    private void EnsureNetworkJackPoints()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (!lowerName.Contains("rj") && !lowerName.Contains("networkpoint"))
            {
                continue;
            }

            if (candidate.GetComponent<NetworkJackConnectionPoint>() == null)
            {
                candidate.gameObject.AddComponent<NetworkJackConnectionPoint>();
            }
        }
    }

    private void EnsureTerminalIndicator()
    {
        EnsureUsePoint();

        if (terminalIndicator != null)
        {
            if (usePoint != null && terminalIndicator.parent != usePoint)
            {
                terminalIndicator.SetParent(usePoint, false);
            }

            return;
        }

        Transform parent = usePoint != null ? usePoint : transform;
        Transform existingIndicator = parent.Find("KeyboardInteractionIndicator");
        if (existingIndicator == null && transform != parent)
        {
            existingIndicator = transform.Find("KeyboardInteractionIndicator");
        }

        if (existingIndicator != null)
        {
            terminalIndicator = existingIndicator;
            terminalIndicator.SetParent(parent, false);
        }
        else
        {
            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            indicatorObject.name = "KeyboardInteractionIndicator";
            terminalIndicator = indicatorObject.transform;
            terminalIndicator.SetParent(parent, false);
            Destroy(indicatorObject.GetComponent<Collider>());
        }

        terminalIndicator.rotation = Quaternion.Euler(-90f, 0f, 0f);
        terminalIndicatorBaseScale = new Vector3(terminalIndicatorSize.x, terminalIndicatorSize.y, 1f);
        terminalIndicator.localScale = terminalIndicatorBaseScale;

        terminalIndicatorRenderer = terminalIndicator.GetComponent<Renderer>();
        terminalIndicatorMaterial = new Material(GetIndicatorShader());
        terminalIndicatorMaterial.color = new Color(terminalIndicatorColor.r, terminalIndicatorColor.g, terminalIndicatorColor.b, 0f);
        terminalIndicatorRenderer.sharedMaterial = terminalIndicatorMaterial;
        terminalIndicator.gameObject.SetActive(false);
    }

    private void UpdateTerminalIndicator()
    {
        EnsureTerminalIndicator();
        bool shouldShow = IsNetworkOperational && IsPlayerNearUsePoint(GetPlayerPosition());
        terminalIndicator.gameObject.SetActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        terminalIndicator.position = GetTerminalIndicatorPosition();
        terminalIndicator.rotation = Quaternion.Euler(-90f, 0f, 0f);

        float pulse = 1f + (Mathf.Sin(Time.time * terminalIndicatorPulseSpeed) * 0.5f + 0.5f) * terminalIndicatorPulseAmount;
        terminalIndicator.localScale = terminalIndicatorBaseScale * pulse;
        terminalIndicatorMaterial.color = terminalIndicatorColor;
    }

    private Vector3 GetTerminalIndicatorPosition()
    {
        EnsureUsePoint();
        if (usePoint == null)
        {
            return transform.position + Vector3.up * terminalIndicatorHeight;
        }

        Renderer[] renderers = usePoint.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(usePoint.position, Vector3.zero);
        bool hasBounds = false;

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.transform == terminalIndicator)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds
            ? new Vector3(bounds.center.x, bounds.max.y + terminalIndicatorHeight, bounds.center.z)
            : usePoint.position + Vector3.up * terminalIndicatorHeight;
    }

    private Vector3 GetPlayerPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        return player != null ? player.transform.position : transform.position + Vector3.one * 999f;
    }

    private void EnsureNetworkDoorDevices()
    {
        NetworkDoorDevice[] existingDevices = FindObjectsOfType<NetworkDoorDevice>(true);
        foreach (NetworkDoorDevice existingDevice in existingDevices)
        {
            ConfigureDoorNetworkDevice(existingDevice);
        }

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            bool isDoorDeviceRoot = lowerName == "dispositivo porta" || lowerName.StartsWith("dispositivo porta ");
            bool parentIsDoorDevice = candidate.parent != null && candidate.parent.name.ToLowerInvariant().Contains("dispositivo porta");
            if (isDoorDeviceRoot && !parentIsDoorDevice)
            {
                NetworkDoorDevice doorDevice = candidate.GetComponent<NetworkDoorDevice>();
                if (doorDevice == null)
                {
                    doorDevice = candidate.gameObject.AddComponent<NetworkDoorDevice>();
                }

                ConfigureDoorNetworkDevice(doorDevice);
            }
        }
    }

    private void ConfigureDoorNetworkDevice(NetworkDoorDevice doorDevice)
    {
        if (doorDevice == null)
        {
            return;
        }

        ComputerInteractable doorNetwork = doorDevice.GetComponent<ComputerInteractable>();
        if (doorNetwork == null)
        {
            doorNetwork = doorDevice.gameObject.AddComponent<ComputerInteractable>();
        }

        DualNetworkDoorController accessGroup = FindAccessGroupForDoorDevice(doorDevice);
        bool useManualIp = accessGroup != null && accessGroup.RequiresManualIpAssignment;
        if (accessGroup != null)
        {
            doorDevice.SetControlledByAccessGroup(true);
        }

        string preferredIp = useManualIp || !doorDevice.AutoAssignPreferredIp ? string.Empty : doorDevice.PreferredIpAddress;
        doorNetwork.ConfigureAsStationaryNetworkDevice(doorDevice.DeviceLabel, preferredIp, doorDevice.DeviceLabel);
    }

    private DualNetworkDoorController FindAccessGroupForDoorDevice(NetworkDoorDevice doorDevice)
    {
        if (doorDevice == null)
        {
            return null;
        }

        DualNetworkDoorController[] accessGroups = FindObjectsOfType<DualNetworkDoorController>(true);
        foreach (DualNetworkDoorController accessGroup in accessGroups)
        {
            if (accessGroup != null && (accessGroup.Controls(doorDevice) || accessGroup.Controls(doorDevice.gameObject)))
            {
                return accessGroup;
            }
        }

        return null;
    }

    private bool IsInSameArea(Transform candidate, Transform areaRoot)
    {
        if (candidate == null || areaRoot == null)
        {
            return true;
        }

        Transform candidateArea = FindAreaRoot(candidate);
        return candidateArea == null || candidateArea == areaRoot;
    }

    private Transform FindAreaRoot(Transform candidate)
    {
        while (candidate != null)
        {
            string lowerName = candidate.name.ToLowerInvariant();
            if (lowerName == "sala" || lowerName.StartsWith("sala "))
            {
                return candidate;
            }

            candidate = candidate.parent;
        }

        return null;
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
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

    private void ResolveMaterials()
    {
#if UNITY_EDITOR
        if (offMaterial == null)
        {
            offMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(WhiteMaterialPath);
        }

        if (screenOffMaterial == null)
        {
            screenOffMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(GrayMaterialPath);
        }

        if (screenOnMaterial == null)
        {
            screenOnMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(WhiteMaterialPath);
        }

        if (noIpMaterial == null)
        {
            noIpMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(RedMaterialPath);
        }

        if (connectedMaterial == null)
        {
            connectedMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(GreenMaterialPath);
        }
#endif

        if (offMaterial == null)
        {
            offMaterial = CreateFallbackMaterial("Fallback_Computer_Light_White", Color.white);
        }

        if (noIpMaterial == null)
        {
            noIpMaterial = CreateFallbackMaterial("Fallback_Computer_Light_Red", Color.red);
        }

        if (connectedMaterial == null)
        {
            connectedMaterial = CreateFallbackMaterial("Fallback_Computer_Light_Green", Color.green);
        }

        if (screenOffMaterial == null)
        {
            screenOffMaterial = CreateFallbackMaterial("Fallback_Computer_Screen_Gray", Color.gray);
        }

        if (screenOnMaterial == null)
        {
            screenOnMaterial = CreateFallbackMaterial("Fallback_Computer_Screen_White", Color.white);
        }
    }

    private Material CreateFallbackMaterial(string materialName, Color color)
    {
        Material material = new Material(GetDefaultShader());
        material.name = materialName;
        material.color = color;
        return material;
    }

    private Shader GetDefaultShader()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return shader;
    }

    private Shader GetIndicatorShader()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = GetDefaultShader();
        }

        return shader;
    }
}
