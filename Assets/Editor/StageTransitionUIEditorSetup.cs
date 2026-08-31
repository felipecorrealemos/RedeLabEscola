using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class StageTransitionUIEditorSetup
{
    private const string CanvasName = "StageTransitionCanvas";
    private const string ExitTriggerName = "Stage1ExitTrigger_AJUSTAR_POSICAO";
    private const string Stage2ScenePath = SceneNames.FactoryPath;

    [MenuItem("Tools/RedeLabEscola/Stages/Ensure Stage Transition UI")]
    public static void EnsureInActiveScene()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || (scene.name != SceneNames.Office && scene.name != SceneNames.Factory)) return;
        if (HasStageTransition(scene)) return;

        CreateStageCanvas(scene, scene.name == SceneNames.Factory);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("StageTransitionCanvas criado. Ajuste seus elementos diretamente no Canvas e salve a cena.");
    }

    public static void EnsureStage2SceneAsset()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene stage2 = SceneManager.GetSceneByPath(Stage2ScenePath);
        bool openedTemporarily = !stage2.IsValid() || !stage2.isLoaded;
        if (openedTemporarily)
        {
            stage2 = EditorSceneManager.OpenScene(Stage2ScenePath, OpenSceneMode.Additive);
        }

        if (!HasStageTransition(stage2))
        {
            CreateStageCanvas(stage2, true);
            EditorSceneManager.SaveScene(stage2);
            Debug.Log("StageTransitionCanvas com fade de entrada gravado na cena A_fabrica.");
        }

        if (openedTemporarily && stage2.IsValid())
        {
            EditorSceneManager.CloseScene(stage2, true);
        }
    }

    private static bool HasStageTransition(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return false;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<StageTransitionUI>(true) != null) return true;
        }
        return false;
    }

    private static void CreateStageCanvas(Scene scene, bool stage2)
    {
        GameObject canvasObject = NewObject(CanvasName, null);
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.localScale = Vector3.one;
        canvasRect.localRotation = Quaternion.identity;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();
        AudioSource audioSource = canvasObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        GameObject announcement = CreatePanel("Announcement_EDITAR", canvasObject.transform,
            new Vector2(0.5f, 0.5f), new Vector2(700f, 270f), new Color(0.025f, 0.03f, 0.04f, 0.94f));
        CanvasGroup announcementGroup = announcement.AddComponent<CanvasGroup>();
        Text eyebrow = CreateText("StageLabel_EDITAR", announcement.transform, "Estágio 1", 28, FontStyle.Bold,
            new Vector2(0.5f, 0.72f), new Vector2(620f, 44f), new Color(0.15f, 0.85f, 1f));
        Text title = CreateText("StageName_EDITAR", announcement.transform, "O escritório", 54, FontStyle.Bold,
            new Vector2(0.5f, 0.49f), new Vector2(640f, 74f), Color.white);
        Text status = CreateText("Status_EDITAR", announcement.transform, string.Empty, 22, FontStyle.Bold,
            new Vector2(0.5f, 0.22f), new Vector2(620f, 40f), new Color(0.35f, 1f, 0.45f));

        GameObject loading = CreatePanel("LoadingScreen_EDITAR", canvasObject.transform,
            new Vector2(0.5f, 0.5f), Vector2.zero, Color.black, true);
        Image loadingImage = loading.GetComponent<Image>();
        loadingImage.sprite = null;
        loadingImage.type = Image.Type.Simple;
        RectTransform loadingRect = loading.GetComponent<RectTransform>();
        loadingRect.offsetMin = new Vector2(-2f, -2f);
        loadingRect.offsetMax = new Vector2(2f, 2f);
        CanvasGroup loadingGroup = loading.AddComponent<CanvasGroup>();
        CreateText("LoadingLabel_EDITAR", loading.transform, "Carregando...", 28, FontStyle.Bold,
            new Vector2(0.5f, 0.5f), new Vector2(500f, 70f), Color.white);

        StageTransitionUI controller = canvasObject.AddComponent<StageTransitionUI>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("stageLabel").stringValue = stage2 ? "Estágio 2" : "Estágio 1";
        serialized.FindProperty("stageName").stringValue = stage2 ? "A fábrica" : "O escritório";
        serialized.FindProperty("portugueseTextVersion").intValue = 1;
        serialized.FindProperty("nextSceneName").stringValue = stage2 ? SceneNames.Provider : SceneNames.Factory;
        serialized.FindProperty("announcementGroup").objectReferenceValue = announcementGroup;
        serialized.FindProperty("stageLabelText").objectReferenceValue = eyebrow;
        serialized.FindProperty("stageNameText").objectReferenceValue = title;
        serialized.FindProperty("statusText").objectReferenceValue = status;
        serialized.FindProperty("loadingGroup").objectReferenceValue = loadingGroup;
        serialized.FindProperty("showLoadingScreenInEditMode").boolValue = stage2;
        serialized.FindProperty("fullScreenLayoutVersion").intValue = 1;
        serialized.FindProperty("celebrationAudioSource").objectReferenceValue = audioSource;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        announcement.SetActive(false);
        loading.SetActive(stage2);

        if (!stage2) CreateExitTrigger(scene, controller);
        canvas.enabled = stage2;
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create stage transition UI");
    }

    private static void CreateExitTrigger(Scene scene, StageTransitionUI controller)
    {
        GameObject triggerObject = NewObject(ExitTriggerName, null);
        SceneManager.MoveGameObjectToScene(triggerObject, scene);
        Transform finalDoor = FindFinalDoor();
        triggerObject.transform.position = finalDoor != null
            ? finalDoor.position + finalDoor.forward * 2.5f + Vector3.up
            : Vector3.zero;
        BoxCollider box = triggerObject.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3f, 2f, 3f);
        StageExitTrigger trigger = triggerObject.AddComponent<StageExitTrigger>();
        SerializedObject serialized = new SerializedObject(trigger);
        serialized.FindProperty("transitionUI").objectReferenceValue = controller;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Undo.RegisterCreatedObjectUndo(triggerObject, "Create stage exit trigger");
    }

    private static Transform FindFinalDoor()
    {
        Transform fallback = null;
        foreach (Transform candidate in Object.FindObjectsOfType<Transform>(true))
        {
            if (!candidate.name.ToLowerInvariant().Contains("door")) continue;
            fallback = candidate;
            string path = GetPath(candidate).ToLowerInvariant();
            if (path.Contains("sala 3") || path.Contains("sala3")) return candidate;
        }
        return fallback;
    }

    private static string GetPath(Transform value)
    {
        string path = value.name;
        while (value.parent != null) { value = value.parent; path = value.name + "/" + path; }
        return path;
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchor, Vector2 size, Color color, bool stretch = false)
    {
        GameObject panel = NewObject(name, parent);
        RectTransform rect = panel.AddComponent<RectTransform>();
        if (stretch)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
        else
        {
            rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size;
        }
        Image image = panel.AddComponent<Image>();
        image.color = color;
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        image.type = Image.Type.Sliced;
        return panel;
    }

    private static Text CreateText(string name, Transform parent, string value, int size, FontStyle style,
        Vector2 anchor, Vector2 dimensions, Color color)
    {
        GameObject textObject = NewObject(name, parent);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = dimensions;
        Text text = textObject.AddComponent<Text>();
        text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = TextAnchor.MiddleCenter; text.color = color;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return text;
    }

    private static GameObject NewObject(string name, Transform parent)
    {
        GameObject result = new GameObject(name);
        if (parent != null) result.transform.SetParent(parent, false);
        return result;
    }
}
