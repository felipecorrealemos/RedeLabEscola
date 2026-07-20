using UnityEngine;

[DisallowMultipleComponent]
public class ScrapCraneBounds : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform movementBoundsRoot;
    [SerializeField] private BoxCollider[] boundaryColliders = new BoxCollider[4];
    [SerializeField] private BoxCollider movingAxisCollider;

    [Header("Bounds")]
    [SerializeField] private bool useAutomaticBounds = true;
    [SerializeField, Min(0f)] private float movementSafetyMargin = 0.05f;
    [SerializeField] private Vector3 manualMinimumLocalPosition = new Vector3(-12f, 0f, -8f);
    [SerializeField] private Vector3 manualMaximumLocalPosition = new Vector3(9f, 0f, 21f);
    [SerializeField] private Vector3 calculatedMinimumPosition;
    [SerializeField] private Vector3 calculatedMaximumPosition;
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool showManualPreviewWhenAutomatic = true;

    public Vector3 ActiveMinimumPosition => useAutomaticBounds ? calculatedMinimumPosition : manualMinimumLocalPosition;
    public Vector3 ActiveMaximumPosition => useAutomaticBounds ? calculatedMaximumPosition : manualMaximumLocalPosition;
    public Vector3 CalculatedMinimumPosition => calculatedMinimumPosition;
    public Vector3 CalculatedMaximumPosition => calculatedMaximumPosition;
    public bool HasValidBounds => IsValidRange(ActiveMinimumPosition, ActiveMaximumPosition);

    public void AssignReferences(Transform root, BoxCollider[] boundaries, BoxCollider axisCollider)
    {
        movementBoundsRoot = root;
        boundaryColliders = boundaries;
        movingAxisCollider = axisCollider;
        RecalculateBounds();
    }

    private void Reset()
    {
        movementBoundsRoot = transform;
        RecalculateBounds();
    }

    private void OnValidate()
    {
        movementSafetyMargin = Mathf.Max(0f, movementSafetyMargin);
        NormalizeManualBounds();
        RecalculateBounds();
    }

    private void Awake()
    {
        if (movementBoundsRoot == null)
        {
            movementBoundsRoot = transform;
        }

        RecalculateBounds();
    }

    [ContextMenu("Recalculate Bounds")]
    public void RecalculateBounds()
    {
        if (movementBoundsRoot == null || boundaryColliders == null || boundaryColliders.Length == 0)
        {
            calculatedMinimumPosition = Vector3.zero;
            calculatedMaximumPosition = Vector3.zero;
            return;
        }

        bool hasBounds = false;
        Bounds combined = new Bounds();
        for (int i = 0; i < boundaryColliders.Length; i++)
        {
            BoxCollider box = boundaryColliders[i];
            if (box == null)
            {
                continue;
            }

            Bounds localBounds = GetColliderBoundsInRootSpace(box, movementBoundsRoot);
            if (!hasBounds)
            {
                combined = localBounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(localBounds);
            }
        }

        if (!hasBounds)
        {
            calculatedMinimumPosition = Vector3.zero;
            calculatedMaximumPosition = Vector3.zero;
            return;
        }

        Vector3 movingExtents = movingAxisCollider != null ? GetColliderBoundsInRootSpace(movingAxisCollider, movementBoundsRoot).extents : Vector3.zero;
        calculatedMinimumPosition = new Vector3(
            combined.min.x + movingExtents.x + movementSafetyMargin,
            0f,
            combined.min.z + movingExtents.z + movementSafetyMargin);
        calculatedMaximumPosition = new Vector3(
            combined.max.x - movingExtents.x - movementSafetyMargin,
            0f,
            combined.max.z - movingExtents.z - movementSafetyMargin);

        if (!IsValidRange(calculatedMinimumPosition, calculatedMaximumPosition))
        {
            calculatedMinimumPosition = Vector3.zero;
            calculatedMaximumPosition = Vector3.zero;
        }
    }

    public Vector3 ClampLocalPosition(Vector3 localPosition)
    {
        if (!HasValidBounds)
        {
            RecalculateBounds();
        }

        Vector3 minimum = ActiveMinimumPosition;
        Vector3 maximum = ActiveMaximumPosition;
        localPosition.x = Mathf.Clamp(localPosition.x, minimum.x, maximum.x);
        localPosition.z = Mathf.Clamp(localPosition.z, minimum.z, maximum.z);
        return localPosition;
    }

    [ContextMenu("Use Current Calculated Bounds As Manual")]
    public void CopyCalculatedBoundsToManual()
    {
        RecalculateBounds();
        if (!IsValidRange(calculatedMinimumPosition, calculatedMaximumPosition))
        {
            return;
        }

        manualMinimumLocalPosition = calculatedMinimumPosition;
        manualMaximumLocalPosition = calculatedMaximumPosition;
        NormalizeManualBounds();
    }

    private void NormalizeManualBounds()
    {
        Vector3 minimum = manualMinimumLocalPosition;
        Vector3 maximum = manualMaximumLocalPosition;
        manualMinimumLocalPosition = new Vector3(
            Mathf.Min(minimum.x, maximum.x),
            minimum.y,
            Mathf.Min(minimum.z, maximum.z));
        manualMaximumLocalPosition = new Vector3(
            Mathf.Max(minimum.x, maximum.x),
            maximum.y,
            Mathf.Max(minimum.z, maximum.z));
    }

    private static bool IsValidRange(Vector3 minimum, Vector3 maximum)
    {
        return minimum.x <= maximum.x && minimum.z <= maximum.z;
    }

    private static Bounds GetColliderBoundsInRootSpace(BoxCollider box, Transform root)
    {
        Vector3 center = box.center;
        Vector3 extents = box.size * 0.5f;
        Bounds bounds = new Bounds(root.InverseTransformPoint(box.transform.TransformPoint(center)), Vector3.zero);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    bounds.Encapsulate(root.InverseTransformPoint(box.transform.TransformPoint(corner)));
                }
            }
        }

        return bounds;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || movementBoundsRoot == null)
        {
            return;
        }

        if (useAutomaticBounds)
        {
            RecalculateBounds();
        }

        Gizmos.matrix = movementBoundsRoot.localToWorldMatrix;
        DrawBoundsGizmo(
            ActiveMinimumPosition,
            ActiveMaximumPosition,
            useAutomaticBounds ? new Color(0f, 0.85f, 1f, 0.22f) : new Color(1f, 0.75f, 0.05f, 0.22f),
            useAutomaticBounds ? Color.cyan : new Color(1f, 0.75f, 0.05f, 1f),
            0.05f);

        if (useAutomaticBounds && showManualPreviewWhenAutomatic && IsValidRange(manualMinimumLocalPosition, manualMaximumLocalPosition))
        {
            DrawBoundsGizmo(manualMinimumLocalPosition, manualMaximumLocalPosition, new Color(1f, 0.75f, 0.05f, 0.08f), new Color(1f, 0.75f, 0.05f, 0.7f), 0.12f);
        }
    }

    private static void DrawBoundsGizmo(Vector3 minimum, Vector3 maximum, Color fillColor, Color wireColor, float height)
    {
        if (!IsValidRange(minimum, maximum))
        {
            return;
        }

        Vector3 center = (minimum + maximum) * 0.5f;
        Vector3 size = new Vector3(
            Mathf.Abs(maximum.x - minimum.x),
            height,
            Mathf.Abs(maximum.z - minimum.z));

        Gizmos.color = fillColor;
        Gizmos.DrawCube(center, size);
        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(center, size);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(minimum, 0.25f);
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(maximum, 0.25f);
    }
}
