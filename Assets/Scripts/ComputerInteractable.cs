using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ComputerInteractable : MonoBehaviour
{
    private const string WhiteMaterialPath = "Assets/Prefabs/materiais/branco.mat";
    private const string ScreenOnMaterialPath = "Assets/Prefabs/materiais/tela branca.mat";
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
    [HideInInspector]
    [FormerlySerializedAs("networkScope")]
    [SerializeField] private NetworkScope networkScopeOverride;
    [SerializeField, InspectorName("Rede detectada")] private NetworkScope detectedNetworkScope;
    [SerializeField, InspectorName("Origem da rede")] private string detectedNetworkSource = "Nenhuma rede detectada";
    [SerializeField] private string preferredIpAddress;
    [SerializeField] private string reservedDeviceName;
    [SerializeField] private Transform usePoint;
    [SerializeField] private float usePointRadius = 1.2f;

    [Header("Wi-Fi")]
    [SerializeField] private bool initialNotebookWiFiEnabled;

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
    [SerializeField] private Light monitorScreenSpotlight;
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
    private WiFiDevice wiFiDevice;
    private RouterInteractable router;
    private RouterInteractable selectedWiFiRouter;
    private NetworkJackConnectionPoint connectedJack;
    private DeviceDropZone currentDropZone;
    private string assignedIp;
    private bool isOpen;
    private bool showingTerminalPanel;
    private bool notebookWiFiEnabled;
    private bool connectedByWiFi;
    private Button wifiToggleButton;
    private Text wifiToggleLabel;

    public bool IsOpen => isOpen;
    public string AssignedIp => assignedIp;
    public string DeviceTitle => deviceTitle;
    public bool IsStationaryNetworkDevice => stationaryNetworkDevice;
    public NetworkScope ActiveNetworkScope => ResolveNetworkScope(false);
    public bool IsConnectedToNetworkJack => connectedJack != null && connectedJack.IsConnected(this);
    public bool HasWiFiInterface => wiFiDevice != null && wiFiDevice.DeviceType == WiFiDeviceType.Notebook;
    public bool IsNotebookWiFiEnabled => notebookWiFiEnabled;
    public bool IsConnectedByWiFi => connectedByWiFi;
    public bool CanInteract => IsConnectedToNetworkJack && (stationaryNetworkDevice || (movableDevice != null && movableDevice.IsPlaced));
    public bool CanConfigureNetwork => CanInteract || HasWiFiInterface;
    public bool IsNetworkOperational => !string.IsNullOrWhiteSpace(assignedIp) && (CanInteract || connectedByWiFi);
    public bool CanBePickedUp => movableDevice != null && !stationaryNetworkDevice && (!IsNetworkOperational || HasWiFiInterface);
    public bool CanShowPrompt => stationaryNetworkDevice || (movableDevice != null && !movableDevice.IsCarried);

    private void Awake()
    {
        movableDevice = GetComponent<MovableDevice>();
        wiFiDevice = GetComponent<WiFiDevice>();
        notebookWiFiEnabled = initialNotebookWiFiEnabled;
        ResolveMaterials();
        ApplyDeviceDefaults();
        ResetRuntimePanel();
        EnsureNetworkJackPoints();
        EnsureUsePoint();
        EnsureNetworkDoorDevices();
        EnsureNetworkPrinterDevices();
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
        StartCoroutine(UpdateStatusLightAfterFirstFrame());
    }

    private IEnumerator UpdateStatusLightAfterFirstFrame()
    {
        yield return null;
        UpdateStatusLight();
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

        if (selectedWiFiRouter != null && selectedWiFiRouter != router)
        {
            selectedWiFiRouter.ReleaseWiFiConnection(this);
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
        currentDropZone = dropZone;
        SetRouter(null);
        EnsureNetworkJackPoints();
        EnsureNetworkDoorDevices();
        EnsureNetworkPrinterDevices();
        EnsureRouter();
        UpdateStatusLight();
        RefreshIpRows();
    }

    public void HandlePickedUp()
    {
        bool keepWiFiConnectionWhileCarried = HasWiFiInterface && connectedByWiFi;
        if (!keepWiFiConnectionWhileCarried)
        {
            ReleaseCurrentIp();
            selectedWiFiRouter = null;
            connectedByWiFi = false;
        }

        currentDropZone = null;
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

        NetworkScope previousScope = ResolveNetworkScope(false);
        NetworkScope nextScope = jack != null ? jack.NetworkScope : null;
        if (connectedJack != null && (jack == null || (previousScope != null && nextScope != null && previousScope != nextScope)))
        {
            ReleaseCurrentIp();
            Close(null);
        }

        connectedJack = jack;
        if (connectedJack != null && connectedByWiFi)
        {
            ReleaseCurrentIp();
            selectedWiFiRouter = null;
            connectedByWiFi = false;
        }
        SetRouter(null);
        EnsureRouter();
        ReleaseAssignedIpIfOutsideActiveScope();
        TryAssignPreferredIp();
        UpdateStatusLight();
        RefreshIpRows();
        ApplyPromptSettings();
        MissionManager.NotifyNetworkDeviceStateChanged(this);
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
        if (!CanConfigureNetwork)
        {
            return;
        }

        if (!HasWiFiInterface || IsConnectedToNetworkJack)
        {
            EnsureRouter();
        }
        EnsureNetworkDoorDevices();
        EnsureNetworkPrinterDevices();
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
        EnsureNetworkPrinterDevices();
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
        RouterInteractable targetRouter = ResolveSelectedRouterForIpAssignment();
        if (targetRouter == null)
        {
            return;
        }

        bool assigned = connectedByWiFi || (HasWiFiInterface && notebookWiFiEnabled && selectedWiFiRouter == targetRouter && !IsConnectedToNetworkJack)
            ? targetRouter.TryConnectWiFi(this, wiFiDevice, ipAddress, reservedDeviceName)
            : targetRouter.TryAssignIp(this, ipAddress, reservedDeviceName);

        if (!assigned)
        {
            return;
        }

        SetRouter(targetRouter);
        connectedByWiFi = HasWiFiInterface && selectedWiFiRouter == targetRouter && !IsConnectedToNetworkJack;
        assignedIp = ipAddress;
        UpdateStatusLight();
        RefreshIpRows();
        MissionManager.NotifyNetworkDeviceConfigured(this);
    }

    private void ReleaseCurrentIp()
    {
        if (connectedByWiFi)
        {
            RouterInteractable wiFiRouter = selectedWiFiRouter != null ? selectedWiFiRouter : router;
            wiFiRouter?.ReleaseWiFiConnection(this);
        }
        else if (router != null)
        {
            router.ReleaseIp(this);
        }

        assignedIp = string.Empty;
        connectedByWiFi = false;
        MissionManager.NotifyNetworkDeviceStateChanged(this);
    }

    private void RemoveSelectedIp()
    {
        ReleaseCurrentIp();
        UpdateStatusLight();
        RefreshIpRows();
    }

    private void TryAssignPreferredIp()
    {
        if (!stationaryNetworkDevice || !string.IsNullOrWhiteSpace(assignedIp))
        {
            return;
        }

        NetworkScope scope = ResolveNetworkScope(false);
        string targetIpAddress = ResolvePreferredIpAddress(scope);
        if (!IsConnectedToNetworkJack || scope == null || string.IsNullOrWhiteSpace(targetIpAddress) || !scope.ContainsAddress(targetIpAddress))
        {
            return;
        }

        SelectIp(targetIpAddress);
    }

    private string ResolvePreferredIpAddress(NetworkScope scope)
    {
        return !string.IsNullOrWhiteSpace(preferredIpAddress) ? preferredIpAddress : string.Empty;
    }

    private RouterInteractable ResolveSelectedRouterForIpAssignment()
    {
        if (HasWiFiInterface && notebookWiFiEnabled && selectedWiFiRouter != null && !IsConnectedToNetworkJack)
        {
            return selectedWiFiRouter;
        }

        EnsureRouter();
        return router;
    }

    public void SetNotebookWiFiEnabled(bool enabled)
    {
        if (!HasWiFiInterface || notebookWiFiEnabled == enabled)
        {
            return;
        }

        notebookWiFiEnabled = enabled;
        if (!notebookWiFiEnabled)
        {
            ReleaseCurrentIp();
            selectedWiFiRouter = null;
        }

        RefreshIpRows();
        ApplyWiFiToggleButton();
        UpdateStatusLight();
    }

    public void ToggleNotebookWiFi()
    {
        SetNotebookWiFiEnabled(!notebookWiFiEnabled);
    }

    public void HandleWiFiAvailabilityChanged()
    {
        if (selectedWiFiRouter != null && (wiFiDevice == null || !wiFiDevice.IsRouterAvailable(selectedWiFiRouter)))
        {
            HandleWiFiRouterOutOfRange(selectedWiFiRouter);
        }

        RefreshIpRows();
    }

    public void HandleWiFiRouterOutOfRange(RouterInteractable lostRouter)
    {
        if (lostRouter == null || selectedWiFiRouter != lostRouter)
        {
            return;
        }

        if (connectedByWiFi)
        {
            ReleaseCurrentIp();
        }

        selectedWiFiRouter = null;
        if (router == lostRouter)
        {
            SetRouter(null);
        }

        RefreshIpRows();
        UpdateStatusLight();
    }

    public void HandleWiFiRouterDisabled(RouterInteractable disabledRouter)
    {
        if (disabledRouter == null || selectedWiFiRouter != disabledRouter)
        {
            return;
        }

        if (connectedByWiFi)
        {
            ReleaseCurrentIp();
        }

        selectedWiFiRouter = null;
        RefreshIpRows();
        UpdateStatusLight();
    }

    private void SelectWiFiRouter(RouterInteractable targetRouter)
    {
        if (!HasWiFiInterface || !notebookWiFiEnabled || targetRouter == null || wiFiDevice == null || !wiFiDevice.IsRouterAvailable(targetRouter))
        {
            return;
        }

        if (connectedByWiFi && selectedWiFiRouter != targetRouter)
        {
            ReleaseCurrentIp();
        }

        selectedWiFiRouter = targetRouter;
        SetRouter(targetRouter);
        RefreshIpRows();
    }

    private void EnsureRouter()
    {
        NetworkScope targetScope = ResolveNetworkScope(true);
        if (IsConnectedToNetworkJack && targetScope == null)
        {
            SetRouter(null);
            return;
        }

        if (router != null)
        {
            if (targetScope != null && router.ActiveNetworkScope == targetScope)
            {
                return;
            }

            if (targetScope == null && IsRouterInCurrentArea(router))
            {
                return;
            }
        }

        SetRouter(FindRouterForScope(targetScope));
    }

    private void SetRouter(RouterInteractable targetRouter)
    {
        if (router == targetRouter)
        {
            return;
        }

        if (router != null)
        {
            router.OnIpPoolChanged -= RefreshIpRows;
        }

        router = targetRouter;

        if (router != null)
        {
            router.OnIpPoolChanged -= RefreshIpRows;
            router.OnIpPoolChanged += RefreshIpRows;
        }
    }

    private RouterInteractable FindRouterForScope(NetworkScope targetScope)
    {
        RouterInteractable[] routers = FindObjectsOfType<RouterInteractable>(true);
        Transform areaRoot = FindAreaRoot(transform);
        RouterInteractable sameAreaFallback = null;

        foreach (RouterInteractable candidate in routers)
        {
            if (candidate == null)
            {
                continue;
            }

            if (targetScope != null && candidate.ActiveNetworkScope == targetScope)
            {
                return candidate;
            }

            if (sameAreaFallback == null && IsInSameArea(candidate.transform, areaRoot))
            {
                sameAreaFallback = candidate;
            }
        }

        if (targetScope == null && sameAreaFallback != null)
        {
            return sameAreaFallback;
        }

        return targetScope == null ? FindObjectOfType<RouterInteractable>() : null;
    }

    private NetworkScope ResolveNetworkScope(bool allowPreferredIpLookup)
    {
        NetworkScope resolvedScope = ResolveNetworkScopeInternal(out string sourceDescription);
        CacheDetectedNetworkScope(resolvedScope, sourceDescription);
        return resolvedScope;
    }

    private NetworkScope ResolveNetworkScopeInternal(out string sourceDescription)
    {
        if (networkScopeOverride != null)
        {
            sourceDescription = "Override manual";
            return networkScopeOverride;
        }

        if (connectedJack != null)
        {
            sourceDescription = "RJ-45: " + connectedJack.name;
            return connectedJack.NetworkScope;
        }

        if (currentDropZone != null && currentDropZone.NetworkScope != null)
        {
            sourceDescription = "Ponto de rede: " + currentDropZone.name;
            return currentDropZone.NetworkScope;
        }

        NetworkScope dropZoneAreaScope = FindNetworkScopeForAreaOwner(currentDropZone != null ? currentDropZone.transform : null);
        if (dropZoneAreaScope != null)
        {
            sourceDescription = "Roteador da area";
            return dropZoneAreaScope;
        }

        NetworkScope parentScope = GetComponentInParent<NetworkScope>();
        if (parentScope != null)
        {
            sourceDescription = "NetworkScope pai";
            return parentScope;
        }

        Transform areaRoot = FindAreaRoot(transform);
        if (areaRoot != null)
        {
            NetworkScope areaScope = areaRoot.GetComponentInChildren<NetworkScope>(true);
            if (areaScope != null)
            {
                sourceDescription = "NetworkScope da sala";
                return areaScope;
            }
        }

        sourceDescription = "Nenhuma rede detectada";
        return null;
    }

    private void CacheDetectedNetworkScope(NetworkScope resolvedScope, string sourceDescription)
    {
        detectedNetworkScope = resolvedScope;
        detectedNetworkSource = string.IsNullOrWhiteSpace(sourceDescription) ? "Nenhuma rede detectada" : sourceDescription;
    }

    private void ReleaseAssignedIpIfOutsideActiveScope()
    {
        if (string.IsNullOrWhiteSpace(assignedIp))
        {
            return;
        }

        NetworkScope scope = ResolveNetworkScope(false);
        if (scope != null && scope.ContainsAddress(assignedIp))
        {
            return;
        }

        ReleaseCurrentIp();
    }

    private bool IsRouterInCurrentArea(RouterInteractable candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        Transform areaOwner = connectedJack != null ? connectedJack.transform : (currentDropZone != null ? currentDropZone.transform : transform);
        return IsInSameArea(candidate.transform, FindAreaRoot(areaOwner));
    }

    private NetworkScope FindNetworkScopeForAreaOwner(Transform areaOwner)
    {
        Transform areaRoot = FindAreaRoot(areaOwner);
        if (areaRoot == null)
        {
            return null;
        }

        RouterInteractable[] routers = FindObjectsOfType<RouterInteractable>(true);
        foreach (RouterInteractable candidate in routers)
        {
            if (candidate != null && IsInSameArea(candidate.transform, areaRoot))
            {
                return candidate.ActiveNetworkScope;
            }
        }

        return null;
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

        return false;
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
        else if (connectedByWiFi)
        {
            targetMaterial = connectedMaterial;
        }
        else if (HasWiFiInterface && notebookWiFiEnabled)
        {
            targetMaterial = noIpMaterial;
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
        if (stationaryNetworkDevice || IsPrinterDevice())
        {
            monitorScreenRenderer = null;
            monitorScreenSpotlight = null;
            return;
        }

        if (monitorScreenRenderer != null && monitorScreenSpotlight != null)
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
            if (monitorScreenRenderer == null)
            {
                monitorScreenRenderer = screen.GetComponent<Renderer>();
            }

            if (monitorScreenSpotlight == null)
            {
                monitorScreenSpotlight = FindSpotlightInChildren(screen);
            }
        }
    }

    private Light FindSpotlightInChildren(Transform parent)
    {
        Light[] lights = parent.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Spot)
            {
                return lights[i];
            }
        }

        return lights.Length > 0 ? lights[0] : null;
    }

    private void UpdateMonitorScreen()
    {
        if (stationaryNetworkDevice || IsPrinterDevice())
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

        if (monitorScreenSpotlight != null)
        {
            bool shouldEnableSpotlight = IsNetworkOperational;
            if (monitorScreenSpotlight.gameObject.activeSelf != shouldEnableSpotlight)
            {
                monitorScreenSpotlight.gameObject.SetActive(shouldEnableSpotlight);
            }

            monitorScreenSpotlight.enabled = shouldEnableSpotlight;
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

        CreateWiFiToggleButton(parent);

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

    private void CreateWiFiToggleButton(Transform parent)
    {
        GameObject buttonObject = CreateUiObject("WiFiToggleButton", parent);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 1f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 1f);
        buttonRect.anchoredPosition = new Vector2(-24f, -22f);
        buttonRect.sizeDelta = new Vector2(108f, 28f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        wifiToggleButton = buttonObject.AddComponent<Button>();
        wifiToggleButton.targetGraphic = buttonImage;
        wifiToggleButton.onClick.AddListener(ToggleNotebookWiFi);

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        wifiToggleLabel = labelObject.AddComponent<Text>();
        ApplyWiFiToggleButton();
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
        EnsureNetworkPrinterDevices();
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

        if (HasWiFiInterface && !IsConnectedToNetworkJack)
        {
            if (!notebookWiFiEnabled)
            {
                CreateMessageRow("Wi-Fi desligado");
                ApplySelectedIpLabel();
                ApplyRemoveIpButton();
                return;
            }

            if (selectedWiFiRouter == null)
            {
                CreateWiFiNetworkRows();
                ApplySelectedIpLabel();
                ApplyRemoveIpButton();
                return;
            }
        }

        RouterInteractable visibleRouter = HasWiFiInterface && !IsConnectedToNetworkJack && selectedWiFiRouter != null ? selectedWiFiRouter : router;
        if (visibleRouter == null)
        {
            CreateMessageRow("Nenhum roteador encontrado");
            return;
        }

        foreach (NetworkScope.IpLease lease in visibleRouter.Leases)
        {
            bool isSelected = lease.AssignedComputer == this;
            bool canSelect = !lease.IsRouter && (lease.IsAvailable || isSelected);
            string status = lease.IsRouter ? "Roteador" : (isSelected ? NetworkScope.GetConnectionTypeLabel(lease.ConnectionType) : (lease.IsAvailable ? "Livre" : NetworkScope.GetConnectionTypeLabel(lease.ConnectionType)));
            CreateIpButtonRow(lease.Address, status, canSelect, isSelected);
        }

        ApplySelectedIpLabel();
        ApplyRemoveIpButton();
    }

    private void CreateWiFiNetworkRows()
    {
        if (wiFiDevice == null || wiFiDevice.AvailableRouters.Count == 0)
        {
            CreateMessageRow("Nenhuma rede Wi-Fi ao alcance");
            return;
        }

        bool createdAny = false;
        foreach (RouterInteractable availableRouter in wiFiDevice.AvailableRouters)
        {
            if (availableRouter == null || !availableRouter.IsWiFiEnabled)
            {
                continue;
            }

            CreateWiFiNetworkRow(availableRouter);
            createdAny = true;
        }

        if (!createdAny)
        {
            CreateMessageRow("Nenhuma rede Wi-Fi ao alcance");
        }
    }

    private void CreateWiFiNetworkRow(RouterInteractable availableRouter)
    {
        GameObject rowObject = CreateUiObject("WiFi_" + availableRouter.WiFiNetworkName, ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = new Color(1f, 1f, 1f, 0.94f);

        Button button = rowObject.AddComponent<Button>();
        button.targetGraphic = rowImage;
        button.onClick.AddListener(() => SelectWiFiRouter(availableRouter));

        Text text = CreateFullRowText(rowObject.transform, "Rede " + availableRouter.WiFiNetworkName, new Color(0.1f, 0.1f, 0.1f, 1f), FontStyle.Normal);
        text.fontSize = 14;
    }

    private void CreateNetworkDeviceRows()
    {
        Transform areaRoot = FindAreaRoot(transform);
        DualNetworkDoorController[] dualDoors = FindObjectsOfType<DualNetworkDoorController>(true);
        HashSet<NetworkDoorDevice> devicesControlledByDualDoors = new HashSet<NetworkDoorDevice>();
        NetworkDoorDevice[] doorDevices = FindObjectsOfType<NetworkDoorDevice>(true);
        NetworkPrinterDevice[] printerDevices = FindObjectsOfType<NetworkPrinterDevice>(true);
        List<NetworkDoorDevice> visibleDoorDevices = new List<NetworkDoorDevice>();
        Dictionary<Transform, NetworkPrinterDevice> visiblePrinterDevices = new Dictionary<Transform, NetworkPrinterDevice>();
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
            visibleDoorDevices.Clear();
        }

        foreach (NetworkDoorDevice device in visibleDoorDevices)
        {
            CreateNetworkDeviceRow(device);
            createdAny = true;
        }

        foreach (NetworkPrinterDevice printer in printerDevices)
        {
            if (printer == null || !printer.isActiveAndEnabled)
            {
                continue;
            }

            NetworkPrinterDevice canonicalPrinter = ResolveCanonicalPrinterDevice(printer);
            if (canonicalPrinter == null || !canonicalPrinter.isActiveAndEnabled || !IsInSameArea(canonicalPrinter.transform, areaRoot))
            {
                continue;
            }

            Transform printerRoot = ResolvePrinterRootTransform(canonicalPrinter.transform);
            if (printerRoot == null)
            {
                printerRoot = canonicalPrinter.transform;
            }

            if (!visiblePrinterDevices.TryGetValue(printerRoot, out NetworkPrinterDevice existingPrinter)
                || (!existingPrinter.CanPrint && canonicalPrinter.CanPrint))
            {
                visiblePrinterDevices[printerRoot] = canonicalPrinter;
            }
        }

        foreach (NetworkPrinterDevice printer in visiblePrinterDevices.Values)
        {
            CreatePrinterDeviceRow(printer);
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

        bool canOperate = firstDevice.CanOperate
            && secondDevice.CanOperate
            && MissionManager.CanOperateDoorCommand(firstDevice)
            && MissionManager.CanOperateDoorCommand(secondDevice);
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
            MissionManager.NotifyDualDoorsStateChanged(targetOpen);
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

    private void CreatePrinterDeviceRow(NetworkPrinterDevice printer)
    {
        GameObject rowObject = CreateUiObject("Printer_" + printer.DeviceLabel, ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        bool canPrintDocument = printer.CanPrint && !printer.HasPrintedDocument;

        Image rowImage = rowObject.AddComponent<Image>();
        rowImage.color = printer.CanPrint ? new Color(1f, 1f, 1f, 0.94f) : new Color(0.82f, 0.82f, 0.82f, 0.92f);

        GameObject textObject = CreateUiObject("Text", rowObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(rowHorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-148f, 0f);

        Text printerText = textObject.AddComponent<Text>();
        printerText.text = printer.DeviceLabel + " - " + printer.StateLabel;
        printerText.alignment = TextAnchor.MiddleLeft;
        printerText.color = new Color(0.1f, 0.1f, 0.1f, 1f);
        printerText.font = GetDefaultFont();
        printerText.fontSize = 14;
        printerText.horizontalOverflow = HorizontalWrapMode.Wrap;
        printerText.verticalOverflow = VerticalWrapMode.Truncate;

        GameObject buttonObject = CreateUiObject("Action", rowObject.transform);
        RectTransform buttonRect = buttonObject.AddComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1f, 0f);
        buttonRect.anchorMax = new Vector2(1f, 1f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-rowHorizontalPadding, 0f);
        buttonRect.sizeDelta = new Vector2(132f, -8f);

        Image buttonImage = buttonObject.AddComponent<Image>();
        buttonImage.color = canPrintDocument ? new Color(0.16f, 0.45f, 0.92f, 0.92f) : new Color(0.62f, 0.62f, 0.62f, 0.72f);

        Button button = buttonObject.AddComponent<Button>();
        button.interactable = canPrintDocument;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(() =>
        {
            printer.PrintDocument();
            RefreshIpRows();
        });

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text buttonText = labelObject.AddComponent<Text>();
        buttonText.text = printer.HasPrintedDocument ? "Impresso" : printer.CanPrint ? printer.ActionLabel : "Sem IP";
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.font = GetDefaultFont();
        buttonText.fontSize = 12;
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        buttonText.verticalOverflow = VerticalWrapMode.Truncate;
    }

    private void CreateDualNetworkDoorRow(DualNetworkDoorController dualDoor)
    {
        GameObject rowObject = CreateUiObject("DualDoor_" + dualDoor.DoorLabel, ipScrollRect.content);
        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, rowHeight);

        LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
        rowLayout.minHeight = rowHeight;
        rowLayout.preferredHeight = rowHeight;

        bool canOperate = dualDoor.CanOperate
            && MissionManager.CanOperateDoorCommand(dualDoor.FirstDevice)
            && MissionManager.CanOperateDoorCommand(dualDoor.SecondDevice);
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
            MissionManager.NotifyDualDoorsStateChanged(dualDoor.IsOpen);
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
        ApplyRowText(text, message, new Color(0.25f, 0.25f, 0.25f, 1f), FontStyle.Normal);
        text.fontSize = 16;
    }

    private Text CreateFullRowText(Transform parent, string value, Color color, FontStyle fontStyle)
    {
        GameObject textObject = CreateUiObject("Text", parent);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(rowHorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-rowHorizontalPadding, 0f);

        Text text = textObject.AddComponent<Text>();
        ApplyRowText(text, value, color, fontStyle);
        return text;
    }

    private void ApplyRowText(Text text, string value, Color color, FontStyle fontStyle)
    {
        text.text = value;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.font = GetDefaultFont();
        text.fontSize = 14;
        text.fontStyle = fontStyle;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
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
        bool canOperate = device.CanOperate && MissionManager.CanOperateDoorCommand(device);
        rowImage.color = canOperate ? new Color(1f, 1f, 1f, 0.94f) : new Color(0.82f, 0.82f, 0.82f, 0.92f);

        GameObject textObject = CreateUiObject("Text", rowObject.transform);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(rowHorizontalPadding, 0f);
        textRect.offsetMax = new Vector2(-92f, 0f);

        Text deviceText = textObject.AddComponent<Text>();
        deviceText.text = device.DeviceLabel + " - " + (device.CanOperate ? (canOperate ? device.StateLabel : "Aguardando missao") : "Sem IP");
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
        buttonImage.color = canOperate ? new Color(0.16f, 0.45f, 0.92f, 0.92f) : new Color(0.62f, 0.62f, 0.62f, 0.72f);

        Button button = buttonObject.AddComponent<Button>();
        button.interactable = canOperate;
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(() =>
        {
            device.Toggle();
            MissionManager.NotifySingleDoorStateChanged(device.IsOpen);
            RefreshIpRows();
        });

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text buttonText = labelObject.AddComponent<Text>();
        buttonText.text = device.CanOperate ? (canOperate ? device.ActionLabel : "Bloqueado") : "Sem IP";
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
        ApplyWiFiToggleButton();

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
        else if (HasWiFiInterface)
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

        if (showingTerminalPanel)
        {
            selectedIpLabel.text = "Dispositivos de rede";
        }
        else if (HasWiFiInterface && !IsConnectedToNetworkJack && selectedWiFiRouter == null)
        {
            selectedIpLabel.text = notebookWiFiEnabled ? "Selecione uma rede Wi-Fi" : "Wi-Fi do notebook desligado";
        }
        else if (string.IsNullOrWhiteSpace(assignedIp))
        {
            selectedIpLabel.text = "IP selecionado: nenhum";
        }
        else
        {
            selectedIpLabel.text = "IP selecionado: " + assignedIp + " - " + (connectedByWiFi ? "Wi-Fi" : "Cabo");
        }
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

    private void ApplyWiFiToggleButton()
    {
        if (wifiToggleButton == null)
        {
            return;
        }

        bool shouldShow = HasWiFiInterface && !IsConnectedToNetworkJack && !showingTerminalPanel;
        wifiToggleButton.gameObject.SetActive(shouldShow);

        Image buttonImage = wifiToggleButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = notebookWiFiEnabled ? new Color(0.08f, 0.62f, 0.26f, 0.96f) : new Color(0.82f, 0.12f, 0.1f, 0.96f);
        }

        if (wifiToggleLabel == null)
        {
            wifiToggleLabel = wifiToggleButton.GetComponentInChildren<Text>(true);
        }

        if (wifiToggleLabel != null)
        {
            wifiToggleLabel.text = notebookWiFiEnabled ? "Wi-Fi ON" : "Wi-Fi OFF";
            wifiToggleLabel.alignment = TextAnchor.MiddleCenter;
            wifiToggleLabel.color = Color.white;
            wifiToggleLabel.font = GetDefaultFont();
            wifiToggleLabel.fontSize = 13;
            wifiToggleLabel.fontStyle = FontStyle.Bold;
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

        Transform wifiToggle = panelObject.transform.Find("WiFiToggleButton");
        if (wifiToggle != null)
        {
            wifiToggleButton = wifiToggle.GetComponent<Button>();
            wifiToggleLabel = wifiToggle.GetComponentInChildren<Text>(true);
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
        wifiToggleButton = null;
        wifiToggleLabel = null;
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
            if (lowerName.Contains("light")
                || lowerName.Contains("luz")
                || lowerName.Contains("lamp")
                || lowerName.Contains("status")
                || lowerName.Contains("button_power")
                || lowerName.Contains("power"))
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

    private void EnsureNetworkPrinterDevices()
    {
        ComputerInteractable[] networkDevices = FindObjectsOfType<ComputerInteractable>(true);
        foreach (ComputerInteractable networkDevice in networkDevices)
        {
            if (networkDevice != null && networkDevice.IsPrinterDevice() && IsPrinterRootTransform(networkDevice.transform) && networkDevice.GetComponent<NetworkPrinterDevice>() == null)
            {
                ConfigurePrinterNetworkDevice(networkDevice.gameObject.AddComponent<NetworkPrinterDevice>());
            }
        }

        RemoveDuplicatePrinterComponents();

        NetworkPrinterDevice[] existingPrinters = FindObjectsOfType<NetworkPrinterDevice>(true);
        foreach (NetworkPrinterDevice existingPrinter in existingPrinters)
        {
            ConfigurePrinterNetworkDevice(existingPrinter);
        }

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            bool isPrinterRoot = lowerName.Contains("printer") || lowerName.Contains("impressora");
            bool parentIsPrinter = HasPrinterNamedParent(candidate);
            if (isPrinterRoot && !parentIsPrinter && IsPrinterRootTransform(candidate))
            {
                NetworkPrinterDevice printer = candidate.GetComponent<NetworkPrinterDevice>();
                if (printer == null)
                {
                    printer = candidate.gameObject.AddComponent<NetworkPrinterDevice>();
                }

                ConfigurePrinterNetworkDevice(printer);
            }
        }
    }

    private void ConfigurePrinterNetworkDevice(NetworkPrinterDevice printer)
    {
        if (printer == null)
        {
            return;
        }

        ComputerInteractable printerNetwork = printer.GetComponent<ComputerInteractable>();
        if (printerNetwork == null)
        {
            printerNetwork = printer.gameObject.AddComponent<ComputerInteractable>();
        }

        if (string.IsNullOrWhiteSpace(printerNetwork.reservedDeviceName))
        {
            printerNetwork.reservedDeviceName = printer.DeviceLabel;
        }
    }

    private void RemoveDuplicatePrinterComponents()
    {
        NetworkPrinterDevice[] printers = FindObjectsOfType<NetworkPrinterDevice>(true);
        foreach (NetworkPrinterDevice printer in printers)
        {
            if (printer == null)
            {
                continue;
            }

            Transform printerRoot = ResolvePrinterRootTransform(printer.transform);
            if (printerRoot == null || printerRoot == printer.transform)
            {
                continue;
            }

            NetworkPrinterDevice rootPrinter = printerRoot.GetComponent<NetworkPrinterDevice>();
            if (rootPrinter == null || rootPrinter == printer)
            {
                continue;
            }

            DestroyImmediateSafe(printer);
        }
    }

    private NetworkPrinterDevice ResolveCanonicalPrinterDevice(NetworkPrinterDevice printer)
    {
        if (printer == null)
        {
            return null;
        }

        Transform printerRoot = ResolvePrinterRootTransform(printer.transform);
        if (printerRoot == null)
        {
            return printer;
        }

        NetworkPrinterDevice rootPrinter = printerRoot.GetComponent<NetworkPrinterDevice>();
        return rootPrinter != null ? rootPrinter : printer;
    }

    private Transform ResolvePrinterRootTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        MovableDevice movableDeviceRoot = candidate.GetComponentInParent<MovableDevice>();
        if (movableDeviceRoot != null && movableDeviceRoot.IsComputerDevice() && IsPrinterNamedTransform(movableDeviceRoot.transform))
        {
            return movableDeviceRoot.transform;
        }

        ComputerInteractable computerRoot = candidate.GetComponentInParent<ComputerInteractable>();
        if (computerRoot != null && computerRoot.IsPrinterDevice())
        {
            return computerRoot.transform;
        }

        return candidate;
    }

    private bool IsPrinterRootTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (HasPrinterNamedParent(candidate))
        {
            return false;
        }

        if (candidate.GetComponent<MovableDevice>() != null || candidate.GetComponent<ComputerInteractable>() != null)
        {
            return true;
        }

        return false;
    }

    private bool IsPrinterNamedTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        string lowerName = candidate.name.ToLowerInvariant();
        return lowerName.Contains("printer") || lowerName.Contains("impressora");
    }

    private bool HasPrinterNamedParent(Transform candidate)
    {
        Transform parent = candidate != null ? candidate.parent : null;
        while (parent != null)
        {
            string lowerParentName = parent.name.ToLowerInvariant();
            if (lowerParentName.Contains("printer") || lowerParentName.Contains("impressora"))
            {
                return true;
            }

            parent = parent.parent;
        }

        return false;
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

    private void DestroyImmediateSafe(Component target)
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
            string offMaterialPath = IsPrinterDevice() ? GrayMaterialPath : WhiteMaterialPath;
            offMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(offMaterialPath);
        }

        if (screenOffMaterial == null)
        {
            screenOffMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(GrayMaterialPath);
        }

        if (screenOnMaterial == null)
        {
            screenOnMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(ScreenOnMaterialPath);
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
            Color offColor = IsPrinterDevice() ? Color.gray : Color.white;
            offMaterial = CreateFallbackMaterial("Fallback_Computer_Light_Off", offColor);
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

    private void ApplyDeviceDefaults()
    {
        if (!IsPrinterDevice())
        {
            return;
        }

        if (deviceTitle == "Computador")
        {
            deviceTitle = "Impressora";
        }

        if (carryPromptText == "E pegar computador")
        {
            carryPromptText = "E pegar impressora";
        }

        if (networkPromptText == "F configurar rede")
        {
            networkPromptText = "F configurar impressora";
        }

        if (string.IsNullOrWhiteSpace(reservedDeviceName))
        {
            reservedDeviceName = "Impressora";
        }
    }

    private bool IsPrinterDevice()
    {
        string lowerObjectName = name.ToLowerInvariant();
        string lowerParentName = transform.parent != null ? transform.parent.name.ToLowerInvariant() : string.Empty;
        string lowerTitle = deviceTitle.ToLowerInvariant();

        return lowerObjectName.Contains("printer")
            || lowerObjectName.Contains("impressora")
            || lowerParentName.Contains("printer")
            || lowerParentName.Contains("impressora")
            || lowerTitle.Contains("impressora")
            || lowerTitle.Contains("printer");
    }

}
