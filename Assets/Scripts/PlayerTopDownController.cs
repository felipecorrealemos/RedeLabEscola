using UnityEngine;

public class PlayerTopDownController : MonoBehaviour
{
    private const string PreferredCarryAnchorName = "Anchor Carry";
    private const string CompactCarryAnchorName = "AnchorCarry";
    private const string LegacyCarryAnchorName = "CarryAnchor";

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.2f;
    [SerializeField] private float runSpeed = 4.0f;
    [SerializeField] private float rotationSpeed = 12f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.28f;
    [SerializeField] private float collisionHeight = 1.45f;
    [SerializeField] private LayerMask collisionMask = ~0;

    [Header("Physics Push")]
    [SerializeField] private bool pushRigidbodies = true;
    [SerializeField] private float pushForce = 0.12f;
    [SerializeField] private float maxPushSpeed = 0.35f;
    [SerializeField] private float pushableMassLimit = 25f;

    [Header("Interaction")]
    [SerializeField] private float interactionRadius = 1.2f;
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private Vector3 carryAnchorLocalPosition = new Vector3(0f, 1.05f, 0.45f);
    [SerializeField] private LayerMask interactionMask = ~0;
    [SerializeField] private DeadZoneCameraFollow cameraFollow;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private bool disableRootMotion = true;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string carryingParameter = "IsCarrying";
    [SerializeField] private string pushButtonParameter = "PushButton";

    private MovableDevice carriedDevice;
    private MovableDevice highlightedDevice;
    private RouterInteractable highlightedRouter;
    private ComputerInteractable highlightedComputer;
    private KeyboardTerminalInteractable highlightedComputerTerminal;
    private RouterInteractable openRouter;
    private ComputerInteractable openComputer;
    private readonly Collider[] interactionHits = new Collider[16];
    private bool movementLocked;

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && disableRootMotion)
        {
            animator.applyRootMotion = false;
        }

        EnsureCarryAnchor();
        EnsureCameraFollow();
    }

    private void Update()
    {
        if (movementLocked)
        {
            UpdateAnimator(Vector3.zero, false);
            UpdateLockedInteractionInput();
            return;
        }

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);

        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 movement = input * (targetSpeed * Time.deltaTime);

        MoveWithCollision(movement);
        RotateTowardsMovement(input);
        UpdateAnimator(input, isRunning);
        UpdateInteractionHighlight();

        if (Input.GetKeyDown(KeyCode.E))
        {
            HandleCarryInput();
        }

        if (Input.GetKeyDown(KeyCode.F))
        {
            HandleInteractionInput();
        }

    }

    private void HandleCarryInput()
    {
        if (carriedDevice != null)
        {
            TryDropCarriedDevice();
            return;
        }

        MovableDevice device = FindNearestMovableDevice(out _);
        if (device != null)
        {
            PickUp(device);
        }
    }

    private void HandleInteractionInput()
    {
        RouterInteractable router = FindNearestRouter(out float routerDistance);
        PressButtonInteractable button = FindNearestButton(out float buttonDistance);
        ComputerInteractable computer = FindNearestComputer(out float computerDistance);
        KeyboardTerminalInteractable computerTerminal = FindNearestComputerTerminal(out float terminalDistance);

        if (computerTerminal != null && terminalDistance <= routerDistance && terminalDistance <= buttonDistance && terminalDistance <= computerDistance)
        {
            OpenComputerTerminal(computerTerminal);
            return;
        }

        if (router != null && routerDistance <= buttonDistance && routerDistance <= computerDistance)
        {
            OpenRouter(router);
            return;
        }

        if (computer != null && computer.CanInteract && computerDistance <= buttonDistance)
        {
            OpenComputer(computer);
            return;
        }

        if (button != null)
        {
            button.Press();
            SetAnimatorTrigger(pushButtonParameter);
        }
    }

    private void PickUp(MovableDevice device)
    {
        EnsureCarryAnchor();

        if (carryAnchor == null)
        {
            return;
        }

        SetHighlightedDevice(null);
        carriedDevice = device;
        carriedDevice.PickUp(carryAnchor);
        SetAnimatorBool(carryingParameter, true);
    }

    private void TryDropCarriedDevice()
    {
        DeviceDropZone dropZone = FindNearestDropZone();
        if (dropZone == null || !dropZone.CanReceive(carriedDevice))
        {
            return;
        }

        carriedDevice.DropAt(dropZone);
        carriedDevice = null;
        SetAnimatorBool(carryingParameter, false);
    }

    private void UpdateInteractionHighlight()
    {
        if (carriedDevice != null)
        {
            SetHighlightedDevice(null);
            SetHighlightedRouter(null);
            SetHighlightedComputer(null);
            SetHighlightedComputerTerminal(null);
            return;
        }

        RouterInteractable nearestRouter = FindNearestRouter(out float routerDistance);
        ComputerInteractable nearestComputer = FindNearestComputer(out float computerDistance);
        KeyboardTerminalInteractable nearestComputerTerminal = FindNearestComputerTerminal(out float terminalDistance);
        MovableDevice nearestDevice = FindNearestMovableDevice(out _);
        SetHighlightedRouter(nearestRouter);
        SetHighlightedComputer(nearestComputer);
        SetHighlightedComputerTerminal(nearestComputerTerminal);
        if (nearestComputerTerminal != null && terminalDistance <= interactionRadius * interactionRadius)
        {
            SetHighlightedDevice(null);
            SetHighlightedComputer(null);
            return;
        }

        if (nearestRouter != null && routerDistance <= interactionRadius * interactionRadius)
        {
            SetHighlightedDevice(null);
            return;
        }

        if (nearestComputer != null && computerDistance <= interactionRadius * interactionRadius)
        {
            SetHighlightedDevice(nearestDevice);
            return;
        }

        SetHighlightedDevice(nearestDevice);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        EnsureCameraFollow();
        cameraFollow?.SetZoomLocked(locked);

        if (!movementLocked)
        {
            openRouter = null;
            openComputer = null;
            UpdateInteractionHighlight();
        }
    }

    private void EnsureCameraFollow()
    {
        if (cameraFollow != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            cameraFollow = mainCamera.GetComponent<DeadZoneCameraFollow>();
        }
    }

    private void SetHighlightedDevice(MovableDevice device)
    {
        if (highlightedDevice == device)
        {
            return;
        }

        if (highlightedDevice != null)
        {
            highlightedDevice.SetInteractionHighlighted(false);
        }

        highlightedDevice = device;

        if (highlightedDevice != null)
        {
            highlightedDevice.SetInteractionHighlighted(true);
        }
    }

    private void SetHighlightedRouter(RouterInteractable router)
    {
        if (highlightedRouter == router)
        {
            return;
        }

        if (highlightedRouter != null)
        {
            highlightedRouter.SetPromptVisible(false);
        }

        highlightedRouter = router;

        if (highlightedRouter != null)
        {
            highlightedRouter.SetPromptVisible(true);
        }
    }

    private void SetHighlightedComputer(ComputerInteractable computer)
    {
        if (highlightedComputer == computer)
        {
            return;
        }

        if (highlightedComputer != null)
        {
            highlightedComputer.SetPromptVisible(false);
        }

        highlightedComputer = computer;

        if (highlightedComputer != null)
        {
            highlightedComputer.SetPromptVisible(true);
        }
    }

    private void SetHighlightedComputerTerminal(KeyboardTerminalInteractable computer)
    {
        if (highlightedComputerTerminal == computer)
        {
            return;
        }

        if (highlightedComputerTerminal != null)
        {
            highlightedComputerTerminal.SetPromptVisible(false);
        }

        highlightedComputerTerminal = computer;

        if (highlightedComputerTerminal != null)
        {
            highlightedComputerTerminal.SetPromptVisible(true);
        }
    }

    private void OpenRouter(RouterInteractable router)
    {
        if (router == null)
        {
            return;
        }

        SetHighlightedDevice(null);
        SetHighlightedRouter(null);
        SetHighlightedComputer(null);
        SetHighlightedComputerTerminal(null);
        openRouter = router;
        openRouter.Open(this);
    }

    private void OpenComputer(ComputerInteractable computer)
    {
        if (computer == null)
        {
            return;
        }

        SetHighlightedDevice(null);
        SetHighlightedRouter(null);
        SetHighlightedComputer(null);
        SetHighlightedComputerTerminal(null);
        openComputer = computer;
        openComputer.Open(this);
    }

    private void OpenComputerTerminal(KeyboardTerminalInteractable computer)
    {
        if (computer == null)
        {
            return;
        }

        SetHighlightedDevice(null);
        SetHighlightedRouter(null);
        SetHighlightedComputer(null);
        SetHighlightedComputerTerminal(null);
        openComputer = computer.Computer;
        computer.Open(this);
    }

    private void UpdateLockedInteractionInput()
    {
        if (openRouter == null && openComputer == null)
        {
            movementLocked = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape))
        {
            if (openRouter != null)
            {
                openRouter.Close(this);
            }

            if (openComputer != null)
            {
                openComputer.Close(this);
            }
        }
    }

    private MovableDevice FindNearestMovableDevice(out float nearestDistance)
    {
        MovableDevice nearestDevice = null;
        nearestDistance = float.MaxValue;
        GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, interactionRadius, interactionHits, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            MovableDevice device = hit.GetComponentInParent<MovableDevice>();
            if (device == null || device.IsCarried)
            {
                continue;
            }

            ComputerInteractable computer = device.GetComponent<ComputerInteractable>();
            if (computer != null && !computer.CanBePickedUp)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(device.transform.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestDevice = device;
            }
        }

        return nearestDevice;
    }

    private PressButtonInteractable FindNearestButton(out float nearestDistance)
    {
        PressButtonInteractable nearestButton = null;
        nearestDistance = float.MaxValue;
        GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, interactionRadius, interactionHits, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            PressButtonInteractable button = hit.GetComponentInParent<PressButtonInteractable>();
            if (button == null)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(button.transform.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestButton = button;
            }
        }

        return nearestButton;
    }

    private RouterInteractable FindNearestRouter(out float nearestDistance)
    {
        RouterInteractable nearestRouter = null;
        nearestDistance = float.MaxValue;
        GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, interactionRadius, interactionHits, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            RouterInteractable router = hit.GetComponentInParent<RouterInteractable>();
            if (router == null)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(router.transform.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestRouter = router;
            }
        }

        return nearestRouter;
    }

    private ComputerInteractable FindNearestComputer(out float nearestDistance)
    {
        ComputerInteractable nearestComputer = null;
        nearestDistance = float.MaxValue;
        GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, interactionRadius, interactionHits, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            ComputerInteractable computer = hit.GetComponentInParent<ComputerInteractable>();
            if (computer == null)
            {
                MovableDevice device = hit.GetComponentInParent<MovableDevice>();
                if (device != null && device.IsPlaced)
                {
                    computer = device.EnsureComputerInteractable();
                }
            }

            if (computer == null || !computer.CanShowPrompt || computer.IsTerminalCollider(hit))
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(computer.transform.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestComputer = computer;
            }
        }

        return nearestComputer;
    }

    private KeyboardTerminalInteractable FindNearestComputerTerminal(out float nearestDistance)
    {
        KeyboardTerminalInteractable nearestComputer = null;
        nearestDistance = float.MaxValue;
        GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, interactionRadius, interactionHits, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            KeyboardTerminalInteractable computer = hit.GetComponentInParent<KeyboardTerminalInteractable>();
            if (computer == null || !computer.CanUse || !computer.ContainsCollider(hit) || !computer.IsPlayerNear(transform.position))
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(computer.InteractionPosition - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestComputer = computer;
            }
        }

        return nearestComputer;
    }

    private DeviceDropZone FindNearestDropZone()
    {
        DeviceDropZone nearestZone = null;
        float nearestDistance = float.MaxValue;
        GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, interactionRadius, interactionHits, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null)
            {
                continue;
            }

            DeviceDropZone dropZone = hit.GetComponentInParent<DeviceDropZone>();
            if (dropZone == null || !dropZone.CanReceive(carriedDevice))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(dropZone.transform.position - transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestZone = dropZone;
            }
        }

        return nearestZone;
    }

    private void EnsureCarryAnchor()
    {
        if (carryAnchor != null)
        {
            return;
        }

        Transform existingAnchor = transform.Find(PreferredCarryAnchorName);
        if (existingAnchor == null)
        {
            existingAnchor = transform.Find(CompactCarryAnchorName);
        }

        if (existingAnchor == null)
        {
            existingAnchor = transform.Find(LegacyCarryAnchorName);
        }

        if (existingAnchor != null)
        {
            carryAnchor = existingAnchor;
            return;
        }

        GameObject anchorObject = new GameObject(PreferredCarryAnchorName);
        carryAnchor = anchorObject.transform;
        carryAnchor.SetParent(transform);
        carryAnchor.localPosition = carryAnchorLocalPosition;
        carryAnchor.localRotation = Quaternion.identity;
        carryAnchor.localScale = Vector3.one;
    }

    private void MoveWithCollision(Vector3 movement)
    {
        if (movement.sqrMagnitude <= 0f)
        {
            return;
        }

        if (!IsBlocked(movement))
        {
            transform.position += movement;
            return;
        }

        Vector3 horizontalMovement = new Vector3(movement.x, 0f, 0f);
        if (horizontalMovement.sqrMagnitude > 0f && !IsBlocked(horizontalMovement))
        {
            transform.position += horizontalMovement;
        }

        Vector3 verticalMovement = new Vector3(0f, 0f, movement.z);
        if (verticalMovement.sqrMagnitude > 0f && !IsBlocked(verticalMovement))
        {
            transform.position += verticalMovement;
        }
    }

    private bool IsBlocked(Vector3 movement)
    {
        Vector3 direction = movement.normalized;
        float distance = movement.magnitude + 0.02f;
        GetCapsulePoints(out Vector3 bottom, out Vector3 top);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            collisionRadius,
            direction,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.collider.bounds.max.y <= 0.15f)
            {
                continue;
            }

            if (TryPushRigidbody(hit, direction))
            {
                return true;
            }

            return true;
        }

        return false;
    }

    private bool TryPushRigidbody(RaycastHit hit, Vector3 direction)
    {
        if (!pushRigidbodies || hit.rigidbody == null)
        {
            return false;
        }

        Rigidbody hitBody = hit.rigidbody;
        if (hitBody.isKinematic || hitBody.mass > pushableMassLimit)
        {
            return false;
        }

        Vector3 pushDirection = new Vector3(direction.x, 0f, direction.z);
        if (pushDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        pushDirection.Normalize();
        hitBody.WakeUp();

        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(hitBody.velocity, Vector3.up);
        if (horizontalVelocity.magnitude < maxPushSpeed)
        {
            hitBody.AddForceAtPosition(pushDirection * pushForce, hit.point, ForceMode.Impulse);
        }

        return true;
    }

    private void GetCapsulePoints(out Vector3 bottom, out Vector3 top)
    {
        Vector3 position = transform.position;
        float halfHeight = Mathf.Max(collisionHeight * 0.5f, collisionRadius);
        bottom = position + Vector3.up * collisionRadius;
        top = position + Vector3.up * ((halfHeight * 2f) - collisionRadius);
    }

    private void GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top)
    {
        Vector3 position = transform.position;
        bottom = position + Vector3.up * collisionRadius;
        top = position + Vector3.up * Mathf.Max(collisionHeight, collisionRadius);
    }

    private void RotateTowardsMovement(Vector3 input)
    {
        if (input.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(input, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    private void UpdateAnimator(Vector3 input, bool isRunning)
    {
        if (animator == null)
        {
            return;
        }

        float normalizedSpeed = input.magnitude;
        if (isRunning && normalizedSpeed > 0f)
        {
            normalizedSpeed = 1.5f;
        }

        animator.SetFloat(speedParameter, normalizedSpeed);
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(parameterName))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (animator != null && !string.IsNullOrWhiteSpace(parameterName))
        {
            animator.SetTrigger(parameterName);
        }
    }
}
