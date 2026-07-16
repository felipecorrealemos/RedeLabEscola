using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoboticArmPickupSensor : MonoBehaviour
{
    [SerializeField] private RoboticArmController controller;
    [SerializeField] private int queuedItems;

    private readonly Queue<ConveyorItem> candidates = new Queue<ConveyorItem>();

    public int QueuedItems => queuedItems;

    public void Configure(RoboticArmController owner)
    {
        controller = owner;
    }

    public ConveyorItem DequeueNextValid()
    {
        while (candidates.Count > 0)
        {
            ConveyorItem item = candidates.Dequeue();
            queuedItems = candidates.Count;
            if (item != null && controller != null && controller.CanAcceptItem(item))
            {
                return item;
            }
        }

        queuedItems = 0;
        return null;
    }

    private void Reset()
    {
        controller = GetComponentInParent<RoboticArmController>();
        Collider sensorCollider = GetComponent<Collider>();
        if (sensorCollider != null)
        {
            sensorCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (controller == null || other == null)
        {
            return;
        }

        ConveyorItem item = other.GetComponentInParent<ConveyorItem>();
        if (item == null || !controller.CanAcceptItem(item))
        {
            controller.ReportRejectedItem(item, other.gameObject);
            return;
        }

        candidates.Enqueue(item);
        queuedItems = candidates.Count;
        controller.ReportDetectedItem(item);
    }
}
