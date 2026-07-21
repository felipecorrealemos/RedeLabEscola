using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class ScrapGrabDetectionZone : MonoBehaviour
{
    [SerializeField] private LayerMask scrapLayers = ~0;
    [SerializeField] private bool requireScrapItemComponent = true;
    [SerializeField] private Color gizmoColor = new Color(1f, 0.65f, 0.05f, 0.35f);

    private readonly HashSet<ScrapItem> candidates = new HashSet<ScrapItem>();
    private Collider zoneCollider;
    private Rigidbody zoneBody;

    public Collider ZoneCollider => zoneCollider;

    private void Awake()
    {
        ConfigureCollider();
        ConfigureRigidbody();
    }

    private void Reset()
    {
        ConfigureCollider();
        ConfigureRigidbody();
    }

    private void OnValidate()
    {
        ConfigureCollider();
    }

    public ScrapItem GetClosestValidScrap(Vector3 center)
    {
        ScrapItem closest = null;
        float closestDistance = float.PositiveInfinity;
        candidates.RemoveWhere(item => item == null || !item.CanBeGrabbed || !IsInsideLayerMask(item.gameObject.layer));

        foreach (ScrapItem item in candidates)
        {
            float distance = Vector3.SqrMagnitude(item.GrabRoot.position - center);
            if (distance < closestDistance)
            {
                closest = item;
                closestDistance = distance;
            }
        }

        return closest;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryAdd(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryAdd(other);
    }

    private void OnTriggerExit(Collider other)
    {
        ScrapItem item = other.GetComponentInParent<ScrapItem>();
        if (item != null)
        {
            candidates.Remove(item);
        }
    }

    private void TryAdd(Collider other)
    {
        if (other == null || !IsInsideLayerMask(other.gameObject.layer))
        {
            return;
        }

        ScrapItem item = other.GetComponentInParent<ScrapItem>();
        if (item != null && item.CanBeGrabbed)
        {
            candidates.Add(item);
        }
        else if (!requireScrapItemComponent)
        {
            item = other.gameObject.GetComponent<ScrapItem>();
            if (item != null)
            {
                candidates.Add(item);
            }
        }
    }

    private bool IsInsideLayerMask(int layer)
    {
        return (scrapLayers.value & (1 << layer)) != 0;
    }

    private void ConfigureCollider()
    {
        if (zoneCollider == null)
        {
            zoneCollider = GetComponent<Collider>();
        }

        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void ConfigureRigidbody()
    {
        if (zoneBody == null)
        {
            zoneBody = GetComponent<Rigidbody>();
        }

        if (zoneBody != null)
        {
            zoneBody.isKinematic = true;
            zoneBody.useGravity = false;
            zoneBody.detectCollisions = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        ConfigureCollider();
        if (zoneCollider == null)
        {
            return;
        }

        Gizmos.color = gizmoColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (zoneCollider is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(box.center, box.size);
        }
        else if (zoneCollider is SphereCollider sphere)
        {
            Gizmos.DrawSphere(sphere.center, sphere.radius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(sphere.center, sphere.radius);
        }
    }
}
