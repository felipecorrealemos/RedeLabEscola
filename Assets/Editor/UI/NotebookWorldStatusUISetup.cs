using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class NotebookWorldStatusUISetup
{
    private const string MenuPath = "Tools/RedeLab/Setup Notebook World Status UI";
    private const string WifiOffSpritePath = "Assets/Imagens/Wifi/wifi desligado.png";
    private const string WifiSearchingSpritePath = "Assets/Imagens/Wifi/wifi procurando rede.png";
    private const string WifiConnectedSpritePath = "Assets/Imagens/Wifi/wifi conectado.png";
    private const string FrameSpritePath = "Assets/Imagens/Molduras/moldura.png";

    [MenuItem(MenuPath, true)]
    private static bool ValidateSetupNotebookWorldStatusUI()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem(MenuPath)]
    private static void SetupNotebookWorldStatusUI()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Notebook World Status UI", "Selecione o Notebook na Hierarchy ou no Prefab antes de executar o setup.", "OK");
            return;
        }

        GameObject notebook = ResolveNotebookRoot(selected);
        if (notebook == null)
        {
            EditorUtility.DisplayDialog("Notebook World Status UI", "O objeto selecionado não parece conter ComputerInteractable/WiFiDevice de notebook.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Setup Notebook World Status UI");
        int undoGroup = Undo.GetCurrentGroup();

        SetupOnNotebook(notebook);

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = notebook;
        EditorUtility.DisplayDialog("Notebook World Status UI", "Estrutura criada/atualizada no Notebook selecionado.", "OK");
    }

    private static GameObject ResolveNotebookRoot(GameObject selected)
    {
        ComputerInteractable computer = selected.GetComponentInParent<ComputerInteractable>();
        if (computer == null)
        {
            computer = selected.GetComponentInChildren<ComputerInteractable>(true);
        }

        WiFiDevice wifi = computer != null ? computer.GetComponent<WiFiDevice>() : selected.GetComponentInParent<WiFiDevice>();
        if (computer != null && wifi != null && wifi.DeviceType == WiFiDeviceType.Notebook)
        {
            return computer.gameObject;
        }

        string lowerName = selected.name.ToLowerInvariant();
        if (lowerName.Contains("notebook"))
        {
            return selected;
        }

        return null;
    }

    private static void SetupOnNotebook(GameObject notebook)
    {
        ComputerInteractable computer = notebook.GetComponent<ComputerInteractable>();
        WiFiDevice wifi = notebook.GetComponent<WiFiDevice>();
        NotebookWorldStatusUI statusUI = notebook.GetComponent<NotebookWorldStatusUI>();
        if (statusUI == null)
        {
            statusUI = Undo.AddComponent<NotebookWorldStatusUI>(notebook);
        }

        Transform anchor = GetOrCreateChildTransform(notebook.transform, "WorldStatusAnchor");
        if (WasJustCreated(anchor))
        {
            anchor.localPosition = new Vector3(0f, 0.85f, 0f);
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;
        }

        Canvas canvas = GetOrCreateCanvas(anchor, "NotebookWorldStatusCanvas");
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (WasJustCreated(canvas.transform))
        {
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.sizeDelta = new Vector2(280f, 112f);
        }
        canvasRect.localScale = Vector3.one;

        CanvasScaler scaler = GetOrAddComponent<CanvasScaler>(canvas.gameObject);
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;

        CanvasGroup canvasGroup = GetOrAddComponent<CanvasGroup>(canvas.gameObject);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        RectTransform panel = GetOrCreateRectTransform(canvas.transform, "StatusPanel");
        ForceConfigureRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(74f, 74f));

        Image background = GetOrCreateImage(panel, "Background");
        RectTransform backgroundRect = background.rectTransform;
        StretchInset(backgroundRect, 10f, 10f, 10f, 10f);
        background.color = new Color(0.03f, 0.035f, 0.04f, 0.62f);
        Outline backgroundOutline = GetOrAddComponent<Outline>(background.gameObject);
        backgroundOutline.enabled = false;

        Image border = GetOrCreateImage(panel, "Border");
        RectTransform borderRect = border.rectTransform;
        StretchInset(borderRect, -10f, -10f, -10f, -10f);
        Sprite frameSprite = LoadSprite(FrameSpritePath);
        border.sprite = frameSprite;
        border.color = frameSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
        border.preserveAspect = false;

        RectTransform iconContainerRect = GetOrCreateRectTransform(panel, "IconContainer");
        ForceConfigureRect(iconContainerRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));

        Sprite offSprite = LoadSprite(WifiOffSpritePath);
        Sprite searchingSprite = LoadSprite(WifiSearchingSpritePath);
        Sprite connectedSprite = LoadSprite(WifiConnectedSpritePath);

        Image iconOffImage = CreateIcon(iconContainerRect, "Icon_WifiOff", offSprite, new Color(0.8f, 0.22f, 0.2f, 1f));
        Image iconSearchingImage = CreateIcon(iconContainerRect, "Icon_WifiSearching", searchingSprite, new Color(1f, 0.76f, 0.18f, 1f));
        Image iconConnectedImage = CreateIcon(iconContainerRect, "Icon_WifiConnected", connectedSprite, new Color(0.16f, 0.72f, 0.34f, 1f));

        TMP_Text titleText = GetOrCreateText(panel, "TitleText", "NOTEBOOK", 18f, FontStyles.Bold);
        RectTransform titleRect = titleText.rectTransform;
        ForceConfigureRect(titleRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(58f, -10f), new Vector2(-70f, 20f));
        titleText.gameObject.SetActive(false);

        TMP_Text statusText = GetOrCreateText(panel, "StatusText", "Wi-Fi desligado", 16f, FontStyles.Bold);
        RectTransform statusRect = statusText.rectTransform;
        ForceConfigureRect(statusRect, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(58f, 0f), new Vector2(-82f, 0f));
        statusText.alignment = TextAlignmentOptions.MidlineLeft;
        statusText.enableWordWrapping = false;
        statusText.enableAutoSizing = true;
        statusText.fontSizeMin = 11f;
        statusText.fontSizeMax = 16f;
        statusText.gameObject.SetActive(false);

        TMP_Text pointer = GetOrCreateText(panel, "Pointer", "v", 12f, FontStyles.Bold);
        RectTransform pointerRect = pointer.rectTransform;
        ForceConfigureRect(pointerRect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(20f, 14f));
        pointer.alignment = TextAlignmentOptions.Center;
        pointer.color = new Color(0.16f, 0.72f, 0.34f, 1f);
        pointer.gameObject.SetActive(false);

        statusUI.AssignReferences(
            anchor,
            panel,
            canvasGroup,
            background,
            border,
            titleText,
            statusText,
            iconOffImage.gameObject,
            iconSearchingImage.gameObject,
            iconConnectedImage.gameObject,
            iconOffImage,
            iconSearchingImage,
            iconConnectedImage,
            computer,
            wifi);
        statusUI.ConfigureBillboardDefaults(true, false);
        statusUI.ConfigurePresentationDefaults(NotebookWorldStatusUI.PresentationMode.ScreenSpaceProjected, new Vector2(0f, 48f));
        statusUI.ConfigureTimingDefaults(8f, 0.22f);
        statusUI.ConfigureSpriteDefaults(offSprite, searchingSprite, connectedSprite, true);

        SetIconState(iconOffImage.gameObject, false);
        SetIconState(iconSearchingImage.gameObject, false);
        SetIconState(iconConnectedImage.gameObject, true);

        MarkDirty(notebook);
    }

    private static Canvas GetOrCreateCanvas(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject canvasObject = existing != null ? existing.gameObject : CreateChild(parent, name, typeof(RectTransform));
        Canvas canvas = GetOrAddComponent<Canvas>(canvasObject);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvas.pixelPerfect = false;
        return canvas;
    }

    private static Image CreateIcon(RectTransform parent, string name, Sprite sprite, Color color)
    {
        RectTransform iconRoot = GetOrCreateRectTransform(parent, name);
        ForceConfigureRect(iconRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));

        Image iconImage = GetOrAddComponent<Image>(iconRoot.gameObject);
        iconImage.sprite = sprite;
        iconImage.color = sprite != null ? Color.white : new Color(color.r, color.g, color.b, 0.16f);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        TMP_Text glyphText = GetOrCreateText(iconRoot, "Glyph", string.Empty, 17f, FontStyles.Bold);
        Stretch(glyphText.rectTransform);
        glyphText.alignment = TextAlignmentOptions.Center;
        glyphText.color = color;
        glyphText.gameObject.SetActive(false);

        return iconImage;
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static TMP_Text GetOrCreateText(Transform parent, string name, string value, float fontSize, FontStyles fontStyle)
    {
        RectTransform rect = GetOrCreateRectTransform(parent, name);
        TMP_Text text = GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        return text;
    }

    private static Image GetOrCreateImage(Transform parent, string name)
    {
        RectTransform rect = GetOrCreateRectTransform(parent, name);
        Image image = GetOrAddComponent<Image>(rect.gameObject);
        image.raycastTarget = false;
        return image;
    }

    private static Transform GetOrCreateChildTransform(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        return existing != null ? existing : CreateChild(parent, name).transform;
    }

    private static RectTransform GetOrCreateRectTransform(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        GameObject child = existing != null ? existing.gameObject : CreateChild(parent, name, typeof(RectTransform));
        return EnsureRectTransform(child);
    }

    private static RectTransform EnsureRectTransform(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect != null)
        {
            return rect;
        }

        Debug.LogWarning("NotebookWorldStatusUISetup found an existing UI object without RectTransform: " + target.name + ". Recreate that child or remove it before running the setup again.", target);
        return null;
    }

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        child.transform.SetParent(parent, false);
        JustCreatedMarker.Mark(child.transform);
        return child;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(target);
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null || (!WasJustCreated(rect) && rect.sizeDelta != Vector2.zero))
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void ForceConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
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

    private static void StretchInset(RectTransform rect, float left, float right, float top, float bottom)
    {
        if (rect == null)
        {
            return;
        }

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void SetIconState(GameObject iconObject, bool active)
    {
        if (iconObject != null)
        {
            iconObject.SetActive(active);
        }
    }

    private static bool WasJustCreated(Component component)
    {
        return component != null && JustCreatedMarker.WasMarked(component.transform);
    }

    private static bool WasJustCreated(Transform transform)
    {
        return transform != null && JustCreatedMarker.WasMarked(transform);
    }

    private static void MarkDirty(GameObject notebook)
    {
        EditorUtility.SetDirty(notebook);
        PrefabUtility.RecordPrefabInstancePropertyModifications(notebook);
        if (!EditorUtility.IsPersistent(notebook))
        {
            EditorSceneManager.MarkSceneDirty(notebook.scene);
        }
    }

    private static class JustCreatedMarker
    {
        private static readonly System.Collections.Generic.HashSet<int> CreatedIds = new System.Collections.Generic.HashSet<int>();

        public static void Mark(Transform transform)
        {
            if (transform != null)
            {
                CreatedIds.Add(transform.GetInstanceID());
            }
        }

        public static bool WasMarked(Transform transform)
        {
            return transform != null && CreatedIds.Contains(transform.GetInstanceID());
        }
    }
}
