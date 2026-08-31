using System.Collections.Generic;
using RedeLabEscola.Menu;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

public static class CharacterSelectionSceneBuilder
{
    private const string ScenePath = SceneNames.CharacterSelectionPath;
    private const string MainMenuScenePath = SceneNames.MainMenuPath;
    private const string GameplayScenePath = SceneNames.OfficePath;
    private const string AlunoPrefabPath = "Assets/Prefabs/Personagens/Players/Player Aluno.prefab";
    private const string AlunaPrefabPath = "Assets/Prefabs/Personagens/Players/Player Aluna.prefab";
    private const string MaterialsFolder = "Assets/Materials/CharacterSelection";
    private const string PostProcessProfilePath = "Assets/Materials/RedeLabEscola_PostProcessProfile.asset";

    [MenuItem("Tools/RedeLabEscola/Create Character Selection Scene")]
    public static void CreateCharacterSelectionScene()
    {
        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "CharacterSelection");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = SceneNames.CharacterSelection;

        Material floorMaterial = GetOrCreateMaterial("Selection_Floor", new Color(0.44f, 0.52f, 0.48f));
        Material wallMaterial = GetOrCreateMaterial("Selection_Backdrop", new Color(0.70f, 0.78f, 0.80f));
        Material panelMaterial = GetOrCreateMaterial("Selection_Platform", new Color(0.19f, 0.27f, 0.30f));

        GameObject root = new GameObject("CharacterSelectionScene");
        BuildEnvironment(root.transform, floorMaterial, wallMaterial, panelMaterial);
        BuildCameraAndLights();
        BuildSelection(root.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddScenesToBuildSettings();
        ConfigureGameplayPlayerPrefab();
        UpdateMainMenuController();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Character selection scene created at {ScenePath}.");
    }

    public static void SetupCharacterSelectionFlow()
    {
        CreateCharacterSelectionScene();
    }

    private static void BuildEnvironment(Transform parent, Material floorMaterial, Material wallMaterial, Material panelMaterial)
    {
        CreateCube("Floor", parent, new Vector3(0f, -0.05f, 0f), new Vector3(7.5f, 0.1f, 5.5f), floorMaterial);
        CreateCube("Backdrop", parent, new Vector3(0f, 1.8f, 1.85f), new Vector3(7.5f, 3.7f, 0.18f), wallMaterial);
        CreateCube("Aluno_Platform", parent, new Vector3(-1.18f, 0.03f, 0.15f), new Vector3(1.6f, 0.06f, 1.25f), panelMaterial);
        CreateCube("Aluna_Platform", parent, new Vector3(1.18f, 0.03f, 0.15f), new Vector3(1.6f, 0.06f, 1.25f), panelMaterial);
    }

    private static void BuildCameraAndLights()
    {
        GameObject cameraObject = new GameObject("Character Selection Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 2.05f, -6.35f);
        camera.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
        camera.fieldOfView = 44f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.68f, 0.76f, 0.78f);
        camera.allowHDR = true;
        PostProcessLayer postLayer = cameraObject.AddComponent<PostProcessLayer>();
        postLayer.volumeLayer = 1 << 8;
        postLayer.antialiasingMode = PostProcessLayer.Antialiasing.FastApproximateAntialiasing;

        PostProcessProfile profile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(PostProcessProfilePath);
        if (profile != null)
        {
            GameObject volumeObject = new GameObject("Global Post Processing");
            volumeObject.layer = 8;
            PostProcessVolume volume = volumeObject.AddComponent<PostProcessVolume>();
            volume.isGlobal = true;
            volume.priority = 5f;
            volume.sharedProfile = profile;
        }

        GameObject keyLightObject = new GameObject("Key Light");
        Light keyLight = keyLightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.2f;
        keyLight.shadows = LightShadows.Soft;
        keyLight.shadowStrength = 0.36f;
        keyLight.transform.rotation = Quaternion.Euler(45f, -20f, 0f);

        CreatePointLight("Aluno Highlight Light", new Vector3(-1.18f, 2.2f, -1.2f), new Color(0.95f, 1f, 0.92f), 0.8f, false);
        CreatePointLight("Aluna Highlight Light", new Vector3(1.18f, 2.2f, -1.2f), new Color(0.95f, 1f, 0.92f), 0.8f, false);

        GameObject fillLightObject = new GameObject("Soft Fill Light");
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.intensity = 1.35f;
        fillLight.range = 7f;
        fillLight.transform.position = new Vector3(0f, 2.1f, -2.8f);
    }

    private static void BuildSelection(Transform root)
    {
        CharacterSelectionController controller = new GameObject("CharacterSelectionController").AddComponent<CharacterSelectionController>();
        controller.transform.SetParent(root);

        CharacterSelectionOption alunoOption = CreateCharacterOption(
            "Aluno Option",
            AlunoPrefabPath,
            CharacterSelectionChoice.Aluno,
            new Vector3(-1.18f, 0f, 0f),
            controller,
            GameObject.Find("Aluno Highlight Light")?.GetComponent<Light>());

        CharacterSelectionOption alunaOption = CreateCharacterOption(
            "Aluna Option",
            AlunaPrefabPath,
            CharacterSelectionChoice.Aluna,
            new Vector3(1.18f, 0f, 0f),
            controller,
            GameObject.Find("Aluna Highlight Light")?.GetComponent<Light>());

        BuildUi(root, controller, alunoOption, alunaOption);
    }

    private static CharacterSelectionOption CreateCharacterOption(string name, string prefabPath, CharacterSelectionChoice choice, Vector3 position, CharacterSelectionController controller, Light highlightLight)
    {
        GameObject optionRoot = new GameObject(name);
        optionRoot.transform.position = position;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Character prefab not found at {prefabPath}.");
            CreateCube(name + "_Placeholder", optionRoot.transform, Vector3.up, new Vector3(0.55f, 2f, 0.35f), GetOrCreateMaterial(name + "_Placeholder", Color.gray));
        }
        else
        {
            GameObject preview = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            preview.name = choice == CharacterSelectionChoice.Aluno ? "Aluno Preview" : "Aluna Preview";
            preview.transform.SetParent(optionRoot.transform);
            preview.transform.localPosition = Vector3.zero;
            preview.transform.localRotation = Quaternion.Euler(0f, GetPreviewRotationY(choice), 0f);
            preview.transform.localScale = Vector3.one;
            StripGameplayComponents(preview);
        }

        BoxCollider selectionCollider = optionRoot.AddComponent<BoxCollider>();
        selectionCollider.center = new Vector3(0f, 1.05f, 0f);
        selectionCollider.size = new Vector3(1.25f, 2.1f, 0.95f);

        CharacterSelectionOption option = optionRoot.AddComponent<CharacterSelectionOption>();
        SerializedObject serializedOption = new SerializedObject(option);
        serializedOption.FindProperty("choice").enumValueIndex = (int)choice;
        serializedOption.FindProperty("controller").objectReferenceValue = controller;
        serializedOption.FindProperty("visualRoot").objectReferenceValue = optionRoot.transform;
        serializedOption.FindProperty("highlightLight").objectReferenceValue = highlightLight;
        serializedOption.ApplyModifiedPropertiesWithoutUndo();

        return option;
    }

    private static float GetPreviewRotationY(CharacterSelectionChoice choice)
    {
        return choice == CharacterSelectionChoice.Aluna ? 213.666f : 192.865f;
    }

    private static void StripGameplayComponents(GameObject preview)
    {
        foreach (PlayerTopDownController component in preview.GetComponentsInChildren<PlayerTopDownController>(true))
        {
            Object.DestroyImmediate(component);
        }

        foreach (PlayerCharacterVisualApplier component in preview.GetComponentsInChildren<PlayerCharacterVisualApplier>(true))
        {
            Object.DestroyImmediate(component);
        }

        foreach (Collider collider in preview.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }

        foreach (Transform child in preview.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "AnchorCarry" || child.name == "Anchor Carry" || child.name == "CarryAnchor")
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void BuildUi(Transform root, CharacterSelectionController controller, CharacterSelectionOption alunoOption, CharacterSelectionOption alunaOption)
    {
        GameObject canvasObject = new GameObject("Character Selection Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Image alunoFrame = CreateSelectionFrame(canvasObject.transform, "Aluno Selection Frame", new Vector2(-285f, 585f));
        Image alunaFrame = CreateSelectionFrame(canvasObject.transform, "Aluna Selection Frame", new Vector2(285f, 585f));

        Text title = CreateText(canvasObject.transform, "Title", "Escolha seu personagem", 44, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -42f);
        titleRect.sizeDelta = new Vector2(760f, 70f);

        Button confirmButton = CreateButton(canvasObject.transform, "Confirmar Button", "Começar", new Vector2(0f, 92f));
        Button backButton = CreateButton(canvasObject.transform, "Voltar Button", "← Voltar", new Vector2(40f, 52f));
        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.zero;
        backRect.pivot = Vector2.zero;
        backRect.anchoredPosition = new Vector2(40f, 52f);
        backRect.sizeDelta = new Vector2(250f, 64f);

        Text confirmationLabel = CreateText(canvasObject.transform, "Confirmation Label", "Escolha um personagem", 32, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform labelRect = confirmationLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0f);
        labelRect.anchorMax = new Vector2(0.5f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 155f);
        labelRect.sizeDelta = new Vector2(760f, 60f);

        CanvasGroup loadingGroup = CreateLoadingOverlay(canvasObject.transform, out Text loadingLabel);

        UnityEventTools.AddPersistentListener(confirmButton.onClick, controller.ConfirmAndStart);
        UnityEventTools.AddPersistentListener(backButton.onClick, controller.BackToMainMenu);

        SerializedObject serializedAlunoOption = new SerializedObject(alunoOption);
        serializedAlunoOption.FindProperty("selectionFrame").objectReferenceValue = alunoFrame;
        serializedAlunoOption.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedAlunaOption = new SerializedObject(alunaOption);
        serializedAlunaOption.FindProperty("selectionFrame").objectReferenceValue = alunaFrame;
        serializedAlunaOption.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("alunoOption").objectReferenceValue = alunoOption;
        serializedController.FindProperty("alunaOption").objectReferenceValue = alunaOption;
        serializedController.FindProperty("confirmButton").objectReferenceValue = confirmButton;
        serializedController.FindProperty("confirmationLabel").objectReferenceValue = confirmationLabel;
        serializedController.FindProperty("loadingGroup").objectReferenceValue = loadingGroup;
        serializedController.FindProperty("loadingLabel").objectReferenceValue = loadingLabel;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(root);
    }

    private static CanvasGroup CreateLoadingOverlay(Transform parent, out Text loadingLabel)
    {
        GameObject overlay = new GameObject("CharacterSelectionLoading_EDITAR", typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = Color.black;

        loadingLabel = CreateText(overlay.transform, "LoadingLabel_EDITAR", "Carregando...", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform labelRect = loadingLabel.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.sizeDelta = new Vector2(500f, 70f);
        loadingLabel.color = Color.white;

        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        overlay.SetActive(false);
        return group;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition)
    {
        GameObject buttonObject = new GameObject(name, typeof(Image), typeof(Outline), typeof(Shadow), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(310f, 68f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.28f, 0.34f, 0.98f);

        Outline outline = buttonObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.56f, 0.82f, 0.76f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        Shadow[] shadows = buttonObject.GetComponents<Shadow>();
        Shadow shadow = shadows[shadows.Length - 1];
        shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
        shadow.effectDistance = new Vector2(0f, -5f);

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
        colors.pressedColor = new Color(0.72f, 0.78f, 0.80f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.50f, 0.50f, 0.72f);
        colors.fadeDuration = 0.12f;
        button.colors = colors;

        Text text = CreateText(buttonObject.transform, "Text", label, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
        RectTransform textRect = text.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.color = new Color(0.96f, 0.98f, 0.94f);

        return button;
    }

    private static Image CreateSelectionFrame(Transform parent, string name, Vector2 anchoredPosition)
    {
        GameObject frameObject = new GameObject(name, typeof(Image), typeof(Outline));
        frameObject.transform.SetParent(parent, false);

        RectTransform rect = frameObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(465f, 740f);

        Image image = frameObject.GetComponent<Image>();
        image.sprite = CreateFrameSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(0.93f, 0.96f, 0.94f, 0.25f);
        image.raycastTarget = false;

        Outline outline = frameObject.GetComponent<Outline>();
        outline.effectColor = new Color(0.93f, 0.96f, 0.94f, 0.75f);
        outline.effectDistance = new Vector2(2f, -2f);
        return image;
    }

    private static Sprite CreateFrameSprite()
    {
        const int size = 32;
        const int border = 3;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Character Selection Frame Sprite",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = Color.clear;
        Color white = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder = x < border || x >= size - border || y < border || y >= size - border;
                texture.SetPixel(x, y, isBorder ? white : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }

    private static Text CreateText(Transform parent, string name, string value, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(name, typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = GetBuiltInFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = new Color(0.06f, 0.08f, 0.09f);
        return text;
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static void CreatePointLight(string name, Vector3 position, Color color, float intensity, bool enabled)
    {
        GameObject lightObject = new GameObject(name);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = 3f;
        light.enabled = enabled;
        light.transform.position = position;
    }

    private static void ConfigureGameplayPlayerPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlunoPrefabPath);
        GameObject alunaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlunaPrefabPath);
        if (prefab == null || alunaPrefab == null)
        {
            return;
        }

        GameObject instance = PrefabUtility.LoadPrefabContents(AlunoPrefabPath);
        PlayerCharacterVisualApplier applier = instance.GetComponent<PlayerCharacterVisualApplier>();
        if (applier == null)
        {
            applier = instance.AddComponent<PlayerCharacterVisualApplier>();
        }

        SerializedObject serializedApplier = new SerializedObject(applier);
        serializedApplier.FindProperty("alunaVisualPrefab").objectReferenceValue = alunaPrefab;
        serializedApplier.FindProperty("alunoVisualRoot").objectReferenceValue = FindChild(instance.transform, "modelo");
        serializedApplier.FindProperty("visualParent").objectReferenceValue = instance.transform;
        serializedApplier.FindProperty("topDownController").objectReferenceValue = instance.GetComponent<PlayerTopDownController>();
        serializedApplier.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(instance, AlunoPrefabPath);
        PrefabUtility.UnloadPrefabContents(instance);
    }

    private static void UpdateMainMenuController()
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindObjectOfType<MainMenuController>();
        if (controller != null)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            SerializedProperty selectionScene = serializedController.FindProperty("characterSelectionSceneName");
            if (selectionScene != null)
            {
                selectionScene.stringValue = SceneNames.CharacterSelection;
            }

            SerializedProperty gameplayScene = serializedController.FindProperty("gameplaySceneName");
            if (gameplayScene != null)
            {
                gameplayScene.stringValue = SceneNames.Office;
            }

            serializedController.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static Transform FindChild(Transform root, string name)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == name)
            {
                return children[i];
            }
        }

        return null;
    }

    private static void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MainMenuScenePath, true),
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(GameplayScenePath, true),
            new EditorBuildSettingsScene(SceneNames.FactoryPath, true),
            new EditorBuildSettingsScene(SceneNames.ProviderPath, true)
        };

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == MainMenuScenePath
                || scene.path == ScenePath
                || scene.path == GameplayScenePath
                || scene.path == SceneNames.FactoryPath
                || scene.path == SceneNames.ProviderPath)
            {
                continue;
            }

            scenes.Add(scene);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static Material GetOrCreateMaterial(string materialName, Color color)
    {
        string materialPath = $"{MaterialsFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material != null)
        {
            return material;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        material = new Material(shader)
        {
            name = materialName,
            color = color
        };

        AssetDatabase.CreateAsset(material, materialPath);
        return material;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string folderPath = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }

    private static Font GetBuiltInFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }
}
