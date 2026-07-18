using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class Stage2MachineDebugGizmo : MonoBehaviour
{
    [SerializeField] private string label = "Stage2 Machine";
    [SerializeField] private bool useAutoPosition = true;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.4f, 0f);
    [SerializeField] private Color gizmoColor = new Color(0.15f, 0.85f, 1f, 0.9f);
    [SerializeField, Min(0.05f)] private float radius = 0.28f;

    public bool UseAutoPosition => useAutoPosition;

    public void Configure(string nextLabel, Vector3 nextLocalOffset, Color nextColor, float nextRadius)
    {
        if (!string.IsNullOrWhiteSpace(nextLabel))
        {
            label = nextLabel;
        }

        if (useAutoPosition)
        {
            localOffset = nextLocalOffset;
        }

        gizmoColor = nextColor;
        radius = Mathf.Max(0.05f, nextRadius);
    }

    private void OnDrawGizmos()
    {
        DrawGizmo();
    }

    private void DrawGizmo()
    {
        Vector3 position = transform.TransformPoint(localOffset);
        Color previousColor = Gizmos.color;
        Gizmos.color = gizmoColor;
        Gizmos.DrawWireSphere(position, radius);
        Gizmos.DrawLine(transform.position, position);
        Gizmos.color = previousColor;

#if UNITY_EDITOR
        Handles.color = gizmoColor;
        Handles.Label(position + Vector3.up * (radius + 0.1f), label);
#endif
    }

#if UNITY_EDITOR
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
    private static void DrawPickableGizmo(Stage2MachineDebugGizmo target, GizmoType gizmoType)
    {
        if (target != null)
        {
            target.DrawGizmo();
        }
    }
#endif
}
