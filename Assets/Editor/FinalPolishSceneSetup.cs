using RedeLabEscola.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FinalPolishSceneSetup
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string OfficePath = "Assets/Scenes/SampleScene.unity";
    private const string FactoryPath = "Assets/Scenes/Stage2/Stage2_Factory.unity";
    private const string GoogleSpritePath = "Assets/Imagens/icone google.png";

    [MenuItem("Tools/RedeLabEscola/Final Polish/Apply Scene Setup")]
    public static void ApplyAll()
    {
        Scene originalScene = SceneManager.GetActiveScene();
        string originalPath = originalScene.path;

        ConfigureMainMenu();
        ConfigureMissionScene(OfficePath);
        ConfigureMissionScene(FactoryPath);

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(originalPath)) EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
        Debug.Log("Final polish scene setup applied: login UI and mission task templates are serialized in their scenes.");
    }

    private static void ConfigureMainMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindObjectOfType<MainMenuController>(true);
        Canvas canvas = FindNamedComponent<Canvas>("Menu Canvas");
        Button sourceButton = FindNamedComponent<Button>("Start Game Button");
        Image sourcePanel = FindNamedComponent<Image>("Bottom Menu Panel");
        if (controller == null || canvas == null || sourceButton == null)
        {
            throw new System.InvalidOperationException("MainMenu nao possui a estrutura esperada para configurar o login.");
        }

        RedeLabMainMenuAuthUI authUi = controller.GetComponent<RedeLabMainMenuAuthUI>();
        if (authUi == null) authUi = controller.gameObject.AddComponent<RedeLabMainMenuAuthUI>();

        Transform existing = canvas.transform.Find("Authentication Panel");
        GameObject panel = existing != null ? existing.gameObject : CreateUiObject("Authentication Panel", canvas.transform);
        RectTransform panelRect = EnsureRect(panel);
        panelRect.anchorMin = new Vector2(0f, 0.5f);
        panelRect.anchorMax = new Vector2(0f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0.5f);
        panelRect.sizeDelta = new Vector2(360f, 152f);
        panelRect.anchoredPosition = new Vector2(52f, 140f);
        Image panelImage = EnsureComponent<Image>(panel);
        if (sourcePanel != null)
        {
            panelImage.sprite = sourcePanel.sprite;
            panelImage.type = sourcePanel.type;
            panelImage.color = sourcePanel.color;
        }

        Text status = EnsureLabel(panel.transform, "Authentication Status", new Vector2(0f, 48f), 16, FontStyle.Normal);
        Text greeting = EnsureLabel(panel.transform, "Authentication Greeting", new Vector2(0f, 20f), 18, FontStyle.Bold);

        Transform existingButton = panel.transform.Find("Entrar com Google Button");
        GameObject buttonObject = existingButton != null
            ? existingButton.gameObject
            : CreateUiObject("Entrar com Google Button", panel.transform);
        RectTransform buttonRect = EnsureRect(buttonObject);
        buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.sizeDelta = new Vector2(285f, 52f);
        buttonRect.anchoredPosition = new Vector2(0f, -40f);
        Image buttonImage = EnsureComponent<Image>(buttonObject);
        Image sourceImage = sourceButton.GetComponent<Image>();
        if (sourceImage != null)
        {
            buttonImage.sprite = sourceImage.sprite;
            buttonImage.type = sourceImage.type;
            buttonImage.color = sourceImage.color;
        }
        Button authButton = EnsureComponent<Button>(buttonObject);
        authButton.targetGraphic = buttonImage;
        authButton.onClick = new Button.ButtonClickedEvent();

        GameObject iconObject = EnsureChild(buttonObject.transform, "Google Icon");
        RectTransform iconRect = EnsureRect(iconObject);
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(30f, 0f);
        iconRect.sizeDelta = new Vector2(28f, 28f);
        Image googleIcon = EnsureComponent<Image>(iconObject);
        googleIcon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GoogleSpritePath);
        googleIcon.preserveAspect = true;
        googleIcon.color = Color.white;
        googleIcon.raycastTarget = false;

        GameObject textObject = EnsureChild(buttonObject.transform, "Text");
        RectTransform textRect = EnsureRect(textObject);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(52f, 0f);
        textRect.offsetMax = new Vector2(-14f, 0f);
        Text buttonLabel = EnsureComponent<Text>(textObject);
        Text sourceLabel = sourceButton.GetComponentInChildren<Text>(true);
        buttonLabel.font = sourceLabel != null ? sourceLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonLabel.fontSize = sourceLabel != null ? sourceLabel.fontSize : 18;
        buttonLabel.fontStyle = FontStyle.Bold;
        buttonLabel.alignment = TextAnchor.MiddleCenter;
        buttonLabel.color = sourceLabel != null ? sourceLabel.color : Color.white;
        buttonLabel.text = "Entrar com Google";
        buttonLabel.raycastTarget = false;

        SerializedObject serialized = new SerializedObject(authUi);
        serialized.FindProperty("authenticationPanel").objectReferenceValue = panel;
        serialized.FindProperty("authButton").objectReferenceValue = authButton;
        serialized.FindProperty("googleIcon").objectReferenceValue = googleIcon;
        serialized.FindProperty("authButtonLabel").objectReferenceValue = buttonLabel;
        serialized.FindProperty("statusLabel").objectReferenceValue = status;
        serialized.FindProperty("greetingLabel").objectReferenceValue = greeting;
        SerializedProperty protectedButtons = serialized.FindProperty("protectedButtons");
        string[] names = { "Start Game Button", "Load Game Button", "Entrar em Sala Button" };
        protectedButtons.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            protectedButtons.GetArrayElementAtIndex(i).objectReferenceValue = FindNamedComponent<Button>(names[i]);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void ConfigureMissionScene(string path)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        MissionManager manager = Object.FindObjectOfType<MissionManager>(true);
        if (manager == null) throw new System.InvalidOperationException(path + " nao possui MissionManager.");

        Transform templateTransform = manager.transform.Find("Mission Task Row Template");
        GameObject template = templateTransform != null
            ? templateTransform.gameObject
            : CreateUiObject("Mission Task Row Template", manager.transform);
        RectTransform rowRect = EnsureRect(template);
        rowRect.sizeDelta = new Vector2(0f, 36.8f);
        LayoutElement rowLayout = EnsureComponent<LayoutElement>(template);
        rowLayout.minHeight = 36.8f;
        rowLayout.preferredHeight = 36.8f;

        GameObject checkboxObject = EnsureChild(template.transform, "Checkbox");
        RectTransform checkboxRect = EnsureRect(checkboxObject);
        checkboxRect.anchorMin = checkboxRect.anchorMax = new Vector2(0f, 0.5f);
        checkboxRect.pivot = new Vector2(0.5f, 0.5f);
        checkboxRect.anchoredPosition = new Vector2(16f, 0f);
        checkboxRect.sizeDelta = new Vector2(20f, 20f);
        Image border = EnsureComponent<Image>(checkboxObject);
        border.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        border.color = new Color(0.94f, 0.97f, 1f, 0.96f);
        border.raycastTarget = false;

        GameObject innerObject = EnsureChild(checkboxObject.transform, "Inner");
        RectTransform innerRect = EnsureRect(innerObject);
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);
        Image inner = EnsureComponent<Image>(innerObject);
        inner.sprite = border.sprite;
        inner.color = new Color(0.08f, 0.1f, 0.11f, 0.96f);
        inner.raycastTarget = false;

        GameObject checkObject = EnsureChild(checkboxObject.transform, "Checkmark");
        RectTransform checkRect = EnsureRect(checkObject);
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.offsetMin = new Vector2(3f, 3f);
        checkRect.offsetMax = new Vector2(-3f, -3f);
        Image check = EnsureComponent<Image>(checkObject);
        check.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        check.color = new Color(0.16f, 0.92f, 0.34f, 1f);
        check.preserveAspect = true;
        check.raycastTarget = false;
        checkObject.transform.SetAsLastSibling();

        GameObject taskTextObject = EnsureChild(template.transform, "Text");
        RectTransform taskTextRect = EnsureRect(taskTextObject);
        taskTextRect.anchorMin = Vector2.zero;
        taskTextRect.anchorMax = Vector2.one;
        taskTextRect.offsetMin = new Vector2(33.6f, 0f);
        taskTextRect.offsetMax = new Vector2(-14.4f, 0f);
        Text taskText = EnsureComponent<Text>(taskTextObject);
        taskText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        taskText.fontSize = 16;
        taskText.alignment = TextAnchor.MiddleLeft;
        taskText.horizontalOverflow = HorizontalWrapMode.Wrap;
        taskText.verticalOverflow = VerticalWrapMode.Overflow;
        taskText.color = Color.white;
        taskText.text = "Descricao da tarefa (template)";
        taskText.raycastTarget = false;

        template.SetActive(false);
        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("taskRowTemplate").objectReferenceValue = template;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        manager.EnsureEditorPreview();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject CreateUiObject(string name, Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created;
    }

    private static GameObject EnsureChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        return child != null ? child.gameObject : CreateUiObject(name, parent);
    }

    private static RectTransform EnsureRect(GameObject target)
    {
        RectTransform rect = target.GetComponent<RectTransform>();
        return rect != null ? rect : target.AddComponent<RectTransform>();
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        return component != null ? component : target.AddComponent<T>();
    }

    private static Text EnsureLabel(Transform parent, string name, Vector2 position, int size, FontStyle style)
    {
        GameObject labelObject = EnsureChild(parent, name);
        RectTransform rect = EnsureRect(labelObject);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, 26f);
        rect.anchoredPosition = position;
        Text label = EnsureComponent<Text>(labelObject);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.90f, 0.96f, 0.92f);
        label.raycastTarget = false;
        return label;
    }

    private static T FindNamedComponent<T>(string objectName) where T : Component
    {
        T[] components = Object.FindObjectsOfType<T>(true);
        foreach (T component in components)
        {
            if (component.gameObject.name == objectName) return component;
        }
        return null;
    }
}
