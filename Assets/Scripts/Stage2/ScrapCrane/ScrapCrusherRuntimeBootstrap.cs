using UnityEngine;

public static class ScrapCrusherRuntimeBootstrap
{
    private static readonly Vector3 DefaultIntakeTriggerLocalPosition = new Vector3(0f, 1.2f, 0f);
    private static readonly Vector3 DefaultIntakeTriggerCenter = new Vector3(0.13759f, -0.7868f, -0.0042f);
    private static readonly Vector3 DefaultIntakeTriggerSize = new Vector3(0.938131f, 0.82507f, 0.57663f);
    private static readonly Vector3 DefaultDropCatchColliderCenter = new Vector3(0.13759f, -1.18f, -0.0042f);
    private static readonly Vector3 DefaultDropCatchColliderSize = new Vector3(0.938131f, 0.08f, 0.57663f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureCrusherIfPresent()
    {
        GameObject crusherObject = GameObject.Find("triturador");
        if (crusherObject == null)
        {
            return;
        }

        Transform crusher = crusherObject.transform;
        Transform blades = FindChildRecursive(crusher, "laminas");
        Transform blades01 = FindChildRecursive(crusher, "laminas.001");
        bool createdTrigger = crusher.Find("CrusherIntakeTrigger") == null;
        Transform triggerTransform = GetOrCreateChild(crusher, "CrusherIntakeTrigger", DefaultIntakeTriggerLocalPosition);
        BoxCollider trigger = triggerTransform.GetComponent<BoxCollider>();
        bool createdCollider = trigger == null;
        if (trigger == null)
        {
            trigger = triggerTransform.gameObject.AddComponent<BoxCollider>();
        }

        trigger.isTrigger = true;
        if (createdTrigger || createdCollider)
        {
            trigger.center = DefaultIntakeTriggerCenter;
            trigger.size = DefaultIntakeTriggerSize;
        }
        ConfigureDropCatchCollider(crusher);

        ScrapCrusherController controller = crusherObject.GetComponent<ScrapCrusherController>();
        if (controller == null)
        {
            controller = crusherObject.AddComponent<ScrapCrusherController>();
        }

        controller.AssignReferences(blades, blades01, trigger, crusher);

        ConfigureScrapItems();
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

        BoxCollider catchCollider = catchTransform.GetComponent<BoxCollider>();
        if (catchCollider == null)
        {
            catchCollider = catchTransform.gameObject.AddComponent<BoxCollider>();
            created = true;
        }

        catchCollider.isTrigger = false;
        if (created)
        {
            catchCollider.center = DefaultDropCatchColliderCenter;
            catchCollider.size = DefaultDropCatchColliderSize;
        }

        if (catchTransform.GetComponent<ScrapCrusherDropCatchCollider>() == null)
        {
            catchTransform.gameObject.AddComponent<ScrapCrusherDropCatchCollider>();
        }
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
                current.gameObject.AddComponent<ScrapItem>();
            }

            ConfigureScrapPhysics(current.gameObject);
        }
    }

    private static void ConfigureScrapPhysics(GameObject scrapObject)
    {
        if (scrapObject.GetComponentInChildren<Collider>() == null)
        {
            scrapObject.AddComponent<BoxCollider>();
        }

        Rigidbody body = scrapObject.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = scrapObject.AddComponent<Rigidbody>();
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
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;
        return child.transform;
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
