using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class EmpilhadeiraController : MonoBehaviour
{
#if UNITY_EDITOR
    private const string ForkliftIconSpritePath = "Assets/Imagens/empilhadeira/imagem empilhadeira.png";
#endif

    [Header("Debug")]
    [SerializeField] private bool playerNearby;
    [SerializeField] private bool playerDriving;
    [SerializeField] private bool debugForkAtRest;
    [SerializeField] private bool debugPalletDetected;
    [SerializeField] private bool debugPalletFitted;
    [SerializeField] private bool debugPalletCarried;
    [SerializeField] private bool debugInsideDropZone;

    [Header("Movimento")]
    [SerializeField] private float forwardForce = 30f;
    [SerializeField] private float reverseForce = 20f;
    [SerializeField] private float steeringTorque = 500f;
    [SerializeField] private float maxForwardSpeed = 5f;
    [SerializeField] private float maxReverseSpeed = 3.5f;
    [SerializeField] private float lateralGrip = 7f;
    [SerializeField] private float yawDamping = 5f;
    [SerializeField] private float maxYawSpeed = 1.2f;
    [SerializeField] private Vector3 localMovementDirection = Vector3.forward;

    [Header("Visual Rodas")]
    [SerializeField] private Transform frontLeftWheel;
    [SerializeField] private Transform frontRightWheel;
    [SerializeField] private Transform rearLeftWheel;
    [SerializeField] private Transform rearRightWheel;
    [SerializeField] private float rearWheelVisualAngle = 28f;
    [SerializeField] private float rearWheelVisualResponse = 12f;
    [SerializeField] private bool invertRearWheelVisualDirection = true;
    [SerializeField] private float wheelSpinMultiplier = 180f;
    [SerializeField] private Vector3 wheelSpinAxis = Vector3.right;

    [Header("Garfos")]
    [SerializeField] private Transform forkLiftTransform;
    [SerializeField] private float forkMinLocalY = 0f;
    [SerializeField] private float forkMaxLocalY = 0.7f;
    [SerializeField] private float forkLiftSpeed = 0.55f;
    [SerializeField] private float forkLowerSpeed = 0.55f;
    [SerializeField] private KeyCode forkLowerKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode forkLiftKey = KeyCode.Alpha2;

    [Header("Audio")]
    [SerializeField] private AudioSource engineAudioSource;
    [SerializeField] private AudioSource forkAudioSource;
    [SerializeField] private AudioClip engineStartupClip;
    [SerializeField] private AudioClip engineContinuousClip;
    [SerializeField] private AudioClip forkMovementClip;
    [SerializeField] private AudioClip forkStopClip;
    [SerializeField, Range(0f, 1f)] private float engineVolumeMultiplier = 0.6f;
    [SerializeField, Range(0.1f, 3f)] private float engineIdlePitch = 1f;
    [SerializeField, Range(0.1f, 3f)] private float engineAcceleratedPitch = 1.3f;
    [SerializeField, Range(0.05f, 1f)] private float enginePitchTransitionDuration = 0.4f;

    [Header("Sensor do Pallet")]
    [SerializeField] private Collider forkPickupSensor;
    [SerializeField] private LayerMask palletLayers = ~0;
    [SerializeField] private string palletNameFilter = "Pallet";
    [SerializeField] private float forkRestTolerance = 0.01f;

    [Header("Fisica")]
    [SerializeField] private bool kinematicWhenParked = true;
    [SerializeField] private Vector3 centerOfMass = new Vector3(0f, 0.25f, 0f);
    [SerializeField] private float linearDrag = 1.5f;
    [SerializeField] private float angularDrag = 8f;

    [Header("Interacao")]
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private Transform driverSeatPoint;
    [SerializeField] private Transform playerExitPoint;
    [SerializeField] private DeadZoneCameraFollow cameraFollow;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private string enterPromptText = "Aperte E para entrar na empilhadeira";
    [SerializeField] private bool allowCameraZoomWhileDriving = true;
    [SerializeField] private float exitSearchRadius = 1.2f;
    [SerializeField] private int exitSearchSteps = 12;
    [SerializeField] private float exitClearancePadding = 0.04f;
    [SerializeField] private LayerMask exitBlockMask = ~0;

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;
    [SerializeField] private GameObject drivingPanelObject;
    [SerializeField] private Text drivingPanelLabel;
    [SerializeField] private Sprite forkliftIconSprite;

    private Rigidbody body;
    private PlayerTopDownController nearbyPlayer;
    private PlayerTopDownController currentPlayer;
    private Transform originalPlayerParent;
    private Collider[] forkliftColliders;
    private Collider[] currentPlayerColliders;
    private Quaternion frontLeftWheelBaseRotation;
    private Quaternion frontRightWheelBaseRotation;
    private Quaternion rearLeftWheelBaseRotation;
    private Quaternion rearRightWheelBaseRotation;
    private float rearWheelCurrentAngle;
    private float wheelSpinAngle;
    private bool inputW;
    private bool inputS;
    private bool inputA;
    private bool inputD;
    private bool inputForkLower;
    private bool inputForkLift;
    private float throttleInput;
    private float steeringInput;
    private float forkCurrentLocalY;
    private Collider detectedPalletCollider;
    private Collider carriedPalletCollider;
    private Transform carriedPallet;
    private Rigidbody carriedPalletRigidbody;
    private ConveyorItem carriedConveyorItem;
    private Transform carriedPalletOriginalParent;
    private Collider[] carriedPalletColliders;
    private bool[] carriedPalletOriginalTriggerStates;
    private bool carriedPalletOriginalUseGravity;
    private bool carriedPalletOriginalIsKinematic;
    private bool carriedPalletOriginalDetectCollisions;
    private bool carriedPalletHasLeftRest;
    private Collider releasedPalletUntilSensorExit;
    private Vector3 carriedPalletLocalPosition;
    private Quaternion carriedPalletLocalRotation;
    private EmpilhadeiraPalletDropZone currentDropZone;
    private readonly Collider[] exitOverlapHits = new Collider[32];
    private InteractionPromptPresenter promptPresenter;
    private Coroutine engineStartupRoutine;
    private bool forkAudioMoving;
    private float currentSfxVolume = 1f;
    private bool currentSfxMuted;
    private bool gameplayPermanentlyLocked;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerNearby = false;
        playerDriving = false;
        nearbyPlayer = null;
        currentPlayer = null;
        ClearInputDebug();
        ResolveReferences();
        ResolveRearWheelReferences();
        CacheWheelVisualRotations();
        ConfigureRigidbody();
        ConfigureInteractionTrigger();
        ConfigureForkPickupSensor();
        PrepareAudioSources();
        EnsurePrompt();
        SetPromptVisible(false);
        SetDrivingPanelVisible(false);
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureInteractionTrigger();
        ConfigureForkPickupSensor();
    }

    private void OnValidate()
    {
        forwardForce = Mathf.Max(0f, forwardForce);
        reverseForce = Mathf.Max(0f, reverseForce);
        steeringTorque = Mathf.Max(0f, steeringTorque);
        maxForwardSpeed = Mathf.Max(0.1f, maxForwardSpeed);
        maxReverseSpeed = Mathf.Max(0.1f, maxReverseSpeed);
        lateralGrip = Mathf.Max(0f, lateralGrip);
        yawDamping = Mathf.Max(0f, yawDamping);
        maxYawSpeed = Mathf.Max(0.1f, maxYawSpeed);
        rearWheelVisualAngle = Mathf.Max(0f, rearWheelVisualAngle);
        rearWheelVisualResponse = Mathf.Max(0.01f, rearWheelVisualResponse);
        wheelSpinMultiplier = Mathf.Max(0f, wheelSpinMultiplier);
        forkMaxLocalY = Mathf.Max(forkMinLocalY, forkMaxLocalY);
        forkLiftSpeed = Mathf.Max(0.01f, forkLiftSpeed);
        forkLowerSpeed = Mathf.Max(0.01f, forkLowerSpeed);
        forkRestTolerance = Mathf.Max(0.001f, forkRestTolerance);
        engineIdlePitch = Mathf.Clamp(engineIdlePitch, 0.1f, 3f);
        engineAcceleratedPitch = Mathf.Clamp(engineAcceleratedPitch, engineIdlePitch, 3f);
        enginePitchTransitionDuration = Mathf.Clamp(enginePitchTransitionDuration, 0.05f, 1f);
        linearDrag = Mathf.Max(0f, linearDrag);
        angularDrag = Mathf.Max(0f, angularDrag);
    }

    private void Update()
    {
        if (gameplayPermanentlyLocked)
        {
            return;
        }

        ValidateNearbyPlayer();
        UpdateInputDebug();
        UpdateEnginePitch(Time.deltaTime);
        RefreshForkAndPalletDebug();
        TryAttachPalletOnLiftInput();
        UpdateFork(Time.deltaTime);
        RefreshForkAndPalletDebug();
        ValidateCurrentDropZone();
        UpdatePalletCarryState();
        UpdateWheelVisuals(Time.deltaTime);
        UpdateDrivingPanel();
        UpdatePrompt();

        if (!Input.GetKeyDown(interactionKey))
        {
            return;
        }

        if (playerDriving)
        {
            ExitForklift();
        }
        else if (playerNearby)
        {
            EnterForklift();
        }
    }

    private void FixedUpdate()
    {
        if (!playerDriving)
        {
            ApplyYawDamping(Time.fixedDeltaTime);
            SyncCarriedPalletToFork();
            return;
        }

        Vector3 direction = GetMovementDirection();
        float forwardSpeed = Vector3.Dot(Vector3.ProjectOnPlane(body.velocity, Vector3.up), direction);

        if (inputW && forwardSpeed < maxForwardSpeed)
        {
            body.AddForce(direction * forwardForce, ForceMode.Force);
        }

        if (inputS && forwardSpeed > -maxReverseSpeed)
        {
            body.AddForce(-direction * reverseForce, ForceMode.Force);
        }

        ApplyLateralGrip(direction);

        if (!Mathf.Approximately(throttleInput, 0f) && !Mathf.Approximately(steeringInput, 0f))
        {
            body.AddTorque(Vector3.up * steeringInput * throttleInput * steeringTorque, ForceMode.Force);
        }

        ApplyYawDamping(Time.fixedDeltaTime);
        SyncCarriedPalletToFork();
    }

    private void LateUpdate()
    {
        SyncCarriedPalletRigidbody();
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerExit(Collider other)
    {
        NotifyPlayerExitInteraction(other);
    }

    public void NotifyPlayerEnterInteraction(Collider other)
    {
        if (gameplayPermanentlyLocked)
        {
            return;
        }

        PlayerTopDownController player = ResolvePlayer(other);
        if (player == null)
        {
            return;
        }

        nearbyPlayer = player;
        playerNearby = true;
    }

    public void NotifyPlayerExitInteraction(Collider other)
    {
        PlayerTopDownController player = ResolvePlayer(other);
        if (player == null || player != nearbyPlayer)
        {
            return;
        }

        nearbyPlayer = null;
        playerNearby = false;
    }

    public void NotifyForkSensorEnter(Collider other)
    {
        if (!IsValidPalletCollider(other))
        {
            return;
        }

        detectedPalletCollider = other;
        RefreshForkAndPalletDebug();
    }

    public void NotifyForkSensorStay(Collider other)
    {
        if (!IsValidPalletCollider(other))
        {
            return;
        }

        detectedPalletCollider = other;
        RefreshForkAndPalletDebug();
    }

    public void NotifyForkSensorExit(Collider other)
    {
        if (other == null || other != detectedPalletCollider)
        {
            if (other != null && other == releasedPalletUntilSensorExit)
            {
                releasedPalletUntilSensorExit = null;
            }

            return;
        }

        if (other == releasedPalletUntilSensorExit)
        {
            releasedPalletUntilSensorExit = null;
        }

        detectedPalletCollider = null;
        RefreshForkAndPalletDebug();
    }

    public void NotifyDropZoneEnter(EmpilhadeiraPalletDropZone dropZone)
    {
        if (dropZone == null)
        {
            return;
        }

        currentDropZone = dropZone;
        debugInsideDropZone = true;

        if (carriedPallet != null)
        {
            ReleasePallet();
        }
    }

    public void NotifyDropZoneExit(EmpilhadeiraPalletDropZone dropZone)
    {
        if (dropZone == null || dropZone != currentDropZone)
        {
            return;
        }

        ValidateCurrentDropZone();
    }

    public void NotifyBeltTouchedByCarriedPallet(EmpilhadeiraBeltPalletDropSensor beltSensor, Collider palletCollider)
    {
        NotifyBeltTouchedByCarriedPallet(beltSensor, palletCollider, null, null, null, true, 0f);
    }

    public void NotifyBeltTouchedByCarriedPallet(
        EmpilhadeiraBeltPalletDropSensor beltSensor,
        Collider palletCollider,
        ConveyorController targetConveyor,
        Transform receivePoint,
        string productId,
        bool keepCurrentRotation,
        float lateralOffset)
    {
        if (beltSensor == null || palletCollider == null || carriedPallet == null)
        {
            return;
        }

        if (palletCollider.transform != carriedPallet && !palletCollider.transform.IsChildOf(carriedPallet))
        {
            return;
        }

        if (beltSensor.RequireLowerInput && !inputForkLower)
        {
            return;
        }

        if (beltSensor.RequireForkSensorOverlap && !beltSensor.ContainsForkSensor(forkPickupSensor))
        {
            return;
        }

        ReleasePallet(targetConveyor, receivePoint, productId, keepCurrentRotation, lateralOffset);
    }

    private void ValidateCurrentDropZone()
    {
        if (currentDropZone != null && !currentDropZone.ContainsForkSensor(forkPickupSensor))
        {
            currentDropZone = null;
        }

        debugInsideDropZone = currentDropZone != null;
    }

    private void ValidateNearbyPlayer()
    {
        if (!playerNearby || nearbyPlayer == null || playerDriving)
        {
            return;
        }

        if (interactionTrigger == null)
        {
            playerNearby = false;
            nearbyPlayer = null;
            return;
        }

        Vector3 playerPosition = nearbyPlayer.transform.position;
        Vector3 closestPoint = interactionTrigger.ClosestPoint(playerPosition);
        float maxDistance = 0.45f;
        if (Vector3.SqrMagnitude(closestPoint - playerPosition) > maxDistance * maxDistance)
        {
            playerNearby = false;
            nearbyPlayer = null;
        }
    }

    private void EnterForklift()
    {
        if (gameplayPermanentlyLocked || nearbyPlayer == null || driverSeatPoint == null)
        {
            return;
        }

        currentPlayer = nearbyPlayer;
        originalPlayerParent = currentPlayer.transform.parent;
        SetParkedKinematic(false);
        currentPlayer.SetExternalMovementLocked(true);
        SetDrivingCameraZoomUnlocked();
        currentPlayer.SetForkliftDrivingAnimation(true);
        currentPlayer.transform.SetParent(driverSeatPoint, true);
        currentPlayer.transform.SetPositionAndRotation(driverSeatPoint.position, driverSeatPoint.rotation);
        SetPlayerCollisionIgnored(true);

        playerDriving = true;
        playerNearby = false;
        SetPromptVisible(false);
        SetDrivingPanelVisible(true);
        PlayEngineStartup();
    }

    private void ExitForklift()
    {
        ExitForklift(false);
    }

    private void ExitForklift(bool forceExit)
    {
        if (currentPlayer == null)
        {
            playerDriving = false;
            StopVehicleAudio(true);
            SetPromptVisible(false);
            return;
        }

        if (!TryFindSafeExitPose(out Vector3 exitPosition, out Quaternion exitRotation))
        {
            if (!forceExit)
            {
                return;
            }

            Transform fallback = playerExitPoint != null ? playerExitPoint : transform;
            exitPosition = fallback.position;
            exitRotation = fallback.rotation;
        }

        currentPlayer.transform.SetParent(originalPlayerParent, true);
        currentPlayer.transform.SetPositionAndRotation(exitPosition, exitRotation);
        currentPlayer.SetForkliftDrivingAnimation(false);
        SetPlayerCollisionIgnored(false);
        currentPlayer.SetExternalMovementLocked(false);

        currentPlayer = null;
        originalPlayerParent = null;
        playerDriving = false;
        SetParkedKinematic(true);
        ClearInputDebug();
        StopVehicleAudio(true);
        SetPromptVisible(false);
        SetDrivingPanelVisible(false);
    }

    public void PrepareForStageCompletion()
    {
        if (gameplayPermanentlyLocked)
        {
            return;
        }

        if (playerDriving || currentPlayer != null)
        {
            ExitForklift(true);
        }

        gameplayPermanentlyLocked = true;
        nearbyPlayer = null;
        playerNearby = false;
        playerDriving = false;
        ClearInputDebug();
        SetParkedKinematic(true);
        StopVehicleAudio(false);
        SetPromptVisible(false);
        SetDrivingPanelVisible(false);
        if (interactionTrigger != null)
        {
            interactionTrigger.enabled = false;
        }
    }

    private bool TryFindSafeExitPose(out Vector3 safePosition, out Quaternion safeRotation)
    {
        Transform exitPoint = playerExitPoint != null ? playerExitPoint : transform;
        safeRotation = exitPoint.rotation;

        if (IsExitPositionClear(exitPoint.position, safeRotation))
        {
            safePosition = exitPoint.position;
            return true;
        }

        int steps = Mathf.Max(4, exitSearchSteps);
        float radius = Mathf.Max(0.2f, exitSearchRadius);
        Vector3 center = exitPoint.position;

        for (int ring = 1; ring <= 2; ring++)
        {
            float ringRadius = radius * ring / 2f;
            for (int i = 0; i < steps; i++)
            {
                float angle = (Mathf.PI * 2f * i) / steps;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
                Vector3 candidate = center + offset;
                if (IsExitPositionClear(candidate, safeRotation))
                {
                    safePosition = candidate;
                    return true;
                }
            }
        }

        safePosition = exitPoint.position;
        return false;
    }

    private bool IsExitPositionClear(Vector3 position, Quaternion rotation)
    {
        if (currentPlayer == null)
        {
            return false;
        }

        CharacterController character = currentPlayer.GetComponent<CharacterController>();
        float radius = 0.32f;
        float height = 1.55f;
        Vector3 centerOffset = Vector3.up * (height * 0.5f);

        if (character != null)
        {
            Vector3 scale = character.transform.lossyScale;
            radius = character.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) + exitClearancePadding;
            height = Mathf.Max(character.height * Mathf.Abs(scale.y), radius * 2f);
            centerOffset = rotation * character.center;
        }

        Vector3 center = position + centerOffset;
        Vector3 up = Vector3.up;
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 bottom = center - up * halfSegment + up * exitClearancePadding;
        Vector3 top = center + up * halfSegment;

        int hitCount = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, exitOverlapHits, exitBlockMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = exitOverlapHits[i];
            exitOverlapHits[i] = null;

            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            if (hit.transform.IsChildOf(currentPlayer.transform) || hit.transform.IsChildOf(transform))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private PlayerTopDownController ResolvePlayer(Collider candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        PlayerTopDownController player = candidate.GetComponentInParent<PlayerTopDownController>();
        if (player != null)
        {
            return player;
        }

        return candidate.CompareTag("Player") ? candidate.GetComponentInParent<PlayerTopDownController>() : null;
    }

    private void ResolveReferences()
    {
        if (cameraFollow == null && Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<DeadZoneCameraFollow>();
        }

        if (interactionTrigger == null)
        {
            Transform triggerTransform = transform.Find("InteractionTrigger");
            interactionTrigger = triggerTransform != null ? triggerTransform.GetComponent<Collider>() : null;
        }

        if (driverSeatPoint == null)
        {
            driverSeatPoint = transform.Find("DriverSeatPoint");
        }

        if (playerExitPoint == null)
        {
            playerExitPoint = transform.Find("PlayerExitPoint");
        }

        if (forkLiftTransform == null)
        {
            forkLiftTransform = FindChildByName("elevacao_garfos");
            if (forkLiftTransform == null)
            {
                forkLiftTransform = FindChildByName("Elevação_Garfo");
            }

            if (forkLiftTransform == null)
            {
                forkLiftTransform = FindChildByName("Elevacao_Garfo");
            }
        }

        if (forkPickupSensor == null)
        {
            Transform sensorTransform = FindChildByName("ForkPickupSensor");
            forkPickupSensor = sensorTransform != null ? sensorTransform.GetComponent<Collider>() : null;
        }
    }

    private void SetDrivingCameraZoomUnlocked()
    {
        if (!allowCameraZoomWhileDriving)
        {
            return;
        }

        if (cameraFollow == null && Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<DeadZoneCameraFollow>();
        }

        cameraFollow?.SetZoomLocked(false);
    }

    private void ResolveRearWheelReferences()
    {
        if (frontLeftWheel != null && frontRightWheel != null && rearLeftWheel != null && rearRightWheel != null)
        {
            return;
        }

        Transform wheelFL = FindChildByName("Wheel_FL");
        Transform wheelFR = FindChildByName("Wheel_FR");
        Transform wheelRL = FindChildByName("Wheel_RL");
        Transform wheelRR = FindChildByName("Wheel_RR");
        if (wheelFL != null && wheelFR != null && wheelRL != null && wheelRR != null)
        {
            frontLeftWheel = frontLeftWheel != null ? frontLeftWheel : wheelFL;
            frontRightWheel = frontRightWheel != null ? frontRightWheel : wheelFR;
            rearLeftWheel = rearLeftWheel != null ? rearLeftWheel : wheelRL;
            rearRightWheel = rearRightWheel != null ? rearRightWheel : wheelRR;
            return;
        }

        Transform[] children = GetComponentsInChildren<Transform>(true);
        Transform[] wheels = new Transform[4];
        float[] wheelForwardPositions = new float[4] { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
        Vector3 localForward = localMovementDirection.sqrMagnitude > 0.0001f ? localMovementDirection.normalized : Vector3.forward;

        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == null || child == transform)
            {
                continue;
            }

            string lowerName = child.name.ToLowerInvariant();
            if (!lowerName.Contains("roda") && !lowerName.Contains("wheel"))
            {
                continue;
            }

            Vector3 rootLocalPosition = transform.InverseTransformPoint(child.position);
            float forwardPosition = Vector3.Dot(rootLocalPosition, localForward);
            for (int wheelIndex = 0; wheelIndex < wheels.Length; wheelIndex++)
            {
                if (forwardPosition >= wheelForwardPositions[wheelIndex])
                {
                    continue;
                }

                for (int shift = wheels.Length - 1; shift > wheelIndex; shift--)
                {
                    wheels[shift] = wheels[shift - 1];
                    wheelForwardPositions[shift] = wheelForwardPositions[shift - 1];
                }

                wheels[wheelIndex] = child;
                wheelForwardPositions[wheelIndex] = forwardPosition;
                break;
            }
        }

        if (wheels[0] == null || wheels[1] == null)
        {
            return;
        }

        AssignLeftRight(wheels[0], wheels[1], ref rearLeftWheel, ref rearRightWheel);

        if (wheels[2] != null && wheels[3] != null)
        {
            AssignLeftRight(wheels[2], wheels[3], ref frontLeftWheel, ref frontRightWheel);
        }
    }

    private void AssignLeftRight(Transform a, Transform b, ref Transform left, ref Transform right)
    {
        Transform resolvedLeft = transform.InverseTransformPoint(a.position).x <= transform.InverseTransformPoint(b.position).x ? a : b;
        Transform resolvedRight = resolvedLeft == a ? b : a;
        left = left != null ? left : resolvedLeft;
        right = right != null ? right : resolvedRight;
    }

    private Transform FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void CacheWheelVisualRotations()
    {
        frontLeftWheelBaseRotation = frontLeftWheel != null ? frontLeftWheel.localRotation : Quaternion.identity;
        frontRightWheelBaseRotation = frontRightWheel != null ? frontRightWheel.localRotation : Quaternion.identity;
        rearLeftWheelBaseRotation = rearLeftWheel != null ? rearLeftWheel.localRotation : Quaternion.identity;
        rearRightWheelBaseRotation = rearRightWheel != null ? rearRightWheel.localRotation : Quaternion.identity;
    }

    private void ConfigureRigidbody()
    {
        if (body == null)
        {
            return;
        }

        body.useGravity = true;
        body.centerOfMass = centerOfMass;
        body.drag = Mathf.Max(0f, linearDrag);
        body.angularDrag = Mathf.Max(0f, angularDrag);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        SetParkedKinematic(!playerDriving);
    }

    private void SetParkedKinematic(bool parked)
    {
        if (body == null)
        {
            return;
        }

        bool shouldBeKinematic = parked && kinematicWhenParked;
        if (shouldBeKinematic)
        {
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            return;
        }

        body.isKinematic = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private void ConfigureInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            return;
        }

        interactionTrigger.isTrigger = true;
        if (interactionTrigger.GetComponent<EmpilhadeiraInteractionTrigger>() == null)
        {
            interactionTrigger.gameObject.AddComponent<EmpilhadeiraInteractionTrigger>();
        }
    }

    private void ConfigureForkPickupSensor()
    {
        if (forkPickupSensor == null && forkLiftTransform != null)
        {
            Transform existingSensor = FindChildByName("ForkPickupSensor");
            if (existingSensor == null)
            {
                GameObject sensorObject = new GameObject("ForkPickupSensor");
                sensorObject.transform.SetParent(forkLiftTransform, false);
                sensorObject.transform.localPosition = Vector3.zero;
                sensorObject.transform.localRotation = Quaternion.identity;
                sensorObject.transform.localScale = Vector3.one * 0.01f;
                BoxCollider sensorBox = sensorObject.AddComponent<BoxCollider>();
                sensorBox.center = new Vector3(0f, 0.08f, 0.59f);
                sensorBox.size = new Vector3(0.56f, 0.16f, 0.08f);
                forkPickupSensor = sensorBox;
            }
            else
            {
                forkPickupSensor = existingSensor.GetComponent<Collider>();
                if (forkPickupSensor == null)
                {
                    forkPickupSensor = existingSensor.gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        if (forkPickupSensor == null)
        {
            return;
        }

        forkPickupSensor.isTrigger = true;
        EmpilhadeiraForkPickupTrigger trigger = forkPickupSensor.GetComponent<EmpilhadeiraForkPickupTrigger>();
        if (trigger == null)
        {
            trigger = forkPickupSensor.gameObject.AddComponent<EmpilhadeiraForkPickupTrigger>();
        }

        trigger.SetController(this);
    }

    private void UpdatePrompt()
    {
        if (playerDriving)
        {
            SetPromptVisible(false);
            return;
        }

        SetPromptText(enterPromptText);
        SetPromptVisible(playerNearby);
    }

    private void UpdateInputDebug()
    {
        if (!playerDriving)
        {
            ClearInputDebug();
            return;
        }

        inputW = Input.GetKey(KeyCode.W);
        inputS = Input.GetKey(KeyCode.S);
        inputA = Input.GetKey(KeyCode.A);
        inputD = Input.GetKey(KeyCode.D);
        inputForkLower = Input.GetKey(forkLowerKey) || Input.GetKey(KeyCode.Keypad1);
        inputForkLift = Input.GetKey(forkLiftKey) || Input.GetKey(KeyCode.Keypad2);

        throttleInput = 0f;
        if (inputW && !inputS)
        {
            throttleInput = 1f;
        }
        else if (inputS && !inputW)
        {
            throttleInput = -1f;
        }

        steeringInput = 0f;
        if (inputD && !inputA)
        {
            steeringInput = 1f;
        }
        else if (inputA && !inputD)
        {
            steeringInput = -1f;
        }
    }

    private void ClearInputDebug()
    {
        inputW = false;
        inputS = false;
        inputA = false;
        inputD = false;
        inputForkLower = false;
        inputForkLift = false;
        throttleInput = 0f;
        steeringInput = 0f;
    }

    private void RefreshForkAndPalletDebug()
    {
        forkCurrentLocalY = forkLiftTransform != null ? forkLiftTransform.localPosition.y : 0f;
        debugForkAtRest = forkLiftTransform != null && forkCurrentLocalY <= forkMinLocalY + forkRestTolerance;

        if (detectedPalletCollider != null && !detectedPalletCollider.enabled)
        {
            detectedPalletCollider = null;
        }

        debugPalletDetected = detectedPalletCollider != null;
        debugPalletFitted = debugPalletDetected && IsForkSensorInsidePallet(detectedPalletCollider);
        debugPalletCarried = carriedPallet != null;
    }

    private void UpdatePalletCarryState()
    {
        if (forkLiftTransform == null)
        {
            return;
        }

        if (carriedPallet == null)
        {
            return;
        }

        if (!debugForkAtRest)
        {
            carriedPalletHasLeftRest = true;
            return;
        }

        if (carriedPalletHasLeftRest)
        {
            ReleasePallet();
        }
    }

    private void TryAttachPalletOnLiftInput()
    {
        if (!playerDriving ||
            !inputForkLift ||
            carriedPallet != null ||
            forkLiftTransform == null ||
            !debugForkAtRest ||
            !debugPalletDetected ||
            !debugPalletFitted ||
            detectedPalletCollider == null ||
            detectedPalletCollider == releasedPalletUntilSensorExit)
        {
            return;
        }

        AttachPallet(detectedPalletCollider);
    }

    private void AttachPallet(Collider palletCollider)
    {
        Transform pallet = ResolvePalletRoot(palletCollider);
        if (pallet == null || carriedPallet != null)
        {
            return;
        }

        carriedPalletCollider = palletCollider;
        carriedPallet = pallet;
        carriedPalletOriginalParent = pallet.parent;
        carriedPalletRigidbody = pallet.GetComponent<Rigidbody>();
        carriedConveyorItem = pallet.GetComponent<ConveyorItem>();
        carriedPalletHasLeftRest = false;

        if (carriedConveyorItem != null)
        {
            carriedConveyorItem.CurrentController?.UnregisterItem(carriedConveyorItem);
            carriedConveyorItem.MarkBeingCollected();
        }

        if (carriedPalletRigidbody != null)
        {
            carriedPalletOriginalUseGravity = carriedPalletRigidbody.useGravity;
            carriedPalletOriginalIsKinematic = carriedPalletRigidbody.isKinematic;
            carriedPalletOriginalDetectCollisions = carriedPalletRigidbody.detectCollisions;

            if (!carriedPalletRigidbody.isKinematic)
            {
                carriedPalletRigidbody.velocity = Vector3.zero;
                carriedPalletRigidbody.angularVelocity = Vector3.zero;
            }

            carriedPalletRigidbody.useGravity = false;
            carriedPalletRigidbody.isKinematic = true;
            carriedPalletRigidbody.detectCollisions = true;
        }

        pallet.SetParent(forkLiftTransform, true);
        carriedPalletLocalPosition = forkLiftTransform.InverseTransformPoint(pallet.position);
        carriedPalletLocalRotation = Quaternion.Inverse(forkLiftTransform.rotation) * pallet.rotation;
        PrepareCarriedPalletCollidersForSensor();
        SetPalletForkliftCollisionIgnored(true);
        SyncCarriedPalletToFork();
        debugPalletCarried = true;
    }

    private void ReleasePallet()
    {
        ReleasePallet(null, null, null, true, 0f);
    }

    private void ReleasePallet(ConveyorController targetConveyor, Transform receivePoint, string productId, bool keepCurrentRotation, float lateralOffset)
    {
        if (carriedPallet == null)
        {
            return;
        }

        ApplyDropZonePlacement();
        RestoreCarriedPalletColliders();
        SetPalletForkliftCollisionIgnored(false);
        Transform releasedPallet = carriedPallet;
        Rigidbody releasedRigidbody = carriedPalletRigidbody;
        ConveyorItem releasedConveyorItem = carriedConveyorItem;
        carriedPallet.SetParent(carriedPalletOriginalParent, true);
        releasedPalletUntilSensorExit = carriedPalletCollider != null ? carriedPalletCollider : detectedPalletCollider;

        if (targetConveyor == null && carriedPalletRigidbody != null)
        {
            carriedPalletRigidbody.isKinematic = carriedPalletOriginalIsKinematic;
            carriedPalletRigidbody.useGravity = carriedPalletOriginalUseGravity;
            carriedPalletRigidbody.detectCollisions = carriedPalletOriginalDetectCollisions;
            if (!carriedPalletRigidbody.isKinematic)
            {
                carriedPalletRigidbody.velocity = Vector3.zero;
                carriedPalletRigidbody.angularVelocity = Vector3.zero;
            }
        }

        if (targetConveyor != null)
        {
            SendReleasedPalletToConveyor(releasedPallet, releasedRigidbody, releasedConveyorItem, targetConveyor, receivePoint, productId, keepCurrentRotation, lateralOffset);
        }

        carriedPallet = null;
        carriedPalletCollider = null;
        carriedPalletRigidbody = null;
        carriedConveyorItem = null;
        carriedPalletOriginalParent = null;
        carriedPalletColliders = null;
        carriedPalletOriginalTriggerStates = null;
        carriedPalletHasLeftRest = false;
        carriedPalletLocalPosition = Vector3.zero;
        carriedPalletLocalRotation = Quaternion.identity;
        debugPalletCarried = false;
    }

    private void SendReleasedPalletToConveyor(
        Transform releasedPallet,
        Rigidbody releasedRigidbody,
        ConveyorItem releasedConveyorItem,
        ConveyorController targetConveyor,
        Transform receivePoint,
        string productId,
        bool keepCurrentRotation,
        float lateralOffset)
    {
        if (releasedPallet == null || targetConveyor == null)
        {
            return;
        }

        string nextProductId = productId;
        if (string.IsNullOrWhiteSpace(nextProductId) && releasedConveyorItem != null)
        {
            nextProductId = releasedConveyorItem.ProductId;
        }

        if (string.IsNullOrWhiteSpace(nextProductId))
        {
            nextProductId = "PalletWithBoxes";
        }

        Quaternion releaseRotation = releasedPallet.rotation;
        bool received = false;

        if (receivePoint != null)
        {
            received = targetConveyor.TryReceiveItem(releasedPallet.gameObject, nextProductId, receivePoint, false, lateralOffset);
        }

        if (!received)
        {
            received = RegisterReleasedPalletAtClosestConveyorPoint(releasedPallet, releasedConveyorItem, targetConveyor, nextProductId, lateralOffset);
        }

        if (keepCurrentRotation)
        {
            releasedPallet.rotation = releaseRotation;
            if (releasedRigidbody != null)
            {
                releasedRigidbody.rotation = releaseRotation;
            }
        }

        if (received)
        {
            MissionManager.NotifyStage2PalletPlacedOnConveyor(releasedPallet.gameObject);
        }
    }

    private bool RegisterReleasedPalletAtClosestConveyorPoint(
        Transform releasedPallet,
        ConveyorItem releasedConveyorItem,
        ConveyorController targetConveyor,
        string productId,
        float lateralOffset)
    {
        if (releasedPallet == null || targetConveyor == null || targetConveyor.ConveyorPath == null || !targetConveyor.ConveyorPath.IsValid())
        {
            return false;
        }

        ConveyorItem item = releasedConveyorItem != null ? releasedConveyorItem : releasedPallet.GetComponent<ConveyorItem>();
        if (item == null)
        {
            item = releasedPallet.gameObject.AddComponent<ConveyorItem>();
        }

        releasedPallet.SetParent(targetConveyor.transform, true);
        targetConveyor.RegisterItem(item);
        float distance = targetConveyor.ConveyorPath.GetClosestDistance(releasedPallet.position);
        item.Initialize(targetConveyor, targetConveyor.ConveyorPath, productId, distance, lateralOffset);
        return true;
    }

    private void PrepareCarriedPalletCollidersForSensor()
    {
        if (carriedPallet == null)
        {
            return;
        }

        carriedPalletColliders = carriedPallet.GetComponentsInChildren<Collider>(true);
        carriedPalletOriginalTriggerStates = new bool[carriedPalletColliders.Length];

        for (int i = 0; i < carriedPalletColliders.Length; i++)
        {
            Collider palletCollider = carriedPalletColliders[i];
            if (palletCollider == null)
            {
                continue;
            }

            carriedPalletOriginalTriggerStates[i] = palletCollider.isTrigger;
            palletCollider.isTrigger = true;
        }
    }

    private void RestoreCarriedPalletColliders()
    {
        if (carriedPalletColliders == null || carriedPalletOriginalTriggerStates == null)
        {
            return;
        }

        int count = Mathf.Min(carriedPalletColliders.Length, carriedPalletOriginalTriggerStates.Length);
        for (int i = 0; i < count; i++)
        {
            Collider palletCollider = carriedPalletColliders[i];
            if (palletCollider == null)
            {
                continue;
            }

            palletCollider.isTrigger = carriedPalletOriginalTriggerStates[i];
        }
    }

    private void ApplyDropZonePlacement()
    {
        if (currentDropZone == null || carriedPallet == null || !currentDropZone.SnapToPlacementPoint)
        {
            return;
        }

        Transform placementPoint = currentDropZone.PalletPlacementPoint;
        if (placementPoint == null)
        {
            return;
        }

        carriedPallet.SetPositionAndRotation(placementPoint.position, placementPoint.rotation);
        if (carriedPalletRigidbody != null)
        {
            carriedPalletRigidbody.position = placementPoint.position;
            carriedPalletRigidbody.rotation = placementPoint.rotation;
        }
    }

    private void SyncCarriedPalletRigidbody()
    {
        SyncCarriedPalletToFork();
    }

    private void SyncCarriedPalletToFork()
    {
        if (carriedPallet == null || forkLiftTransform == null)
        {
            return;
        }

        Vector3 targetPosition = forkLiftTransform.TransformPoint(carriedPalletLocalPosition);
        Quaternion targetRotation = forkLiftTransform.rotation * carriedPalletLocalRotation;

        carriedPallet.SetPositionAndRotation(targetPosition, targetRotation);
        if (carriedPalletRigidbody != null)
        {
            carriedPalletRigidbody.position = targetPosition;
            carriedPalletRigidbody.rotation = targetRotation;
        }
    }

    private Transform ResolvePalletRoot(Collider palletCollider)
    {
        if (palletCollider == null)
        {
            return null;
        }

        return palletCollider.attachedRigidbody != null ? palletCollider.attachedRigidbody.transform : palletCollider.transform;
    }

    private void SetPalletForkliftCollisionIgnored(bool ignored)
    {
        if (carriedPallet == null)
        {
            return;
        }

        Collider[] solidForkliftColliders = GetComponentsInChildren<Collider>(true);
        Collider[] palletColliders = carriedPallet.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < solidForkliftColliders.Length; i++)
        {
            Collider forkliftCollider = solidForkliftColliders[i];
            if (forkliftCollider == null ||
                forkliftCollider.isTrigger ||
                forkliftCollider.transform.IsChildOf(carriedPallet))
            {
                continue;
            }

            for (int j = 0; j < palletColliders.Length; j++)
            {
                Collider palletCollider = palletColliders[j];
                if (palletCollider == null || palletCollider.isTrigger)
                {
                    continue;
                }

                Physics.IgnoreCollision(forkliftCollider, palletCollider, ignored);
            }
        }
    }

    private bool IsValidPalletCollider(Collider candidate)
    {
        if (candidate == null || candidate.isTrigger)
        {
            return false;
        }

        if ((palletLayers.value & (1 << candidate.gameObject.layer)) == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(palletNameFilter))
        {
            return true;
        }

        Transform current = candidate.transform;
        while (current != null)
        {
            if (current.name.IndexOf(palletNameFilter, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsForkSensorInsidePallet(Collider palletCollider)
    {
        if (forkPickupSensor == null || palletCollider == null)
        {
            return false;
        }

        Bounds palletBounds = palletCollider.bounds;
        Bounds sensorBounds = forkPickupSensor.bounds;
        Vector3 sensorCenter = sensorBounds.center;
        if (palletBounds.Contains(sensorCenter))
        {
            return true;
        }

        Vector3 closestPoint = palletCollider.ClosestPoint(sensorCenter);
        float distance = Vector3.Distance(closestPoint, sensorCenter);
        float tolerance = Mathf.Max(0.02f, Mathf.Min(sensorBounds.extents.x, sensorBounds.extents.z) * 0.25f);
        return distance <= tolerance;
    }

    private void UpdateWheelVisuals(float deltaTime)
    {
        float forwardSpeed = body != null ? Vector3.Dot(Vector3.ProjectOnPlane(body.velocity, Vector3.up), GetMovementDirection()) : 0f;
        wheelSpinAngle += forwardSpeed * wheelSpinMultiplier * deltaTime;
        wheelSpinAngle = Mathf.Repeat(wheelSpinAngle, 360f);
        Quaternion spinRotation = Quaternion.AngleAxis(wheelSpinAngle, wheelSpinAxis.sqrMagnitude > 0.0001f ? wheelSpinAxis.normalized : Vector3.right);

        float directionMultiplier = invertRearWheelVisualDirection ? -1f : 1f;
        float targetAngle = playerDriving ? steeringInput * rearWheelVisualAngle * directionMultiplier : 0f;
        rearWheelCurrentAngle = Mathf.MoveTowards(rearWheelCurrentAngle, targetAngle, rearWheelVisualResponse * rearWheelVisualAngle * deltaTime);
        Quaternion steeringRotation = Quaternion.Euler(0f, rearWheelCurrentAngle, 0f);

        if (frontLeftWheel != null)
        {
            frontLeftWheel.localRotation = frontLeftWheelBaseRotation * spinRotation;
        }

        if (frontRightWheel != null)
        {
            frontRightWheel.localRotation = frontRightWheelBaseRotation * spinRotation;
        }

        if (rearLeftWheel != null)
        {
            rearLeftWheel.localRotation = rearLeftWheelBaseRotation * steeringRotation * spinRotation;
        }

        if (rearRightWheel != null)
        {
            rearRightWheel.localRotation = rearRightWheelBaseRotation * steeringRotation * spinRotation;
        }
    }

    private void UpdateFork(float deltaTime)
    {
        if (forkLiftTransform != null)
        {
            forkCurrentLocalY = forkLiftTransform.localPosition.y;
        }

        if (!playerDriving || forkLiftTransform == null)
        {
            SetForkAudioMoving(false);
            return;
        }

        float startY = forkLiftTransform.localPosition.y;
        float targetY = forkLiftTransform.localPosition.y;
        if (inputForkLift)
        {
            targetY += forkLiftSpeed * deltaTime;
        }
        else if (inputForkLower)
        {
            targetY -= forkLowerSpeed * deltaTime;
        }
        else
        {
            SetForkAudioMoving(false);
            return;
        }

        Vector3 localPosition = forkLiftTransform.localPosition;
        localPosition.y = Mathf.Clamp(targetY, forkMinLocalY, forkMaxLocalY);
        forkLiftTransform.localPosition = localPosition;
        forkCurrentLocalY = localPosition.y;
        SetForkAudioMoving(Mathf.Abs(localPosition.y - startY) > 0.00001f);
    }

    public void ApplyAudioVolumeSettings(float volume, bool muted)
    {
        currentSfxVolume = Mathf.Clamp01(volume);
        currentSfxMuted = muted;
        ApplyAudioVolume(engineAudioSource, currentSfxVolume * engineVolumeMultiplier, currentSfxMuted);
        ApplyAudioVolume(forkAudioSource, currentSfxVolume, currentSfxMuted);
    }

    private void PrepareAudioSources()
    {
        ConfigureAudioSource(engineAudioSource);
        ConfigureAudioSource(forkAudioSource);

        AudioManager manager = AudioManager.Instance;
        float volume = manager != null ? manager.MasterVolume * manager.SfxVolume : 1f;
        bool muted = manager != null && (manager.IsSfxMuted || manager.IsAllAudioDisabledForTesting);
        ApplyAudioVolumeSettings(volume, muted);
    }

    private void PlayEngineStartup()
    {
        PrepareAudioSources();
        if (engineStartupRoutine != null)
        {
            StopCoroutine(engineStartupRoutine);
        }

        if (engineAudioSource == null)
        {
            return;
        }

        engineAudioSource.Stop();
        engineAudioSource.pitch = engineIdlePitch;
        engineAudioSource.loop = false;
        if (engineStartupClip == null)
        {
            StartContinuousEngine();
            return;
        }

        engineAudioSource.clip = engineStartupClip;
        engineAudioSource.Play();
        engineStartupRoutine = StartCoroutine(WaitForEngineStartup());
    }

    private IEnumerator WaitForEngineStartup()
    {
        while (playerDriving && engineAudioSource != null && engineAudioSource.isPlaying)
        {
            yield return null;
        }

        engineStartupRoutine = null;
        if (playerDriving)
        {
            StartContinuousEngine();
        }
    }

    private void StartContinuousEngine()
    {
        if (engineAudioSource == null || engineContinuousClip == null)
        {
            return;
        }

        engineAudioSource.Stop();
        engineAudioSource.clip = engineContinuousClip;
        engineAudioSource.loop = true;
        engineAudioSource.pitch = engineIdlePitch;
        engineAudioSource.Play();
    }

    private void UpdateEnginePitch(float deltaTime)
    {
        if (engineAudioSource == null || !engineAudioSource.isPlaying ||
            engineAudioSource.clip != engineContinuousClip)
        {
            return;
        }

        float targetPitch = Mathf.Abs(throttleInput) > 0.01f
            ? engineAcceleratedPitch
            : engineIdlePitch;
        float pitchRange = Mathf.Max(0.01f, engineAcceleratedPitch - engineIdlePitch);
        float maxDelta = pitchRange / Mathf.Max(0.05f, enginePitchTransitionDuration) * deltaTime;
        engineAudioSource.pitch = Mathf.MoveTowards(engineAudioSource.pitch, targetPitch, maxDelta);
    }

    private void SetForkAudioMoving(bool moving)
    {
        if (moving == forkAudioMoving)
        {
            return;
        }

        forkAudioMoving = moving;
        if (forkAudioSource == null)
        {
            return;
        }

        forkAudioSource.Stop();
        forkAudioSource.pitch = 1f;
        if (moving)
        {
            if (forkMovementClip == null)
            {
                return;
            }

            forkAudioSource.clip = forkMovementClip;
            forkAudioSource.loop = true;
            forkAudioSource.Play();
            return;
        }

        if (forkStopClip != null)
        {
            forkAudioSource.clip = forkStopClip;
            forkAudioSource.loop = false;
            forkAudioSource.Play();
        }
    }

    private void StopVehicleAudio(bool playForkStop)
    {
        if (engineStartupRoutine != null)
        {
            StopCoroutine(engineStartupRoutine);
            engineStartupRoutine = null;
        }

        if (engineAudioSource != null)
        {
            engineAudioSource.Stop();
            engineAudioSource.loop = false;
            engineAudioSource.pitch = engineIdlePitch;
        }

        if (forkAudioMoving && playForkStop)
        {
            SetForkAudioMoving(false);
        }
        else
        {
            forkAudioMoving = false;
            if (forkAudioSource != null)
            {
                forkAudioSource.Stop();
                forkAudioSource.loop = false;
            }
        }
    }

    private void OnDisable()
    {
        StopVehicleAudio(false);
    }

    private static void ConfigureAudioSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.spatialBlend = 0f;
    }

    private static void ApplyAudioVolume(AudioSource source, float volume, bool muted)
    {
        if (source == null)
        {
            return;
        }

        source.volume = Mathf.Clamp01(volume);
        source.mute = muted;
    }

    private Vector3 GetMovementDirection()
    {
        Vector3 direction = transform.TransformDirection(localMovementDirection.normalized);
        direction = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (direction.sqrMagnitude <= 0.0001f)
        {
            return transform.forward;
        }

        return direction.normalized;
    }

    private void ApplyLateralGrip(Vector3 forwardDirection)
    {
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.velocity, Vector3.up);
        float forwardSpeed = Vector3.Dot(horizontalVelocity, forwardDirection);
        Vector3 lateralVelocity = horizontalVelocity - forwardDirection * forwardSpeed;
        body.AddForce(-lateralVelocity * lateralGrip, ForceMode.Acceleration);
    }

    private void ApplyYawDamping(float deltaTime)
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        Vector3 angularVelocity = body.angularVelocity;
        angularVelocity.x = 0f;
        angularVelocity.z = 0f;

        if (Mathf.Approximately(steeringInput, 0f) || Mathf.Approximately(throttleInput, 0f))
        {
            angularVelocity.y = Mathf.MoveTowards(angularVelocity.y, 0f, yawDamping * deltaTime);
        }

        angularVelocity.y = Mathf.Clamp(angularVelocity.y, -maxYawSpeed, maxYawSpeed);
        body.angularVelocity = angularVelocity;
    }

    private void SetPlayerCollisionIgnored(bool ignored)
    {
        if (currentPlayer == null)
        {
            return;
        }

        if (forkliftColliders == null || forkliftColliders.Length == 0)
        {
            forkliftColliders = GetComponentsInChildren<Collider>(true);
        }

        currentPlayerColliders = currentPlayer.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < forkliftColliders.Length; i++)
        {
            Collider forkliftCollider = forkliftColliders[i];
            if (forkliftCollider == null || forkliftCollider.isTrigger)
            {
                continue;
            }

            if (forkliftCollider.transform.IsChildOf(currentPlayer.transform))
            {
                continue;
            }

            for (int j = 0; j < currentPlayerColliders.Length; j++)
            {
                Collider playerCollider = currentPlayerColliders[j];
                if (playerCollider == null || playerCollider.isTrigger)
                {
                    continue;
                }

                Physics.IgnoreCollision(forkliftCollider, playerCollider, ignored);
            }
        }
    }

    private void EnsurePrompt()
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

        RuntimeEventSystemUtility.EnsureSingleEventSystem();

        GameObject legacyPrompt = promptObject;
        promptPresenter = InteractionPromptPresenter.GetOrCreate(canvas);
        promptObject = promptPresenter != null ? promptPresenter.gameObject : null;
        promptLabel = null;
        if (legacyPrompt != null && legacyPrompt != promptObject && legacyPrompt.name == "EmpilhadeiraPrompt")
        {
            legacyPrompt.SetActive(false);
            Destroy(legacyPrompt);
        }

        Transform legacyCanvasPrompt = canvas.transform.Find("EmpilhadeiraPrompt");
        if (legacyCanvasPrompt != null && legacyCanvasPrompt.gameObject != promptObject)
        {
            legacyCanvasPrompt.gameObject.SetActive(false);
            Destroy(legacyCanvasPrompt.gameObject);
        }

        EnsureDrivingPanel();
    }

    private void EnsureDrivingPanel()
    {
        if (canvas == null)
        {
            return;
        }

        if (drivingPanelObject == null)
        {
            Transform existingPanel = canvas.transform.Find("EmpilhadeiraDrivingPanel");
            if (existingPanel != null)
            {
                drivingPanelObject = existingPanel.gameObject;
                drivingPanelLabel = existingPanel.GetComponentInChildren<Text>(true);
            }
        }

        bool createdPanel = false;
        if (drivingPanelObject == null)
        {
            drivingPanelObject = new GameObject("EmpilhadeiraDrivingPanel");
            drivingPanelObject.transform.SetParent(canvas.transform, false);

            RectTransform panelRect = drivingPanelObject.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 1f);
            panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -96f);
            panelRect.sizeDelta = new Vector2(260f, 230f);

            Image background = drivingPanelObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.6f);

            GameObject labelObject = new GameObject("Text");
            labelObject.transform.SetParent(drivingPanelObject.transform, false);
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 8f);
            labelRect.offsetMax = new Vector2(-12f, -8f);
            drivingPanelLabel = labelObject.AddComponent<Text>();
            createdPanel = true;
        }

        EnsureDrivingPanelChildren(createdPanel);

        RectTransform drivingRect = drivingPanelObject.GetComponent<RectTransform>();
        if (drivingRect != null)
        {
            drivingRect.anchorMin = new Vector2(1f, 1f);
            drivingRect.anchorMax = new Vector2(1f, 1f);
            drivingRect.pivot = new Vector2(1f, 1f);
            drivingRect.anchoredPosition = new Vector2(-24f, -96f);
        }

        if (drivingPanelLabel != null && createdPanel)
        {
            drivingPanelLabel.alignment = TextAnchor.UpperLeft;
            drivingPanelLabel.color = Color.white;
            drivingPanelLabel.font = GetDefaultFont();
            drivingPanelLabel.fontSize = 15;
            drivingPanelLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            drivingPanelLabel.verticalOverflow = VerticalWrapMode.Truncate;
        }
    }

    private void EnsureDrivingPanelChildren(bool applyDefaultLayout)
    {
        if (drivingPanelObject == null)
        {
            return;
        }

        ResolveForkliftIconSprite();
        ConfigureForkliftIcon(drivingPanelObject.transform, forkliftIconSprite, applyDefaultLayout);

        if (drivingPanelLabel != null && applyDefaultLayout)
        {
            RectTransform labelRect = drivingPanelLabel.GetComponent<RectTransform>();
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 8f);
                labelRect.offsetMax = new Vector2(-12f, -96f);
            }
        }
    }

    private static void ConfigureForkliftIcon(Transform panel, Sprite sprite, bool applyDefaultLayout)
    {
        Transform iconTransform = panel.Find("ForkliftIcon");
        bool createdIcon = false;
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("ForkliftIcon", typeof(RectTransform));
            iconObject.transform.SetParent(panel, false);
            iconTransform = iconObject.transform;
            createdIcon = true;
        }

        RectTransform iconRect = iconTransform.GetComponent<RectTransform>();
        if (iconRect == null)
        {
            iconRect = iconTransform.gameObject.AddComponent<RectTransform>();
            iconTransform = iconRect.transform;
            createdIcon = true;
        }

        if (applyDefaultLayout || createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -10f);
            iconRect.sizeDelta = new Vector2(96f, 72f);
        }

        Image icon = iconTransform.GetComponent<Image>();
        if (icon == null)
        {
            icon = iconTransform.gameObject.AddComponent<Image>();
        }

        if (sprite != null && icon.sprite == null)
        {
            icon.sprite = sprite;
        }

        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    private void ResolveForkliftIconSprite()
    {
#if UNITY_EDITOR
        if (forkliftIconSprite == null)
        {
            forkliftIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ForkliftIconSpritePath);
        }
#endif
    }

    private void UpdateDrivingPanel()
    {
        if (!playerDriving)
        {
            return;
        }

        EnsurePrompt();
        if (drivingPanelLabel == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(drivingPanelLabel.text))
        {
            drivingPanelLabel.text =
                "EMPILHADEIRA\n\n" +
                "W / S - Frente e re\n" +
                "A / D - Direcao\n" +
                "1 - Baixar garfos\n" +
                "2 - Levantar garfos\n" +
                "E - Sair";
        }
    }

    private Font GetDefaultFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

        return null;
    }

    private void SetPromptText(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            enterPromptText = text;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        EnsurePrompt();
        if (visible)
        {
            promptPresenter?.ShowAmbient(this, "EMPILHADEIRA", new InteractionPromptAction(GetInteractionKeyLabel(), "Entrar"));
        }
        else
        {
            promptPresenter?.Hide(this);
        }
    }

    private string GetInteractionKeyLabel()
    {
        string value = interactionKey.ToString();
        return value.StartsWith("Alpha") ? value.Substring("Alpha".Length) : value.ToUpperInvariant();
    }

    private void SetDrivingPanelVisible(bool visible)
    {
        EnsurePrompt();
        if (drivingPanelObject != null)
        {
            drivingPanelObject.SetActive(visible);
        }
    }
}
