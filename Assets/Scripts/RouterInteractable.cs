using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RouterInteractable : MonoBehaviour
{
    [SerializeField] private bool allowConfigurationAccess = true;
    [SerializeField] private bool allowMovement;
    [SerializeField] private bool initialWiFiEnabled;
    [FormerlySerializedAs("industrialDhcpEnabled")]
    [InspectorName("DHCP Enabled")]
    [SerializeField] private bool dhcpEnabled = true;

    [Header("Wi-Fi")]
    [SerializeField] private int wifiRange = 5;
    [SerializeField] private float wifiDetectionInterval = 1f;
    [SerializeField] private LayerMask wifiDeviceLayerMask = ~0;

    [Header("Network")]
    [SerializeField] private string promptText = "F configurar roteador";
    [SerializeField] private NetworkScope networkScope;
    [SerializeField] private string networkPrefix = "192.168.0.";
    [SerializeField] private int routerAddress = 1;
    [SerializeField] private int firstDeviceAddress = 2;
    [SerializeField] private int availableAddressCount = 4;

    [Header("DHCP")]
    [FormerlySerializedAs("industrialDhcpDiscoveryInterval")]
    [SerializeField] private float dhcpDiscoveryInterval = 1f;
    [FormerlySerializedAs("logIndustrialDhcpEvents")]
    [SerializeField] private bool logDhcpEvents;

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
    private bool isWiFiEnabled;
    private MovableDevice movableDevice;
    private Button wifiToggleButton;
    private Text wifiToggleLabel;
    private GameObject dhcpIndicatorObject;
    private Text dhcpIndicatorLabel;
    private InteractionPromptPresenter promptPresenter;
    private readonly HashSet<WiFiDevice> detectedWiFiDevices = new HashSet<WiFiDevice>();
    private readonly List<IndustrialDhcpLease> industrialDhcpLeases = new List<IndustrialDhcpLease>();

    public event Action OnIpPoolChanged;
    public bool IsOpen => isOpen;
    public IReadOnlyList<NetworkScope.IpLease> Leases => ActiveNetworkScope != null ? ActiveNetworkScope.Leases : Array.Empty<NetworkScope.IpLease>();
    public NetworkScope ActiveNetworkScope => ResolveNetworkScope(false);
    public string RouterIpAddress => ActiveNetworkScope != null ? ActiveNetworkScope.RouterIpAddress : networkPrefix + routerAddress;
    public bool IsWiFiEnabled => isWiFiEnabled;
    public bool InitialWiFiEnabled => initialWiFiEnabled;
    public int WiFiRange => Mathf.Max(wifiRange, 0);
    public string WiFiNetworkName => (ActiveNetworkScope != null ? ActiveNetworkScope.NetworkPrefix : networkPrefix).TrimEnd('.');
    public bool AllowConfigurationAccess => allowConfigurationAccess;
    public bool AllowMovement => allowMovement;
    public bool IndustrialDhcpEnabled => dhcpEnabled;
    public float IndustrialDhcpDiscoveryInterval => Mathf.Max(dhcpDiscoveryInterval, 0.1f);
    public bool IsRouterOperational => isActiveAndEnabled;
    public bool CanProvideIndustrialDhcp => IsRouterOperational && isWiFiEnabled && dhcpEnabled;
    public IReadOnlyList<IndustrialDhcpLease> ConnectedIndustrialDevices => industrialDhcpLeases;

    [Serializable]
    public class IndustrialDhcpLease
    {
        [SerializeField] private RoboticArmNetworkAdapter adapter;
        [SerializeField] private string deviceName;
        [SerializeField] private string deviceId;
        [SerializeField] private string ipAddress;
        [SerializeField] private string networkId;

        public RoboticArmNetworkAdapter Adapter => adapter;
        public string DeviceName => deviceName;
        public string DeviceId => deviceId;
        public string IpAddress => ipAddress;
        public string NetworkId => networkId;

        public IndustrialDhcpLease(RoboticArmNetworkAdapter adapter, string ipAddress, string networkId)
        {
            Update(adapter, ipAddress, networkId);
        }

        public void Update(RoboticArmNetworkAdapter nextAdapter, string nextIpAddress, string nextNetworkId)
        {
            adapter = nextAdapter;
            deviceName = nextAdapter != null ? nextAdapter.DeviceName : string.Empty;
            deviceId = nextAdapter != null ? nextAdapter.DeviceId : string.Empty;
            ipAddress = nextIpAddress;
            networkId = nextNetworkId;
        }
    }

    private void Awake()
    {
        if (allowMovement)
        {
            EnsureMovableDevice();
        }
        SetNetworkScope(ResolveNetworkScope(true));
        isWiFiEnabled = initialWiFiEnabled;
        ResetRuntimePanel();
        isOpen = false;
        EnsureUi();
        RefreshIpRows();
        ForceUiClosed();
        EnsureGreenLightBlinkers();
    }

    private void Start()
    {
        StartCoroutine(EnsureGreenLightBlinkersAfterFirstFrame());
        StartCoroutine(DetectWiFiDevicesRoutine());
        ForceUiClosed();
    }

    private IEnumerator EnsureGreenLightBlinkersAfterFirstFrame()
    {
        yield return null;
        EnsureGreenLightBlinkers();
    }

    private void OnValidate()
    {
        movableDevice = GetComponent<MovableDevice>();
        movableDevice?.ConfigureDeviceName("Roteador");
        ResolveNetworkScope(false);
        wifiRange = Mathf.Max(wifiRange, 0);
        wifiDetectionInterval = Mathf.Max(wifiDetectionInterval, 0.1f);
        dhcpDiscoveryInterval = Mathf.Max(dhcpDiscoveryInterval, 0.1f);
        ApplyUiSettings();
        RefreshIpRows();
        if (Application.isPlaying && !CanProvideIndustrialDhcp)
        {
            ReleaseAllIndustrialDhcpDevices();
        }
    }

    private void OnDestroy()
    {
        ClearDetectedWiFiDevices();
        if (networkScope != null)
        {
            networkScope.OnIpPoolChanged -= NotifyPoolChanged;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.22f);
        Gizmos.DrawSphere(transform.position, Mathf.Max(wifiRange, 0));
        Gizmos.color = new Color(0.1f, 0.55f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, Mathf.Max(wifiRange, 0));
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
        bool shouldShow = allowConfigurationAccess && visible && !isOpen && (!allowMovement || movableDevice == null || !movableDevice.IsCarried);
        if (shouldShow)
        {
            if (allowMovement)
            {
                promptPresenter?.Show(this, "ROTEADOR",
                    new InteractionPromptAction("E", "Mover"),
                    new InteractionPromptAction("F", "Configurar"));
            }
            else
            {
                promptPresenter?.Show(this, "ROTEADOR", new InteractionPromptAction("F", "Configurar"));
            }
        }
        else
        {
            promptPresenter?.Hide(this);
        }
    }

    public void Toggle(PlayerTopDownController player)
    {
        if (!allowConfigurationAccess)
        {
            return;
        }

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
        if (!allowConfigurationAccess)
        {
            return;
        }

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
        return TryAssignIp(computer, address, reservedDeviceName, NetworkConnectionType.Cable);
    }

    public bool TryAssignIp(ComputerInteractable computer, string address, string reservedDeviceName, NetworkConnectionType connectionType)
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

        return scope.TryAssignIp(computer, address, reservedDeviceName, connectionType);
    }

    public void ReleaseIp(ComputerInteractable computer)
    {
        NetworkScope scope = ResolveNetworkScope(false);
        if (scope != null)
        {
            scope.ReleaseIp(computer);
        }
    }

    public bool TryConnectWiFi(ComputerInteractable computer, WiFiDevice wiFiDevice, string address, string reservedDeviceName)
    {
        if (!isWiFiEnabled || computer == null || wiFiDevice == null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        if (!IsWiFiDeviceInRange(wiFiDevice))
        {
            return false;
        }

        return TryAssignIp(computer, address, reservedDeviceName, NetworkConnectionType.WiFi);
    }

    public void ReleaseWiFiConnection(ComputerInteractable computer)
    {
        if (computer == null)
        {
            return;
        }

        NetworkScope scope = ResolveNetworkScope(false);
        if (scope == null)
        {
            return;
        }

        foreach (NetworkScope.IpLease lease in scope.Leases)
        {
            if (lease.AssignedComputer == computer && lease.ConnectionType == NetworkConnectionType.WiFi)
            {
                scope.ReleaseIp(computer);
                return;
            }
        }
    }

    public void SetWiFiEnabled(bool enabled)
    {
        if (isWiFiEnabled == enabled)
        {
            return;
        }

        isWiFiEnabled = enabled;
        if (!isWiFiEnabled)
        {
            ReleaseAllIndustrialDhcpDevices();
            DisconnectAllWiFiDevices();
            ClearDetectedWiFiDevices();
        }

        ApplyWiFiToggleButton();
        ApplyDhcpIndicator();
        RefreshIpRows();
    }

    public void SetIndustrialDhcpEnabled(bool enabled)
    {
        if (dhcpEnabled == enabled)
        {
            return;
        }

        dhcpEnabled = enabled;
        if (!CanProvideIndustrialDhcp)
        {
            ReleaseAllIndustrialDhcpDevices();
        }

        ApplyDhcpIndicator();
    }

    public bool TryAssignIndustrialDhcp(RoboticArmNetworkAdapter adapter, out string ipAddress)
    {
        ipAddress = string.Empty;
        if (!CanProvideIndustrialDhcp || adapter == null)
        {
            return false;
        }

        CleanupIndustrialDhcpLeases();

        IndustrialDhcpLease existingLease = FindIndustrialLease(adapter);
        if (existingLease != null)
        {
            NetworkScope scope = ResolveNetworkScope(false);
            if (scope != null && scope.ContainsAddress(existingLease.IpAddress))
            {
                ipAddress = existingLease.IpAddress;
                return true;
            }

            industrialDhcpLeases.Remove(existingLease);
        }

        NetworkScope activeScope = ResolveNetworkScope(true);
        string networkId = WiFiNetworkName;
        if (activeScope == null)
        {
            return false;
        }

        foreach (NetworkScope.IpLease lease in activeScope.Leases)
        {
            if (lease == null || lease.IsRouter || !lease.IsAvailable || IsIndustrialAddressInUse(lease.Address))
            {
                continue;
            }

            if (!activeScope.TryReserveIpForDevice(lease.Address, adapter.DeviceName))
            {
                continue;
            }

            IndustrialDhcpLease industrialLease = new IndustrialDhcpLease(adapter, lease.Address, networkId);
            industrialDhcpLeases.Add(industrialLease);
            ipAddress = lease.Address;
            LogIndustrialDhcp("IP " + lease.Address + " atribuido ao " + adapter.DeviceName + ".");
            return true;
        }

        return false;
    }

    public void ReleaseIndustrialDhcp(RoboticArmNetworkAdapter adapter)
    {
        if (adapter == null)
        {
            return;
        }

        for (int i = industrialDhcpLeases.Count - 1; i >= 0; i--)
        {
            IndustrialDhcpLease lease = industrialDhcpLeases[i];
            if (lease == null || lease.Adapter == adapter || lease.DeviceId == adapter.DeviceId)
            {
                if (lease != null)
                {
                    ReleaseIndustrialLeaseReservation(lease);
                    LogIndustrialDhcp("IP " + lease.IpAddress + " liberado.");
                }

                industrialDhcpLeases.RemoveAt(i);
            }
        }
    }

    private void ReleaseAllIndustrialDhcpDevices()
    {
        if (industrialDhcpLeases.Count == 0)
        {
            return;
        }

        for (int i = industrialDhcpLeases.Count - 1; i >= 0; i--)
        {
            IndustrialDhcpLease lease = industrialDhcpLeases[i];
            if (lease != null)
            {
                ReleaseIndustrialLeaseReservation(lease);
                lease.Adapter?.HandleIndustrialDhcpRouterUnavailable(this);
                LogIndustrialDhcp("IP " + lease.IpAddress + " liberado.");
            }
        }

        industrialDhcpLeases.Clear();
    }

    private void CleanupIndustrialDhcpLeases()
    {
        for (int i = industrialDhcpLeases.Count - 1; i >= 0; i--)
        {
            IndustrialDhcpLease lease = industrialDhcpLeases[i];
            if (lease == null || lease.Adapter == null)
            {
                if (lease != null)
                {
                    ReleaseIndustrialLeaseReservation(lease);
                }

                industrialDhcpLeases.RemoveAt(i);
            }
        }
    }

    private IndustrialDhcpLease FindIndustrialLease(RoboticArmNetworkAdapter adapter)
    {
        if (adapter == null)
        {
            return null;
        }

        for (int i = 0; i < industrialDhcpLeases.Count; i++)
        {
            IndustrialDhcpLease lease = industrialDhcpLeases[i];
            if (lease != null && (lease.Adapter == adapter || lease.DeviceId == adapter.DeviceId))
            {
                return lease;
            }
        }

        return null;
    }

    private bool IsIndustrialAddressInUse(string address)
    {
        for (int i = 0; i < industrialDhcpLeases.Count; i++)
        {
            IndustrialDhcpLease lease = industrialDhcpLeases[i];
            if (lease != null && lease.IpAddress == address)
            {
                return true;
            }
        }

        return false;
    }

    private void ReleaseIndustrialLeaseReservation(IndustrialDhcpLease lease)
    {
        if (lease == null)
        {
            return;
        }

        NetworkScope scope = ResolveNetworkScope(false);
        if (scope == null)
        {
            return;
        }

        scope.ReleaseReservedIp(lease.IpAddress, lease.DeviceName);
    }

    private void LogIndustrialDhcp(string message)
    {
        if (logDhcpEvents)
        {
            Debug.Log("[" + name + "] " + message, this);
        }
    }

    public bool IsWiFiDeviceInRange(WiFiDevice wiFiDevice)
    {
        if (!isWiFiEnabled || wiFiDevice == null)
        {
            return false;
        }

        float range = Mathf.Max(wifiRange, 0);
        return Vector3.SqrMagnitude(wiFiDevice.transform.position - transform.position) <= range * range;
    }

    public RouterInteractable FindRouterForDevice(MovableDevice device)
    {
        return this;
    }

    private void EnsureMovableDevice()
    {
        if (movableDevice == null)
        {
            movableDevice = GetComponent<MovableDevice>();
        }

        if (movableDevice == null)
        {
            movableDevice = gameObject.AddComponent<MovableDevice>();
        }

        movableDevice.ConfigureDeviceName("Roteador");
    }

    private void NotifyPoolChanged()
    {
        RefreshIpRows();
        OnIpPoolChanged?.Invoke();
    }

    private IEnumerator DetectWiFiDevicesRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(Mathf.Max(wifiDetectionInterval, 0.1f));
        while (enabled)
        {
            if (isWiFiEnabled)
            {
                DetectWiFiDevices();
            }

            yield return wait;
            wait = new WaitForSeconds(Mathf.Max(wifiDetectionInterval, 0.1f));
        }
    }

    private void DetectWiFiDevices()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, Mathf.Max(wifiRange, 0), wifiDeviceLayerMask, QueryTriggerInteraction.Collide);
        HashSet<WiFiDevice> currentDevices = new HashSet<WiFiDevice>();

        foreach (Collider hit in hits)
        {
            if (hit == null)
            {
                continue;
            }

            WiFiDevice wiFiDevice = hit.GetComponentInParent<WiFiDevice>();
            if (wiFiDevice == null || !IsWiFiDeviceInRange(wiFiDevice))
            {
                continue;
            }

            currentDevices.Add(wiFiDevice);
            if (detectedWiFiDevices.Add(wiFiDevice))
            {
                wiFiDevice.SetRouterAvailable(this, true);
            }
        }

        List<WiFiDevice> devicesOutOfRange = new List<WiFiDevice>();
        foreach (WiFiDevice wiFiDevice in detectedWiFiDevices)
        {
            if (wiFiDevice == null || !currentDevices.Contains(wiFiDevice))
            {
                devicesOutOfRange.Add(wiFiDevice);
            }
        }

        foreach (WiFiDevice wiFiDevice in devicesOutOfRange)
        {
            detectedWiFiDevices.Remove(wiFiDevice);
            if (wiFiDevice != null)
            {
                wiFiDevice.SetRouterAvailable(this, false);
                wiFiDevice.Computer?.HandleWiFiRouterOutOfRange(this);
            }
        }
    }

    private void DisconnectAllWiFiDevices()
    {
        NetworkScope scope = ResolveNetworkScope(false);
        if (scope == null)
        {
            return;
        }

        List<ComputerInteractable> computersToDisconnect = new List<ComputerInteractable>();
        foreach (NetworkScope.IpLease lease in scope.Leases)
        {
            if (lease.AssignedComputer != null && lease.ConnectionType == NetworkConnectionType.WiFi)
            {
                computersToDisconnect.Add(lease.AssignedComputer);
            }
        }

        foreach (ComputerInteractable computer in computersToDisconnect)
        {
            computer.HandleWiFiRouterDisabled(this);
        }
    }

    private void ClearDetectedWiFiDevices()
    {
        foreach (WiFiDevice wiFiDevice in detectedWiFiDevices)
        {
            if (wiFiDevice != null)
            {
                wiFiDevice.SetRouterAvailable(this, false);
            }
        }

        detectedWiFiDevices.Clear();
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
        RuntimeEventSystemUtility.EnsureSingleEventSystem();
    }

    private void EnsurePrompt()
    {
        GameObject legacyPrompt = promptObject;
        promptPresenter = InteractionPromptPresenter.GetOrCreate(canvas);
        promptObject = promptPresenter != null ? promptPresenter.gameObject : null;
        promptLabel = null;

        if (legacyPrompt != null && legacyPrompt != promptObject && legacyPrompt.name == "RouterInteractionPrompt")
        {
            DestroyImmediateSafe(legacyPrompt);
        }

        Transform legacyCanvasPrompt = canvas != null ? canvas.transform.Find("RouterInteractionPrompt") : null;
        if (legacyCanvasPrompt != null && legacyCanvasPrompt.gameObject != promptObject)
        {
            DestroyImmediateSafe(legacyCanvasPrompt.gameObject);
        }
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

        CreateWiFiToggleButton(parent);
        CreateDhcpIndicator(parent);
        CreateFooter(parent, hint);
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
        wifiToggleButton.transition = Selectable.Transition.None;
        wifiToggleButton.interactable = false;

        GameObject labelObject = CreateUiObject("Text", buttonObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        wifiToggleLabel = labelObject.AddComponent<Text>();
        ApplyWiFiToggleButton();
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

        if (!string.IsNullOrWhiteSpace(lease.AssignedDeviceName))
        {
            return lease.AssignedDeviceName;
        }

        return lease.IsAvailable ? "Disponivel" : NetworkScope.GetConnectionTypeLabel(lease.ConnectionType);
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

        promptLabel.text = GetPromptText();
        promptLabel.alignment = TextAnchor.MiddleCenter;
        promptLabel.color = Color.white;
        promptLabel.font = GetDefaultFont();
        promptLabel.fontSize = 18;
    }

    private string GetPromptText()
    {
        if (string.IsNullOrWhiteSpace(promptText)
            || promptText.Contains("E para interagir")
            || promptText.Contains("F para interagir")
            || promptText.Contains("interagir"))
        {
            return allowMovement ? "E mover roteador  |  F configurar roteador" : "F configurar roteador";
        }

        return allowMovement && !promptText.Contains("E mover")
            ? "E mover roteador  |  " + promptText
            : promptText;
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

        ApplyWiFiToggleButton();
        ApplyCloseHintLabel();

        if (ipScrollRect != null)
        {
            ipScrollRect.horizontal = false;
            ipScrollRect.scrollSensitivity = scrollSensitivity;
            ApplyContentPadding();
        }
    }

    private void ApplyWiFiToggleButton()
    {
        if (wifiToggleButton == null)
        {
            return;
        }

        wifiToggleButton.interactable = false;
        wifiToggleButton.transition = Selectable.Transition.None;

        Image buttonImage = wifiToggleButton.GetComponent<Image>();
        if (buttonImage != null)
        {
            buttonImage.color = isWiFiEnabled ? new Color(0.08f, 0.62f, 0.26f, 0.96f) : new Color(0.82f, 0.12f, 0.1f, 0.96f);
        }

        if (wifiToggleLabel == null)
        {
            wifiToggleLabel = wifiToggleButton.GetComponentInChildren<Text>(true);
        }

        if (wifiToggleLabel != null)
        {
            wifiToggleLabel.text = isWiFiEnabled ? "Wi-Fi ON" : "Wi-Fi OFF";
            wifiToggleLabel.alignment = TextAnchor.MiddleCenter;
            wifiToggleLabel.color = Color.white;
            wifiToggleLabel.font = GetDefaultFont();
            wifiToggleLabel.fontSize = 13;
            wifiToggleLabel.fontStyle = FontStyle.Bold;
        }

        ApplyDhcpIndicator();
    }

    private void CreateDhcpIndicator(Transform parent)
    {
        GameObject indicatorObject = CreateUiObject("DhcpIndicator", parent);
        RectTransform indicatorRect = indicatorObject.AddComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(1f, 1f);
        indicatorRect.anchorMax = new Vector2(1f, 1f);
        indicatorRect.pivot = new Vector2(1f, 1f);
        indicatorRect.anchoredPosition = new Vector2(-138f, -22f);
        indicatorRect.sizeDelta = new Vector2(74f, 28f);

        indicatorObject.AddComponent<Image>();

        GameObject labelObject = CreateUiObject("Text", indicatorObject.transform);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        dhcpIndicatorObject = indicatorObject;
        dhcpIndicatorLabel = labelObject.AddComponent<Text>();
        ApplyDhcpIndicator();
    }

    private void ApplyDhcpIndicator()
    {
        if (dhcpIndicatorObject == null && panelObject != null)
        {
            Transform indicator = panelObject.transform.Find("DhcpIndicator");
            if (indicator != null)
            {
                dhcpIndicatorObject = indicator.gameObject;
                dhcpIndicatorLabel = indicator.GetComponentInChildren<Text>(true);
            }
        }

        if (dhcpIndicatorObject == null)
        {
            return;
        }

        bool active = CanProvideIndustrialDhcp;
        Image indicatorImage = dhcpIndicatorObject.GetComponent<Image>();
        if (indicatorImage != null)
        {
            indicatorImage.color = active ? new Color(0.08f, 0.62f, 0.26f, 0.96f) : new Color(0.42f, 0.42f, 0.42f, 0.58f);
        }

        if (dhcpIndicatorLabel == null)
        {
            dhcpIndicatorLabel = dhcpIndicatorObject.GetComponentInChildren<Text>(true);
        }

        if (dhcpIndicatorLabel != null)
        {
            dhcpIndicatorLabel.text = "DHCP";
            dhcpIndicatorLabel.alignment = TextAnchor.MiddleCenter;
            dhcpIndicatorLabel.color = Color.white;
            dhcpIndicatorLabel.font = GetDefaultFont();
            dhcpIndicatorLabel.fontSize = 13;
            dhcpIndicatorLabel.fontStyle = FontStyle.Bold;
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

        Transform wifiToggle = panelObject.transform.Find("WiFiToggleButton");
        if (wifiToggle != null)
        {
            wifiToggleButton = wifiToggle.GetComponent<Button>();
            wifiToggleLabel = wifiToggle.GetComponentInChildren<Text>(true);
        }

        Transform dhcpIndicator = panelObject.transform.Find("DhcpIndicator");
        if (dhcpIndicator != null)
        {
            dhcpIndicatorObject = dhcpIndicator.gameObject;
            dhcpIndicatorLabel = dhcpIndicator.GetComponentInChildren<Text>(true);
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
        wifiToggleButton = null;
        wifiToggleLabel = null;
        dhcpIndicatorObject = null;
        dhcpIndicatorLabel = null;
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
