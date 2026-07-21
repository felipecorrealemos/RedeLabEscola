using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class Stage2ScrapCraneSceneSetup
{
    private const string ScenePath = "Assets/Scenes/Stage2/Stage2_Factory.unity";
    private const string FakeShadowMaterialPath = "Assets/Materials/Player_FakeShadow.mat";
    private static readonly Vector3 DefaultCameraTargetLocalPosition = new Vector3(1.77f, -14.4f, 1.8f);
    private static readonly Vector2 DefaultCraneShadowSize = new Vector2(2.35f, 2.35f);
    private static readonly Vector3 DefaultGrabZoneLocalPosition = new Vector3(0f, -1.45f, 0f);
    private static readonly Vector3 DefaultGrabZoneSize = new Vector3(2.8f, 2.2f, 2.8f);
    private const float DefaultCraneShadowFloorLocalY = -22.62f;

    [MenuItem("Tools/RedeLabEscola/Stage2/Setup Scrap Crane")]
    public static void ApplyToStage2Factory()
    {
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        GameObject areaObject = GameObject.Find("AreaGarra");
        if (areaObject == null)
        {
            Debug.LogError("AreaGarra not found in Stage2_Factory.");
            return;
        }

        Transform area = areaObject.transform;
        Transform movementArea = FindChildRecursive(area, "area de percurso");
        Transform movingAxis = FindChildRecursive(area, "eixo movimento");
        Transform claw = FindChildRecursive(area, "garra");
        Transform station = FindChildRecursive(area, "maquina de controle");

        if (movementArea == null || movingAxis == null || claw == null || station == null)
        {
            Debug.LogError("Scrap crane setup could not find area de percurso, eixo movimento, garra, or maquina de controle.", areaObject);
            return;
        }

        Undo.SetCurrentGroupName("Setup Scrap Crane");
        int undoGroup = Undo.GetCurrentGroup();

        ScrapCraneBounds craneBounds = GetOrAddComponent<ScrapCraneBounds>(movementArea.gameObject);
        BoxCollider[] limits = FindBoundaryColliders(movementArea, movingAxis);
        BoxCollider movingAxisCollider = movingAxis.GetComponent<BoxCollider>();
        ConfigureBounds(craneBounds, movementArea, limits, movingAxisCollider);

        Transform grabZoneTransform = GetOrCreateChild(claw, "GrabDetectionZone", DefaultGrabZoneLocalPosition);
        Undo.RecordObject(grabZoneTransform, "Set scrap grab zone transform");
        grabZoneTransform.localPosition = DefaultGrabZoneLocalPosition;
        grabZoneTransform.localRotation = Quaternion.identity;
        grabZoneTransform.localScale = Vector3.one;
        BoxCollider grabZoneCollider = GetOrAddComponent<BoxCollider>(grabZoneTransform.gameObject);
        Undo.RecordObject(grabZoneCollider, "Set scrap grab zone collider");
        grabZoneCollider.isTrigger = true;
        grabZoneCollider.size = DefaultGrabZoneSize;
        Rigidbody grabZoneBody = GetOrAddComponent<Rigidbody>(grabZoneTransform.gameObject);
        grabZoneBody.isKinematic = true;
        grabZoneBody.useGravity = false;
        grabZoneBody.detectCollisions = true;
        ScrapGrabDetectionZone grabZone = GetOrAddComponent<ScrapGrabDetectionZone>(grabZoneTransform.gameObject);

        Transform carryPoint = GetOrCreateChild(claw, "CarryPoint", new Vector3(0f, -0.85f, 0f));
        Transform cameraTarget = GetOrCreateChild(area, "CraneCameraTarget", GetCameraTargetLocalPosition(area, movementArea));
        Undo.RecordObject(cameraTarget, "Set Scrap Crane camera target");
        cameraTarget.localPosition = DefaultCameraTargetLocalPosition;
        ScrapCraneGroundShadow groundShadow = ConfigureGroundShadow(area, claw);

        ScrapCraneController craneController = GetOrAddComponent<ScrapCraneController>(areaObject);
        ScrapCraneInputController inputController = GetOrAddComponent<ScrapCraneInputController>(areaObject);
        craneController.AssignReferences(area, movementArea, movingAxis, claw, craneBounds, grabZone, carryPoint);
        inputController.AssignController(craneController);
        craneController.ConfigureDefaultBladeRotations();
        craneController.ConfigureDefaultTimings();
        craneController.ConfigureDefaultRestPose();

        Transform[] bladePivots = GuessBladePivots(claw);
        craneController.AssignBladePivots(
            bladePivots.Length > 0 ? bladePivots[0] : null,
            bladePivots.Length > 1 ? bladePivots[1] : null,
            bladePivots.Length > 2 ? bladePivots[2] : null);

        Transform triggerTransform = GetOrCreateChild(station, "CraneInteractionTrigger", Vector3.zero);
        BoxCollider triggerCollider = GetOrAddComponent<BoxCollider>(triggerTransform.gameObject);
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(3f, 2.2f, 3f);
        ScrapCraneStationTrigger triggerForwarder = GetOrAddComponent<ScrapCraneStationTrigger>(triggerTransform.gameObject);

        Canvas canvas = GetOrCreateInteractionCanvas();
        GameObject promptObject = GetOrCreatePrompt(canvas.transform);
        Text promptText = promptObject.GetComponentInChildren<Text>(true);
        GameObject commandsPanel = GetOrCreateCommandsPanel(canvas.transform);
        Text commandsText = commandsPanel.GetComponentInChildren<Text>(true);

        ScrapCraneControlStation controlStation = GetOrAddComponent<ScrapCraneControlStation>(station.gameObject);
        DeadZoneCameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<DeadZoneCameraFollow>() : Object.FindObjectOfType<DeadZoneCameraFollow>();
        controlStation.AssignReferences(craneController, inputController, triggerCollider, cameraFollow, cameraTarget, canvas, promptObject, promptText, commandsPanel, commandsText);
        triggerForwarder.AssignStation(controlStation);

        EditorUtility.SetDirty(areaObject);
        if (groundShadow != null)
        {
            EditorUtility.SetDirty(groundShadow);
        }
        EditorUtility.SetDirty(movementArea.gameObject);
        EditorUtility.SetDirty(station.gameObject);
        EditorSceneManager.MarkSceneDirty(areaObject.scene);
        EditorSceneManager.SaveScene(areaObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log($"Scrap crane setup complete. Boundary colliders found: {limits.Length}. Blade pivots guessed: {bladePivots.Length}. Review blade pivot assignments, especially blade03, before final tuning.", areaObject);
    }

    private static ScrapCraneGroundShadow ConfigureGroundShadow(Transform area, Transform claw)
    {
        Transform shadowTransform = GetOrCreateChild(area, "CraneGroundShadow", Vector3.zero);
        ScrapCraneGroundShadow shadow = GetOrAddComponent<ScrapCraneGroundShadow>(shadowTransform.gameObject);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(FakeShadowMaterialPath);
        shadow.AssignReferences(claw, area);
        shadow.ConfigureDefaults(material, DefaultCraneShadowSize, DefaultCraneShadowFloorLocalY);
        return shadow;
    }

    private static void ConfigureBounds(ScrapCraneBounds craneBounds, Transform movementArea, BoxCollider[] limits, BoxCollider movingAxisCollider)
    {
        SerializedObject serialized = new SerializedObject(craneBounds);
        serialized.FindProperty("movementBoundsRoot").objectReferenceValue = movementArea;
        SerializedProperty boundaryProperty = serialized.FindProperty("boundaryColliders");
        boundaryProperty.arraySize = limits.Length;
        for (int i = 0; i < limits.Length; i++)
        {
            boundaryProperty.GetArrayElementAtIndex(i).objectReferenceValue = limits[i];
        }

        serialized.FindProperty("movingAxisCollider").objectReferenceValue = movingAxisCollider;
        serialized.FindProperty("useAutomaticBounds").boolValue = true;
        serialized.FindProperty("movementSafetyMargin").floatValue = 0.05f;
        serialized.ApplyModifiedProperties();
        craneBounds.RecalculateBounds();
    }

    private static BoxCollider[] FindBoundaryColliders(Transform movementArea, Transform movingAxis)
    {
        List<BoxCollider> colliders = new List<BoxCollider>();
        for (int i = 0; i < movementArea.childCount; i++)
        {
            Transform child = movementArea.GetChild(i);
            if (child == movingAxis || child.name != "limite")
            {
                continue;
            }

            BoxCollider box = child.GetComponent<BoxCollider>();
            if (box != null)
            {
                colliders.Add(box);
            }
        }

        return colliders.ToArray();
    }

    private static Transform[] GuessBladePivots(Transform claw)
    {
        List<Transform> candidates = new List<Transform>();
        Transform[] children = claw.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == claw)
            {
                continue;
            }

            string lower = child.name.ToLowerInvariant();
            if (lower.Contains("pivot") || lower.Contains("pa") || lower.Contains("pá") || lower.Contains("blade"))
            {
                candidates.Add(child);
            }
        }

        candidates.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (candidates.Count > 3)
        {
            candidates.RemoveRange(3, candidates.Count - 3);
        }

        return candidates.ToArray();
    }

    private static Vector3 GetCameraTargetLocalPosition(Transform area, Transform movementArea)
    {
        return DefaultCameraTargetLocalPosition;
    }

    private static Vector3 GetCalculatedCameraTargetLocalPosition(Transform area, Transform movementArea)
    {
        Renderer[] renderers = movementArea.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return movementArea.localPosition + new Vector3(0f, 6f, 6f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        Vector3 center = area.InverseTransformPoint(bounds.center);
        center.y += 7f;
        return center;
    }

    private static Canvas GetOrCreateInteractionCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == "InteractionCanvas")
            {
                return canvases[i];
            }
        }

        GameObject canvasObject = new GameObject("InteractionCanvas");
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create InteractionCanvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        RuntimeEventSystemUtility.EnsureSingleEventSystem();
        return canvas;
    }

    private static GameObject GetOrCreatePrompt(Transform canvas)
    {
        Transform existing = canvas.Find("ScrapCranePrompt");
        if (existing != null)
        {
            return existing.gameObject;
        }

        GameObject prompt = new GameObject("ScrapCranePrompt");
        Undo.RegisterCreatedObjectUndo(prompt, "Create ScrapCranePrompt");
        prompt.transform.SetParent(canvas, false);
        RectTransform rect = prompt.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 176f);
        rect.sizeDelta = new Vector2(440f, 42f);
        Image background = prompt.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.65f);
        Text text = CreateText(prompt.transform, "Text", TextAnchor.MiddleCenter, 18);
        text.text = "Aperte E para controlar a garra";
        prompt.SetActive(false);
        return prompt;
    }

    private static GameObject GetOrCreateCommandsPanel(Transform canvas)
    {
        Transform existing = canvas.Find("ScrapCraneCommandsPanel");
        if (existing != null)
        {
            Text existingText = existing.GetComponentInChildren<Text>(true);
            if (existingText != null)
            {
                ConfigureCommandsText(existingText);
            }

            return existing.gameObject;
        }

        GameObject panel = new GameObject("ScrapCraneCommandsPanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create ScrapCraneCommandsPanel");
        panel.transform.SetParent(canvas, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -96f);
        rect.sizeDelta = new Vector2(285f, 190f);
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.62f);
        Text text = CreateText(panel.transform, "Text", TextAnchor.UpperLeft, 15);
        ConfigureCommandsText(text);
        panel.SetActive(false);
        return panel;
    }

    private static void ConfigureCommandsText(Text text)
    {
        text.alignment = TextAnchor.UpperLeft;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 15;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.text =
            "CONTROLE DA GARRA\n\n" +
            "W A S D - Movimentar\n" +
            "1 - Coletar/Soltar\n" +
            "E ou Esc - Sair";
    }

    private static Text CreateText(Transform parent, string name, TextAnchor alignment, int fontSize)
    {
        GameObject label = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(label, "Create Text");
        label.transform.SetParent(parent, false);
        RectTransform rect = label.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12f, 8f);
        rect.offsetMax = new Vector2(-12f, -8f);
        Text text = label.AddComponent<Text>();
        text.alignment = alignment;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
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
