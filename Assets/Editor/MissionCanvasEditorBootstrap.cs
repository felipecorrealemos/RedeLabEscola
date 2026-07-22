using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class MissionCanvasEditorBootstrap
{
    private const string GameplaySceneName = "SampleScene";
    private const string Stage2SceneName = "Stage2_Factory";
    private const string MissionManagerName = "MissionManager";

    static MissionCanvasEditorBootstrap()
    {
        EditorApplication.delayCall += EnsureMissionCanvasInActiveScene;
        EditorSceneManager.sceneOpened += (_, __) => EditorApplication.delayCall += EnsureMissionCanvasInActiveScene;
        EditorSceneManager.activeSceneChangedInEditMode += (_, __) => EditorApplication.delayCall += EnsureMissionCanvasInActiveScene;
    }

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
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static bool IsMissionScene(Scene scene)
    {
        return scene.IsValid() && (scene.name == GameplaySceneName || scene.name == Stage2SceneName);
    }
}
