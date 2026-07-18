using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PackagingMachineInputConsumer : MonoBehaviour
{
    [SerializeField] private PackagingMachineController packagingMachine;
    [SerializeField] private bool consumeFromTrigger = true;
    [SerializeField] private bool logRejectedItems;

    private ConveyorItem lastRejectedItem;
    private readonly HashSet<int> consumedItemIds = new HashSet<int>();

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

        int itemId = item.GetInstanceID();
        if (consumedItemIds.Contains(itemId))
        {
            return;
        }

        if (packagingMachine.TryReceiveItem(item))
        {
            consumedItemIds.Add(itemId);
            lastRejectedItem = null;
        }
        else if (logRejectedItems && lastRejectedItem != item)
        {
            Debug.LogWarning($"{name} rejected '{item.ProductId}'. Expected '{packagingMachine.AcceptedProductId}'.", this);
            lastRejectedItem = item;
        }
    }
}
