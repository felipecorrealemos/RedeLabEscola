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
    [SerializeField] private float stepOffset = 0.45f;
    [SerializeField, Range(0f, 89f)] private float slopeLimit = 50f;
    [SerializeField] private float skinWidth = 0.03f;
    [SerializeField] private float groundedStickForce = -2f;
    [SerializeField] private float gravity = -20f;

    [Header("Physics Push")]
    [SerializeField] private bool pushRigidbodies = true;
    [SerializeField] private float pushForce = 0.12f;
    [SerializeField] private float maxPushSpeed = 0.35f;
    [SerializeField] private float pushableMassLimit = 25f;

    [Header("Interaction")]
    [SerializeField] private float interactionRadius = 1.6f;
    [SerializeField] private float documentFallbackInteractionRadius = 1.6f;
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
    [SerializeField] private string drivingForkliftParameter = "IsDrivingForklift";
    [SerializeField] private string drivingForkliftStateName = "dirigindo";

    private MovableDevice carriedDevice;
    private PrintedDocumentInteractable carriedDocument;
    private MovableDevice highlightedDevice;
    private PrintedDocumentInteractable highlightedDocument;
    private ProfessorDocumentReceiver highlightedProfessor;
    private RouterInteractable highlightedRouter;
    private ComputerInteractable highlightedComputer;
    private KeyboardTerminalInteractable highlightedComputerTerminal;
    private RouterInteractable openRouter;
    private ComputerInteractable openComputer;
    // Desks contain many small colliders (monitor, keyboard, chair, props, etc.).
    // A 16-entry non-alloc buffer can fill before Unity returns the terminal
    // trigger, making an otherwise valid interaction disappear unpredictably.
    private readonly Collider[] interactionHits = new Collider[64];
    private CharacterController characterController;
    private float verticalVelocity;
    private bool movementLocked;
    private bool externalMovementLocked;

    private enum PromptTargetType
    {
        None,
        Router,
        Computer,
        ComputerTerminal,
        Document,
        Device
    }

    private struct PromptTarget
    {
        public PromptTargetType Type;
        public float Distance;
        public RouterInteractable Router;
        public ComputerInteractable Computer;
        public KeyboardTerminalInteractable ComputerTerminal;
        public PrintedDocumentInteractable Document;
        public MovableDevice Device;
    }

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        characterController = GetComponent<CharacterController>();
    }

    private void Awake()
    {
        interactionRadius = Mathf.Max(interactionRadius, 1.6f);
        documentFallbackInteractionRadius = Mathf.Max(documentFallbackInteractionRadius, interactionRadius);
        EnsureCharacterController();

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
        if (movementLocked || externalMovementLocked)
        {
            UpdateAnimator(Vector3.zero, false);
            if (movementLocked)
            {
                UpdateLockedInteractionInput();
            }

            return;
        }

        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);

        bool isRunning = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float targetSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 movement = input * (targetSpeed * Time.deltaTime);

        MoveWithCharacterController(movement);
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

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            HandleComputerUseInput();
        }

    }

    private void HandleCarryInput()
    {
        if (carriedDevice != null)
        {
            TryDropCarriedDevice();
            return;
        }

        PromptTarget target = FindNearestPromptTarget();
        if (target.Type == PromptTargetType.Document && target.Document != null)
        {
            PickUpDocument(target.Document);
            return;
        }

        MovableDevice device = target.Device;
        if (device == null && target.Type == PromptTargetType.Router && target.Router != null && target.Router.AllowMovement)
        {
            device = GetMovableDeviceForRouter(target.Router);
        }
        else if (device == null && target.Type == PromptTargetType.Computer && target.Computer != null)
        {
            device = GetMovableDeviceForComputer(target.Computer);
        }
        else if (device == null && target.Type == PromptTargetType.ComputerTerminal && target.ComputerTerminal != null)
        {
            device = GetMovableDeviceForComputer(target.ComputerTerminal.Computer);
        }

        if (device == null)
        {
            device = FindNearestMovableDevice(out _);
        }

        if (CanHighlightMovableDevice(device))
        {
            PickUp(device);
        }
    }

    private void HandleInteractionInput()
    {
        if (carriedDocument != null)
        {
            ProfessorDocumentReceiver professor = FindNearestProfessorDocumentReceiver(out _);
            if (professor != null)
            {
                professor.Receive(carriedDocument);
                carriedDocument = null;
                SetAnimatorBool(carryingParameter, false);
                SetHighlightedProfessor(null);
                return;
            }
        }

        RouterInteractable router = FindNearestRouter(out float routerDistance);
        PressButtonInteractable button = FindNearestButton(out float buttonDistance);
        ComputerInteractable computer = FindNearestComputer(out float computerDistance);

        if (router != null && routerDistance <= buttonDistance && routerDistance <= computerDistance)
        {
            OpenRouter(router);
            return;
        }

        if (computer != null && computer.CanConfigureNetwork && computerDistance <= buttonDistance)
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

    private void HandleComputerUseInput()
    {
        KeyboardTerminalInteractable terminal = FindNearestComputerTerminal(out _);
        if (terminal != null)
        {
            OpenComputerTerminal(terminal);
            return;
        }

        ComputerInteractable computer = FindNearestComputer(out _);
        if (computer != null && computer.CanUseTerminal)
        {
            OpenComputerTerminal(computer);
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

    private void PickUpDocument(PrintedDocumentInteractable document)
    {
        EnsureCarryAnchor();

        if (carryAnchor == null || document == null)
        {
            return;
        }

        SetHighlightedDocument(null);
        carriedDocument = document;
        carriedDocument.PickUp(carryAnchor);
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
            SetHighlightedDocument(null);
            SetHighlightedProfessor(null);
            SetHighlightedRouter(null);
            SetHighlightedComputer(null);
            SetHighlightedComputerTerminal(null);
            return;
        }

        if (carriedDocument != null)
        {
            SetHighlightedDevice(null);
            SetHighlightedDocument(null);
            SetHighlightedRouter(null);
            SetHighlightedComputer(null);
            SetHighlightedComputerTerminal(null);
            SetHighlightedProfessor(FindNearestProfessorDocumentReceiver(out _));
            return;
        }

        PromptTarget target = FindNearestPromptTarget();

        SetHighlightedProfessor(null);
        SetHighlightedRouter(target.Type == PromptTargetType.Router ? target.Router : null);
        SetHighlightedComputer(target.Type == PromptTargetType.Computer ? target.Computer : null);
        SetHighlightedComputerTerminal(target.Type == PromptTargetType.ComputerTerminal ? target.ComputerTerminal : null);
        SetHighlightedDocument(target.Type == PromptTargetType.Document ? target.Document : null);
        SetHighlightedDevice(target.Device);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        EnsureCameraFollow();
        cameraFollow?.SetZoomLocked(locked || externalMovementLocked);

        if (!movementLocked)
        {
            openRouter = null;
            openComputer = null;
            UpdateInteractionHighlight();
        }
    }

    public void SetExternalMovementLocked(bool locked)
    {
        externalMovementLocked = locked;
        EnsureCameraFollow();
        cameraFollow?.SetZoomLocked(locked || movementLocked);

        if (externalMovementLocked)
        {
            SetHighlightedDevice(null);
            SetHighlightedDocument(null);
            SetHighlightedProfessor(null);
            SetHighlightedRouter(null);
            SetHighlightedComputer(null);
            SetHighlightedComputerTerminal(null);
        }
        else if (!movementLocked)
        {
            UpdateInteractionHighlight();
        }
    }

    public void SetForkliftDrivingAnimation(bool driving)
    {
        if (animator == null)
        {
            return;
        }

        SetAnimatorBool(drivingForkliftParameter, driving);

        if (driving && !string.IsNullOrWhiteSpace(drivingForkliftStateName))
        {
            int stateHash = Animator.StringToHash(drivingForkliftStateName);
            if (animator.HasState(0, stateHash))
            {
                animator.CrossFade(stateHash, 0.08f, 0, 0f);
            }
        }
    }

    public void SetCharacterAnimator(Animator newAnimator)
    {
        animator = newAnimator;

        if (animator != null && disableRootMotion)
        {
            animator.applyRootMotion = false;
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

    private void SetHighlightedDocument(PrintedDocumentInteractable document)
    {
        if (highlightedDocument == document)
        {
            return;
        }

        if (highlightedDocument != null)
        {
            highlightedDocument.SetPromptVisible(false);
        }

        highlightedDocument = document;

        if (highlightedDocument != null)
        {
            highlightedDocument.SetPromptVisible(true);
        }
    }

    private void SetHighlightedProfessor(ProfessorDocumentReceiver professor)
    {
        if (highlightedProfessor == professor)
        {
            return;
        }

        if (highlightedProfessor != null)
        {
            highlightedProfessor.SetPromptVisible(false);
        }

        highlightedProfessor = professor;

        if (highlightedProfessor != null)
        {
            highlightedProfessor.SetPromptVisible(carriedDocument != null);
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

    private bool IsDeviceForRouter(MovableDevice device, RouterInteractable router)
    {
        if (device == null || router == null)
        {
            return false;
        }

        return device.GetComponent<RouterInteractable>() == router
            || router.GetComponent<MovableDevice>() == device
            || device.transform == router.transform
            || router.transform.IsChildOf(device.transform)
            || device.transform.IsChildOf(router.transform);
    }

    private bool IsDeviceForComputer(MovableDevice device, ComputerInteractable computer)
    {
        if (device == null || computer == null)
        {
            return false;
        }

        return device.GetComponent<ComputerInteractable>() == computer
            || computer.GetComponent<MovableDevice>() == device
            || device.transform == computer.transform
            || computer.transform.IsChildOf(device.transform)
            || device.transform.IsChildOf(computer.transform);
    }

    private RouterInteractable GetRouterForDevice(MovableDevice device)
    {
        if (device == null)
        {
            return null;
        }

        RouterInteractable router = device.GetComponent<RouterInteractable>();
        if (router != null)
        {
            return router;
        }

        router = device.GetComponentInParent<RouterInteractable>();
        return router != null ? router : device.GetComponentInChildren<RouterInteractable>();
    }

    private ComputerInteractable GetComputerForDevice(MovableDevice device)
    {
        if (device == null)
        {
            return null;
        }

        ComputerInteractable computer = device.GetComponent<ComputerInteractable>();
        if (computer != null)
        {
            return computer;
        }

        computer = device.GetComponentInParent<ComputerInteractable>();
        return computer != null ? computer : device.GetComponentInChildren<ComputerInteractable>();
    }

    private PromptTarget FindNearestPromptTarget()
    {
        PromptTarget target = new PromptTarget
        {
            Type = PromptTargetType.None,
            Distance = float.MaxValue
        };

        GetInteractionCapsulePoints(out Vector3 bottom, out Vector3 top);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, interactionRadius, interactionHits, interactionMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = interactionHits[i];
            if (hit == null || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            // Collider.ClosestPoint logs a warning every frame for non-convex MeshColliders.
            // Bounds.ClosestPoint is supported by every collider type and is precise enough
            // for ranking nearby interaction prompts.
            Vector3 nearestPoint = hit.bounds.ClosestPoint(transform.position);
            float distance = Vector3.SqrMagnitude(nearestPoint - transform.position);

            KeyboardTerminalInteractable terminal = hit.GetComponentInParent<KeyboardTerminalInteractable>();
            if (terminal != null && terminal.CanUse && terminal.ContainsCollider(hit) && terminal.IsPlayerNear(transform.position))
            {
                TrySelectPromptTarget(ref target, PromptTargetType.ComputerTerminal, distance, null, null, terminal, null, null);
                continue;
            }

            PrintedDocumentInteractable document = hit.GetComponentInParent<PrintedDocumentInteractable>();
            if (document != null && document.CanPickUp)
            {
                TrySelectPromptTarget(ref target, PromptTargetType.Document, distance, null, null, null, document, null);
                continue;
            }

            RouterInteractable router = hit.GetComponentInParent<RouterInteractable>();
            if (router != null)
            {
                MovableDevice routerDevice = router.AllowMovement ? GetMovableDeviceForRouter(router) : null;
                if (router.AllowConfigurationAccess)
                {
                    TrySelectPromptTarget(ref target, PromptTargetType.Router, distance, router, null, null, null, routerDevice);
                }
                else if (CanHighlightMovableDevice(routerDevice))
                {
                    TrySelectPromptTarget(ref target, PromptTargetType.Device, distance, null, null, null, null, routerDevice);
                }
                continue;
            }

            ComputerInteractable computer = hit.GetComponentInParent<ComputerInteractable>();
            if (computer != null && computer.CanShowPrompt && !computer.IsTerminalCollider(hit))
            {
                NetworkDoorDevice doorDevice = computer.GetComponent<NetworkDoorDevice>();
                if (doorDevice != null && !doorDevice.CanPlayerInteract(transform))
                {
                    continue;
                }

                TrySelectPromptTarget(ref target, PromptTargetType.Computer, distance, null, computer, null, null, GetMovableDeviceForComputer(computer));
                continue;
            }

            MovableDevice device = hit.GetComponentInParent<MovableDevice>();
            if (device == null || device.IsCarried)
            {
                continue;
            }

            RouterInteractable deviceRouter = GetRouterForDevice(device);
            if (deviceRouter != null)
            {
                MovableDevice routerDevice = deviceRouter.AllowMovement ? device : null;
                if (deviceRouter.AllowConfigurationAccess)
                {
                    TrySelectPromptTarget(ref target, PromptTargetType.Router, distance, deviceRouter, null, null, null, routerDevice);
                }
                else if (CanHighlightMovableDevice(routerDevice))
                {
                    TrySelectPromptTarget(ref target, PromptTargetType.Device, distance, null, null, null, null, routerDevice);
                }
                continue;
            }

            ComputerInteractable deviceComputer = GetComputerForDevice(device);
            if (deviceComputer != null && deviceComputer.CanShowPrompt)
            {
                TrySelectPromptTarget(ref target, PromptTargetType.Computer, distance, null, deviceComputer, null, null, device);
                continue;
            }

            if (CanHighlightMovableDevice(device))
            {
                TrySelectPromptTarget(ref target, PromptTargetType.Device, distance, null, null, null, null, device);
            }
        }

        if (target.Type != PromptTargetType.Document)
        {
            PrintedDocumentInteractable fallbackDocument = FindNearestPrintedDocumentByDistance(out float documentDistance);
            if (fallbackDocument != null)
            {
                target.Type = PromptTargetType.Document;
                target.Distance = documentDistance;
                target.Router = null;
                target.Computer = null;
                target.ComputerTerminal = null;
                target.Document = fallbackDocument;
                target.Device = null;
            }
        }

        return target;
    }

    private void TrySelectPromptTarget(
        ref PromptTarget target,
        PromptTargetType type,
        float distance,
        RouterInteractable router,
        ComputerInteractable computer,
        KeyboardTerminalInteractable computerTerminal,
        PrintedDocumentInteractable document,
        MovableDevice device)
    {
        if (distance >= target.Distance)
        {
            return;
        }

        target.Type = type;
        target.Distance = distance;
        target.Router = router;
        target.Computer = computer;
        target.ComputerTerminal = computerTerminal;
        target.Document = document;
        target.Device = device;
    }

    private bool CanHighlightMovableDevice(MovableDevice device)
    {
        if (device == null || device.IsCarried)
        {
            return false;
        }

        RouterInteractable router = device.GetComponent<RouterInteractable>();
        if (router != null && !router.AllowMovement)
        {
            return false;
        }

        ComputerInteractable computer = device.GetComponent<ComputerInteractable>();
        return computer == null || computer.CanBePickedUp;
    }

    private MovableDevice GetMovableDeviceForRouter(RouterInteractable router)
    {
        if (router == null)
        {
            return null;
        }

        MovableDevice device = router.GetComponent<MovableDevice>();
        if (device != null)
        {
            return device;
        }

        device = router.GetComponentInParent<MovableDevice>();
        return device != null ? device : router.GetComponentInChildren<MovableDevice>();
    }

    private MovableDevice GetMovableDeviceForComputer(ComputerInteractable computer)
    {
        if (computer == null)
        {
            return null;
        }

        MovableDevice device = computer.GetComponent<MovableDevice>();
        if (device != null)
        {
            return device;
        }

        device = computer.GetComponentInParent<MovableDevice>();
        return device != null ? device : computer.GetComponentInChildren<MovableDevice>();
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

    private void OpenComputerTerminal(ComputerInteractable computer)
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
        openComputer.OpenTerminal(this);
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
            if (Input.GetKeyDown(KeyCode.Escape)) EscapeInputGuard.Consume();
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

            RouterInteractable router = device.GetComponent<RouterInteractable>();
            if (router != null && !router.AllowMovement)
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

    private PrintedDocumentInteractable FindNearestPrintedDocument(out float nearestDistance)
    {
        PrintedDocumentInteractable nearestDocument = null;
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

            PrintedDocumentInteractable document = hit.GetComponentInParent<PrintedDocumentInteractable>();
            if (document == null || !document.CanPickUp)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(document.transform.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestDocument = document;
            }
        }

        return nearestDocument;
    }

    private PrintedDocumentInteractable FindNearestPrintedDocumentByDistance(out float nearestDistance)
    {
        PrintedDocumentInteractable nearestDocument = null;
        nearestDistance = float.MaxValue;
        float maxDistance = Mathf.Max(documentFallbackInteractionRadius, interactionRadius);
        float maxDistanceSqr = maxDistance * maxDistance;
        PrintedDocumentInteractable[] documents = FindObjectsOfType<PrintedDocumentInteractable>(true);

        for (int i = 0; i < documents.Length; i++)
        {
            PrintedDocumentInteractable document = documents[i];
            if (document == null || !document.CanPickUp)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(document.transform.position - transform.position);
            if (sqrDistance <= maxDistanceSqr && sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestDocument = document;
            }
        }

        return nearestDocument;
    }

    private ProfessorDocumentReceiver FindNearestProfessorDocumentReceiver(out float nearestDistance)
    {
        ProfessorDocumentReceiver nearestProfessor = null;
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

            ProfessorDocumentReceiver professor = hit.GetComponentInParent<ProfessorDocumentReceiver>();

            if (professor == null)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(professor.transform.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestProfessor = professor;
            }
        }

        return nearestProfessor;
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
            if (router == null || !router.AllowConfigurationAccess)
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

        if (nearestZone != null)
        {
            return nearestZone;
        }

        DeviceDropZone[] dropZones = FindObjectsOfType<DeviceDropZone>();
        for (int i = 0; i < dropZones.Length; i++)
        {
            DeviceDropZone dropZone = dropZones[i];
            if (dropZone == null || !dropZone.CanReceive(carriedDevice) || !dropZone.IsDeviceInPlacementRange(carriedDevice))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(dropZone.PlacePosition - carriedDevice.transform.position);
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

    private void MoveWithCharacterController(Vector3 horizontalMovement)
    {
        EnsureCharacterController();
        if (characterController == null)
        {
            transform.position += horizontalMovement;
            return;
        }

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        verticalVelocity += gravity * Time.deltaTime;
        Vector3 movement = horizontalMovement + Vector3.up * (verticalVelocity * Time.deltaTime);
        CollisionFlags flags = characterController.Move(movement);

        if ((flags & CollisionFlags.Below) != 0 && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }
    }

    private void EnsureCharacterController()
    {
        if (characterController == null)
        {
            characterController = GetComponent<CharacterController>();
        }

        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
        }

        characterController.radius = collisionRadius;
        characterController.height = collisionHeight;
        characterController.center = Vector3.up * (collisionHeight * 0.5f);
        characterController.stepOffset = Mathf.Min(stepOffset, collisionHeight);
        characterController.slopeLimit = slopeLimit;
        characterController.skinWidth = skinWidth;
        characterController.minMoveDistance = 0f;

        CapsuleCollider legacyCapsule = GetComponent<CapsuleCollider>();
        if (legacyCapsule != null)
        {
            legacyCapsule.enabled = false;
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        TryPushRigidbody(hit);
    }

    private bool TryPushRigidbody(ControllerColliderHit hit)
    {
        if (!pushRigidbodies || hit == null || hit.rigidbody == null)
        {
            return false;
        }

        Rigidbody hitBody = hit.rigidbody;
        if (hitBody.isKinematic || hitBody.mass > pushableMassLimit)
        {
            return false;
        }

        Vector3 pushDirection = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
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
        if (animator != null && HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (animator != null && HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterName);
        }
    }

    private bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == parameterType && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }
}
