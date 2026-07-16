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
    [SerializeField] private ConveyorController destinationConveyor;
    [SerializeField] private Renderer indicatorLightRenderer;
    [SerializeField] private Light indicatorLight;

    [Header("Product")]
    [SerializeField] private RoboticArmProductType acceptedProductType = RoboticArmProductType.Custom;
    [SerializeField, Tooltip("Optional exact prefab accepted by this arm. ProductId is still checked when this is empty.")] private GameObject acceptedPrefab;
    [SerializeField] private string acceptedProductId = "RawMaterial_A";
    [SerializeField] private Vector3 itemSocketLocalPosition;
    [SerializeField] private Vector3 itemSocketLocalRotation;
    [SerializeField] private bool useDropPointRotation = true;

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
    [SerializeField] private float rotationToDropAngle = 180f;
    [SerializeField, Min(0.01f)] private float angularTolerance = 1f;
    [SerializeField, Min(0.001f)] private float positionTolerance = 0.03f;
    [SerializeField] private bool invertDropRotation;
    [SerializeField] private bool keepProductOrientationWhileCarried;
    [SerializeField] private bool useSafeLiftPoint = true;
    [SerializeField, Min(0f)] private float pickupArrivalTimeout = 2f;

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

    public ArmState CurrentState => currentState;
    public RoboticArmProductType AcceptedProductType => acceptedProductType;
    public string AcceptedProductId => acceptedProductId;
    public bool IsBusy => currentState != ArmState.Idle && currentState != ArmState.Error;

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

        ConveyorItem nextItem = pickupSensor != null ? pickupSensor.DequeueNextValid() : null;
        if (nextItem != null)
        {
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

        if (acceptedPrefab != null && !IsPrefabMatch(item.gameObject, acceptedPrefab))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(acceptedProductId))
        {
            return string.Equals(item.ProductId, acceptedProductId, System.StringComparison.OrdinalIgnoreCase);
        }

        return true;
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
        yield return WaitForItemAtPickup(item);
        if (item == null || pickupPoint == null || Vector3.Distance(item.transform.position, pickupPoint.position) > positionTolerance)
        {
            AbortCycle("item did not reach the pickup point in time.");
            yield break;
        }

        item.HoldForRoboticPickup(pickupPoint);

        ChangeState(ArmState.MovingToPickup, Color.cyan);
        yield return MovePose(pickupPose, pickupMovementSpeed);

        ChangeState(ArmState.ClosingGripper, Color.cyan);
        while (gripper != null && !gripper.MoveClosed(gripperSpeed, Time.deltaTime))
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

        gripper.Attach(currentItem, itemSocketLocalPosition, itemSocketLocalRotation);
        yield return null;

        ChangeState(ArmState.Lifting, Color.cyan);
        yield return MovePose(liftPose, pickupMovementSpeed);

        ChangeState(ArmState.RotatingToDrop, Color.cyan);
        yield return RotateBaseToDrop();

        ChangeState(ArmState.MovingToDrop, Color.cyan);
        yield return MovePose(dropPose, dropMovementSpeed);

        ChangeState(ArmState.WaitingForDropArea, Color.yellow);
        while (dropAreaSensor != null && dropAreaSensor.IsOccupied)
        {
            yield return null;
        }

        yield return Wait(delayBeforeRelease);

        ChangeState(ArmState.OpeningGripper, Color.cyan);
        while (gripper != null && !gripper.MoveOpen(gripperSpeed, Time.deltaTime))
        {
            yield return null;
        }

        ChangeState(ArmState.ReleasingItem, Color.cyan);
        if (currentItem != null)
        {
            gripper.Release(currentItem, destinationConveyor, dropPoint, useDropPointRotation);
            if (logItemEvents)
            {
                Debug.Log($"{name}: released {currentItem.ProductId}.", this);
            }
        }

        currentItem = null;
        yield return Wait(delayAfterRelease);
        yield return Wait(delayBeforeReturn);

        ChangeState(ArmState.ReturningHome, Color.cyan);
        yield return MovePose(liftPose, returnSpeed);
        yield return ReturnBaseHome();
        yield return MovePose(homePose, returnSpeed);
        while (gripper != null && !gripper.MoveOpen(gripperSpeed, Time.deltaTime))
        {
            yield return null;
        }

        ChangeState(ArmState.Idle, Color.green);
        cycleRoutine = null;
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
        while (item != null && pickupPoint != null && Vector3.Distance(item.transform.position, pickupPoint.position) > positionTolerance)
        {
            if (pickupArrivalTimeout > 0f && elapsed >= pickupArrivalTimeout)
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator MovePose(RoboticArmPose pose, float speedMultiplier)
    {
        while (!IsPoseReached(pose))
        {
            float deltaTime = Time.deltaTime * Mathf.Max(0.01f, speedMultiplier);
            RotateLocal(pivotBaseRotation, pose.baseRotation, baseRotationSpeed * deltaTime);
            RotateLocal(pivotShoulder, pose.shoulderRotation, shoulderSpeed * deltaTime);
            RotateLocal(pivotElbow, pose.elbowRotation, elbowSpeed * deltaTime);
            RotateLocal(pivotWrist, pose.wristRotation, wristSpeed * deltaTime);

            if (keepProductOrientationWhileCarried && currentItem != null && itemSocket != null)
            {
                currentItem.transform.localRotation = Quaternion.Euler(itemSocketLocalRotation);
            }

            yield return null;
        }
    }

    private IEnumerator RotateBaseToDrop()
    {
        if (pivotBaseRotation == null)
        {
            yield break;
        }

        float signedAngle = Mathf.Abs(rotationToDropAngle) * (invertDropRotation ? -1f : 1f);
        Quaternion startRotation = hasHomeBaseRotation ? homeBaseRotation : pivotBaseRotation.localRotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(0f, signedAngle, 0f);
        while (Quaternion.Angle(pivotBaseRotation.localRotation, targetRotation) > angularTolerance)
        {
            pivotBaseRotation.localRotation = Quaternion.RotateTowards(pivotBaseRotation.localRotation, targetRotation, baseRotationSpeed * Time.deltaTime);
            yield return null;
        }
    }

    private IEnumerator ReturnBaseHome()
    {
        if (pivotBaseRotation == null || !hasHomeBaseRotation)
        {
            yield break;
        }

        while (Quaternion.Angle(pivotBaseRotation.localRotation, homeBaseRotation) > angularTolerance)
        {
            pivotBaseRotation.localRotation = Quaternion.RotateTowards(pivotBaseRotation.localRotation, homeBaseRotation, baseRotationSpeed * Time.deltaTime);
            yield return null;
        }
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

    private bool IsPoseReached(RoboticArmPose pose)
    {
        return AngleReached(pivotBaseRotation, pose.baseRotation)
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
