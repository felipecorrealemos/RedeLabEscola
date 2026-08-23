using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PackagingMachineController : MonoBehaviour
{
    [Header("Debug Box Output Shortcut")]
    [SerializeField] private bool enableDebugBoxGeneration;
    [SerializeField, Min(0.1f)] private float debugBoxInterval = 3f;

    [Header("Input")]
    [SerializeField] private string acceptedProductId = "ProcessedPart";
    [SerializeField, Min(1)] private int requiredInputItems = 1;
    [SerializeField] private int storedInputItems;
    [SerializeField] private string inputProgress = "0 / 1";
    [SerializeField, Min(1)] private int maximumStoredInputItems = 6;

    [Header("Packaging")]
    [SerializeField, Min(0.05f)] private float packagingTime = 2f;
    [SerializeField] private GameObject packedBoxPrefab;
    [SerializeField] private Transform outputSpawnPoint;
    [SerializeField] private ConveyorController outputConveyor;
    [SerializeField] private string packedBoxProductId = "PackedBox";
    [SerializeField] private bool autoStartPackaging = true;
    [SerializeField] private bool usePlaceholderWhenPrefabMissing = true;
    [SerializeField] private bool preservePrefabScale;
    [SerializeField] private Vector3 packedBoxScale = Vector3.one;

    [Header("Output Jam Sensor")]
    [SerializeField] private ConveyorJamSensor outputJamSensor;
    [SerializeField] private bool stopPackagingWhenOutputJammed = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onInputReceived;
    [SerializeField] private UnityEvent onPackagingStarted;
    [SerializeField] private UnityEvent onBoxCreated;
    [SerializeField] private UnityEvent onOutputBlocked;

    [Header("Runtime Debug")]
    [SerializeField] private ProcessingMachineState currentState = ProcessingMachineState.PoweredOff;
    [SerializeField] private int pendingPackedBoxes;
    [SerializeField] private float currentPackagingTimer;
    [SerializeField] private int stoppedItemsInOutputJamSensor;
    [SerializeField] private bool outputJamBlocked;
    [SerializeField] private float debugBoxTimer;
    [SerializeField] private int debugGeneratedBoxes;

    private Coroutine packagingRoutine;
    private bool placeholderWarningLogged;
    private bool missingPrefabWarningLogged;
    private float outputRestartTimer;
    private bool attemptedOutputReferenceResolve;

    public ProcessingMachineState CurrentState => currentState;
    public string AcceptedProductId => acceptedProductId;
    public int StoredInputItems => storedInputItems;
    public int PendingPackedBoxes => pendingPackedBoxes;
    public bool AcceptsInput => storedInputItems < maximumStoredInputItems;
    public int RequiredInputItems => Mathf.Max(1, requiredInputItems);
    public int MaximumStoredInputItems => Mathf.Max(RequiredInputItems, maximumStoredInputItems);

    public void Configure(ConveyorController conveyor, Transform spawnPoint, ConveyorJamSensor jamSensor, GameObject boxPrefab)
    {
        outputConveyor = conveyor;
        outputSpawnPoint = spawnPoint;
        outputJamSensor = jamSensor;
        if (packedBoxPrefab == null)
        {
            packedBoxPrefab = boxPrefab;
        }
    }

    public void ConfigureInput(string productId, int requiredItems, int maximumStoredItems)
    {
        acceptedProductId = productId;
        requiredInputItems = Mathf.Max(1, requiredItems);
        maximumStoredInputItems = Mathf.Max(requiredInputItems, maximumStoredItems);
        RefreshInputProgress();
    }

    public void ConfigureInputDefaults(string productId, int requiredItems, int maximumStoredItems)
    {
        if (string.IsNullOrWhiteSpace(acceptedProductId) || acceptedProductId == "ProcessedPart")
        {
            acceptedProductId = productId;
        }

        requiredInputItems = Mathf.Max(1, requiredInputItems);
        maximumStoredInputItems = Mathf.Max(requiredInputItems, maximumStoredInputItems);

        if (requiredInputItems == 1)
        {
            requiredInputItems = Mathf.Max(1, requiredItems);
        }

        if (maximumStoredInputItems <= 6)
        {
            maximumStoredInputItems = Mathf.Max(requiredInputItems, maximumStoredItems);
        }

        RefreshInputProgress();
    }

    public void ConfigureOutputProduct(string productId, GameObject prefab)
    {
        if (!string.IsNullOrWhiteSpace(productId))
        {
            packedBoxProductId = productId;
        }

        if (packedBoxPrefab == null)
        {
            packedBoxPrefab = prefab;
        }
    }

    public void ConfigureOutputScale(bool preserveScale, Vector3 explicitScale)
    {
        preservePrefabScale = preserveScale;
        packedBoxScale = explicitScale;
    }

    private void Awake()
    {
        ResolveOutputReferences();
        RefreshInputProgress();
        currentState = ProcessingMachineState.WaitingForMaterials;
    }

    private void Update()
    {
        RefreshInputProgress();
        UpdateOutputJamState(Time.deltaTime);
        TryReleasePendingOutput();

        if (enableDebugBoxGeneration)
        {
            UpdateDebugBoxGeneration(Time.deltaTime);
        }
        else if (autoStartPackaging && packagingRoutine == null && pendingPackedBoxes == 0 && storedInputItems >= requiredInputItems)
        {
            if (IsOutputReadyForNewItem())
            {
                packagingRoutine = StartCoroutine(PackageItem());
            }
            else
            {
                ChangeState(ProcessingMachineState.OutputBlocked);
            }
        }
        else if (packagingRoutine == null && pendingPackedBoxes == 0 && currentState != ProcessingMachineState.OutputBlocked)
        {
            ChangeState(storedInputItems >= requiredInputItems ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
        }
    }

    private void OnValidate()
    {
        requiredInputItems = Mathf.Max(1, requiredInputItems);
        maximumStoredInputItems = Mathf.Max(requiredInputItems, maximumStoredInputItems);
        RefreshInputProgress();
    }

    private void UpdateDebugBoxGeneration(float deltaTime)
    {
        if (pendingPackedBoxes > 0)
        {
            return;
        }

        if (!IsOutputReadyForNewItem())
        {
            ChangeState(ProcessingMachineState.OutputBlocked);
            onOutputBlocked?.Invoke();
            return;
        }

        ChangeState(ProcessingMachineState.Processing);
        debugBoxTimer += deltaTime;
        currentPackagingTimer = debugBoxTimer;

        if (debugBoxTimer < debugBoxInterval)
        {
            return;
        }

        debugBoxTimer = 0f;
        currentPackagingTimer = 0f;
        pendingPackedBoxes++;
        debugGeneratedBoxes++;
        TryReleasePendingOutput();
    }

    public bool CanAcceptItem(ConveyorItem item)
    {
        if (item == null || !AcceptsInput)
        {
            return false;
        }

        if (item.CurrentState == ConveyorItemState.Removed || item.CurrentState == ConveyorItemState.BeingCollected)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(acceptedProductId)
            || string.Equals(item.ProductId, acceptedProductId, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool TryReceiveItem(ConveyorItem item)
    {
        if (!CanAcceptItem(item))
        {
            return false;
        }

        storedInputItems++;
        RefreshInputProgress();
        ConveyorController sourceController = item.CurrentController;
        if (sourceController != null)
        {
            sourceController.NotifyItemCollected(item);
        }
        else
        {
            item.MarkRemoved();
            Destroy(item.gameObject);
        }

        onInputReceived?.Invoke();
        ChangeState(storedInputItems >= requiredInputItems ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
        return true;
    }

    private IEnumerator PackageItem()
    {
        storedInputItems -= requiredInputItems;
        RefreshInputProgress();
        currentPackagingTimer = 0f;
        ChangeState(ProcessingMachineState.Processing);
        onPackagingStarted?.Invoke();

        while (currentPackagingTimer < packagingTime)
        {
            currentPackagingTimer += Time.deltaTime;
            yield return null;
        }

        currentPackagingTimer = 0f;
        pendingPackedBoxes++;
        packagingRoutine = null;
        TryReleasePendingOutput();
    }

    private void TryReleasePendingOutput()
    {
        if (pendingPackedBoxes <= 0)
        {
            if (currentState == ProcessingMachineState.OutputBlocked && !IsOutputJamActive())
            {
                ChangeState(storedInputItems >= requiredInputItems ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
            }

            return;
        }

        if (!IsOutputReadyForNewItem())
        {
            ChangeState(ProcessingMachineState.OutputBlocked);
            onOutputBlocked?.Invoke();
            return;
        }

        GameObject boxObject = CreateBoxObject();
        if (boxObject == null)
        {
            ChangeState(ProcessingMachineState.Error);
            return;
        }

        bool received = outputConveyor.TryReceiveItem(boxObject, packedBoxProductId, outputSpawnPoint, true, 0f);
        if (!received)
        {
            Destroy(boxObject);
            ChangeState(ProcessingMachineState.OutputBlocked);
            onOutputBlocked?.Invoke();
            return;
        }

        pendingPackedBoxes--;
        onBoxCreated?.Invoke();
        if (string.Equals(packedBoxProductId, "PalletWithBoxes", System.StringComparison.OrdinalIgnoreCase))
        {
            MissionManager.NotifyStage2MachinePalletSent(boxObject);
        }
        ChangeState(storedInputItems >= requiredInputItems ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
    }

    private GameObject CreateBoxObject()
    {
        Vector3 spawnPosition = outputSpawnPoint != null ? outputSpawnPoint.position : transform.position;
        Quaternion spawnRotation = outputSpawnPoint != null ? outputSpawnPoint.rotation : transform.rotation;

        if (packedBoxPrefab != null)
        {
            GameObject instance = Instantiate(packedBoxPrefab, spawnPosition, spawnRotation);
            if (!preservePrefabScale && packedBoxScale != Vector3.zero)
            {
                instance.transform.localScale = packedBoxScale;
            }

            instance.name = packedBoxProductId;
            return instance;
        }

        if (!usePlaceholderWhenPrefabMissing)
        {
            if (!missingPrefabWarningLogged)
            {
                Debug.LogWarning($"{name} cannot create packed box because packedBoxPrefab is empty.", this);
                missingPrefabWarningLogged = true;
            }

            return null;
        }

        if (!placeholderWarningLogged)
        {
            Debug.LogWarning($"{name} is using a temporary box placeholder because packedBoxPrefab is empty.", this);
            placeholderWarningLogged = true;
        }

        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        if (!preservePrefabScale)
        {
            placeholder.transform.localScale = packedBoxScale == Vector3.zero ? new Vector3(0.8f, 0.55f, 0.8f) : packedBoxScale;
        }

        placeholder.name = packedBoxProductId + "_Placeholder";
        return placeholder;
    }

    private bool IsOutputReadyForNewItem()
    {
        if (stopPackagingWhenOutputJammed && IsOutputJamActive())
        {
            return false;
        }

        return outputConveyor != null
            && outputSpawnPoint != null
            && outputConveyor.CanReceiveItemAt(outputSpawnPoint);
    }

    private void UpdateOutputJamState(float deltaTime)
    {
        ResolveOutputReferences();
        stoppedItemsInOutputJamSensor = outputJamSensor != null && outputConveyor != null
            ? outputJamSensor.CountItems(outputConveyor.ActiveItems, outputConveyor.ConveyorPath)
            : 0;

        if (outputConveyor != null && outputConveyor.IsJammed)
        {
            outputJamBlocked = true;
            ChangeState(ProcessingMachineState.OutputBlocked);
            return;
        }

        if (outputJamBlocked)
        {
            if (outputJamSensor == null || stoppedItemsInOutputJamSensor > outputJamSensor.RestartItemThreshold)
            {
                outputRestartTimer = 0f;
                ChangeState(ProcessingMachineState.OutputBlocked);
                return;
            }

            outputRestartTimer += deltaTime;
            if (outputRestartTimer >= outputJamSensor.RestartDelay)
            {
                outputJamBlocked = false;
                outputJamSensor.SetCongestionTime(0f);
                outputRestartTimer = 0f;
            }

            return;
        }

        outputRestartTimer = 0f;
        if (outputJamSensor != null && stoppedItemsInOutputJamSensor >= outputJamSensor.JamItemThreshold)
        {
            float congestionTime = outputJamSensor.CurrentCongestionTime + deltaTime;
            outputJamSensor.SetCongestionTime(congestionTime);
            if (congestionTime >= outputJamSensor.JamDetectionTime)
            {
                outputJamBlocked = true;
                ChangeState(ProcessingMachineState.OutputBlocked);
                onOutputBlocked?.Invoke();
            }
        }
        else
        {
            outputJamSensor?.SetCongestionTime(0f);
        }
    }

    private bool IsOutputJamActive()
    {
        if (outputJamBlocked)
        {
            return true;
        }

        if (outputConveyor != null && outputConveyor.IsJammed)
        {
            return true;
        }

        return outputJamSensor != null
            && outputJamSensor.CurrentItemCount >= outputJamSensor.JamItemThreshold
            && outputJamSensor.CurrentCongestionTime >= outputJamSensor.JamDetectionTime;
    }

    private void ResolveOutputReferences()
    {
        if (outputConveyor != null && outputJamSensor == null)
        {
            outputJamSensor = outputConveyor.GetComponentInChildren<ConveyorJamSensor>();
        }

        if ((outputConveyor != null && outputJamSensor != null) || attemptedOutputReferenceResolve)
        {
            return;
        }

        attemptedOutputReferenceResolve = true;
        Transform output = transform.Find("PackagingOutputConveyor");
        if (output == null)
        {
            output = transform.parent != null ? transform.parent.Find("PackagingOutputConveyor") : null;
        }

        if (output == null)
        {
            return;
        }

        outputConveyor = output.GetComponent<ConveyorController>();
        outputJamSensor = output.GetComponentInChildren<ConveyorJamSensor>();
    }

    private void ChangeState(ProcessingMachineState nextState)
    {
        currentState = nextState;
    }

    private void RefreshInputProgress()
    {
        inputProgress = $"{storedInputItems} / {Mathf.Max(1, requiredInputItems)}";
    }
}
