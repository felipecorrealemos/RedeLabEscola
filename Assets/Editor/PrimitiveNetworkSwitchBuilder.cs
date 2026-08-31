using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrimitiveNetworkSwitchBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Office";
    private const string PrefabPath = PrefabFolder + "/NetworkSwitch.prefab";
    private const string ScenePath = SceneNames.OfficePath;
    private const string SwitchInstanceName = "Switch_Sala_3";
    [MenuItem("Tools/RedeLabEscola/Prefabs/Create Primitive Network Switch")]
    public static void CreatePrimitiveNetworkSwitchPrefab()
    {
        EnsurePrefabFolder();

        GameObject networkSwitch = BuildSwitchRoot();
        PrefabUtility.SaveAsPrefabAsset(networkSwitch, PrefabPath);
        Object.DestroyImmediate(networkSwitch);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Primitive network switch prefab saved at {PrefabPath}.");
    }

    [MenuItem("Tools/RedeLabEscola/Scene/Add Network Switch To Room 3")]
    public static void CreatePrefabAndPlaceInRoom3()
    {
        TryCreatePrefabAndPlaceInRoom3(true);
    }

    private static bool TryCreatePrefabAndPlaceInRoom3(bool canOpenScene)
    {
        CreatePrimitiveNetworkSwitchPrefab();

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            if (!canOpenScene)
            {
                Debug.Log("Primitive network switch prefab created. Open O_escritorio or use Tools/RedeLabEscola/Scene/Add Network Switch To Room 3 to place it.");
                return false;
            }

            EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject room = GameObject.Find("sala 3");
        if (room == null)
        {
            room = GameObject.Find("sala 2 (1)");
        }

        if (room == null)
        {
            Debug.LogWarning("Could not find room 3. Falling back to Environment.");
            room = GameObject.Find("Environment");
        }

        GameObject existingSwitch = GameObject.Find(SwitchInstanceName);
        if (existingSwitch != null)
        {
            Object.DestroyImmediate(existingSwitch);
        }

        GameObject switchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject switchInstance = (GameObject)PrefabUtility.InstantiatePrefab(switchPrefab);
        switchInstance.name = SwitchInstanceName;
        switchInstance.transform.SetParent(room != null ? room.transform : null, false);
        switchInstance.transform.localPosition = new Vector3(-5.55f, 1.05f, 2.85f);
        switchInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        switchInstance.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("Primitive network switch placed in room 3.");
        return true;
    }

    private static GameObject BuildSwitchRoot()
    {
        Material bodyMaterial = CreateMaterial("Mat_Switch_Body_Gray", new Color(0.34f, 0.35f, 0.36f));
        Material topMaterial = CreateMaterial("Mat_Switch_Top_Gray", new Color(0.42f, 0.43f, 0.44f));
        Material frontMaterial = CreateMaterial("Mat_Switch_Front_DarkGray", new Color(0.12f, 0.13f, 0.14f));
        Material portMaterial = CreateMaterial("Mat_Switch_Port_Black", new Color(0.68f, 0.70f, 0.70f));
        Material lightMaterial = CreateMaterial("Mat_Switch_Light_MutedGreen", new Color(0.18f, 0.55f, 0.26f));

        GameObject root = new GameObject("NetworkSwitch");

        CreateCube("Switch_Body", new Vector3(0f, 0.16f, 0f), new Vector3(2.25f, 0.32f, 0.82f), bodyMaterial, root.transform);
        CreateCube("Switch_Top_Bevel", new Vector3(0f, 0.345f, 0.02f), new Vector3(2.12f, 0.06f, 0.70f), topMaterial, root.transform);
        CreateCube("Switch_Front_Panel", new Vector3(0f, 0.17f, -0.425f), new Vector3(2.05f, 0.22f, 0.045f), frontMaterial, root.transform);
        CreateCube("Switch_Back_Shadow", new Vector3(0f, 0.14f, 0.425f), new Vector3(2.0f, 0.12f, 0.035f), frontMaterial, root.transform);

        for (int row = 0; row < 2; row++)
        {
            for (int column = 0; column < 8; column++)
            {
                float x = -0.77f + column * 0.22f;
                float y = 0.22f - row * 0.105f;
                CreateCube($"Switch_Port_{row + 1}_{column + 1}", new Vector3(x, y, -0.455f), new Vector3(0.13f, 0.07f, 0.035f), portMaterial, root.transform);
            }
        }

        for (int lightIndex = 0; lightIndex < 6; lightIndex++)
        {
            CreateCube($"Switch_Status_Light_{lightIndex + 1}", new Vector3(-1.0f + lightIndex * 0.08f, 0.29f, -0.462f), new Vector3(0.025f, 0.025f, 0.02f), lightMaterial, root.transform);
        }

        CreateCube("Switch_Left_Vent", new Vector3(-1.16f, 0.17f, -0.06f), new Vector3(0.035f, 0.14f, 0.42f), frontMaterial, root.transform);
        CreateCube("Switch_Right_Vent", new Vector3(1.16f, 0.17f, -0.06f), new Vector3(0.035f, 0.14f, 0.42f), frontMaterial, root.transform);
        CreateCylinder("Switch_Foot_Left", new Vector3(-0.72f, -0.02f, -0.24f), new Vector3(0.11f, 0.025f, 0.11f), frontMaterial, root.transform);
        CreateCylinder("Switch_Foot_Right", new Vector3(0.72f, -0.02f, -0.24f), new Vector3(0.11f, 0.025f, 0.11f), frontMaterial, root.transform);

        return root;
    }

    private static Material CreateMaterial(string materialName, Color color)
    {
        string materialFolder = PrefabFolder + "/Materials";
        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            AssetDatabase.CreateFolder(PrefabFolder, "Materials");
        }

        string materialPath = $"{materialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.name = materialName;
        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject CreateCube(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localScale = scale;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    private static GameObject CreateCylinder(string objectName, Vector3 position, Vector3 scale, Material material, Transform parent)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = objectName;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = position;
        cylinder.transform.localScale = scale;
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        return cylinder;
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", Path.GetFileName(PrefabFolder));
        }
    }
}
