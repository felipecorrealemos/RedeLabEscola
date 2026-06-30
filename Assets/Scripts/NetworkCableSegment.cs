using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkCableSegment : MonoBehaviour
{
    private static readonly List<NetworkCableSegment> Segments = new List<NetworkCableSegment>();

    [SerializeField] private float contactTolerance = 0.12f;

    public float ContactTolerance => Mathf.Max(contactTolerance, 0.01f);

    private void OnEnable()
    {
        if (!Segments.Contains(this))
        {
            Segments.Add(this);
        }
    }

    private void OnDisable()
    {
        Segments.Remove(this);
    }

    public static void EnsureSceneCableSegments()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate == null || !IsCableTransform(candidate))
            {
                continue;
            }

            if (candidate.GetComponent<NetworkCableSegment>() == null)
            {
                candidate.gameObject.AddComponent<NetworkCableSegment>();
            }
        }
    }

    public static IReadOnlyList<NetworkCableSegment> AllSegments
    {
        get
        {
            PruneMissingSegments();
            return Segments;
        }
    }

    public bool IsConnectedTo(NetworkCableSegment other)
    {
        if (other == null || other == this)
        {
            return false;
        }

        float tolerance = Mathf.Max(ContactTolerance, other.ContactTolerance);
        return AreTransformsTouching(transform, other.transform, tolerance);
    }

    public bool IsConnectedToJack(NetworkJackConnectionPoint jack)
    {
        if (jack == null)
        {
            return false;
        }

        return AreTransformsTouching(transform, jack.transform, ContactTolerance);
    }

    public RouterInteractable FindConnectedRouter()
    {
        RouterInteractable[] routers = FindObjectsOfType<RouterInteractable>(true);
        RouterInteractable nearestRouter = null;
        float nearestDistance = float.MaxValue;

        foreach (RouterInteractable router in routers)
        {
            if (router == null)
            {
                continue;
            }

            float sqrDistance = GetSqrDistanceBetweenTransforms(transform, router.transform);
            if (sqrDistance <= ContactTolerance * ContactTolerance && sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestRouter = router;
            }
        }

        return nearestRouter;
    }

    public void AppendConnectedSegments(List<NetworkCableSegment> results)
    {
        if (results == null)
        {
            return;
        }

        IReadOnlyList<NetworkCableSegment> segments = AllSegments;
        foreach (NetworkCableSegment segment in segments)
        {
            if (segment != null && segment != this && IsConnectedTo(segment))
            {
                results.Add(segment);
            }
        }
    }

    private static void PruneMissingSegments()
    {
        for (int i = Segments.Count - 1; i >= 0; i--)
        {
            if (Segments[i] == null)
            {
                Segments.RemoveAt(i);
            }
        }
    }

    private static bool IsCableTransform(Transform candidate)
    {
        string lowerName = candidate.name.ToLowerInvariant();
        return lowerName == "cabo" || lowerName.StartsWith("cabo ");
    }

    private static bool AreTransformsTouching(Transform first, Transform second, float tolerance)
    {
        return GetSqrDistanceBetweenTransforms(first, second) <= tolerance * tolerance;
    }

    private static float GetSqrDistanceBetweenTransforms(Transform first, Transform second)
    {
        if (!TryGetPhysicalBounds(first, out Bounds firstBounds) || !TryGetPhysicalBounds(second, out Bounds secondBounds))
        {
            return Vector3.SqrMagnitude(first.position - second.position);
        }

        Vector3 firstPoint = firstBounds.ClosestPoint(secondBounds.center);
        Vector3 secondPoint = secondBounds.ClosestPoint(firstBounds.center);
        float firstDistance = secondBounds.SqrDistance(firstPoint);
        float secondDistance = firstBounds.SqrDistance(secondPoint);
        return Mathf.Min(firstDistance, secondDistance);
    }

    private static bool TryGetPhysicalBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders)
        {
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        if (hasBounds)
        {
            return true;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }
}
