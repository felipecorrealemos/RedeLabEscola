using UnityEngine;

[DisallowMultipleComponent]
public class EmpilhadeiraBeltPalletDropSensor : MonoBehaviour
{
    [SerializeField] private Collider beltCollider;
    [SerializeField] private ConveyorController targetConveyor;
    [SerializeField] private Transform receivePoint;
    [SerializeField] private string productId = "PalletWithBoxes";
    [SerializeField] private bool keepCurrentRotation = true;
    [SerializeField] private float lateralOffset;
    [SerializeField] private bool requireLowerInput = true;
    [SerializeField] private bool requireForkSensorOverlap = true;
    [SerializeField] private LayerMask palletLayers = ~0;
    [SerializeField] private string palletNameFilter = "Pallet";

    public bool RequireLowerInput => requireLowerInput;
    public bool RequireForkSensorOverlap => requireForkSensorOverlap;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnTriggerEnter(Collider other)
    {
        TryReleaseCarriedPallet(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryReleaseCarriedPallet(other);
    }

    private void TryReleaseCarriedPallet(Collider other)
    {
        if (!IsValidPalletCollider(other))
        {
            return;
        }

        EmpilhadeiraController controller = other.GetComponentInParent<EmpilhadeiraController>();
        if (controller == null)
        {
            return;
        }

        controller.NotifyBeltTouchedByCarriedPallet(this, other, targetConveyor, receivePoint, productId, keepCurrentRotation, lateralOffset);
    }

    private bool IsValidPalletCollider(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        if ((palletLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(palletNameFilter))
        {
            return true;
        }

        Transform current = other.transform;
        while (current != null)
        {
            if (current.name.IndexOf(palletNameFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void ResolveReferences()
    {
        if (beltCollider == null)
        {
            beltCollider = GetComponent<Collider>();
        }

        if (targetConveyor == null)
        {
            Transform machineRoot = FindParentByName(transform, "PalletMachine");
            Transform outputConveyor = machineRoot != null ? FindChildRecursive(machineRoot, "PalletOutputConveyor") : null;
            if (outputConveyor != null)
            {
                targetConveyor = outputConveyor.GetComponent<ConveyorController>();
            }
        }

        if (targetConveyor == null)
        {
            targetConveyor = GetComponentInParent<ConveyorController>();
        }
    }

    public bool ContainsForkSensor(Collider forkSensor)
    {
        ResolveReferences();
        return beltCollider != null && forkSensor != null && beltCollider.bounds.Intersects(forkSensor.bounds);
    }

    private static Transform FindParentByName(Transform start, string targetName)
    {
        Transform current = start;
        while (current != null)
        {
            if (current.name == targetName)
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
