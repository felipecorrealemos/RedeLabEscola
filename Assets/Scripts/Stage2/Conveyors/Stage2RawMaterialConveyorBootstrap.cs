using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class Stage2RawMaterialConveyorBootstrap : MonoBehaviour
{
    private const string ConveyorRootName = "RawMaterialConveyor";
    private const string EntryConveyorName = "RawConveyor_Entry_Straight";
    private const string CurveConveyorName = "RawConveyor_Curve_90";
    private const string FinalConveyorName = "RawConveyor_Arm_Feed_Horizontal";
    private const string RuntimeSetupName = "Stage2_FirstProductionLine_RuntimeSetup";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureStage2RawMaterialConveyor()
    {
        GameObject root = GameObject.Find(ConveyorRootName);
        if (root == null)
        {
            return;
        }

        if (root.transform.Find(RuntimeSetupName) != null && root.GetComponent<ConveyorController>() != null && !NeedsRuntimeUpgrade(root.transform))
        {
            return;
        }

        ApplyToRoot(root.transform);
    }

    public static void ApplyToRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Transform setupMarker = GetOrCreateChild(root, RuntimeSetupName);
        setupMarker.localPosition = Vector3.zero;
        setupMarker.localRotation = Quaternion.identity;
        setupMarker.localScale = Vector3.one;

        Transform entry = root.Find(EntryConveyorName);
        Transform curve = root.Find(CurveConveyorName);
        Transform final = root.Find(FinalConveyorName);
        Transform machine = root.Find("RawMaterialMachine");

        if (entry == null || curve == null || final == null)
        {
            Debug.LogWarning("RawMaterialConveyor setup skipped because one or more conveyor visual objects were not found.", root);
            return;
        }

        ConveyorPath path = GetOrAddChildComponent<ConveyorPath>(root, "ConveyorPath");
        List<Transform> waypoints = BuildWaypoints(path.transform, entry, curve, final);
        path.ConfigureWaypoints(waypoints);

        ConveyorController controller = root.GetComponent<ConveyorController>();
        if (controller == null)
        {
            controller = root.gameObject.AddComponent<ConveyorController>();
        }

        Transform spawnPoint = GetOrCreateChild(machine != null ? machine : root, "SpawnPoint", out bool createdSpawnPoint);
        if (createdSpawnPoint)
        {
            spawnPoint.position = waypoints[0].position;
            spawnPoint.rotation = Quaternion.LookRotation((waypoints[1].position - waypoints[0].position).normalized, Vector3.up);
        }

        ConveyorItemSpawner spawner = spawnPoint.GetComponent<ConveyorItemSpawner>();
        if (spawner == null)
        {
            spawner = spawnPoint.gameObject.AddComponent<ConveyorItemSpawner>();
        }

        BoxCollider jamCollider = CreateOrUpdateZone(root, "JamSensorZone", GetJamCenter(waypoints), new Vector3(3.2f, 1.3f, 2.2f));
        ConveyorJamSensor jamSensor = jamCollider.GetComponent<ConveyorJamSensor>();
        if (jamSensor == null)
        {
            jamSensor = jamCollider.gameObject.AddComponent<ConveyorJamSensor>();
        }

        BoxCollider collectionCollider = CreateOrUpdateZone(root, "CollectionZone", waypoints[waypoints.Count - 1].position, new Vector3(1.6f, 1.2f, 1.6f));
        ConveyorCollectionZone collectionZone = collectionCollider.GetComponent<ConveyorCollectionZone>();
        if (collectionZone == null)
        {
            collectionZone = collectionCollider.gameObject.AddComponent<ConveyorCollectionZone>();
        }

        Transform collectionPoint = GetOrCreateChild(collectionCollider.transform, "CollectionPoint");
        collectionPoint.localPosition = Vector3.zero;
        collectionPoint.localRotation = Quaternion.identity;

        collectionZone.Configure(collectionPoint, collectionCollider, 22);
        collectionZone.ConfigureDualQueue(-0.22f, 0.22f, 0.72f, 11, QueueDistributionMode.ShortestQueue);
        collectionZone.SetPath(path);
        jamSensor.Configure(jamCollider, 3, 1f, 1, 1f);
        spawner.Configure(controller, path, spawnPoint, CreateDefaultProducts());
        spawner.EnsureMinimumActiveItems(80);
        controller.Configure(path, spawner, jamSensor, collectionZone);

        ConveyorControlPanelLights panelLights = GetOrAddControlPanelLights(root, machine);
        panelLights?.Configure(controller, jamSensor);
    }

    private static List<Transform> BuildWaypoints(Transform pathRoot, Transform entry, Transform curve, Transform final)
    {
        Bounds entryBounds = GetWorldBounds(entry);
        Bounds curveBounds = GetWorldBounds(curve);
        Bounds finalBounds = GetWorldBounds(final);
        float surfaceY = Mathf.Max(entryBounds.max.y, curveBounds.max.y, finalBounds.max.y) + 0.14f;

        float entryX = entryBounds.center.x;
        float entryStartZ = entryBounds.min.z + 0.35f;
        float entryEndZ = Mathf.Lerp(entryBounds.min.z, curveBounds.center.z, 0.78f);
        float finalZ = finalBounds.center.z;
        float finalStartX = Mathf.Min(curveBounds.center.x + 0.55f, finalBounds.min.x + 0.4f);
        float finalEndX = finalBounds.max.x - 0.35f;

        Vector3[] positions =
        {
            new Vector3(entryX, surfaceY, entryStartZ),
            new Vector3(entryX, surfaceY, Mathf.Lerp(entryStartZ, entryEndZ, 0.35f)),
            new Vector3(entryX, surfaceY, entryEndZ),
            new Vector3(curveBounds.center.x, surfaceY, curveBounds.center.z),
            new Vector3(finalStartX, surfaceY, finalZ),
            new Vector3(Mathf.Lerp(finalStartX, finalEndX, 0.55f), surfaceY, finalZ),
            new Vector3(finalEndX, surfaceY, finalZ)
        };

        string[] names =
        {
            "Point_00",
            "Point_01",
            "Point_02",
            "Point_03_Curve",
            "Point_04",
            "Point_05",
            "Point_06_End"
        };

        List<Transform> waypoints = new List<Transform>(positions.Length);
        for (int i = 0; i < positions.Length; i++)
        {
            Transform waypoint = GetOrCreateChild(pathRoot, names[i], out bool createdWaypoint);
            if (createdWaypoint)
            {
                waypoint.position = positions[i];
            }

            if (createdWaypoint && i < positions.Length - 1)
            {
                Vector3 direction = positions[i + 1] - positions[i];
                if (direction.sqrMagnitude > 0.0001f)
                {
                    waypoint.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            waypoints.Add(waypoint);
        }

        return waypoints;
    }

    private static Vector3 GetJamCenter(List<Transform> waypoints)
    {
        Vector3 beforeEnd = waypoints[Mathf.Max(0, waypoints.Count - 3)].position;
        Vector3 end = waypoints[waypoints.Count - 1].position;
        return Vector3.Lerp(beforeEnd, end, 0.55f);
    }

    private static bool NeedsRuntimeUpgrade(Transform root)
    {
        ConveyorJamSensor jamSensor = root.GetComponentInChildren<ConveyorJamSensor>();
        ConveyorCollectionZone collectionZone = root.GetComponentInChildren<ConveyorCollectionZone>();
        Transform machine = root.Find("RawMaterialMachine");
        Transform controlPanel = machine != null ? machine.Find("ControlPanel") : root.Find("ControlPanel");

        if (jamSensor == null || collectionZone == null)
        {
            return true;
        }

        return jamSensor.JamItemThreshold > 3
            || jamSensor.RestartItemThreshold > 1
            || jamSensor.StopConveyorOnJam
            || controlPanel == null
            || controlPanel.GetComponent<ConveyorControlPanelLights>() == null
            || !collectionZone.UseDualQueue
            || collectionZone.MaximumItemsPerQueue < 10
            || collectionZone.QueueItemSpacing < 0.7f
            || collectionZone.DistributionMode != QueueDistributionMode.ShortestQueue;
    }

    private static ConveyorControlPanelLights GetOrAddControlPanelLights(Transform root, Transform machine)
    {
        Transform controlPanel = machine != null ? machine.Find("ControlPanel") : null;
        if (controlPanel == null)
        {
            controlPanel = root.Find("ControlPanel");
        }

        if (controlPanel == null)
        {
            return null;
        }

        ConveyorControlPanelLights panelLights = controlPanel.GetComponent<ConveyorControlPanelLights>();
        return panelLights != null ? panelLights : controlPanel.gameObject.AddComponent<ConveyorControlPanelLights>();
    }

    private static List<ConveyorItemSpawner.ProductDefinition> CreateDefaultProducts()
    {
        return new List<ConveyorItemSpawner.ProductDefinition>
        {
            new ConveyorItemSpawner.ProductDefinition
            {
                productId = "RawMaterial_A",
                probabilityWeight = 1f,
                initialRotation = Vector3.zero,
                scale = new Vector3(0.46f, 0.26f, 0.46f)
            },
            new ConveyorItemSpawner.ProductDefinition
            {
                productId = "RawMaterial_B",
                probabilityWeight = 1f,
                initialRotation = new Vector3(0f, 30f, 0f),
                scale = new Vector3(0.56f, 0.22f, 0.38f)
            },
            new ConveyorItemSpawner.ProductDefinition
            {
                productId = "RawMaterial_C",
                probabilityWeight = 1f,
                initialRotation = new Vector3(0f, 60f, 0f),
                scale = new Vector3(0.36f, 0.34f, 0.5f)
            }
        };
    }

    private static T GetOrAddChildComponent<T>(Transform parent, string childName) where T : Component
    {
        Transform child = GetOrCreateChild(parent, childName);
        T component = child.GetComponent<T>();
        return component != null ? component : child.gameObject.AddComponent<T>();
    }

    private static Transform GetOrCreateChild(Transform parent, string childName)
    {
        return GetOrCreateChild(parent, childName, out _);
    }

    private static Transform GetOrCreateChild(Transform parent, string childName, out bool created)
    {
        Transform child = parent.Find(childName);
        if (child != null)
        {
            created = false;
            return child;
        }

        GameObject childObject = new GameObject(childName);
        child = childObject.transform;
        child.SetParent(parent, false);
        created = true;
        return child;
    }

    private static BoxCollider CreateOrUpdateZone(Transform parent, string zoneName, Vector3 worldCenter, Vector3 size)
    {
        Transform zone = GetOrCreateChild(parent, zoneName);
        zone.position = worldCenter;
        zone.rotation = Quaternion.identity;
        zone.localScale = Vector3.one;

        BoxCollider box = zone.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = zone.gameObject.AddComponent<BoxCollider>();
        }

        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = size;
        return box;
    }

    private static Bounds GetWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(root.position, Vector3.one);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return bounds;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return bounds;
    }
}
