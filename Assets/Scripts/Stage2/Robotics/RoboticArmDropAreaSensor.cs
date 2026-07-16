using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoboticArmDropAreaSensor : MonoBehaviour
{
    [SerializeField] private LayerMask detectionMask = ~0;
    [SerializeField] private bool ignoreCarriedItems = true;
    [SerializeField] private int occupiedCount;

    private readonly HashSet<Collider> colliders = new HashSet<Collider>();

    public bool IsOccupied => occupiedCount > 0;
    public int OccupiedCount => occupiedCount;

    private void OnTriggerEnter(Collider other)
    {
        if (ShouldTrack(other))
        {
            colliders.Add(other);
            RefreshCount();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (colliders.Remove(other))
        {
            RefreshCount();
        }
    }

    public void Clear()
    {
        colliders.Clear();
        occupiedCount = 0;
    }

    private bool ShouldTrack(Collider other)
    {
        if (other == null || !other.gameObject.activeInHierarchy)
        {
            return false;
        }

        if ((detectionMask.value & (1 << other.gameObject.layer)) == 0)
        {
            return false;
        }

        ConveyorItem item = other.GetComponentInParent<ConveyorItem>();
        return item == null || !ignoreCarriedItems || !item.IsBeingCarried;
    }

    private void RefreshCount()
    {
        colliders.RemoveWhere(collider => collider == null || !collider.gameObject.activeInHierarchy || !ShouldTrack(collider));
        occupiedCount = colliders.Count;
    }
}
