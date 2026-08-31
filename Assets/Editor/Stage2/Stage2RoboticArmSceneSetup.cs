using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage2RoboticArmSceneSetup
{
    private const string ScenePath = SceneNames.FactoryPath;

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

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Robotic Arm Network Components")]
    public static void ApplyNetworkComponentsToOpenScene()
    {
        int configuredCount = 0;
        configuredCount += ConfigureArmNetwork("RoboticArm_Pipes", "Braço Robótico 1", "stage2-robotic-arm-01") ? 1 : 0;
        configuredCount += ConfigureArmNetwork("RoboticArm_Beams", "Braço Robótico 2", "stage2-robotic-arm-02") ? 1 : 0;
        configuredCount += ConfigureArmNetwork("RoboticArm_Ingots", "Braço Robótico 3", "stage2-robotic-arm-03") ? 1 : 0;

        if (configuredCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        Debug.Log("Robotic arm network components configured: " + configuredCount + ".");
    }

    [MenuItem("Tools/RedeLabEscola/Stage2/Apply Pipes Tuning To Beams")]
    public static void ApplyPipesTuningToBeams()
    {
        GameObject sourceArm = GameObject.Find("RoboticArm_Pipes");
        GameObject targetArm = GameObject.Find("RoboticArm_Beams");
        if (targetArm == null)
        {
            targetArm = GameObject.Find("Robotic Arms_Beams");
        }

        if (sourceArm == null || targetArm == null)
        {
            Debug.LogError("Could not find RoboticArm_Pipes and RoboticArm_Beams in the current scene.");
            return;
        }

        RoboticArmController sourceController = sourceArm.GetComponent<RoboticArmController>();
        RoboticArmController targetController = targetArm.GetComponent<RoboticArmController>();
        if (sourceController == null)
        {
            Debug.LogError("RoboticArm_Pipes does not have RoboticArmController.");
            return;
        }

        if (targetController == null)
        {
            ConfigureArm(targetArm.name, RoboticArmProductType.Beams, "RawMaterial_B");
            targetController = targetArm.GetComponent<RoboticArmController>();
        }

        if (targetController == null)
        {
            Debug.LogError("Could not add RoboticArmController to the Beams arm.");
            return;
        }

        Undo.RecordObject(targetController, "Apply Pipes robotic arm tuning to Beams");
        CopyControllerTuning(sourceController, targetController);
        targetController.ConfigureProduct(RoboticArmProductType.Beams, "RawMaterial_B", FindNearestConveyor(GetDropPointPosition(targetArm.transform)));
        AssignAcceptedPrefab(targetController, "Assets/Prefabs/Peças/placa de circuito.prefab");

        RoboticArmGripper sourceGripper = sourceArm.GetComponentInChildren<RoboticArmGripper>(true);
        RoboticArmGripper targetGripper = targetArm.GetComponentInChildren<RoboticArmGripper>(true);
        if (sourceGripper != null && targetGripper != null)
        {
            Undo.RecordObject(targetGripper, "Apply Pipes gripper tuning to Beams");
            CopyGripperTuning(sourceGripper, targetGripper);
            EditorUtility.SetDirty(targetGripper);
        }

        EditorUtility.SetDirty(targetController);
        EditorSceneManager.MarkSceneDirty(targetArm.scene);
        Debug.Log("Applied RoboticArm_Pipes tuning to RoboticArm_Beams. Product set to RawMaterial_B / placa de circuito. PickupPoint and DropPoint were preserved.");
    }

    [MenuItem("Tools/RedeLabEscola/Stage2/Apply Pipes Tuning To Ingots")]
    public static void ApplyPipesTuningToIngots()
    {
        GameObject sourceArm = GameObject.Find("RoboticArm_Pipes");
        GameObject targetArm = GameObject.Find("RoboticArm_Ingots");
        if (targetArm == null)
        {
            targetArm = GameObject.Find("Robotic Arms_Ingots");
        }
        if (targetArm == null)
        {
            targetArm = GameObject.Find("RoboticArm_Inbox");
        }
        if (targetArm == null)
        {
            targetArm = GameObject.Find("Robotic Arms_Inbox");
        }

        if (sourceArm == null || targetArm == null)
        {
            Debug.LogError("Could not find RoboticArm_Pipes and RoboticArm_Ingots in the current scene.");
            return;
        }

        RoboticArmController sourceController = sourceArm.GetComponent<RoboticArmController>();
        RoboticArmController targetController = targetArm.GetComponent<RoboticArmController>();
        if (sourceController == null)
        {
            Debug.LogError("RoboticArm_Pipes does not have RoboticArmController.");
            return;
        }

        if (targetController == null)
        {
            ConfigureArm(targetArm.name, RoboticArmProductType.Ingots, "RawMaterial_C");
            targetController = targetArm.GetComponent<RoboticArmController>();
        }

        if (targetController == null)
        {
            Debug.LogError("Could not add RoboticArmController to the Ingots arm.");
            return;
        }

        Undo.RecordObject(targetController, "Apply Pipes robotic arm tuning to Ingots");
        CopyControllerTuning(sourceController, targetController);
        targetController.ConfigureProduct(RoboticArmProductType.Ingots, "RawMaterial_C", FindNearestConveyor(GetDropPointPosition(targetArm.transform)));
        AssignAcceptedPrefab(targetController, "Assets/Prefabs/Peças/Carcaça mecanica com eixo.prefab");

        RoboticArmGripper sourceGripper = sourceArm.GetComponentInChildren<RoboticArmGripper>(true);
        RoboticArmGripper targetGripper = targetArm.GetComponentInChildren<RoboticArmGripper>(true);
        if (sourceGripper != null && targetGripper != null)
        {
            Undo.RecordObject(targetGripper, "Apply Pipes gripper tuning to Ingots");
            CopyGripperTuning(sourceGripper, targetGripper);
            EditorUtility.SetDirty(targetGripper);
        }

        EditorUtility.SetDirty(targetController);
        EditorSceneManager.MarkSceneDirty(targetArm.scene);
        Debug.Log("Applied RoboticArm_Pipes tuning to RoboticArm_Ingots. Product set to RawMaterial_C / Carcaça mecanica com eixo. PickupPoint and DropPoint were preserved.");
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
        ConfigureArmNetwork(armObject, GetArmDeviceName(armObject.name), GetArmDeviceId(armObject.name), indicatorRenderer);
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

    private static bool ConfigureArmNetwork(string armName, string deviceName, string deviceId)
    {
        GameObject armObject = GameObject.Find(armName);
        if (armObject == null)
        {
            Debug.LogWarning("Could not find " + armName + " in the open scene.");
            return false;
        }

        return ConfigureArmNetwork(armObject, deviceName, deviceId, FindStatusRenderer(armObject.transform));
    }

    private static bool ConfigureArmNetwork(GameObject armObject, string deviceName, string deviceId, Renderer statusRenderer)
    {
        if (armObject == null)
        {
            return false;
        }

        WiFiDevice existingWiFi = armObject.GetComponent<WiFiDevice>();
        WiFiDevice wiFiDevice = existingWiFi != null ? existingWiFi : Undo.AddComponent<WiFiDevice>(armObject);
        if (existingWiFi == null)
        {
            wiFiDevice.Configure(WiFiDeviceType.RoboticArm, deviceId, 0.65f);
        }
        else
        {
            wiFiDevice.ConfigureIdentity(WiFiDeviceType.RoboticArm, deviceId);
        }

        RoboticArmNetworkAdapter adapter = armObject.GetComponent<RoboticArmNetworkAdapter>();
        if (adapter == null)
        {
            adapter = Undo.AddComponent<RoboticArmNetworkAdapter>(armObject);
        }

        adapter.ConfigureIdentity(deviceName, deviceId);
        adapter.ConfigureReferences(wiFiDevice, statusRenderer, null);
        EditorUtility.SetDirty(wiFiDevice);
        EditorUtility.SetDirty(adapter);
        EditorUtility.SetDirty(armObject);
        return true;
    }

    private static string GetArmDeviceName(string armName)
    {
        if (armName == "RoboticArm_Beams")
        {
            return "Braço Robótico 2";
        }

        if (armName == "RoboticArm_Ingots")
        {
            return "Braço Robótico 3";
        }

        return "Braço Robótico 1";
    }

    private static string GetArmDeviceId(string armName)
    {
        if (armName == "RoboticArm_Beams")
        {
            return "stage2-robotic-arm-02";
        }

        if (armName == "RoboticArm_Ingots")
        {
            return "stage2-robotic-arm-03";
        }

        return "stage2-robotic-arm-01";
    }

    private static Renderer FindStatusRenderer(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            string lowerName = renderer.name.ToLowerInvariant();
            if (lowerName.Contains("light_yellow")
                || lowerName.Contains("light_red")
                || lowerName.Contains("indicator")
                || lowerName.Contains("status"))
            {
                return renderer;
            }
        }

        return null;
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

    private static void CopyControllerTuning(RoboticArmController source, RoboticArmController target)
    {
        SerializedObject sourceObject = new SerializedObject(source);
        SerializedObject targetObject = new SerializedObject(target);

        string[] copiedProperties =
        {
            "itemSocketLocalPosition",
            "itemSocketLocalRotation",
            "useDropPointRotation",
            "homePose.baseRotation",
            "homePose.shoulderRotation",
            "homePose.elbowRotation",
            "homePose.wristRotation",
            "pickupPose.baseRotation",
            "pickupPose.shoulderRotation",
            "pickupPose.elbowRotation",
            "pickupPose.wristRotation",
            "liftPose.baseRotation",
            "liftPose.shoulderRotation",
            "liftPose.elbowRotation",
            "liftPose.wristRotation",
            "dropPose.baseRotation",
            "dropPose.shoulderRotation",
            "dropPose.elbowRotation",
            "dropPose.wristRotation",
            "baseRotationSpeed",
            "shoulderSpeed",
            "elbowSpeed",
            "wristSpeed",
            "gripperSpeed",
            "pickupMovementSpeed",
            "dropMovementSpeed",
            "returnSpeed",
            "delayBeforePickup",
            "delayAfterClosingGripper",
            "delayBeforeRelease",
            "delayAfterRelease",
            "delayBeforeReturn",
            "rotationToDropAngle",
            "angularTolerance",
            "positionTolerance",
            "invertDropRotation",
            "keepProductOrientationWhileCarried",
            "useSafeLiftPoint",
            "pickupArrivalTimeout",
            "pickupHoldTolerance",
            "smoothItemToSocketAfterAttach",
            "itemToSocketSpeed",
            "itemToSocketRotationSpeed",
            "itemToSocketTimeout",
            "wristRaisedRotation",
            "wristPickupLoweredRotation",
            "wristDropLoweredRotation",
            "wristPickupDropSpeedMultiplier",
            "gripperCloseSpeedMultiplier",
            "waitForDropAreaToClear",
            "maxDropAreaWaitTime",
            "useDropPoseBeforeRelease",
            "handReleasedItemToDestinationConveyor",
            "smoothReleaseToDropPoint",
            "releaseSmoothDuration",
            "maxPoseMoveTime",
            "maxWristMoveTime",
            "maxBaseRotationTime",
            "showGizmos",
            "logStateTransitions",
            "logItemEvents"
        };

        CopyProperties(sourceObject, targetObject, copiedProperties);
        targetObject.ApplyModifiedProperties();
    }

    private static void CopyGripperTuning(RoboticArmGripper source, RoboticArmGripper target)
    {
        SerializedObject sourceObject = new SerializedObject(source);
        SerializedObject targetObject = new SerializedObject(target);

        string[] copiedProperties =
        {
            "leftOpenLocalPosition",
            "leftClosedLocalPosition",
            "rightOpenLocalPosition",
            "rightClosedLocalPosition",
            "positionTolerance"
        };

        CopyProperties(sourceObject, targetObject, copiedProperties);
        targetObject.ApplyModifiedProperties();
    }

    private static void CopyProperties(SerializedObject sourceObject, SerializedObject targetObject, string[] propertyNames)
    {
        for (int i = 0; i < propertyNames.Length; i++)
        {
            SerializedProperty sourceProperty = sourceObject.FindProperty(propertyNames[i]);
            SerializedProperty targetProperty = targetObject.FindProperty(propertyNames[i]);
            if (sourceProperty == null || targetProperty == null)
            {
                continue;
            }

            CopyPropertyValue(sourceProperty, targetProperty);
        }
    }

    private static void CopyPropertyValue(SerializedProperty source, SerializedProperty target)
    {
        if (source.propertyType != target.propertyType)
        {
            return;
        }

        switch (source.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
            case SerializedPropertyType.ArraySize:
            case SerializedPropertyType.Character:
                target.intValue = source.intValue;
                break;
            case SerializedPropertyType.Boolean:
                target.boolValue = source.boolValue;
                break;
            case SerializedPropertyType.Float:
                target.floatValue = source.floatValue;
                break;
            case SerializedPropertyType.String:
                target.stringValue = source.stringValue;
                break;
            case SerializedPropertyType.Color:
                target.colorValue = source.colorValue;
                break;
            case SerializedPropertyType.ObjectReference:
                target.objectReferenceValue = source.objectReferenceValue;
                break;
            case SerializedPropertyType.Enum:
                target.enumValueIndex = source.enumValueIndex;
                break;
            case SerializedPropertyType.Vector2:
                target.vector2Value = source.vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                target.vector3Value = source.vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                target.vector4Value = source.vector4Value;
                break;
            case SerializedPropertyType.Rect:
                target.rectValue = source.rectValue;
                break;
            case SerializedPropertyType.AnimationCurve:
                target.animationCurveValue = source.animationCurveValue;
                break;
            case SerializedPropertyType.Bounds:
                target.boundsValue = source.boundsValue;
                break;
            case SerializedPropertyType.Quaternion:
                target.quaternionValue = source.quaternionValue;
                break;
            case SerializedPropertyType.ExposedReference:
                target.exposedReferenceValue = source.exposedReferenceValue;
                break;
            case SerializedPropertyType.Vector2Int:
                target.vector2IntValue = source.vector2IntValue;
                break;
            case SerializedPropertyType.Vector3Int:
                target.vector3IntValue = source.vector3IntValue;
                break;
            case SerializedPropertyType.RectInt:
                target.rectIntValue = source.rectIntValue;
                break;
            case SerializedPropertyType.BoundsInt:
                target.boundsIntValue = source.boundsIntValue;
                break;
        }
    }

    private static void AssignAcceptedPrefab(RoboticArmController controller, string assetPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"Could not find accepted prefab at {assetPath}.");
            return;
        }

        SerializedObject serializedController = new SerializedObject(controller);
        SerializedProperty acceptedPrefab = serializedController.FindProperty("acceptedPrefab");
        if (acceptedPrefab != null)
        {
            acceptedPrefab.objectReferenceValue = prefab;
            serializedController.ApplyModifiedProperties();
        }
    }

    private static Vector3 GetDropPointPosition(Transform arm)
    {
        Transform points = FindDirectChildOrAny(arm, "Points");
        Transform dropPoint = points != null ? FindDirectChildOrAny(points, "DropPoint") : FindDirectChildOrAny(arm, "DropPoint");
        return dropPoint != null ? dropPoint.position : arm.position;
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
