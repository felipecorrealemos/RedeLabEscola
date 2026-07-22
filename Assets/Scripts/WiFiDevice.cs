using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WiFiDevice : MonoBehaviour
{
    [SerializeField] private WiFiDeviceType deviceType = WiFiDeviceType.Notebook;
    [SerializeField] private string deviceIdentifier;
    [SerializeField] private float sensorRadius = 0.35f;

    private readonly HashSet<RouterInteractable> availableRouters = new HashSet<RouterInteractable>();
    private SphereCollider sensorCollider;

    public WiFiDeviceType DeviceType => deviceType;
    public string DeviceIdentifier => string.IsNullOrWhiteSpace(deviceIdentifier) ? name : deviceIdentifier;
    public float SensorRadius => sensorRadius;
    public IReadOnlyCollection<RouterInteractable> AvailableRouters => availableRouters;
    public ComputerInteractable Computer { get; private set; }

    private void Awake()
    {
        EnsureSensorCollider();
        Computer = GetComponent<ComputerInteractable>();
        if (Computer == null)
        {
            Computer = GetComponentInParent<ComputerInteractable>();
        }

        if (string.IsNullOrWhiteSpace(deviceIdentifier))
        {
            deviceIdentifier = name;
        }
    }

    private void Reset()
    {
        EnsureSensorCollider();
    }

    private void OnValidate()
    {
        sensorRadius = Mathf.Max(sensorRadius, 0.05f);
        if (sensorCollider != null)
        {
            sensorCollider.radius = sensorRadius;
        }
    }

    private void OnDisable()
    {
        ClearAvailableRouters();
    }

    public void SetRouterAvailable(RouterInteractable router, bool available)
    {
        if (router == null)
        {
            return;
        }

        if (available)
        {
            availableRouters.Add(router);
        }
        else
        {
            availableRouters.Remove(router);
        }

        Computer?.HandleWiFiAvailabilityChanged();
    }

    public bool IsRouterAvailable(RouterInteractable router)
    {
        return router != null && availableRouters.Contains(router);
    }

    public void Configure(WiFiDeviceType type, string identifier, float radius)
    {
        deviceType = type;
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            deviceIdentifier = identifier;
        }

        sensorRadius = Mathf.Max(radius, 0.05f);
        EnsureSensorCollider();
        if (sensorCollider != null)
        {
            sensorCollider.radius = sensorRadius;
        }
    }

    public void ConfigureIdentity(WiFiDeviceType type, string identifier)
    {
        deviceType = type;
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            deviceIdentifier = identifier;
        }

        EnsureSensorCollider();
    }

    public bool IsWiFiSensorCollider(Collider candidate)
    {
        return candidate != null && candidate.isTrigger && candidate.GetComponentInParent<WiFiDevice>() == this;
    }

    public void ClearAvailableRouters()
    {
        if (availableRouters.Count == 0)
        {
            return;
        }

        availableRouters.Clear();
        Computer?.HandleWiFiAvailabilityChanged();
    }

    private void EnsureSensorCollider()
    {
        SphereCollider[] sphereColliders = GetComponents<SphereCollider>();
        for (int i = 0; i < sphereColliders.Length; i++)
        {
            if (sphereColliders[i] != null && sphereColliders[i].isTrigger)
            {
                sensorCollider = sphereColliders[i];
                break;
            }
        }

        if (sensorCollider == null)
        {
            sensorCollider = gameObject.AddComponent<SphereCollider>();
        }

        sensorCollider.isTrigger = true;
        sensorCollider.radius = Mathf.Max(sensorRadius, 0.05f);
    }
}
