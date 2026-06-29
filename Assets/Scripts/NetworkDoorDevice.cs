using UnityEngine;

[DisallowMultipleComponent]
public class NetworkDoorDevice : MonoBehaviour
{
    [SerializeField] private string deviceLabel = "Dispositivo da porta";
    [SerializeField] private bool autoAssignPreferredIp = true;
    [SerializeField] private string preferredIpAddress = "192.168.0.5";
    [SerializeField] private Transform doorPivot;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 180f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool controlledByAccessGroup;
    private bool capturedClosedRotation;

    public string DeviceLabel => deviceLabel;
    public bool AutoAssignPreferredIp => autoAssignPreferredIp;
    public string PreferredIpAddress => preferredIpAddress;
    public bool IsControlledByAccessGroup => controlledByAccessGroup;
    public bool IsOpen { get; private set; }
    public string StateLabel => IsOpen ? "Aberta" : "Fechada";
    public string ActionLabel => IsOpen ? "Fechar" : "Abrir";
    public bool CanOperate
    {
        get
        {
            ComputerInteractable networkDevice = GetComponent<ComputerInteractable>();
            return networkDevice != null && networkDevice.IsNetworkOperational;
        }
    }

    private void Awake()
    {
        EnsureDoorPivot();
        CaptureClosedRotation();
    }

    private void Update()
    {
        if (doorPivot == null)
        {
            return;
        }

        Quaternion targetRotation = IsOpen ? openRotation : closedRotation;
        doorPivot.localRotation = Quaternion.RotateTowards(doorPivot.localRotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    public void Toggle()
    {
        EnsureDoorPivot();
        if (doorPivot == null || controlledByAccessGroup || !CanOperate)
        {
            return;
        }

        IsOpen = !IsOpen;
    }

    public void SetControlledByAccessGroup(bool controlled)
    {
        controlledByAccessGroup = controlled;
    }

    public void SetOpenFromAccessGroup(bool open)
    {
        EnsureDoorPivot();
        if (doorPivot == null || !CanOperate)
        {
            return;
        }

        if (!capturedClosedRotation)
        {
            CaptureClosedRotation();
        }

        IsOpen = open;
    }

    private void CaptureClosedRotation()
    {
        EnsureDoorPivot();
        if (doorPivot == null)
        {
            return;
        }

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        capturedClosedRotation = true;
    }

    private void EnsureDoorPivot()
    {
        if (doorPivot != null)
        {
            Transform resolvedPivot = ResolvePivotForDoorTransform(doorPivot);
            if (resolvedPivot != null)
            {
                doorPivot = resolvedPivot;
            }

            return;
        }

        doorPivot = FindNearestDoorPivot();
    }

    private Transform FindNearestDoorPivot()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        Transform nearestPivot = null;
        float nearestDistance = float.MaxValue;

        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.IsChildOf(transform) || !IsPivotTransform(candidate) || !ContainsDoorTransform(candidate))
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(candidate.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestPivot = candidate;
            }
        }

        if (nearestPivot != null)
        {
            return nearestPivot;
        }

        foreach (Transform candidate in transforms)
        {
            if (candidate == null || candidate.IsChildOf(transform) || !IsDoorTransform(candidate))
            {
                continue;
            }

            Transform pivot = ResolvePivotForDoorTransform(candidate);
            if (pivot == null)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(pivot.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestPivot = pivot;
            }
        }

        return nearestPivot;
    }

    private bool IsDoorTransform(Transform candidate)
    {
        string lowerName = candidate.name.ToLowerInvariant();
        return lowerName == "door" || lowerName.StartsWith("door ");
    }

    private Transform ResolvePivotForDoorTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        if (IsPivotTransform(candidate))
        {
            return candidate;
        }

        Transform parent = candidate.parent;
        while (parent != null)
        {
            if (IsPivotTransform(parent))
            {
                return parent;
            }

            parent = parent.parent;
        }

        return FindNearestPivotUnder(candidate);
    }

    private Transform FindNearestPivotUnder(Transform root)
    {
        Transform nearestPivot = null;
        float nearestDistance = float.MaxValue;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == null || !IsPivotTransform(child))
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(child.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestPivot = child;
            }
        }

        return nearestPivot;
    }

    private bool ContainsDoorTransform(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child != root && IsDoorTransform(child))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPivotTransform(Transform candidate)
    {
        string lowerName = candidate.name.ToLowerInvariant();
        return lowerName == "pivot" || lowerName == "pivo" || lowerName.StartsWith("piv");
    }
}
