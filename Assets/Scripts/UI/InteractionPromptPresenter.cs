using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct InteractionPromptAction
{
    public readonly string Key;
    public readonly string Label;
    public readonly bool Visible;
    public readonly bool Enabled;

    public InteractionPromptAction(string key, string label, bool visible = true, bool enabled = true)
    {
        Key = key;
        Label = label;
        Visible = visible;
        Enabled = enabled;
    }
}

/// <summary>
/// Reusable, non-interactive HUD prompt. The visual hierarchy is generated at runtime so
/// scenes that only contain the shared InteractionCanvas do not need a prompt prefab.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractionPromptPresenter : MonoBehaviour
{
    private static Sprite roundedSprite;

    private readonly List<GameObject> actionSlots = new List<GameObject>(4);
    private TMP_Text titleLabel;
    private Transform actionsContainer;
    private Object currentOwner;
    private int currentPriority;

    public static InteractionPromptPresenter GetOrCreate(Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        InteractionPromptPresenter presenter = canvas.GetComponentInChildren<InteractionPromptPresenter>(true);
        if (presenter != null)
        {
            presenter.gameObject.name = "InteractionPromptPanel";
            presenter.EnsureBuilt();
            RemoveLegacyPromptObjects(canvas, presenter.gameObject);
            return presenter;
        }

        GameObject panel = new GameObject("InteractionPromptPanel", typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(canvas.transform, false);
        presenter = panel.AddComponent<InteractionPromptPresenter>();
        presenter.EnsureBuilt();
        RemoveLegacyPromptObjects(canvas, panel);
        panel.SetActive(false);
        return presenter;
    }

    private static void RemoveLegacyPromptObjects(Canvas canvas, GameObject sharedPanel)
    {
        string[] legacyNames =
        {
            "ComputerInteractionPrompt",
            "RouterInteractionPrompt",
            "PrintedDocumentPrompt",
            "ProfessorDocumentPrompt",
            "DropZonePrompt",
            "EmpilhadeiraPrompt",
            "ScrapCranePrompt"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            Transform legacy = canvas.transform.Find(legacyNames[i]);
            if (legacy != null && legacy.gameObject != sharedPanel)
            {
                legacy.gameObject.SetActive(false);
                DestroyImmediate(legacy.gameObject);
            }
        }
    }

    public void EnsureBuilt()
    {
        if (titleLabel != null)
        {
            return;
        }

        TMP_Text existingTitle = transform.Find("Background/Content/Header/Title")?.GetComponent<TMP_Text>();
        if (existingTitle != null)
        {
            CacheReferences(existingTitle);
            return;
        }

        ClearLegacyVisuals();
        Build();
    }

    public void Show(Object owner, string title, params InteractionPromptAction[] actions)
    {
        ShowInternal(owner, title, 10, actions);
    }

    public void ShowAmbient(Object owner, string title, params InteractionPromptAction[] actions)
    {
        ShowInternal(owner, title, 0, actions);
    }

    public void Refresh(Object owner, string title, params InteractionPromptAction[] actions)
    {
        if (owner != null && currentOwner == owner && gameObject.activeSelf)
        {
            ShowInternal(owner, title, currentPriority, actions);
        }
    }

    private void ShowInternal(Object owner, string title, int priority, InteractionPromptAction[] actions)
    {
        if (owner == null)
        {
            return;
        }

        EnsureBuilt();
        if (currentOwner != null && currentOwner != owner && currentPriority > priority && gameObject.activeSelf)
        {
            return;
        }

        currentOwner = owner;
        currentPriority = priority;
        titleLabel.text = string.IsNullOrWhiteSpace(title) ? "NOTEBOOK" : title.ToUpperInvariant();
        UpdateHeaderWidths();
        int actionCount = actions != null ? actions.Length : 0;
        int visibleActionCount = 0;
        for (int i = 0; i < actionCount; i++)
        {
            if (actions[i].Visible)
            {
                visibleActionCount++;
            }
        }

        EnsureActionSlotCount(visibleActionCount);
        int visibleActionIndex = 0;
        for (int i = 0; i < actionSlots.Count; i++)
        {
            GameObject slot = actionSlots[i];
            while (visibleActionIndex < actionCount && !actions[visibleActionIndex].Visible)
            {
                visibleActionIndex++;
            }

            bool used = i < visibleActionCount && visibleActionIndex < actionCount;
            if (used)
            {
                slot.SetActive(true);
                ApplyAction(slot, actions[visibleActionIndex]);
                visibleActionIndex++;
            }
            else
            {
                slot.SetActive(false);
            }
        }

        RectTransform root = GetComponent<RectTransform>();
        if (root != null)
        {
            root.sizeDelta = new Vector2(410f, 72f + Mathf.Max(visibleActionCount, 1) * 34f);
        }

        LayoutElement actionsElement = actionsContainer != null ? actionsContainer.GetComponent<LayoutElement>() : null;
        if (actionsElement != null)
        {
            actionsElement.preferredHeight = Mathf.Max(visibleActionCount, 1) * 32f + Mathf.Max(visibleActionCount - 1, 0) * 7f;
        }

        gameObject.SetActive(visibleActionCount > 0);
    }

    public void Hide(Object owner)
    {
        if (owner != null && currentOwner != null && currentOwner != owner)
        {
            return;
        }

        currentOwner = null;
        currentPriority = 0;
        gameObject.SetActive(false);
    }

    private void Build()
    {
        RectTransform root = GetOrAddRectTransform(gameObject);
        root.anchorMin = new Vector2(0.5f, 0f);
        root.anchorMax = new Vector2(0.5f, 0f);
        root.pivot = new Vector2(0.5f, 0f);
        root.anchoredPosition = new Vector2(0f, 34f);
        root.sizeDelta = new Vector2(410f, 174f);

        CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Image border = gameObject.GetComponent<Image>();
        if (border == null)
        {
            border = gameObject.AddComponent<Image>();
        }
        ConfigureSlicedImage(border, new Color(0.82f, 0.9f, 0.94f, 0.82f));
        border.raycastTarget = false;

        GameObject backgroundObject = CreateUiObject("Background", transform);
        RectTransform backgroundRect = GetOrAddRectTransform(backgroundObject);
        Stretch(backgroundRect, 1.5f);
        Image background = backgroundObject.AddComponent<Image>();
        ConfigureSlicedImage(background, new Color(0.035f, 0.045f, 0.055f, 0.84f));
        background.raycastTarget = false;

        GameObject content = CreateUiObject("Content", backgroundObject.transform);
        RectTransform contentRect = GetOrAddRectTransform(content);
        Stretch(contentRect, 0f);
        VerticalLayoutGroup vertical = content.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(24, 24, 15, 14);
        vertical.spacing = 12f;
        vertical.childAlignment = TextAnchor.UpperCenter;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;

        BuildHeader(content.transform);

        GameObject actions = CreateUiObject("Actions", content.transform);
        actionsContainer = actions.transform;
        actions.AddComponent<LayoutElement>().preferredHeight = 104f;
        VerticalLayoutGroup actionsLayout = actions.AddComponent<VerticalLayoutGroup>();
        actionsLayout.padding = new RectOffset(-200, 0, 0, 0);
        actionsLayout.spacing = 7f;
        actionsLayout.childAlignment = TextAnchor.MiddleCenter;
        actionsLayout.childControlWidth = false;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = false;

        // Visible actions are compacted into these reusable rows. This keeps a single
        // action centered and divides the available height evenly as rows are added.
        actionSlots.Add(BuildAction(actions.transform, "Action_1", "E", "Pegar", 224f, 76f));
        actionSlots.Add(BuildAction(actions.transform, "Action_2", "F", "Configurar", 224f, 76f));
        actionSlots.Add(BuildAction(actions.transform, "Action_3", "ENTER", "Usar", 224f, 76f));
        actionSlots.Add(BuildAction(actions.transform, "Action_4", string.Empty, string.Empty, 224f, 48f));
    }

    private void BuildHeader(Transform parent)
    {
        GameObject header = CreateUiObject("Header", parent);
        header.AddComponent<LayoutElement>().preferredHeight = 28f;
        HorizontalLayoutGroup layout = header.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        BuildLine(header.transform, "LeftLine");
        BuildDot(header.transform, "LeftDot");
        titleLabel = BuildText(header.transform, "Title", "NOTEBOOK", 17f, FontStyles.Bold, TextAlignmentOptions.Center);
        titleLabel.characterSpacing = 3f;
        LayoutElement titleElement = titleLabel.gameObject.AddComponent<LayoutElement>();
        titleElement.preferredWidth = 132f;
        titleElement.preferredHeight = 28f;
        BuildDot(header.transform, "RightDot");
        BuildLine(header.transform, "RightLine");
    }

    private void UpdateHeaderWidths()
    {
        if (titleLabel == null)
        {
            return;
        }

        LayoutElement titleElement = titleLabel.GetComponent<LayoutElement>();
        LayoutElement leftLine = titleLabel.transform.parent?.Find("LeftLine")?.GetComponent<LayoutElement>();
        LayoutElement rightLine = titleLabel.transform.parent?.Find("RightLine")?.GetComponent<LayoutElement>();
        if (titleElement == null || leftLine == null || rightLine == null)
        {
            return;
        }

        // The usable header width is 362 px (410 px panel minus 24 px padding on
        // each side). Spacing and the two dots consume 44 px, leaving 318 px to
        // share between the title and both decorative lines.
        const float availableForTitleAndLines = 318f;
        float preferredTitleWidth = titleLabel.GetPreferredValues(titleLabel.text).x + 18f;
        float titleWidth = Mathf.Clamp(preferredTitleWidth, 132f, 240f);
        float lineWidth = Mathf.Max(28f, (availableForTitleAndLines - titleWidth) * 0.5f);

        titleElement.preferredWidth = titleWidth;
        leftLine.preferredWidth = lineWidth;
        rightLine.preferredWidth = lineWidth;
    }

    private static GameObject BuildAction(Transform parent, string name, string key, string label, float width, float keyWidth)
    {
        const float keyAreaWidth = 76f;
        const float keyToLabelSpacing = 12f;

        GameObject action = CreateUiObject(name, parent);
        LayoutElement actionElement = action.AddComponent<LayoutElement>();
        actionElement.preferredWidth = width;
        actionElement.preferredHeight = 32f;
        HorizontalLayoutGroup layout = action.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = keyToLabelSpacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        // The fixed KeyArea keeps every action label on the same horizontal axis while
        // allowing short keys and ENTER to retain their appropriate visual widths.
        GameObject keyAreaObject = CreateUiObject("KeyArea", action.transform);
        LayoutElement keyAreaElement = keyAreaObject.AddComponent<LayoutElement>();
        keyAreaElement.preferredWidth = keyAreaWidth;
        keyAreaElement.preferredHeight = 30f;
        HorizontalLayoutGroup keyAreaLayout = keyAreaObject.AddComponent<HorizontalLayoutGroup>();
        keyAreaLayout.childAlignment = TextAnchor.MiddleRight;
        keyAreaLayout.childControlWidth = true;
        keyAreaLayout.childControlHeight = true;
        keyAreaLayout.childForceExpandWidth = false;
        keyAreaLayout.childForceExpandHeight = false;

        GameObject keyBorderObject = CreateUiObject("KeyBackground", keyAreaObject.transform);
        LayoutElement keyElement = keyBorderObject.AddComponent<LayoutElement>();
        keyElement.preferredWidth = keyWidth;
        keyElement.preferredHeight = 30f;
        Image keyBorder = keyBorderObject.AddComponent<Image>();
        ConfigureSlicedImage(keyBorder, new Color(0.75f, 0.82f, 0.86f, 0.9f));
        keyBorder.raycastTarget = false;

        GameObject keyFillObject = CreateUiObject("KeyFill", keyBorderObject.transform);
        RectTransform keyFillRect = GetOrAddRectTransform(keyFillObject);
        Stretch(keyFillRect, 1.5f);
        Image keyFill = keyFillObject.AddComponent<Image>();
        ConfigureSlicedImage(keyFill, new Color(0.25f, 0.29f, 0.33f, 0.98f));
        keyFill.raycastTarget = false;

        TMP_Text keyText = BuildText(keyFillObject.transform, "KeyText", key, key == "ENTER" ? 12f : 15f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(keyText.rectTransform, 0f);

        TMP_Text actionText = BuildText(action.transform, "ActionText", label, 16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        LayoutElement actionTextElement = actionText.gameObject.AddComponent<LayoutElement>();
        actionTextElement.preferredWidth = width - keyAreaWidth - keyToLabelSpacing;
        actionTextElement.preferredHeight = 30f;
        return action;
    }

    private static void BuildLine(Transform parent, string name)
    {
        GameObject line = CreateUiObject(name, parent);
        LayoutElement element = line.AddComponent<LayoutElement>();
        element.preferredWidth = 92f;
        element.preferredHeight = 2f;
        Image image = line.AddComponent<Image>();
        image.color = new Color(0.12f, 0.63f, 0.82f, 0.8f);
        image.raycastTarget = false;
    }

    private static void BuildDot(Transform parent, string name)
    {
        GameObject dot = CreateUiObject(name, parent);
        LayoutElement element = dot.AddComponent<LayoutElement>();
        element.preferredWidth = 6f;
        element.preferredHeight = 6f;
        Image image = dot.AddComponent<Image>();
        image.sprite = GetRoundedSprite();
        image.color = new Color(0.18f, 0.72f, 0.9f, 0.95f);
        image.raycastTarget = false;
    }

    private static TMP_Text BuildText(Transform parent, string name, string value, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = CreateUiObject(name, parent);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    private void CacheReferences(TMP_Text existingTitle)
    {
        titleLabel = existingTitle;
        actionSlots.Clear();
        Transform actions = transform.Find("Background/Content/Actions");
        actionsContainer = actions;
        if (actions != null)
        {
            for (int i = 0; i < actions.childCount; i++)
            {
                if (actions.GetChild(i).name.StartsWith("Action_"))
                {
                    actionSlots.Add(actions.GetChild(i).gameObject);
                }
            }
        }
    }

    private void EnsureActionSlotCount(int requiredCount)
    {
        if (actionsContainer == null)
        {
            actionsContainer = transform.Find("Background/Content/Actions");
        }

        while (actionsContainer != null && actionSlots.Count < requiredCount)
        {
            int index = actionSlots.Count + 1;
            actionSlots.Add(BuildAction(actionsContainer, "Action_" + index, string.Empty, string.Empty, 224f, 48f));
        }
    }

    private static void ApplyAction(GameObject slot, InteractionPromptAction action)
    {
        TMP_Text keyText = slot.transform.Find("KeyArea/KeyBackground/KeyFill/KeyText")?.GetComponent<TMP_Text>();
        TMP_Text actionText = slot.transform.Find("ActionText")?.GetComponent<TMP_Text>();
        LayoutElement keyElement = slot.transform.Find("KeyArea/KeyBackground")?.GetComponent<LayoutElement>();
        if (keyText != null)
        {
            keyText.text = action.Key ?? string.Empty;
            keyText.fontSize = string.Equals(action.Key, "ENTER", System.StringComparison.OrdinalIgnoreCase) ? 12f : 15f;
        }
        if (actionText != null)
        {
            actionText.text = action.Label ?? string.Empty;
            actionText.fontStyle = action.Enabled ? FontStyles.Bold : FontStyles.Bold | FontStyles.Strikethrough;
            actionText.color = action.Enabled ? Color.white : new Color(0.72f, 0.75f, 0.77f, 1f);
        }
        if (keyElement != null)
        {
            keyElement.preferredWidth = 76f;
        }

        SetSlotVisible(slot, action.Visible, action.Enabled);
    }

    private static void SetSlotVisible(GameObject slot, bool visible, bool enabled)
    {
        if (slot == null)
        {
            return;
        }

        slot.SetActive(visible);
        CanvasGroup group = slot.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = slot.AddComponent<CanvasGroup>();
        }

        group.alpha = visible ? (enabled ? 1f : 0.48f) : 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void ClearLegacyVisuals()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }

        Text legacyText = GetComponent<Text>();
        if (legacyText != null)
        {
            DestroyImmediate(legacyText);
        }
    }

    private static void ConfigureSlicedImage(Image image, Color color)
    {
        image.sprite = GetRoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
    }

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null)
        {
            return roundedSprite;
        }

        const int size = 32;
        const float radius = 8f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "InteractionPrompt_Rounded9Slice",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - 0.5f, 0f, x + 0.5f - (size - radius));
                float dy = Mathf.Max(radius - y - 0.5f, 0f, y + 0.5f - (size - radius));
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        roundedSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        roundedSprite.name = "InteractionPrompt_Rounded9Slice";
        roundedSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedSprite;
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        result.transform.SetParent(parent, false);
        return result;
    }

    private static RectTransform GetOrAddRectTransform(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = target.AddComponent<RectTransform>();
        }

        return rect;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
