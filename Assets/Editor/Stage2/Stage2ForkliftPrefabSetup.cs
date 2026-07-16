#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage2ForkliftPrefabSetup
{
    private const string ForkliftPrefabPath = "Assets/Modelos 3D/Stage2_Factory/Prefabs/Forklift/Stage2_Forklift_LowPoly.prefab";
    private const string PalletPrefabPath = "Assets/Modelos 3D/Stage2_Factory/Prefabs/IndustrialProps/Pallet com caixas.prefab";
    private const string AlunoAnimatorControllerPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/player Animator Controller.controller";
    private const string ForkliftDrivingClipPath = "Assets/Modelos 3D/Personagem/Aluno/Animacoes personagem 3d aluno/dirigindo@Driving.fbx";
    private const string Stage2ScenePath = "Assets/Scenes/Stage2/Stage2_Factory.unity";
    private const string ConveyorRootName = "RawMaterialConveyor";
    private const string ForkliftDrivingParameter = "IsDrivingForklift";
    private const string ForkliftDrivingStateName = "dirigindo";

    [MenuItem("RedeLab/Stage 2/Configurar empilhadeira selecionada")]
    [MenuItem("Tools/RedeLabEscola/Stage2/Configurar empilhadeira selecionada")]
    public static void ConfigureSelectedForklift()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogError("Selecione o objeto raiz da empilhadeira na Hierarchy antes de configurar.");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(selected, "Configurar empilhadeira selecionada");
        ConfigureForklift(selected);
        ConfigureAlunoDrivingAnimator();
        EditorUtility.SetDirty(selected);
        MarkCurrentSceneDirtyIfNeeded(selected);
        Debug.Log("Empilhadeira selecionada configurada: " + selected.name);
    }

    [MenuItem("RedeLab/Stage 2/Configurar empilhadeira selecionada", true)]
    [MenuItem("Tools/RedeLabEscola/Stage2/Configurar empilhadeira selecionada", true)]
    public static bool CanConfigureSelectedForklift()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem("RedeLab/Stage 2/Configurar empilhadeira")]
    public static void ConfigureForkliftPrefab()
    {
        if (!File.Exists(ForkliftPrefabPath))
        {
            Debug.LogError("Prefab da empilhadeira nao encontrado: " + ForkliftPrefabPath);
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(ForkliftPrefabPath);
        try
        {
            ConfigureForklift(prefabRoot);
            ConfigureAlunoDrivingAnimator();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, ForkliftPrefabPath);
            Debug.Log("Empilhadeira configurada: " + ForkliftPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem("RedeLab/Stage 2/Configurar pallet da empilhadeira")]
    public static void ConfigurePalletPrefab()
    {
        if (!File.Exists(PalletPrefabPath))
        {
            Debug.LogError("Prefab do pallet nao encontrado: " + PalletPrefabPath);
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PalletPrefabPath);
        try
        {
            if (prefabRoot.GetComponent<ForkliftPallet>() == null)
            {
                prefabRoot.AddComponent<ForkliftPallet>();
            }

            ConfigurePalletPhysics(prefabRoot);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PalletPrefabPath);
            Debug.Log("Pallet configurado: " + PalletPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    [MenuItem("RedeLab/Stage 2/Configurar animacao dirigindo do aluno")]
    [MenuItem("Tools/RedeLabEscola/Stage2/Configurar animacao dirigindo do aluno")]
    public static void ConfigureAlunoDrivingAnimatorMenu()
    {
        ConfigureAlunoDrivingAnimator();
    }

    [MenuItem("RedeLab/Stage 2/Configurar zona de entrega da empilhadeira")]
    public static void ConfigureDropZoneInStage2Scene()
    {
        Scene scene = EditorSceneManager.OpenScene(Stage2ScenePath, OpenSceneMode.Single);
        GameObject conveyorRoot = GameObject.Find(ConveyorRootName);
        if (conveyorRoot == null)
        {
            Debug.LogError("Esteira de destino nao encontrada: " + ConveyorRootName);
            return;
        }

        ConveyorController conveyor = conveyorRoot.GetComponent<ConveyorController>();
        if (conveyor == null)
        {
            Debug.LogError("ConveyorController nao encontrado em " + ConveyorRootName);
            return;
        }

        Transform collectionZone = FindChild(conveyorRoot.transform, "CollectionZone");
        Transform placementReference = FindChild(collectionZone != null ? collectionZone : conveyorRoot.transform, "CollectionPoint");
        if (placementReference == null && collectionZone != null)
        {
            placementReference = collectionZone;
        }

        Transform dropZone = EnsureChild(conveyorRoot.transform, "ForkliftPalletDropZone", Vector3.zero, Quaternion.identity);
        if (placementReference != null)
        {
            dropZone.position = placementReference.position;
            dropZone.rotation = placementReference.rotation;
        }
        else
        {
            dropZone.localPosition = new Vector3(5.67f, 1.03f, 11.81f);
            dropZone.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        BoxCollider trigger = dropZone.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = dropZone.gameObject.AddComponent<BoxCollider>();
        }

        trigger.isTrigger = true;
        trigger.size = new Vector3(2.4f, 1.6f, 2.2f);
        trigger.center = new Vector3(0f, 0.25f, 0f);

        Transform placementPoint = EnsureChild(dropZone, "PalletPlacementPoint", Vector3.zero, Quaternion.identity);
        placementPoint.position = placementReference != null ? placementReference.position : dropZone.position;
        placementPoint.rotation = placementReference != null ? placementReference.rotation : dropZone.rotation;

        ForkliftPalletDropZone palletDropZone = dropZone.GetComponent<ForkliftPalletDropZone>();
        if (palletDropZone == null)
        {
            palletDropZone = dropZone.gameObject.AddComponent<ForkliftPalletDropZone>();
        }

        SerializedObject serialized = new SerializedObject(palletDropZone);
        SetObject(serialized, "palletPlacementPoint", placementPoint);
        SetObject(serialized, "destinationConveyor", conveyor);
        SetFloat(serialized, "deliveryHeight", placementPoint.position.y);
        SetFloat(serialized, "heightTolerance", 0.2f);
        SetFloat(serialized, "distanceTolerance", 1.6f);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(dropZone.gameObject);
        EditorUtility.SetDirty(conveyorRoot);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Zona de entrega da empilhadeira configurada em " + Stage2ScenePath);
    }

    private static void ConfigureForklift(GameObject root)
    {
        ForkliftController controller = root.GetComponent<ForkliftController>();
        if (controller == null)
        {
            controller = root.AddComponent<ForkliftController>();
        }

        Rigidbody body = root.GetComponent<Rigidbody>();
        if (body == null)
        {
            body = root.AddComponent<Rigidbody>();
        }

        body.mass = 350f;
        body.isKinematic = true;
        body.useGravity = false;
        body.drag = 2.5f;
        body.angularDrag = 8f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        EnsureMainBodyCollider(root);

        Transform forkCarriage = FindFirstChild(root.transform, "ForkCarriage", "elevacao_garfos", "elevacao garfos");
        Transform driverSeatPoint = EnsureChild(root.transform, "DriverSeatPoint", new Vector3(0f, 0.389f, -0.453f), Quaternion.identity);
        Transform playerExitPoint = EnsureChild(root.transform, "PlayerExitPoint", new Vector3(1.75f, 0f, 0.55f), Quaternion.identity);
        Transform runtimeAttachments = EnsureChild(root.transform, "ForkRuntimeAttachments", GetRuntimeAttachmentLocalPosition(root.transform, forkCarriage), Quaternion.identity);
        runtimeAttachments.localScale = Vector3.one;
        Transform forkCarryPoint = EnsureRuntimeChild(runtimeAttachments, "ForkCarryPoint", new Vector3(0f, 0.48f, 0f), Quaternion.identity);
        Transform sensor = EnsureRuntimeChild(runtimeAttachments, "ForkPickupSensor", new Vector3(0f, 0.32f, -1.35f), Quaternion.identity);
        Transform interaction = EnsureChild(root.transform, "InteractionTrigger", new Vector3(0f, 0.9f, 0.25f), Quaternion.identity);

        BoxCollider interactionCollider = interaction.GetComponent<BoxCollider>();
        if (interactionCollider == null)
        {
            interactionCollider = interaction.gameObject.AddComponent<BoxCollider>();
        }

        interactionCollider.isTrigger = true;
        interactionCollider.center = new Vector3(0f, -0.29f, -0.6f);
        interactionCollider.size = new Vector3(1.01f, 1.1f, 1.44f);

        BoxCollider sensorCollider = sensor.GetComponent<BoxCollider>();
        if (sensorCollider == null)
        {
            sensorCollider = sensor.gameObject.AddComponent<BoxCollider>();
        }

        sensorCollider.isTrigger = true;
        sensorCollider.center = new Vector3(-0.011f, -0.257f, -0.403f);
        sensorCollider.size = new Vector3(0.5985f, 0.074f, 1.1422f);
#if UNITY_2022_2_OR_NEWER
        sensorCollider.providesContacts = false;
#endif

        ForkliftPickupSensor pickupSensor = sensor.GetComponent<ForkliftPickupSensor>();
        if (pickupSensor == null)
        {
            pickupSensor = sensor.gameObject.AddComponent<ForkliftPickupSensor>();
        }

        pickupSensor.Configure(controller);

        SerializedObject serialized = new SerializedObject(controller);
        SetFloat(serialized, "maxForwardSpeed", 4.5f);
        SetFloat(serialized, "maxReverseSpeed", 3f);
        SetFloat(serialized, "acceleration", 5.5f);
        SetFloat(serialized, "deceleration", 11f);
        SetFloat(serialized, "brakeForce", 22f);
        SetFloat(serialized, "maxSteeringAngle", 75f);
        SetFloat(serialized, "steeringResponseSpeed", 8f);
        SetFloat(serialized, "highSpeedSteeringReduction", 0.65f);
        SetFloat(serialized, "movementSkinWidth", 0.04f);
        SetFloat(serialized, "wheelVisualRotationSpeed", 130f);
        SetFloat(serialized, "wheelVisualSteeringAngle", 32f);
        SetFloat(serialized, "forkLocalMinHeight", 0f);
        SetFloat(serialized, "forkLocalMaxHeight", 0.68f);
        SetFloat(serialized, "forkLiftSpeed", 0.55f);
        SetFloat(serialized, "forkLowerSpeed", 0.55f);
        SetFloat(serialized, "fallbackInteractionRadius", 2f);
        SetFloat(serialized, "minimumFacingDot", 0.35f);
        SetVector3(serialized, "forkRuntimeLocalBasePosition", GetRuntimeAttachmentLocalPosition(root.transform, forkCarriage));
        SetObject(serialized, "forkLiftTransform", forkCarriage);
        SetObject(serialized, "forkRuntimeAttachments", runtimeAttachments);
        SetObject(serialized, "driverSeatPoint", driverSeatPoint);
        SetObject(serialized, "playerExitPoint", playerExitPoint);
        SetObject(serialized, "interactionTrigger", interactionCollider);
        SetObject(serialized, "forkPickupSensor", pickupSensor);
        SetObject(serialized, "forkCarryPoint", forkCarryPoint);
        AssignWheelReferences(serialized, root.transform);
        SetObject(serialized, "steeringWheel", FindFirstChild(root.transform, "SteeringWheel", "volante"));

        Transform lamp = FindFirstChild(root.transform, "IndicatorLight", "lampada de cima", "lâmpada de cima", "lampada", "luz");
        SetObject(serialized, "topLampRenderer", lamp != null ? lamp.GetComponent<Renderer>() : null);
        SetObject(serialized, "topLampLight", lamp != null ? lamp.GetComponent<Light>() : null);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigurePalletPhysics(GameObject palletRoot)
    {
        Rigidbody palletBody = palletRoot.GetComponent<Rigidbody>();
        if (palletBody == null)
        {
            palletBody = palletRoot.AddComponent<Rigidbody>();
        }

        palletBody.mass = 20f;
        palletBody.isKinematic = false;
        palletBody.useGravity = true;
        palletBody.interpolation = RigidbodyInterpolation.Interpolate;
        palletBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        palletBody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        BoxCollider boxCollider = palletRoot.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            boxCollider = palletRoot.AddComponent<BoxCollider>();
        }

        FitBoxColliderToRenderers(palletRoot.transform, boxCollider, 0.96f);
    }

    private static void ConfigureAlunoDrivingAnimator()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AlunoAnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogWarning("Animator Controller do aluno nao encontrado: " + AlunoAnimatorControllerPath);
            return;
        }

        AnimationClip drivingClip = LoadDrivingClip();
        if (drivingClip == null)
        {
            Debug.LogWarning("Animacao dirigindo nao encontrada: " + ForkliftDrivingClipPath);
            return;
        }

        EnsureAnimatorBoolParameter(controller, ForkliftDrivingParameter);

        AnimatorControllerLayer layer = controller.layers.Length > 0 ? controller.layers[0] : null;
        if (layer == null || layer.stateMachine == null)
        {
            Debug.LogWarning("Animator Controller do aluno nao possui layer base configurada.");
            return;
        }

        AnimatorStateMachine stateMachine = layer.stateMachine;
        AnimatorState drivingState = FindState(stateMachine, ForkliftDrivingStateName);
        if (drivingState == null)
        {
            drivingState = stateMachine.AddState(ForkliftDrivingStateName, new Vector3(460f, -120f, 0f));
        }

        drivingState.motion = drivingClip;
        drivingState.writeDefaultValues = true;

        EnsureAnyStateTransition(stateMachine, drivingState, ForkliftDrivingParameter, true, 0.08f);

        AnimatorState defaultState = stateMachine.defaultState;
        if (defaultState != null)
        {
            EnsureStateTransition(drivingState, defaultState, ForkliftDrivingParameter, false, 0.12f);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Animacao de direcao da empilhadeira configurada no Animator do aluno.");
    }

    private static AnimationClip LoadDrivingClip()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ForkliftDrivingClipPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview", System.StringComparison.OrdinalIgnoreCase))
            {
                return clip;
            }
        }

        return null;
    }

    private static void EnsureAnimatorBoolParameter(AnimatorController controller, string parameterName)
    {
        for (int i = 0; i < controller.parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = controller.parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                return;
            }
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state != null && string.Equals(state.name, stateName, System.StringComparison.OrdinalIgnoreCase))
            {
                return state;
            }
        }

        return null;
    }

    private static void EnsureAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination, string parameterName, bool expectedValue, float duration)
    {
        if (HasMatchingTransition(stateMachine.anyStateTransitions, destination, parameterName, expectedValue))
        {
            return;
        }

        AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(destination);
        ConfigureAnimatorTransition(transition, parameterName, expectedValue, duration);
        transition.canTransitionToSelf = false;
    }

    private static void EnsureStateTransition(AnimatorState source, AnimatorState destination, string parameterName, bool expectedValue, float duration)
    {
        if (HasMatchingTransition(source.transitions, destination, parameterName, expectedValue))
        {
            return;
        }

        AnimatorStateTransition transition = source.AddTransition(destination);
        ConfigureAnimatorTransition(transition, parameterName, expectedValue, duration);
    }

    private static bool HasMatchingTransition(AnimatorStateTransition[] transitions, AnimatorState destination, string parameterName, bool expectedValue)
    {
        AnimatorConditionMode expectedMode = expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
        for (int i = 0; i < transitions.Length; i++)
        {
            AnimatorStateTransition transition = transitions[i];
            if (transition == null || transition.destinationState != destination)
            {
                continue;
            }

            AnimatorCondition[] conditions = transition.conditions;
            for (int conditionIndex = 0; conditionIndex < conditions.Length; conditionIndex++)
            {
                AnimatorCondition condition = conditions[conditionIndex];
                if (condition.mode == expectedMode && condition.parameter == parameterName)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ConfigureAnimatorTransition(AnimatorStateTransition transition, string parameterName, bool expectedValue, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        transition.exitTime = 0f;
        transition.offset = 0f;
        transition.AddCondition(expectedValue ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0f, parameterName);
    }

    private static Vector3 GetRuntimeAttachmentLocalPosition(Transform root, Transform forkCarriage)
    {
        if (root == null)
        {
            return Vector3.zero;
        }

        if (forkCarriage == null)
        {
            return Vector3.zero;
        }

        return Vector3.zero;
    }

    private static void EnsureMainBodyCollider(GameObject root)
    {
        BoxCollider rootCollider = root.GetComponent<BoxCollider>();
        if (rootCollider == null)
        {
            rootCollider = root.AddComponent<BoxCollider>();
        }

        rootCollider.isTrigger = false;
        rootCollider.center = new Vector3(-0.011f, 0.5912f, -0.161f);
        rootCollider.size = new Vector3(0.782f, 1.1855f, 1.49f);
    }

    private static void FitBoxColliderToRenderers(Transform root, BoxCollider boxCollider, float sizeMultiplier)
    {
        if (root == null || boxCollider == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            boxCollider.center = Vector3.zero;
            boxCollider.size = Vector3.one;
            return;
        }

        Bounds worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }
        }

        Vector3 localCenter = root.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = new Vector3(
            SafeDivide(worldBounds.size.x, Mathf.Abs(root.lossyScale.x)),
            SafeDivide(worldBounds.size.y, Mathf.Abs(root.lossyScale.y)),
            SafeDivide(worldBounds.size.z, Mathf.Abs(root.lossyScale.z))) * Mathf.Max(0.01f, sizeMultiplier);

        boxCollider.center = localCenter;
        boxCollider.size = localSize;
        boxCollider.isTrigger = false;
    }

    private static void AssignWheelReferences(SerializedObject serialized, Transform root)
    {
        Transform frontLeft = FindFirstChild(root, "Wheel_FL", "FrontLeftWheel", "FrontLeftWheelPivot");
        Transform frontRight = FindFirstChild(root, "Wheel_FR", "FrontRightWheel", "FrontRightWheelPivot");
        Transform rearLeft = FindFirstChild(root, "Wheel_RL", "RearLeftWheel", "RearLeftSteeringPivot");
        Transform rearRight = FindFirstChild(root, "Wheel_RR", "RearRightWheel", "RearRightSteeringPivot");

        if (frontLeft == null || frontRight == null || rearLeft == null || rearRight == null)
        {
            List<Transform> wheels = FindWheelLikeChildren(root);
            if (wheels.Count >= 4)
            {
                wheels.Sort((a, b) =>
                {
                    Vector3 localA = root.InverseTransformPoint(a.position);
                    Vector3 localB = root.InverseTransformPoint(b.position);
                    int zCompare = localA.z.CompareTo(localB.z);
                    return zCompare != 0 ? zCompare : localA.x.CompareTo(localB.x);
                });

                List<Transform> front = new List<Transform> { wheels[0], wheels[1] };
                List<Transform> rear = new List<Transform> { wheels[wheels.Count - 2], wheels[wheels.Count - 1] };
                front.Sort((a, b) => root.InverseTransformPoint(a.position).x.CompareTo(root.InverseTransformPoint(b.position).x));
                rear.Sort((a, b) => root.InverseTransformPoint(a.position).x.CompareTo(root.InverseTransformPoint(b.position).x));

                frontLeft = frontLeft != null ? frontLeft : front[0];
                frontRight = frontRight != null ? frontRight : front[1];
                rearLeft = rearLeft != null ? rearLeft : rear[0];
                rearRight = rearRight != null ? rearRight : rear[1];
            }
        }

        SetObject(serialized, "frontLeftWheel", frontLeft);
        SetObject(serialized, "frontRightWheel", frontRight);
        SetObject(serialized, "rearLeftWheel", rearLeft);
        SetObject(serialized, "rearRightWheel", rearRight);
    }

    private static List<Transform> FindWheelLikeChildren(Transform root)
    {
        List<Transform> wheels = new List<Transform>();
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == root)
            {
                continue;
            }

            string normalized = NormalizeName(child.name);
            if (normalized.Contains("wheel") || normalized.Contains("roda"))
            {
                wheels.Add(child);
            }
        }

        return wheels;
    }

    private static Transform EnsureChild(Transform parent, string childName, Vector3 localPosition, Quaternion localRotation)
    {
        Transform child = FindChild(parent, childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
            child.SetParent(parent, false);
        }

        child.localPosition = localPosition;
        child.localRotation = localRotation;
        child.localScale = Vector3.one;
        return child;
    }

    private static Transform EnsureRuntimeChild(Transform parent, string childName, Vector3 localPosition, Quaternion localRotation)
    {
        Transform searchRoot = parent.parent != null ? parent.parent : parent;
        Transform child = FindChild(searchRoot, childName);
        if (child == null)
        {
            child = new GameObject(childName).transform;
        }

        child.SetParent(parent, false);
        child.localPosition = localPosition;
        child.localRotation = localRotation;
        child.localScale = Vector3.one;
        return child;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return children[i];
            }
        }

        return null;
    }

    private static Transform FindFirstChild(Transform root, params string[] childNames)
    {
        for (int i = 0; i < childNames.Length; i++)
        {
            Transform child = FindChild(root, childNames[i]);
            if (child != null)
            {
                return child;
            }
        }

        return null;
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.ToLowerInvariant()
                .Replace("â", "a")
                .Replace("ã", "a")
                .Replace("á", "a")
                .Replace("à", "a")
                .Replace("é", "e")
                .Replace("ê", "e")
                .Replace("í", "i")
                .Replace("ó", "o")
                .Replace("õ", "o")
                .Replace("ô", "o")
                .Replace("ú", "u")
                .Replace("_", " ")
                .Replace(".", " ");
    }

    private static float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

    private static void MarkCurrentSceneDirtyIfNeeded(GameObject target)
    {
        if (target.scene.IsValid() && target.scene.isLoaded)
        {
            EditorSceneManager.MarkSceneDirty(target.scene);
        }
    }

    private static void SetObject(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    private static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }
}
#endif
