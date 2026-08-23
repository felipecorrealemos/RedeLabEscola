using RedeLabEscola.Menu;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class InitialSceneFlowSetup
{
    private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
    private const string CharacterSelectionPath = "Assets/Scenes/CharacterSelection.unity";
    private const string SessionKey = "RedeLabEscola.InitialSceneFlowSetup.v1";

    [InitializeOnLoadMethod]
    private static void ApplyOnce()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Apply;
    }

    [MenuItem("Tools/RedeLabEscola/Setup Initial Scene Fades And Quit Dialog")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        SetupMainMenu();
        SetupCharacterSelection();
        EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        AssetDatabase.SaveAssets();
        Debug.Log("Initial scene fades and quit confirmation dialog configured.");
    }

    private static void SetupMainMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        MainMenuController controller = Object.FindObjectOfType<MainMenuController>();
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (controller == null || canvas == null) return;

        SceneFadeTransition transition = EnsureFade(canvas.transform, controller.gameObject);
        GameObject dialog = EnsureQuitDialog(canvas.transform, controller);

        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("sceneTransition").objectReferenceValue = transition;
        serialized.FindProperty("quitConfirmationDialog").objectReferenceValue = dialog;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void SetupCharacterSelection()
    {
        Scene scene = EditorSceneManager.OpenScene(CharacterSelectionPath, OpenSceneMode.Single);
        CharacterSelectionController controller = Object.FindObjectOfType<CharacterSelectionController>();
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (controller == null || canvas == null) return;

        SceneFadeTransition transition = EnsureFade(canvas.transform, controller.gameObject);
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("sceneTransition").objectReferenceValue = transition;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static SceneFadeTransition EnsureFade(Transform canvas, GameObject host)
    {
        Transform existing = canvas.Find("Scene Fade Overlay");
        GameObject overlay = existing != null ? existing.gameObject : new GameObject("Scene Fade Overlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(canvas, false);
        overlay.transform.SetAsLastSibling();
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = overlay.GetComponent<Image>();
        image.color = Color.black;
        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        SceneFadeTransition transition = host.GetComponent<SceneFadeTransition>();
        if (transition == null) transition = host.AddComponent<SceneFadeTransition>();
        SerializedObject serialized = new SerializedObject(transition);
        serialized.FindProperty("fadeGroup").objectReferenceValue = group;
        serialized.FindProperty("fadeDuration").floatValue = 0.45f;
        serialized.FindProperty("fadeInOnStart").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return transition;
    }

    private static GameObject EnsureQuitDialog(Transform canvas, MainMenuController controller)
    {
        Transform existing = canvas.Find("Quit Confirmation Dialog");
        if (existing != null) return existing.gameObject;

        Sprite rounded = MainMenuVerticalLayoutRefinement.GetOrCreateRoundedSprite();
        GameObject root = new GameObject("Quit Confirmation Dialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(canvas, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);

        GameObject panel = new GameObject("Dialog Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline), typeof(Shadow));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 260f);
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = rounded;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.035f, 0.085f, 0.105f, 0.98f);
        panel.GetComponent<Outline>().effectColor = new Color(0.36f, 0.70f, 0.76f, 0.9f);

        Text title = CreateText(panel.transform, "Question", "Deseja realmente sair do jogo?", 28, new Vector2(0f, 55f), new Vector2(450f, 70f));
        title.fontStyle = FontStyle.Bold;
        CreateDialogButton(panel.transform, "Sim Button", "Sim", new Vector2(-105f, -60f), controller.ConfirmQuit, rounded);
        CreateDialogButton(panel.transform, "Não Button", "Não", new Vector2(105f, -60f), controller.CancelQuit, rounded);
        root.SetActive(false);
        return root;
    }

    private static void CreateDialogButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action, Sprite sprite)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(160f, 56f);
        Image image = buttonObject.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.12f, 0.30f, 0.38f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.24f, 1.24f, 1.24f, 1f);
        colors.pressedColor = new Color(0.72f, 0.82f, 0.86f, 1f);
        colors.fadeDuration = 0.2f;
        button.colors = colors;
        UnityEventTools.AddPersistentListener(button.onClick, action);
        CreateText(buttonObject.transform, "Text", label, 22, Vector2.zero, Vector2.zero, true);
    }

    private static Text CreateText(Transform parent, string name, string value, int size, Vector2 position, Vector2 dimensions, bool stretch = false)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        if (stretch)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
        }
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.96f, 0.98f, 0.94f);
        return text;
    }
}
