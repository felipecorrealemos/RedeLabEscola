using UnityEngine;

[DisallowMultipleComponent]
public class DeviceDropZone : MonoBehaviour
{
    [SerializeField] private Transform placePoint;
    [SerializeField] private bool acceptsAnyDevice = true;
    [SerializeField] private string acceptedDeviceName = "Device";
    [SerializeField] private Vector2 indicatorSize = new Vector2(1f, 1f);
    [SerializeField] private Color validColor = new Color(0.1f, 0.9f, 0.35f, 0.35f);
    [SerializeField] private Color occupiedColor = new Color(0.25f, 0.5f, 1f, 0.22f);
    [SerializeField] private float indicatorHeight = 0.03f;
    [SerializeField] private float indicatorVerticalOffset = 0f;
    [SerializeField] private float placementVerticalOffset = 0f;
    [SerializeField] private float pulseAmount = 0.12f;
    [SerializeField] private float pulseSpeed = 4f;
    [SerializeField] private float visibleRange = 2.2f;

    public Vector3 PlacePosition => GetSurfacePosition() + Vector3.up * placementVerticalOffset;
    public Vector3 IndicatorPosition => PlacePosition + Vector3.up * (indicatorHeight + indicatorVerticalOffset);
    public Quaternion PlaceRotation => placePoint != null ? placePoint.rotation : transform.rotation;
    public MovableDevice CurrentDevice { get; private set; }

    private Transform indicator;
    private Renderer indicatorRenderer;
    private Material indicatorMaterial;
    private Vector3 baseIndicatorScale;
    private float visibleBlend;

    private void Awake()
    {
        EnsureIndicator();
    }

    private void Update()
    {
        UpdateIndicator();
    }

    public bool CanReceive(MovableDevice device)
    {
        if (device == null || CurrentDevice != null)
        {
            return false;
        }

        return acceptsAnyDevice || device.DeviceName == acceptedDeviceName;
    }

    public void Receive(MovableDevice device)
    {
        CurrentDevice = device;
    }

    public void Release(MovableDevice device)
    {
        if (CurrentDevice == device)
        {
            CurrentDevice = null;
        }
    }

    public Vector3 GetDropPositionFor(MovableDevice device)
    {
        if (device == null)
        {
            return PlacePosition;
        }

        return PlacePosition + Vector3.up * device.GetBottomToPivotOffset();
    }

    private void EnsureIndicator()
    {
        if (indicator != null)
        {
            return;
        }

        Transform existingIndicator = transform.Find("DropIndicator");
        if (existingIndicator != null)
        {
            indicator = existingIndicator;
        }
        else
        {
            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            indicatorObject.name = "DropIndicator";
            indicator = indicatorObject.transform;
            indicator.SetParent(transform);
            Destroy(indicatorObject.GetComponent<Collider>());
        }

        PositionIndicator();
        baseIndicatorScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);
        indicator.localScale = baseIndicatorScale;

        indicatorRenderer = indicator.GetComponent<Renderer>();
        indicatorMaterial = new Material(GetIndicatorShader());
        indicatorMaterial.color = validColor;
        indicatorRenderer.sharedMaterial = indicatorMaterial;
        SetIndicatorAlpha(0f);
    }

    private void UpdateIndicator()
    {
        EnsureIndicator();

        MovableDevice carriedDevice = FindNearbyCarriedDevice();
        bool canReceive = CanReceive(carriedDevice);
        bool shouldShow = carriedDevice != null && (canReceive || CurrentDevice != null);
        float targetBlend = shouldShow ? 1f : 0f;

        visibleBlend = Mathf.MoveTowards(visibleBlend, targetBlend, Time.deltaTime * 6f);
        Color targetColor = canReceive ? validColor : occupiedColor;
        indicatorMaterial.color = new Color(targetColor.r, targetColor.g, targetColor.b, targetColor.a * visibleBlend);

        PositionIndicator();
        float pulse = 1f + (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * pulseAmount * visibleBlend;
        indicator.localScale = baseIndicatorScale * pulse;
        indicator.gameObject.SetActive(visibleBlend > 0.01f);
    }

    private void PositionIndicator()
    {
        indicator.position = IndicatorPosition;
        indicator.rotation = Quaternion.Euler(-90f, 0f, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 placePosition = PlacePosition;
        Vector3 indicatorPosition = IndicatorPosition;

        Gizmos.color = new Color(0.1f, 0.9f, 0.35f, 0.75f);
        Gizmos.DrawWireCube(indicatorPosition, new Vector3(indicatorSize.x, 0.02f, indicatorSize.y));

        Gizmos.color = new Color(1f, 0.75f, 0.1f, 0.9f);
        Gizmos.DrawSphere(placePosition, 0.08f);
        Gizmos.DrawLine(placePosition, indicatorPosition);
    }

    private Vector3 GetSurfacePosition()
    {
        if (placePoint == null)
        {
            return transform.position;
        }

        Transform reference = placePoint;
        if (TryGetReferenceBounds(reference, out Bounds bounds))
        {
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }

        return reference.position;
    }

    private bool TryGetReferenceBounds(Transform reference, out Bounds bounds)
    {
        bounds = new Bounds(reference.position, Vector3.zero);
        bool hasBounds = false;

        Collider[] referenceColliders = reference.GetComponentsInChildren<Collider>();
        foreach (Collider referenceCollider in referenceColliders)
        {
            if (referenceCollider == null || ShouldIgnoreSurfaceReference(referenceCollider.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = referenceCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(referenceCollider.bounds);
            }
        }

        Renderer[] referenceRenderers = reference.GetComponentsInChildren<Renderer>();
        foreach (Renderer referenceRenderer in referenceRenderers)
        {
            if (referenceRenderer == null || ShouldIgnoreSurfaceReference(referenceRenderer.transform))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = referenceRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(referenceRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private bool ShouldIgnoreSurfaceReference(Transform referenceChild)
    {
        if (referenceChild == null)
        {
            return true;
        }

        if (referenceChild == indicator || referenceChild.name == "DropIndicator" || referenceChild.name == "InteractionIndicator")
        {
            return true;
        }

        return referenceChild.GetComponentInParent<MovableDevice>() != null;
    }

    private MovableDevice FindNearbyCarriedDevice()
    {
        MovableDevice[] devices = FindObjectsOfType<MovableDevice>();
        float visibleRangeSqr = visibleRange * visibleRange;

        foreach (MovableDevice device in devices)
        {
            if (device == null || !device.IsCarried)
            {
                continue;
            }

            if (Vector3.SqrMagnitude(device.transform.position - PlacePosition) <= visibleRangeSqr)
            {
                return device;
            }
        }

        return null;
    }

    private void SetIndicatorAlpha(float alpha)
    {
        Color color = indicatorMaterial.color;
        indicatorMaterial.color = new Color(color.r, color.g, color.b, alpha);
        indicator.gameObject.SetActive(alpha > 0.01f);
    }

    private Shader GetIndicatorShader()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }
}
