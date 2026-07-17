using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-80)]
public class Stage2PackagingMachineBootstrap : MonoBehaviour
{
    private const string MachineRootName = "PackagingMachine_Boxes";
    private const string RuntimeSetupName = "Stage2_PackagingMachine_RuntimeSetup";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureStage2PackagingMachine()
    {
        GameObject root = GameObject.Find(MachineRootName);
        if (root == null)
        {
            return;
        }

        if (root.transform.Find(RuntimeSetupName) != null
            && root.GetComponent<PackagingMachineController>() != null
            && root.transform.Find("PackagingOutputConveyor") != null)
        {
            return;
        }

        ApplyToRoot(root.transform, null);
    }

    public static void ApplyToRoot(Transform root, GameObject boxPrefab)
    {
        if (root == null)
        {
            return;
        }

        Transform setupMarker = GetOrCreateChild(root, RuntimeSetupName);
        setupMarker.localPosition = Vector3.zero;
        setupMarker.localRotation = Quaternion.identity;
        setupMarker.localScale = Vector3.one;

        PackagingMachineController machine = root.GetComponent<PackagingMachineController>();
        if (machine == null)
        {
            machine = root.gameObject.AddComponent<PackagingMachineController>();
        }

        Transform tunnelInput = FindChildRecursive(root, "TunnelOpening_Input");
        Transform tunnelOutput = FindChildRecursive(root, "TunnelOpening_Output");
        Transform central = FindDirectChild(root, "PackagingPartsConveyor_Central");
        Transform curve = FindDirectChild(root, "PackagingPartsConveyor_Curve");
        Transform final = FindDirectChild(root, "PackagingPartsConveyor_Central (1)");
        if (final == null)
        {
            final = FindDirectChild(root, "PackagingPartsConveyor_Central (2)");
        }

        PackagingMachineInputConsumer inputConsumer = ConfigureInputTrigger(root, machine, tunnelInput);
        ConveyorController outputConveyor = null;
        Transform outputSpawnPoint = null;
        ConveyorJamSensor outputJamSensor = null;

        if (central != null && curve != null && final != null)
        {
            outputConveyor = ConfigureOutputConveyor(root, tunnelOutput, central, curve, final, out outputSpawnPoint, out outputJamSensor);
        }
        else
        {
            Debug.LogWarning("PackagingMachine_Boxes output conveyor setup skipped because one or more visual conveyor sections were not found.", root);
        }

        machine.Configure(outputConveyor, outputSpawnPoint, outputJamSensor, boxPrefab);
        inputConsumer?.Configure(machine);
    }

    private static PackagingMachineInputConsumer ConfigureInputTrigger(Transform root, PackagingMachineController machine, Transform tunnelInput)
    {
        Transform logic = GetOrCreateChild(root, "Logic");
        Vector3 inputPosition = tunnelInput != null ? tunnelInput.position : GetWorldBounds(root).center;
        BoxCollider inputCollider = CreateOrUpdateZone(logic, "PackagingInputTrigger", inputPosition, new Vector3(1.4f, 1.2f, 1.4f));
        PackagingMachineInputConsumer consumer = inputCollider.GetComponent<PackagingMachineInputConsumer>();
        if (consumer == null)
        {
            consumer = inputCollider.gameObject.AddComponent<PackagingMachineInputConsumer>();
        }

        consumer.Configure(machine);
        return consumer;
    }

    private static ConveyorController ConfigureOutputConveyor(
        Transform root,
        Transform tunnelOutput,
        Transform central,
        Transform curve,
        Transform final,
        out Transform outputSpawnPoint,
        out ConveyorJamSensor outputJamSensor)
    {
        Transform outputRoot = GetOrCreateChild(root, "PackagingOutputConveyor");
        Transform logic = GetOrCreateChild(outputRoot, "Logic");
        ConveyorPath path = GetOrAddChildComponent<ConveyorPath>(logic, "PackagingOutputPath");
        List<Transform> waypoints = BuildOutputWaypoints(path.transform, root, tunnelOutput, central, curve, final);
        path.ConfigureWaypoints(waypoints);

        ConveyorController controller = outputRoot.GetComponent<ConveyorController>();
        if (controller == null)
        {
            controller = outputRoot.gameObject.AddComponent<ConveyorController>();
        }

        outputSpawnPoint = GetOrCreateChild(logic, "OutputSpawnPoint", out bool createdSpawnPoint);
        if (createdSpawnPoint)
        {
            outputSpawnPoint.position = waypoints[0].position;
            outputSpawnPoint.rotation = Quaternion.LookRotation((waypoints[1].position - waypoints[0].position).normalized, Vector3.up);
        }

        Vector3 endPosition = waypoints[waypoints.Count - 1].position;
        Vector3 jamCenter = Vector3.Lerp(waypoints[Mathf.Max(0, waypoints.Count - 3)].position, endPosition, 0.65f);
        BoxCollider jamCollider = CreateOrUpdateZone(logic, "OutputJamSensorZone", jamCenter, new Vector3(3.4f, 1.2f, 2.4f));
        outputJamSensor = jamCollider.GetComponent<ConveyorJamSensor>();
        if (outputJamSensor == null)
        {
            outputJamSensor = jamCollider.gameObject.AddComponent<ConveyorJamSensor>();
        }

        outputJamSensor.Configure(jamCollider, 6, 1f, 3, 1f);
        outputJamSensor.SetCountOnlyStoppedItems(false);

        BoxCollider collectionCollider = CreateOrUpdateZone(logic, "EndCollectionZone", endPosition, new Vector3(2.4f, 1.2f, 2.6f));
        ConveyorCollectionZone collectionZone = collectionCollider.GetComponent<ConveyorCollectionZone>();
        if (collectionZone == null)
        {
            collectionZone = collectionCollider.gameObject.AddComponent<ConveyorCollectionZone>();
        }

        Transform collectionPoint = GetOrCreateChild(collectionCollider.transform, "EndCollectionPoint");
        collectionPoint.localPosition = Vector3.zero;
        collectionPoint.localRotation = Quaternion.identity;

        collectionZone.Configure(collectionPoint, collectionCollider, 16);
        collectionZone.ConfigureSingleQueue(0f, 1.65f, 10);
        collectionZone.SetQueueRotation(false);
        collectionZone.SetPath(path);

        controller.Configure(path, null, outputJamSensor, collectionZone);
        controller.ConfigureSpacing(1.65f, 3.1f, 1.05f, 1.65f, 1.05f);
        return controller;
    }

    private static List<Transform> BuildOutputWaypoints(Transform pathRoot, Transform root, Transform tunnelOutput, Transform central, Transform curve, Transform final)
    {
        Bounds centralBounds = GetWorldBounds(central);
        Bounds curveBounds = GetWorldBounds(curve);
        Bounds finalBounds = GetWorldBounds(final);
        float surfaceY = Mathf.Max(centralBounds.max.y, curveBounds.max.y, finalBounds.max.y) + 0.14f;

        Vector3 start = tunnelOutput != null ? ProjectToSurface(tunnelOutput.position, surfaceY) : ProjectToSurface(centralBounds.center, surfaceY);
        Vector3 centralPoint = ProjectToSurface(centralBounds.center, surfaceY);
        Vector3 curvePoint = ProjectToSurface(curveBounds.center, surfaceY);
        Vector3 finalPoint = ProjectToSurface(finalBounds.center, surfaceY);
        Vector3 end = ProjectToSurface(GetFarthestBoundsCorner(finalBounds, start), surfaceY);

        Vector3[] positions =
        {
            start,
            Vector3.Lerp(start, centralPoint, 0.55f),
            centralPoint,
            curvePoint,
            finalPoint,
            end
        };

        return EnsureWaypoints(pathRoot, positions, new[]
        {
            "Point_00_Output",
            "Point_01",
            "Point_02",
            "Point_03_Curve",
            "Point_04",
            "Point_05_End"
        });
    }

    private static List<Transform> EnsureWaypoints(Transform pathRoot, Vector3[] positions, string[] names)
    {
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

    private static Vector3 ProjectToSurface(Vector3 position, float surfaceY)
    {
        return new Vector3(position.x, surfaceY, position.z);
    }

    private static Vector3 GetFarthestBoundsCorner(Bounds bounds, Vector3 from)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        Vector3[] corners =
        {
            new Vector3(min.x, bounds.center.y, min.z),
            new Vector3(min.x, bounds.center.y, max.z),
            new Vector3(max.x, bounds.center.y, min.z),
            new Vector3(max.x, bounds.center.y, max.z)
        };

        Vector3 farthest = corners[0];
        float bestDistance = (farthest - from).sqrMagnitude;
        for (int i = 1; i < corners.Length; i++)
        {
            float distance = (corners[i] - from).sqrMagnitude;
            if (distance > bestDistance)
            {
                bestDistance = distance;
                farthest = corners[i];
            }
        }

        return farthest;
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
        Transform zone = GetOrCreateChild(parent, zoneName, out bool created);

        BoxCollider box = zone.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = zone.gameObject.AddComponent<BoxCollider>();
        }

        box.isTrigger = true;
        if (created)
        {
            zone.position = worldCenter;
            zone.rotation = Quaternion.identity;
            zone.localScale = Vector3.one;
            box.center = Vector3.zero;
            box.size = size;
        }

        return box;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
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
