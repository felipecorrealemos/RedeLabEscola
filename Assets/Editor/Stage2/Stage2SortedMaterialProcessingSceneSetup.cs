using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage2SortedMaterialProcessingSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Stage2/Stage2_Factory.unity";
    private const string SortedRootName = "SortedMaterialConveyors";
    private const string ProcessedPartPrefabPath = "Assets/Prefabs/Peças/Motor eletrico industrial.prefab";

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Sorted Material Processing")]
    public static void ApplyToStage2Factory()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find(SortedRootName);
        if (root == null)
        {
            Debug.LogError($"Could not find {SortedRootName} in {ScenePath}.");
            return;
        }

        GameObject processedPartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProcessedPartPrefabPath);
        Stage2SortedMaterialProcessingBootstrap.ApplyToRoot(root.transform, processedPartPrefab);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Configured {SortedRootName} in {ScenePath}. Processed part prefab: {(processedPartPrefab != null ? processedPartPrefab.name : "not assigned")}.");
    }
}
