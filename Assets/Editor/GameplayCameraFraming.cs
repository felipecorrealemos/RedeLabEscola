using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayCameraFraming
{
    private const float OrthographicSize = 4.6f;

    [MenuItem("Tools/RedeLabEscola/Setup Dead Zone Camera Follow")]
    public static void SetupDeadZoneCameraFollow()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("Main Camera not found.");
            return;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        if (player == null)
        {
            Debug.LogWarning("Player object not found.");
            return;
        }

        DeadZoneCameraFollow follow = camera.GetComponent<DeadZoneCameraFollow>();
        if (follow == null)
        {
            follow = Undo.AddComponent<DeadZoneCameraFollow>(camera.gameObject);
        }

        SerializedObject serializedFollow = new SerializedObject(follow);
        SetProperty(serializedFollow, "target", player.transform);
        serializedFollow.ApplyModifiedProperties();

        EditorUtility.SetDirty(follow);
        MarkActiveSceneDirty();

        Debug.Log("Dead zone camera follow configured.");
    }

    [MenuItem("Tools/RedeLabEscola/Frame Gameplay Camera")]
    public static void FrameGameplayCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("Main Camera not found.");
            return;
        }

        Undo.RecordObject(camera, "Frame Gameplay Camera");
        Undo.RecordObject(camera.transform, "Frame Gameplay Camera");

        camera.orthographic = true;
        camera.orthographicSize = OrthographicSize;
        camera.transform.position = new Vector3(0f, 16f, -12f);
        camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

        EditorUtility.SetDirty(camera);
        EditorUtility.SetDirty(camera.transform);

        MarkActiveSceneDirty();

        Debug.Log("Gameplay camera framed closer.");
    }

    private static void MarkActiveSceneDirty()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }
    }

    private static void SetProperty(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }
}
