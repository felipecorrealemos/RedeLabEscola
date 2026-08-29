using UnityEngine;

[DisallowMultipleComponent]
public class MovableDevice : MonoBehaviour
{
    [SerializeField] private string deviceName = "Device";
    [SerializeField] private float settleSpeed = 10f;
    [SerializeField] private Vector2 interactionIndicatorSize = new Vector2(1.4f, 1.4f);
    [SerializeField] private Color interactionColor = new Color(1f, 0.85f, 0.15f, 0.45f);
    [SerializeField] private float interactionIndicatorHeight = 0.03f;
    [SerializeField] private float interactionPulseAmount = 0.12f;
    [SerializeField] private float interactionPulseSpeed = 5f;

    private Transform targetAnchor;
    private Transform originalParent;
    private DeviceDropZone currentDropZone;
    private Collider[] colliders;
    private Rigidbody rigidBody;
    private Transform interactionIndicator;
    private Renderer interactionIndicatorRenderer;
    private Material interactionIndicatorMaterial;
    private Vector3 baseIndicatorScale;
    private bool isInteractionHighlighted;
    private bool isDropping;
    private DeviceDropZone pendingDropZone;
    private Vector3 dropTargetPosition;
    private Quaternion dropTargetRotation;
    private Vector3 defaultLocalScale;

    public bool IsCarried { get; private set; }
    public string DeviceName => deviceName;
    public bool IsPlaced => currentDropZone != null && !IsCarried && !isDropping;
    public DeviceDropZone CurrentDropZone => currentDropZone;

    public void ConfigureDeviceName(string nextDeviceName)
    {
        if (!string.IsNullOrWhiteSpace(nextDeviceName))
        {
            deviceName = nextDeviceName;
        }
    }

    private void Awake()
    {
        defaultLocalScale = transform.localScale;
        RefreshReferences();
        EnsureInteractionIndicator();
        EnsureComputerInteractable();
    }

    private void Update()
    {
        UpdateInteractionIndicator();

        if (isDropping)
        {
            UpdateDropMotion();
            return;
        }

        if (!IsCarried || targetAnchor == null)
        {
            return;
        }

        transform.localPosition = Vector3.Lerp(transform.localPosition, Vector3.zero, settleSpeed * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, settleSpeed * Time.deltaTime);
    }

    public void PickUp(Transform carryAnchor)
    {
        if (carryAnchor == null)
        {
            return;
        }

        targetAnchor = carryAnchor;
        originalParent = transform.parent;
        IsCarried = true;
        isDropping = false;
        pendingDropZone = null;
        RefreshReferences();
        currentDropZone?.Release(this);
        currentDropZone = null;
        GetComponent<ComputerInteractable>()?.HandlePickedUp();

        if (rigidBody != null)
        {
            rigidBody.isKinematic = true;
            rigidBody.useGravity = false;
        }

        SetCollidersEnabled(false);
        SetInteractionHighlighted(false);
        transform.SetParent(targetAnchor, true);
        transform.localScale = defaultLocalScale;
    }

    public void DropAt(DeviceDropZone dropZone)
    {
        if (dropZone == null)
        {
            return;
        }

        IsCarried = false;
        targetAnchor = null;
        isDropping = true;
        pendingDropZone = dropZone;
        dropTargetRotation = dropZone.PlaceRotation;
        dropTargetPosition = dropZone.GetDropPositionFor(this);
        transform.SetParent(GetStablePlacementParent(dropZone.transform), true);
        transform.localScale = defaultLocalScale;

        if (rigidBody != null)
        {
            rigidBody.isKinematic = true;
            rigidBody.useGravity = false;
        }

        SetCollidersEnabled(false);
        SetInteractionHighlighted(false);
        dropZone.Receive(this);
        currentDropZone = dropZone;
    }

    public void RestorePlacedAt(DeviceDropZone dropZone)
    {
        if (dropZone == null) return;

        RefreshReferences();
        currentDropZone?.Release(this);
        IsCarried = false;
        isDropping = false;
        pendingDropZone = null;
        targetAnchor = null;
        currentDropZone = dropZone;
        transform.SetParent(GetStablePlacementParent(dropZone.transform), true);
        transform.SetPositionAndRotation(dropZone.GetDropPositionFor(this), dropZone.PlaceRotation);
        transform.localScale = defaultLocalScale;
        if (rigidBody != null)
        {
            rigidBody.isKinematic = true;
            rigidBody.useGravity = false;
        }
        SetCollidersEnabled(true);
        SetInteractionHighlighted(false);
        dropZone.Receive(this);
        GetComponent<ComputerInteractable>()?.HandlePlaced(dropZone);
    }

    public float GetBottomToPivotOffset()
    {
        Bounds bounds = GetInteractionBounds();
        return transform.position.y - bounds.min.y;
    }

    public void CancelCarry()
    {
        IsCarried = false;
        isDropping = false;
        pendingDropZone = null;
        targetAnchor = null;
        transform.SetParent(GetStablePlacementParent(originalParent), true);
        transform.localScale = defaultLocalScale;
        SetCollidersEnabled(true);
        SetInteractionHighlighted(false);
    }

    public void SetInteractionHighlighted(bool highlighted)
    {
        isInteractionHighlighted = highlighted && !IsCarried;
    }

    private void SetCollidersEnabled(bool enabled)
    {
        WiFiDevice wiFiDevice = GetComponent<WiFiDevice>();
        foreach (Collider deviceCollider in colliders)
        {
            if (deviceCollider != null)
            {
                bool keepWiFiSensorEnabled = !enabled && wiFiDevice != null && wiFiDevice.IsWiFiSensorCollider(deviceCollider);
                deviceCollider.enabled = enabled || keepWiFiSensorEnabled;
            }
        }
    }

    private void UpdateDropMotion()
    {
        transform.position = Vector3.Lerp(transform.position, dropTargetPosition, settleSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, dropTargetRotation, settleSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, dropTargetPosition) > 0.01f || Quaternion.Angle(transform.rotation, dropTargetRotation) > 1f)
        {
            return;
        }

        FinishDropMotion();
    }

    private void FinishDropMotion()
    {
        isDropping = false;
        transform.position = dropTargetPosition;
        transform.rotation = dropTargetRotation;

        if (pendingDropZone != null)
        {
            transform.SetParent(GetStablePlacementParent(pendingDropZone.transform), true);
            transform.localScale = defaultLocalScale;
        }

        pendingDropZone = null;
        SetCollidersEnabled(true);
        GetComponent<ComputerInteractable>()?.HandlePlaced(currentDropZone);
    }

    private void RefreshReferences()
    {
        colliders = GetComponentsInChildren<Collider>();
        rigidBody = GetComponent<Rigidbody>();
    }

    public ComputerInteractable EnsureComputerInteractable()
    {
        if (!IsComputerDevice())
        {
            return null;
        }

        ComputerInteractable computer = GetComponent<ComputerInteractable>();
        if (computer == null)
        {
            computer = gameObject.AddComponent<ComputerInteractable>();
        }

        return computer;
    }

    public bool IsComputerDevice()
    {
        string lowerDeviceName = deviceName.ToLowerInvariant();
        string lowerObjectName = name.ToLowerInvariant();
        return lowerDeviceName.Contains("computer")
            || lowerDeviceName.Contains("computador")
            || lowerDeviceName.Contains("gabinete")
            || lowerDeviceName.Contains("printer")
            || lowerDeviceName.Contains("impressora")
            || lowerObjectName.Contains("computer")
            || lowerObjectName.Contains("computador")
            || lowerObjectName.Contains("gabinete")
            || lowerObjectName.Contains("printer")
            || lowerObjectName.Contains("impressora");
    }

    public bool IsComputerCabinetDevice()
    {
        if (HasChildNamed(transform, "Computer_Base"))
        {
            return true;
        }

        string lowerDeviceName = deviceName.ToLowerInvariant();
        string lowerObjectName = name.ToLowerInvariant();
        bool isPrinter = lowerDeviceName.Contains("printer")
            || lowerDeviceName.Contains("impressora")
            || lowerObjectName.Contains("printer")
            || lowerObjectName.Contains("impressora");

        if (isPrinter)
        {
            return false;
        }

        return lowerDeviceName.Contains("computer")
            || lowerDeviceName.Contains("computador")
            || lowerDeviceName.Contains("gabinete")
            || lowerObjectName.Contains("computer")
            || lowerObjectName.Contains("computador")
            || lowerObjectName.Contains("gabinete");
    }

    public bool IsPrinterDevice()
    {
        string lowerDeviceName = deviceName.ToLowerInvariant();
        string lowerObjectName = name.ToLowerInvariant();
        return lowerDeviceName.Contains("printer")
            || lowerDeviceName.Contains("impressora")
            || lowerObjectName.Contains("printer")
            || lowerObjectName.Contains("impressora");
    }

    public bool IsRouterDevice()
    {
        string lowerDeviceName = deviceName.ToLowerInvariant();
        string lowerObjectName = name.ToLowerInvariant();
        return lowerDeviceName.Contains("router")
            || lowerDeviceName.Contains("roteador")
            || lowerObjectName.Contains("router")
            || lowerObjectName.Contains("roteador");
    }

    private bool HasChildNamed(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName))
        {
            return false;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureInteractionIndicator()
    {
        if (interactionIndicator != null)
        {
            return;
        }

        Transform existingIndicator = transform.Find("InteractionIndicator");
        if (existingIndicator != null)
        {
            interactionIndicator = existingIndicator;
        }
        else
        {
            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            indicatorObject.name = "InteractionIndicator";
            interactionIndicator = indicatorObject.transform;
            interactionIndicator.SetParent(transform);
            Destroy(indicatorObject.GetComponent<Collider>());
        }

        interactionIndicator.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        baseIndicatorScale = new Vector3(interactionIndicatorSize.x, interactionIndicatorSize.y, 1f);
        interactionIndicator.localScale = baseIndicatorScale;

        interactionIndicatorRenderer = interactionIndicator.GetComponent<Renderer>();
        interactionIndicatorMaterial = new Material(GetIndicatorShader());
        interactionIndicatorMaterial.color = new Color(interactionColor.r, interactionColor.g, interactionColor.b, 0f);
        interactionIndicatorRenderer.sharedMaterial = interactionIndicatorMaterial;
        interactionIndicator.gameObject.SetActive(false);
    }

    private void UpdateInteractionIndicator()
    {
        EnsureInteractionIndicator();

        bool shouldShow = isInteractionHighlighted && !IsCarried;
        interactionIndicator.gameObject.SetActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        PositionInteractionIndicator();

        float pulse = 1f + (Mathf.Sin(Time.time * interactionPulseSpeed) * 0.5f + 0.5f) * interactionPulseAmount;
        interactionIndicator.localScale = baseIndicatorScale * pulse;
        interactionIndicatorMaterial.color = interactionColor;
    }

    private void PositionInteractionIndicator()
    {
        interactionIndicator.position = GetInteractionIndicatorPosition();
    }

    private Vector3 GetInteractionIndicatorPosition()
    {
        if (currentDropZone != null)
        {
            return currentDropZone.IndicatorPosition;
        }

        Bounds bounds = GetInteractionBounds();
        Vector3 surfacePosition = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 rayOrigin = new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);
        float rayDistance = bounds.size.y + 1.5f;
        RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, rayDistance, ~0, QueryTriggerInteraction.Ignore);
        float highestSurface = float.NegativeInfinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || IsOwnCollider(hit.collider))
            {
                continue;
            }

            if (hit.point.y > highestSurface)
            {
                highestSurface = hit.point.y;
                surfacePosition = hit.point;
            }
        }

        return new Vector3(surfacePosition.x, surfacePosition.y + interactionIndicatorHeight, surfacePosition.z);
    }

    private bool IsOwnCollider(Collider hitCollider)
    {
        foreach (Collider deviceCollider in colliders)
        {
            if (deviceCollider == hitCollider)
            {
                return true;
            }
        }

        return false;
    }

    private Bounds GetInteractionBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(transform.position, Vector3.one);
        bool hasBounds = false;

        foreach (Renderer deviceRenderer in renderers)
        {
            if (ShouldIgnoreBoundsRenderer(deviceRenderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = deviceRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(deviceRenderer.bounds);
            }
        }

        return bounds;
    }

    private bool ShouldIgnoreBoundsRenderer(Renderer deviceRenderer)
    {
        if (deviceRenderer == null || deviceRenderer.transform == interactionIndicator)
        {
            return true;
        }

        Transform rendererTransform = deviceRenderer.transform;
        string rendererName = rendererTransform.name;
        if (rendererName == "InteractionIndicator"
            || rendererName == "DropIndicator"
            || rendererName == "WiFiRangeVisualizer"
            || rendererName.StartsWith("OuterDottedRing_")
            || rendererName.StartsWith("InnerRing"))
        {
            return true;
        }

        if (deviceRenderer is LineRenderer)
        {
            return true;
        }

        return rendererTransform.GetComponentInParent<WiFiRangeVisualizer>() != null;
    }

    private Transform GetStablePlacementParent(Transform referenceTransform)
    {
        Transform candidate = referenceTransform;
        while (candidate != null)
        {
            if (HasUnitWorldScale(candidate))
            {
                return candidate;
            }

            candidate = candidate.parent;
        }

        return GetScenePlacementRoot();
    }

    private bool HasUnitWorldScale(Transform candidate)
    {
        Vector3 scale = candidate.lossyScale;
        return Mathf.Approximately(scale.x, 1f)
            && Mathf.Approximately(scale.y, 1f)
            && Mathf.Approximately(scale.z, 1f);
    }

    private Transform GetScenePlacementRoot()
    {
        const string placementRootName = "PlacedMovableDevices";
        GameObject existingRoot = GameObject.Find(placementRootName);
        if (existingRoot != null)
        {
            return existingRoot.transform;
        }

        GameObject placementRoot = new GameObject(placementRootName);
        placementRoot.transform.position = Vector3.zero;
        placementRoot.transform.rotation = Quaternion.identity;
        placementRoot.transform.localScale = Vector3.one;
        return placementRoot.transform;
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
