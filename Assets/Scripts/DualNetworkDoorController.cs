using UnityEngine;

[DisallowMultipleComponent]
public class DualNetworkDoorController : MonoBehaviour
{
    [SerializeField] private string doorLabel = "Porta dupla";
    [SerializeField] private GameObject firstDeviceObject;
    [SerializeField] private GameObject secondDeviceObject;
    [SerializeField] private NetworkDoorDevice firstDevice;
    [SerializeField] private NetworkDoorDevice secondDevice;
    [SerializeField] private bool requireManualIpAssignment = true;
    [SerializeField] private bool closeWhenRequirementsAreLost = true;

    public string DoorLabel => doorLabel;
    public NetworkDoorDevice FirstDevice => ResolveDevice(ref firstDevice, firstDeviceObject, true);
    public NetworkDoorDevice SecondDevice => ResolveDevice(ref secondDevice, secondDeviceObject, true);
    public bool RequiresManualIpAssignment => requireManualIpAssignment;
    public bool IsOpen { get; private set; }
    public string StateLabel => IsOpen ? "Aberta" : "Fechada";
    public string ActionLabel => IsOpen ? "Fechar" : "Abrir";
    public bool CanOperate => IsDeviceOperational(FirstDevice) && IsDeviceOperational(SecondDevice);

    private void Awake()
    {
        RefreshControlledDevices();
    }

    private void OnValidate()
    {
        ResolveDevice(ref firstDevice, firstDeviceObject, false);
        ResolveDevice(ref secondDevice, secondDeviceObject, false);
    }

    private void Update()
    {
        RefreshControlledDevices();

        if (closeWhenRequirementsAreLost && IsOpen && !CanOperate)
        {
            SetOpen(false);
        }
    }

    public bool Controls(NetworkDoorDevice device)
    {
        return device != null && (device == FirstDevice || device == SecondDevice);
    }

    public bool Controls(GameObject deviceObject)
    {
        if (deviceObject == null)
        {
            return false;
        }

        return deviceObject == firstDeviceObject
            || deviceObject == secondDeviceObject
            || (FirstDevice != null && deviceObject == FirstDevice.gameObject)
            || (SecondDevice != null && deviceObject == SecondDevice.gameObject);
    }

    public void Toggle()
    {
        if (!CanOperate)
        {
            return;
        }

        SetOpen(!IsOpen);
    }

    private void SetOpen(bool open)
    {
        IsOpen = open;
        SetDeviceOpen(FirstDevice, open);
        SetDeviceOpen(SecondDevice, open);
        MissionManager.NotifyDualDoorsStateChanged(IsOpen);
    }

    private void RefreshControlledDevices()
    {
        MarkControlled(FirstDevice);
        MarkControlled(SecondDevice);
    }

    private void MarkControlled(NetworkDoorDevice device)
    {
        if (device != null)
        {
            device.SetControlledByAccessGroup(true);
        }
    }

    private void SetDeviceOpen(NetworkDoorDevice device, bool open)
    {
        if (device != null)
        {
            device.SetOpenFromAccessGroup(open);
        }
    }

    private bool IsDeviceOperational(NetworkDoorDevice device)
    {
        return device != null && device.CanOperate;
    }

    private NetworkDoorDevice ResolveDevice(ref NetworkDoorDevice device, GameObject deviceObject, bool addMissingComponent)
    {
        if (device != null)
        {
            return device;
        }

        if (deviceObject == null)
        {
            return null;
        }

        device = deviceObject.GetComponent<NetworkDoorDevice>();
        if (device == null && addMissingComponent)
        {
            device = deviceObject.AddComponent<NetworkDoorDevice>();
        }

        return device;
    }
}
