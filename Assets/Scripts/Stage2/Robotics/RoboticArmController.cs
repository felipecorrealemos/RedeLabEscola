using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class RoboticArmController : MonoBehaviour
{
    public enum ArmState
    {
        Idle,
        PreparingPickup,
        MovingToPickup,
        ClosingGripper,
        AttachingItem,
        Lifting,
        RotatingToDrop,
        MovingToDrop,
        WaitingForDropArea,
        OpeningGripper,
        ReleasingItem,
        ReturningHome,
        Error
    }

    [Header("References")]
    [SerializeField] private Transform pivotBaseRotation;
    [SerializeField] private Transform pivotShoulder;
    [SerializeField] private Transform pivotElbow;
    [SerializeField] private Transform pivotWrist;
    [SerializeField] private Transform pivotClawLeft;
    [SerializeField] private Transform pivotClawRight;
    [SerializeField] private Transform itemSocket;
    [SerializeField] private Transform pickupPoint;
    [SerializeField] private Transform dropPoint;
    [SerializeField] private Transform safeLiftPoint;
    [SerializeField] private RoboticArmPickupSensor pickupSensor;
    [SerializeField] private RoboticArmDropAreaSensor dropAreaSensor;
    [SerializeField] private RoboticArmGripper gripper;
    [SerializeField] private RoboticArmNetworkAdapter networkAdapter;
    [SerializeField] private ConveyorController destinationConveyor;
    [SerializeField] private Renderer indicatorLightRenderer;
    [SerializeField] private Light indicatorLight;
    [SerializeField] private bool useOperationalIndicator;

    [Header("Product")]
    [SerializeField] private RoboticArmProductType acceptedProductType = RoboticArmProductType.Custom;
    [SerializeField, Tooltip("Optional exact prefab accepted by this arm. ProductId is still checked when this is empty.")] private GameObject acceptedPrefab;
    [SerializeField] private string acceptedProductId = "RawMaterial_A";
    [SerializeField] private Vector3 itemSocketLocalPosition;
    [SerializeField] private Vector3 itemSocketLocalRotation;
    [SerializeField] private bool useDropPointRotation = true;

    [Header("Fallback Pickup")]
    [SerializeField] private bool allowAnyProductWhenIdle;
    [SerializeField, Min(0f)] private float fallbackPickupIdleTime = 4f;
    [SerializeField] private bool fallbackOnlyWhenNoAcceptedItem = true;

    [Header("Poses")]
    [SerializeField] private RoboticArmPose homePose = new RoboticArmPose();
    [SerializeField] private RoboticArmPose pickupPose = new RoboticArmPose();
    [SerializeField] private RoboticArmPose liftPose = new RoboticArmPose();
    [SerializeField] private RoboticArmPose dropPose = new RoboticArmPose();

    [Header("Speeds")]
    [SerializeField, Min(1f)] private float baseRotationSpeed = 90f;
    [SerializeField, Min(1f)] private float shoulderSpeed = 90f;
    [SerializeField, Min(1f)] private float elbowSpeed = 90f;
    [SerializeField, Min(1f)] private float wristSpeed = 90f;
    [SerializeField, Min(0.01f)] private float gripperSpeed = 0.35f;
    [SerializeField, Min(0.01f)] private float pickupMovementSpeed = 1f;
    [SerializeField, Min(0.01f)] private float dropMovementSpeed = 1f;
    [SerializeField, Min(0.01f)] private float returnSpeed = 1f;

    [Header("Times")]
    [SerializeField, Min(0f)] private float delayBeforePickup = 0.1f;
    [SerializeField, Min(0f)] private float delayAfterClosingGripper = 0.15f;
    [SerializeField, Min(0f)] private float delayBeforeRelease = 0.1f;
    [SerializeField, Min(0f)] private float delayAfterRelease = 0.1f;
    [SerializeField, Min(0f)] private float delayBeforeReturn = 0.1f;

    [Header("Movement")]
    [SerializeField, Tooltip("Signed local rotation applied from the home base rotation when moving to the drop side. Use negative values to rotate the opposite direction.")]
    private float rotationToDropAngle = 180f;
    [SerializeField, Tooltip("Local axis used to rotate the base toward the drop side.")]
    private Vector3 baseRotationAxis = Vector3.forward;
    [SerializeField, Min(0.01f)] private float angularTolerance = 1f;
    [SerializeField, Min(0.001f)] private float positionTolerance = 0.03f;
    [SerializeField] private bool invertDropRotation;
    [SerializeField] private bool keepProductOrientationWhileCarried;
    [SerializeField] private bool useSafeLiftPoint = true;
    [SerializeField, Min(0f)] private float pickupArrivalTimeout = 2f;
    [SerializeField, Min(0.001f)] private float pickupHoldTolerance = 0.45f;
    [SerializeField] private bool smoothItemToSocketAfterAttach = true;
    [SerializeField, Min(0.01f)] private float itemToSocketSpeed = 2.8f;
    [SerializeField, Min(0.01f)] private float itemToSocketRotationSpeed = 360f;
    [SerializeField, Min(0.05f)] private float itemToSocketTimeout = 0.75f;
    [SerializeField] private Vector3 wristRaisedRotation = Vector3.zero;
    [SerializeField] private Vector3 wristPickupLoweredRotation = new Vector3(-40f, 0f, 0f);
    [SerializeField] private Vector3 wristDropLoweredRotation = new Vector3(-40f, 0f, 0f);
    [SerializeField, Min(1f)] private float wristPickupDropSpeedMultiplier = 2.5f;
    [SerializeField, Min(1f)] private float gripperCloseSpeedMultiplier = 3f;
    [SerializeField] private bool waitForDropAreaToClear;
    [SerializeField, Min(0f)] private float maxDropAreaWaitTime = 0.5f;
    [SerializeField] private bool requireDestinationConveyorSpaceBeforeDrop = true;
    [SerializeField] private bool useDropPoseBeforeRelease;
    [SerializeField] private bool handReleasedItemToDestinationConveyor;
    [SerializeField] private bool smoothReleaseToDropPoint = true;
    [SerializeField, Min(0.01f)] private float releaseSmoothDuration = 0.25f;
    [SerializeField, Min(0.05f)] private float maxPoseMoveTime = 1.5f;
    [SerializeField, Min(0.05f)] private float maxWristMoveTime = 0.8f;
    [SerializeField, Min(0.05f)] private float maxBaseRotationTime = 2f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private bool logStateTransitions;
    [SerializeField] private bool logItemEvents;
    [SerializeField] private ArmState currentState = ArmState.Idle;

    private MaterialPropertyBlock propertyBlock;
    private ConveyorItem currentItem;
    private Coroutine cycleRoutine;
    private Quaternion homeBaseRotation;
    private bool hasHomeBaseRotation;
    private float currentBaseDropOffset;
    private float idleWithoutAcceptedItemTimer;

    public ArmState CurrentState => currentState;
    public RoboticArmProductType AcceptedProductType => acceptedProductType;
    public string AcceptedProductId => acceptedProductId;
    public bool IsBusy => currentState != ArmState.Idle && currentState != ArmState.Error;
    public bool CanStartAuthorizedCycle => networkAdapter != null && networkAdapter.CanStartNewCycle;

    private void Awake()
    {
        ResolveReferences();
        CacheHomeBaseRotation();
        SetIndicator(Color.green);
    }

    private void OnValidate()
    {
        pickupMovementSpeed = Mathf.Max(0.01f, pickupMovementSpeed);
        dropMovementSpeed = Mathf.Max(0.01f, dropMovementSpeed);
        returnSpeed = Mathf.Max(0.01f, returnSpeed);
        angularTolerance = Mathf.Max(0.01f, angularTolerance);
        positionTolerance = Mathf.Max(0.001f, positionTolerance);
        pickupHoldTolerance = Mathf.Max(0.001f, pickupHoldTolerance);
        itemToSocketSpeed = Mathf.Max(0.01f, itemToSocketSpeed);
        itemToSocketRotationSpeed = Mathf.Max(0.01f, itemToSocketRotationSpeed);
        itemToSocketTimeout = Mathf.Max(0.05f, itemToSocketTimeout);
        wristPickupDropSpeedMultiplier = Mathf.Max(1f, wristPickupDropSpeedMultiplier);
        gripperCloseSpeedMultiplier = Mathf.Max(1f, gripperCloseSpeedMultiplier);
        maxDropAreaWaitTime = Mathf.Max(0f, maxDropAreaWaitTime);
        releaseSmoothDuration = Mathf.Max(0.01f, releaseSmoothDuration);
        maxPoseMoveTime = Mathf.Max(0.05f, maxPoseMoveTime);
        maxWristMoveTime = Mathf.Max(0.05f, maxWristMoveTime);
        maxBaseRotationTime = Mathf.Max(0.05f, maxBaseRotationTime);
    }

    private void Update()
    {
        if (currentState != ArmState.Idle || cycleRoutine != null)
        {
            if (currentState == ArmState.Error && cycleRoutine == null)
            {
                StartCoroutine(RecoverFromError());
            }

            return;
        }

        if (networkAdapter == null)
        {
            networkAdapter = GetComponent<RoboticArmNetworkAdapter>();
        }

        if (!CanStartAuthorizedCycle)
        {
            idleWithoutAcceptedItemTimer = 0f;
            return;
        }

        ConveyorItem nextItem = pickupSensor != null ? pickupSensor.DequeueNextValid() : null;
        if (nextItem == null)
        {
            idleWithoutAcceptedItemTimer += Time.deltaTime;
            if (allowAnyProductWhenIdle && idleWithoutAcceptedItemTimer >= fallbackPickupIdleTime)
            {
                nextItem = fallbackOnlyWhenNoAcceptedItem && pickupSensor != null ? pickupSensor.DequeueNextValid() : null;
                if (nextItem == null)
                {
                    nextItem = pickupSensor != null ? pickupSensor.DequeueNextAvailable() : null;
                }
            }
        }

        if (nextItem != null)
        {
            idleWithoutAcceptedItemTimer = 0f;
            cycleRoutine = StartCoroutine(RunCycle(nextItem));
        }
    }

    public void ConfigureReferences(
        Transform basePivot,
        Transform shoulder,
        Transform elbow,
        Transform wrist,
        Transform leftClaw,
        Transform rightClaw,
        Transform socket,
        Transform pickup,
        Transform drop,
        Transform safeLift,
        RoboticArmPickupSensor pickupSensorComponent,
        RoboticArmDropAreaSensor dropSensorComponent,
        RoboticArmGripper gripperComponent,
        Renderer lightRenderer)
    {
        pivotBaseRotation = basePivot;
        pivotShoulder = shoulder;
        pivotElbow = elbow;
        pivotWrist = wrist;
        pivotClawLeft = leftClaw;
        pivotClawRight = rightClaw;
        itemSocket = socket;
        pickupPoint = pickup;
        dropPoint = drop;
        safeLiftPoint = safeLift;
        pickupSensor = pickupSensorComponent;
        dropAreaSensor = dropSensorComponent;
        gripper = gripperComponent;
        indicatorLightRenderer = lightRenderer;

        pickupSensor?.Configure(this);
        gripper?.Configure(pivotClawLeft, pivotClawRight, itemSocket);
        CacheHomeBaseRotation();
    }

    public void ConfigureProduct(RoboticArmProductType type, string productId, ConveyorController targetConveyor)
    {
        acceptedProductType = type;
        acceptedProductId = productId;
        destinationConveyor = targetConveyor;
        handReleasedItemToDestinationConveyor = targetConveyor != null;
        waitForDropAreaToClear = targetConveyor != null;
        requireDestinationConveyorSpaceBeforeDrop = targetConveyor != null;
        maxDropAreaWaitTime = targetConveyor != null ? 0f : maxDropAreaWaitTime;
    }

    public void ConfigureAnyProductFallback(bool enabled, float idleTime)
    {
        allowAnyProductWhenIdle = enabled;
        fallbackPickupIdleTime = Mathf.Max(0f, idleTime);
    }

    public void CaptureCurrentPoseAsHome()
    {
        homePose = CapturePose();
        pickupPose = CapturePose();
        liftPose = CapturePose();
        dropPose = CapturePose();
        CacheHomeBaseRotation();
    }

    public bool CanAcceptItem(ConveyorItem item)
    {
        if (item == null || item.IsReservedForCollection || item.IsBeingCarried)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(acceptedProductId))
        {
            return string.Equals(item.ProductId, acceptedProductId, System.StringComparison.OrdinalIgnoreCase);
        }

        return acceptedPrefab == null || IsPrefabMatch(item.gameObject, acceptedPrefab);
    }

    public bool CanStartPickup(ConveyorItem item)
    {
        return CanAcceptItem(item)
            && pickupPoint != null
            && Vector3.Distance(item.transform.position, pickupPoint.position) <= pickupHoldTolerance;
    }

    public void ReportDetectedItem(ConveyorItem item)
    {
        if (logItemEvents && item != null)
        {
            Debug.Log($"{name}: detected {item.ProductId}.", this);
        }

        if (currentState == ArmState.Idle)
        {
            SetIndicator(Color.yellow);
        }
    }

    public void ReportRejectedItem(ConveyorItem item, GameObject source)
    {
        if (logItemEvents)
        {
            string itemName = item != null ? $"{item.ProductId} ({item.name})" : source != null ? source.name : "Unknown";
            Debug.Log($"{name}: rejected {itemName}.", this);
        }
    }

    private IEnumerator RunCycle(ConveyorItem item)
    {
        currentItem = item;

        if (!ValidateRequiredReferences())
        {
            yield break;
        }

        ChangeState(ArmState.PreparingPickup, Color.yellow);
        if (!item.TryReserveForRoboticArm())
        {
            AbortCycle("item is already reserved.");
            yield break;
        }

        yield return Wait(delayBeforePickup);

        ChangeState(ArmState.MovingToPickup, Color.cyan);
        yield return MovePose(pickupPose, pickupMovementSpeed);
        yield return MoveWristTo(wristPickupLoweredRotation, wristPickupDropSpeedMultiplier);

        ChangeState(ArmState.ClosingGripper, Color.cyan);
        while (gripper != null && !gripper.MoveClosed(gripperSpeed * gripperCloseSpeedMultiplier, Time.deltaTime))
        {
            yield return null;
        }

        yield return Wait(delayAfterClosingGripper);

        ChangeState(ArmState.AttachingItem, Color.cyan);
        if (currentItem == null)
        {
            AbortCycle("item was destroyed before attach.");
            yield break;
        }

        gripper.Attach(currentItem, itemSocketLocalPosition, itemSocketLocalRotation, !smoothItemToSocketAfterAttach);
        if (smoothItemToSocketAfterAttach)
        {
            yield return MoveItemToSocket(currentItem);
        }

        yield return null;

        ChangeState(ArmState.Lifting, Color.cyan);
        yield return MoveWristTo(wristRaisedRotation, wristPickupDropSpeedMultiplier);
        yield return MovePose(liftPose, pickupMovementSpeed, false);

        ChangeState(ArmState.RotatingToDrop, Color.cyan);
        yield return RotateBaseToDrop();

        ChangeState(ArmState.MovingToDrop, Color.cyan);
        if (useDropPoseBeforeRelease)
        {
            yield return MovePose(dropPose, dropMovementSpeed, false);
        }

        yield return MoveWristTo(wristDropLoweredRotation, wristPickupDropSpeedMultiplier);

        if (waitForDropAreaToClear)
        {
            ChangeState(ArmState.WaitingForDropArea, Color.yellow);
            while (IsDropBlocked())
            {
                yield return null;
            }
        }

        yield return Wait(delayBeforeRelease);

        ChangeState(ArmState.ReleasingItem, Color.cyan);
        yield return ReleaseCurrentItemAtDropPoint();

        ChangeState(ArmState.OpeningGripper, Color.cyan);
        while (gripper != null && !gripper.MoveOpen(gripperSpeed, Time.deltaTime))
        {
            yield return null;
        }

        currentItem = null;
        yield return Wait(delayAfterRelease);
        yield return Wait(delayBeforeReturn);
        yield return MoveWristTo(wristRaisedRotation, wristPickupDropSpeedMultiplier);

        ChangeState(ArmState.ReturningHome, Color.cyan);
        yield return MovePose(liftPose, returnSpeed, false);
        yield return ReturnBaseHome();
        yield return MovePose(homePose, returnSpeed, false);
        while (gripper != null && !gripper.MoveOpen(gripperSpeed, Time.deltaTime))
        {
            yield return null;
        }

        ChangeState(ArmState.Idle, Color.green);
        cycleRoutine = null;
    }

    private IEnumerator ReleaseCurrentItemAtDropPoint()
    {
        if (currentItem == null)
        {
            yield break;
        }

        ConveyorItem releasedItem = currentItem;
        ConveyorController conveyorForRelease = handReleasedItemToDestinationConveyor ? destinationConveyor : null;
        if (smoothReleaseToDropPoint && dropPoint != null)
        {
            Transform destinationParent = conveyorForRelease != null ? conveyorForRelease.transform : null;
            releasedItem.PrepareSmoothRoboticDrop(destinationParent);

            Vector3 startPosition = releasedItem.transform.position;
            Quaternion startRotation = releasedItem.transform.rotation;
            Vector3 targetPosition = dropPoint.position;
            Quaternion targetRotation = useDropPointRotation ? dropPoint.rotation : startRotation;
            float elapsed = 0f;

            while (elapsed < releaseSmoothDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / releaseSmoothDuration);
                t = t * t * (3f - 2f * t);
                releasedItem.MoveDuringSmoothRoboticDrop(
                    Vector3.Lerp(startPosition, targetPosition, t),
                    Quaternion.Slerp(startRotation, targetRotation, t));
                yield return null;
            }

            releasedItem.MoveDuringSmoothRoboticDrop(targetPosition, targetRotation);
            releasedItem.CompleteRoboticDrop(conveyorForRelease, dropPoint, useDropPointRotation);
        }
        else
        {
            gripper.Release(releasedItem, conveyorForRelease, dropPoint, useDropPointRotation);
        }

        if (logItemEvents)
        {
            Debug.Log($"{name}: released {releasedItem.ProductId}.", this);
        }
    }

    private bool IsDropBlocked()
    {
        if (dropAreaSensor != null && dropAreaSensor.IsOccupied)
        {
            return true;
        }

        return requireDestinationConveyorSpaceBeforeDrop
            && handReleasedItemToDestinationConveyor
            && destinationConveyor != null
            && !destinationConveyor.CanReceiveItemAt(dropPoint);
    }

    private IEnumerator RecoverFromError()
    {
        cycleRoutine = StartCoroutine(ReturnHomeAfterError());
        yield return cycleRoutine;
        cycleRoutine = null;
    }

    private IEnumerator ReturnHomeAfterError()
    {
        yield return MovePose(homePose, returnSpeed);
        while (gripper != null && !gripper.MoveOpen(gripperSpeed, Time.deltaTime))
        {
            yield return null;
        }

        ChangeState(ArmState.Idle, Color.green);
    }

    private IEnumerator WaitForItemAtPickup(ConveyorItem item)
    {
        float elapsed = 0f;
        while (item != null && pickupPoint != null && Vector3.Distance(item.transform.position, pickupPoint.position) > pickupHoldTolerance)
        {
            if (pickupArrivalTimeout > 0f && elapsed >= pickupArrivalTimeout)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator MovePose(RoboticArmPose pose, float speedMultiplier, bool rotateBase = true)
    {
        float elapsed = 0f;
        while (!IsPoseReached(pose, rotateBase) && elapsed < maxPoseMoveTime)
        {
            float deltaTime = Time.deltaTime * Mathf.Max(0.01f, speedMultiplier);
            if (rotateBase)
            {
                RotateLocal(pivotBaseRotation, pose.baseRotation, baseRotationSpeed * deltaTime);
            }

            RotateLocal(pivotShoulder, pose.shoulderRotation, shoulderSpeed * deltaTime);
            RotateLocal(pivotElbow, pose.elbowRotation, elbowSpeed * deltaTime);
            RotateLocal(pivotWrist, pose.wristRotation, wristSpeed * deltaTime);

            if (keepProductOrientationWhileCarried && currentItem != null && itemSocket != null)
            {
                currentItem.transform.localRotation = Quaternion.Euler(itemSocketLocalRotation);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator MoveWristTo(Vector3 localEulerAngles, float speedMultiplier)
    {
        if (pivotWrist == null)
        {
            yield break;
        }

        Quaternion targetRotation = Quaternion.Euler(localEulerAngles);
        float speed = wristSpeed * Mathf.Max(0.01f, speedMultiplier);
        float elapsed = 0f;
        while (Quaternion.Angle(pivotWrist.localRotation, targetRotation) > angularTolerance && elapsed < maxWristMoveTime)
        {
            pivotWrist.localRotation = Quaternion.RotateTowards(pivotWrist.localRotation, targetRotation, speed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator MoveItemToSocket(ConveyorItem item)
    {
        if (item == null || itemSocket == null)
        {
            yield break;
        }

        Transform itemTransform = item.transform;
        Quaternion targetRotation = itemSocket.rotation * Quaternion.Euler(itemSocketLocalRotation);
        Vector3 targetPosition = itemSocket.TransformPoint(itemSocketLocalPosition);
        float elapsed = 0f;

        while (itemTransform != null
            && elapsed < itemToSocketTimeout
            && (Vector3.Distance(itemTransform.position, targetPosition) > positionTolerance
                || Quaternion.Angle(itemTransform.rotation, targetRotation) > angularTolerance))
        {
            targetPosition = itemSocket.TransformPoint(itemSocketLocalPosition);
            targetRotation = itemSocket.rotation * Quaternion.Euler(itemSocketLocalRotation);
            Vector3 nextPosition = Vector3.MoveTowards(itemTransform.position, targetPosition, itemToSocketSpeed * Time.deltaTime);
            Quaternion nextRotation = Quaternion.RotateTowards(itemTransform.rotation, targetRotation, itemToSocketRotationSpeed * Time.deltaTime);
            item.MoveDuringSmoothRoboticCarry(nextPosition, nextRotation);
            elapsed += Time.deltaTime;
            yield return null;
        }

        item.FinishSmoothRoboticCarry();
    }

    private IEnumerator RotateBaseToDrop()
    {
        if (pivotBaseRotation == null)
        {
            yield break;
        }

        float signedAngle = invertDropRotation ? -rotationToDropAngle : rotationToDropAngle;
        Quaternion startRotation = hasHomeBaseRotation ? homeBaseRotation : pivotBaseRotation.localRotation;
        currentBaseDropOffset = 0f;
        float elapsed = 0f;
        while (Mathf.Abs(signedAngle - currentBaseDropOffset) > angularTolerance && elapsed < maxBaseRotationTime)
        {
            currentBaseDropOffset = Mathf.MoveTowards(currentBaseDropOffset, signedAngle, baseRotationSpeed * Time.deltaTime);
            pivotBaseRotation.localRotation = startRotation * BaseOffsetRotation(currentBaseDropOffset);
            elapsed += Time.deltaTime;
            yield return null;
        }

        currentBaseDropOffset = signedAngle;
        pivotBaseRotation.localRotation = startRotation * BaseOffsetRotation(signedAngle);
    }

    private IEnumerator ReturnBaseHome()
    {
        if (pivotBaseRotation == null || !hasHomeBaseRotation)
        {
            yield break;
        }

        while (Mathf.Abs(currentBaseDropOffset) > angularTolerance)
        {
            currentBaseDropOffset = Mathf.MoveTowards(currentBaseDropOffset, 0f, baseRotationSpeed * Time.deltaTime);
            pivotBaseRotation.localRotation = homeBaseRotation * BaseOffsetRotation(currentBaseDropOffset);
            yield return null;
        }

        currentBaseDropOffset = 0f;
        pivotBaseRotation.localRotation = homeBaseRotation;
    }

    private Quaternion BaseOffsetRotation(float angle)
    {
        Vector3 axis = baseRotationAxis.sqrMagnitude > 0.0001f ? baseRotationAxis.normalized : Vector3.forward;
        return Quaternion.AngleAxis(angle, axis);
    }

    private void RotateLocal(Transform target, Vector3 eulerAngles, float maxDegreesDelta)
    {
        if (target == null)
        {
            return;
        }

        Quaternion desired = Quaternion.Euler(eulerAngles);
        target.localRotation = Quaternion.RotateTowards(target.localRotation, desired, maxDegreesDelta);
    }

    private bool IsPoseReached(RoboticArmPose pose, bool checkBaseRotation = true)
    {
        return (!checkBaseRotation || AngleReached(pivotBaseRotation, pose.baseRotation))
            && AngleReached(pivotShoulder, pose.shoulderRotation)
            && AngleReached(pivotElbow, pose.elbowRotation)
            && AngleReached(pivotWrist, pose.wristRotation);
    }

    private bool AngleReached(Transform target, Vector3 eulerAngles)
    {
        return target == null || Quaternion.Angle(target.localRotation, Quaternion.Euler(eulerAngles)) <= angularTolerance;
    }

    private IEnumerator Wait(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }
    }

    private bool ValidateRequiredReferences()
    {
        if (pivotBaseRotation == null || itemSocket == null || pickupPoint == null || dropPoint == null || pickupSensor == null || dropAreaSensor == null || gripper == null)
        {
            AbortCycle("required robotic arm reference is missing.");
            return false;
        }

        return true;
    }

    private void AbortCycle(string reason)
    {
        Debug.LogWarning($"{name}: robotic arm cycle cancelled because {reason}", this);
        if (currentItem != null)
        {
            currentItem.ReleaseReservation();
            currentItem = null;
        }

        ChangeState(ArmState.Error, Color.red);
        if (cycleRoutine != null)
        {
            StopCoroutine(cycleRoutine);
            cycleRoutine = null;
        }
    }

    private void ResolveReferences()
    {
        if (pickupSensor == null)
        {
            pickupSensor = GetComponentInChildren<RoboticArmPickupSensor>();
        }

        if (dropAreaSensor == null)
        {
            dropAreaSensor = GetComponentInChildren<RoboticArmDropAreaSensor>();
        }

        if (networkAdapter == null)
        {
            networkAdapter = GetComponent<RoboticArmNetworkAdapter>();
        }

        if (gripper == null)
        {
            gripper = GetComponentInChildren<RoboticArmGripper>();
        }

        pickupSensor?.Configure(this);
    }

    private RoboticArmPose CapturePose()
    {
        return new RoboticArmPose
        {
            baseRotation = pivotBaseRotation != null ? pivotBaseRotation.localEulerAngles : Vector3.zero,
            shoulderRotation = pivotShoulder != null ? pivotShoulder.localEulerAngles : Vector3.zero,
            elbowRotation = pivotElbow != null ? pivotElbow.localEulerAngles : Vector3.zero,
            wristRotation = pivotWrist != null ? pivotWrist.localEulerAngles : Vector3.zero
        };
    }

    private void CacheHomeBaseRotation()
    {
        if (pivotBaseRotation != null)
        {
            homeBaseRotation = pivotBaseRotation.localRotation;
            hasHomeBaseRotation = true;
            currentBaseDropOffset = 0f;
        }
    }

    private bool IsPrefabMatch(GameObject itemObject, GameObject prefab)
    {
        if (itemObject == null || prefab == null)
        {
            return true;
        }

        string prefabName = prefab.name.Replace("(Clone)", string.Empty).Trim();
        string itemName = itemObject.name.Replace("(Clone)", string.Empty).Trim();
        return itemName.StartsWith(prefabName, System.StringComparison.OrdinalIgnoreCase);
    }

    private void ChangeState(ArmState nextState, Color color)
    {
        if (currentState != nextState && logStateTransitions)
        {
            Debug.Log($"{name}: {currentState} -> {nextState}", this);
        }

        currentState = nextState;
        SetIndicator(color);
    }

    private void SetIndicator(Color color)
    {
        if (!useOperationalIndicator)
        {
            return;
        }

        if (indicatorLight != null)
        {
            indicatorLight.color = color;
            indicatorLight.enabled = true;
        }

        if (indicatorLightRenderer == null)
        {
            return;
        }

        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        indicatorLightRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);
        propertyBlock.SetColor("_EmissionColor", color);
        indicatorLightRenderer.SetPropertyBlock(propertyBlock);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos)
        {
            return;
        }

        DrawPoint(pickupPoint, Color.green, 0.18f);
        DrawPoint(dropPoint, Color.cyan, 0.18f);
        DrawPoint(safeLiftPoint, Color.yellow, 0.14f);
        DrawPoint(itemSocket, Color.magenta, 0.1f);

        if (pickupSensor != null)
        {
            DrawCollider(pickupSensor.GetComponent<Collider>(), new Color(0f, 1f, 0f, 0.4f));
        }

        if (dropAreaSensor != null)
        {
            DrawCollider(dropAreaSensor.GetComponent<Collider>(), new Color(0f, 0.7f, 1f, 0.4f));
        }
    }

    private void DrawPoint(Transform target, Color color, float radius)
    {
        if (target == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(target.position, radius);
        Gizmos.DrawLine(target.position, target.position + target.forward * 0.35f);
    }

    private void DrawCollider(Collider collider, Color color)
    {
        if (collider == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
    }
}
