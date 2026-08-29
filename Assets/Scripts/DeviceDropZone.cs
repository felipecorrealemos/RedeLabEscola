using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DeviceDropZone : MonoBehaviour
{
    [SerializeField] private Transform placePoint;
    [SerializeField] private NetworkScope networkScope;
    [SerializeField] private int missionNumber;
    [SerializeField] private string placementTaskId;
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
    [SerializeField] private float occupancyHeight = 1.6f;
    [SerializeField] private string placePromptFormat = "Aperte E para colocar {0}";
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;
    private InteractionPromptPresenter promptPresenter;

    public Vector3 PlacePosition => GetSurfacePosition() + Vector3.up * placementVerticalOffset;
    public Vector3 IndicatorPosition => PlacePosition + Vector3.up * (indicatorHeight + indicatorVerticalOffset);
    public Quaternion PlaceRotation => placePoint != null ? placePoint.rotation : transform.rotation;
    public NetworkScope NetworkScope => networkScope != null ? networkScope : GetComponentInParent<NetworkScope>();
    public int MissionNumber => missionNumber > 0 ? missionNumber : InferMissionNumber();
    public string PlacementTaskId => !string.IsNullOrWhiteSpace(placementTaskId) ? placementTaskId : InferPlacementTaskId();
    public bool IsComputerPlacementZone => LooksLikeComputerPlacementZoneForMission(MissionNumber);
    public MovableDevice CurrentDevice { get; private set; }

    private Transform indicator;
    private Renderer indicatorRenderer;
    private Material indicatorMaterial;
    private Vector3 baseIndicatorScale;
    private float visibleBlend;
    private readonly Collider[] occupancyHits = new Collider[32];

    private void Awake()
    {
        EnsureIndicator();
        EnsurePrompt();
        SetPromptVisible(false, null);
    }

    private void Update()
    {
        UpdateIndicator();
    }

    public bool CanReceive(MovableDevice device)
    {
        if (device == null || CurrentDevice != null || IsOccupiedByAnotherDevice(device))
        {
            return false;
        }

        return acceptsAnyDevice || device.DeviceName == acceptedDeviceName;
    }

    public bool IsDeviceInPlacementRange(MovableDevice device)
    {
        return device != null && Vector3.SqrMagnitude(device.transform.position - PlacePosition) <= visibleRange * visibleRange;
    }

    public void Receive(MovableDevice device)
    {
        CurrentDevice = device;
        MissionManager.NotifyDevicePlaced(device, this);
    }

    public void Release(MovableDevice device)
    {
        if (CurrentDevice == device)
        {
            CurrentDevice = null;
            MissionManager.NotifyDeviceRemoved(device, this);
        }
    }

    public Vector3 GetDropPositionFor(MovableDevice device)
    {
        if (device == null)
        {
            return PlacePosition;
        }

        if (device.IsRouterDevice())
        {
            // Routers use their root/pivot as the placement anchor. Their child
            // visuals can extend far outside the physical body and skew bounds.
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
        SetPromptVisible(canReceive && visibleBlend > 0.5f, carriedDevice);
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

        Gizmos.color = new Color(1f, 0.25f, 0.1f, 0.5f);
        Gizmos.DrawWireCube(GetOccupancyCenter(), GetOccupancySize());
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

    private int InferMissionNumber()
    {
        NetworkScope resolvedScope = NetworkScope;
        if (resolvedScope != null)
        {
            string[] parts = resolvedScope.NetworkPrefix.TrimEnd('.').Split('.');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int subnet))
            {
                return subnet + 1;
            }
        }

        Transform current = transform;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("sala 1"))
            {
                return 1;
            }

            if (lowerName.Contains("sala 2"))
            {
                return 2;
            }

            if (lowerName.Contains("sala 3"))
            {
                return 3;
            }

            current = current.parent;
        }

        return 0;
    }

    private string InferPlacementTaskId()
    {
        int inferredMissionNumber = MissionNumber;
        if (!LooksLikeComputerPlacementZoneForMission(inferredMissionNumber))
        {
            return string.Empty;
        }

        if (inferredMissionNumber == 1)
        {
            return "sala1_colocar_gabinete";
        }

        if (inferredMissionNumber == 2)
        {
            return "sala2_colocar_gabinete";
        }

        return string.Empty;
    }

    public bool IsComputerPlacementZoneForMission(int targetMissionNumber)
    {
        return LooksLikeComputerPlacementZoneForMission(targetMissionNumber);
    }

    private bool LooksLikeComputerPlacementZoneForMission(int targetMissionNumber)
    {
        string lowerName = GetHierarchyName().ToLowerInvariant();
        bool isComputerDropPoint = lowerName.Contains("computer_base_droppoint")
            || lowerName.Contains("computer base droppoint")
            || lowerName.Contains("computer_base_drop_point");
        bool isDeskZone = lowerName.Contains("desk_") || lowerName.Contains("desk ");

        if (!isComputerDropPoint && !isDeskZone)
        {
            return false;
        }

        if (isComputerDropPoint)
        {
            return MissionNumber == targetMissionNumber;
        }

        if (targetMissionNumber == 1)
        {
            return IsDeskNumber(lowerName, 1);
        }

        if (targetMissionNumber == 2)
        {
            return IsDeskNumber(lowerName, 1);
        }

        return false;
    }

    private bool IsDeskNumber(string lowerName, int deskNumber)
    {
        string number = deskNumber.ToString();
        return lowerName.Contains("desk_" + number)
            || lowerName.Contains("desk_0" + number)
            || lowerName.Contains("desk " + number)
            || lowerName.Contains("desk0" + number)
            || lowerName.Contains("desk" + number);
    }

    private string GetHierarchyName()
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder(name);
        Transform current = transform.parent;
        while (current != null)
        {
            builder.Append(' ');
            builder.Append(current.name);
            current = current.parent;
        }

        return builder.ToString();
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

    private bool IsOccupiedByAnotherDevice(MovableDevice ignoredDevice)
    {
        if (CurrentDevice != null && CurrentDevice != ignoredDevice)
        {
            return true;
        }

        Vector3 center = GetOccupancyCenter();
        Vector3 halfExtents = GetOccupancySize() * 0.5f;

        MovableDevice[] devices = FindObjectsOfType<MovableDevice>();
        for (int i = 0; i < devices.Length; i++)
        {
            MovableDevice device = devices[i];
            if (device == null || device == ignoredDevice || device.IsCarried)
            {
                continue;
            }

            if (device.CurrentDropZone == this || IsInsideOccupancyFootprint(device.transform.position))
            {
                return true;
            }
        }

        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, occupancyHits, Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = occupancyHits[i];
            if (hit == null)
            {
                continue;
            }

            MovableDevice device = hit.GetComponentInParent<MovableDevice>();
            if (device == null || device == ignoredDevice || device.IsCarried)
            {
                continue;
            }

            if (device.CurrentDropZone == this || IsInsideOccupancyFootprint(device.transform.position))
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetOccupancyCenter()
    {
        return PlacePosition + Vector3.up * (occupancyHeight * 0.5f);
    }

    private Vector3 GetOccupancySize()
    {
        return new Vector3(Mathf.Max(indicatorSize.x, 0.1f), Mathf.Max(occupancyHeight, 0.1f), Mathf.Max(indicatorSize.y, 0.1f));
    }

    private bool IsInsideOccupancyFootprint(Vector3 worldPosition)
    {
        Vector3 center = PlacePosition;
        Vector3 size = GetOccupancySize();
        float halfX = size.x * 0.5f;
        float halfZ = size.z * 0.5f;
        return Mathf.Abs(worldPosition.x - center.x) <= halfX && Mathf.Abs(worldPosition.z - center.z) <= halfZ;
    }

    private void SetPromptVisible(bool visible, MovableDevice device)
    {
        EnsurePrompt();
        if (visible && device != null)
        {
            string deviceName = GetDisplayDeviceName(device);
            promptPresenter?.Show(this, "ÁREA DE ENTREGA", new InteractionPromptAction("E", "Colocar " + deviceName));
        }
        else
        {
            promptPresenter?.Hide(this);
        }
    }

    private string GetDisplayDeviceName(MovableDevice device)
    {
        if (device == null)
        {
            return "dispositivo";
        }

        string lowerName = (device.DeviceName + " " + device.name).ToLowerInvariant();
        if (lowerName.Contains("notebook") || lowerName.Contains("laptop"))
        {
            return "o notebook";
        }

        if (lowerName.Contains("printer") || lowerName.Contains("impressora"))
        {
            return "a impressora";
        }

        if (lowerName.Contains("router") || lowerName.Contains("roteador"))
        {
            return "o roteador";
        }

        if (lowerName.Contains("computer") || lowerName.Contains("computador") || lowerName.Contains("gabinete"))
        {
            return "o computador";
        }

        return "o dispositivo";
    }

    private void EnsurePrompt()
    {
        if (canvas == null)
        {
            canvas = FindCanvasByName("InteractionCanvas");
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("InteractionCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        EnsureEventSystem();

        GameObject legacyPrompt = promptObject;
        promptPresenter = InteractionPromptPresenter.GetOrCreate(canvas);
        promptObject = promptPresenter != null ? promptPresenter.gameObject : null;
        promptLabel = null;

        if (legacyPrompt != null && legacyPrompt != promptObject && legacyPrompt.name == "DropZonePrompt")
        {
            legacyPrompt.SetActive(false);
            Destroy(legacyPrompt);
        }

        Transform legacyCanvasPrompt = canvas.transform.Find("DropZonePrompt");
        if (legacyCanvasPrompt != null && legacyCanvasPrompt.gameObject != promptObject)
        {
            legacyCanvasPrompt.gameObject.SetActive(false);
            Destroy(legacyCanvasPrompt.gameObject);
        }
    }

    private Canvas FindCanvasByName(string canvasName)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == canvasName)
            {
                return canvases[i];
            }
        }

        return null;
    }

    private void EnsureEventSystem()
    {
        RuntimeEventSystemUtility.EnsureSingleEventSystem();
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
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
