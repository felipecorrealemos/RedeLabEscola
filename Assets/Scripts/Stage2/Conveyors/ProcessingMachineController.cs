using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class ProcessingMachineController : MonoBehaviour
{
    [Header("Debug Output Shortcut")]
    [SerializeField] private bool enableDebugOutputGeneration;
    [SerializeField, Min(0.1f)] private float debugOutputInterval = 3f;

    [Header("Recipe")]
    [SerializeField] private bool useRecipeRequirements;
    [SerializeField, Min(1)] private int requiredPipes = 1;
    [SerializeField, Min(1)] private int requiredBeams = 1;
    [SerializeField, Min(1)] private int requiredIngots = 1;
    [SerializeField, Min(1)] private int requiredTotalMaterials = 3;
    [SerializeField, Min(1)] private int maximumStoredPipes = 4;
    [SerializeField, Min(1)] private int maximumStoredBeams = 4;
    [SerializeField, Min(1)] private int maximumStoredIngots = 4;
    [SerializeField, Min(1)] private int maximumStoredTotalMaterials = 12;

    [Header("Processing")]
    [SerializeField, Min(0.05f)] private float processingTime = 3f;
    [SerializeField] private GameObject processedPartPrefab;
    [SerializeField] private Transform outputSpawnPoint;
    [SerializeField] private ConveyorController outputConveyor;
    [SerializeField] private string processedProductId = "ProcessedPart";
    [SerializeField] private bool autoStartProcessing = true;
    [SerializeField] private bool usePlaceholderWhenPrefabMissing = true;
    [SerializeField] private bool startPoweredOn = true;

    [Header("Output")]
    [SerializeField, Min(1)] private int maximumPendingOutputItems = 1;
    [SerializeField] private bool stopProductionWhenOutputFull;
    [SerializeField] private Vector3 processedPartScale = Vector3.one;

    [Header("Output Jam Sensor")]
    [SerializeField] private ConveyorJamSensor outputJamSensor;
    [SerializeField] private bool stopProcessingWhenOutputJammed = true;

    [Header("Optional Indicators")]
    [SerializeField] private Renderer poweredBlueRenderer;
    [SerializeField] private Renderer processingGreenRenderer;
    [SerializeField] private Renderer waitingYellowRenderer;
    [SerializeField] private Renderer errorRedRenderer;

    [Header("Events")]
    [SerializeField] private UnityEvent onMaterialReceived;
    [SerializeField] private UnityEvent onProcessingStarted;
    [SerializeField] private UnityEvent onProcessedPartCreated;
    [SerializeField] private UnityEvent onOutputBlocked;

    [Header("Runtime Debug")]
    [SerializeField] private ProcessingMachineState currentState = ProcessingMachineState.PoweredOff;
    [SerializeField] private int storedPipes;
    [SerializeField] private int storedBeams;
    [SerializeField] private int storedIngots;
    [SerializeField] private int storedTotalMaterials;
    [SerializeField] private int pendingProcessedParts;
    [SerializeField] private float currentProcessingTimer;
    [SerializeField] private int stoppedItemsInOutputJamSensor;
    [SerializeField] private bool outputJamBlocked;
    [SerializeField] private float debugOutputTimer;
    [SerializeField] private int debugGeneratedOutputItems;

    private float outputRestartTimer;

    private Coroutine processingRoutine;
    private bool missingPrefabWarningLogged;
    private bool placeholderWarningLogged;
    private bool attemptedOutputReferenceResolve;

    public ProcessingMachineState CurrentState => currentState;
    public int StoredPipes => storedPipes;
    public int StoredBeams => storedBeams;
    public int StoredIngots => storedIngots;
    public int StoredTotalMaterials => storedTotalMaterials;
    public int PendingProcessedParts => pendingProcessedParts;
    public ConveyorController OutputConveyor => outputConveyor;
    public bool OutputJamBlocked => outputJamBlocked;
    public int StoppedItemsInOutputJamSensor => stoppedItemsInOutputJamSensor;
    public bool AcceptsAnyMaterial => !useRecipeRequirements;

    public void Configure(ConveyorController conveyor, Transform spawnPoint, GameObject prefab)
    {
        outputConveyor = conveyor;
        outputSpawnPoint = spawnPoint;
        if (processedPartPrefab == null)
        {
            processedPartPrefab = prefab;
        }
    }

    public void ConfigureOutputJamSensor(ConveyorJamSensor jamSensor)
    {
        outputJamSensor = jamSensor;
        stopProductionWhenOutputFull = false;
    }

    private void Awake()
    {
        ResolveOutputReferences();
        currentState = startPoweredOn ? ProcessingMachineState.WaitingForMaterials : ProcessingMachineState.PoweredOff;
        UpdateIndicators();
    }

    private void Update()
    {
        if (currentState == ProcessingMachineState.PoweredOff || currentState == ProcessingMachineState.Error)
        {
            UpdateIndicators();
            return;
        }

        UpdateOutputJamState(Time.deltaTime);
        TryReleasePendingOutput();

        if (enableDebugOutputGeneration)
        {
            UpdateDebugOutputGeneration(Time.deltaTime);
        }
        else if (autoStartProcessing && processingRoutine == null && pendingProcessedParts < maximumPendingOutputItems && HasRecipe())
        {
            if (IsOutputReadyForNewItem())
            {
                processingRoutine = StartCoroutine(ProcessRecipe());
            }
            else if (stopProductionWhenOutputFull)
            {
                ChangeState(ProcessingMachineState.OutputBlocked);
            }
        }
        else if (processingRoutine == null && pendingProcessedParts == 0 && currentState != ProcessingMachineState.OutputBlocked)
        {
            ChangeState(HasRecipe() ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
        }

        UpdateIndicators();
    }

    private void UpdateDebugOutputGeneration(float deltaTime)
    {
        if (pendingProcessedParts >= maximumPendingOutputItems)
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
        debugOutputTimer += deltaTime;
        currentProcessingTimer = debugOutputTimer;

        if (debugOutputTimer < debugOutputInterval)
        {
            return;
        }

        debugOutputTimer = 0f;
        currentProcessingTimer = 0f;
        pendingProcessedParts++;
        debugGeneratedOutputItems++;
        TryReleasePendingOutput();
    }

    public bool CanAcceptMaterial(RoboticArmProductType itemType)
    {
        if (!useRecipeRequirements)
        {
            return storedTotalMaterials < maximumStoredTotalMaterials;
        }

        switch (itemType)
        {
            case RoboticArmProductType.Pipes:
                return storedPipes < maximumStoredPipes;
            case RoboticArmProductType.Beams:
                return storedBeams < maximumStoredBeams;
            case RoboticArmProductType.Ingots:
                return storedIngots < maximumStoredIngots;
            default:
                return false;
        }
    }

    public bool TryReceiveMaterial(RoboticArmProductType itemType, ConveyorItem item)
    {
        if (item == null || !CanAcceptMaterial(itemType))
        {
            return false;
        }

        if (!useRecipeRequirements)
        {
            storedTotalMaterials++;
        }
        else
        {
            switch (itemType)
            {
                case RoboticArmProductType.Pipes:
                    storedPipes++;
                    break;
                case RoboticArmProductType.Beams:
                    storedBeams++;
                    break;
                case RoboticArmProductType.Ingots:
                    storedIngots++;
                    break;
                default:
                    return false;
            }
        }

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

        onMaterialReceived?.Invoke();
        ChangeState(HasRecipe() ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
        return true;
    }

    public GameObject GetAvailableProcessedItem()
    {
        return outputConveyor != null ? outputConveyor.GetAvailableItem() : null;
    }

    public bool ReserveProcessedItem(GameObject item)
    {
        return outputConveyor != null && outputConveyor.ReserveItem(item);
    }

    public void ReleaseProcessedItem(GameObject item)
    {
        outputConveyor?.ReleaseReservation(item);
    }

    public void RemoveProcessedItem(GameObject item)
    {
        outputConveyor?.RemoveItem(item);
    }

    public void NotifyProcessedItemCollected(GameObject item)
    {
        outputConveyor?.NotifyItemCollected(item);
    }

    private IEnumerator ProcessRecipe()
    {
        if (useRecipeRequirements)
        {
            storedPipes -= requiredPipes;
            storedBeams -= requiredBeams;
            storedIngots -= requiredIngots;
        }
        else
        {
            storedTotalMaterials -= requiredTotalMaterials;
        }

        currentProcessingTimer = 0f;
        ChangeState(ProcessingMachineState.Processing);
        onProcessingStarted?.Invoke();

        while (currentProcessingTimer < processingTime)
        {
            currentProcessingTimer += Time.deltaTime;
            yield return null;
        }

        currentProcessingTimer = 0f;
        pendingProcessedParts++;
        processingRoutine = null;
        TryReleasePendingOutput();
    }

    private void TryReleasePendingOutput()
    {
        if (pendingProcessedParts <= 0)
        {
            if (currentState == ProcessingMachineState.OutputBlocked && !outputJamBlocked)
            {
                ChangeState(HasRecipe() ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
            }

            return;
        }

        if (!IsOutputReadyForNewItem())
        {
            ChangeState(ProcessingMachineState.OutputBlocked);
            onOutputBlocked?.Invoke();
            return;
        }

        GameObject itemObject = CreateProcessedPartObject();
        if (itemObject == null)
        {
            ChangeState(ProcessingMachineState.Error);
            return;
        }

        bool received = outputConveyor.TryReceiveItem(itemObject, processedProductId, outputSpawnPoint, true, 0f);
        if (!received)
        {
            Destroy(itemObject);
            ChangeState(ProcessingMachineState.OutputBlocked);
            onOutputBlocked?.Invoke();
            return;
        }

        pendingProcessedParts--;
        onProcessedPartCreated?.Invoke();
        ChangeState(HasRecipe() ? ProcessingMachineState.Ready : ProcessingMachineState.WaitingForMaterials);
    }

    private GameObject CreateProcessedPartObject()
    {
        Vector3 spawnPosition = outputSpawnPoint != null ? outputSpawnPoint.position : transform.position;
        Quaternion spawnRotation = outputSpawnPoint != null ? outputSpawnPoint.rotation : transform.rotation;

        if (processedPartPrefab != null)
        {
            GameObject instance = Instantiate(processedPartPrefab, spawnPosition, spawnRotation);
            if (processedPartScale != Vector3.zero)
            {
                instance.transform.localScale = processedPartScale;
            }

            instance.name = processedProductId;
            return instance;
        }

        if (!usePlaceholderWhenPrefabMissing)
        {
            if (!missingPrefabWarningLogged)
            {
                Debug.LogWarning($"{name} cannot create processed part because processedPartPrefab is empty.", this);
                missingPrefabWarningLogged = true;
            }

            return null;
        }

        if (!placeholderWarningLogged)
        {
            Debug.LogWarning($"{name} is using a temporary placeholder because processedPartPrefab is empty. Assign the final processed part prefab in the Inspector.", this);
            placeholderWarningLogged = true;
        }

        GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
        placeholder.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        placeholder.transform.localScale = processedPartScale == Vector3.zero ? new Vector3(0.45f, 0.25f, 0.45f) : processedPartScale;
        placeholder.name = processedProductId + "_Placeholder";
        return placeholder;
    }

    private bool HasRecipe()
    {
        if (!useRecipeRequirements)
        {
            return storedTotalMaterials >= requiredTotalMaterials;
        }

        return storedPipes >= requiredPipes
            && storedBeams >= requiredBeams
            && storedIngots >= requiredIngots;
    }

    private bool IsOutputReadyForNewItem()
    {
        if (stopProcessingWhenOutputJammed && IsOutputJamActive())
        {
            return false;
        }

        if (outputConveyor == null || outputSpawnPoint == null)
        {
            return false;
        }

        if (!outputConveyor.CanReceiveItemAt(outputSpawnPoint))
        {
            return false;
        }

        ConveyorCollectionZone outputZone = outputConveyor.CollectionZone;
        return outputZone == null || !stopProductionWhenOutputFull || outputZone.CanAcceptItem();
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
        GameObject outputObject = GameObject.Find("ProcessingOutputConveyor");
        if (outputObject == null)
        {
            return;
        }

        if (outputConveyor == null)
        {
            outputConveyor = outputObject.GetComponent<ConveyorController>();
        }

        if (outputJamSensor == null)
        {
            outputJamSensor = outputObject.GetComponentInChildren<ConveyorJamSensor>();
        }
    }

    private void ChangeState(ProcessingMachineState nextState)
    {
        if (currentState != nextState)
        {
            currentState = nextState;
        }
    }

    private void UpdateIndicators()
    {
        SetRenderer(poweredBlueRenderer, currentState == ProcessingMachineState.WaitingForMaterials || currentState == ProcessingMachineState.Ready);
        SetRenderer(processingGreenRenderer, currentState == ProcessingMachineState.Processing);
        SetRenderer(waitingYellowRenderer, currentState == ProcessingMachineState.WaitingForMaterials || currentState == ProcessingMachineState.OutputBlocked);
        SetRenderer(errorRedRenderer, currentState == ProcessingMachineState.Error);
    }

    private void SetRenderer(Renderer renderer, bool active)
    {
        if (renderer != null)
        {
            renderer.enabled = active;
        }
    }
}
