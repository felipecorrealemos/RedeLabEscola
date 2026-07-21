using System.Collections.Generic;
using UnityEngine;

public static class ScrapCraneRuntimeBootstrap
{
    private static readonly Vector3 DefaultCameraTargetLocalPosition = new Vector3(1.77f, -14.4f, 1.8f);
    private static readonly Vector2 DefaultCraneShadowSize = new Vector2(2.35f, 2.35f);
    private static readonly Vector3 DefaultGrabZoneLocalPosition = new Vector3(0f, -1.45f, 0f);
    private static readonly Vector3 DefaultGrabZoneSize = new Vector3(2.8f, 2.2f, 2.8f);
    private const float DefaultCraneShadowFloorLocalY = -22.62f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureScrapCraneIfPresent()
    {
        GameObject areaObject = GameObject.Find("AreaGarra");
        if (areaObject == null)
        {
            return;
        }

        Transform area = areaObject.transform;
        Transform movementArea = FindChildRecursive(area, "area de percurso");
        Transform movingAxis = FindChildRecursive(area, "eixo movimento");
        Transform claw = FindChildRecursive(area, "garra");
        Transform station = FindChildRecursive(area, "maquina de controle");

        if (movementArea == null || movingAxis == null || claw == null || station == null)
        {
            Debug.LogWarning("ScrapCraneRuntimeBootstrap: AreaGarra found, but required children are missing.");
            return;
        }

        ConfigureGroundShadow(area, claw);
        ScrapGrabDetectionZone grabZone = ConfigureGrabZone(claw);

        if (areaObject.GetComponent<ScrapCraneController>() != null)
        {
            return;
        }

        ScrapCraneBounds bounds = movementArea.gameObject.GetComponent<ScrapCraneBounds>();
        if (bounds == null)
        {
            bounds = movementArea.gameObject.AddComponent<ScrapCraneBounds>();
        }

        bounds.AssignReferences(movementArea, FindBoundaryColliders(movementArea, movingAxis), movingAxis.GetComponent<BoxCollider>());

        Transform carryPoint = GetOrCreateChild(claw, "CarryPoint", new Vector3(0f, -0.85f, 0f));
        Transform cameraTarget = GetOrCreateChild(area, "CraneCameraTarget", GetCameraTargetLocalPosition(area, movementArea));
        cameraTarget.localPosition = DefaultCameraTargetLocalPosition;

        ScrapCraneController controller = areaObject.AddComponent<ScrapCraneController>();
        ScrapCraneInputController input = areaObject.AddComponent<ScrapCraneInputController>();
        controller.AssignReferences(area, movementArea, movingAxis, claw, bounds, grabZone, carryPoint);
        input.AssignController(controller);
        controller.ConfigureDefaultBladeRotations();
        controller.ConfigureDefaultTimings();
        controller.ConfigureDefaultRestPose();

        Transform[] bladePivots = GuessBladePivots(claw);
        controller.AssignBladePivots(
            bladePivots.Length > 0 ? bladePivots[0] : null,
            bladePivots.Length > 1 ? bladePivots[1] : null,
            bladePivots.Length > 2 ? bladePivots[2] : null);

        ScrapCraneControlStation controlStation = station.gameObject.GetComponent<ScrapCraneControlStation>();
        if (controlStation == null)
        {
            controlStation = station.gameObject.AddComponent<ScrapCraneControlStation>();
        }

        DeadZoneCameraFollow cameraFollow = Camera.main != null ? Camera.main.GetComponent<DeadZoneCameraFollow>() : Object.FindObjectOfType<DeadZoneCameraFollow>();
        controlStation.AssignReferences(controller, input, null, cameraFollow, cameraTarget, null, null, null, null, null);

        Debug.Log($"ScrapCraneRuntimeBootstrap configured AreaGarra at runtime. Blade pivots guessed: {bladePivots.Length}. Run the Stage2 setup menu to persist and tune references.");
    }

    private static ScrapGrabDetectionZone ConfigureGrabZone(Transform claw)
    {
        Transform grabZoneTransform = GetOrCreateChild(claw, "GrabDetectionZone", DefaultGrabZoneLocalPosition);
        grabZoneTransform.localPosition = DefaultGrabZoneLocalPosition;
        grabZoneTransform.localRotation = Quaternion.identity;
        grabZoneTransform.localScale = Vector3.one;
        BoxCollider grabZoneCollider = grabZoneTransform.GetComponent<BoxCollider>();
        if (grabZoneCollider == null)
        {
            grabZoneCollider = grabZoneTransform.gameObject.AddComponent<BoxCollider>();
        }

        grabZoneCollider.isTrigger = true;
        grabZoneCollider.size = DefaultGrabZoneSize;
        Rigidbody grabZoneBody = grabZoneTransform.GetComponent<Rigidbody>();
        if (grabZoneBody == null)
        {
            grabZoneBody = grabZoneTransform.gameObject.AddComponent<Rigidbody>();
        }

        grabZoneBody.isKinematic = true;
        grabZoneBody.useGravity = false;
        grabZoneBody.detectCollisions = true;
        ScrapGrabDetectionZone grabZone = grabZoneTransform.GetComponent<ScrapGrabDetectionZone>();
        if (grabZone == null)
        {
            grabZone = grabZoneTransform.gameObject.AddComponent<ScrapGrabDetectionZone>();
        }

        return grabZone;
    }

    private static void ConfigureGroundShadow(Transform area, Transform claw)
    {
        Transform shadowTransform = GetOrCreateChild(area, "CraneGroundShadow", Vector3.zero);
        ScrapCraneGroundShadow shadow = shadowTransform.GetComponent<ScrapCraneGroundShadow>();
        if (shadow == null)
        {
            shadow = shadowTransform.gameObject.AddComponent<ScrapCraneGroundShadow>();
        }

        shadow.AssignReferences(claw, area);
        shadow.ConfigureDefaults(null, DefaultCraneShadowSize, DefaultCraneShadowFloorLocalY);
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
            return movementArea.localPosition + new Vector3(0f, 7f, 6f);
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
