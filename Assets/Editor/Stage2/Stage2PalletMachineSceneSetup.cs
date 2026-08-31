using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage2PalletMachineSceneSetup
{
    private const string ScenePath = SceneNames.FactoryPath;
    private const string MachineRootName = "PalletMachine";
    private const string PalletPrefabPath = "Assets/Modelos 3D/Stage2_Factory/Prefabs/IndustrialProps/Pallet com caixas.prefab";

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Pallet Machine")]
    public static void ApplyToStage2Factory()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject root = GameObject.Find(MachineRootName);
        if (root == null)
        {
            Debug.LogError($"Could not find {MachineRootName} in {ScenePath}.");
            return;
        }

        GameObject palletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PalletPrefabPath);
        Stage2PalletMachineBootstrap.ApplyToRoot(root.transform, palletPrefab);
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"Configured {MachineRootName} in {ScenePath}. Pallet prefab: {(palletPrefab != null ? palletPrefab.name : "not assigned")}.");
    }
}
