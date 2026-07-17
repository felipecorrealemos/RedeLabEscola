using UnityEngine;

[DisallowMultipleComponent]
public class PackagingMachineInputConsumer : MonoBehaviour
{
    [SerializeField] private PackagingMachineController packagingMachine;
    [SerializeField] private bool consumeFromTrigger = true;
    [SerializeField] private bool logRejectedItems;

    private ConveyorItem lastRejectedItem;

    public void Configure(PackagingMachineController machine)
    {
        packagingMachine = machine;
    }

    private void Awake()
    {
        if (packagingMachine == null)
        {
            packagingMachine = GetComponentInParent<PackagingMachineController>();
        }

        Collider inputCollider = GetComponent<Collider>();
        if (inputCollider != null)
        {
            inputCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryConsume(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryConsume(other);
    }

    private void TryConsume(Collider other)
    {
        if (!consumeFromTrigger || packagingMachine == null || other == null)
        {
            return;
        }

        ConveyorItem item = other.GetComponentInParent<ConveyorItem>();
        if (item == null)
        {
            return;
        }

        if (packagingMachine.TryReceiveItem(item))
        {
            lastRejectedItem = null;
        }
        else if (logRejectedItems && lastRejectedItem != item)
        {
            Debug.LogWarning($"{name} rejected '{item.ProductId}'. Expected '{packagingMachine.AcceptedProductId}'.", this);
            lastRejectedItem = item;
        }
    }
}
