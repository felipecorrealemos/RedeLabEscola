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

public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/MainMenu.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ProfessorPath = "Assets/Modelos 3D/Personagem/Professor/animacoes/professor@Standing W_Briefcase Idle.fbx";
    private const string ProfessorControllerPath = "Assets/Modelos 3D/Personagem/Professor/animacoes/Animator Controller professor.controller";
    private const string MaterialsFolder = "Assets/Materials/Menu";
    private const string PostProcessProfilePath = "Assets/Materials/RedeLabEscola_PostProcessProfile.asset";

    [MenuItem("Tools/RedeLabEscola/Create Main Menu Scene")]
    public static void CreateMainMenuScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.Log("Main menu scene creation canceled.");
            return;
        }

        EnsureFolder("Assets", "Materials");
        EnsureFolder("Assets/Materials", "Menu");

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "MainMenu";

        Material floorMaterial = GetOrCreateMaterial("Menu_Floor", new Color(0.58f, 0.62f, 0.60f));
        Material wallMaterial = GetOrCreateMaterial("Menu_Wall", new Color(0.78f, 0.84f, 0.86f));
        Material woodMaterial = GetOrCreateMaterial("Menu_Wood", new Color(0.45f, 0.27f, 0.13f));
        Material chairMaterial = GetOrCreateMaterial("Menu_Chair", new Color(0.12f, 0.28f, 0.43f));
        Material boardMaterial = GetOrCreateMaterial("Menu_Board", new Color(0.05f, 0.24f, 0.15f));
        Material metalMaterial = GetOrCreateMaterial("Menu_Metal", new Color(0.25f, 0.27f, 0.29f));

        GameObject root = new GameObject("MainMenuScene");
        GameObject environment = new GameObject("Environment");
        environment.transform.SetParent(root.transform);

        BuildClassroom(environment.transform, floorMaterial, wallMaterial, woodMaterial, chairMaterial, boardMaterial, metalMaterial);
        BuildProfessor(environment.transform);
        BuildCameraAndLight();
        BuildMenuUi(root.transform);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddScenesToBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Main menu scene created at {ScenePath}.");
    }

    private static void BuildClassroom(Transform parent, Material floorMaterial, Material wallMaterial, Material woodMaterial, Material chairMaterial, Material boardMaterial, Material metalMaterial)
    {
        CreateCube("Floor", parent, new Vector3(0f, -0.05f, 0f), new Vector3(12f, 0.1f, 8f), floorMaterial);
        CreateCube("Back_Wall", parent, new Vector3(0f, 2.2f, 3.15f), new Vector3(12f, 4.5f, 0.2f), wallMaterial);
        CreateCube("Left_Wall", parent, new Vector3(-6f, 2.2f, -0.4f), new Vector3(0.2f, 4.5f, 7.2f), wallMaterial);

        CreateCube("Blackboard", parent, new Vector3(0f, 2.35f, 3.02f), new Vector3(4.8f, 1.65f, 0.08f), boardMaterial);
        CreateCube("Blackboard_Frame_Top", parent, new Vector3(0f, 3.22f, 2.95f), new Vector3(5.1f, 0.08f, 0.12f), woodMaterial);
        CreateCube("Blackboard_Frame_Bottom", parent, new Vector3(0f, 1.48f, 2.95f), new Vector3(5.1f, 0.08f, 0.12f), woodMaterial);
        CreateCube("Blackboard_Frame_Left", parent, new Vector3(-2.55f, 2.35f, 2.95f), new Vector3(0.08f, 1.82f, 0.12f), woodMaterial);
        CreateCube("Blackboard_Frame_Right", parent, new Vector3(2.55f, 2.35f, 2.95f), new Vector3(0.08f, 1.82f, 0.12f), woodMaterial);

        CreateCube("Teacher_Desk_Top", parent, new Vector3(0f, 0.85f, 0.8f), new Vector3(3.4f, 0.22f, 1.25f), woodMaterial);
        CreateCube("Teacher_Desk_Front", parent, new Vector3(0f, 0.45f, 0.16f), new Vector3(3.4f, 0.75f, 0.12f), woodMaterial);
        CreateCube("Teacher_Desk_Left_Leg", parent, new Vector3(-1.45f, 0.4f, 1.25f), new Vector3(0.18f, 0.8f, 0.18f), woodMaterial);
        CreateCube("Teacher_Desk_Right_Leg", parent, new Vector3(1.45f, 0.4f, 1.25f), new Vector3(0.18f, 0.8f, 0.18f), woodMaterial);

        CreateChair(parent, new Vector3(2.6f, 0f, 0.95f), chairMaterial, metalMaterial);
        CreateCube("Book_Stack_01", parent, new Vector3(-0.85f, 1.02f, 0.58f), new Vector3(0.72f, 0.11f, 0.42f), boardMaterial);
        CreateCube("Book_Stack_02", parent, new Vector3(-0.82f, 1.14f, 0.58f), new Vector3(0.62f, 0.11f, 0.38f), wallMaterial);
    }

    private static void BuildProfessor(Transform parent)
    {
        GameObject professorAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ProfessorPath);
        if (professorAsset == null)
        {
            Debug.LogWarning($"Professor model not found at {ProfessorPath}. A placeholder will be created.");
            CreateCube("Professor_Placeholder", parent, new Vector3(-1.15f, 1f, 1.65f), new Vector3(0.55f, 2f, 0.35f), GetOrCreateMaterial("Menu_Professor_Placeholder", new Color(0.2f, 0.25f, 0.34f)));
            return;
        }

        GameObject professor = (GameObject)PrefabUtility.InstantiatePrefab(professorAsset);
        professor.name = "Professor";
        professor.transform.SetParent(parent);
        professor.transform.position = new Vector3(-2.15f, 0f, 0.65f);
        professor.transform.rotation = Quaternion.Euler(0f, 165f, 0f);
        professor.transform.localScale = Vector3.one;

        RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ProfessorControllerPath);
        Animator animator = professor.GetComponent<Animator>();
        if (animator != null && controller != null)
        {
            animator.runtimeAnimatorController = controller;
        }
    }

    private static void BuildCameraAndLight()
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 2.25f, -6.2f);
        camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
        camera.fieldOfView = 42f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.72f, 0.78f, 0.82f);
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

        GameObject lightObject = new GameObject("Directional Light");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.25f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.38f;
        light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        GameObject fillLightObject = new GameObject("Soft Fill Light");
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.intensity = 1.4f;
        fillLight.range = 6f;
        fillLight.transform.position = new Vector3(0f, 2.6f, -2.8f);
    }

    private static void BuildMenuUi(Transform root)
    {
        GameObject controllerObject = new GameObject("MainMenuController");
        controllerObject.transform.SetParent(root);
        MainMenuController controller = controllerObject.AddComponent<MainMenuController>();

        GameObject canvasObject = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panel = new GameObject("Bottom Menu Panel", typeof(Image));
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 390f);
        panelRect.anchoredPosition = new Vector2(52f, -145f);

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = new Color(0.025f, 0.055f, 0.065f, 0.88f);
        panelImage.sprite = MainMenuVerticalLayoutRefinement.GetOrCreateRoundedSprite();
        panelImage.type = Image.Type.Sliced;

        GameObject row = new GameObject("Button Row", typeof(VerticalLayoutGroup));
        row.transform.SetParent(panel.transform, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(310f, 330f);
        rowRect.anchoredPosition = Vector2.zero;

        VerticalLayoutGroup layout = row.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.spacing = 16f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        CreateButton(row.transform, "Start Game", controller.StartGame);
        CreateButton(row.transform, "Load Game", controller.LoadGame);
        CreateButton(row.transform, "Entrar em Sala", controller.EnterRoom);
        CreateButton(row.transform, "Sair", controller.QuitGame);

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(root);
    }

    private static void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(label + " Button", typeof(Image), typeof(Outline), typeof(Shadow), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = 285f;
        layout.preferredHeight = 58f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.12f, 0.30f, 0.38f, 0.98f);
        image.sprite = MainMenuVerticalLayoutRefinement.GetOrCreateRoundedSprite();
        image.type = Image.Type.Sliced;

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
        colors.highlightedColor = new Color(1.24f, 1.24f, 1.24f, 1f);
        colors.pressedColor = new Color(0.72f, 0.82f, 0.86f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.45f, 0.50f, 0.50f, 0.72f);
        colors.fadeDuration = 0.20f;
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, action);

        GameObject textObject = new GameObject("Text", typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = GetBuiltInFont();
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.96f, 0.98f, 0.94f);
    }

    private static void CreateChair(Transform parent, Vector3 basePosition, Material chairMaterial, Material metalMaterial)
    {
        CreateCube("Chair_Seat", parent, basePosition + new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.16f, 0.8f), chairMaterial);
        CreateCube("Chair_Back", parent, basePosition + new Vector3(0f, 1.0f, 0.35f), new Vector3(0.8f, 0.95f, 0.14f), chairMaterial);
        CreateCube("Chair_Leg_FL", parent, basePosition + new Vector3(-0.3f, 0.22f, -0.3f), new Vector3(0.1f, 0.44f, 0.1f), metalMaterial);
        CreateCube("Chair_Leg_FR", parent, basePosition + new Vector3(0.3f, 0.22f, -0.3f), new Vector3(0.1f, 0.44f, 0.1f), metalMaterial);
        CreateCube("Chair_Leg_BL", parent, basePosition + new Vector3(-0.3f, 0.22f, 0.3f), new Vector3(0.1f, 0.44f, 0.1f), metalMaterial);
        CreateCube("Chair_Leg_BR", parent, basePosition + new Vector3(0.3f, 0.22f, 0.3f), new Vector3(0.1f, 0.44f, 0.1f), metalMaterial);
    }

    private static GameObject CreateCube(string objectName, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent);
        cube.transform.position = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
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

    private static void AddScenesToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(SampleScenePath, true)
        };

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.path == ScenePath || scene.path == SampleScenePath)
            {
                continue;
            }

            scenes.Add(scene);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
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
