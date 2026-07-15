using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage2RawMaterialConveyorSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Stage2/Stage2_Factory.unity";
    private const string ConveyorRootName = "RawMaterialConveyor";

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Raw Material Conveyor")]
    public static void ApplyToStage2Factory()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find(ConveyorRootName);
        if (root == null)
        {
            Debug.LogError($"Could not find {ConveyorRootName} in {ScenePath}.");
            return;
        }

        Stage2RawMaterialConveyorBootstrap.ApplyToRoot(root.transform);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Configured {ConveyorRootName} in {ScenePath}.");
    }
}
