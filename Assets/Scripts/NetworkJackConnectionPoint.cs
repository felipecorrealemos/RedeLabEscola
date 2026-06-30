using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class NetworkJackConnectionPoint : MonoBehaviour
{
    [SerializeField] private NetworkScope networkScope;
    [SerializeField] private RouterInteractable sourceRouter;
    [SerializeField] private Vector3 triggerSize = new Vector3(3.5f, 2f, 3.5f);
    [SerializeField] private Vector3 triggerCenter = Vector3.zero;
    [SerializeField] private Vector3 minimumWorldTriggerSize = new Vector3(1.8f, 1.2f, 1.8f);
    [SerializeField] private float refreshInterval = 1f;
    [SerializeField] private float routerContactTolerance = 0.25f;
    [SerializeField] private int maxCablePropagationDepth = 16;

    private BoxCollider triggerCollider;
    private ComputerInteractable connectedComputer;
    private float nextRefreshTime;

    public ComputerInteractable ConnectedComputer => connectedComputer;
    public bool HasConnectedComputer => connectedComputer != null;
    public NetworkScope NetworkScope => ResolveNetworkScope();
    public RouterInteractable SourceRouter => sourceRouter;

    private void Awake()
    {
        EnsureTrigger();
        NetworkCableSegment.EnsureSceneCableSegments();
    }

    private void Reset()
    {
        EnsureTrigger();
    }

    private void OnValidate()
    {
        EnsureTrigger();
    }

    private void Update()
    {
        if (Time.time < nextRefreshTime)
        {
            return;
        }

        nextRefreshTime = Time.time + Mathf.Max(refreshInterval, 1f);
        CacheResolvedNetworkScope();
        RefreshConnectedComputer();
    }

    public bool IsConnected(ComputerInteractable computer)
    {
        return computer != null && connectedComputer == computer;
    }

    private void RefreshConnectedComputer()
    {
        EnsureTrigger();

        ComputerInteractable bestComputer = null;
        float bestDistance = float.MaxValue;
        Vector3 center = GetWorldTriggerCenter();
        Vector3 halfExtents = GetWorldTriggerHalfExtents();
        Collider[] hits = Physics.OverlapBox(center, halfExtents, transform.rotation, ~0, QueryTriggerInteraction.Collide);

        foreach (Collider hit in hits)
        {
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            ComputerInteractable computer = FindComputerForHit(hit);
            if (computer == null)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(computer.transform.position - transform.position);
            if (sqrDistance < bestDistance)
            {
                bestDistance = sqrDistance;
                bestComputer = computer;
            }
        }

        if (connectedComputer == bestComputer)
        {
            return;
        }

        if (connectedComputer != null)
        {
            connectedComputer.SetNetworkJack(null);
        }

        connectedComputer = bestComputer;

        if (connectedComputer != null)
        {
            connectedComputer.SetNetworkJack(this);
        }
    }

    private ComputerInteractable FindComputerForHit(Collider hit)
    {
        MovableDevice device = hit.GetComponentInParent<MovableDevice>();
        if (device != null && device.IsPlaced && device.IsComputerDevice())
        {
            return device.EnsureComputerInteractable();
        }

        ComputerInteractable stationaryComputer = hit.GetComponentInParent<ComputerInteractable>();
        if (stationaryComputer != null && stationaryComputer.IsStationaryNetworkDevice)
        {
            return stationaryComputer;
        }

        return null;
    }

    private NetworkScope ResolveNetworkScope()
    {
        if (networkScope != null)
        {
            return networkScope;
        }

        RouterInteractable physicalRouter = FindPhysicallyConnectedRouter();
        if (physicalRouter != null)
        {
            sourceRouter = physicalRouter;
            return physicalRouter.ActiveNetworkScope;
        }

        return FindCableConnectedScope();
    }

    private void CacheResolvedNetworkScope()
    {
        NetworkScope resolvedScope = ResolveNetworkScope();
        if (resolvedScope == null)
        {
            sourceRouter = null;
            return;
        }

        networkScope = resolvedScope;
        if (sourceRouter == null)
        {
            sourceRouter = resolvedScope.OwnerRouter;
        }
    }

    private NetworkScope FindCableConnectedScope()
    {
        NetworkCableSegment.EnsureSceneCableSegments();
        List<NetworkCableSegment> connectedSegments = FindConnectedCableSegments();
        foreach (NetworkCableSegment segment in connectedSegments)
        {
            NetworkScope scope = FindScopeThroughCableGraph(segment);
            if (scope != null)
            {
                return scope;
            }
        }

        return null;
    }

    private NetworkScope FindScopeThroughCableGraph(NetworkCableSegment startCable)
    {
        Queue<NetworkCableSegment> cablesToVisit = new Queue<NetworkCableSegment>();
        HashSet<NetworkCableSegment> visitedCables = new HashSet<NetworkCableSegment>();
        HashSet<NetworkJackConnectionPoint> visitedJacks = new HashSet<NetworkJackConnectionPoint>();

        cablesToVisit.Enqueue(startCable);
        int depth = 0;

        while (cablesToVisit.Count > 0 && depth < Mathf.Max(maxCablePropagationDepth, 1))
        {
            depth++;
            NetworkCableSegment cable = cablesToVisit.Dequeue();
            if (cable == null || visitedCables.Contains(cable))
            {
                continue;
            }

            visitedCables.Add(cable);

            RouterInteractable router = cable.FindConnectedRouter();
            if (router != null)
            {
                sourceRouter = router;
                return router.ActiveNetworkScope;
            }

            NetworkJackConnectionPoint[] jacks = FindObjectsOfType<NetworkJackConnectionPoint>(true);
            foreach (NetworkJackConnectionPoint jack in jacks)
            {
                if (jack == null || visitedJacks.Contains(jack) || !cable.IsConnectedToJack(jack))
                {
                    continue;
                }

                visitedJacks.Add(jack);

                RouterInteractable jackRouter = jack.FindPhysicallyConnectedRouter();
                if (jackRouter != null)
                {
                    sourceRouter = jackRouter;
                    return jackRouter.ActiveNetworkScope;
                }

                if (jack.sourceRouter != null)
                {
                    sourceRouter = jack.sourceRouter;
                    return jack.sourceRouter.ActiveNetworkScope;
                }

                if (jack.networkScope != null)
                {
                    sourceRouter = jack.networkScope.OwnerRouter;
                    return jack.networkScope;
                }
            }

            List<NetworkCableSegment> nextSegments = new List<NetworkCableSegment>();
            cable.AppendConnectedSegments(nextSegments);
            foreach (NetworkCableSegment nextSegment in nextSegments)
            {
                if (nextSegment != null && !visitedCables.Contains(nextSegment))
                {
                    cablesToVisit.Enqueue(nextSegment);
                }
            }
        }

        return null;
    }

    private List<NetworkCableSegment> FindConnectedCableSegments()
    {
        List<NetworkCableSegment> connectedSegments = new List<NetworkCableSegment>();
        IReadOnlyList<NetworkCableSegment> segments = NetworkCableSegment.AllSegments;

        foreach (NetworkCableSegment segment in segments)
        {
            if (segment != null && segment.IsConnectedToJack(this))
            {
                connectedSegments.Add(segment);
            }
        }

        return connectedSegments;
    }

    private RouterInteractable FindPhysicallyConnectedRouter()
    {
        RouterInteractable[] routers = FindObjectsOfType<RouterInteractable>(true);
        RouterInteractable nearestRouter = null;
        float nearestDistance = float.MaxValue;
        float tolerance = Mathf.Max(routerContactTolerance, 0.01f);

        foreach (RouterInteractable router in routers)
        {
            if (router == null)
            {
                continue;
            }

            float sqrDistance = GetSqrDistanceBetweenPhysicalBounds(transform, router.transform);
            if (sqrDistance <= tolerance * tolerance && sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestRouter = router;
            }
        }

        return nearestRouter;
    }

    private float GetSqrDistanceBetweenPhysicalBounds(Transform first, Transform second)
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

    private bool TryGetPhysicalBounds(Transform root, out Bounds bounds)
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

    private bool AreBoundsClose(Transform first, Transform second, float maxDistance)
    {
        if (!TryGetBounds(first, out Bounds firstBounds) || !TryGetBounds(second, out Bounds secondBounds))
        {
            return Vector3.SqrMagnitude(first.position - second.position) <= maxDistance * maxDistance;
        }

        return firstBounds.SqrDistance(secondBounds.ClosestPoint(firstBounds.center)) <= maxDistance * maxDistance
            || secondBounds.SqrDistance(firstBounds.ClosestPoint(secondBounds.center)) <= maxDistance * maxDistance;
    }

    private float GetSqrDistanceToTransformBounds(Vector3 point, Transform target)
    {
        if (TryGetBounds(target, out Bounds bounds))
        {
            return bounds.SqrDistance(point);
        }

        return Vector3.SqrMagnitude(point - target.position);
    }

    private bool TryGetBounds(Transform root, out Bounds bounds)
    {
        bounds = new Bounds(root.position, Vector3.zero);
        bool hasBounds = false;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider candidate in colliders)
        {
            if (candidate == null || candidate == triggerCollider)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(candidate.bounds);
            }
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer candidate in renderers)
        {
            if (candidate == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = candidate.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(candidate.bounds);
            }
        }

        return hasBounds;
    }

    private void EnsureTrigger()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider>();
        }

        if (triggerCollider == null)
        {
            return;
        }

        triggerCollider.isTrigger = true;
        triggerCollider.center = triggerCenter;
        triggerCollider.size = triggerSize;
    }

    private Vector3 GetWorldTriggerCenter()
    {
        return triggerCollider != null ? transform.TransformPoint(triggerCollider.center) : transform.position;
    }

    private Vector3 GetWorldTriggerHalfExtents()
    {
        Vector3 lossyScale = transform.lossyScale;
        Vector3 scaledSize = new Vector3(
            Mathf.Abs(triggerSize.x * lossyScale.x),
            Mathf.Abs(triggerSize.y * lossyScale.y),
            Mathf.Abs(triggerSize.z * lossyScale.z));

        return Vector3.Max(scaledSize, minimumWorldTriggerSize) * 0.5f;
    }

    private void OnDrawGizmosSelected()
    {
        EnsureTrigger();

        Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.28f);
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(GetWorldTriggerCenter(), transform.rotation, Vector3.one);
        Gizmos.DrawCube(Vector3.zero, GetWorldTriggerHalfExtents() * 2f);

        Gizmos.color = new Color(0.1f, 0.65f, 1f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, GetWorldTriggerHalfExtents() * 2f);
        Gizmos.matrix = previousMatrix;
    }
}
