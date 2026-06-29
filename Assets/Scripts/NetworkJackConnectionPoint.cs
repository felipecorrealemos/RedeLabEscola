using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class NetworkJackConnectionPoint : MonoBehaviour
{
    [SerializeField] private Vector3 triggerSize = new Vector3(3.5f, 2f, 3.5f);
    [SerializeField] private Vector3 triggerCenter = Vector3.zero;
    [SerializeField] private Vector3 minimumWorldTriggerSize = new Vector3(1.8f, 1.2f, 1.8f);
    [SerializeField] private float refreshInterval = 0.15f;

    private BoxCollider triggerCollider;
    private ComputerInteractable connectedComputer;
    private float nextRefreshTime;

    public ComputerInteractable ConnectedComputer => connectedComputer;
    public bool HasConnectedComputer => connectedComputer != null;

    private void Awake()
    {
        EnsureTrigger();
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

        nextRefreshTime = Time.time + refreshInterval;
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

            MovableDevice device = hit.GetComponentInParent<MovableDevice>();
            if (device == null || !device.IsPlaced || !device.IsComputerDevice())
            {
                continue;
            }

            ComputerInteractable computer = device.EnsureComputerInteractable();
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
