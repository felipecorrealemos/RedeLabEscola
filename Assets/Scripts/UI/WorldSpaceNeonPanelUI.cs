using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[ExecuteAlways]
public class WorldSpaceNeonPanelUI : MonoBehaviour
{
    public enum PresentationMode
    {
        WorldSpace,
        ScreenSpaceProjected
    }

    [Header("Texts")]
    [SerializeField] private string title = "ACTIVATE";
    [SerializeField] private string subtitle = "Power up the conveyor";
    [SerializeField, Min(1f)] private float titleFontSize = 38f;
    [SerializeField, Min(1f)] private float subtitleFontSize = 15f;

    [Header("Panel")]
    [SerializeField] private Vector2 panelSize = new Vector2(360f, 132f);
    [SerializeField] private Color backgroundColor = new Color(0.005f, 0.035f, 0.025f, 0.92f);
    [SerializeField] private Color borderColor = new Color(0.1f, 1f, 0.45f, 1f);

    [Header("Glow")]
    [SerializeField] private Color glowColor = new Color(0.1f, 1f, 0.45f, 1f);
    [SerializeField] private Color glowPulseColor = new Color(0.45f, 1f, 0.75f, 1f);
    [SerializeField, Range(0f, 1f)] private float glowBack01Alpha = 0.18f;
    [SerializeField, Range(0f, 1f)] private float glowBack02Alpha = 0.36f;
    [SerializeField, Range(0f, 1f)] private float glowPulseAlphaBoost = 0.28f;
    [SerializeField, Min(1f)] private float glowBack01Scale = 1.2f;
    [SerializeField, Min(1f)] private float glowBack02Scale = 1.08f;
    [SerializeField, Range(0.1f, 2f)] private float visualGlowIntensity = 1f;
    [SerializeField] private bool animateGlowPulse = true;
    [SerializeField, Min(0.05f)] private float glowPulseSpeed = 1.1f;
    [SerializeField, Range(0f, 1f)] private float glowPulseAmount = 0.55f;

    [Header("Border Color Flow")]
    [SerializeField] private bool animateBorderColorFlow = true;
    [SerializeField] private bool useProceduralSquareFlowBorder = true;
    [SerializeField, Min(0f)] private float borderFlowSpeed = 0.175f;
    [SerializeField, Tooltip("How many different colors are visible around the border at the same time. Lower values look more uniform.")]
    private float borderVisibleColorSpread = 0.35f;
    [SerializeField, Range(0.001f, 0.2f), Tooltip("Visual thickness of the animated border.")]
    private float borderFlowWidth = 0.025f;
    [SerializeField, Range(0.001f, 0.08f), Tooltip("Soft edge width for the animated border.")]
    private float borderFlowSoftness = 0.01f;
    [SerializeField, Range(0f, 1f)] private float borderFlowSaturation = 0.85f;
    [SerializeField, Range(0f, 2f)] private float borderFlowBrightness = 1.25f;
    [SerializeField, Range(0f, 1f)] private float borderFlowAlpha = 1f;

    [Header("Text Colors")]
    [SerializeField] private Color titleColor = new Color(0.16f, 1f, 0.52f, 1f);
    [SerializeField] private Color titlePulseColor = new Color(0.7f, 1f, 0.82f, 1f);
    [SerializeField] private Color subtitleColor = new Color(0.78f, 1f, 0.88f, 1f);

    [Header("Behavior")]
    [SerializeField] private bool enableFadeByPlayerDistance = true;
    [SerializeField] private bool showPreviewInEditMode = true;
    [SerializeField] private PresentationMode presentationMode = PresentationMode.ScreenSpaceProjected;
    [SerializeField] private Transform worldAnchor;
    [SerializeField] private Vector2 screenSpaceOffset = new Vector2(0f, 64f);
    [SerializeField] private int screenSpaceSortingOrder = 70;
    [SerializeField, Min(0.1f)] private float appearanceDistance = 2.4f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.22f;
    [SerializeField, Min(0.1f)] private float playerSearchInterval = 1f;
    [SerializeField] private Transform playerReference;

    [Header("Billboard")]
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool rotateOnlyOnY = true;
    [SerializeField] private Camera cameraReference;

    [Header("References")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Image glowBack01;
    [SerializeField] private Image glowBack02;
    [SerializeField] private Image background;
    [SerializeField] private Image border;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private Image pointerTail;

    private bool targetVisible;
    private bool currentProjectedOnScreen = true;
    private float nextPlayerSearchTime;
    private Color currentGlowColor;
    private Color currentTitleColor;
    private float currentPulse;
    private Material borderFlowMaterial;
    private Material glowFlowMaterial;

    private void Reset()
    {
        CacheReferences();
        ConfigureCanvasGroup();
        ConfigureCanvasForPresentation();
        ApplyVisuals();
    }

    private void OnEnable()
    {
        CacheReferences();
        ConfigureCanvasGroup();
        ConfigureCanvasForPresentation();
        ApplyVisuals();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ApplyEditorPreview();
            return;
        }

        UpdateProjectedUiPosition();
        UpdateVisibilityFromPlayerDistance();
        UpdateCanvasAlpha();
        if (presentationMode == PresentationMode.WorldSpace)
        {
            ApplyBillboard();
        }

        UpdatePulseVisuals();
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && presentationMode == PresentationMode.WorldSpace)
        {
            ApplyBillboard();
        }
    }

    private void OnValidate()
    {
        glowBack01Scale = Mathf.Max(1f, glowBack01Scale);
        glowBack02Scale = Mathf.Max(1f, glowBack02Scale);
        visualGlowIntensity = Mathf.Max(0.1f, visualGlowIntensity);
        glowPulseSpeed = Mathf.Max(0.05f, glowPulseSpeed);
        appearanceDistance = Mathf.Max(0.1f, appearanceDistance);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        playerSearchInterval = Mathf.Max(0.1f, playerSearchInterval);
        borderVisibleColorSpread = Mathf.Max(0f, borderVisibleColorSpread);
        borderFlowWidth = Mathf.Max(0.001f, borderFlowWidth);
        borderFlowSoftness = Mathf.Max(0.001f, borderFlowSoftness);
        CacheReferences();
        ConfigureCanvasGroup();
        ConfigureCanvasForPresentation();
        ApplyVisuals();
        ConfigureFlowMaterials();
        UpdatePulseVisuals();
    }

    public void AssignReferences(
        Canvas canvas,
        CanvasGroup group,
        RectTransform root,
        Image outerGlow,
        Image innerGlow,
        Image panelBackground,
        Image panelBorder,
        TMP_Text mainText,
        TMP_Text secondaryText,
        Image tail)
    {
        worldCanvas = canvas;
        canvasGroup = group;
        panelRoot = root;
        glowBack01 = outerGlow;
        glowBack02 = innerGlow;
        background = panelBackground;
        border = panelBorder;
        titleText = mainText;
        subtitleText = secondaryText;
        pointerTail = tail;
        ConfigureCanvasGroup();
        ApplyVisuals();
    }

    public void ConfigureBehaviorDefaults(bool fadeByDistance, float distance, float fadeSeconds, bool previewVisible)
    {
        enableFadeByPlayerDistance = fadeByDistance;
        appearanceDistance = Mathf.Max(0.1f, distance);
        fadeDuration = Mathf.Max(0.01f, fadeSeconds);
        showPreviewInEditMode = previewVisible;
        ConfigureCanvasGroup();
    }

    public void ConfigurePresentationDefaults(PresentationMode mode, Transform anchor, Vector2 projectedOffset)
    {
        presentationMode = mode;
        worldAnchor = anchor;
        screenSpaceOffset = projectedOffset;
        ConfigureCanvasForPresentation();
    }

    public void ConfigureBillboardDefaults(bool shouldFaceCamera, bool shouldRotateOnlyOnY)
    {
        faceCamera = shouldFaceCamera;
        rotateOnlyOnY = shouldRotateOnlyOnY;
    }

    public void ConfigureTextDefaults(string mainTitle, string secondarySubtitle)
    {
        title = mainTitle;
        subtitle = secondarySubtitle;
        ApplyVisuals();
    }

    [ContextMenu("Apply Neon Panel Visuals")]
    public void ApplyVisuals()
    {
        if (panelRoot != null)
        {
            panelRoot.sizeDelta = panelSize;
        }

        ConfigureGlow(glowBack01, glowBack01Scale, glowBack01Alpha);
        ConfigureGlow(glowBack02, glowBack02Scale, glowBack02Alpha);

        if (background != null)
        {
            Stretch(background.rectTransform);
            background.color = backgroundColor;
            background.raycastTarget = false;
        }

        if (border != null)
        {
            Stretch(border.rectTransform);
            if (useProceduralSquareFlowBorder && animateBorderColorFlow)
            {
                border.sprite = null;
            }

            border.color = ResolveBorderColor();
            border.material = ResolveBorderMaterial();
            border.raycastTarget = false;
            border.preserveAspect = false;
        }

        if (titleText != null)
        {
            titleText.text = title;
            titleText.fontSize = titleFontSize;
            titleText.color = ResolveTitleColor();
            titleText.raycastTarget = false;
        }

        if (subtitleText != null)
        {
            subtitleText.text = subtitle;
            subtitleText.fontSize = subtitleFontSize;
            subtitleText.color = subtitleColor;
            subtitleText.raycastTarget = false;
        }

        if (pointerTail != null)
        {
            Color tailColor = ResolveGlowColor();
            pointerTail.color = new Color(tailColor.r, tailColor.g, tailColor.b, 0.8f);
            pointerTail.raycastTarget = false;
        }

        ConfigureFlowMaterials();
    }

    private void CacheReferences()
    {
        if (worldCanvas == null)
        {
            worldCanvas = GetComponentInChildren<Canvas>(true);
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        if (worldAnchor == null)
        {
            worldAnchor = transform;
        }

        if (panelRoot == null)
        {
            Transform root = transform.Find("Canvas_WorldSpace/PanelRoot");
            panelRoot = root as RectTransform;
        }

        if (panelRoot == null)
        {
            return;
        }

        glowBack01 = glowBack01 != null ? glowBack01 : FindImage("Glow_Back_01");
        glowBack02 = glowBack02 != null ? glowBack02 : FindImage("Glow_Back_02");
        background = background != null ? background : FindImage("Background");
        border = border != null ? border : FindImage("Border");
        titleText = titleText != null ? titleText : FindText("TitleText");
        subtitleText = subtitleText != null ? subtitleText : FindText("SubtitleText");
        pointerTail = pointerTail != null ? pointerTail : FindImage("PointerTail");
    }

    private void ConfigureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        if (!Application.isPlaying)
        {
            canvasGroup.alpha = showPreviewInEditMode ? 1f : 0f;
        }
        else if (!targetVisible)
        {
            canvasGroup.alpha = 0f;
        }
    }

    private void ConfigureCanvasForPresentation()
    {
        if (worldCanvas == null)
        {
            return;
        }

        if (presentationMode == PresentationMode.ScreenSpaceProjected)
        {
            worldCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            worldCanvas.sortingOrder = screenSpaceSortingOrder;
        }
        else
        {
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = screenSpaceSortingOrder;
        }
    }

    private void ApplyEditorPreview()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = showPreviewInEditMode ? 1f : 0f;
        }
    }

    private void UpdateVisibilityFromPlayerDistance()
    {
        if (!enableFadeByPlayerDistance)
        {
            targetVisible = true;
            return;
        }

        CachePlayer();
        targetVisible = currentProjectedOnScreen && playerReference != null && IsPlayerNear();
    }

    private bool IsPlayerNear()
    {
        Vector3 playerPosition = playerReference.position;
        Vector3 panelPosition = GetAnchorWorldPosition();
        playerPosition.y = panelPosition.y;
        return Vector3.SqrMagnitude(playerPosition - panelPosition) <= appearanceDistance * appearanceDistance;
    }

    private Vector3 GetAnchorWorldPosition()
    {
        return worldAnchor != null ? worldAnchor.position : transform.position;
    }

    private void CachePlayer()
    {
        if (playerReference != null || Time.time < nextPlayerSearchTime)
        {
            return;
        }

        nextPlayerSearchTime = Time.time + Mathf.Max(playerSearchInterval, 0.1f);
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerObject = GameObject.Find("Player");
        }

        if (playerObject != null)
        {
            playerReference = playerObject.transform;
        }
    }

    private void UpdateCanvasAlpha()
    {
        if (canvasGroup == null)
        {
            return;
        }

        float targetAlpha = targetVisible ? 1f : 0f;
        float duration = Mathf.Max(fadeDuration, 0.01f);
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime / duration);
    }

    private void UpdateProjectedUiPosition()
    {
        if (presentationMode != PresentationMode.ScreenSpaceProjected)
        {
            currentProjectedOnScreen = true;
            return;
        }

        CacheCamera();
        if (cameraReference == null || panelRoot == null || worldCanvas == null)
        {
            currentProjectedOnScreen = false;
            return;
        }

        Vector3 screenPoint = cameraReference.WorldToScreenPoint(GetAnchorWorldPosition());
        currentProjectedOnScreen = screenPoint.z > 0f
            && screenPoint.x >= 0f
            && screenPoint.x <= Screen.width
            && screenPoint.y >= 0f
            && screenPoint.y <= Screen.height;

        if (!currentProjectedOnScreen)
        {
            return;
        }

        RectTransform canvasRect = worldCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
        panelRoot.anchoredPosition = localPoint + screenSpaceOffset;
        panelRoot.localRotation = Quaternion.identity;
    }

    private void ApplyBillboard()
    {
        if (!faceCamera)
        {
            return;
        }

        CacheCamera();
        if (cameraReference == null)
        {
            return;
        }

        Transform billboardTransform = worldCanvas != null ? worldCanvas.transform : transform;
        Vector3 direction = billboardTransform.position - cameraReference.transform.position;
        if (rotateOnlyOnY)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3 upDirection = rotateOnlyOnY ? Vector3.up : cameraReference.transform.up;
        billboardTransform.rotation = Quaternion.LookRotation(direction.normalized, upDirection);
    }

    private void CacheCamera()
    {
        if (cameraReference != null)
        {
            return;
        }

        cameraReference = Camera.main;
    }

    private Image FindImage(string childName)
    {
        Transform child = panelRoot.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = panelRoot.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private void ConfigureGlow(Image image, float scale, float alpha)
    {
        if (image == null)
        {
            return;
        }

        if (useProceduralSquareFlowBorder && animateBorderColorFlow)
        {
            image.sprite = null;
        }

        RectTransform rect = image.rectTransform;
        Stretch(rect);
        rect.localScale = Vector3.one * scale;
        float finalAlpha = ResolveGlowAlpha(alpha);
        Color resolvedGlow = ResolveGlowColor();
        image.color = new Color(resolvedGlow.r, resolvedGlow.g, resolvedGlow.b, finalAlpha);
        image.material = ResolveGlowMaterial();
        image.raycastTarget = false;
        image.preserveAspect = false;
    }

    private void UpdatePulseVisuals()
    {
        if (!animateGlowPulse)
        {
            currentGlowColor = glowColor;
            currentTitleColor = titleColor;
            return;
        }

        float pulse = (Mathf.Sin(Time.time * glowPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
        currentPulse = pulse * glowPulseAmount;
        currentGlowColor = Color.Lerp(glowColor, glowPulseColor, currentPulse);
        currentTitleColor = Color.Lerp(titleColor, titlePulseColor, currentPulse);

        ConfigureGlow(glowBack01, glowBack01Scale, glowBack01Alpha);
        ConfigureGlow(glowBack02, glowBack02Scale, glowBack02Alpha);

        if (border != null)
        {
            Color borderPulseColor = ResolveBorderColor();
            border.color = new Color(borderPulseColor.r, borderPulseColor.g, borderPulseColor.b, Mathf.Clamp01(borderColor.a + currentPulse * 0.2f));
            border.material = ResolveBorderMaterial();
        }

        if (titleText != null)
        {
            titleText.color = ResolveTitleColor();
        }

        if (pointerTail != null)
        {
            Color tailColor = ResolveGlowColor();
            pointerTail.color = new Color(tailColor.r, tailColor.g, tailColor.b, 0.8f);
        }

        UpdateFlowMaterials();
    }

    private Color ResolveGlowColor()
    {
        return animateGlowPulse ? currentGlowColor : glowColor;
    }

    private Color ResolveTitleColor()
    {
        return animateGlowPulse ? currentTitleColor : titleColor;
    }

    private Color ResolveBorderColor()
    {
        Color resolvedGlow = ResolveGlowColor();
        return Color.Lerp(borderColor, resolvedGlow, animateGlowPulse ? currentPulse : 0f);
    }

    private float ResolveGlowAlpha(float baseAlpha)
    {
        float pulseBoost = animateGlowPulse ? currentPulse * glowPulseAlphaBoost : 0f;
        return Mathf.Clamp01((baseAlpha + pulseBoost) * visualGlowIntensity);
    }

    private Material ResolveBorderMaterial()
    {
        return animateBorderColorFlow ? borderFlowMaterial : null;
    }

    private Material ResolveGlowMaterial()
    {
        return animateBorderColorFlow ? glowFlowMaterial : null;
    }

    private void ConfigureFlowMaterials()
    {
        if (!animateBorderColorFlow)
        {
            if (border != null)
            {
                border.material = null;
            }

            if (glowBack01 != null)
            {
                glowBack01.material = null;
            }

            if (glowBack02 != null)
            {
                glowBack02.material = null;
            }

            return;
        }

        Shader shader = Shader.Find("RedeLab/UI/Animated Border Flow");
        if (shader == null)
        {
            return;
        }

        if (borderFlowMaterial == null || borderFlowMaterial.shader != shader)
        {
            borderFlowMaterial = new Material(shader)
            {
                name = "Router Border Flow Runtime",
                hideFlags = HideFlags.DontSave
            };
        }

        if (glowFlowMaterial == null || glowFlowMaterial.shader != shader)
        {
            glowFlowMaterial = new Material(shader)
            {
                name = "Router Glow Flow Runtime",
                hideFlags = HideFlags.DontSave
            };
        }

        if (border != null)
        {
            if (useProceduralSquareFlowBorder)
            {
                border.sprite = null;
            }

            border.material = borderFlowMaterial;
        }

        if (glowBack01 != null)
        {
            if (useProceduralSquareFlowBorder)
            {
                glowBack01.sprite = null;
            }

            glowBack01.material = glowFlowMaterial;
        }

        if (glowBack02 != null)
        {
            if (useProceduralSquareFlowBorder)
            {
                glowBack02.sprite = null;
            }

            glowBack02.material = glowFlowMaterial;
        }

        UpdateFlowMaterials();
    }

    private void UpdateFlowMaterials()
    {
        if (!animateBorderColorFlow)
        {
            return;
        }

        ConfigureFlowMaterial(borderFlowMaterial, borderFlowAlpha);
        ConfigureFlowMaterial(glowFlowMaterial, Mathf.Clamp01(borderFlowAlpha * 0.55f));
    }

    private void ConfigureFlowMaterial(Material material, float alpha)
    {
        if (material == null)
        {
            return;
        }

        material.SetFloat("_FlowSpeed", borderFlowSpeed);
        material.SetFloat("_ColorSpread", borderVisibleColorSpread);
        material.SetFloat("_BorderWidth", borderFlowWidth);
        material.SetFloat("_EdgeSoftness", borderFlowSoftness);
        material.SetFloat("_AspectRatio", ResolvePanelAspectRatio());
        material.SetFloat("_Saturation", borderFlowSaturation);
        material.SetFloat("_Value", borderFlowBrightness);
        material.SetFloat("_Alpha", alpha);
    }

    private float ResolvePanelAspectRatio()
    {
        Vector2 size = panelRoot != null ? panelRoot.rect.size : panelSize;
        if (size.y <= 0.001f)
        {
            return 1f;
        }

        return Mathf.Max(0.001f, size.x / size.y);
    }

    private static void Stretch(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
