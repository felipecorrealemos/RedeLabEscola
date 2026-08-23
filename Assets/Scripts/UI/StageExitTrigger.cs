using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class StageExitTrigger : MonoBehaviour
{
    [SerializeField] private StageTransitionUI transitionUI;
    [SerializeField] private bool triggerOnce = true;
    [Header("Debug")]
    [Tooltip("Permite encerrar a fase apenas tocando o trigger, mesmo com tarefas pendentes.")]
    [SerializeField] private bool allowIncompleteMissionForDebug;

    private bool triggered;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnValidate()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box != null) box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if ((triggerOnce && triggered) || !IsPlayer(other)) return;
        if (transitionUI == null) transitionUI = FindObjectOfType<StageTransitionUI>(true);
        if (transitionUI != null && transitionUI.TryCompleteStage(allowIncompleteMissionForDebug)) triggered = true;
    }

    private static bool IsPlayer(Collider other)
    {
        return other != null && (other.CompareTag("Player") || other.GetComponentInParent<PlayerTopDownController>() != null);
    }

    private void OnDrawGizmos()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.35f);
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previous;
    }
}
