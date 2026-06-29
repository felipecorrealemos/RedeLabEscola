using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerMovementSetup
{
    private const string PreferredCarryAnchorName = "Anchor Carry";
    private const string CompactCarryAnchorName = "AnchorCarry";
    private const string LegacyCarryAnchorName = "CarryAnchor";

    [MenuItem("Tools/RedeLabEscola/Setup Player Movement")]
    public static void Setup()
    {
        GameObject player = Selection.activeGameObject;
        if (player != null && player.transform.parent != null)
        {
            player = player.transform.root.gameObject;
        }

        if (player == null || !player.name.Equals("Player", System.StringComparison.OrdinalIgnoreCase))
        {
            player = GameObject.Find("Player");
        }

        if (player == null)
        {
            player = GameObject.Find("player");
        }

        if (player == null)
        {
            Debug.LogWarning("Player object named 'Player' or 'player' not found in the current scene.");
            return;
        }

        Undo.RegisterCompleteObjectUndo(player, "Setup Player Movement");

        PlayerTopDownController controller = player.GetComponent<PlayerTopDownController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<PlayerTopDownController>(player);
        }

        Animator animator = player.GetComponentInChildren<Animator>();
        Transform carryAnchor = GetOrCreateCarryAnchor(player);
        SerializedObject serializedController = new SerializedObject(controller);
        SetProperty(serializedController, "animator", animator);
        SetProperty(serializedController, "carryAnchor", carryAnchor);
        SetProperty(serializedController, "walkSpeed", 2.2f);
        SetProperty(serializedController, "runSpeed", 4.0f);
        SetProperty(serializedController, "rotationSpeed", 12f);
        SetProperty(serializedController, "collisionRadius", 0.28f);
        SetProperty(serializedController, "collisionHeight", 1.45f);
        serializedController.ApplyModifiedProperties();

        EditorUtility.SetDirty(controller);
        NormalizeVisualChild(player, animator);

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
        }

        Debug.Log("Player movement setup complete.");
    }

    private static Transform GetOrCreateCarryAnchor(GameObject player)
    {
        Transform existingAnchor = player.transform.Find(PreferredCarryAnchorName);
        if (existingAnchor == null)
        {
            existingAnchor = player.transform.Find(CompactCarryAnchorName);
        }

        if (existingAnchor == null)
        {
            existingAnchor = player.transform.Find(LegacyCarryAnchorName);
        }

        if (existingAnchor != null)
        {
            return existingAnchor;
        }

        GameObject anchorObject = new GameObject(PreferredCarryAnchorName);
        Undo.RegisterCreatedObjectUndo(anchorObject, "Create Carry Anchor");
        Transform anchor = anchorObject.transform;
        anchor.SetParent(player.transform);
        anchor.localPosition = new Vector3(0f, 1.05f, 0.45f);
        anchor.localRotation = Quaternion.identity;
        anchor.localScale = Vector3.one;
        return anchor;
    }

    private static void NormalizeVisualChild(GameObject player, Animator animator)
    {
        if (animator == null || animator.gameObject == player)
        {
            return;
        }

        Undo.RecordObject(animator.transform, "Normalize Player Visual Child");
        animator.transform.localPosition = Vector3.zero;
        animator.transform.localRotation = Quaternion.identity;
        animator.transform.localScale = new Vector3(150f, 150f, 150f);
        EditorUtility.SetDirty(animator.transform);
    }

    private static void SetProperty(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetProperty(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }
}
