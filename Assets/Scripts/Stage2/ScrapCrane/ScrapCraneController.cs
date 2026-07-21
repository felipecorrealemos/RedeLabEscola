using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ScrapCraneController : MonoBehaviour
{
    public static readonly Vector3 DefaultBlade01ClosedEuler = new Vector3(-66f, 64.646f, 90f);
    public static readonly Vector3 DefaultBlade01OpenEuler = new Vector3(-150f, 64.646f, 90f);
    public static readonly Vector3 DefaultBlade02ClosedEuler = new Vector3(-66f, -58.872f, 90f);
    public static readonly Vector3 DefaultBlade02OpenEuler = new Vector3(-150f, -58.872f, 90f);
    public static readonly Vector3 DefaultBlade03ClosedEuler = new Vector3(-114f, 0f, -90f);
    public static readonly Vector3 DefaultBlade03OpenEuler = new Vector3(-34f, 0f, -90f);

    public enum BladeState
    {
        Open,
        Opening,
        Closed,
        Closing
    }

    [System.Serializable]
    public class BladeConfiguration
    {
        public Transform pivot;
        public Vector3 closedLocalEuler = DefaultBlade01ClosedEuler;
        public Vector3 openLocalEuler = DefaultBlade01OpenEuler;
    }

    private class CarriedScrapState
    {
        public ScrapItem Item;
        public Transform Root;
        public Transform OriginalParent;
        public Rigidbody Body;
        public bool HadRigidbody;
        public bool WasKinematic;
        public bool UsedGravity;
        public bool DetectedCollisions;
        public Collider[] Colliders;
        public bool[] ColliderTriggerStates;
    }

    [Header("References")]
    [SerializeField] private Transform areaGarra;
    [SerializeField] private Transform movementArea;
    [SerializeField] private Transform movingAxis;
    [SerializeField] private Transform claw;
    [SerializeField] private ScrapCraneBounds bounds;

    [Header("Horizontal Movement")]
    [SerializeField, Min(0.01f)] private float horizontalSpeed = 5f;
    [SerializeField] private bool useAcceleration = true;
    [SerializeField, Min(0.01f)] private float horizontalAcceleration = 18f;
    [SerializeField, Min(0.01f)] private float horizontalDeceleration = 24f;
    [SerializeField] private bool allowDiagonalMovement = true;

    [Header("Vertical Movement")]
    [SerializeField] private float upperLocalY = -4f;
    [SerializeField] private float lowerLocalY = -13f;
    [SerializeField] private float releaseLowerLocalY = -8f;
    [SerializeField, Min(0.01f)] private float verticalSpeed = 5f;
    [SerializeField, Min(0f)] private float delayAfterClosingBeforeRaise = 0.15f;
    [SerializeField, Min(0f)] private float delayAfterOpeningBeforeRaise = 0.12f;

    [Header("Blades")]
    [SerializeField] private BladeConfiguration blade01 = new BladeConfiguration();
    [SerializeField] private BladeConfiguration blade02 = new BladeConfiguration
    {
        closedLocalEuler = DefaultBlade02ClosedEuler,
        openLocalEuler = DefaultBlade02OpenEuler
    };
    [SerializeField] private BladeConfiguration blade03 = new BladeConfiguration
    {
        closedLocalEuler = DefaultBlade03ClosedEuler,
        openLocalEuler = DefaultBlade03OpenEuler
    };
    [SerializeField, Min(0.01f)] private float bladeMovementDuration = 0.55f;
    [SerializeField] private AnimationCurve bladeMovementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private BladeState initialBladeState = BladeState.Open;
    [SerializeField] private BladeState currentBladeState = BladeState.Open;

    [Header("Capture")]
    [SerializeField] private bool allowCapture = true;
    [SerializeField] private ScrapGrabDetectionZone grabDetectionZone;
    [SerializeField] private Transform carryPoint;
    [SerializeField] private Vector3 carriedLocalPosition;
    [SerializeField] private Vector3 carriedLocalEulerAngles;
    [SerializeField, Min(0.01f)] private float captureMoveDuration = 0.65f;
    [SerializeField] private AnimationCurve captureMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool sendReleasedScrapToCrusher = true;
    [SerializeField] private bool dropToGroundWhenNoCrusher = false;
    [SerializeField] private LayerMask groundDropLayers = ~0;
    [SerializeField, Min(0.1f)] private float groundDropProbeDistance = 12f;
    [SerializeField, Min(0f)] private float groundDropOffset = 0.02f;
    [SerializeField, Min(0.1f)] private float crusherDropProbeDistance = 30f;
    [SerializeField, Min(0f)] private float physicsDropInitialDownVelocity = 0.75f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool logWarnings = true;

    private Vector3 horizontalVelocity;
    private Coroutine bladeRoutine;
    private Coroutine actionRoutine;
    private CarriedScrapState carriedScrap;
    private bool isControlActive;

    public bool IsControlActive => isControlActive;
    public bool IsActionRunning => actionRoutine != null;
    public bool IsCarryingScrap => carriedScrap != null;
    public BladeState CurrentBladeState => currentBladeState;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ApplyMinimumRuntimeTimings();
        ResolveReferences();
        initialBladeState = BladeState.Open;
        ApplyInitialBladeState();
    }

    private void OnValidate()
    {
        horizontalSpeed = Mathf.Max(0.01f, horizontalSpeed);
        horizontalAcceleration = Mathf.Max(0.01f, horizontalAcceleration);
        horizontalDeceleration = Mathf.Max(0.01f, horizontalDeceleration);
        verticalSpeed = Mathf.Max(0.01f, verticalSpeed);
        delayAfterClosingBeforeRaise = Mathf.Max(0f, delayAfterClosingBeforeRaise);
        delayAfterOpeningBeforeRaise = Mathf.Max(0f, delayAfterOpeningBeforeRaise);
        bladeMovementDuration = Mathf.Max(0.01f, bladeMovementDuration);
        captureMoveDuration = Mathf.Max(0.01f, captureMoveDuration);
        groundDropProbeDistance = Mathf.Max(0.1f, groundDropProbeDistance);
        groundDropOffset = Mathf.Max(0f, groundDropOffset);
        crusherDropProbeDistance = Mathf.Max(0.1f, crusherDropProbeDistance);
        physicsDropInitialDownVelocity = Mathf.Max(0f, physicsDropInitialDownVelocity);
        ApplyMinimumRuntimeTimings();
    }

    public void SetControlActive(bool active)
    {
        isControlActive = active;
        if (!active)
        {
            horizontalVelocity = Vector3.zero;
        }
    }

    public void MoveHorizontal(Vector2 input, float deltaTime)
    {
        if (!isControlActive || actionRoutine != null || movingAxis == null)
        {
            return;
        }

        if (!allowDiagonalMovement && Mathf.Abs(input.x) > 0.01f && Mathf.Abs(input.y) > 0.01f)
        {
            input.y = 0f;
        }

        Vector3 desiredDirection = new Vector3(input.x, 0f, input.y);
        desiredDirection = Vector3.ClampMagnitude(desiredDirection, 1f);
        Vector3 desiredVelocity = desiredDirection * horizontalSpeed;

        if (useAcceleration)
        {
            float rate = desiredVelocity.sqrMagnitude > 0.001f ? horizontalAcceleration : horizontalDeceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, rate * deltaTime);
        }
        else
        {
            horizontalVelocity = desiredVelocity;
        }

        Vector3 nextLocalPosition = movingAxis.localPosition + horizontalVelocity * deltaTime;
        float lockedY = movingAxis.localPosition.y;
        if (bounds != null)
        {
            nextLocalPosition = bounds.ClampLocalPosition(nextLocalPosition);
        }

        nextLocalPosition.y = lockedY;
        movingAxis.localPosition = nextLocalPosition;
    }

    public void MoveVertical(float input, float deltaTime)
    {
        if (!isControlActive || actionRoutine != null || claw == null || Mathf.Abs(input) <= 0.001f)
        {
            return;
        }

        Vector3 localPosition = claw.localPosition;
        float minY = Mathf.Min(upperLocalY, lowerLocalY);
        float maxY = Mathf.Max(upperLocalY, lowerLocalY);
        localPosition.y = Mathf.Clamp(localPosition.y + input * verticalSpeed * deltaTime, minY, maxY);
        claw.localPosition = localPosition;
    }

    public void ToggleBlades()
    {
        if (!isControlActive || actionRoutine != null || bladeRoutine != null || currentBladeState == BladeState.Opening || currentBladeState == BladeState.Closing)
        {
            return;
        }

        if (currentBladeState == BladeState.Closed)
        {
            bladeRoutine = StartCoroutine(AnimateBlades(true));
        }
        else if (currentBladeState == BladeState.Open)
        {
            bladeRoutine = StartCoroutine(AnimateBlades(false));
        }
    }

    public void StartPrimaryAction()
    {
        if (!isControlActive || actionRoutine != null || claw == null)
        {
            return;
        }

        actionRoutine = StartCoroutine(RunPrimaryAction());
    }

    [ContextMenu("Capture Current Blades As Closed")]
    public void CaptureCurrentBladesAsClosed()
    {
        CaptureBladeRotations(true);
    }

    [ContextMenu("Capture Current Blades As Open")]
    public void CaptureCurrentBladesAsOpen()
    {
        CaptureBladeRotations(false);
    }

    [ContextMenu("Apply Closed Blade Pose")]
    public void ApplyClosedBladePose()
    {
        ApplyBladePose(false);
        currentBladeState = BladeState.Closed;
    }

    [ContextMenu("Apply Open Blade Pose")]
    public void ApplyOpenBladePose()
    {
        ApplyBladePose(true);
        currentBladeState = BladeState.Open;
    }

    [ContextMenu("Validate References")]
    public void ValidateReferences()
    {
        ResolveReferences();

        if (movingAxis == null)
        {
            Warn("Missing moving axis reference.");
        }

        if (claw == null)
        {
            Warn("Missing claw reference.");
        }

        if (bounds == null)
        {
            Warn("Missing ScrapCraneBounds reference.");
        }

        if (grabDetectionZone == null)
        {
            Warn("Missing GrabDetectionZone reference.");
        }

        if (carryPoint == null)
        {
            Warn("Missing CarryPoint reference.");
        }
    }

    public void AssignReferences(Transform area, Transform movementAreaTransform, Transform movingAxisTransform, Transform clawTransform, ScrapCraneBounds craneBounds, ScrapGrabDetectionZone detectionZone, Transform carry)
    {
        areaGarra = area;
        movementArea = movementAreaTransform;
        movingAxis = movingAxisTransform;
        claw = clawTransform;
        bounds = craneBounds;
        grabDetectionZone = detectionZone;
        carryPoint = carry;
    }

    public void AssignBladePivots(Transform first, Transform second, Transform third)
    {
        blade01.pivot = first;
        blade02.pivot = second;
        blade03.pivot = third;
    }

    public void ConfigureDefaultBladeRotations()
    {
        blade01.closedLocalEuler = DefaultBlade01ClosedEuler;
        blade01.openLocalEuler = DefaultBlade01OpenEuler;
        blade02.closedLocalEuler = DefaultBlade02ClosedEuler;
        blade02.openLocalEuler = DefaultBlade02OpenEuler;
        blade03.closedLocalEuler = DefaultBlade03ClosedEuler;
        blade03.openLocalEuler = DefaultBlade03OpenEuler;
    }

    public void ConfigureDefaultTimings()
    {
        captureMoveDuration = 0.65f;
    }

    public void ConfigureDefaultRestPose()
    {
        initialBladeState = BladeState.Open;
        currentBladeState = BladeState.Open;
        ApplyBladePose(true);
    }

    private void ApplyMinimumRuntimeTimings()
    {
        captureMoveDuration = Mathf.Max(captureMoveDuration, 0.65f);
    }

    private IEnumerator AnimateBlades(bool opening)
    {
        currentBladeState = opening ? BladeState.Opening : BladeState.Closing;

        Quaternion[] startRotations =
        {
            GetLocalRotation(blade01),
            GetLocalRotation(blade02),
            GetLocalRotation(blade03)
        };
        Quaternion[] targetRotations =
        {
            Quaternion.Euler(opening ? blade01.openLocalEuler : blade01.closedLocalEuler),
            Quaternion.Euler(opening ? blade02.openLocalEuler : blade02.closedLocalEuler),
            Quaternion.Euler(opening ? blade03.openLocalEuler : blade03.closedLocalEuler)
        };

        float elapsed = 0f;
        while (elapsed < bladeMovementDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bladeMovementDuration);
            if (bladeMovementCurve != null)
            {
                t = bladeMovementCurve.Evaluate(t);
            }

            SetLocalRotation(blade01, Quaternion.Slerp(startRotations[0], targetRotations[0], t));
            SetLocalRotation(blade02, Quaternion.Slerp(startRotations[1], targetRotations[1], t));
            SetLocalRotation(blade03, Quaternion.Slerp(startRotations[2], targetRotations[2], t));
            yield return null;
        }

        SetLocalRotation(blade01, targetRotations[0]);
        SetLocalRotation(blade02, targetRotations[1]);
        SetLocalRotation(blade03, targetRotations[2]);

        currentBladeState = opening ? BladeState.Open : BladeState.Closed;
        bladeRoutine = null;

        if (opening)
        {
            ReleaseCarriedScrap();
        }
        else
        {
            yield return TryCaptureScrap();
        }
    }

    private IEnumerator RunPrimaryAction()
    {
        horizontalVelocity = Vector3.zero;

        if (carriedScrap == null)
        {
            if (currentBladeState != BladeState.Open)
            {
                yield return AnimateBladesAndWait(true);
            }

            yield return MoveClawToLocalY(lowerLocalY);
            yield return AnimateBladesAndWait(false);
            if (carriedScrap == null)
            {
                yield return AnimateBladesAndWait(true);
            }
            else if (delayAfterClosingBeforeRaise > 0f)
            {
                yield return new WaitForSeconds(delayAfterClosingBeforeRaise);
            }

            yield return MoveClawToLocalY(upperLocalY);
        }
        else
        {
            if (!CanCurrentScrapDropToCrusherFromAbove())
            {
                actionRoutine = null;
                yield break;
            }

            yield return AnimateBladesAndWait(true);
            if (delayAfterOpeningBeforeRaise > 0f)
            {
                yield return new WaitForSeconds(delayAfterOpeningBeforeRaise);
            }

            yield return MoveClawToLocalY(upperLocalY);
        }

        actionRoutine = null;
    }

    private IEnumerator MoveClawToLocalY(float targetLocalY)
    {
        if (claw == null)
        {
            yield break;
        }

        float minY = Mathf.Min(upperLocalY, lowerLocalY, releaseLowerLocalY);
        float maxY = Mathf.Max(upperLocalY, lowerLocalY, releaseLowerLocalY);
        targetLocalY = Mathf.Clamp(targetLocalY, minY, maxY);

        while (Mathf.Abs(claw.localPosition.y - targetLocalY) > 0.005f)
        {
            Vector3 localPosition = claw.localPosition;
            localPosition.y = Mathf.MoveTowards(localPosition.y, targetLocalY, verticalSpeed * Time.deltaTime);
            claw.localPosition = localPosition;
            yield return null;
        }

        Vector3 finalPosition = claw.localPosition;
        finalPosition.y = targetLocalY;
        claw.localPosition = finalPosition;
    }

    private IEnumerator AnimateBladesAndWait(bool opening)
    {
        if (bladeRoutine != null)
        {
            yield return bladeRoutine;
        }

        bladeRoutine = StartCoroutine(AnimateBlades(opening));
        yield return bladeRoutine;
    }

    private IEnumerator TryCaptureScrap()
    {
        if (!allowCapture || carriedScrap != null || grabDetectionZone == null || carryPoint == null)
        {
            yield break;
        }

        ScrapItem item = grabDetectionZone.GetClosestValidScrap(carryPoint.position);
        if (item == null)
        {
            yield break;
        }

        Transform root = item.GrabRoot;
        Rigidbody body = root.GetComponent<Rigidbody>();
        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        bool[] triggerStates = new bool[colliders.Length];

        carriedScrap = new CarriedScrapState
        {
            Item = item,
            Root = root,
            OriginalParent = root.parent,
            Body = body,
            HadRigidbody = body != null,
            Colliders = colliders,
            ColliderTriggerStates = triggerStates
        };
        item.SetCanBeGrabbed(false);
        item.ClearCrusherDropState();

        if (body != null)
        {
            carriedScrap.WasKinematic = body.isKinematic;
            carriedScrap.UsedGravity = body.useGravity;
            carriedScrap.DetectedCollisions = body.detectCollisions;
            ClearRigidbodyVelocityIfDynamic(body);
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            triggerStates[i] = colliders[i].isTrigger;
            colliders[i].isTrigger = true;
        }

        yield return MoveCapturedScrapToCarryPoint(root);
    }

    private IEnumerator MoveCapturedScrapToCarryPoint(Transform root)
    {
        if (root == null || carryPoint == null)
        {
            yield break;
        }

        root.SetParent(carryPoint, true);
        Vector3 startLocalPosition = root.localPosition;
        Quaternion startLocalRotation = root.localRotation;
        Quaternion targetLocalRotation = Quaternion.Euler(carriedLocalEulerAngles);

        float elapsed = 0f;
        while (elapsed < captureMoveDuration && root != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / captureMoveDuration);
            if (captureMoveCurve != null)
            {
                t = captureMoveCurve.Evaluate(t);
            }

            root.localPosition = Vector3.Lerp(startLocalPosition, carriedLocalPosition, t);
            root.localRotation = Quaternion.Slerp(startLocalRotation, targetLocalRotation, t);
            yield return null;
        }

        if (root != null)
        {
            root.localPosition = carriedLocalPosition;
            root.localRotation = targetLocalRotation;
        }
    }

    private void ReleaseCarriedScrap()
    {
        if (carriedScrap == null || carriedScrap.Root == null)
        {
            carriedScrap = null;
            return;
        }

        ScrapItem releasedItem = carriedScrap.Item;
        Transform root = carriedScrap.Root;
        Vector3 releaseWorldPosition = root.position;
        Quaternion releaseWorldRotation = root.rotation;
        bool shouldUsePhysicsDrop = sendReleasedScrapToCrusher && CanScrapDropToCrusherFromAbove(carriedScrap);

        root.SetParent(carriedScrap.OriginalParent, true);
        root.SetPositionAndRotation(releaseWorldPosition, releaseWorldRotation);
        if (releasedItem != null)
        {
            if (shouldUsePhysicsDrop)
            {
                releasedItem.MarkReleasedForCrusherDrop();
            }
            else
            {
                releasedItem.ClearCrusherDropState();
            }

            releasedItem.SetCanBeGrabbed(true);
        }

        if (carriedScrap.Body != null)
        {
            carriedScrap.Body.isKinematic = carriedScrap.WasKinematic;
            carriedScrap.Body.useGravity = carriedScrap.UsedGravity;
            carriedScrap.Body.detectCollisions = carriedScrap.DetectedCollisions;
            if (!carriedScrap.Body.isKinematic)
            {
                carriedScrap.Body.velocity = Vector3.zero;
                carriedScrap.Body.angularVelocity = Vector3.zero;
            }

            carriedScrap.Body.position = releaseWorldPosition;
            carriedScrap.Body.rotation = releaseWorldRotation;
        }

        if (carriedScrap.Colliders != null && carriedScrap.ColliderTriggerStates != null)
        {
            for (int i = 0; i < carriedScrap.Colliders.Length && i < carriedScrap.ColliderTriggerStates.Length; i++)
            {
                if (carriedScrap.Colliders[i] != null)
                {
                    carriedScrap.Colliders[i].isTrigger = carriedScrap.ColliderTriggerStates[i];
                }
            }
        }

        CarriedScrapState releasedState = carriedScrap;
        carriedScrap = null;

        if (shouldUsePhysicsDrop)
        {
            EnablePhysicsDrop(releasedState);
        }

        if (!shouldUsePhysicsDrop && dropToGroundWhenNoCrusher)
        {
            PlaceReleasedScrapOnGround(releasedState);
        }
    }

    private bool CanCurrentScrapDropToCrusherFromAbove()
    {
        if (!sendReleasedScrapToCrusher || carriedScrap == null || carriedScrap.Root == null)
        {
            return false;
        }

        return CanScrapDropToCrusherFromAbove(carriedScrap);
    }

    private bool CanScrapDropToCrusherFromAbove(CarriedScrapState scrapState)
    {
        if (!sendReleasedScrapToCrusher || scrapState == null || scrapState.Root == null)
        {
            return false;
        }

        Vector3[] probeOrigins = GetScrapDropProbeOrigins(scrapState);
        ScrapCrusherController[] crushers = FindObjectsOfType<ScrapCrusherController>();
        for (int i = 0; i < crushers.Length; i++)
        {
            if (crushers[i] == null)
            {
                continue;
            }

            for (int probeIndex = 0; probeIndex < probeOrigins.Length; probeIndex++)
            {
                if (crushers[i].IsDropRayOverIntake(probeOrigins[probeIndex], crusherDropProbeDistance))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Vector3[] GetScrapDropProbeOrigins(CarriedScrapState scrapState)
    {
        if (scrapState == null || scrapState.Root == null)
        {
            Vector3 fallback = carryPoint != null ? carryPoint.position : transform.position;
            return new[] { fallback };
        }

        Bounds bounds = GetWorldBounds(scrapState.Root, scrapState.Colliders);
        float y = bounds.center.y + 0.25f;
        return new[]
        {
            new Vector3(bounds.center.x, y, bounds.center.z),
            new Vector3(bounds.min.x, y, bounds.min.z),
            new Vector3(bounds.min.x, y, bounds.max.z),
            new Vector3(bounds.max.x, y, bounds.min.z),
            new Vector3(bounds.max.x, y, bounds.max.z)
        };
    }

    private void EnablePhysicsDrop(CarriedScrapState releasedState)
    {
        if (releasedState == null || releasedState.Root == null)
        {
            return;
        }

        Rigidbody body = releasedState.Body;
        if (body == null)
        {
            body = releasedState.Root.gameObject.AddComponent<Rigidbody>();
            releasedState.Body = body;
            releasedState.HadRigidbody = true;
        }

        body.isKinematic = false;
        body.useGravity = true;
        body.detectCollisions = true;
        body.position = releasedState.Root.position;
        body.rotation = releasedState.Root.rotation;

        body.WakeUp();
        body.velocity = Vector3.down * physicsDropInitialDownVelocity;
        body.angularVelocity = Vector3.zero;
    }

    private static void ClearRigidbodyVelocityIfDynamic(Rigidbody body)
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void PlaceReleasedScrapOnGround(CarriedScrapState releasedState)
    {
        if (releasedState == null || releasedState.Root == null)
        {
            return;
        }

        Transform root = releasedState.Root;
        Bounds bounds = GetWorldBounds(root, releasedState.Colliders);
        Vector3 probeStart = bounds.center + Vector3.up * 0.25f;
        RaycastHit[] hits = Physics.RaycastAll(probeStart, Vector3.down, groundDropProbeDistance, groundDropLayers, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
        {
            return;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null || hitCollider.transform.IsChildOf(root) || (claw != null && hitCollider.transform.IsChildOf(claw)))
            {
                continue;
            }

            float bottomY = bounds.min.y;
            float deltaY = hits[i].point.y + groundDropOffset - bottomY;
            root.position += Vector3.up * deltaY;
            return;
        }
    }

    private static Bounds GetWorldBounds(Transform root, Collider[] colliders)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(root.position, Vector3.zero);
        if (colliders != null)
        {
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
        }

        return bounds;
    }

    private void ResolveReferences()
    {
        if (areaGarra == null)
        {
            areaGarra = transform;
        }

        if (movementArea == null && areaGarra != null)
        {
            movementArea = FindChildRecursive(areaGarra, "area de percurso");
        }

        if (movingAxis == null && movementArea != null)
        {
            movingAxis = FindChildRecursive(movementArea, "eixo movimento");
        }

        if (claw == null && movingAxis != null)
        {
            claw = FindChildRecursive(movingAxis, "garra");
        }

        if (bounds == null && movementArea != null)
        {
            bounds = movementArea.GetComponent<ScrapCraneBounds>();
        }

        if (grabDetectionZone == null && claw != null)
        {
            grabDetectionZone = claw.GetComponentInChildren<ScrapGrabDetectionZone>(true);
        }

        if (carryPoint == null && claw != null)
        {
            Transform found = FindChildRecursive(claw, "CarryPoint");
            carryPoint = found;
        }
    }

    private void ApplyInitialBladeState()
    {
        currentBladeState = initialBladeState == BladeState.Open ? BladeState.Open : BladeState.Closed;
        ApplyBladePose(currentBladeState == BladeState.Open);
    }

    private void ApplyBladePose(bool open)
    {
        SetLocalRotation(blade01, Quaternion.Euler(open ? blade01.openLocalEuler : blade01.closedLocalEuler));
        SetLocalRotation(blade02, Quaternion.Euler(open ? blade02.openLocalEuler : blade02.closedLocalEuler));
        SetLocalRotation(blade03, Quaternion.Euler(open ? blade03.openLocalEuler : blade03.closedLocalEuler));
    }

    private void CaptureBladeRotations(bool closed)
    {
        CaptureBladeRotation(blade01, closed);
        CaptureBladeRotation(blade02, closed);
        CaptureBladeRotation(blade03, closed);
    }

    private void CaptureBladeRotation(BladeConfiguration blade, bool closed)
    {
        if (blade == null || blade.pivot == null)
        {
            return;
        }

        if (closed)
        {
            blade.closedLocalEuler = blade.pivot.localEulerAngles;
        }
        else
        {
            blade.openLocalEuler = blade.pivot.localEulerAngles;
        }
    }

    private Quaternion GetLocalRotation(BladeConfiguration blade)
    {
        return blade != null && blade.pivot != null ? blade.pivot.localRotation : Quaternion.identity;
    }

    private void SetLocalRotation(BladeConfiguration blade, Quaternion rotation)
    {
        if (blade != null && blade.pivot != null)
        {
            blade.pivot.localRotation = rotation;
        }
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

    private void Warn(string message)
    {
        if (logWarnings)
        {
            Debug.LogWarning($"{name}: {message}", this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || claw == null)
        {
            return;
        }

        Vector3 localPosition = claw.localPosition;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(claw.parent.TransformPoint(new Vector3(localPosition.x, upperLocalY, localPosition.z)), 0.22f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(claw.parent.TransformPoint(new Vector3(localPosition.x, lowerLocalY, localPosition.z)), 0.22f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(claw.parent.TransformPoint(new Vector3(localPosition.x, releaseLowerLocalY, localPosition.z)), 0.18f);

        if (carryPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(carryPoint.position, 0.18f);
        }
    }
}
