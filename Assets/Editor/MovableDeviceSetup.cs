using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MovableDeviceSetup
{
    [MenuItem("Tools/RedeLabEscola/Setup Movable Devices")]
    public static void Setup()
    {
        int configuredCount = 0;
        configuredCount += SetupComputerBase("Computer_01");
        configuredCount += SetupComputerBase("Computer_02");

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        Debug.Log($"Movable device setup complete. Configured {configuredCount} object(s).");
    }

    [MenuItem("Tools/RedeLabEscola/Movable Devices/Make Selected Movable")]
    public static void MakeSelectedMovable()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Select one or more GameObjects to make movable.");
            return;
        }

        foreach (GameObject selectedObject in selectedObjects)
        {
            ConfigureMovableDevice(selectedObject, selectedObject.name);
        }

        MarkActiveSceneDirty();
        Debug.Log($"Configured {selectedObjects.Length} movable device(s).");
    }

    [MenuItem("Tools/RedeLabEscola/Movable Devices/Make Selected Drop Zone")]
    public static void MakeSelectedDropZone()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("Select one or more GameObjects to make drop zones.");
            return;
        }

        foreach (GameObject selectedObject in selectedObjects)
        {
            ConfigureDropZone(selectedObject);
        }

        MarkActiveSceneDirty();
        Debug.Log($"Configured {selectedObjects.Length} device drop zone(s).");
    }

    private static int SetupComputerBase(string computerName)
    {
        GameObject computerObject = GameObject.Find(computerName);
        if (computerObject == null)
        {
            return 0;
        }

        MovableDevice parentDevice = computerObject.GetComponent<MovableDevice>();
        if (parentDevice != null)
        {
            Object.DestroyImmediate(parentDevice);
        }

        Transform baseTransform = computerObject.transform.Find("Computer_Base");
        if (baseTransform == null)
        {
            Debug.LogWarning($"{computerName} does not have a Computer_Base child.");
            return 0;
        }

        ConfigureMovableDevice(baseTransform.gameObject, "Computer_Base");
        ConfigureComputerInteractable(baseTransform.gameObject);
        GetOrCreateDropPoint(computerObject.transform, baseTransform);
        EditorUtility.SetDirty(computerObject);
        return 1;
    }

    private static void GetOrCreateDropPoint(Transform computer, Transform computerBase)
    {
        Transform existingDropPoint = computer.Find("Computer_Base_DropPoint");
        if (existingDropPoint != null)
        {
            ConfigureDropZone(existingDropPoint.gameObject);
            return;
        }

        GameObject dropPoint = new GameObject("Computer_Base_DropPoint");
        Undo.RegisterCreatedObjectUndo(dropPoint, "Create Computer Drop Point");
        dropPoint.transform.SetParent(computer);
        dropPoint.transform.position = computerBase.position;
        dropPoint.transform.rotation = computerBase.rotation;
        ConfigureDropZone(dropPoint);
    }

    private static void ConfigureMovableDevice(GameObject target, string deviceName)
    {
        if (target.GetComponentInChildren<Collider>() == null)
        {
            Undo.AddComponent<BoxCollider>(target);
        }

        MovableDevice movableDevice = target.GetComponent<MovableDevice>();
        if (movableDevice == null)
        {
            movableDevice = Undo.AddComponent<MovableDevice>(target);
        }

        SerializedObject serializedDevice = new SerializedObject(movableDevice);
        SerializedProperty deviceNameProperty = serializedDevice.FindProperty("deviceName");
        if (deviceNameProperty != null)
        {
            deviceNameProperty.stringValue = deviceName;
        }

        serializedDevice.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void ConfigureComputerInteractable(GameObject target)
    {
        if (target.GetComponent<ComputerInteractable>() == null)
        {
            Undo.AddComponent<ComputerInteractable>(target);
        }

        EditorUtility.SetDirty(target);
    }

    private static void ConfigureDropZone(GameObject target)
    {
        Collider collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider boxCollider = Undo.AddComponent<BoxCollider>(target);
            boxCollider.size = new Vector3(1f, 0.3f, 1f);
            collider = boxCollider;
        }

        Undo.RecordObject(collider, "Configure Device Drop Zone Collider");
        collider.isTrigger = true;

        if (target.GetComponent<DeviceDropZone>() == null)
        {
            Undo.AddComponent<DeviceDropZone>(target);
        }

        EditorUtility.SetDirty(target);
    }

    private static void MarkActiveSceneDirty()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
    }
}
