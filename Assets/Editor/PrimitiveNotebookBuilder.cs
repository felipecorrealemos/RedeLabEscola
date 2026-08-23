using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PrimitiveNotebookBuilder
{
    private const string PrefabFolder = "Assets/Prefabs/Office";
    private const string MaterialFolder = "Assets/Prefabs/materiais";
    private const string PrefabPath = PrefabFolder + "/Notebook.prefab";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string NotebookInstanceName = "Notebook_Sala_3";

    [MenuItem("Tools/RedeLabEscola/Prefabs/Create Primitive Notebook")]
    public static void CreatePrimitiveNotebookPrefab()
    {
        EnsurePrefabFolder();

        GameObject notebook = null;
        try
        {
            notebook = BuildNotebookRoot();
            PrefabUtility.SaveAsPrefabAsset(notebook, PrefabPath);
        }
        finally
        {
            if (notebook != null) Object.DestroyImmediate(notebook);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Primitive notebook prefab saved at {PrefabPath}.");
    }

    [MenuItem("Tools/RedeLabEscola/Scene/Add Notebook To Room 3")]
    public static void CreatePrefabAndPlaceInRoom3()
    {
        TryCreatePrefabAndPlaceInRoom3(true);
    }

    private static bool TryCreatePrefabAndPlaceInRoom3(bool canOpenScene)
    {
        CreatePrimitiveNotebookPrefab();

        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != ScenePath)
        {
            if (!canOpenScene)
            {
                Debug.Log("Primitive notebook prefab created. Open SampleScene or use Tools/RedeLabEscola/Scene/Add Notebook To Room 3 to place it.");
                return false;
            }

            EditorSceneManager.OpenScene(ScenePath);
        }

        GameObject room = GameObject.Find("sala 3");
        if (room == null)
        {
            Debug.LogWarning("Could not find 'sala 3'. Falling back to Environment.");
            room = GameObject.Find("Environment");
        }

        GameObject existingNotebook = GameObject.Find(NotebookInstanceName);
        if (existingNotebook != null)
        {
            Object.DestroyImmediate(existingNotebook);
        }

        GameObject notebookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject notebookInstance = (GameObject)PrefabUtility.InstantiatePrefab(notebookPrefab);
        notebookInstance.name = NotebookInstanceName;
        notebookInstance.transform.SetParent(room != null ? room.transform : null, false);
        notebookInstance.transform.localPosition = new Vector3(-1.85f, 1.18f, 3.02f);
        notebookInstance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        notebookInstance.transform.localScale = Vector3.one;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        Debug.Log("Primitive notebook placed in room 3.");
        return true;
    }

    private static GameObject BuildNotebookRoot()
    {
        Material bodyMaterial = LoadMaterial("cinza");
        Material darkMaterial = LoadMaterial("preto");
        Material screenMaterial = LoadMaterial("azul claro tela");
        Material accentMaterial = LoadMaterial("azul escuro");
        Material keyMaterial = LoadMaterial("branco");
        Material lightMaterial = LoadMaterial("verde");

        GameObject root = new GameObject("Notebook");

        CreateCube("Notebook_Base", new Vector3(0f, 0.04f, 0f), new Vector3(1.55f, 0.08f, 1.02f), bodyMaterial, root.transform);
        CreateCube("Notebook_Base_Bevel_Front", new Vector3(0f, 0.095f, -0.48f), new Vector3(1.42f, 0.035f, 0.05f), darkMaterial, root.transform);
        CreateCube("Notebook_Touchpad", new Vector3(0f, 0.102f, -0.25f), new Vector3(0.42f, 0.012f, 0.24f), accentMaterial, root.transform);
        CreateCube("Notebook_Touchpad_Button_Left", new Vector3(-0.11f, 0.112f, -0.39f), new Vector3(0.20f, 0.012f, 0.035f), darkMaterial, root.transform);
        CreateCube("Notebook_Touchpad_Button_Right", new Vector3(0.11f, 0.112f, -0.39f), new Vector3(0.20f, 0.012f, 0.035f), darkMaterial, root.transform);
        CreateCube("Notebook_Status_Light", new Vector3(0.66f, 0.115f, -0.47f), new Vector3(0.055f, 0.014f, 0.025f), lightMaterial, root.transform);

        CreateKeyboard(keyMaterial, darkMaterial, root.transform);
        CreatePorts(darkMaterial, accentMaterial, root.transform);

        GameObject displayPivot = new GameObject("Notebook_Display_Hinge");
        displayPivot.transform.SetParent(root.transform, false);
        displayPivot.transform.localPosition = new Vector3(0f, 0.1f, 0.47f);
        displayPivot.transform.localRotation = Quaternion.Euler(-68f, 0f, 0f);

        CreateCube("Notebook_Display_Back", new Vector3(0f, 0.48f, 0f), new Vector3(1.48f, 0.9f, 0.055f), bodyMaterial, displayPivot.transform);
        CreateCube("Notebook_Display_Bezel", new Vector3(0f, 0.48f, -0.035f), new Vector3(1.36f, 0.78f, 0.035f), darkMaterial, displayPivot.transform);
        CreateCube("Notebook_Screen", new Vector3(0f, 0.48f, -0.058f), new Vector3(1.18f, 0.60f, 0.018f), screenMaterial, displayPivot.transform);
        CreateCube("Notebook_Webcam", new Vector3(0f, 0.83f, -0.066f), new Vector3(0.055f, 0.035f, 0.012f), accentMaterial, displayPivot.transform);
        CreateCube("Notebook_Brand_Badge", new Vector3(0f, 0.08f, -0.066f), new Vector3(0.22f, 0.04f, 0.012f), keyMaterial, displayPivot.transform);

        CreateCylinder("Notebook_Hinge_Left", new Vector3(-0.54f, 0.12f, 0.47f), new Vector3(0.045f, 0.18f, 0.045f), darkMaterial, root.transform);
        CreateCylinder("Notebook_Hinge_Right", new Vector3(0.54f, 0.12f, 0.47f), new Vector3(0.045f, 0.18f, 0.045f), darkMaterial, root.transform);

        MovableDevice movableDevice = root.AddComponent<MovableDevice>();
        ComputerInteractable computerInteractable = root.AddComponent<ComputerInteractable>();
        ConfigureNotebookComponents(movableDevice, computerInteractable);

        return root;
    }

    private static void CreateKeyboard(Material keyMaterial, Material darkMaterial, Transform parent)
    {
        CreateCube("Keyboard", new Vector3(0f, 0.103f, 0.1f), new Vector3(1.16f, 0.01f, 0.42f), darkMaterial, parent);

        for (int row = 0; row < 4; row++)
        {
            int columns = row == 3 ? 8 : 10;
            float rowWidth = row == 3 ? 0.92f : 1.08f;
            float startX = -rowWidth * 0.5f;
            float z = 0.255f - row * 0.095f;

            for (int column = 0; column < columns; column++)
            {
                float x = startX + column * (rowWidth / (columns - 1));
                CreateCube($"Notebook_Key_{row + 1}_{column + 1}", new Vector3(x, 0.122f, z), new Vector3(0.075f, 0.018f, 0.052f), keyMaterial, parent);
            }
        }

        CreateCube("Notebook_Spacebar", new Vector3(0f, 0.124f, -0.08f), new Vector3(0.42f, 0.018f, 0.052f), keyMaterial, parent);
    }

    private static void CreatePorts(Material darkMaterial, Material accentMaterial, Transform parent)
    {
        CreateCube("Notebook_Left_USB_Port", new Vector3(-0.79f, 0.06f, 0.16f), new Vector3(0.018f, 0.035f, 0.13f), darkMaterial, parent);
        CreateCube("Notebook_Left_HDMI_Port", new Vector3(-0.79f, 0.06f, -0.05f), new Vector3(0.018f, 0.04f, 0.18f), darkMaterial, parent);
        CreateCube("Notebook_Right_USB_Port", new Vector3(0.79f, 0.06f, 0.1f), new Vector3(0.018f, 0.035f, 0.13f), darkMaterial, parent);
        CreateCube("Notebook_Charging_Port", new Vector3(0.79f, 0.06f, -0.17f), new Vector3(0.018f, 0.045f, 0.07f), accentMaterial, parent);
    }

    private static void ConfigureNotebookComponents(MovableDevice movableDevice, ComputerInteractable computerInteractable)
    {
        SerializedObject serializedDevice = new SerializedObject(movableDevice);
        serializedDevice.FindProperty("deviceName").stringValue = "Computador";
        serializedDevice.FindProperty("interactionIndicatorSize").vector2Value = new Vector2(1.55f, 1.1f);
        serializedDevice.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedComputer = new SerializedObject(computerInteractable);
        serializedComputer.FindProperty("deviceTitle").stringValue = "Notebook";
        serializedComputer.FindProperty("carryPromptText").stringValue = "E pegar notebook";
        serializedComputer.FindProperty("networkPromptText").stringValue = "F configurar notebook";
        serializedComputer.FindProperty("useComputerPromptText").stringValue = "F usar notebook";
        serializedComputer.FindProperty("reservedDeviceName").stringValue = "Notebook";
        serializedComputer.FindProperty("generatedLightLocalPosition").vector3Value = new Vector3(0.66f, 0.115f, -0.47f);
        serializedComputer.FindProperty("generatedUseColliderSize").vector3Value = new Vector3(1.2f, 0.25f, 0.6f);
        serializedComputer.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material LoadMaterial(string materialName)
    {
        string materialPath = $"{MaterialFolder}/{materialName}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material != null)
        {
            return material;
        }

        Debug.LogWarning($"Could not find material at {materialPath}. Using default material.");
        return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
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
        cylinder.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
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
