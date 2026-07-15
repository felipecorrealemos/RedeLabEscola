using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ConveyorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ConveyorPath conveyorPath;
    [SerializeField] private ConveyorItemSpawner itemSpawner;
    [SerializeField] private ConveyorJamSensor jamSensor;
    [SerializeField] private ConveyorCollectionZone collectionZone;

    [Header("Speed")]
    [SerializeField] private float conveyorSpeed = 1f;
    [SerializeField] private float acceleration = 2f;
    [SerializeField] private float deceleration = 3f;
    [SerializeField] private bool useSmoothSpeedTransition = true;
    [SerializeField] private bool startConveyorOnPlay = true;

    [Header("Item Spacing")]
    [SerializeField] private float minimumItemSpacing = 0.65f;
    [SerializeField] private float forwardDetectionDistance = 1.25f;
    [SerializeField] private float forwardDetectionRadius = 0.7f;
    [SerializeField] private LayerMask conveyorItemLayer = ~0;
    [SerializeField] private float itemSpeedAdjustment = 6f;
    [SerializeField] private float lateralBlockingTolerance = 0.42f;
    [SerializeField] private float itemApproximateWidth = 0.45f;
    [SerializeField] private float longitudinalSafetyDistance = 0.65f;

    [Header("Lateral Position")]
    [SerializeField] private float leftLaneOffset = -0.3f;
    [SerializeField] private float centerLaneOffset = 0f;
    [SerializeField] private float rightLaneOffset = 0.3f;
    [SerializeField] private float lateralTransitionSpeed = 2.5f;
    [SerializeField] private bool randomizeLateralPosition = true;
    [SerializeField] private bool avoidRepeatingSameLane = true;
    [SerializeField] private float lateralNoise = 0.04f;
    [SerializeField] private float maximumLateralOffset = 0.45f;

    [Header("Optional Visual Signals")]
    [SerializeField] private Light runningGreenLight;
    [SerializeField] private Light congestedYellowLight;
    [SerializeField] private Light stoppedRedLight;
    [SerializeField] private Renderer runningGreenRenderer;
    [SerializeField] private Renderer congestedYellowRenderer;
    [SerializeField] private Renderer stoppedRedRenderer;
    [SerializeField] private AudioSource alarmAudioSource;

    [Header("Events")]
    [SerializeField] private UnityEvent onConveyorStarted;
    [SerializeField] private UnityEvent onConveyorStopped;
    [SerializeField] private UnityEvent onJamDetected;
    [SerializeField] private UnityEvent onJamCleared;
    [SerializeField] private ConveyorItemGameObjectEvent onItemSpawned;
    [SerializeField] private ConveyorItemGameObjectEvent onItemReachedCollectionPoint;
    [SerializeField] private ConveyorItemGameObjectEvent onItemCollected;

    [Header("Runtime Debug")]
    [SerializeField] private ConveyorState currentState = ConveyorState.Stopped;
    [SerializeField] private float currentSpeed;
    [SerializeField] private int activeItemCount;
    [SerializeField] private int jamSensorItemCount;
    [SerializeField] private float currentCongestionTime;
    [SerializeField] private bool hasAvailableCollectionItem;

    private readonly List<ConveyorItem> activeItems = new List<ConveyorItem>();
    private int lastLaneIndex = -1;
    private float restartTimer;
    private bool jamMessageLogged;
    private bool movementRequested;

    public ConveyorPath ConveyorPath => conveyorPath;
    public ConveyorCollectionZone CollectionZone => collectionZone;
    public IReadOnlyList<ConveyorItem> ActiveItems => activeItems;
    public ConveyorState CurrentState => currentState;
    public float CurrentSpeed => currentSpeed;
    public float TargetSpeed => movementRequested && (currentState != ConveyorState.Jammed || (jamSensor != null && !jamSensor.StopConveyorOnJam)) ? Mathf.Max(0f, conveyorSpeed) : 0f;
    public float MinimumItemSpacing => Mathf.Max(0.01f, minimumItemSpacing);
    public float ForwardDetectionDistance => Mathf.Max(MinimumItemSpacing, forwardDetectionDistance);
    public float ForwardDetectionRadius => Mathf.Max(0.01f, forwardDetectionRadius);
    public LayerMask ConveyorItemLayer => conveyorItemLayer;
    public float ItemSpeedAdjustment => Mathf.Max(0.01f, itemSpeedAdjustment);
    public float LateralTransitionSpeed => Mathf.Max(0.01f, lateralTransitionSpeed);
    public float CollectionQueueApproachHoldDistance => collectionZone != null ? collectionZone.QueueItemSpacing : MinimumItemSpacing;
    public float CollectionQueueAssignmentDistance => collectionZone != null ? collectionZone.QueueAssignmentDistance : MinimumItemSpacing;
    public bool IsJammed => currentState == ConveyorState.Jammed;
    public bool CanSpawn => movementRequested && currentState != ConveyorState.Jammed;

    public void Configure(ConveyorPath path, ConveyorItemSpawner spawner, ConveyorJamSensor sensor, ConveyorCollectionZone zone)
    {
        conveyorPath = path;
        itemSpawner = spawner;
        jamSensor = sensor;
        collectionZone = zone;
    }

    private void Awake()
    {
        ResolveReferences();
        movementRequested = startConveyorOnPlay;
        currentState = startConveyorOnPlay ? ConveyorState.Starting : ConveyorState.Stopped;
        UpdateSignals();
    }

    private void OnValidate()
    {
        conveyorSpeed = Mathf.Max(0f, conveyorSpeed);
        acceleration = Mathf.Max(0.01f, acceleration);
        deceleration = Mathf.Max(0.01f, deceleration);
        minimumItemSpacing = Mathf.Max(0.01f, minimumItemSpacing);
        forwardDetectionDistance = Mathf.Max(minimumItemSpacing, forwardDetectionDistance);
        forwardDetectionRadius = Mathf.Max(0.01f, forwardDetectionRadius);
        itemSpeedAdjustment = Mathf.Max(0.01f, itemSpeedAdjustment);
        lateralBlockingTolerance = Mathf.Max(0.01f, lateralBlockingTolerance);
        itemApproximateWidth = Mathf.Max(0.01f, itemApproximateWidth);
        longitudinalSafetyDistance = Mathf.Max(0.01f, longitudinalSafetyDistance);
        lateralTransitionSpeed = Mathf.Max(0.01f, lateralTransitionSpeed);
        maximumLateralOffset = Mathf.Max(0f, maximumLateralOffset);
    }

    private void Update()
    {
        CleanupNullItems();
        UpdateSpeed();

        for (int i = 0; i < activeItems.Count; i++)
        {
            ConveyorItem item = activeItems[i];
            if (item != null)
            {
                item.TickItem(Time.deltaTime);
            }
        }

        UpdateJamState(Time.deltaTime);
        UpdateDebugInfo();
        UpdateSignals();
    }

    public void StartConveyor()
    {
        movementRequested = true;
        if (currentState == ConveyorState.Stopped)
        {
            currentState = ConveyorState.Starting;
        }

        onConveyorStarted?.Invoke();
    }

    public void StopConveyor()
    {
        movementRequested = false;
        if (currentState != ConveyorState.Jammed)
        {
            currentState = ConveyorState.Stopped;
        }

        onConveyorStopped?.Invoke();
    }

    public void RegisterItem(ConveyorItem item)
    {
        if (item == null || activeItems.Contains(item))
        {
            return;
        }

        activeItems.Add(item);
        activeItemCount = activeItems.Count;
        onItemSpawned?.Invoke(item.gameObject);
    }

    public void UnregisterItem(ConveyorItem item)
    {
        if (item == null)
        {
            return;
        }

        activeItems.Remove(item);
        collectionZone?.UnregisterItem(item);
        activeItemCount = activeItems.Count;
    }

    public float GetDistanceToNearestBlockingItem(ConveyorItem source)
    {
        if (source == null)
        {
            return float.PositiveInfinity;
        }

        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < activeItems.Count; i++)
        {
            ConveyorItem candidate = activeItems[i];
            if (candidate == null || candidate == source || candidate.CurrentState == ConveyorItemState.Removed || candidate.CurrentState == ConveyorItemState.BeingCollected)
            {
                continue;
            }

            if (collectionZone != null
                && collectionZone.UseDualQueue
                && candidate.IsAssignedToCollectionQueue
                && !source.IsAssignedToCollectionQueue
                && collectionZone.CanAcceptItem())
            {
                continue;
            }

            if (collectionZone != null
                && collectionZone.UseDualQueue
                && source.IsAssignedToCollectionQueue
                && candidate.IsAssignedToCollectionQueue
                && candidate.CollectionQueueIndex != source.CollectionQueueIndex)
            {
                continue;
            }

            if (collectionZone != null
                && collectionZone.UseDualQueue
                && source.IsAssignedToCollectionQueue
                && candidate.IsAssignedToCollectionQueue
                && candidate.CollectionQueueIndex == source.CollectionQueueIndex)
            {
                continue;
            }

            float aheadDistance = candidate.ProgressDistance - source.ProgressDistance;
            if (aheadDistance <= 0f || aheadDistance > ForwardDetectionDistance)
            {
                continue;
            }

            float lateralDistance = Mathf.Abs(GetBlockingLateralPosition(candidate) - GetBlockingLateralPosition(source));
            float blockingTolerance = Mathf.Min(ForwardDetectionRadius, Mathf.Max(lateralBlockingTolerance, itemApproximateWidth * 0.75f));
            if (aheadDistance <= longitudinalSafetyDistance && lateralDistance <= blockingTolerance && aheadDistance < nearestDistance)
            {
                nearestDistance = aheadDistance;
            }
            else if (lateralDistance <= blockingTolerance && aheadDistance < nearestDistance)
            {
                nearestDistance = aheadDistance;
            }
        }

        return nearestDistance;
    }

    public bool IsSpawnBlocked(float spawnDistance)
    {
        for (int i = 0; i < activeItems.Count; i++)
        {
            ConveyorItem item = activeItems[i];
            if (item == null || item.CurrentState == ConveyorItemState.Removed || item.CurrentState == ConveyorItemState.BeingCollected)
            {
                continue;
            }

            if (Mathf.Abs(item.ProgressDistance - spawnDistance) <= MinimumItemSpacing)
            {
                return true;
            }
        }

        return false;
    }

    public float GetNextLateralOffset()
    {
        float[] offsets = { leftLaneOffset, centerLaneOffset, rightLaneOffset };
        int laneIndex;
        if (randomizeLateralPosition)
        {
            laneIndex = Random.Range(0, offsets.Length);
            if (avoidRepeatingSameLane && offsets.Length > 1 && laneIndex == lastLaneIndex)
            {
                laneIndex = (laneIndex + Random.Range(1, offsets.Length)) % offsets.Length;
            }
        }
        else
        {
            laneIndex = (lastLaneIndex + 1) % offsets.Length;
        }

        lastLaneIndex = laneIndex;
        float offset = offsets[laneIndex] + Random.Range(-Mathf.Abs(lateralNoise), Mathf.Abs(lateralNoise));
        return Mathf.Clamp(offset, -maximumLateralOffset, maximumLateralOffset);
    }

    public void NotifyItemReachedCollectionPoint(ConveyorItem item)
    {
        if (item == null)
        {
            return;
        }

        onItemReachedCollectionPoint?.Invoke(item.gameObject);
    }

    public bool TryQueueItemForCollection(ConveyorItem item)
    {
        if (item == null || collectionZone == null)
        {
            return false;
        }

        return collectionZone.TryRegisterItem(item, conveyorPath);
    }

    public void MoveQueuedItemToCollectionSlot(ConveyorItem item, Rigidbody itemRigidbody, float deltaTime)
    {
        collectionZone?.MoveQueuedItemToSlot(item, itemRigidbody, deltaTime);
    }

    public float GetCollectionSlotPathDistance(ConveyorItem item)
    {
        if (item == null || conveyorPath == null || collectionZone == null || !item.IsAssignedToCollectionQueue)
        {
            return conveyorPath != null ? conveyorPath.TotalLength : 0f;
        }

        return collectionZone.GetSlotPathDistance(conveyorPath, item.CollectionQueueSlotIndex);
    }

    public void NotifyItemCollected(ConveyorItem item)
    {
        if (item == null)
        {
            return;
        }

        item.MarkRemoved();
        collectionZone?.UnregisterItem(item);
        activeItems.Remove(item);
        onItemCollected?.Invoke(item.gameObject);
        Destroy(item.gameObject);
    }

    public GameObject GetAvailableItem()
    {
        return collectionZone != null ? collectionZone.GetAvailableItem() : null;
    }

    public bool ReserveItem(GameObject item)
    {
        return collectionZone != null && collectionZone.ReserveItem(item);
    }

    public void ReleaseReservation(GameObject item)
    {
        collectionZone?.ReleaseReservation(item);
    }

    public void RemoveItem(GameObject item)
    {
        collectionZone?.RemoveItem(item);
    }

    public void NotifyItemCollected(GameObject item)
    {
        collectionZone?.NotifyItemCollected(item);
    }

    private void ResolveReferences()
    {
        if (conveyorPath == null)
        {
            conveyorPath = GetComponentInChildren<ConveyorPath>();
        }

        if (itemSpawner == null)
        {
            itemSpawner = GetComponentInChildren<ConveyorItemSpawner>();
        }

        if (jamSensor == null)
        {
            jamSensor = GetComponentInChildren<ConveyorJamSensor>();
        }

        if (collectionZone == null)
        {
            collectionZone = GetComponentInChildren<ConveyorCollectionZone>();
        }

        collectionZone?.SetPath(conveyorPath);
    }

    private float GetBlockingLateralPosition(ConveyorItem item)
    {
        if (item == null)
        {
            return 0f;
        }

        if (collectionZone != null && collectionZone.UseDualQueue && item.IsAssignedToCollectionQueue)
        {
            return collectionZone.GetQueueOffset(item.CollectionQueueIndex);
        }

        return item.LateralOffset;
    }

    private void UpdateSpeed()
    {
        float targetSpeed = TargetSpeed;
        if (useSmoothSpeedTransition)
        {
            float rate = targetSpeed > currentSpeed ? acceleration : deceleration;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);
        }
        else
        {
            currentSpeed = targetSpeed;
        }

        if (currentState == ConveyorState.Starting && currentSpeed > 0.001f)
        {
            currentState = ConveyorState.Running;
        }
        else if (currentState == ConveyorState.Restarting && currentSpeed > 0.001f)
        {
            currentState = ConveyorState.Running;
        }
        else if (!movementRequested && currentState != ConveyorState.Jammed)
        {
            currentState = currentSpeed > 0.001f ? ConveyorState.Stopped : ConveyorState.Stopped;
        }
    }

    private void UpdateJamState(float deltaTime)
    {
        if (jamSensor == null)
        {
            return;
        }

        jamSensorItemCount = jamSensor.CountItems(activeItems, conveyorPath);

        if (currentState == ConveyorState.Jammed)
        {
            if (!jamSensor.RestartAutomatically || jamSensorItemCount > jamSensor.RestartItemThreshold)
            {
                restartTimer = 0f;
                return;
            }

            restartTimer += deltaTime;
            if (restartTimer >= jamSensor.RestartDelay)
            {
                ClearJamAndRestart();
            }

            return;
        }

        if (jamSensorItemCount >= jamSensor.JamItemThreshold)
        {
            currentCongestionTime += deltaTime;
            jamSensor.SetCongestionTime(currentCongestionTime);

            if (currentCongestionTime >= jamSensor.JamDetectionTime)
            {
                ConfirmJam();
            }
            else if (currentState == ConveyorState.Running)
            {
                currentState = ConveyorState.Congested;
            }
        }
        else
        {
            currentCongestionTime = 0f;
            jamSensor.SetCongestionTime(0f);
            if (currentState == ConveyorState.Congested)
            {
                currentState = movementRequested ? ConveyorState.Running : ConveyorState.Stopped;
            }
        }
    }

    private void ConfirmJam()
    {
        if (currentState == ConveyorState.Jammed)
        {
            return;
        }

        currentState = ConveyorState.Jammed;
        restartTimer = 0f;

        if (jamSensor.StopConveyorOnJam)
        {
            movementRequested = false;
        }

        if (jamSensor.StopSpawnerOnJam && itemSpawner != null)
        {
            itemSpawner.SetPausedByJam(true);
        }

        if (!jamMessageLogged)
        {
            Debug.Log($"Conveyor jam detected on {name}. Items in sensor: {jamSensorItemCount}.", this);
            jamMessageLogged = true;
        }

        onJamDetected?.Invoke();
        if (jamSensor.StopConveyorOnJam)
        {
            onConveyorStopped?.Invoke();
        }
    }

    private void ClearJamAndRestart()
    {
        currentCongestionTime = 0f;
        restartTimer = 0f;
        jamMessageLogged = false;
        jamSensor.SetCongestionTime(0f);
        itemSpawner?.SetPausedByJam(false);
        movementRequested = true;
        currentState = ConveyorState.Restarting;
        onJamCleared?.Invoke();
        if (jamSensor.StopConveyorOnJam)
        {
            onConveyorStarted?.Invoke();
        }
    }

    private void CleanupNullItems()
    {
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            if (activeItems[i] == null)
            {
                activeItems.RemoveAt(i);
            }
        }
    }

    private void UpdateDebugInfo()
    {
        activeItemCount = activeItems.Count;
        hasAvailableCollectionItem = collectionZone != null && collectionZone.GetAvailableItem() != null;
    }

    private void UpdateSignals()
    {
        bool running = currentState == ConveyorState.Running || currentState == ConveyorState.Starting || currentState == ConveyorState.Restarting;
        bool congested = currentState == ConveyorState.Congested;
        bool stopped = currentState == ConveyorState.Stopped || currentState == ConveyorState.Jammed;

        SetLight(runningGreenLight, running);
        SetLight(congestedYellowLight, congested);
        SetLight(stoppedRedLight, stopped);
        SetRenderer(runningGreenRenderer, running);
        SetRenderer(congestedYellowRenderer, congested);
        SetRenderer(stoppedRedRenderer, stopped);

        if (alarmAudioSource != null)
        {
            if (currentState == ConveyorState.Jammed && !alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Play();
            }
            else if (currentState != ConveyorState.Jammed && alarmAudioSource.isPlaying)
            {
                alarmAudioSource.Stop();
            }
        }
    }

    private void SetLight(Light targetLight, bool enabled)
    {
        if (targetLight != null)
        {
            targetLight.enabled = enabled;
        }
    }

    private void SetRenderer(Renderer targetRenderer, bool enabled)
    {
        if (targetRenderer != null)
        {
            targetRenderer.enabled = enabled;
        }
    }
}
