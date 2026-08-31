using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Stage2ForkliftUiSetup
{
    private const string ScenePath = SceneNames.FactoryPath;
    private const string ForkliftIconSpritePath = "Assets/Imagens/empilhadeira/imagem empilhadeira.png";

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Forklift UI")]
    public static void ApplyToStage2Factory()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        EmpilhadeiraController[] forklifts = Object.FindObjectsOfType<EmpilhadeiraController>(true);
        if (forklifts.Length == 0)
        {
            Debug.LogWarning("No EmpilhadeiraController found in A_fabrica.");
            return;
        }

        Undo.SetCurrentGroupName("Setup Forklift UI");
        int undoGroup = Undo.GetCurrentGroup();

        Canvas canvas = GetOrCreateInteractionCanvas();
        GameObject drivingPanel = GetOrCreateDrivingPanel(canvas.transform);
        Text drivingPanelText = drivingPanel.GetComponentInChildren<Text>(true);
        Sprite forkliftIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ForkliftIconSpritePath);
        DeadZoneCameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<DeadZoneCameraFollow>() : Object.FindObjectOfType<DeadZoneCameraFollow>();

        EnsureForkliftIcon(drivingPanel.transform, forkliftIcon, false);

        for (int i = 0; i < forklifts.Length; i++)
        {
            ConfigureForklift(forklifts[i], canvas, drivingPanel, drivingPanelText, forkliftIcon, cameraFollow);
            EditorUtility.SetDirty(forklifts[i]);
        }

        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        EditorSceneManager.SaveScene(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"Forklift UI setup complete. Forklifts configured: {forklifts.Length}.", canvas.gameObject);
    }

    private static void ConfigureForklift(
        EmpilhadeiraController forklift,
        Canvas canvas,
        GameObject drivingPanel,
        Text drivingPanelText,
        Sprite forkliftIcon,
        DeadZoneCameraFollow cameraFollow)
    {
        if (forklift == null)
        {
            return;
        }

        SerializedObject serialized = new SerializedObject(forklift);
        AssignObjectIfNull(serialized, "canvas", canvas);
        AssignObjectIfNull(serialized, "drivingPanelObject", drivingPanel);
        AssignObjectIfNull(serialized, "drivingPanelLabel", drivingPanelText);
        AssignObjectIfNull(serialized, "forkliftIconSprite", forkliftIcon);
        AssignObjectIfNull(serialized, "cameraFollow", cameraFollow);
        serialized.ApplyModifiedProperties();
    }

    private static void AssignObjectIfNull(SerializedObject serialized, string propertyName, Object value)
    {
        if (value == null)
        {
            return;
        }

        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null && property.objectReferenceValue == null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static Canvas GetOrCreateInteractionCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == "InteractionCanvas")
            {
                return canvases[i];
            }
        }

        GameObject canvasObject = new GameObject("InteractionCanvas");
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create InteractionCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        RuntimeEventSystemUtility.EnsureSingleEventSystem();
        return canvas;
    }

    private static GameObject GetOrCreateDrivingPanel(Transform canvas)
    {
        Transform existing = canvas.Find("EmpilhadeiraDrivingPanel");
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject panel = new GameObject("EmpilhadeiraDrivingPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create EmpilhadeiraDrivingPanel");
        panel.transform.SetParent(canvas, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -96f);
        panelRect.sizeDelta = new Vector2(260f, 230f);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.6f);

        Text text = CreateText(panel.transform);
        text.text =
            "EMPILHADEIRA\n\n" +
            "W / S - Frente e re\n" +
            "A / D - Direcao\n" +
            "1 - Baixar garfos\n" +
            "2 - Levantar garfos\n" +
            "E - Sair";

        EnsureForkliftIcon(panel.transform, AssetDatabase.LoadAssetAtPath<Sprite>(ForkliftIconSpritePath), true);
        panel.SetActive(false);
        return panel;
    }

    private static Text CreateText(Transform parent)
    {
        GameObject label = new GameObject("Text");
        Undo.RegisterCreatedObjectUndo(label, "Create Forklift UI Text");
        label.transform.SetParent(parent, false);

        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(12f, 8f);
        labelRect.offsetMax = new Vector2(-12f, -96f);

        Text text = label.AddComponent<Text>();
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 15;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void EnsureForkliftIcon(Transform panel, Sprite sprite, bool applyDefaultLayout)
    {
        Transform iconTransform = panel.Find("ForkliftIcon");
        bool createdIcon = false;
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("ForkliftIcon", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(iconObject, "Create Forklift Icon");
            iconObject.transform.SetParent(panel, false);
            iconTransform = iconObject.transform;
            createdIcon = true;
        }

        RectTransform iconRect = iconTransform.GetComponent<RectTransform>();
        if (iconRect == null)
        {
            iconRect = iconTransform.gameObject.AddComponent<RectTransform>();
            iconTransform = iconRect.transform;
            createdIcon = true;
        }

        if (applyDefaultLayout || createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -10f);
            iconRect.sizeDelta = new Vector2(96f, 72f);
        }

        Image icon = iconTransform.GetComponent<Image>();
        if (icon == null)
        {
            icon = iconTransform.gameObject.AddComponent<Image>();
        }

        if (sprite != null && icon.sprite == null)
        {
            icon.sprite = sprite;
        }

        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }
}
