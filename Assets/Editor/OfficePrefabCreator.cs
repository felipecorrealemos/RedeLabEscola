using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class OfficePrefabCreator
{
    private const string PrefabFolder = "Assets/Prefabs/Office";

    [MenuItem("Tools/RedeLabEscola/Prefabs/Create Default Office Prefabs")]
    public static void CreateDefaultOfficePrefabs()
    {
        EnsurePrefabFolder();

        int createdCount = 0;
        createdCount += CreatePrefabIfFound("Computer_01", "Computer");
        createdCount += CreatePrefabIfFound("Desk_01", "Desk");
        createdCount += CreatePrefabIfFound("Chair_01", "Chair");
        createdCount += CreatePrefabIfFound("Router", "Router");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Office prefab creation complete. Created or updated {createdCount} prefab(s).");
    }

    [MenuItem("Tools/RedeLabEscola/Prefabs/Create Prefabs From Selection")]
    public static void CreatePrefabsFromSelection()
    {
        EnsurePrefabFolder();

        int createdCount = 0;
        foreach (GameObject selectedObject in Selection.gameObjects)
        {
            if (selectedObject == null)
            {
                continue;
            }

            createdCount += CreatePrefab(selectedObject, GetPrefabName(selectedObject.name));
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Selection prefab creation complete. Created or updated {createdCount} prefab(s).");
    }

    private static int CreatePrefabIfFound(string sceneObjectName, string prefabName)
    {
        GameObject sceneObject = GameObject.Find(sceneObjectName);
        if (sceneObject == null)
        {
            Debug.LogWarning($"{sceneObjectName} not found in the current scene.");
            return 0;
        }

        return CreatePrefab(sceneObject, prefabName);
    }

    private static int CreatePrefab(GameObject source, string prefabName)
    {
        GameObject normalizedRoot = CreateNormalizedPrefabRoot(source, prefabName);
        string prefabPath = $"{PrefabFolder}/{prefabName}.prefab";

        PrefabUtility.SaveAsPrefabAsset(normalizedRoot, prefabPath);
        Object.DestroyImmediate(normalizedRoot);

        Debug.Log($"Prefab saved: {prefabPath}");
        return 1;
    }

    private static GameObject CreateNormalizedPrefabRoot(GameObject source, string prefabName)
    {
        GameObject sourceClone = Object.Instantiate(source);
        sourceClone.name = source.name;

        Bounds bounds = CalculateRendererBounds(sourceClone);
        Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        GameObject normalizedRoot = new GameObject(prefabName);
        normalizedRoot.transform.SetPositionAndRotation(pivot, source.transform.rotation);
        normalizedRoot.transform.localScale = Vector3.one;

        while (sourceClone.transform.childCount > 0)
        {
            Transform child = sourceClone.transform.GetChild(0);
            child.SetParent(normalizedRoot.transform, true);
        }

        CopySupportedRootComponents(source, normalizedRoot);
        Object.DestroyImmediate(sourceClone);

        normalizedRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return normalizedRoot;
    }

    private static void CopySupportedRootComponents(GameObject source, GameObject destination)
    {
        foreach (Component component in source.GetComponents<Component>())
        {
            if (component == null || component is Transform)
            {
                continue;
            }

            UnityEditorInternal.ComponentUtility.CopyComponent(component);
            UnityEditorInternal.ComponentUtility.PasteComponentAsNew(destination);
        }
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static string GetPrefabName(string sceneObjectName)
    {
        string prefabName = Regex.Replace(sceneObjectName, @"_\d+$", string.Empty);
        prefabName = Regex.Replace(prefabName, @"[^A-Za-z0-9_ -]", string.Empty).Trim();

        return string.IsNullOrWhiteSpace(prefabName) ? "Prefab" : prefabName;
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "Office");
        }
    }
}
