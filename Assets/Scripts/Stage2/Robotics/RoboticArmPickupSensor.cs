using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoboticArmPickupSensor : MonoBehaviour
{
    [SerializeField] private RoboticArmController controller;
    [SerializeField] private int queuedItems;
    [SerializeField] private LayerMask detectionMask = ~0;

    private readonly List<ConveyorItem> candidates = new List<ConveyorItem>();

    public int QueuedItems => queuedItems;

    public void Configure(RoboticArmController owner)
    {
        controller = owner;
    }

    public ConveyorItem DequeueNextValid()
    {
        RefreshOverlappingItems();

        for (int i = 0; i < candidates.Count; i++)
        {
            ConveyorItem item = candidates[i];
            if (item != null && controller != null && controller.CanAcceptItem(item))
            {
                candidates.RemoveAt(i);
                queuedItems = candidates.Count;
                return item;
            }
        }

        CleanupCandidates();
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
        TrackCandidate(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TrackCandidate(other);
    }

    private void OnTriggerExit(Collider other)
    {
        ConveyorItem item = other != null ? other.GetComponentInParent<ConveyorItem>() : null;
        if (item != null && candidates.Remove(item))
        {
            queuedItems = candidates.Count;
        }
    }

    private void TrackCandidate(Collider other)
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

        if (candidates.Contains(item))
        {
            queuedItems = candidates.Count;
            return;
        }

        candidates.Add(item);
        queuedItems = candidates.Count;
        controller.ReportDetectedItem(item);
    }

    private void RefreshOverlappingItems()
    {
        Collider sensorCollider = GetComponent<Collider>();
        if (sensorCollider == null)
        {
            CleanupCandidates();
            return;
        }

        Collider[] overlaps = GetOverlaps(sensorCollider);
        for (int i = 0; i < overlaps.Length; i++)
        {
            TrackCandidate(overlaps[i]);
        }

        CleanupCandidates();
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

        CapsuleCollider capsule = sensorCollider as CapsuleCollider;
        if (capsule != null)
        {
            GetCapsulePoints(capsule, out Vector3 point0, out Vector3 point1, out float radius);
            return Physics.OverlapCapsule(point0, point1, radius, detectionMask, QueryTriggerInteraction.Collide);
        }

        Bounds bounds = sensorCollider.bounds;
        return Physics.OverlapBox(bounds.center, bounds.extents, Quaternion.identity, detectionMask, QueryTriggerInteraction.Collide);
    }

    private void CleanupCandidates()
    {
        candidates.RemoveAll(item => item == null || controller == null || !controller.CanAcceptItem(item));
        queuedItems = candidates.Count;
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static float MaxComponent(Vector3 value)
    {
        return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
    }

    private static void GetCapsulePoints(CapsuleCollider capsule, out Vector3 point0, out Vector3 point1, out float radius)
    {
        Vector3 scale = Abs(capsule.transform.lossyScale);
        int direction = capsule.direction;
        float heightScale = direction == 0 ? scale.x : direction == 1 ? scale.y : scale.z;
        float radiusScale = direction == 0 ? Mathf.Max(scale.y, scale.z) : direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
        radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * heightScale, radius * 2f);
        float offset = Mathf.Max(0f, height * 0.5f - radius);

        Vector3 axis = direction == 0 ? Vector3.right : direction == 1 ? Vector3.up : Vector3.forward;
        Vector3 worldCenter = capsule.transform.TransformPoint(capsule.center);
        Vector3 worldAxis = capsule.transform.TransformDirection(axis);
        point0 = worldCenter + worldAxis * offset;
        point1 = worldCenter - worldAxis * offset;
    }
}
