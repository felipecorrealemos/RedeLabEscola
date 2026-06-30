using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkScope : MonoBehaviour
{
    [Serializable]
    public class IpLease
    {
        public string Address;
        public ComputerInteractable AssignedComputer;
        public string AssignedDeviceName;

        public bool IsRouter => AssignedComputer == null && Address != null && Address.EndsWith(".1");
        public bool IsAvailable => AssignedComputer == null && string.IsNullOrWhiteSpace(AssignedDeviceName) && !IsRouter;
    }

    [SerializeField] private string networkPrefix = "192.168.0.";
    [SerializeField] private int routerAddress = 1;
    [SerializeField] private int firstDeviceAddress = 2;
    [SerializeField] private int availableAddressCount = 4;
    [SerializeField] private bool createHierarchyView = true;
    [SerializeField] private RouterInteractable ownerRouter;

    private readonly List<IpLease> leases = new List<IpLease>();

    public event Action OnIpPoolChanged;
    public IReadOnlyList<IpLease> Leases => leases;
    public string NetworkPrefix => networkPrefix;
    public string RouterIpAddress => networkPrefix + routerAddress;
    public int LastDeviceAddress => firstDeviceAddress + Mathf.Max(availableAddressCount, 1) - 1;
    public RouterInteractable OwnerRouter => ownerRouter;

    private void Awake()
    {
        RebuildLeases();
        RefreshHierarchyView();
    }

    private void OnValidate()
    {
        RebuildLeases();
    }

    public void Configure(string prefix, int router, int firstDevice, int addressCount)
    {
        Configure(prefix, router, firstDevice, addressCount, ownerRouter);
    }

    public void Configure(string prefix, int router, int firstDevice, int addressCount, RouterInteractable owner)
    {
        string nextPrefix = string.IsNullOrWhiteSpace(prefix) ? networkPrefix : prefix;
        int nextRouterAddress = Mathf.Max(router, 1);
        int nextFirstDeviceAddress = Mathf.Max(firstDevice, 1);
        int nextAvailableAddressCount = Mathf.Max(addressCount, 1);
        RouterInteractable nextOwnerRouter = owner != null ? owner : ownerRouter;
        bool changed = networkPrefix != nextPrefix
            || routerAddress != nextRouterAddress
            || firstDeviceAddress != nextFirstDeviceAddress
            || availableAddressCount != nextAvailableAddressCount
            || ownerRouter != nextOwnerRouter;

        if (!changed)
        {
            return;
        }

        networkPrefix = nextPrefix;
        routerAddress = nextRouterAddress;
        firstDeviceAddress = nextFirstDeviceAddress;
        availableAddressCount = nextAvailableAddressCount;
        ownerRouter = nextOwnerRouter;
        RebuildLeases();
        NotifyPoolChanged();
    }

    public void SetOwnerRouter(RouterInteractable owner)
    {
        if (ownerRouter == owner)
        {
            return;
        }

        ownerRouter = owner;
        RefreshHierarchyView();
    }

    public bool TryAssignIp(ComputerInteractable computer, string address, string reservedDeviceName)
    {
        if (computer == null || string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        IpLease targetLease = leases.Find(lease => lease.Address == address);
        if (targetLease == null || targetLease.IsRouter)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(targetLease.AssignedDeviceName) && targetLease.AssignedDeviceName != reservedDeviceName)
        {
            return false;
        }

        if (targetLease.AssignedComputer != null && targetLease.AssignedComputer != computer)
        {
            return false;
        }

        ReleaseIp(computer, false);
        targetLease.AssignedComputer = computer;
        targetLease.AssignedDeviceName = string.Empty;
        NotifyPoolChanged();
        return true;
    }

    public void ReleaseIp(ComputerInteractable computer)
    {
        ReleaseIp(computer, true);
    }

    public bool ContainsAddress(string address)
    {
        return !string.IsNullOrWhiteSpace(address) && leases.Exists(lease => lease.Address == address);
    }

    private void ReleaseIp(ComputerInteractable computer, bool notify)
    {
        if (computer == null)
        {
            return;
        }

        bool changed = false;
        foreach (IpLease lease in leases)
        {
            if (lease.AssignedComputer == computer)
            {
                lease.AssignedComputer = null;
                changed = true;
            }
        }

        if (changed && notify)
        {
            NotifyPoolChanged();
        }
    }

    private void NotifyPoolChanged()
    {
        RefreshHierarchyView();
        OnIpPoolChanged?.Invoke();
    }

    private void RebuildLeases()
    {
        Dictionary<string, ComputerInteractable> currentAssignments = new Dictionary<string, ComputerInteractable>();
        foreach (IpLease lease in leases)
        {
            if (lease.AssignedComputer != null && !string.IsNullOrWhiteSpace(lease.Address))
            {
                currentAssignments[lease.Address] = lease.AssignedComputer;
            }
        }

        leases.Clear();
        leases.Add(new IpLease { Address = RouterIpAddress });

        int addressCount = Mathf.Max(availableAddressCount, 1);
        for (int i = 0; i < addressCount; i++)
        {
            string address = networkPrefix + (firstDeviceAddress + i);
            currentAssignments.TryGetValue(address, out ComputerInteractable assignedComputer);
            leases.Add(new IpLease
            {
                Address = address,
                AssignedComputer = assignedComputer,
                AssignedDeviceName = string.Empty
            });
        }
    }

    private void RefreshHierarchyView()
    {
        if (!createHierarchyView || !Application.isPlaying)
        {
            return;
        }

        name = GetHierarchyName();
        RemoveStaleLeaseNodes();

        foreach (IpLease lease in leases)
        {
            Transform leaseNode = FindLeaseNode(lease.Address);
            if (leaseNode == null)
            {
                GameObject leaseObject = new GameObject(lease.Address);
                leaseNode = leaseObject.transform;
                leaseNode.SetParent(transform, false);
            }

            leaseNode.name = GetLeaseNodeName(lease);
        }
    }

    private string GetLeaseNodeName(IpLease lease)
    {
        if (lease.IsRouter)
        {
            return lease.Address + " - Roteador";
        }

        if (lease.IsAvailable)
        {
            return lease.Address + " - Livre";
        }

        if (lease.AssignedComputer != null)
        {
            return lease.Address + " - " + lease.AssignedComputer.DeviceTitle;
        }

        return lease.Address + " - Em uso";
    }

    private string TrimTrailingDot(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Rede" : value.TrimEnd('.');
    }

    private string GetHierarchyName()
    {
        string ownerName = ownerRouter != null ? SanitizeObjectName(ownerRouter.name) : "SemRoteador";
        return "Network_" + TrimTrailingDot(networkPrefix) + "_" + ownerName;
    }

    private string SanitizeObjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Roteador";
        }

        return value.Replace(" ", "_").Replace("/", "_").Replace("\\", "_");
    }

    private void RemoveStaleLeaseNodes()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child == null || IsCurrentLeaseNode(child.name))
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private bool IsCurrentLeaseNode(string nodeName)
    {
        foreach (IpLease lease in leases)
        {
            if (!string.IsNullOrWhiteSpace(lease.Address)
                && (nodeName == lease.Address || nodeName.StartsWith(lease.Address + " - ", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private Transform FindLeaseNode(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && (child.name == address || child.name.StartsWith(address + " - ", StringComparison.Ordinal)))
            {
                return child;
            }
        }

        return null;
    }
}
