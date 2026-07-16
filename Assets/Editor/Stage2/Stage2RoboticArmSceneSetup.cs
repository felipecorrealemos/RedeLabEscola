using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage2RoboticArmSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Stage2/Stage2_Factory.unity";

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Robotic Arms")]
    public static void ApplyToStage2Factory()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        ConfigureArm("RoboticArm_Pipes", RoboticArmProductType.Pipes, "RawMaterial_A");
        ConfigureArm("RoboticArm_Beams", RoboticArmProductType.Beams, "RawMaterial_B");
        ConfigureArm("RoboticArm_Ingots", RoboticArmProductType.Ingots, "RawMaterial_C");

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Robotic arms configured. Review points, poses and destination conveyors before saving the scene.");
    }

    private static void ConfigureArm(string armName, RoboticArmProductType productType, string productId)
    {
        GameObject armObject = GameObject.Find(armName);
        if (armObject == null)
        {
            Debug.LogWarning($"Could not find {armName} in {ScenePath}.");
            return;
        }

        Transform arm = armObject.transform;
        Transform baseVisual = FindDirectChild(arm, "Base");
        Transform rotatingBase = FindDirectChildOrAny(arm, "RotatingBase");
        Transform lowerArm = FindDirectChildOrAny(arm, "LowerArm");
        Transform elbowJoint = FindDirectChildOrAny(arm, "ElbowJoint");
        Transform upperArm = FindDirectChildOrAny(arm, "UpperArm");
        Transform wrist = FindDirectChildOrAny(arm, "Wrist");
        Transform gripper = FindDirectChildOrAny(arm, "Gripper");
        Transform leftClaw = gripper != null ? FindDirectChildOrAny(gripper, "Claw_Left") : FindDirectChildOrAny(arm, "Claw_Left");
        Transform rightClaw = gripper != null ? FindDirectChildOrAny(gripper, "Claw_Right") : FindDirectChildOrAny(arm, "Claw_Right");
        Transform indicatorLight = gripper != null ? FindDirectChildOrAny(gripper, "IndicatorLight") : FindDirectChildOrAny(arm, "IndicatorLight");

        Transform pivotBase = GetOrCreateChild(arm, "Pivot_BaseRotation", rotatingBase != null ? rotatingBase.position : arm.position + Vector3.up * 0.55f, arm.rotation);
        Transform pivotShoulder = GetOrCreateChild(pivotBase, "Pivot_Shoulder", lowerArm != null ? lowerArm.position + arm.up * -0.45f : arm.TransformPoint(0f, 0.85f, -0.25f), arm.rotation);
        Transform pivotElbow = GetOrCreateChild(pivotShoulder, "Pivot_Elbow", elbowJoint != null ? elbowJoint.position : arm.TransformPoint(0f, 2.25f, -0.55f), arm.rotation);
        Transform pivotWrist = GetOrCreateChild(pivotElbow, "Pivot_Wrist", wrist != null ? wrist.position : arm.TransformPoint(0f, 2.25f, -2.25f), arm.rotation);

        ReparentIfNeeded(rotatingBase, pivotBase);
        ReparentIfNeeded(pivotShoulder, pivotBase);
        ReparentIfNeeded(lowerArm, pivotShoulder);
        ReparentIfNeeded(pivotElbow, pivotShoulder);
        ReparentIfNeeded(elbowJoint, pivotElbow);
        ReparentIfNeeded(upperArm, pivotElbow);
        ReparentIfNeeded(pivotWrist, pivotElbow);
        ReparentIfNeeded(wrist, pivotWrist);
        ReparentIfNeeded(gripper, pivotWrist);

        Transform leftClawPivot = null;
        Transform rightClawPivot = null;
        Transform itemSocket = null;
        if (gripper != null)
        {
            leftClawPivot = GetOrCreateChild(gripper, "Pivot_Claw_Left", leftClaw != null ? leftClaw.position : gripper.position + gripper.TransformDirection(Vector3.left * 0.25f), gripper.rotation);
            rightClawPivot = GetOrCreateChild(gripper, "Pivot_Claw_Right", rightClaw != null ? rightClaw.position : gripper.position + gripper.TransformDirection(Vector3.right * 0.25f), gripper.rotation);
            itemSocket = GetOrCreateChild(gripper, "ItemSocket", gripper.TransformPoint(0f, 0f, -0.42f), gripper.rotation);
            ReparentIfNeeded(leftClaw, leftClawPivot);
            ReparentIfNeeded(rightClaw, rightClawPivot);
        }

        Transform points = GetOrCreateChild(arm, "Points", arm.position, arm.rotation);
        Transform pickupPoint = GetOrCreateChild(points, "PickupPoint", arm.TransformPoint(0f, 0.65f, -2.95f), arm.rotation);
        Transform dropPoint = GetOrCreateChild(points, "DropPoint", arm.TransformPoint(0f, 0.65f, 2.95f), arm.rotation * Quaternion.Euler(0f, 180f, 0f));
        Transform safeLiftPoint = GetOrCreateChild(points, "SafeLiftPoint", arm.TransformPoint(0f, 1.8f, -1.2f), arm.rotation);

        Transform sensors = GetOrCreateChild(arm, "Sensors", arm.position, arm.rotation);
        Transform pickupSensorTransform = GetOrCreateChild(sensors, "PickupSensor", pickupPoint.position, arm.rotation);
        Transform dropSensorTransform = GetOrCreateChild(sensors, "DropAreaSensor", dropPoint.position, arm.rotation);

        BoxCollider pickupCollider = EnsureTriggerBox(pickupSensorTransform.gameObject, new Vector3(1f, 0.75f, 1f));
        BoxCollider dropCollider = EnsureTriggerBox(dropSensorTransform.gameObject, new Vector3(1.2f, 0.75f, 1.2f));
        RoboticArmPickupSensor pickupSensor = EnsureComponent<RoboticArmPickupSensor>(pickupSensorTransform.gameObject);
        RoboticArmDropAreaSensor dropSensor = EnsureComponent<RoboticArmDropAreaSensor>(dropSensorTransform.gameObject);
        RoboticArmGripper gripperComponent = gripper != null ? EnsureComponent<RoboticArmGripper>(gripper.gameObject) : null;
        RoboticArmController controller = EnsureComponent<RoboticArmController>(armObject);

        if (pickupCollider != null)
        {
            pickupCollider.center = Vector3.zero;
        }

        if (dropCollider != null)
        {
            dropCollider.center = Vector3.zero;
        }

        Renderer indicatorRenderer = indicatorLight != null ? indicatorLight.GetComponent<Renderer>() : null;
        controller.ConfigureReferences(
            pivotBase,
            pivotShoulder,
            pivotElbow,
            pivotWrist,
            leftClawPivot,
            rightClawPivot,
            itemSocket,
            pickupPoint,
            dropPoint,
            safeLiftPoint,
            pickupSensor,
            dropSensor,
            gripperComponent,
            indicatorRenderer);

        controller.ConfigureProduct(productType, productId, FindNearestConveyor(dropPoint.position));
        controller.CaptureCurrentPoseAsHome();
        gripperComponent?.CaptureCurrentAsOpen();
        gripperComponent?.SetClosedFromOpen(0.14f);
        EditorUtility.SetDirty(armObject);
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

    private static Transform FindDirectChildOrAny(Transform parent, string childName)
    {
        Transform direct = FindDirectChild(parent, childName);
        if (direct != null || parent == null)
        {
            return direct;
        }

        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private static Transform GetOrCreateChild(Transform parent, string childName, Vector3 worldPosition, Quaternion worldRotation)
    {
        Transform existing = FindDirectChild(parent, childName);
        if (existing != null)
        {
            return existing;
        }

        GameObject created = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(created, $"Create {childName}");
        created.transform.SetPositionAndRotation(worldPosition, worldRotation);
        Undo.SetTransformParent(created.transform, parent, $"Parent {childName}");
        return created.transform;
    }

    private static void ReparentIfNeeded(Transform child, Transform parent)
    {
        if (child == null || parent == null || child.parent == parent)
        {
            return;
        }

        Undo.SetTransformParent(child, parent, $"Parent {child.name}");
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component != null)
        {
            return component;
        }

        return Undo.AddComponent<T>(target);
    }

    private static BoxCollider EnsureTriggerBox(GameObject target, Vector3 size)
    {
        BoxCollider box = target.GetComponent<BoxCollider>();
        if (box == null)
        {
            box = Undo.AddComponent<BoxCollider>(target);
        }

        box.isTrigger = true;
        box.size = size;
        return box;
    }

    private static ConveyorController FindNearestConveyor(Vector3 position)
    {
        ConveyorController[] conveyors = Object.FindObjectsOfType<ConveyorController>();
        ConveyorController nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < conveyors.Length; i++)
        {
            ConveyorController conveyor = conveyors[i];
            if (conveyor == null || conveyor.ConveyorPath == null || !conveyor.ConveyorPath.IsValid())
            {
                continue;
            }

            float distance = Vector3.Distance(conveyor.ConveyorPath.GetSample(conveyor.ConveyorPath.GetClosestDistance(position)).Position, position);
            if (distance < nearestDistance)
            {
                nearest = conveyor;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}
