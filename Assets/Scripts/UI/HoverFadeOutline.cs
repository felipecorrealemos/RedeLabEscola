using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class HoverFadeOutline : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image targetImage;
    [SerializeField] private Color outlineColor = new Color(0.05f, 0.05f, 0.05f, 1f);
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.16f;

    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private bool isHovered;
    private float currentAlpha;

    private void Awake()
    {
        rectTransform = transform as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
        if (targetImage == null)
        {
            targetImage = GetComponent<Image>();
        }

        isHovered = IsPointerInside();
        ApplyAlpha(isHovered ? outlineColor.a : 0f);
    }

    private void OnDisable()
    {
        isHovered = false;
        ApplyAlpha(0f);
    }

    private void Update()
    {
        isHovered = IsPointerInside();
        float targetAlpha = isHovered ? outlineColor.a : 0f;
        if (Mathf.Approximately(currentAlpha, targetAlpha))
        {
            return;
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(fadeDuration, 0.01f);
        ApplyAlpha(Mathf.MoveTowards(currentAlpha, targetAlpha, step));
    }

    public void Configure(Image image, Color color, float duration)
    {
        rectTransform = transform as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
        targetImage = image != null ? image : GetComponent<Image>();
        outlineColor = color;
        fadeDuration = Mathf.Max(duration, 0.01f);
        isHovered = IsPointerInside();
        ApplyAlpha(isHovered ? outlineColor.a : 0f);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
    }

    private void ApplyAlpha(float alpha)
    {
        currentAlpha = Mathf.Clamp01(alpha);
        if (targetImage == null)
        {
            return;
        }

        Color color = outlineColor;
        color.a = currentAlpha;
        targetImage.color = color;
    }

    private bool IsPointerInside()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (rectTransform == null)
        {
            return isHovered;
        }

        Camera eventCamera = null;
        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>();
        }

        if (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = parentCanvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, Input.mousePosition, eventCamera);
    }
}
