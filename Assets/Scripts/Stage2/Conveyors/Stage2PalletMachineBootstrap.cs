using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-75)]
public class Stage2PalletMachineBootstrap : MonoBehaviour
{
    private const string MachineRootName = "PalletMachine";
    private const string RuntimeSetupName = "Stage2_PalletMachine_RuntimeSetup";
    private const string AcceptedBoxProductId = "PackedBox";
    private const string PalletProductId = "PalletWithBoxes";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureStage2PalletMachine()
    {
        GameObject root = GameObject.Find(MachineRootName);
        if (root == null)
        {
            return;
        }

        if (root.transform.Find(RuntimeSetupName) != null
            && root.GetComponent<PackagingMachineController>() != null
            && root.transform.Find("PalletOutputConveyor") != null)
        {
            return;
        }

        ApplyToRoot(root.transform, null);
    }

    public static void ApplyToRoot(Transform root, GameObject palletPrefab)
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

        Transform finishConveyor = FindChildRecursive(root, "FinishPartConveyor");
        if (finishConveyor == null)
        {
            finishConveyor = FindChildRecursive(root, "FinishedPartsConveyor");
        }

        if (finishConveyor == null)
        {
            finishConveyor = FindChildRecursive(root, "FinishedPartsConveyor_Central");
        }

        if (finishConveyor == null)
        {
            finishConveyor = FindChildRecursive(root, "Finish Part Conveyor");
        }

        PackagingMachineInputConsumer inputConsumer = ConfigureInputTrigger(root, machine, finishConveyor);
        ConveyorController outputConveyor = null;
        Transform outputSpawnPoint = null;

        if (finishConveyor != null)
        {
            outputConveyor = ConfigureOutputConveyor(root, finishConveyor, out outputSpawnPoint);
        }
        else
        {
            Debug.LogWarning("PalletMachine output conveyor setup skipped because FinishPartConveyor was not found.", root);
        }

        machine.ConfigureInput(AcceptedBoxProductId, 3, 9);
        machine.ConfigureOutputProduct(PalletProductId, palletPrefab);
        machine.ConfigureOutputScale(true, Vector3.zero);
        machine.Configure(outputConveyor, outputSpawnPoint, null, palletPrefab);
        inputConsumer?.Configure(machine);
    }

    private static PackagingMachineInputConsumer ConfigureInputTrigger(Transform root, PackagingMachineController machine, Transform finishConveyor)
    {
        Transform logic = GetOrCreateChild(root, "Logic");
        Bounds rootBounds = GetWorldBounds(root);
        Bounds conveyorBounds = finishConveyor != null ? GetWorldBounds(finishConveyor) : rootBounds;
        Vector3 inputPosition = GetHorizontalEndpoint(conveyorBounds, rootBounds.center, true);

        BoxCollider inputCollider = CreateOrUpdateZone(logic, "PalletInputTrigger", inputPosition, new Vector3(1.8f, 1.4f, 1.8f));
        PackagingMachineInputConsumer consumer = inputCollider.GetComponent<PackagingMachineInputConsumer>();
        if (consumer == null)
        {
            consumer = inputCollider.gameObject.AddComponent<PackagingMachineInputConsumer>();
        }

        consumer.Configure(machine);
        return consumer;
    }

    private static ConveyorController ConfigureOutputConveyor(Transform root, Transform finishConveyor, out Transform outputSpawnPoint)
    {
        Transform outputRoot = GetOrCreateChild(root, "PalletOutputConveyor");
        Transform logic = GetOrCreateChild(outputRoot, "Logic");
        ConveyorPath path = GetOrAddChildComponent<ConveyorPath>(logic, "PalletOutputPath");

        Bounds conveyorBounds = GetWorldBounds(finishConveyor);
        Bounds rootBounds = GetWorldBounds(root);
        List<Transform> waypoints = BuildOutputWaypoints(path.transform, conveyorBounds, rootBounds.center);
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

        controller.Configure(path, null, null, null);
        controller.ConfigureSpacing(2.2f, 4.2f, 1.3f, 2.2f, 1.25f);
        controller.ConfigureEndBehavior(true);
        return controller;
    }

    private static List<Transform> BuildOutputWaypoints(Transform pathRoot, Bounds conveyorBounds, Vector3 inputReference)
    {
        Vector3 axis = conveyorBounds.size.x >= conveyorBounds.size.z ? Vector3.right : Vector3.forward;
        float halfLength = Mathf.Max(conveyorBounds.extents.x, conveyorBounds.extents.z);
        float surfaceY = conveyorBounds.max.y + 0.16f;

        Vector3 endpointA = ProjectToSurface(conveyorBounds.center - axis * halfLength, surfaceY);
        Vector3 endpointB = ProjectToSurface(conveyorBounds.center + axis * halfLength, surfaceY);
        Vector3 start = (endpointA - inputReference).sqrMagnitude <= (endpointB - inputReference).sqrMagnitude ? endpointA : endpointB;
        Vector3 end = start == endpointA ? endpointB : endpointA;
        Vector3 direction = (end - start).sqrMagnitude > 0.0001f ? (end - start).normalized : Vector3.forward;
        Vector3 exit = end + direction * 3f;

        Vector3[] positions =
        {
            start,
            Vector3.Lerp(start, end, 0.45f),
            end,
            exit
        };

        return EnsureWaypoints(pathRoot, positions, new[]
        {
            "Point_00_Output",
            "Point_01",
            "Point_02_EndOfBelt",
            "Point_03_Exit"
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

    private static Vector3 GetHorizontalEndpoint(Bounds bounds, Vector3 reference, bool closestToReference)
    {
        Vector3 axis = bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
        float halfLength = Mathf.Max(bounds.extents.x, bounds.extents.z);
        Vector3 endpointA = new Vector3(bounds.center.x, bounds.max.y + 0.16f, bounds.center.z) - axis * halfLength;
        Vector3 endpointB = new Vector3(bounds.center.x, bounds.max.y + 0.16f, bounds.center.z) + axis * halfLength;
        bool aIsCloser = (endpointA - reference).sqrMagnitude <= (endpointB - reference).sqrMagnitude;
        return closestToReference == aIsCloser ? endpointA : endpointB;
    }

    private static Vector3 ProjectToSurface(Vector3 position, float surfaceY)
    {
        return new Vector3(position.x, surfaceY, position.z);
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
