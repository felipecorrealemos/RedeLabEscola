using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class MissionTrigger : MonoBehaviour
{
    [SerializeField] private int missionNumber = 2;
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered;
    private BoxCollider triggerCollider;

    private void Awake()
    {
        EnsureTriggerCollider();
    }

    private void Reset()
    {
        EnsureTriggerCollider();
    }

    private void OnValidate()
    {
        EnsureTriggerCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce)
        {
            return;
        }

        if (!IsPlayer(other))
        {
            return;
        }

        MissionManager manager = MissionManager.Instance;
        if (manager == null)
        {
            manager = FindObjectOfType<MissionManager>();
        }

        manager?.SetMission(missionNumber);
        hasTriggered = true;
    }

    private bool IsPlayer(Collider other)
    {
        return other != null && (other.CompareTag("Player") || other.GetComponentInParent<PlayerTopDownController>() != null);
    }

    private void EnsureTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
}
