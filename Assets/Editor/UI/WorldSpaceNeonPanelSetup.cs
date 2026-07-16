using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WorldSpaceNeonPanelSetup
{
    private const string MenuPath = "Tools/RedeLab/Create Control Room Router Neon Panel";
    private const string Stage2ScenePath = "Assets/Scenes/Stage2/Stage2_Factory.unity";
    private const string FrameSpritePath = "Assets/Imagens/Molduras/moldura3.png";
    private const string RootName = "RouterWorldStatusAnchor";
    private const string LegacyRootName = "WorldPanel_Test";
    private const string LegacyControlRoomRootName = "RouterWorldPanel_ControlRoom";
    private const string CanvasName = "Canvas_WorldSpace";
    private const string PanelRootName = "PanelRoot";
    private const string TitleTextValue = "ROUTER";
    private const string SubtitleTextValue = "Control room network";

    [InitializeOnLoadMethod]
    private static void AutoCreateInOpenStage2Scene()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || activeScene.path != Stage2ScenePath)
            {
                return;
            }

            if (FindExistingPanel() != null)
            {
                return;
            }

            CreatePanelInScene(activeScene, true);
        };
    }

    [MenuItem(MenuPath)]
    public static void CreatePanelFromMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            scene = EditorSceneManager.OpenScene(Stage2ScenePath, OpenSceneMode.Single);
        }

        CreatePanelInScene(scene, false);
    }

    public static void CreatePanelInStage2Scene()
    {
        Scene scene = EditorSceneManager.OpenScene(Stage2ScenePath, OpenSceneMode.Single);
        CreatePanelInScene(scene, true);
    }

    private static void CreatePanelInScene(Scene scene, bool saveScene)
    {
        GameObject existing = FindExistingPanel();
        if (existing != null)
        {
            ConfigureExistingPanel(existing);
            CleanupDuplicatePanels(existing);
            EditorUtility.SetDirty(existing);
            EditorSceneManager.MarkSceneDirty(scene);
            if (saveScene)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Selection.activeGameObject = existing;
            return;
        }

        Undo.SetCurrentGroupName("Create World Space Neon Panel Test");
        int undoGroup = Undo.GetCurrentGroup();

        Sprite frameSprite = LoadSprite(FrameSpritePath);

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create " + RootName);
        SceneManager.MoveGameObjectToScene(root, scene);
        Transform target = FindLikelyTarget();
        if (target != null)
        {
            root.transform.SetParent(target, false);
            root.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        }
        else
        {
            root.transform.position = ResolveInitialPanelPosition();
        }

        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        GameObject canvasObject = CreateChild(root.transform, CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(420f, 180f);
        canvasRect.localPosition = Vector3.zero;
        canvasRect.localRotation = Quaternion.identity;
        canvasRect.localScale = Vector3.one;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        canvas.pixelPerfect = false;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 12f;
        scaler.referencePixelsPerUnit = 100f;

        CanvasGroup group = canvasObject.GetComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform panelRoot = CreateRect(canvasObject.transform, PanelRootName);
        ConfigureRect(panelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(360f, 132f));

        Image glowBack01 = CreateLayerImage(panelRoot, "Glow_Back_01", frameSprite, new Color(0.1f, 1f, 0.45f, 0.18f));
        glowBack01.rectTransform.localScale = Vector3.one * 1.2f;

        Image glowBack02 = CreateLayerImage(panelRoot, "Glow_Back_02", frameSprite, new Color(0.1f, 1f, 0.45f, 0.36f));
        glowBack02.rectTransform.localScale = Vector3.one * 1.08f;

        Image background = CreateLayerImage(panelRoot, "Background", null, new Color(0.005f, 0.035f, 0.025f, 0.92f));
        StretchInset(background.rectTransform, 18f, 18f, 16f, 16f);

        Image border = CreateLayerImage(panelRoot, "Border", frameSprite, new Color(0.1f, 1f, 0.45f, 1f));

        TMP_Text titleText = CreateText(panelRoot, "TitleText", TitleTextValue, 38f, FontStyles.Bold, new Color(0.16f, 1f, 0.52f, 1f));
        ConfigureRect(titleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(-56f, -42f));
        titleText.alignment = TextAlignmentOptions.Center;

        TMP_Text subtitleText = CreateText(panelRoot, "SubtitleText", SubtitleTextValue, 15f, FontStyles.Normal, new Color(0.78f, 1f, 0.88f, 1f));
        ConfigureRect(subtitleText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(-72f, -42f));
        subtitleText.alignment = TextAlignmentOptions.Center;

        Image pointerTail = CreateLayerImage(panelRoot, "PointerTail", null, new Color(0.1f, 1f, 0.45f, 0.8f));
        ConfigureRect(pointerTail.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(56f, 8f));
        pointerTail.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);

        AddTextGlow(titleText.gameObject, new Color(0.1f, 1f, 0.45f, 0.55f), new Vector2(0f, -2f));
        AddTextGlow(subtitleText.gameObject, new Color(0.1f, 1f, 0.45f, 0.28f), new Vector2(0f, -1f));

        WorldSpaceNeonPanelUI panelUI = Undo.AddComponent<WorldSpaceNeonPanelUI>(root);
        panelUI.AssignReferences(canvas, group, panelRoot, glowBack01, glowBack02, background, border, titleText, subtitleText, pointerTail);
        panelUI.ConfigureTextDefaults(TitleTextValue, SubtitleTextValue);
        panelUI.ConfigureBehaviorDefaults(true, 2.4f, 0.22f, true);
        panelUI.ConfigurePresentationDefaults(WorldSpaceNeonPanelUI.PresentationMode.ScreenSpaceProjected, root.transform, new Vector2(0f, 64f));
        panelUI.ConfigureBillboardDefaults(true, true);

        Undo.CollapseUndoOperations(undoGroup);
        CleanupDuplicatePanels(root);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        if (saveScene)
        {
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static Vector3 ResolveInitialPanelPosition()
    {
        Transform target = FindLikelyTarget();
        if (target != null)
        {
            return target.position + new Vector3(0f, 1.35f, -0.55f);
        }

        return new Vector3(0f, 1.7f, 0f);
    }

    private static Transform FindLikelyTarget()
    {
        Transform controlRoom = FindTransformByName("ControlRoom");
        RouterInteractable[] routers = Object.FindObjectsOfType<RouterInteractable>(true);
        if (routers.Length > 0)
        {
            RouterInteractable closestRouter = null;
            float closestDistance = float.PositiveInfinity;
            Vector3 referencePosition = controlRoom != null ? controlRoom.position : Vector3.zero;
            for (int i = 0; i < routers.Length; i++)
            {
                RouterInteractable router = routers[i];
                if (router == null)
                {
                    continue;
                }

                float distance = Vector3.SqrMagnitude(router.transform.position - referencePosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRouter = router;
                }
            }

            if (closestRouter != null)
            {
                return closestRouter.transform;
            }
        }

        Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            string lower = candidate.name.ToLowerInvariant();
            if (lower.Contains("router_rawmaterials") || lower.Contains("router") || lower.Contains("roteador"))
            {
                return candidate;
            }
        }

        return null;
    }

    private static GameObject FindExistingPanel()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            return existing;
        }

        existing = GameObject.Find(LegacyControlRoomRootName);
        if (existing != null)
        {
            return existing;
        }

        return GameObject.Find(LegacyRootName);
    }

    private static void CleanupDuplicatePanels(GameObject keep)
    {
        WorldSpaceNeonPanelUI[] panels = Object.FindObjectsOfType<WorldSpaceNeonPanelUI>(true);
        for (int i = 0; i < panels.Length; i++)
        {
            WorldSpaceNeonPanelUI panel = panels[i];
            if (panel == null || panel.gameObject == keep)
            {
                continue;
            }

            string panelName = panel.gameObject.name;
            if (panelName == LegacyRootName || panelName == LegacyControlRoomRootName || panelName == RootName)
            {
                Undo.DestroyObjectImmediate(panel.gameObject);
            }
        }
    }

    private static void ConfigureExistingPanel(GameObject root)
    {
        root.name = RootName;
        Transform target = FindLikelyTarget();
        if (target != null)
        {
            root.transform.SetParent(target, false);
            root.transform.localPosition = new Vector3(0f, 1.25f, 0f);
        }
        else
        {
            root.transform.position = ResolveInitialPanelPosition();
        }

        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        Canvas canvas = root.GetComponentInChildren<Canvas>(true);
        CanvasGroup group = root.GetComponentInChildren<CanvasGroup>(true);
        RectTransform panelRoot = FindChildRect(root.transform, PanelRootName);
        Image glowBack01 = FindChildImage(root.transform, "Glow_Back_01");
        Image glowBack02 = FindChildImage(root.transform, "Glow_Back_02");
        Image background = FindChildImage(root.transform, "Background");
        Image border = FindChildImage(root.transform, "Border");
        TMP_Text titleText = FindChildText(root.transform, "TitleText");
        TMP_Text subtitleText = FindChildText(root.transform, "SubtitleText");
        Image pointerTail = FindChildImage(root.transform, "PointerTail");

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvas.transform.localRotation = Quaternion.identity;
            canvas.transform.localScale = Vector3.one;
        }

        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        Sprite frameSprite = LoadSprite(FrameSpritePath);
        if (glowBack01 != null)
        {
            glowBack01.sprite = frameSprite;
        }

        if (glowBack02 != null)
        {
            glowBack02.sprite = frameSprite;
        }

        if (border != null)
        {
            border.sprite = frameSprite;
        }

        WorldSpaceNeonPanelUI panelUI = root.GetComponent<WorldSpaceNeonPanelUI>();
        if (panelUI == null)
        {
            panelUI = Undo.AddComponent<WorldSpaceNeonPanelUI>(root);
        }

        panelUI.AssignReferences(canvas, group, panelRoot, glowBack01, glowBack02, background, border, titleText, subtitleText, pointerTail);
        panelUI.ConfigureTextDefaults(TitleTextValue, SubtitleTextValue);
        panelUI.ConfigureBehaviorDefaults(true, 2.4f, 0.22f, true);
        panelUI.ConfigurePresentationDefaults(WorldSpaceNeonPanelUI.PresentationMode.ScreenSpaceProjected, root.transform, new Vector2(0f, 64f));
        panelUI.ConfigureBillboardDefaults(true, true);
    }

    private static Transform FindTransformByName(string targetName)
    {
        Transform[] transforms = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == targetName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static RectTransform FindChildRect(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        return child as RectTransform;
    }

    private static Image FindChildImage(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private static TMP_Text FindChildText(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChild(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static Image CreateLayerImage(Transform parent, string name, Sprite sprite, Color color)
    {
        RectTransform rect = CreateRect(parent, name);
        Stretch(rect);

        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize, FontStyles style, Color color)
    {
        RectTransform rect = CreateRect(parent, name);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(8f, fontSize * 0.55f);
        text.fontSizeMax = fontSize;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static void AddTextGlow(GameObject target, Color color, Vector2 distance)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
    }

    private static Sprite LoadSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool changed = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject CreateChild(Transform parent, string name, params System.Type[] components)
    {
        GameObject child = new GameObject(name, components);
        Undo.RegisterCreatedObjectUndo(child, "Create " + name);
        child.transform.SetParent(parent, false);
        return child;
    }

    private static RectTransform CreateRect(Transform parent, string name)
    {
        GameObject child = CreateChild(parent, name, typeof(RectTransform));
        return child.GetComponent<RectTransform>();
    }

    private static void ConfigureRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchInset(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
