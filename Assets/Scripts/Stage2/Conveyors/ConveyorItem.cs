using UnityEngine;

[DisallowMultipleComponent]
public class ConveyorItem : MonoBehaviour
{
    [SerializeField] private ConveyorItemState currentState = ConveyorItemState.Moving;
    [SerializeField] private string productId;
    [SerializeField] private float progressDistance;
    [SerializeField] private float lateralOffset;
    [SerializeField] private float currentMoveSpeed;
    [SerializeField] private bool reservedForCollection;
    [SerializeField] private int collectionQueueIndex = -1;
    [SerializeField] private int collectionQueueSlotIndex = -1;

    private ConveyorController controller;
    private ConveyorPath path;
    private Rigidbody itemRigidbody;
    private float verticalOffset;
    private float lateralBlendOffset;
    private bool isInitialized;
    private bool waitingForCollectionSpace;
    private bool wasRigidbodyKinematicBeforeCarry;
    private bool wasRigidbodyUsingGravityBeforeCarry;
    private bool wasRigidbodyDetectingCollisionsBeforeCarry;
    private Collider[] carriedColliders;
    private bool[] carriedColliderTriggerStates;
    private Transform carriedSocket;
    private Vector3 carriedLocalPosition;
    private Quaternion carriedLocalRotation;
    private bool lockToCarriedSocket;

    public ConveyorItemState CurrentState => currentState;
    public string ProductId => productId;
    public float ProgressDistance => progressDistance;
    public float LateralOffset => lateralOffset;
    public float CurrentMoveSpeed => currentMoveSpeed;
    public bool IsReservedForCollection => reservedForCollection;
    public int CollectionQueueIndex => collectionQueueIndex;
    public int CollectionQueueSlotIndex => collectionQueueSlotIndex;
    public bool IsAssignedToCollectionQueue => collectionQueueIndex >= 0 && collectionQueueSlotIndex >= 0;
    public bool IsStoppedForJamSensor => currentState == ConveyorItemState.WaitingForItem
        || currentState == ConveyorItemState.WaitingForMachine
        || currentState == ConveyorItemState.QueuedForCollection;
    public bool IsAvailableForCollection => (currentState == ConveyorItemState.QueuedForCollection || currentState == ConveyorItemState.WaitingForMachine)
        && collectionQueueSlotIndex <= 0
        && !reservedForCollection;
    public ConveyorController CurrentController => controller;
    public bool IsBeingCarried => currentState == ConveyorItemState.BeingCollected;

    private void Awake()
    {
        EnsurePhysicsSetup();
    }

    private void OnDestroy()
    {
        if (controller != null)
        {
            controller.UnregisterItem(this);
            controller = null;
        }
    }

    private void LateUpdate()
    {
        if (lockToCarriedSocket && carriedSocket != null && currentState == ConveyorItemState.BeingCollected)
        {
            transform.SetParent(carriedSocket, false);
            transform.localPosition = carriedLocalPosition;
            transform.localRotation = carriedLocalRotation;

            if (itemRigidbody != null)
            {
                itemRigidbody.position = transform.position;
                itemRigidbody.rotation = transform.rotation;
                itemRigidbody.velocity = Vector3.zero;
                itemRigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    public void Initialize(ConveyorController owner, ConveyorPath conveyorPath, string nextProductId, float startDistance, float nextLateralOffset)
    {
        controller = owner;
        path = conveyorPath;
        productId = nextProductId;
        progressDistance = Mathf.Max(0f, startDistance);
        lateralOffset = nextLateralOffset;
        lateralBlendOffset = nextLateralOffset;
        currentMoveSpeed = 0f;
        currentState = ConveyorItemState.Moving;
        reservedForCollection = false;
        waitingForCollectionSpace = false;
        collectionQueueIndex = -1;
        collectionQueueSlotIndex = -1;
        isInitialized = true;

        EnsurePhysicsSetup();
        SnapToPath();
    }

    public void TickItem(float deltaTime)
    {
        if (!isInitialized || controller == null || path == null || currentState == ConveyorItemState.BeingCollected || currentState == ConveyorItemState.Removed)
        {
            return;
        }

        if (currentState == ConveyorItemState.QueuedForCollection)
        {
            currentMoveSpeed = 0f;
            controller.MoveQueuedItemToCollectionSlot(this, itemRigidbody, deltaTime);
            return;
        }

        if (waitingForCollectionSpace)
        {
            currentMoveSpeed = 0f;
            if (controller.TryQueueItemForCollection(this))
            {
                waitingForCollectionSpace = false;
                controller.NotifyItemReachedCollectionPoint(this);
            }
            else
            {
                SnapToPath();
            }

            return;
        }

        if (currentState == ConveyorItemState.WaitingForMachine)
        {
            currentMoveSpeed = 0f;
            SnapToPath();
            return;
        }

        if (!IsAssignedToCollectionQueue && progressDistance >= path.TotalLength - controller.CollectionQueueAssignmentDistance)
        {
            currentMoveSpeed = 0f;
            if (!controller.TryQueueItemForCollection(this))
            {
                waitingForCollectionSpace = true;
                currentState = ConveyorItemState.WaitingForItem;
                SnapToPath();
                return;
            }
        }

        float targetSpeed = controller.CurrentSpeed;
        float blockerDistance = controller.GetDistanceToNearestBlockingItem(this);

        if (blockerDistance <= controller.MinimumItemSpacing)
        {
            targetSpeed = 0f;
            currentState = ConveyorItemState.WaitingForItem;
        }
        else if (blockerDistance < controller.ForwardDetectionDistance)
        {
            float speedFactor = Mathf.InverseLerp(controller.MinimumItemSpacing, controller.ForwardDetectionDistance, blockerDistance);
            targetSpeed *= Mathf.Clamp01(speedFactor);
            currentState = ConveyorItemState.SlowingDown;
        }
        else
        {
            currentState = targetSpeed > 0.001f ? ConveyorItemState.Moving : ConveyorItemState.WaitingForItem;
        }

        currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, controller.ItemSpeedAdjustment * deltaTime);
        float maximumProgress = IsAssignedToCollectionQueue ? controller.GetCollectionSlotPathDistance(this) : path.TotalLength;
        progressDistance = Mathf.Min(progressDistance + currentMoveSpeed * deltaTime, maximumProgress);
        lateralBlendOffset = Mathf.MoveTowards(lateralBlendOffset, lateralOffset, controller.LateralTransitionSpeed * deltaTime);

        if (IsAssignedToCollectionQueue && progressDistance >= maximumProgress - path.WaypointArrivalDistance)
        {
            progressDistance = maximumProgress;
            currentMoveSpeed = 0f;
            EnterCollectionQueue();
            controller.NotifyItemReachedCollectionPoint(this);
        }
        else if (progressDistance >= path.TotalLength - path.WaypointArrivalDistance)
        {
            progressDistance = path.TotalLength;
            currentMoveSpeed = 0f;
            if (controller.TryQueueItemForCollection(this))
            {
                waitingForCollectionSpace = false;
                EnterCollectionQueue();
                controller.NotifyItemReachedCollectionPoint(this);
            }
            else
            {
                progressDistance = Mathf.Max(0f, path.TotalLength - controller.CollectionQueueApproachHoldDistance);
                waitingForCollectionSpace = true;
                currentState = ConveyorItemState.WaitingForItem;
            }
        }

        SnapToPath();
    }

    public void Reserve()
    {
        reservedForCollection = true;
    }

    public void ReleaseReservation()
    {
        reservedForCollection = false;
    }

    public bool TryReserveForRoboticArm()
    {
        if (reservedForCollection || currentState == ConveyorItemState.BeingCollected || currentState == ConveyorItemState.Removed)
        {
            return false;
        }

        Reserve();
        return true;
    }

    public void MarkBeingCollected()
    {
        currentState = ConveyorItemState.BeingCollected;
        reservedForCollection = true;
    }

    public void HoldForRoboticPickup(Transform holdPoint, bool snapToHoldPoint)
    {
        MarkBeingCollected();
        waitingForCollectionSpace = false;
        currentMoveSpeed = 0f;

        EnsurePhysicsSetup();
        if (itemRigidbody != null)
        {
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }

        if (!snapToHoldPoint || holdPoint == null)
        {
            return;
        }

        if (itemRigidbody != null)
        {
            itemRigidbody.MovePosition(holdPoint.position);
            itemRigidbody.MoveRotation(holdPoint.rotation);
        }
        else
        {
            transform.SetPositionAndRotation(holdPoint.position, holdPoint.rotation);
        }
    }

    public void BeginRoboticCarry(Transform socket, Vector3 localPosition, Vector3 localEulerAngles, bool snapToSocket)
    {
        if (socket == null)
        {
            return;
        }

        if (controller != null)
        {
            controller.UnregisterItem(this);
            controller = null;
        }

        MarkBeingCollected();
        waitingForCollectionSpace = false;
        collectionQueueIndex = -1;
        collectionQueueSlotIndex = -1;
        isInitialized = false;
        currentMoveSpeed = 0f;
        carriedSocket = socket;
        carriedLocalPosition = localPosition;
        carriedLocalRotation = Quaternion.Euler(localEulerAngles);
        lockToCarriedSocket = snapToSocket;

        EnsurePhysicsSetup();
        if (itemRigidbody != null)
        {
            wasRigidbodyKinematicBeforeCarry = itemRigidbody.isKinematic;
            wasRigidbodyUsingGravityBeforeCarry = itemRigidbody.useGravity;
            wasRigidbodyDetectingCollisionsBeforeCarry = itemRigidbody.detectCollisions;
            itemRigidbody.isKinematic = true;
            itemRigidbody.useGravity = false;
            itemRigidbody.detectCollisions = false;
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }

        carriedColliders = GetComponentsInChildren<Collider>();
        carriedColliderTriggerStates = new bool[carriedColliders.Length];
        for (int i = 0; i < carriedColliders.Length; i++)
        {
            if (carriedColliders[i] != null)
            {
                carriedColliderTriggerStates[i] = carriedColliders[i].isTrigger;
                carriedColliders[i].isTrigger = true;
            }
        }

        transform.SetParent(socket, !snapToSocket);
        if (snapToSocket)
        {
            transform.localPosition = carriedLocalPosition;
            transform.localRotation = carriedLocalRotation;
        }
    }

    public void MoveDuringSmoothRoboticCarry(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        if (itemRigidbody != null)
        {
            itemRigidbody.position = position;
            itemRigidbody.rotation = rotation;
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void FinishSmoothRoboticCarry()
    {
        if (carriedSocket == null)
        {
            return;
        }

        transform.SetParent(carriedSocket, false);
        transform.localPosition = carriedLocalPosition;
        transform.localRotation = carriedLocalRotation;
        lockToCarriedSocket = true;

        if (itemRigidbody != null)
        {
            itemRigidbody.position = transform.position;
            itemRigidbody.rotation = transform.rotation;
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void PrepareSmoothRoboticDrop(Transform destinationParent)
    {
        lockToCarriedSocket = false;
        carriedSocket = null;
        transform.SetParent(destinationParent, true);

        EnsurePhysicsSetup();
        if (itemRigidbody != null)
        {
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
            itemRigidbody.position = transform.position;
            itemRigidbody.rotation = transform.rotation;
        }
    }

    public void MoveDuringSmoothRoboticDrop(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);

        if (itemRigidbody != null)
        {
            itemRigidbody.position = position;
            itemRigidbody.rotation = rotation;
            itemRigidbody.velocity = Vector3.zero;
            itemRigidbody.angularVelocity = Vector3.zero;
        }
    }

    public void CompleteRoboticDrop(ConveyorController destinationController, Transform dropPoint, bool keepDropRotation)
    {
        lockToCarriedSocket = false;
        carriedSocket = null;
        Transform destinationParent = destinationController != null ? destinationController.transform : null;
        transform.SetParent(destinationParent, true);
        Vector3 dropPosition = dropPoint != null ? dropPoint.position : transform.position;
        Quaternion dropRotation = dropPoint != null && keepDropRotation ? dropPoint.rotation : transform.rotation;

        if (dropPoint != null)
        {
            transform.SetPositionAndRotation(dropPosition, dropRotation);
        }

        if (destinationController != null && destinationController.ConveyorPath != null && destinationController.ConveyorPath.IsValid())
        {
            float dropDistance = destinationController.ConveyorPath.GetClosestDistance(transform.position);
            if (controller != destinationController)
            {
                destinationController.RegisterItem(this);
            }

            Initialize(destinationController, destinationController.ConveyorPath, productId, dropDistance, 0f);
            transform.SetPositionAndRotation(dropPosition, dropRotation);
            if (itemRigidbody != null)
            {
                itemRigidbody.position = dropPosition;
                itemRigidbody.rotation = dropRotation;
            }
        }
        else
        {
            controller = null;
            path = null;
            isInitialized = false;
            currentState = ConveyorItemState.Moving;
            reservedForCollection = false;
            collectionQueueIndex = -1;
            collectionQueueSlotIndex = -1;
            currentMoveSpeed = 0f;
            transform.SetPositionAndRotation(dropPosition, dropRotation);
            if (itemRigidbody != null)
            {
                itemRigidbody.position = dropPosition;
                itemRigidbody.rotation = dropRotation;
            }
        }

        if (itemRigidbody != null)
        {
            itemRigidbody.isKinematic = wasRigidbodyKinematicBeforeCarry;
            itemRigidbody.useGravity = wasRigidbodyUsingGravityBeforeCarry;
            itemRigidbody.detectCollisions = wasRigidbodyDetectingCollisionsBeforeCarry;
        }

        if (carriedColliders != null && carriedColliderTriggerStates != null)
        {
            for (int i = 0; i < carriedColliders.Length && i < carriedColliderTriggerStates.Length; i++)
            {
                if (carriedColliders[i] != null)
                {
                    carriedColliders[i].isTrigger = carriedColliderTriggerStates[i];
                }
            }
        }

        carriedColliders = null;
        carriedColliderTriggerStates = null;
        carriedLocalPosition = Vector3.zero;
        carriedLocalRotation = Quaternion.identity;

        ReleaseReservation();
    }

    public void MarkRemoved()
    {
        currentState = ConveyorItemState.Removed;
    }

    public void AssignCollectionQueue(int queueIndex, int slotIndex)
    {
        collectionQueueIndex = queueIndex;
        collectionQueueSlotIndex = slotIndex;
        waitingForCollectionSpace = false;
    }

    public void EnterCollectionQueue()
    {
        currentState = ConveyorItemState.QueuedForCollection;
        waitingForCollectionSpace = false;
    }

    public void AssignLegacyCollectionSlot()
    {
        collectionQueueIndex = -1;
        collectionQueueSlotIndex = 0;
        currentState = ConveyorItemState.WaitingForMachine;
        waitingForCollectionSpace = false;
    }

    private void EnsurePhysicsSetup()
    {
        itemRigidbody = GetComponent<Rigidbody>();
        if (itemRigidbody == null)
        {
            itemRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        itemRigidbody.isKinematic = true;
        itemRigidbody.useGravity = false;
        itemRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        itemRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        Collider[] itemColliders = GetComponentsInChildren<Collider>();
        if (itemColliders == null || itemColliders.Length == 0)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            FitBoxColliderToRenderers(boxCollider);
            itemColliders = new Collider[] { boxCollider };
        }

        for (int i = 0; i < itemColliders.Length; i++)
        {
            if (itemColliders[i] != null)
            {
                itemColliders[i].isTrigger = false;
            }
        }
    }

    private void FitBoxColliderToRenderers(BoxCollider boxCollider)
    {
        if (boxCollider == null)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
        {
            boxCollider.center = Vector3.zero;
            boxCollider.size = Vector3.one * 0.5f;
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

        Vector3 localCenter = transform.InverseTransformPoint(worldBounds.center);
        Vector3 localSize = new Vector3(
            SafeDivide(worldBounds.size.x, Mathf.Abs(transform.lossyScale.x)),
            SafeDivide(worldBounds.size.y, Mathf.Abs(transform.lossyScale.y)),
            SafeDivide(worldBounds.size.z, Mathf.Abs(transform.lossyScale.z)));

        boxCollider.center = localCenter;
        boxCollider.size = localSize;
    }

    private float SafeDivide(float value, float divisor)
    {
        return divisor > 0.0001f ? value / divisor : value;
    }

    private void SnapToPath()
    {
        ConveyorPathSample sample = path.GetSample(progressDistance);
        Vector3 targetPosition = sample.Position + sample.Lateral * lateralBlendOffset + Vector3.up * verticalOffset;
        Quaternion targetRotation = transform.rotation;

        if (path.RotateItemsAlongPath && sample.Direction.sqrMagnitude > 0.0001f)
        {
            targetRotation = Quaternion.LookRotation(sample.Direction, Vector3.up);
        }

        if (itemRigidbody != null)
        {
            itemRigidbody.MovePosition(targetPosition);
            itemRigidbody.MoveRotation(Quaternion.Slerp(itemRigidbody.rotation, targetRotation, path.ItemRotationSpeed * Time.deltaTime));
        }
        else
        {
            transform.SetPositionAndRotation(targetPosition, Quaternion.Slerp(transform.rotation, targetRotation, path.ItemRotationSpeed * Time.deltaTime));
        }
    }
}
