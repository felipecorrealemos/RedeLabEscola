using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ConveyorPath : MonoBehaviour
{
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private float waypointArrivalDistance = 0.05f;
    [SerializeField] private float itemRotationSpeed = 8f;
    [SerializeField] private bool rotateItemsAlongPath = true;
    [SerializeField] private Color pathColor = new Color(0.1f, 0.85f, 1f, 0.95f);
    [SerializeField] private Color startColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color endColor = new Color(1f, 0.25f, 0.15f, 1f);

    private readonly List<float> segmentLengths = new List<float>();
    private float totalLength;
    private int cachedWaypointCount = -1;

    public IReadOnlyList<Transform> Waypoints => waypoints;
    public float WaypointArrivalDistance => Mathf.Max(0.001f, waypointArrivalDistance);
    public float ItemRotationSpeed => Mathf.Max(0.01f, itemRotationSpeed);
    public bool RotateItemsAlongPath => rotateItemsAlongPath;
    public float TotalLength
    {
        get
        {
            RebuildIfNeeded();
            return totalLength;
        }
    }

    public void ConfigureWaypoints(List<Transform> nextWaypoints)
    {
        waypoints = nextWaypoints ?? new List<Transform>();
        RebuildSegments();
    }

    private void Awake()
    {
        RebuildSegments();
    }

    private void OnValidate()
    {
        waypointArrivalDistance = Mathf.Max(0.001f, waypointArrivalDistance);
        itemRotationSpeed = Mathf.Max(0.01f, itemRotationSpeed);
        RebuildSegments();
    }

    public bool IsValid()
    {
        return waypoints != null && waypoints.Count >= 2 && waypoints[0] != null && waypoints[waypoints.Count - 1] != null;
    }

    public Vector3 GetStartPosition()
    {
        return IsValid() ? waypoints[0].position : transform.position;
    }

    public Vector3 GetEndPosition()
    {
        return IsValid() ? waypoints[waypoints.Count - 1].position : transform.position;
    }

    public Vector3 GetEndDirection()
    {
        if (!IsValid())
        {
            return transform.forward;
        }

        return GetDirectionForWaypoint(waypoints.Count - 1);
    }

    public ConveyorPathSample GetSample(float distance)
    {
        RebuildIfNeeded();

        if (!IsValid())
        {
            return new ConveyorPathSample(transform.position, transform.forward, transform.right, 0f, 0);
        }

        float clampedDistance = Mathf.Clamp(distance, 0f, Mathf.Max(totalLength, 0f));
        float distanceAtSegmentStart = 0f;

        for (int i = 0; i < segmentLengths.Count; i++)
        {
            float segmentLength = segmentLengths[i];
            if (segmentLength <= 0.0001f)
            {
                continue;
            }

            bool isLastSegment = i == segmentLengths.Count - 1;
            if (clampedDistance <= distanceAtSegmentStart + segmentLength || isLastSegment)
            {
                Transform from = waypoints[i];
                Transform to = waypoints[i + 1];
                float t = Mathf.Clamp01((clampedDistance - distanceAtSegmentStart) / segmentLength);
                Vector3 direction = (to.position - from.position).normalized;
                Vector3 lateral = GetLateral(direction);
                return new ConveyorPathSample(Vector3.Lerp(from.position, to.position, t), direction, lateral, clampedDistance, i);
            }

            distanceAtSegmentStart += segmentLength;
        }

        Vector3 fallbackDirection = GetDirectionForWaypoint(waypoints.Count - 1);
        return new ConveyorPathSample(GetEndPosition(), fallbackDirection, GetLateral(fallbackDirection), clampedDistance, waypoints.Count - 2);
    }

    public float GetClosestDistance(Vector3 worldPosition)
    {
        RebuildIfNeeded();

        if (!IsValid())
        {
            return 0f;
        }

        float bestDistance = 0f;
        float bestSqrMagnitude = float.PositiveInfinity;
        float distanceAtSegmentStart = 0f;

        for (int i = 0; i < segmentLengths.Count; i++)
        {
            Transform from = waypoints[i];
            Transform to = waypoints[i + 1];
            Vector3 segment = to.position - from.position;
            float segmentSqrMagnitude = segment.sqrMagnitude;
            if (segmentSqrMagnitude <= 0.0001f)
            {
                continue;
            }

            float t = Mathf.Clamp01(Vector3.Dot(worldPosition - from.position, segment) / segmentSqrMagnitude);
            Vector3 closest = from.position + segment * t;
            float sqrMagnitude = (worldPosition - closest).sqrMagnitude;
            if (sqrMagnitude < bestSqrMagnitude)
            {
                bestSqrMagnitude = sqrMagnitude;
                bestDistance = distanceAtSegmentStart + segmentLengths[i] * t;
            }

            distanceAtSegmentStart += segmentLengths[i];
        }

        return bestDistance;
    }

    private void RebuildIfNeeded()
    {
        if (waypoints == null || cachedWaypointCount != waypoints.Count || segmentLengths.Count != Mathf.Max(0, waypoints.Count - 1))
        {
            RebuildSegments();
        }
    }

    private void RebuildSegments()
    {
        segmentLengths.Clear();
        totalLength = 0f;
        cachedWaypointCount = waypoints != null ? waypoints.Count : 0;

        if (waypoints == null || waypoints.Count < 2)
        {
            return;
        }

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            float length = waypoints[i] != null && waypoints[i + 1] != null
                ? Vector3.Distance(waypoints[i].position, waypoints[i + 1].position)
                : 0f;
            segmentLengths.Add(length);
            totalLength += length;
        }
    }

    private Vector3 GetDirectionForWaypoint(int waypointIndex)
    {
        if (!IsValid())
        {
            return transform.forward;
        }

        int fromIndex = Mathf.Clamp(waypointIndex - 1, 0, waypoints.Count - 2);
        int toIndex = Mathf.Clamp(fromIndex + 1, 1, waypoints.Count - 1);
        Vector3 direction = waypoints[toIndex].position - waypoints[fromIndex].position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : transform.forward;
    }

    private Vector3 GetLateral(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        Vector3 lateral = Vector3.Cross(Vector3.up, direction.normalized);
        return lateral.sqrMagnitude > 0.0001f ? lateral.normalized : transform.right;
    }

    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            return;
        }

        for (int i = 0; i < waypoints.Count; i++)
        {
            Transform waypoint = waypoints[i];
            if (waypoint == null)
            {
                continue;
            }

            Gizmos.color = i == 0 ? startColor : (i == waypoints.Count - 1 ? endColor : pathColor);
            Gizmos.DrawSphere(waypoint.position, i == 0 || i == waypoints.Count - 1 ? 0.12f : 0.08f);

            if (i < waypoints.Count - 1 && waypoints[i + 1] != null)
            {
                Vector3 from = waypoint.position;
                Vector3 to = waypoints[i + 1].position;
                Vector3 direction = (to - from).normalized;

                Gizmos.color = pathColor;
                Gizmos.DrawLine(from, to);
                DrawArrow(Vector3.Lerp(from, to, 0.65f), direction);
            }
        }
    }

    private void DrawArrow(Vector3 position, Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, 150f, 0f) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -150f, 0f) * Vector3.forward;
        Gizmos.DrawLine(position, position + right * 0.25f);
        Gizmos.DrawLine(position, position + left * 0.25f);
    }
}

public readonly struct ConveyorPathSample
{
    public readonly Vector3 Position;
    public readonly Vector3 Direction;
    public readonly Vector3 Lateral;
    public readonly float Distance;
    public readonly int SegmentIndex;

    public ConveyorPathSample(Vector3 position, Vector3 direction, Vector3 lateral, float distance, int segmentIndex)
    {
        Position = position;
        Direction = direction;
        Lateral = lateral;
        Distance = distance;
        SegmentIndex = segmentIndex;
    }
}
