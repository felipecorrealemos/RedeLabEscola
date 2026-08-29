using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MissionCanvasEditorBootstrap
{
    private const string GameplaySceneName = "SampleScene";
    private const string Stage2SceneName = "Stage2_Factory";
    private const string MissionManagerName = "MissionManager";

    [MenuItem("Tools/RedeLabEscola/Missions/Ensure Mission Canvas In Scene")]
    public static void EnsureMissionCanvasInActiveScene()
    {
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!IsMissionScene(scene))
        {
            return;
        }

        MissionManager manager = Object.FindObjectOfType<MissionManager>(true);
        if (manager == null)
        {
            GameObject managerObject = new GameObject(MissionManagerName);
            SceneManager.MoveGameObjectToScene(managerObject, scene);
            manager = managerObject.AddComponent<MissionManager>();
            Undo.RegisterCreatedObjectUndo(managerObject, "Create MissionManager");
        }

        manager.EnsureEditorPreview();
        EnsureEditablePanelStyle();
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void EnsureEditablePanelStyle()
    {
        GameObject panel = GameObject.Find("MissionPanel");
        if (panel == null)
        {
            return;
        }

        Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null && panelImage.sprite == null)
        {
            panelImage.sprite = roundedSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = new Color(0f, 0f, 0f, 220f / 255f);
            EditorUtility.SetDirty(panelImage);
        }

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null)
        {
            outline = Undo.AddComponent<Outline>(panel);
            outline.effectColor = new Color(0.88f, 0.95f, 1f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
            EditorUtility.SetDirty(outline);
        }

        Transform toggle = panel.transform.Find("ToggleButton");
        Image toggleImage = toggle != null ? toggle.GetComponent<Image>() : null;
        if (toggleImage != null && toggleImage.sprite == null)
        {
            toggleImage.sprite = roundedSprite;
            toggleImage.type = Image.Type.Sliced;
            toggleImage.color = new Color(0.025f, 0.025f, 0.025f, 0.96f);
            EditorUtility.SetDirty(toggleImage);
        }
    }

    [MenuItem("Tools/RedeLabEscola/Missions/Save Mission Canvas In Scene")]
    public static void SaveMissionCanvasInActiveScene()
    {
        EnsureMissionCanvasInActiveScene();
        Scene scene = SceneManager.GetActiveScene();
        if (IsMissionScene(scene) && scene.isDirty)
        {
            EditorSceneManager.SaveScene(scene);
        }
    }

    private static bool IsMissionScene(Scene scene)
    {
        return scene.IsValid() && (scene.name == GameplaySceneName || scene.name == Stage2SceneName);
    }
}
