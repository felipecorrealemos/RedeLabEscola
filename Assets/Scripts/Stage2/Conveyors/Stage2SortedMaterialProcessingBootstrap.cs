using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public class Stage2SortedMaterialProcessingBootstrap : MonoBehaviour
{
    private const string SortedRootName = "SortedMaterialConveyors";
    private const string RuntimeSetupName = "Stage2_SortedMaterialProcessing_RuntimeSetup";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureStage2SortedMaterialProcessing()
    {
        GameObject root = GameObject.Find(SortedRootName);
        if (root == null)
        {
            return;
        }

        if (root.transform.Find(RuntimeSetupName) != null
            && root.GetComponentInChildren<ProcessingMachineController>() != null
            && HasRequiredRuntimeReceivers(root.transform))
        {
            return;
        }

        ApplyToRoot(root.transform, null);
    }

    public static void ApplyToRoot(Transform sortedRoot, GameObject processedPartPrefab)
    {
        if (sortedRoot == null)
        {
            return;
        }

        Transform setupMarker = GetOrCreateChild(sortedRoot, RuntimeSetupName);
        setupMarker.localPosition = Vector3.zero;
        setupMarker.localRotation = Quaternion.identity;
        setupMarker.localScale = Vector3.one;

        Transform processingMachine = FindDirectChild(sortedRoot, "ProcessingMachine");
        Transform pipesConveyor = FindDirectChild(sortedRoot, "Conveyor_Pipes");
        Transform beamsConveyor = FindDirectChild(sortedRoot, "Conveyor_Beams");
        Transform ingotsConveyor = FindDirectChild(sortedRoot, "Conveyor_Ingots");
        Transform outputSegmentA = FindDirectChild(sortedRoot, "Conveyor_Pipes (1)");
        Transform outputSegmentB = FindDirectChild(sortedRoot, "Conveyor_Pipes (2)");
        Transform outputSegmentC = FindDirectChild(sortedRoot, "Conveyor_Pipes (3)");

        if (processingMachine == null || pipesConveyor == null || beamsConveyor == null || ingotsConveyor == null)
        {
            Debug.LogWarning("Sorted material processing setup skipped because an input conveyor or ProcessingMachine was not found.", sortedRoot);
            return;
        }

        ProcessingMachineController machineController = processingMachine.GetComponent<ProcessingMachineController>();
        if (machineController == null)
        {
            machineController = processingMachine.gameObject.AddComponent<ProcessingMachineController>();
        }

        Transform inputPipes = FindChildRecursive(processingMachine, "Input_Pipes");
        Transform inputBeams = FindChildRecursive(processingMachine, "Input_Beams");
        Transform inputIngots = FindChildRecursive(processingMachine, "Input_Ingots");
        Transform outputAnchor = FindChildRecursive(processingMachine, "Output_FinishedParts_Central");

        ConveyorController pipesController = ConfigureInputConveyor(
            pipesConveyor,
            processingMachine,
            inputPipes,
            "Pipes",
            RoboticArmProductType.Pipes,
            "RawMaterial_A",
            machineController);

        ConveyorController beamsController = ConfigureInputConveyor(
            beamsConveyor,
            processingMachine,
            inputBeams,
            "Beams",
            RoboticArmProductType.Beams,
            "RawMaterial_B",
            machineController);

        ConveyorController ingotsController = ConfigureInputConveyor(
            ingotsConveyor,
            processingMachine,
            inputIngots,
            "Ingots",
            RoboticArmProductType.Ingots,
            "RawMaterial_C",
            machineController);

        ConveyorController outputController = null;
        Transform outputSpawnPoint = null;
        ConveyorJamSensor outputJamSensor = null;
        if (outputSegmentA != null && outputSegmentB != null && outputSegmentC != null)
        {
            outputController = ConfigureOutputConveyor(sortedRoot, processingMachine, outputAnchor, outputSegmentA, outputSegmentB, outputSegmentC, out outputSpawnPoint, out outputJamSensor);
        }
        else
        {
            Debug.LogWarning("Processing output conveyor setup skipped because one or more Conveyor_Pipes output segments were not found.", sortedRoot);
        }

        machineController.Configure(outputController, outputSpawnPoint, processedPartPrefab);
        machineController.ConfigureOutputJamSensor(outputJamSensor);
        ConfigureRoboticArm("RoboticArm_Pipes", RoboticArmProductType.Pipes, "RawMaterial_A", pipesController);
        ConfigureRoboticArm("RoboticArm_Beams", RoboticArmProductType.Beams, "RawMaterial_B", beamsController);
        ConfigureRoboticArm("RoboticArm_Ingots", RoboticArmProductType.Ingots, "RawMaterial_C", ingotsController);
    }

    private static ConveyorController ConfigureInputConveyor(
        Transform conveyorRoot,
        Transform processingMachine,
        Transform machineInput,
        string label,
        RoboticArmProductType itemType,
        string productId,
        ProcessingMachineController machineController)
    {
        Transform logic = GetOrCreateChild(conveyorRoot, "Logic");
        ConveyorPath path = GetOrAddChildComponent<ConveyorPath>(logic, label + "Path");
        List<Transform> waypoints = BuildInputWaypoints(path.transform, conveyorRoot, processingMachine, machineInput, label);
        path.ConfigureWaypoints(waypoints);

        ConveyorController controller = conveyorRoot.GetComponent<ConveyorController>();
        if (controller == null)
        {
            controller = conveyorRoot.gameObject.AddComponent<ConveyorController>();
        }

        Vector3 endPosition = waypoints[waypoints.Count - 1].position;
        BoxCollider inputCollider = CreateOrUpdateZone(logic, label + "MachineInputZone", endPosition, new Vector3(1.1f, 1f, 1.1f));
        ConveyorCollectionZone collectionZone = inputCollider.GetComponent<ConveyorCollectionZone>();
        if (collectionZone == null)
        {
            collectionZone = inputCollider.gameObject.AddComponent<ConveyorCollectionZone>();
        }

        Transform inputPoint = GetOrCreateChild(inputCollider.transform, label + "InputPoint");
        inputPoint.localPosition = Vector3.zero;
        inputPoint.localRotation = Quaternion.identity;

        collectionZone.ConfigureSingleStop(inputPoint, inputCollider);
        collectionZone.SetPath(path);
        controller.Configure(path, null, null, collectionZone);
        controller.ConfigureSpacing(1.05f, 2.1f, 0.85f, 1.05f, 0.8f);

        ProcessingMachineInputConsumer consumer = inputCollider.GetComponent<ProcessingMachineInputConsumer>();
        if (consumer == null)
        {
            consumer = inputCollider.gameObject.AddComponent<ProcessingMachineInputConsumer>();
        }

        consumer.Configure(machineController, controller, collectionZone, itemType, productId);
        ConfigureMachineInputTransform(machineInput, machineController, controller, itemType, productId);
        return controller;
    }

    private static void ConfigureMachineInputTransform(
        Transform machineInput,
        ProcessingMachineController machineController,
        ConveyorController sourceController,
        RoboticArmProductType itemType,
        string productId)
    {
        if (machineInput == null)
        {
            return;
        }

        Collider inputCollider = machineInput.GetComponent<Collider>();
        if (inputCollider == null)
        {
            inputCollider = machineInput.gameObject.AddComponent<BoxCollider>();
        }

        inputCollider.isTrigger = true;

        ProcessingMachineInputConsumer consumer = machineInput.GetComponent<ProcessingMachineInputConsumer>();
        if (consumer == null)
        {
            consumer = machineInput.gameObject.AddComponent<ProcessingMachineInputConsumer>();
        }

        consumer.Configure(machineController, sourceController, null, itemType, productId);
    }

    private static ConveyorController ConfigureOutputConveyor(
        Transform sortedRoot,
        Transform processingMachine,
        Transform outputAnchor,
        Transform segmentA,
        Transform segmentB,
        Transform segmentC,
        out Transform outputSpawnPoint,
        out ConveyorJamSensor outputJamSensor)
    {
        Transform outputRoot = GetOrCreateChild(sortedRoot, "ProcessingOutputConveyor");
        Transform logic = GetOrCreateChild(outputRoot, "Logic");
        ConveyorPath path = GetOrAddChildComponent<ConveyorPath>(logic, "OutputPath");
        List<Transform> waypoints = BuildOutputWaypoints(path.transform, processingMachine, outputAnchor, segmentA, segmentB, segmentC);
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
        Vector3 jamCenter = Vector3.Lerp(waypoints[Mathf.Max(0, waypoints.Count - 3)].position, endPosition, 0.55f);
        BoxCollider jamCollider = CreateOrUpdateZone(logic, "OutputJamSensorZone", jamCenter, new Vector3(3.6f, 1.2f, 2.4f));
        outputJamSensor = jamCollider.GetComponent<ConveyorJamSensor>();
        if (outputJamSensor == null)
        {
            outputJamSensor = jamCollider.gameObject.AddComponent<ConveyorJamSensor>();
        }

        outputJamSensor.Configure(jamCollider, 8, 1f, 4, 1f);
        outputJamSensor.SetCountOnlyStoppedItems(false);

        BoxCollider endCollider = CreateOrUpdateZone(logic, "EndCollectionZone", endPosition, new Vector3(2.2f, 1f, 2.6f));
        ConveyorCollectionZone collectionZone = endCollider.GetComponent<ConveyorCollectionZone>();
        if (collectionZone == null)
        {
            collectionZone = endCollider.gameObject.AddComponent<ConveyorCollectionZone>();
        }

        Transform endPoint = GetOrCreateChild(endCollider.transform, "EndCollectionPoint");
        endPoint.localPosition = Vector3.zero;
        endPoint.localRotation = Quaternion.identity;

        collectionZone.Configure(endPoint, endCollider, 24);
        collectionZone.ConfigureDualQueue(-0.28f, 0.28f, 1.05f, 12, QueueDistributionMode.ShortestQueue);
        collectionZone.SetPath(path);
        controller.Configure(path, null, outputJamSensor, collectionZone);
        controller.ConfigureSpacing(1.05f, 2.2f, 0.85f, 1.05f, 0.8f);
        return controller;
    }

    private static List<Transform> BuildInputWaypoints(Transform pathRoot, Transform conveyorRoot, Transform processingMachine, Transform machineInput, string label)
    {
        Bounds conveyorBounds = GetWorldBounds(conveyorRoot);
        Bounds machineBounds = GetWorldBounds(processingMachine);
        float surfaceY = conveyorBounds.max.y + 0.12f;
        Vector3 dropPosition = GetArmDropPosition(label);
        Vector3 start = dropPosition != Vector3.zero ? ProjectToSurface(dropPosition, surfaceY) : ProjectToSurface(conveyorBounds.center, surfaceY);
        Vector3 end = machineInput != null ? ProjectToSurface(machineInput.position, surfaceY) : ProjectToSurface(GetClosestPointOnBounds(machineBounds, conveyorBounds.center), surfaceY);
        Vector3 middle = ProjectToSurface(conveyorBounds.center, surfaceY);

        Vector3[] positions =
        {
            start,
            Vector3.Lerp(start, middle, 0.5f),
            middle,
            Vector3.Lerp(middle, end, 0.5f),
            end
        };

        return EnsureWaypoints(pathRoot, positions, new[]
        {
            "Point_00_Receive",
            "Point_01",
            "Point_02",
            "Point_03",
            "Point_04_Input"
        });
    }

    private static List<Transform> BuildOutputWaypoints(Transform pathRoot, Transform processingMachine, Transform outputAnchor, Transform segmentA, Transform segmentB, Transform segmentC)
    {
        Bounds a = GetWorldBounds(segmentA);
        Bounds b = GetWorldBounds(segmentB);
        Bounds c = GetWorldBounds(segmentC);
        float surfaceY = Mathf.Max(a.max.y, b.max.y, c.max.y) + 0.12f;
        Vector3 start = outputAnchor != null ? ProjectToSurface(outputAnchor.position, surfaceY) : ProjectToSurface(GetWorldBounds(processingMachine).center, surfaceY);
        Vector3 end = ProjectToSurface(GetFarthestBoundsCorner(c, start), surfaceY);

        Vector3[] positions =
        {
            start,
            ProjectToSurface(a.center, surfaceY),
            ProjectToSurface(b.center, surfaceY),
            ProjectToSurface(c.center, surfaceY),
            end
        };

        return EnsureWaypoints(pathRoot, positions, new[]
        {
            "Point_00_Output",
            "Point_01",
            "Point_02_Curve",
            "Point_03",
            "Point_04_End"
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

    private static void ConfigureRoboticArm(string armName, RoboticArmProductType type, string productId, ConveyorController destination)
    {
        GameObject armObject = GameObject.Find(armName);
        RoboticArmController arm = armObject != null ? armObject.GetComponent<RoboticArmController>() : null;
        if (arm != null)
        {
            arm.ConfigureProduct(type, productId, destination);
            if (armName == "RoboticArm_Ingots")
            {
                arm.ConfigureAnyProductFallback(true, 0f);
            }
        }
    }

    private static Vector3 GetArmDropPosition(string label)
    {
        string armName = "RoboticArm_" + label;
        GameObject armObject = GameObject.Find(armName);
        Transform dropPoint = armObject != null ? FindChildRecursive(armObject.transform, "DropPoint") : null;
        return dropPoint != null ? dropPoint.position : Vector3.zero;
    }

    private static Vector3 ProjectToSurface(Vector3 position, float surfaceY)
    {
        return new Vector3(position.x, surfaceY, position.z);
    }

    private static Vector3 GetClosestPointOnBounds(Bounds bounds, Vector3 position)
    {
        return bounds.ClosestPoint(position);
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

    private static bool HasRequiredRuntimeReceivers(Transform root)
    {
        Transform processingMachine = FindDirectChild(root, "ProcessingMachine");
        if (processingMachine == null)
        {
            return false;
        }

        return HasInputConsumer(processingMachine, "Input_Pipes")
            && HasInputConsumer(processingMachine, "Input_Beams")
            && HasInputConsumer(processingMachine, "Input_Ingots")
            && HasOutputJamSensor(root);
    }

    private static bool HasInputConsumer(Transform processingMachine, string inputName)
    {
        Transform input = FindChildRecursive(processingMachine, inputName);
        return input != null && input.GetComponent<ProcessingMachineInputConsumer>() != null;
    }

    private static bool HasOutputJamSensor(Transform root)
    {
        Transform outputConveyor = FindDirectChild(root, "ProcessingOutputConveyor");
        return outputConveyor != null && outputConveyor.GetComponentInChildren<ConveyorJamSensor>() != null;
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
