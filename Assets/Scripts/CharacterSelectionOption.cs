using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionOption : MonoBehaviour
{
    [SerializeField] private CharacterSelectionChoice choice = CharacterSelectionChoice.None;
    [SerializeField] private CharacterSelectionController controller;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float hoverScale = 1.06f;
    [SerializeField] private float scaleTransitionSpeed = 8f;
    [SerializeField] private Light highlightLight;
    [SerializeField] private Image selectionFrame;
    [SerializeField] private Color frameNormalColor = new Color(0.93f, 0.96f, 0.94f, 0.25f);
    [SerializeField] private Color frameHighlightedColor = new Color(0.93f, 0.96f, 0.94f, 0.75f);
    [SerializeField] private Color frameSelectedColor = new Color(0.58f, 0.92f, 0.72f, 0.95f);

    private Vector3 originalScale;
    private float originalLightIntensity;
    private bool selected;
    private bool hovered;

    public CharacterSelectionChoice Choice => choice;

    public void Configure(CharacterSelectionChoice newChoice, CharacterSelectionController newController, Transform newVisualRoot, Light newHighlightLight, Image newSelectionFrame = null)
    {
        choice = newChoice;
        controller = newController;
        visualRoot = newVisualRoot;
        highlightLight = newHighlightLight;
        selectionFrame = newSelectionFrame;
        originalScale = visualRoot != null ? visualRoot.localScale : Vector3.one;
        originalLightIntensity = highlightLight != null ? Mathf.Max(highlightLight.intensity, 0.01f) : 0f;
        ApplyHighlightImmediate();
    }

    public void SetSelectionFrame(Image newSelectionFrame)
    {
        selectionFrame = newSelectionFrame;
        ApplyHighlightImmediate();
    }

    private void Awake()
    {
        if (visualRoot == null)
        {
            visualRoot = transform;
        }

        originalScale = visualRoot.localScale;
        originalLightIntensity = highlightLight != null ? Mathf.Max(highlightLight.intensity, 0.01f) : 0f;
        ApplyHighlightImmediate();
    }

    private void Update()
    {
        ApplyHighlightSmooth();
    }

    private void OnMouseEnter()
    {
        hovered = true;
    }

    private void OnMouseExit()
    {
        hovered = false;
    }

    private void OnMouseDown()
    {
        Select();
    }

    public void Select()
    {
        controller?.Select(choice);
    }

    public void SetSelected(bool isSelected)
    {
        selected = isSelected;
    }

    private void ApplyHighlightImmediate()
    {
        bool highlighted = hovered || selected;

        if (visualRoot != null)
        {
            visualRoot.localScale = highlighted ? originalScale * hoverScale : originalScale;
        }

        if (highlightLight != null)
        {
            highlightLight.intensity = highlighted ? originalLightIntensity : 0f;
            highlightLight.enabled = highlighted;
        }

        if (selectionFrame != null)
        {
            selectionFrame.color = GetTargetFrameColor();
        }
    }

    private void ApplyHighlightSmooth()
    {
        bool highlighted = hovered || selected;
        float t = 1f - Mathf.Exp(-scaleTransitionSpeed * Time.deltaTime);

        if (visualRoot != null)
        {
            Vector3 targetScale = highlighted ? originalScale * hoverScale : originalScale;
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, targetScale, t);
        }

        if (highlightLight != null)
        {
            float targetIntensity = highlighted ? originalLightIntensity : 0f;
            highlightLight.intensity = Mathf.Lerp(highlightLight.intensity, targetIntensity, t);
            highlightLight.enabled = highlightLight.intensity > 0.01f || highlighted;
        }

        if (selectionFrame != null)
        {
            selectionFrame.color = Color.Lerp(selectionFrame.color, GetTargetFrameColor(), t);
        }
    }

    private Color GetTargetFrameColor()
    {
        if (selected)
        {
            return frameSelectedColor;
        }

        return hovered ? frameHighlightedColor : frameNormalColor;
    }
}
