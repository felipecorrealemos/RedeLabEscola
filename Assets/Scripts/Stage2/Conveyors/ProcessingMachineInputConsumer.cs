using UnityEngine;

[DisallowMultipleComponent]
public class ProcessingMachineInputConsumer : MonoBehaviour
{
    [SerializeField] private ProcessingMachineController processingMachine;
    [SerializeField] private ConveyorController sourceConveyor;
    [SerializeField] private ConveyorCollectionZone collectionZone;
    [SerializeField] private RoboticArmProductType acceptedItemType = RoboticArmProductType.Pipes;
    [SerializeField] private string acceptedProductId = "RawMaterial_A";
    [SerializeField] private bool consumeFromCollectionZone = true;
    [SerializeField] private bool consumeFromTrigger = true;
    [SerializeField] private bool onlyConsumeStoppedItems;
    [SerializeField] private bool logRejectedItems = true;

    private ConveyorItem lastRejectedItem;

    public RoboticArmProductType AcceptedItemType => acceptedItemType;
    public string AcceptedProductId => acceptedProductId;

    public void Configure(ProcessingMachineController machine, ConveyorController conveyor, ConveyorCollectionZone zone, RoboticArmProductType itemType, string productId)
    {
        processingMachine = machine;
        sourceConveyor = conveyor;
        collectionZone = zone;
        acceptedItemType = itemType;
        acceptedProductId = productId;
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureTriggerCollider();
    }

    private void Update()
    {
        if (!consumeFromCollectionZone || processingMachine == null || collectionZone == null)
        {
            return;
        }

        GameObject availableObject = collectionZone.GetAvailableItem();
        if (availableObject == null)
        {
            return;
        }

        ConveyorItem item = availableObject.GetComponent<ConveyorItem>();
        if (!IsAccepted(item))
        {
            WarnRejected(item);
            return;
        }

        TryConsumeItem(item);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryConsumeFromTrigger(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryConsumeFromTrigger(other);
    }

    private void ResolveReferences()
    {
        if (sourceConveyor == null)
        {
            sourceConveyor = GetComponentInParent<ConveyorController>();
        }

        if (collectionZone == null && sourceConveyor != null)
        {
            collectionZone = sourceConveyor.CollectionZone;
        }

        if (processingMachine == null)
        {
            processingMachine = FindObjectOfType<ProcessingMachineController>();
        }
    }

    private bool IsAccepted(ConveyorItem item)
    {
        if (item == null)
        {
            return false;
        }

        if (onlyConsumeStoppedItems && !item.IsStoppedForJamSensor)
        {
            return false;
        }

        if (processingMachine != null && processingMachine.AcceptsAnyMaterial)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(acceptedProductId)
            || string.Equals(item.ProductId, acceptedProductId, System.StringComparison.OrdinalIgnoreCase);
    }

    private void TryConsumeFromTrigger(Collider other)
    {
        if (!consumeFromTrigger || processingMachine == null || other == null)
        {
            return;
        }

        ConveyorItem item = other.GetComponentInParent<ConveyorItem>();
        TryConsumeItem(item);
    }

    private bool TryConsumeItem(ConveyorItem item)
    {
        if (!IsAccepted(item))
        {
            WarnRejected(item);
            return false;
        }

        if (processingMachine.TryReceiveMaterial(acceptedItemType, item))
        {
            lastRejectedItem = null;
            return true;
        }

        return false;
    }

    private void EnsureTriggerCollider()
    {
        Collider inputCollider = GetComponent<Collider>();
        if (inputCollider != null)
        {
            inputCollider.isTrigger = true;
        }
    }

    private void WarnRejected(ConveyorItem item)
    {
        if (!logRejectedItems || item == null || lastRejectedItem == item)
        {
            return;
        }

        Debug.LogWarning($"{name} rejected '{item.ProductId}'. Expected '{acceptedProductId}'.", this);
        lastRejectedItem = item;
    }
}
