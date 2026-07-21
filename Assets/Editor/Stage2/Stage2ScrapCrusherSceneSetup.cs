using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class Stage2ScrapCrusherSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Stage2/Stage2_Factory.unity";
    private static readonly Vector3 DefaultIntakeTriggerLocalPosition = new Vector3(0f, 1.2f, 0f);
    private static readonly Vector3 DefaultIntakeTriggerCenter = new Vector3(0.13759f, -0.7868f, -0.0042f);
    private static readonly Vector3 DefaultIntakeTriggerSize = new Vector3(0.938131f, 0.82507f, 0.57663f);
    private static readonly Vector3 DefaultDropCatchColliderCenter = new Vector3(0.13759f, -1.18f, -0.0042f);
    private static readonly Vector3 DefaultDropCatchColliderSize = new Vector3(0.938131f, 0.08f, 0.57663f);

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Scrap Crusher")]
    public static void ApplyToStage2Factory()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject crusherObject = GameObject.Find("triturador");
        if (crusherObject == null)
        {
            Debug.LogError("triturador not found in Stage2_Factory.");
            return;
        }

        Undo.SetCurrentGroupName("Setup Scrap Crusher");
        int undoGroup = Undo.GetCurrentGroup();

        Transform crusher = crusherObject.transform;
        Transform blades = FindChildRecursive(crusher, "laminas");
        Transform blades01 = FindChildRecursive(crusher, "laminas.001");
        Transform triggerTransform = GetOrCreateChild(crusher, "CrusherIntakeTrigger", DefaultIntakeTriggerLocalPosition);
        Undo.RecordObject(triggerTransform, "Set crusher intake trigger transform");
        triggerTransform.localPosition = DefaultIntakeTriggerLocalPosition;
        triggerTransform.localRotation = Quaternion.identity;
        triggerTransform.localScale = Vector3.one;
        BoxCollider trigger = GetOrAddComponent<BoxCollider>(triggerTransform.gameObject);
        Undo.RecordObject(trigger, "Set crusher intake trigger collider");
        trigger.isTrigger = true;
        trigger.center = DefaultIntakeTriggerCenter;
        trigger.size = DefaultIntakeTriggerSize;
        ConfigureDropCatchCollider(crusher);

        ScrapCrusherController controller = GetOrAddComponent<ScrapCrusherController>(crusherObject);
        controller.AssignReferences(blades, blades01, trigger, crusher);

        ConfigureScrapItems();

        EditorUtility.SetDirty(crusherObject);

        EditorSceneManager.MarkSceneDirty(crusherObject.scene);
        EditorSceneManager.SaveScene(crusherObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"Scrap crusher setup complete. Blades A: {(blades != null ? blades.name : "missing")}. Blades B: {(blades01 != null ? blades01.name : "missing")}.", crusherObject);
    }

    private static void ConfigureDropCatchCollider(Transform crusher)
    {
        bool created = false;
        Transform catchTransform = crusher.Find("CrusherDropCatchCollider");
        if (catchTransform == null)
        {
            catchTransform = GetOrCreateChild(crusher, "CrusherDropCatchCollider", DefaultIntakeTriggerLocalPosition);
            created = true;
        }

        BoxCollider catchCollider = GetOrAddComponent<BoxCollider>(catchTransform.gameObject);
        Undo.RecordObject(catchCollider, "Set crusher drop catch collider");
        catchCollider.isTrigger = false;
        if (created)
        {
            catchCollider.center = DefaultDropCatchColliderCenter;
            catchCollider.size = DefaultDropCatchColliderSize;
        }

        GetOrAddComponent<ScrapCrusherDropCatchCollider>(catchTransform.gameObject);
    }

    private static void ConfigureScrapItems()
    {
        Transform[] sceneObjects = Object.FindObjectsOfType<Transform>(true);
        for (int i = 0; i < sceneObjects.Length; i++)
        {
            Transform current = sceneObjects[i];
            if (current == null || !IsKnownScrapName(current.name))
            {
                continue;
            }

            if (current.GetComponent<ScrapItem>() == null)
            {
                Undo.AddComponent<ScrapItem>(current.gameObject);
            }

            ConfigureScrapPhysics(current.gameObject);
            EditorUtility.SetDirty(current.gameObject);
        }
    }

    private static void ConfigureScrapPhysics(GameObject scrapObject)
    {
        if (scrapObject.GetComponentInChildren<Collider>() == null)
        {
            Undo.AddComponent<BoxCollider>(scrapObject);
        }

        Rigidbody body = scrapObject.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = Undo.AddComponent<Rigidbody>(scrapObject);
        }

        body.isKinematic = true;
        body.useGravity = false;
        body.detectCollisions = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private static bool IsKnownScrapName(string objectName)
    {
        string normalized = objectName.Replace(" ", string.Empty).ToLowerInvariant();
        return normalized == "cube" || normalized == "entulho" || normalized == "entulho2";
    }

    private static Transform GetOrCreateChild(Transform parent, string childName, Vector3 localPosition)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject child = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(child, $"Create {childName}");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(target);
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }
}
