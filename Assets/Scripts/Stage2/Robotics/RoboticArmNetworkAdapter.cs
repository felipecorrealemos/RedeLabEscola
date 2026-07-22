using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoboticArmNetworkAdapter : MonoBehaviour
{
    public enum OperationalState
    {
        Off,
        Running,
        Stopping
    }

    public enum NetworkState
    {
        SemRede,
        Connecting,
        ConnectedWithoutAuthorization,
        Operating,
        Stopping,
        NetworkConflict,
        NoNetwork = SemRede,
        Connected = ConnectedWithoutAuthorization
    }

    [Header("Debug")]
    [SerializeField] private bool hasValidNetwork;
    [SerializeField] private bool hasNetworkConflict;
    [SerializeField] private NetworkState currentNetworkState = NetworkState.NoNetwork;
    [SerializeField] private int validDhcpRoutersDetected;
    [SerializeField] private string assignedIp;
    [SerializeField] private string connectedNetworkId;
    [SerializeField] private RouterInteractable connectedRouter;
    [SerializeField] private bool operationalAuthorization;

    [Header("Identity")]
    [SerializeField] private string deviceName = "Braco Robotico";
    [SerializeField] private string deviceId = "robotic-arm";

    [Header("Wi-Fi")]
    [SerializeField] private WiFiDevice wiFiDevice;
    [SerializeField] private float discoveryInterval = 1f;
    [SerializeField] private float wifiSensorRadius = 0.65f;

    [Header("Status Light")]
    [SerializeField] private Renderer statusLightRenderer;
    [SerializeField] private Light statusLight;
    [SerializeField] private float blinkFrequency = 3f;
    [SerializeField] private Color disconnectedColor = new Color(1f, 0.08f, 0.04f, 1f);
    [SerializeField] private Color connectedColor = new Color(1f, 0.78f, 0.05f, 1f);
    [SerializeField] private Color authorizedColor = new Color(0.1f, 0.95f, 0.24f, 1f);

    [Header("Diagnostics")]
    [SerializeField] private bool logNetworkEvents;

    private readonly List<RouterInteractable> validRouters = new List<RouterInteractable>(4);
    private MaterialPropertyBlock propertyBlock;
    private RoboticArmController controller;
    private float nextDiscoveryTime;
    private NetworkState lastLoggedState = (NetworkState)(-1);
    private NetworkState stateAfterSafeStop = NetworkState.SemRede;
    private OperationalState cachedOperationalState = OperationalState.Off;

    public event Action<RoboticArmNetworkAdapter> OnConnected;
    public event Action<RoboticArmNetworkAdapter> OnDisconnected;
    public event Action<RoboticArmNetworkAdapter> OnNetworkConflict;
    public event Action<RoboticArmNetworkAdapter, string> OnIpAssigned;
    public event Action<RoboticArmNetworkAdapter, NetworkState> OnNetworkStateChanged;
    public event Action<RoboticArmNetworkAdapter, OperationalState> OnOperationalStateChanged;

    public string DeviceName => string.IsNullOrWhiteSpace(deviceName) ? name : deviceName;
    public string DeviceId => string.IsNullOrWhiteSpace(deviceId) ? GetInstanceID().ToString() : deviceId;
    public string AssignedIp => assignedIp;
    public bool HasValidNetwork => hasValidNetwork;
    public bool HasNetworkConflict => hasNetworkConflict;
    public NetworkState CurrentNetworkState => currentNetworkState;
    public RouterInteractable ConnectedRouter => connectedRouter;
    public string ConnectedNetworkId => connectedNetworkId;
    public int ValidDhcpRoutersDetected => validDhcpRoutersDetected;
    public bool HasOperationalAuthorization => operationalAuthorization;
    public OperationalState CurrentOperationalState => currentNetworkState == NetworkState.Stopping
        ? OperationalState.Stopping
        : operationalAuthorization ? OperationalState.Running : OperationalState.Off;
    public bool IsAccessibleByFactorySystem => hasValidNetwork && !hasNetworkConflict && !string.IsNullOrWhiteSpace(assignedIp);
    public bool CanStartNewCycle => IsAccessibleByFactorySystem && operationalAuthorization;
    public bool IsOperatingOrStopping => currentNetworkState == NetworkState.Operating || currentNetworkState == NetworkState.Stopping;

    private void Awake()
    {
        ResolveReferences();
        ConfigureWiFiDevice();
        ApplyState(NetworkState.SemRede, false);
    }

    private void OnEnable()
    {
        nextDiscoveryTime = 0f;
        MissionManager.NotifyStage2RoboticArmOperationChanged();
    }

    private void OnDisable()
    {
        InvalidateOperationalAuthorization();
        DisconnectFromRouter(false);
        wiFiDevice?.ClearAvailableRouters();
        MissionManager.NotifyStage2RoboticArmOperationChanged();
    }

    private void OnValidate()
    {
        discoveryInterval = Mathf.Max(discoveryInterval, 0.1f);
        wifiSensorRadius = Mathf.Max(wifiSensorRadius, 0.05f);
        blinkFrequency = Mathf.Max(blinkFrequency, 0f);
    }

    private void Update()
    {
        UpdateStatusLight();

        if (Time.time < nextDiscoveryTime)
        {
            return;
        }

        nextDiscoveryTime = Time.time + Mathf.Max(discoveryInterval, 0.1f);
        EvaluateNetwork();
    }

    public void ConfigureIdentity(string visibleName, string uniqueId)
    {
        if (!string.IsNullOrWhiteSpace(visibleName))
        {
            deviceName = visibleName;
        }

        if (!string.IsNullOrWhiteSpace(uniqueId))
        {
            deviceId = uniqueId;
        }

        ConfigureWiFiDevice();
    }

    public void ConfigureReferences(WiFiDevice device, Renderer lightRenderer, Light light)
    {
        if (device != null)
        {
            wiFiDevice = device;
        }

        if (lightRenderer != null)
        {
            statusLightRenderer = lightRenderer;
        }

        if (light != null)
        {
            statusLight = light;
        }

        ResolveReferences();
        ConfigureWiFiDevice();
    }

    public void HandleIndustrialDhcpRouterUnavailable(RouterInteractable router)
    {
        if (router == null || connectedRouter != router)
        {
            return;
        }

        bool hadConnection = !string.IsNullOrWhiteSpace(assignedIp) || connectedRouter != null;
        InvalidateOperationalAuthorization();
        ClearConnectionData();
        ApplyStateOrStopSafely(NetworkState.SemRede);
        if (hadConnection)
        {
            LogStateOnce("Rede perdida. IP liberado.");
            OnDisconnected?.Invoke(this);
        }
    }

    public bool RequestStartWork()
    {
        if (!IsAccessibleByFactorySystem)
        {
            InvalidateOperationalAuthorization();
            ApplyState(hasNetworkConflict ? NetworkState.NetworkConflict : NetworkState.SemRede, true);
            MissionManager.NotifyStage2RoboticArmOperationChanged();
            return false;
        }

        if (currentNetworkState == NetworkState.Stopping)
        {
            return false;
        }

        operationalAuthorization = true;
        Debug.Log("[" + DeviceName + "] Autorizacao operacional ativada.", this);
        ApplyState(NetworkState.Operating, true);
        MissionManager.NotifyStage2RoboticArmOperationChanged();
        return true;
    }

    public bool RequestStopWork()
    {
        if (currentNetworkState == NetworkState.Stopping)
        {
            return false;
        }

        bool wasRunning = operationalAuthorization || currentNetworkState == NetworkState.Operating;
        InvalidateOperationalAuthorization();
        if (controller != null && controller.IsBusy)
        {
            Debug.Log("[" + DeviceName + "] Parada solicitada. Finalizando ciclo atual.", this);
        }

        ApplyStateOrStopSafely(IsAccessibleByFactorySystem ? NetworkState.ConnectedWithoutAuthorization : NetworkState.SemRede);
        MissionManager.NotifyStage2RoboticArmOperationChanged();
        return wasRunning || currentNetworkState == NetworkState.ConnectedWithoutAuthorization;
    }

    public void AuthorizeOperation()
    {
        RequestStartWork();
    }

    public void RequestStopOperation()
    {
        RequestStopWork();
    }

    private void EvaluateNetwork()
    {
        ResolveReferences();
        ConfigureWiFiDevice();
        CollectValidDhcpRouters();

        if (currentNetworkState == NetworkState.Stopping && controller != null && controller.IsBusy)
        {
            UpdatePendingStateWhileStopping();
            ApplyState(NetworkState.Stopping, false);
            return;
        }

        if (validRouters.Count == 0)
        {
            if (connectedRouter != null || currentNetworkState != NetworkState.NoNetwork)
            {
                InvalidateOperationalAuthorization();
                DisconnectFromRouter(true);
                ApplyStateOrStopSafely(NetworkState.SemRede);
            }
            else
            {
                ApplyState(NetworkState.SemRede, false);
            }

            LogStateOnce("Nenhuma rede DHCP valida encontrada.");
            return;
        }

        if (validRouters.Count > 1)
        {
            InvalidateOperationalAuthorization();
            DisconnectFromRouter(true);
            ApplyStateOrStopSafely(NetworkState.NetworkConflict);
            LogStateOnce("Conflito: " + validRouters.Count + " roteadores DHCP validos detectados.");
            return;
        }

        RouterInteractable targetRouter = validRouters[0];
        if (connectedRouter == targetRouter && !string.IsNullOrWhiteSpace(assignedIp))
        {
            if (targetRouter.ActiveNetworkScope != null && targetRouter.ActiveNetworkScope.ContainsAddress(assignedIp))
            {
                NetworkState connectedState = operationalAuthorization ? NetworkState.Operating : NetworkState.ConnectedWithoutAuthorization;
                ApplyState(connectedState, false);
                return;
            }

            DisconnectFromRouter(false);
            InvalidateOperationalAuthorization();
        }
        else
        {
            DisconnectFromRouter(false);
            InvalidateOperationalAuthorization();
        }

        ApplyState(NetworkState.Connecting, true);

        if (targetRouter.TryAssignIndustrialDhcp(this, out string nextIp))
        {
            connectedRouter = targetRouter;
            assignedIp = nextIp;
            connectedNetworkId = targetRouter.WiFiNetworkName;
            ApplyState(NetworkState.ConnectedWithoutAuthorization, true);
            LogStateOnce("Conectado a rede " + connectedNetworkId + " com IP " + assignedIp + ".");
            OnIpAssigned?.Invoke(this, assignedIp);
            OnConnected?.Invoke(this);
        }
        else
        {
            ClearConnectionData();
            ApplyState(NetworkState.SemRede, true);
            LogStateOnce("Nenhum IP industrial disponivel no roteador " + targetRouter.name + ".");
        }
    }

    private void UpdatePendingStateWhileStopping()
    {
        if (validRouters.Count == 0)
        {
            InvalidateOperationalAuthorization();
            DisconnectFromRouter(true);
            stateAfterSafeStop = NetworkState.SemRede;
            return;
        }

        if (validRouters.Count > 1)
        {
            InvalidateOperationalAuthorization();
            DisconnectFromRouter(true);
            stateAfterSafeStop = NetworkState.NetworkConflict;
            return;
        }

        if (stateAfterSafeStop != NetworkState.ConnectedWithoutAuthorization)
        {
            return;
        }

        RouterInteractable targetRouter = validRouters[0];
        if (connectedRouter != targetRouter || string.IsNullOrWhiteSpace(assignedIp))
        {
            stateAfterSafeStop = NetworkState.SemRede;
        }
    }

    private void LateUpdate()
    {
        if (currentNetworkState == NetworkState.Stopping && (controller == null || !controller.IsBusy))
        {
            ApplyState(stateAfterSafeStop, true);
            Debug.Log("[" + DeviceName + "] Retornou a posicao inicial e foi desligado.", this);
        }
    }

    private void ApplyStateOrStopSafely(NetworkState finalState)
    {
        stateAfterSafeStop = finalState;
        ApplyState(controller != null && controller.IsBusy ? NetworkState.Stopping : finalState, true);
    }

    private void CollectValidDhcpRouters()
    {
        validRouters.Clear();
        if (wiFiDevice == null)
        {
            validDhcpRoutersDetected = 0;
            return;
        }

        foreach (RouterInteractable router in wiFiDevice.AvailableRouters)
        {
            if (router == null
                || !router.CanProvideIndustrialDhcp
                || !router.IsWiFiDeviceInRange(wiFiDevice)
                || validRouters.Contains(router))
            {
                continue;
            }

            validRouters.Add(router);
        }

        validDhcpRoutersDetected = validRouters.Count;
    }

    private void DisconnectFromRouter(bool notify)
    {
        RouterInteractable previousRouter = connectedRouter;
        if (previousRouter != null)
        {
            previousRouter.ReleaseIndustrialDhcp(this);
        }

        bool hadConnection = !string.IsNullOrWhiteSpace(assignedIp) || previousRouter != null;
        ClearConnectionData();

        if (notify && hadConnection)
        {
            LogStateOnce("Rede perdida. IP liberado.");
            OnDisconnected?.Invoke(this);
        }
    }

    private void ClearConnectionData()
    {
        connectedRouter = null;
        assignedIp = string.Empty;
        connectedNetworkId = string.Empty;
        hasValidNetwork = false;
    }

    private void InvalidateOperationalAuthorization()
    {
        operationalAuthorization = false;
    }

    private void ApplyState(NetworkState nextState, bool notify)
    {
        bool changed = currentNetworkState != nextState;
        OperationalState previousOperationalState = cachedOperationalState;
        currentNetworkState = nextState;
        hasNetworkConflict = nextState == NetworkState.NetworkConflict || (nextState == NetworkState.Stopping && stateAfterSafeStop == NetworkState.NetworkConflict);
        hasValidNetwork = (nextState == NetworkState.ConnectedWithoutAuthorization || nextState == NetworkState.Operating || nextState == NetworkState.Stopping)
            && !string.IsNullOrWhiteSpace(assignedIp)
            && connectedRouter != null
            && !hasNetworkConflict;
        OperationalState nextOperationalState = CurrentOperationalState;
        cachedOperationalState = nextOperationalState;

        if (!changed && previousOperationalState == nextOperationalState)
        {
            return;
        }

        MissionManager.NotifyStage2RoboticArmOperationChanged();

        if (notify)
        {
            if (changed)
            {
                OnNetworkStateChanged?.Invoke(this, nextState);
            }

            if (previousOperationalState != nextOperationalState)
            {
                OnOperationalStateChanged?.Invoke(this, nextOperationalState);
            }

            if (hasNetworkConflict)
            {
                OnNetworkConflict?.Invoke(this);
            }
        }
    }

    private void ResolveReferences()
    {
        if (wiFiDevice == null)
        {
            wiFiDevice = GetComponent<WiFiDevice>();
        }

        if (controller == null)
        {
            controller = GetComponent<RoboticArmController>();
        }

        if (statusLightRenderer == null)
        {
            statusLightRenderer = FindStatusRenderer();
        }

        if (statusLight == null)
        {
            statusLight = GetComponentInChildren<Light>(true);
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }
    }

    private void ConfigureWiFiDevice()
    {
        if (wiFiDevice != null)
        {
            wiFiDevice.ConfigureIdentity(WiFiDeviceType.RoboticArm, DeviceId);
            wifiSensorRadius = wiFiDevice.SensorRadius;
        }
    }

    private Renderer FindStatusRenderer()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            string lowerName = renderer.name.ToLowerInvariant();
            if (lowerName.Contains("light") || lowerName.Contains("luz") || lowerName.Contains("status") || lowerName.Contains("indicator"))
            {
                return renderer;
            }
        }

        return null;
    }

    private void UpdateStatusLight()
    {
        Color stateColor = currentNetworkState == NetworkState.Operating ? authorizedColor
            : currentNetworkState == NetworkState.ConnectedWithoutAuthorization || currentNetworkState == NetworkState.Connecting ? connectedColor
            : disconnectedColor;

        bool shouldBlink = currentNetworkState != NetworkState.Operating;
        bool visible = !shouldBlink || blinkFrequency <= 0f || Mathf.Repeat(Time.time * blinkFrequency, 1f) < 0.5f;
        Color appliedColor = visible ? stateColor : Color.black;

        if (statusLightRenderer != null)
        {
            statusLightRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", appliedColor);
            propertyBlock.SetColor("_EmissionColor", appliedColor);
            statusLightRenderer.SetPropertyBlock(propertyBlock);
        }

        if (statusLight != null)
        {
            statusLight.enabled = visible;
            statusLight.color = stateColor;
        }
    }

    private void LogStateOnce(string message)
    {
        if (!logNetworkEvents || lastLoggedState == currentNetworkState)
        {
            return;
        }

        lastLoggedState = currentNetworkState;
        Debug.Log("[" + DeviceName + "] " + message, this);
    }
}
