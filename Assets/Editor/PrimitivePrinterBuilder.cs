using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrimitivePrinterBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Office";
    private const string PrefabPath = PrefabFolder + "/Printer.prefab";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PrinterInstanceName = "Impressora_Sala_3";
    [MenuItem("Tools/RedeLabEscola/Prefabs/Create Primitive Printer")]
    public static void CreatePrimitivePrinterPrefab()
    {
        EnsurePrefabFolder();

        GameObject printer = BuildPrinterRoot();
        PrefabUtility.SaveAsPrefabAsset(printer, PrefabPath);
        Object.DestroyImmediate(printer);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Primitive printer prefab saved at {PrefabPath}.");
    }

    [MenuItem("Tools/RedeLabEscola/Scene/Add Printer To Room 3")]
    public static void CreatePrefabAndPlaceInRoom3()
    {
        TryCreatePrefabAndPlaceInRoom3(true);
    }

    private static bool TryCreatePrefabAndPlaceInRoom3(bool canOpenScene)
    {
        CreatePrimitivePrinterPrefab();

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            if (!canOpenScene)
            {
                Debug.Log("Primitive printer prefab created. Open SampleScene or use Tools/RedeLabEscola/Scene/Add Printer To Room 3 to place it.");
                return false;
            }

            EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject room = GameObject.Find("sala 2 (1)");
        if (room == null)
        {
            Debug.LogWarning("Could not find 'sala 2 (1)'. Falling back to Environment.");
            room = GameObject.Find("Environment");
        }

        GameObject existingPrinter = GameObject.Find(PrinterInstanceName);
        if (existingPrinter != null)
        {
            Object.DestroyImmediate(existingPrinter);
        }

        GameObject printerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject printerInstance = (GameObject)PrefabUtility.InstantiatePrefab(printerPrefab);
        printerInstance.name = PrinterInstanceName;
        printerInstance.transform.SetParent(room != null ? room.transform : null, false);
        printerInstance.transform.localPosition = new Vector3(-7.45f, 0f, 2.85f);
        printerInstance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        printerInstance.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("Primitive printer placed in the last room.");
        return true;
    }

    private static GameObject BuildPrinterRoot()
    {
        Material shellMaterial = CreateMaterial("Mat_Printer_Shell", new Color(0.86f, 0.88f, 0.88f));
        Material darkMaterial = CreateMaterial("Mat_Printer_Dark", new Color(0.10f, 0.11f, 0.12f));
        Material paperMaterial = CreateMaterial("Mat_Printer_Paper", new Color(0.98f, 0.98f, 0.94f));
        Material accentMaterial = CreateMaterial("Mat_Printer_Display", new Color(0.04f, 0.30f, 0.45f));
        Material buttonMaterial = CreateMaterial("Mat_Printer_Button", new Color(0.02f, 0.55f, 0.22f));

        GameObject root = new GameObject("Printer");

        CreateCube("Printer_Stand_Top", new Vector3(0f, 0.74f, 0f), new Vector3(1.65f, 0.16f, 1.05f), darkMaterial, root.transform);
        CreateCube("Printer_Stand_Leg_FL", new Vector3(-0.65f, 0.36f, -0.39f), new Vector3(0.13f, 0.72f, 0.13f), darkMaterial, root.transform);
        CreateCube("Printer_Stand_Leg_FR", new Vector3(0.65f, 0.36f, -0.39f), new Vector3(0.13f, 0.72f, 0.13f), darkMaterial, root.transform);
        CreateCube("Printer_Stand_Leg_BL", new Vector3(-0.65f, 0.36f, 0.39f), new Vector3(0.13f, 0.72f, 0.13f), darkMaterial, root.transform);
        CreateCube("Printer_Stand_Leg_BR", new Vector3(0.65f, 0.36f, 0.39f), new Vector3(0.13f, 0.72f, 0.13f), darkMaterial, root.transform);

        CreateCube("Printer_Body", new Vector3(0f, 1.15f, 0f), new Vector3(1.45f, 0.52f, 0.88f), shellMaterial, root.transform);
        CreateCube("Printer_Top_Lid", new Vector3(0f, 1.45f, 0.04f), new Vector3(1.28f, 0.10f, 0.72f), shellMaterial, root.transform);
        CreateCube("Printer_Scanner_Glass", new Vector3(0f, 1.515f, 0.04f), new Vector3(1.05f, 0.025f, 0.50f), accentMaterial, root.transform);
        CreateCube("Printer_Front_Panel", new Vector3(0f, 1.16f, -0.47f), new Vector3(1.18f, 0.22f, 0.06f), darkMaterial, root.transform);
        CreateCube("Printer_Output_Slot", new Vector3(0f, 1.27f, -0.515f), new Vector3(0.95f, 0.07f, 0.04f), accentMaterial, root.transform);
        CreateCube("Printer_Paper_Stack", new Vector3(0f, 0.97f, -0.72f), new Vector3(1.05f, 0.08f, 0.52f), paperMaterial, root.transform);
        CreateCube("Printer_Output_Tray", new Vector3(0f, 0.88f, -0.70f), new Vector3(1.20f, 0.08f, 0.58f), darkMaterial, root.transform);
        CreateCube("Printer_Input_Tray", new Vector3(0f, 1.05f, 0.57f), new Vector3(1.10f, 0.08f, 0.38f), darkMaterial, root.transform);
        CreateCube("Printer_Input_Paper", new Vector3(0f, 1.12f, 0.68f), new Vector3(0.92f, 0.07f, 0.34f), paperMaterial, root.transform);
        CreateCube("Printer_Display", new Vector3(0.46f, 1.47f, -0.34f), new Vector3(0.32f, 0.035f, 0.18f), accentMaterial, root.transform);
        CreateCube("Printer_Button_Power", new Vector3(0.72f, 1.48f, -0.34f), new Vector3(0.10f, 0.04f, 0.10f), buttonMaterial, root.transform);
        CreateCylinder("Printer_Roller_Left", new Vector3(-0.48f, 1.01f, -0.50f), new Vector3(0.12f, 0.12f, 0.12f), darkMaterial, root.transform);
        CreateCylinder("Printer_Roller_Right", new Vector3(0.48f, 1.01f, -0.50f), new Vector3(0.12f, 0.12f, 0.12f), darkMaterial, root.transform);

        root.AddComponent<MovableDevice>();
        root.AddComponent<ComputerInteractable>();

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
        cylinder.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
