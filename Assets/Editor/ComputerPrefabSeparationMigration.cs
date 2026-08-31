using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ComputerPrefabSeparationMigration
{
    private const string ComputerPrefabPath = "Assets/Prefabs/Office/Computer.prefab";
    private const string CabinetPrefabPath = "Assets/Prefabs/Office/Computer_Base.prefab";

    private sealed class CabinetRecord
    {
        public Transform station;
        public Transform parent;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
    }

    [MenuItem("Tools/RedeLabEscola/Computer/Separate Cabinet From Workstation")]
    public static void Execute()
    {
        List<CabinetRecord> records = CaptureSceneCabinets();
        GameObject computerRoot = PrefabUtility.LoadPrefabContents(ComputerPrefabPath);
        try
        {
            Transform cabinet = computerRoot.transform.Find("Computer_Base");
            if (cabinet != null)
            {
                EnsureCabinetComponents(cabinet.gameObject);
                PrefabUtility.SaveAsPrefabAsset(cabinet.gameObject, CabinetPrefabPath);
                Object.DestroyImmediate(cabinet.gameObject);
            }

            EnsureWorkstation(computerRoot);
            PrefabUtility.SaveAsPrefabAsset(computerRoot, ComputerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(computerRoot);
        }

        GameObject cabinetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CabinetPrefabPath);
        foreach (CabinetRecord record in records)
        {
            if (record.station == null || cabinetPrefab == null)
            {
                continue;
            }

            GameObject cabinetInstance = (GameObject)PrefabUtility.InstantiatePrefab(cabinetPrefab, record.station.gameObject.scene);
            cabinetInstance.transform.SetParent(record.parent, true);
            cabinetInstance.transform.SetPositionAndRotation(record.position, record.rotation);
            cabinetInstance.transform.localScale = record.localScale;
            cabinetInstance.name = "Computer_Base";
            EnsureCabinetComponents(cabinetInstance);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"Computer refatorado: {records.Count} gabinetes independentes preservados e vinculados por DeviceDropZone.");
    }

    private static List<CabinetRecord> CaptureSceneCabinets()
    {
        var records = new List<CabinetRecord>();
        ComputerWorkstation[] migrated = Object.FindObjectsOfType<ComputerWorkstation>(true);
        if (migrated.Length > 0)
        {
            return records;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ComputerPrefabPath);
        foreach (Transform candidate in Object.FindObjectsOfType<Transform>(true))
        {
            if (candidate == null || PrefabUtility.GetCorrespondingObjectFromSource(candidate.gameObject) != prefab)
            {
                continue;
            }

            Transform cabinet = candidate.Find("Computer_Base");
            if (cabinet == null)
            {
                continue;
            }

            records.Add(new CabinetRecord
            {
                station = candidate,
                parent = candidate.parent,
                position = cabinet.position,
                rotation = cabinet.rotation,
                localScale = cabinet.localScale
            });
        }
        return records;
    }

    private static void EnsureCabinetComponents(GameObject cabinet)
    {
        if (cabinet.GetComponent<MovableDevice>() == null) cabinet.AddComponent<MovableDevice>();
        if (cabinet.GetComponent<ComputerCabinet>() == null) cabinet.AddComponent<ComputerCabinet>();
        if (cabinet.GetComponent<ComputerInteractable>() == null) cabinet.AddComponent<ComputerInteractable>();
    }

    private static void EnsureWorkstation(GameObject root)
    {
        ComputerWorkstation workstation = root.GetComponent<ComputerWorkstation>();
        if (workstation == null) workstation = root.AddComponent<ComputerWorkstation>();
        DeviceDropZone dropZone = root.GetComponentInChildren<DeviceDropZone>(true);
        Transform screen = root.transform.Find("Monitor_Screen");
        Renderer screenRenderer = screen != null ? screen.GetComponent<Renderer>() : null;
        Light spotlight = screen != null ? screen.GetComponentInChildren<Light>(true) : null;
        workstation.Configure(dropZone, screenRenderer, spotlight);
    }
}
