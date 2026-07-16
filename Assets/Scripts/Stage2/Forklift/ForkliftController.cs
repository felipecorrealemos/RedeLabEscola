using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class ForkliftController : MonoBehaviour
{
    private enum DriverState
    {
        PlayerOutside,
        PlayerDriving
    }

    private enum ForkCargoState
    {
        ForkEmpty,
        PalletAvailable,
        CarryingPallet,
        InsideDropZone,
        DepositingPallet
    }

    [Header("Movement")]
    [SerializeField] private float maxForwardSpeed = 4.5f;
    [SerializeField] private float maxReverseSpeed = 3f;
    [SerializeField] private float acceleration = 5.5f;
    [SerializeField] private float deceleration = 11f;
    [SerializeField] private float brakeForce = 22f;
    [SerializeField] private float maxSteeringAngle = 75f;
    [SerializeField] private float steeringResponseSpeed = 8f;
    [SerializeField, Range(0f, 1f)] private float highSpeedSteeringReduction = 0.65f;
    [SerializeField] private float movementSkinWidth = 0.04f;
    [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 0.28f, 0.1f);
    [SerializeField] private bool requiresInstalledMotor;

    [Header("Visual")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;
    [SerializeField] private Transform steeringWheel;
    [SerializeField] private float wheelVisualRotationSpeed = 130f;
    [SerializeField] private float wheelVisualSteeringAngle = 32f;
    [SerializeField] private float steeringWheelMultiplier = 2.4f;
    [SerializeField] private Light topLampLight;
    [SerializeField] private Renderer topLampRenderer;
    [SerializeField] private Material topLampOnMaterial;
    [SerializeField] private Material topLampOffMaterial;

    [Header("Fork")]
    [SerializeField] private Transform forkLiftTransform;
    [SerializeField] private float forkLocalMinHeight = 0f;
    [SerializeField] private float forkLocalMaxHeight = 0.68f;
    [SerializeField] private float forkLiftSpeed = 0.55f;
    [SerializeField] private float forkLowerSpeed = 0.55f;

    [Header("Interaction")]
    [SerializeField] private Transform driverSeatPoint;
    [SerializeField] private Transform playerExitPoint;
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private float fallbackInteractionRadius = 1.8f;
    [SerializeField] private bool requirePlayerFacingForklift = true;
    [SerializeField, Range(-1f, 1f)] private float minimumFacingDot = 0.35f;
    [SerializeField] private LayerMask exitBlockingMask = ~0;
    [SerializeField] private float exitCheckRadius = 0.35f;
    [SerializeField] private float exitCheckHeight = 1.65f;
    [SerializeField] private int exitSearchSteps = 12;
    [SerializeField] private float exitSearchRadius = 1.6f;

    [Header("Pallet")]
    [SerializeField] private Transform forkRuntimeAttachments;
    [SerializeField] private Vector3 forkRuntimeLocalBasePosition = Vector3.zero;
    [SerializeField] private ForkliftPickupSensor forkPickupSensor;
    [SerializeField] private Transform forkCarryPoint;
    [SerializeField] private float captureDistance = 1.45f;
    [SerializeField] private float captureWidth = 1.4f;
    [SerializeField] private float angularTolerance = 45f;
    [SerializeField] private float autoSnapSpeed = 8f;
    [SerializeField] private LayerMask validPalletLayers = ~0;

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject interactionPromptObject;
    [SerializeField] private Text interactionPromptLabel;
    [SerializeField] private GameObject drivingPanelObject;
    [SerializeField] private Text drivingPanelLabel;
    [SerializeField] private string enterPromptText = "Pressione E para dirigir a empilhadeira";
    [SerializeField] private string blockedExitText = "Saida bloqueada";

    [Header("Debug")]
    [SerializeField] private DriverState driverState = DriverState.PlayerOutside;
    [SerializeField] private ForkCargoState cargoState = ForkCargoState.ForkEmpty;

    private readonly HashSet<ForkliftPallet> nearbyPallets = new HashSet<ForkliftPallet>();
    private readonly HashSet<ForkliftPalletDropZone> activeDropZones = new HashSet<ForkliftPalletDropZone>();
    private readonly Collider[] overlapBuffer = new Collider[20];
    private readonly Collider[] palletScanBuffer = new Collider[24];
    private Rigidbody forkliftRigidbody;
    private PlayerTopDownController currentPlayer;
    private Transform originalPlayerParent;
    private ForkliftPallet availablePallet;
    private ForkliftPallet carriedPallet;
    private ForkliftPalletDropZone currentDropZone;
    private float currentSpeed;
    private float currentSteeringAngle;
    private float wheelSpin;
    private bool playerInInteractionRange;
    private string temporaryPrompt;
    private float temporaryPromptTimer;
    private Vector3[] wheelBaseLocalEuler;
    private Quaternion steeringWheelBaseLocalRotation;

    public bool IsDriving => driverState == DriverState.PlayerDriving;
    public bool IsCarryingPallet => carriedPallet != null;
    public bool RequiresInstalledMotor => requiresInstalledMotor;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
        ConfigureRigidbody();
        CacheVisualRotations();
        EnsureUi();
        SetPromptVisible(false);
        SetDrivingPanelVisible(false);
        SetTopLamp(false);
    }

    private void OnValidate()
    {
        maxForwardSpeed = Mathf.Max(0f, maxForwardSpeed);
        maxReverseSpeed = Mathf.Max(0f, maxReverseSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        deceleration = Mathf.Max(0.01f, deceleration);
        brakeForce = Mathf.Max(0.01f, brakeForce);
        maxSteeringAngle = Mathf.Max(0f, maxSteeringAngle);
        steeringResponseSpeed = Mathf.Max(0.01f, steeringResponseSpeed);
        wheelVisualSteeringAngle = Mathf.Max(0f, wheelVisualSteeringAngle);
        forkLocalMaxHeight = Mathf.Max(forkLocalMinHeight, forkLocalMaxHeight);
        forkLiftSpeed = Mathf.Max(0.01f, forkLiftSpeed);
        forkLowerSpeed = Mathf.Max(0.01f, forkLowerSpeed);
        captureDistance = Mathf.Max(0.05f, captureDistance);
        captureWidth = Mathf.Max(0.05f, captureWidth);
        autoSnapSpeed = Mathf.Max(0.01f, autoSnapSpeed);
        movementSkinWidth = Mathf.Max(0f, movementSkinWidth);
    }

    private void Update()
    {
        UpdateTemporaryPrompt();
        UpdateInteractionPrompt();

        if (Input.GetKeyDown(interactionKey))
        {
            if (driverState == DriverState.PlayerDriving)
            {
                TryExit();
            }
            else if (playerInInteractionRange)
            {
                TryEnter();
            }
        }

        if (driverState != DriverState.PlayerDriving)
        {
            currentSteeringAngle = Mathf.MoveTowards(currentSteeringAngle, 0f, steeringResponseSpeed * maxSteeringAngle * Time.deltaTime);
            UpdateForkRuntimeAttachments();
            UpdateWheelVisuals(Time.deltaTime);
            return;
        }

        UpdateForkRuntimeAttachments();
        UpdateFork(Time.deltaTime);
        UpdateAvailablePallet();
        UpdateCarriedPallet(Time.deltaTime);
        UpdateDrivingPanel();
        UpdateWheelVisuals(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (driverState != DriverState.PlayerDriving)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeForce * Time.fixedDeltaTime);
            ApplyMovement(0f, Time.fixedDeltaTime);
            return;
        }

        float throttle = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
        bool braking = Input.GetKey(KeyCode.Space);
        float targetSpeed = throttle > 0f ? maxForwardSpeed : throttle < 0f ? -maxReverseSpeed : 0f;
        float speedRate = braking ? brakeForce : Mathf.Abs(targetSpeed) > Mathf.Abs(currentSpeed) && throttle != 0f ? acceleration : deceleration;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedRate * Time.fixedDeltaTime);

        float steeringInput = Input.GetKey(KeyCode.A) ? -1f : Input.GetKey(KeyCode.D) ? 1f : 0f;
        float speedFactor = Mathf.InverseLerp(0f, maxForwardSpeed, Mathf.Abs(currentSpeed));
        float steeringLimit = Mathf.Lerp(maxSteeringAngle, maxSteeringAngle * (1f - highSpeedSteeringReduction), speedFactor);
        float targetSteering = steeringInput * steeringLimit;
        currentSteeringAngle = Mathf.MoveTowards(currentSteeringAngle, targetSteering, steeringResponseSpeed * maxSteeringAngle * Time.fixedDeltaTime);

        ApplyMovement(currentSteeringAngle, Time.fixedDeltaTime);
    }

    public void NotifyPickupSensorEnter(Collider other)
    {
        ForkliftPallet pallet = ResolvePallet(other);
        if (pallet != null && IsLayerAllowed(pallet.gameObject.layer))
        {
            nearbyPallets.Add(pallet);
            UpdateAvailablePallet();
        }
    }

    public void NotifyPickupSensorExit(Collider other)
    {
        ForkliftPallet pallet = ResolvePallet(other);
        if (pallet != null)
        {
            nearbyPallets.Remove(pallet);
            if (availablePallet == pallet)
            {
                availablePallet = null;
            }

            UpdateAvailablePallet();
        }
    }

    public void NotifyDropZoneEnter(ForkliftPalletDropZone zone)
    {
        if (zone != null)
        {
            activeDropZones.Add(zone);
            UpdateCurrentDropZone();
        }
    }

    public void NotifyDropZoneExit(ForkliftPalletDropZone zone)
    {
        if (zone != null)
        {
            activeDropZones.Remove(zone);
            if (currentDropZone == zone)
            {
                currentDropZone = null;
            }

            UpdateCurrentDropZone();
        }
    }

    private void TryEnter()
    {
        if (driverState != DriverState.PlayerOutside)
        {
            return;
        }

        PlayerTopDownController player = FindPlayerInRange();
        if (player == null || driverSeatPoint == null)
        {
            return;
        }

        currentPlayer = player;
        originalPlayerParent = player.transform.parent;
        player.SetExternalMovementLocked(true);
        player.SetForkliftDrivingAnimation(true);
        player.transform.SetParent(driverSeatPoint, true);
        player.transform.SetPositionAndRotation(driverSeatPoint.position, driverSeatPoint.rotation);
        driverState = DriverState.PlayerDriving;
        playerInInteractionRange = false;
        SetPromptVisible(false);
        SetDrivingPanelVisible(true);
        SetTopLamp(true);
    }

    private void TryExit()
    {
        if (currentPlayer == null)
        {
            driverState = DriverState.PlayerOutside;
            SetDrivingPanelVisible(false);
            SetTopLamp(false);
            currentSpeed = 0f;
            return;
        }

        if (!TryFindSafeExitPosition(out Vector3 exitPosition))
        {
            ShowTemporaryPrompt(blockedExitText);
            return;
        }

        currentPlayer.transform.SetParent(originalPlayerParent, true);
        currentPlayer.transform.position = exitPosition;
        if (playerExitPoint != null)
        {
            currentPlayer.transform.rotation = playerExitPoint.rotation;
        }

        currentPlayer.SetForkliftDrivingAnimation(false);
        currentPlayer.SetExternalMovementLocked(false);
        currentPlayer = null;
        originalPlayerParent = null;
        driverState = DriverState.PlayerOutside;
        currentSpeed = 0f;
        SetDrivingPanelVisible(false);
        SetTopLamp(false);
    }

    private void ApplyMovement(float steeringAngle, float deltaTime)
    {
        if (forkliftRigidbody == null)
        {
            return;
        }

        ClearDynamicVelocity();

        Vector3 movement = transform.forward * (currentSpeed * deltaTime);
        movement = ClampMovementAgainstCollision(movement);
        forkliftRigidbody.MovePosition(forkliftRigidbody.position + movement);

        if (Mathf.Abs(currentSpeed) > 0.05f && Mathf.Abs(steeringAngle) > 0.05f)
        {
            float direction = Mathf.Sign(currentSpeed);
            float turnDegrees = steeringAngle * direction * Mathf.Clamp01(Mathf.Abs(currentSpeed) / Mathf.Max(0.1f, maxForwardSpeed)) * deltaTime;
            Quaternion turnRotation = Quaternion.Euler(0f, turnDegrees, 0f);
            forkliftRigidbody.MoveRotation(forkliftRigidbody.rotation * turnRotation);
        }

        ClearDynamicVelocity();
    }

    private void ClearDynamicVelocity()
    {
        if (forkliftRigidbody == null || forkliftRigidbody.isKinematic)
        {
            return;
        }

        forkliftRigidbody.velocity = Vector3.zero;
        forkliftRigidbody.angularVelocity = Vector3.zero;
    }

    private Vector3 ClampMovementAgainstCollision(Vector3 movement)
    {
        if (movement.sqrMagnitude <= 0.000001f || forkliftRigidbody == null)
        {
            return Vector3.zero;
        }

        float distance = movement.magnitude;
        Vector3 direction = movement / distance;
        if (forkliftRigidbody.SweepTest(direction, out RaycastHit hit, distance + movementSkinWidth, QueryTriggerInteraction.Ignore))
        {
            float allowedDistance = Mathf.Max(0f, hit.distance - movementSkinWidth);
            if (allowedDistance <= movementSkinWidth)
            {
                currentSpeed = 0f;
                return Vector3.zero;
            }

            movement = direction * allowedDistance;
            currentSpeed = Mathf.Min(Mathf.Abs(currentSpeed), allowedDistance / Mathf.Max(Time.fixedDeltaTime, 0.0001f)) * Mathf.Sign(currentSpeed);
        }

        return movement;
    }

    private void UpdateFork(float deltaTime)
    {
        if (forkLiftTransform == null)
        {
            return;
        }

        bool lifting = Input.GetKey(KeyCode.R);
        bool lowering = Input.GetKey(KeyCode.Q);
        if (!lifting && !lowering)
        {
            return;
        }

        if (lifting)
        {
            TryCaptureAvailablePallet();
        }

        float targetHeight = forkLiftTransform.localPosition.y;
        if (lifting)
        {
            targetHeight += forkLiftSpeed * deltaTime;
        }
        else if (lowering)
        {
            targetHeight -= forkLowerSpeed * deltaTime;
            ForkliftPalletDropZone dropZone = GetValidDropZoneForCarry();
            if (dropZone != null && IsForkAtDeliveryHeight(dropZone))
            {
                targetHeight = Mathf.Max(targetHeight, GetLocalForkHeightForWorldY(dropZone.DeliveryHeight));
                BeginDrop(dropZone);
            }
        }

        targetHeight = Mathf.Clamp(targetHeight, forkLocalMinHeight, forkLocalMaxHeight);
        Vector3 localPosition = forkLiftTransform.localPosition;
        localPosition.y = targetHeight;
        forkLiftTransform.localPosition = localPosition;
        UpdateForkRuntimeAttachments();
    }

    private void TryCaptureAvailablePallet()
    {
        if (carriedPallet != null)
        {
            return;
        }

        UpdateAvailablePallet();
        if (availablePallet == null || forkCarryPoint == null)
        {
            return;
        }

        carriedPallet = availablePallet;
        availablePallet = null;
        nearbyPallets.Remove(carriedPallet);
        ConveyorItem conveyorItem = carriedPallet.GetComponent<ConveyorItem>();
        conveyorItem?.CurrentController?.UnregisterItem(conveyorItem);
        carriedPallet.PrepareForCarry();
        carriedPallet.transform.SetParent(forkCarryPoint, true);
        cargoState = ForkCargoState.CarryingPallet;
    }

    private void UpdateCarriedPallet(float deltaTime)
    {
        if (carriedPallet == null || forkCarryPoint == null)
        {
            return;
        }

        carriedPallet.transform.localPosition = Vector3.Lerp(carriedPallet.transform.localPosition, Vector3.zero, autoSnapSpeed * deltaTime);
        carriedPallet.transform.localRotation = Quaternion.Slerp(carriedPallet.transform.localRotation, Quaternion.identity, autoSnapSpeed * deltaTime);
    }

    private void UpdateForkRuntimeAttachments()
    {
        if (forkRuntimeAttachments == null || forkLiftTransform == null)
        {
            return;
        }

        Vector3 localPosition = forkRuntimeLocalBasePosition;
        localPosition.y += forkLiftTransform.localPosition.y;
        forkRuntimeAttachments.localPosition = localPosition;
        forkRuntimeAttachments.localRotation = Quaternion.identity;
        forkRuntimeAttachments.localScale = Vector3.one;
    }

    private void BeginDrop(ForkliftPalletDropZone dropZone)
    {
        if (carriedPallet == null || dropZone == null || !dropZone.CanAccept(carriedPallet) || !dropZone.IsPalletCloseEnough(carriedPallet))
        {
            return;
        }

        cargoState = ForkCargoState.DepositingPallet;
        ForkliftPallet pallet = carriedPallet;
        carriedPallet = null;
        pallet.transform.SetParent(null, true);
        dropZone.CompleteDrop(pallet);
        currentDropZone = null;
        cargoState = ForkCargoState.ForkEmpty;
    }

    private ForkliftPalletDropZone GetValidDropZoneForCarry()
    {
        if (carriedPallet == null)
        {
            return null;
        }

        UpdateCurrentDropZone();
        return currentDropZone != null && currentDropZone.CanAccept(carriedPallet) && currentDropZone.IsPalletCloseEnough(carriedPallet)
            ? currentDropZone
            : null;
    }

    private bool IsForkAtDeliveryHeight(ForkliftPalletDropZone dropZone)
    {
        if (dropZone == null || forkLiftTransform == null)
        {
            return false;
        }

        float carryY = forkCarryPoint != null ? forkCarryPoint.position.y : forkLiftTransform.position.y;
        return carryY <= dropZone.DeliveryHeight + dropZone.HeightTolerance;
    }

    private float GetLocalForkHeightForWorldY(float worldY)
    {
        if (forkLiftTransform == null || forkLiftTransform.parent == null)
        {
            return forkLocalMinHeight;
        }

        Vector3 parentLocal = forkLiftTransform.parent.InverseTransformPoint(new Vector3(forkLiftTransform.position.x, worldY, forkLiftTransform.position.z));
        return parentLocal.y;
    }

    private void UpdateAvailablePallet()
    {
        if (carriedPallet != null)
        {
            availablePallet = null;
            cargoState = activeDropZones.Count > 0 ? ForkCargoState.InsideDropZone : ForkCargoState.CarryingPallet;
            return;
        }

        ScanPalletsNearForks();

        ForkliftPallet best = null;
        float bestDistance = float.MaxValue;
        foreach (ForkliftPallet pallet in nearbyPallets)
        {
            if (!IsValidPalletCandidate(pallet, out float distance))
            {
                continue;
            }

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pallet;
            }
        }

        availablePallet = best;
        cargoState = availablePallet != null ? ForkCargoState.PalletAvailable : ForkCargoState.ForkEmpty;
    }

    private void ScanPalletsNearForks()
    {
        if (forkPickupSensor == null)
        {
            return;
        }

        Transform sensorTransform = forkPickupSensor.transform;
        Vector3 halfExtents = new Vector3(captureWidth * 0.5f, 0.6f, captureDistance * 0.5f);
        Vector3 center = sensorTransform.position + sensorTransform.forward * (captureDistance * 0.25f);
        int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, palletScanBuffer, sensorTransform.rotation, validPalletLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hitCount; i++)
        {
            ForkliftPallet pallet = ResolvePallet(palletScanBuffer[i]);
            if (pallet != null && !pallet.IsCarried)
            {
                nearbyPallets.Add(pallet);
            }
        }
    }

    private bool IsValidPalletCandidate(ForkliftPallet pallet, out float distance)
    {
        distance = float.MaxValue;
        if (pallet == null || pallet.IsCarried || !IsLayerAllowed(pallet.gameObject.layer) || forkPickupSensor == null)
        {
            return false;
        }

        Vector3 local = transform.InverseTransformPoint(pallet.transform.position);
        distance = new Vector2(local.x, local.z).magnitude;
        if (distance > captureDistance || Mathf.Abs(local.x) > captureWidth * 0.5f)
        {
            return false;
        }

        float angle = Vector3.Angle(transform.forward, pallet.transform.forward);
        angle = Mathf.Min(angle, Mathf.Abs(180f - angle));
        return angle <= angularTolerance;
    }

    private void UpdateCurrentDropZone()
    {
        currentDropZone = null;
        float bestDistance = float.MaxValue;
        foreach (ForkliftPalletDropZone zone in activeDropZones)
        {
            if (zone == null || (carriedPallet != null && !zone.CanAccept(carriedPallet)))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(transform.position - zone.PalletPlacementPoint.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                currentDropZone = zone;
            }
        }
    }

    private void UpdateWheelVisuals(float deltaTime)
    {
        wheelSpin += currentSpeed * wheelVisualRotationSpeed * deltaTime;
        float visualSteeringAngle = maxSteeringAngle > 0.01f
            ? Mathf.Clamp(currentSteeringAngle / maxSteeringAngle, -1f, 1f) * wheelVisualSteeringAngle
            : 0f;
        SetWheelVisual(frontLeftWheel, 0, 0f);
        SetWheelVisual(frontRightWheel, 1, 0f);
        SetWheelVisual(rearLeftWheel, 2, visualSteeringAngle);
        SetWheelVisual(rearRightWheel, 3, visualSteeringAngle);

        if (steeringWheel != null)
        {
            steeringWheel.localRotation = steeringWheelBaseLocalRotation * Quaternion.Euler(0f, 0f, -visualSteeringAngle * steeringWheelMultiplier);
        }
    }

    private void SetWheelVisual(Transform wheel, int index, float steeringAngle)
    {
        if (wheel == null || wheelBaseLocalEuler == null || index >= wheelBaseLocalEuler.Length)
        {
            return;
        }

        Vector3 baseEuler = wheelBaseLocalEuler[index];
        wheel.localRotation = Quaternion.Euler(baseEuler) * Quaternion.Euler(0f, steeringAngle, 0f) * Quaternion.Euler(wheelSpin, 0f, 0f);
    }

    private void UpdateInteractionPrompt()
    {
        if (driverState == DriverState.PlayerDriving)
        {
            SetPromptVisible(temporaryPromptTimer > 0f);
            return;
        }

        PlayerTopDownController player = FindPlayerInRange();
        playerInInteractionRange = player != null;
        SetPromptVisible(playerInInteractionRange || temporaryPromptTimer > 0f);
    }

    private void UpdateDrivingPanel()
    {
        if (drivingPanelLabel == null)
        {
            return;
        }

        drivingPanelLabel.text =
            "EMPILHADEIRA\n\n" +
            "W / S - Frente e re\n" +
            "A / D - Direcao\n" +
            "R - Levantar garfos\n" +
            "Q - Abaixar garfos\n" +
            "Espaco - Frear\n" +
            "E - Sair";
    }

    private void SetPromptVisible(bool visible)
    {
        EnsureUi();
        if (interactionPromptLabel != null)
        {
            interactionPromptLabel.text = temporaryPromptTimer > 0f ? temporaryPrompt : enterPromptText;
        }

        if (interactionPromptObject != null)
        {
            interactionPromptObject.SetActive(visible);
        }
    }

    private void SetDrivingPanelVisible(bool visible)
    {
        EnsureUi();
        if (drivingPanelObject != null)
        {
            drivingPanelObject.SetActive(visible);
        }
    }

    private void ShowTemporaryPrompt(string message)
    {
        temporaryPrompt = message;
        temporaryPromptTimer = 1.4f;
        SetPromptVisible(true);
    }

    private void UpdateTemporaryPrompt()
    {
        if (temporaryPromptTimer <= 0f)
        {
            return;
        }

        temporaryPromptTimer -= Time.deltaTime;
        if (temporaryPromptTimer <= 0f)
        {
            temporaryPrompt = string.Empty;
        }
    }

    private bool TryFindSafeExitPosition(out Vector3 exitPosition)
    {
        Vector3 basePosition = playerExitPoint != null ? playerExitPoint.position : transform.position + transform.right * 1.5f;
        if (IsExitPositionSafe(basePosition))
        {
            exitPosition = basePosition;
            return true;
        }

        for (int i = 0; i < exitSearchSteps; i++)
        {
            float angle = (360f / Mathf.Max(1, exitSearchSteps)) * i;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * exitSearchRadius;
            Vector3 candidate = basePosition + offset;
            if (IsExitPositionSafe(candidate))
            {
                exitPosition = candidate;
                return true;
            }
        }

        exitPosition = basePosition;
        return false;
    }

    private bool IsExitPositionSafe(Vector3 position)
    {
        Vector3 bottom = position + Vector3.up * exitCheckRadius;
        Vector3 top = position + Vector3.up * Mathf.Max(exitCheckHeight, exitCheckRadius * 2f);
        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, exitCheckRadius, overlapBuffer, exitBlockingMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = overlapBuffer[i];
            if (hit == null || hit.transform.IsChildOf(transform) || (currentPlayer != null && hit.transform.IsChildOf(currentPlayer.transform)))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private PlayerTopDownController FindPlayerInRange()
    {
        PlayerTopDownController player = FindNearestPlayerController();
        if (player == null)
        {
            return null;
        }

        if (!IsPlayerFacingForklift(player))
        {
            return null;
        }

        if (interactionTrigger != null)
        {
            Vector3 closest = interactionTrigger.ClosestPoint(player.transform.position);
            float triggerMargin = Mathf.Max(0.25f, fallbackInteractionRadius * 0.35f);
            if (Vector3.SqrMagnitude(closest - player.transform.position) <= triggerMargin * triggerMargin || interactionTrigger.bounds.Contains(player.transform.position))
            {
                return player;
            }
        }

        return Vector3.SqrMagnitude(player.transform.position - transform.position) <= fallbackInteractionRadius * fallbackInteractionRadius ? player : null;
    }

    private bool IsPlayerFacingForklift(PlayerTopDownController player)
    {
        if (!requirePlayerFacingForklift || player == null)
        {
            return true;
        }

        Vector3 toForklift = transform.position - player.transform.position;
        toForklift.y = 0f;
        if (toForklift.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        Vector3 playerForward = player.transform.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        float facingDot = Vector3.Dot(playerForward.normalized, toForklift.normalized);
        return facingDot >= minimumFacingDot;
    }

    private PlayerTopDownController FindNearestPlayerController()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerObject = GameObject.Find("Player");
        }

        PlayerTopDownController player = playerObject != null ? playerObject.GetComponent<PlayerTopDownController>() : null;
        if (player != null)
        {
            return player;
        }

        PlayerTopDownController[] players = FindObjectsOfType<PlayerTopDownController>(true);
        PlayerTopDownController nearestPlayer = null;
        float nearestDistance = float.MaxValue;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || !players[i].gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(players[i].transform.position - transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPlayer = players[i];
            }
        }

        return nearestPlayer;
    }

    private ForkliftPallet ResolvePallet(Collider candidate)
    {
        return candidate != null ? candidate.GetComponentInParent<ForkliftPallet>() : null;
    }

    private bool IsLayerAllowed(int layer)
    {
        return (validPalletLayers.value & (1 << layer)) != 0;
    }

    private void ResolveReferences()
    {
        forkliftRigidbody = GetComponent<Rigidbody>();
        if (forkPickupSensor == null)
        {
            forkPickupSensor = GetComponentInChildren<ForkliftPickupSensor>(true);
        }

        forkPickupSensor?.Configure(this);

        if (forkLiftTransform == null)
        {
            forkLiftTransform = FindChildByName("ForkCarriage");
            if (forkLiftTransform == null)
            {
                forkLiftTransform = FindChildByName("elevacao_garfos");
            }
        }

        if (forkCarryPoint == null)
        {
            forkCarryPoint = FindChildByName("ForkCarryPoint");
        }

        if (forkRuntimeAttachments == null)
        {
            forkRuntimeAttachments = FindChildByName("ForkRuntimeAttachments");
        }

        if (driverSeatPoint == null)
        {
            driverSeatPoint = FindChildByName("DriverSeatPoint");
        }

        if (playerExitPoint == null)
        {
            playerExitPoint = FindChildByName("PlayerExitPoint");
        }

        if (frontLeftWheel == null) frontLeftWheel = FindChildByName("Wheel_FL");
        if (frontRightWheel == null) frontRightWheel = FindChildByName("Wheel_FR");
        if (rearLeftWheel == null) rearLeftWheel = FindChildByName("Wheel_RL");
        if (rearRightWheel == null) rearRightWheel = FindChildByName("Wheel_RR");
        ResolveWheelFallbacks();

        if (steeringWheel == null)
        {
            steeringWheel = FindChildByName("SteeringWheel");
            if (steeringWheel == null)
            {
                steeringWheel = FindChildByName("volante");
            }
        }

        if (topLampLight == null)
        {
            topLampLight = GetComponentInChildren<Light>(true);
        }

        if (topLampRenderer == null)
        {
            Transform lamp = FindChildByName("IndicatorLight");
            if (lamp == null)
            {
                lamp = FindChildByName("lampada de cima");
            }

            topLampRenderer = lamp != null ? lamp.GetComponent<Renderer>() : null;
        }
    }

    private void ResolveWheelFallbacks()
    {
        if (frontLeftWheel != null && frontRightWheel != null && rearLeftWheel != null && rearRightWheel != null)
        {
            return;
        }

        List<Transform> wheels = new List<Transform>();
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            string lowerName = child.name.ToLowerInvariant();
            if (lowerName.Contains("wheel") || lowerName.Contains("roda"))
            {
                wheels.Add(child);
            }
        }

        if (wheels.Count < 4)
        {
            return;
        }

        wheels.Sort((a, b) =>
        {
            Vector3 localA = transform.InverseTransformPoint(a.position);
            Vector3 localB = transform.InverseTransformPoint(b.position);
            int zCompare = localA.z.CompareTo(localB.z);
            return zCompare != 0 ? zCompare : localA.x.CompareTo(localB.x);
        });

        List<Transform> front = new List<Transform> { wheels[0], wheels[1] };
        List<Transform> rear = new List<Transform> { wheels[wheels.Count - 2], wheels[wheels.Count - 1] };
        front.Sort((a, b) => transform.InverseTransformPoint(a.position).x.CompareTo(transform.InverseTransformPoint(b.position).x));
        rear.Sort((a, b) => transform.InverseTransformPoint(a.position).x.CompareTo(transform.InverseTransformPoint(b.position).x));

        if (frontLeftWheel == null) frontLeftWheel = front[0];
        if (frontRightWheel == null) frontRightWheel = front[1];
        if (rearLeftWheel == null) rearLeftWheel = rear[0];
        if (rearRightWheel == null) rearRightWheel = rear[1];
    }

    private void ConfigureRigidbody()
    {
        if (forkliftRigidbody == null)
        {
            return;
        }

        forkliftRigidbody.mass = Mathf.Max(250f, forkliftRigidbody.mass);
        forkliftRigidbody.isKinematic = true;
        forkliftRigidbody.useGravity = false;
        forkliftRigidbody.centerOfMass = centerOfMass;
        forkliftRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        forkliftRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        forkliftRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        forkliftRigidbody.drag = Mathf.Max(forkliftRigidbody.drag, 2.5f);
        forkliftRigidbody.angularDrag = Mathf.Max(forkliftRigidbody.angularDrag, 8f);
    }

    private void CacheVisualRotations()
    {
        wheelBaseLocalEuler = new[]
        {
            frontLeftWheel != null ? frontLeftWheel.localEulerAngles : Vector3.zero,
            frontRightWheel != null ? frontRightWheel.localEulerAngles : Vector3.zero,
            rearLeftWheel != null ? rearLeftWheel.localEulerAngles : Vector3.zero,
            rearRightWheel != null ? rearRightWheel.localEulerAngles : Vector3.zero
        };

        steeringWheelBaseLocalRotation = steeringWheel != null ? steeringWheel.localRotation : Quaternion.identity;
    }

    private void SetTopLamp(bool enabled)
    {
        if (topLampLight != null)
        {
            topLampLight.enabled = enabled;
        }

        if (topLampRenderer != null)
        {
            Material targetMaterial = enabled ? topLampOnMaterial : topLampOffMaterial;
            if (targetMaterial != null)
            {
                topLampRenderer.sharedMaterial = targetMaterial;
            }
        }
    }

    private void EnsureUi()
    {
        if (canvas == null)
        {
            canvas = FindCanvasByName("InteractionCanvas");
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("InteractionCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (interactionPromptObject == null)
        {
            interactionPromptObject = CreatePanel("ForkliftInteractionPrompt", new Vector2(0.5f, 0.17f), new Vector2(420f, 46f));
            interactionPromptLabel = CreateText(interactionPromptObject.transform, "Text", enterPromptText, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        if (drivingPanelObject == null)
        {
            drivingPanelObject = CreatePanel("ForkliftDrivingPanel", new Vector2(0.03f, 0.66f), new Vector2(260f, 230f));
            drivingPanelLabel = CreateText(drivingPanelObject.transform, "Text", string.Empty, 17, FontStyle.Normal, TextAnchor.UpperLeft);
            RectTransform textRect = drivingPanelLabel.GetComponent<RectTransform>();
            textRect.offsetMin = new Vector2(18f, 16f);
            textRect.offsetMax = new Vector2(-18f, -16f);
            UpdateDrivingPanel();
        }
    }

    private GameObject CreatePanel(string panelName, Vector2 anchor, Vector2 size)
    {
        GameObject panel = new GameObject(panelName);
        panel.transform.SetParent(canvas.transform, false);
        RectTransform rect = panel.AddComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x <= 0.1f ? 0f : 0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.05f, 0.07f, 0.08f, 0.86f);
        return panel;
    }

    private Text CreateText(Transform parent, string objectName, string value, int fontSize, FontStyle style, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text text = textObject.AddComponent<Text>();
        text.text = value;
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Canvas FindCanvasByName(string canvasName)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == canvasName)
            {
                return canvases[i];
            }
        }

        return FindObjectOfType<Canvas>();
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private Transform FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return children[i];
            }
        }

        return null;
    }
}
