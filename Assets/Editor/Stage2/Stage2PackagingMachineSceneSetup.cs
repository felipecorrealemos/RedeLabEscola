using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage2PackagingMachineSceneSetup
{
    private const string ScenePath = SceneNames.FactoryPath;
    private const string MachineRootName = "PackagingMachine_Boxes";
    private const string BoxPrefabPath = "Assets/Modelos 3D/Stage2_Factory/Prefabs/IndustrialProps/Stage2_LargeBox_Static.prefab";

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Packaging Machine Boxes")]
    public static void ApplyToStage2Factory()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find(MachineRootName);
        if (root == null)
        {
            Debug.LogError($"Could not find {MachineRootName} in {ScenePath}.");
            return;
        }

        GameObject boxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoxPrefabPath);
        Stage2PackagingMachineBootstrap.ApplyToRoot(root.transform, boxPrefab);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Configured {MachineRootName} in {ScenePath}. Box prefab: {(boxPrefab != null ? boxPrefab.name : "not assigned")}.");
    }
}
