using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class WiFiRangeVisualizer : MonoBehaviour
{
    private const string RootName = "WiFiRangeVisualizer";
    private const string OuterRingPrefix = "OuterDottedRing_";
    private const string InnerRingPrefix = "InnerRing";
    private const int MaxRaycastHits = 16;

    [Header("Router")]
    [SerializeField] private RouterInteractable routerInteractable;

    [Header("Ground")]
    [SerializeField] private LayerMask groundLayerMask = ~0;
    [SerializeField, Min(0f)] private float groundOffset = 0.02f;
    [SerializeField, Min(0.1f)] private float groundRaycastHeight = 3f;
    [SerializeField, Min(0.1f)] private float groundRaycastDistance = 40f;

    [Header("Visibility")]
    [SerializeField] private bool showRangeAtStart;
    [SerializeField] private Color ringColor = new Color(0.08f, 0.85f, 1f, 0.72f);

    [Header("Rings")]
    [SerializeField, Min(0.001f)] private float outerRingWidth = 0.035f;
    [SerializeField, Min(0.001f)] private float innerRingWidth = 0.018f;
    [SerializeField, Range(8, 64)] private int outerDashCount = 32;
    [SerializeField, Range(12, 128)] private int circleResolution = 48;
    [SerializeField, Range(1, 3)] private int innerRingCount = 3;

    [Header("Animation")]
    [SerializeField] private bool animateRings = true;
    [SerializeField, Min(0.01f)] private float animationSpeed = 0.25f;
    [SerializeField, Min(0.02f)] private float animationUpdateInterval = 0.08f;

    private static Material sharedLineMaterial;
    private static readonly RaycastHit[] GroundHits = new RaycastHit[MaxRaycastHits];

    private Transform visualRoot;
    private LineRenderer[] outerDashRenderers;
    private LineRenderer[] innerRingRenderers;
    private Vector3[][] innerRingPoints;
    private readonly Vector3[] dashPoints = new Vector3[2];

    private bool requestedVisible;
    private bool renderersVisible;
    private bool warnedMissingGround;
    private float lastRange = -1f;
    private int lastOuterDashCount = -1;
    private int lastCircleResolution = -1;
    private int lastInnerRingCount = -1;
    private float lastUpdateTime;
    private Vector3 groundCenter;
    private Vector3 lastRouterPosition;

    public RouterInteractable Router => routerInteractable;
    public bool IsRangeRequestedVisible => requestedVisible;
    public bool IsRangeVisible => renderersVisible;

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            requestedVisible = false;
            DisableExistingVisualRenderers();
            return;
        }

        EnsureRouterReference();
        EnsureVisualObjects();
        requestedVisible = showRangeAtStart;
        RefreshVisual(true);
    }

    private void OnEnable()
    {
        EnsureRouterReference();
        if (Application.isPlaying)
        {
            EnsureVisualObjects();
            requestedVisible = showRangeAtStart;
            RefreshVisual(true);
        }
        else
        {
            requestedVisible = false;
            DisableExistingVisualRenderers();
        }
    }

    private void Start()
    {
        if (!Application.isPlaying)
        {
            requestedVisible = false;
            DisableExistingVisualRenderers();
            return;
        }

        requestedVisible = showRangeAtStart;
        RefreshVisual(true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            SetRenderersEnabled(false);
            return;
        }

        float time = Time.time;
        if (time - lastUpdateTime < animationUpdateInterval)
        {
            return;
        }

        lastUpdateTime = time;
        RefreshVisual(false);
    }

    private void OnValidate()
    {
        outerRingWidth = Mathf.Max(0.001f, outerRingWidth);
        innerRingWidth = Mathf.Max(0.001f, innerRingWidth);
        outerDashCount = Mathf.Clamp(outerDashCount, 8, 64);
        circleResolution = Mathf.Clamp(circleResolution, 12, 128);
        innerRingCount = Mathf.Clamp(innerRingCount, 1, 3);
        groundOffset = Mathf.Max(0f, groundOffset);
        groundRaycastHeight = Mathf.Max(0.1f, groundRaycastHeight);
        groundRaycastDistance = Mathf.Max(0.1f, groundRaycastDistance);
        animationSpeed = Mathf.Max(0.01f, animationSpeed);
        animationUpdateInterval = Mathf.Max(0.02f, animationUpdateInterval);

        EnsureRouterReference();
        if (!Application.isPlaying)
        {
            requestedVisible = false;
            DisableExistingVisualRenderers();
            return;
        }

        RefreshExistingVisual();
    }

    [ContextMenu("Show Wi-Fi Range Preview")]
    public void ShowRange()
    {
        if (!Application.isPlaying)
        {
            requestedVisible = false;
            DisableExistingVisualRenderers();
            return;
        }

        requestedVisible = true;
        EnsureRouterReference();
        EnsureVisualObjects();
        RefreshVisual(true);
    }

    [ContextMenu("Hide Wi-Fi Range Preview")]
    public void HideRange()
    {
        requestedVisible = false;
        SetRenderersEnabled(false);
    }

    public void SetRangeVisible(bool visible)
    {
        if (!Application.isPlaying)
        {
            requestedVisible = false;
            DisableExistingVisualRenderers();
            return;
        }

        if (visible)
        {
            ShowRange();
        }
        else
        {
            HideRange();
        }
    }

    [ContextMenu("Rebuild Wi-Fi Range Visualizer")]
    public void RebuildVisualizer()
    {
        if (!Application.isPlaying)
        {
            requestedVisible = false;
            DisableExistingVisualRenderers();
            return;
        }

        EnsureRouterReference();
        EnsureVisualObjects(true);
        RefreshVisual(true);
    }

    private void RefreshExistingVisual()
    {
        visualRoot = transform.Find(RootName);
        if (visualRoot == null)
        {
            return;
        }

        RefreshVisual(true);
    }

    private void RefreshVisual(bool forceGeometry)
    {
        EnsureRouterReference();
        if (routerInteractable == null)
        {
            SetRenderersEnabled(false);
            return;
        }

        bool wiFiEnabledForPreview = Application.isPlaying ? routerInteractable.IsWiFiEnabled : routerInteractable.InitialWiFiEnabled;
        bool shouldRender = requestedVisible && wiFiEnabledForPreview && routerInteractable.WiFiRange > 0;
        if (!shouldRender)
        {
            SetRenderersEnabled(false);
            return;
        }

        EnsureVisualObjects();
        UpdateGroundCenter();
        SetRenderersEnabled(true);
        ApplyRendererSettings();

        float range = routerInteractable.WiFiRange;
        bool structureChanged = lastOuterDashCount != outerDashCount || lastCircleResolution != circleResolution || lastInnerRingCount != innerRingCount;
        bool geometryChanged = forceGeometry || structureChanged || !Mathf.Approximately(lastRange, range) || lastRouterPosition != transform.position;

        if (geometryChanged)
        {
            BuildOuterRing(range);
            BuildInnerRings(range, GetAnimationPhase());
            lastRange = range;
            lastRouterPosition = transform.position;
            lastOuterDashCount = outerDashCount;
            lastCircleResolution = circleResolution;
            lastInnerRingCount = innerRingCount;
        }
        else if (animateRings)
        {
            BuildInnerRings(range, GetAnimationPhase());
        }
    }

    private void EnsureRouterReference()
    {
        if (routerInteractable == null)
        {
            routerInteractable = GetComponent<RouterInteractable>();
        }
    }

    private void EnsureVisualObjects(bool forceRebuild = false)
    {
        visualRoot = transform.Find(RootName);
        if (visualRoot == null)
        {
            GameObject rootObject = new GameObject(RootName);
            visualRoot = rootObject.transform;
            visualRoot.SetParent(transform, false);
        }

        if (forceRebuild)
        {
            RemoveLineChildren();
        }

        EnsureOuterDashRenderers();
        EnsureInnerRingRenderers();
        ApplyRendererSettings();
    }

    private void EnsureOuterDashRenderers()
    {
        if (outerDashRenderers != null && outerDashRenderers.Length == outerDashCount)
        {
            return;
        }

        outerDashRenderers = new LineRenderer[outerDashCount];
        for (int i = 0; i < outerDashCount; i++)
        {
            outerDashRenderers[i] = GetOrCreateLineRenderer(OuterRingPrefix + (i + 1).ToString("00"));
        }
    }

    private void EnsureInnerRingRenderers()
    {
        if (innerRingRenderers != null && innerRingRenderers.Length == innerRingCount && innerRingPoints != null && innerRingPoints.Length == innerRingCount)
        {
            return;
        }

        innerRingRenderers = new LineRenderer[innerRingCount];
        innerRingPoints = new Vector3[innerRingCount][];
        for (int i = 0; i < innerRingCount; i++)
        {
            innerRingRenderers[i] = GetOrCreateLineRenderer(InnerRingPrefix + (i + 1).ToString("00"));
            innerRingPoints[i] = new Vector3[circleResolution + 1];
        }
    }

    private LineRenderer GetOrCreateLineRenderer(string objectName)
    {
        Transform child = visualRoot.Find(objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(objectName);
            child = childObject.transform;
            child.SetParent(visualRoot, false);
        }

        LineRenderer lineRenderer = child.GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = child.gameObject.AddComponent<LineRenderer>();
        }

        return lineRenderer;
    }

    private void RemoveLineChildren()
    {
        for (int i = visualRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = visualRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        outerDashRenderers = null;
        innerRingRenderers = null;
        innerRingPoints = null;
    }

    private void UpdateGroundCenter()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * groundRaycastHeight;
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, GroundHits, groundRaycastHeight + groundRaycastDistance, groundLayerMask, QueryTriggerInteraction.Ignore);
        float lowestY = float.PositiveInfinity;
        bool foundGround = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = GroundHits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            float hitY = GroundHits[i].point.y;
            if (hitY < lowestY)
            {
                lowestY = hitY;
                foundGround = true;
            }
        }

        if (foundGround)
        {
            warnedMissingGround = false;
            groundCenter = new Vector3(transform.position.x, lowestY + groundOffset, transform.position.z);
        }
        else
        {
            groundCenter = new Vector3(transform.position.x, transform.position.y + groundOffset, transform.position.z);
            if (!warnedMissingGround)
            {
                Debug.LogWarning("WiFiRangeVisualizer could not find ground below " + name + ". Using router position as fallback.", this);
                warnedMissingGround = true;
            }
        }

        if (visualRoot != null)
        {
            visualRoot.position = groundCenter;
            visualRoot.rotation = Quaternion.identity;
        }
    }

    private void BuildOuterRing(float range)
    {
        float angleStep = Mathf.PI * 2f / outerDashCount;
        float dashAngle = angleStep * 0.62f;

        for (int i = 0; i < outerDashCount; i++)
        {
            float centerAngle = i * angleStep;
            float startAngle = centerAngle - dashAngle * 0.5f;
            float endAngle = centerAngle + dashAngle * 0.5f;

            dashPoints[0] = GetCirclePoint(startAngle, range);
            dashPoints[1] = GetCirclePoint(endAngle, range);

            LineRenderer renderer = outerDashRenderers[i];
            renderer.positionCount = 2;
            renderer.SetPositions(dashPoints);
        }
    }

    private void BuildInnerRings(float range, float animationPhase)
    {
        for (int ringIndex = 0; ringIndex < innerRingCount; ringIndex++)
        {
            float baseRatio = (ringIndex + 1f) / (innerRingCount + 1f);
            if (innerRingCount == 3)
            {
                baseRatio = ringIndex == 0 ? 0.3f : ringIndex == 1 ? 0.6f : 0.9f;
            }

            float ringPhase = Mathf.Repeat(animationPhase + ringIndex * 0.33f, 1f);
            float pulseAmount = animateRings ? Mathf.SmoothStep(0f, 1f, ringPhase) * 0.08f : 0f;
            float radius = Mathf.Min(range, range * (baseRatio + pulseAmount));
            Vector3[] points = innerRingPoints[ringIndex];

            if (points == null || points.Length != circleResolution + 1)
            {
                points = new Vector3[circleResolution + 1];
                innerRingPoints[ringIndex] = points;
            }

            for (int i = 0; i <= circleResolution; i++)
            {
                float angle = Mathf.PI * 2f * i / circleResolution;
                points[i] = GetCirclePoint(angle, radius);
            }

            LineRenderer renderer = innerRingRenderers[ringIndex];
            renderer.positionCount = points.Length;
            renderer.SetPositions(points);

            float alpha = animateRings ? Mathf.Lerp(0.22f, 0.08f, ringPhase) : 0.18f;
            Color innerColor = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * alpha);
            renderer.startColor = innerColor;
            renderer.endColor = innerColor;
        }
    }

    private Vector3 GetCirclePoint(float angle, float radius)
    {
        return new Vector3(
            groundCenter.x + Mathf.Cos(angle) * radius,
            groundCenter.y,
            groundCenter.z + Mathf.Sin(angle) * radius);
    }

    private float GetAnimationPhase()
    {
        if (!animateRings)
        {
            return 0f;
        }

        return Mathf.Repeat(Time.time * animationSpeed, 1f);
    }

    private void ApplyRendererSettings()
    {
        Material lineMaterial = GetLineMaterial();
        Color outerColor = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a);

        if (outerDashRenderers != null)
        {
            for (int i = 0; i < outerDashRenderers.Length; i++)
            {
                ConfigureLineRenderer(outerDashRenderers[i], lineMaterial, outerRingWidth, outerColor);
            }
        }

        if (innerRingRenderers != null)
        {
            for (int i = 0; i < innerRingRenderers.Length; i++)
            {
                Color innerColor = new Color(ringColor.r, ringColor.g, ringColor.b, ringColor.a * 0.18f);
                ConfigureLineRenderer(innerRingRenderers[i], lineMaterial, innerRingWidth, innerColor);
            }
        }
    }

    private static void ConfigureLineRenderer(LineRenderer lineRenderer, Material lineMaterial, float width, Color color)
    {
        if (lineRenderer == null)
        {
            return;
        }

        lineRenderer.sharedMaterial = lineMaterial;
        lineRenderer.useWorldSpace = true;
        lineRenderer.loop = false;
        lineRenderer.textureMode = LineTextureMode.Stretch;
        lineRenderer.alignment = LineAlignment.View;
        lineRenderer.numCornerVertices = 2;
        lineRenderer.numCapVertices = 2;
        lineRenderer.widthMultiplier = width;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
        lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lineRenderer.receiveShadows = false;
        lineRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lineRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    private void SetRenderersEnabled(bool enabled)
    {
        renderersVisible = enabled;

        if (outerDashRenderers != null)
        {
            for (int i = 0; i < outerDashRenderers.Length; i++)
            {
                if (outerDashRenderers[i] != null)
                {
                    outerDashRenderers[i].enabled = enabled;
                }
            }
        }

        if (innerRingRenderers != null)
        {
            for (int i = 0; i < innerRingRenderers.Length; i++)
            {
                if (innerRingRenderers[i] != null)
                {
                    innerRingRenderers[i].enabled = enabled;
                }
            }
        }
    }

    private void DisableExistingVisualRenderers()
    {
        visualRoot = transform.Find(RootName);
        if (visualRoot == null)
        {
            renderersVisible = false;
            return;
        }

        LineRenderer[] lineRenderers = visualRoot.GetComponentsInChildren<LineRenderer>(true);
        foreach (LineRenderer lineRenderer in lineRenderers)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = false;
            }
        }

        renderersVisible = false;
    }

    private static Material GetLineMaterial()
    {
        if (sharedLineMaterial != null)
        {
            return sharedLineMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        sharedLineMaterial = new Material(shader)
        {
            name = "Runtime_WiFiRange_Line",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 3000
        };
        sharedLineMaterial.color = Color.white;
        return sharedLineMaterial;
    }
}
