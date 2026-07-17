using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoboticArmDropAreaSensor : MonoBehaviour
{
    [SerializeField] private LayerMask detectionMask = ~0;
    [SerializeField] private bool ignoreCarriedItems = true;
    [SerializeField] private bool onlyTrackConveyorItems = true;
    [SerializeField] private int occupiedCount;

    private readonly HashSet<Collider> colliders = new HashSet<Collider>();

    public bool IsOccupied
    {
        get
        {
            RefreshOverlappingItems();
            RefreshCount();
            return occupiedCount > 0;
        }
    }
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

        Collider ownCollider = GetComponent<Collider>();
        if (other == ownCollider)
        {
            return false;
        }

        if ((detectionMask.value & (1 << other.gameObject.layer)) == 0)
        {
            return false;
        }

        ConveyorItem item = other.GetComponentInParent<ConveyorItem>();
        if (item == null)
        {
            return !onlyTrackConveyorItems;
        }

        return !ignoreCarriedItems || !item.IsBeingCarried;
    }

    private void RefreshCount()
    {
        colliders.RemoveWhere(collider => collider == null || !collider.gameObject.activeInHierarchy || !ShouldTrack(collider));
        occupiedCount = colliders.Count;
    }

    private void RefreshOverlappingItems()
    {
        Collider sensorCollider = GetComponent<Collider>();
        if (sensorCollider == null)
        {
            return;
        }

        Collider[] overlaps = GetOverlaps(sensorCollider);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (ShouldTrack(overlaps[i]))
            {
                colliders.Add(overlaps[i]);
            }
        }
    }

    private Collider[] GetOverlaps(Collider sensorCollider)
    {
        BoxCollider box = sensorCollider as BoxCollider;
        if (box != null)
        {
            Vector3 worldCenter = box.transform.TransformPoint(box.center);
            Vector3 halfExtents = Vector3.Scale(box.size, Abs(box.transform.lossyScale)) * 0.5f;
            return Physics.OverlapBox(worldCenter, halfExtents, box.transform.rotation, detectionMask, QueryTriggerInteraction.Collide);
        }

        SphereCollider sphere = sensorCollider as SphereCollider;
        if (sphere != null)
        {
            Vector3 worldCenter = sphere.transform.TransformPoint(sphere.center);
            float radius = sphere.radius * MaxComponent(Abs(sphere.transform.lossyScale));
            return Physics.OverlapSphere(worldCenter, radius, detectionMask, QueryTriggerInteraction.Collide);
        }

        Bounds bounds = sensorCollider.bounds;
        return Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, detectionMask, QueryTriggerInteraction.Collide);
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }
}
