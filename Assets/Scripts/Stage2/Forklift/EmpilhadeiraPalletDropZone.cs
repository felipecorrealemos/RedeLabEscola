using UnityEngine;

[DisallowMultipleComponent]
public class EmpilhadeiraPalletDropZone : MonoBehaviour
{
    [SerializeField] private Collider dropZoneTrigger;
    [SerializeField] private Transform palletPlacementPoint;
    [SerializeField] private ConveyorController targetConveyor;
    [SerializeField] private string palletProductId = "PalletWithBoxes";
    [SerializeField] private bool snapToPlacementPoint;

    public Transform PalletPlacementPoint => palletPlacementPoint;
    public ConveyorController TargetConveyor => targetConveyor;
    public string PalletProductId => palletProductId;
    public bool SnapToPlacementPoint => snapToPlacementPoint;

    public bool ContainsForkSensor(Collider forkSensor)
    {
        if (forkSensor == null || !forkSensor.enabled)
        {
            return false;
        }

        ResolveReferences();
        if (dropZoneTrigger == null)
        {
            return false;
        }

        return dropZoneTrigger.bounds.Intersects(forkSensor.bounds);
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureTrigger();
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        ConfigureTrigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyForklift(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyForklift(other, true);
    }

    private void OnTriggerExit(Collider other)
    {
        NotifyForklift(other, false);
    }

    private void NotifyForklift(Collider other, bool inside)
    {
        if (other == null)
        {
            return;
        }

        EmpilhadeiraForkPickupTrigger forkSensor = other.GetComponentInParent<EmpilhadeiraForkPickupTrigger>();
        if (forkSensor == null)
        {
            return;
        }

        EmpilhadeiraController controller = forkSensor.GetComponentInParent<EmpilhadeiraController>();
        if (controller == null)
        {
            return;
        }

        if (inside)
        {
            controller.NotifyDropZoneEnter(this);
        }
        else
        {
            controller.NotifyDropZoneExit(this);
        }
    }

    private void ResolveReferences()
    {
        if (dropZoneTrigger == null)
        {
            dropZoneTrigger = GetComponent<Collider>();
        }

        if (palletPlacementPoint == null)
        {
            Transform placement = transform.Find("PalletPlacementPoint");
            if (placement != null)
            {
                palletPlacementPoint = placement;
            }
        }

        if (targetConveyor == null)
        {
            targetConveyor = GetComponentInParent<ConveyorController>();
        }
    }

    private void ConfigureTrigger()
    {
        if (dropZoneTrigger == null)
        {
            dropZoneTrigger = GetComponent<Collider>();
        }

        if (dropZoneTrigger != null)
        {
            dropZoneTrigger.isTrigger = true;
        }
    }
}
