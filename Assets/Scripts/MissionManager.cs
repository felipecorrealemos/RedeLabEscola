using System.Collections;
using System.Collections.Generic;
using RedeLabEscola.Auth;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MissionManager : MonoBehaviour
{
    [System.Serializable]
    public class MissionTask
    {
        public string Id;
        public string Description;
        public bool IsComplete;
    }

    [System.Serializable]
    public class Mission
    {
        public int Number;
        public string Title;
        public List<MissionTask> Tasks = new List<MissionTask>();
    }

    private const string MissionCanvasName = "MissionCanvas";
    private const string MissionPanelName = "MissionPanel";
    private const string GameplaySceneName = "SampleScene";
    private const string Stage2SceneName = "Stage2_Factory";

    [SerializeField] private List<Mission> missions = new List<Mission>();
    [SerializeField] private int startingMissionNumber = 1;
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private CanvasGroup expandedContentCanvasGroup;
    [SerializeField] private Text titleLabel;
    [SerializeField] private RectTransform taskListRoot;
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private Button toggleButton;
    [SerializeField] private GameObject expandedContent;
    [Header("Template visual das tarefas (editavel no Inspector)")]
    [SerializeField] private GameObject taskRowTemplate;
    [SerializeField] private Color completedCheckColor = new Color(0.16f, 0.92f, 0.34f, 1f);
    [SerializeField] private Color incompleteCircleColor = new Color(0.94f, 0.97f, 1f, 0.96f);
    [SerializeField] private Color completedTaskTextColor = new Color(0.75f, 1f, 0.8f, 1f);
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.02f, 0.34f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.34f, 0.96f);
    [SerializeField] private Vector2 panelOffsetMin = Vector2.zero;
    [SerializeField] private Vector2 panelOffsetMax = Vector2.zero;
    [SerializeField] private Vector2 collapsedPanelAnchorMin = new Vector2(0.02f, 0.912f);
    [SerializeField] private Vector2 collapsedPanelAnchorMax = new Vector2(0.18f, 0.96f);
    [SerializeField] private Vector2 collapsedPanelOffsetMin = Vector2.zero;
    [SerializeField] private Vector2 collapsedPanelOffsetMax = Vector2.zero;
    [SerializeField] [Range(0.25f, 1f)] private float panelOpacity = 0.86f;
    [SerializeField] private float taskRowHeight = 36.8f;
    [SerializeField] private int taskFontSize = 16;
    [SerializeField] private float minimumExpandedPanelHeight = 344f;
    [SerializeField] private float autoCollapseDelay = 5f;
    [SerializeField] private bool updateMissionFromNearestRouter = true;
    [SerializeField] private float routerMissionDetectionRadius = 8f;
    [SerializeField] private float missionAreaRefreshInterval = 1f;
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float stage2MissionStateRefreshInterval = 0.5f;

    [Header("Teste do Estágio 2")]
    [Tooltip("Conclui todas as tarefas da fábrica e exibe imediatamente a apresentação final durante o Play Mode.")]
    [SerializeField] private bool autoCompleteStage2ForTesting;

    private readonly Dictionary<int, Mission> missionsByNumber = new Dictionary<int, Mission>();
    private Mission currentMission;
    private bool isExpanded = true;
    private bool usingStage2MissionProfile;
    private bool stage2RoboticArmRefreshQueued;
    private bool capturedScenePanelLayout;
    [SerializeField] private int stage2RoboticArmOperatingCount;
    [SerializeField] private int stage2ScrapConsumedCount;
    [SerializeField] private int stage2ScrapTotalCount;
    [SerializeField] private int stage2PalletsPlacedCount;
    [SerializeField] private int stage2MachinePalletsSentCount;
    [SerializeField] private int sala2ConfiguredDoorDeviceCount;
    private readonly HashSet<int> stage2PlacedPalletIds = new HashSet<int>();
    private readonly HashSet<int> stage2MachinePalletIds = new HashSet<int>();
    private readonly HashSet<int> stage2ConsumedScrapIds = new HashSet<int>();
    private readonly HashSet<string> persistentlyCompletedTaskIds = new HashSet<string>();
    private bool stage2CompletionPresentationStarted;
    private bool stage2TestCompletionApplied;
    private string lastUiStateSignature;
    private float collapseAtTime;
    private float nextMissionAreaRefreshTime;
    private float nextStage2MissionStateRefreshTime;
    private float fadeTarget = 1f;
    private bool stagePresentationHasPriority;
    private Coroutine delayedStageMissionOpen;
    private Transform sala2EntranceDoorPivot;
    private Transform sala3EntranceDoorPivot;
    private Quaternion sala2EntranceClosedRotation;
    private Quaternion sala3EntranceClosedRotation;
    private Transform lastOpenedDoorPivot;
    private Quaternion lastOpenedDoorClosedRotation;
    private RectTransform titleLeftAccent;
    private RectTransform titleRightAccent;
    private static Sprite roundedPanelSprite;
    [SerializeField, HideInInspector] private int missionUiTypographyVersion;

    public static MissionManager Instance { get; private set; }
    public int CurrentMissionNumber => currentMission != null ? currentMission.Number : 0;
    public bool AreAllCurrentTasksComplete
    {
        get
        {
            if (currentMission == null || currentMission.Tasks == null || currentMission.Tasks.Count == 0)
            {
                return false;
            }

            return currentMission.Tasks.TrueForAll(task => task != null && task.IsComplete);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallbacks()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureForScene(scene);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EnsureForCurrentScene()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (IsMissionScene(activeScene))
        {
            EnsureForScene(activeScene);
            return;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (IsMissionScene(scene))
            {
                EnsureForScene(scene);
                return;
            }
        }

        DestroySceneMissionUi();
    }

    public static void EnsureForScene(Scene scene)
    {
        if (!IsMissionScene(scene))
        {
            DestroySceneMissionUi();
            return;
        }

        if (Instance != null || FindObjectOfType<MissionManager>() != null)
        {
            return;
        }

        GameObject managerObject = new GameObject("MissionManager");
        if (scene.IsValid() && scene.isLoaded)
        {
            SceneManager.MoveGameObjectToScene(managerObject, scene);
        }

        managerObject.AddComponent<MissionManager>();
    }

#if UNITY_EDITOR
    public void EnsureEditorPreview()
    {
        if (Application.isPlaying || !IsMissionScene(GetMissionSceneContext()))
        {
            return;
        }

        ApplyMissionUiTypographyMigration();
        ConfigureMissionsForActiveScene();
        RebuildMissionLookup();
        EnsureUi();
        SetMission(startingMissionNumber);
        if (canvas != null) canvas.enabled = true;

        UnityEditor.EditorUtility.SetDirty(this);
        if (canvas != null)
        {
            UnityEditor.EditorUtility.SetDirty(canvas.gameObject);
        }

        if (panelRect != null)
        {
            UnityEditor.EditorUtility.SetDirty(panelRect.gameObject);
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(GetMissionSceneContext());
    }
#endif

    private void Awake()
    {
        if (!IsMissionScene(GetMissionSceneContext()))
        {
            Destroy(gameObject);
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        InitializeOnlineProgressSession();
        ApplyMissionUiTypographyMigration();
        ConfigureMissionsForActiveScene();
        RebuildMissionLookup();
        EnsureUi();
        CacheProgressionEntranceDoors();
        if (canvas != null) canvas.enabled = true;
        SetMission(startingMissionNumber);
        RefreshStage2RoboticArmMission();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        ApplyMissionUiTypographyMigration();
        if (Application.isPlaying)
        {
            RebuildMissionLookup();
            RefreshUiIfStateChanged(true);
            return;
        }

        ConfigureMissionsForActiveScene();
        if (!Application.isPlaying && canvas != null) canvas.enabled = false;
    }

    private Scene GetMissionSceneContext()
    {
        return gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
    }

    private void Update()
    {
        if (!stagePresentationHasPriority && Input.GetKeyDown(KeyCode.Q))
        {
            ToggleExpanded();
        }

        RefreshMissionFromNearestRouter();
        RefreshStage2MissionState();
        ApplyStage2TestCompletionIfRequested();
        RefreshUiIfStateChanged(false);

        if (isExpanded && autoCollapseDelay > 0f && Time.unscaledTime >= collapseAtTime)
        {
            SetExpanded(false);
        }

        UpdateFade();
    }

    public void SetMission(int missionNumber)
    {
        SetMissionInternal(missionNumber, true);
    }

    // Seleciona a sala durante Load Game sem reproduzir a passagem física
    // entre salas nem agendar uma nova trava de entrada.
    public void RestoreMission(int missionNumber)
    {
        SetMissionInternal(missionNumber, false);
    }

    private void SetMissionInternal(int missionNumber, bool applyRoomTransitionEffects)
    {
        RebuildMissionLookup();
        if (!missionsByNumber.TryGetValue(missionNumber, out Mission mission))
        {
            Debug.LogWarning($"Mission {missionNumber} was not found.");
            return;
        }

        int previousMissionNumber = CurrentMissionNumber;
        bool shouldLockPreviousRoom = applyRoomTransitionEffects
            && previousMissionNumber > 0
            && missionNumber == previousMissionNumber + 1
            && AreAllCurrentTasksComplete;

        currentMission = mission;
        if (missionNumber == 2)
        {
            RefreshSala2DoorDeviceMission();
        }
        else if (missionNumber == 3)
        {
            RefreshSala3DoorDeviceMission();
        }

        RefreshUi();

        if (shouldLockPreviousRoom && (missionNumber == 2 || missionNumber == 3))
        {
            StartCoroutine(LockEntranceDoorAfterPlayerPasses(missionNumber));
        }
    }

    private void CacheProgressionEntranceDoors()
    {
        sala2EntranceDoorPivot = FindProgressionEntranceDoorPivot(2);
        sala3EntranceDoorPivot = FindProgressionEntranceDoorPivot(3);
        if (sala2EntranceDoorPivot != null)
        {
            sala2EntranceClosedRotation = sala2EntranceDoorPivot.localRotation;
        }

        if (sala3EntranceDoorPivot != null)
        {
            sala3EntranceClosedRotation = sala3EntranceDoorPivot.localRotation;
        }
    }

    private IEnumerator LockEntranceDoorAfterPlayerPasses(int enteredMissionNumber)
    {
        yield return new WaitForSeconds(0.35f);

        Transform pivot = lastOpenedDoorPivot != null
            ? lastOpenedDoorPivot
            : enteredMissionNumber == 2 ? sala2EntranceDoorPivot : sala3EntranceDoorPivot;
        Quaternion closedRotation = enteredMissionNumber == 2 ? sala2EntranceClosedRotation : sala3EntranceClosedRotation;
        if (lastOpenedDoorPivot != null)
        {
            closedRotation = lastOpenedDoorClosedRotation;
        }
        if (pivot == null)
        {
            CacheProgressionEntranceDoors();
            pivot = enteredMissionNumber == 2 ? sala2EntranceDoorPivot : sala3EntranceDoorPivot;
            closedRotation = enteredMissionNumber == 2 ? sala2EntranceClosedRotation : sala3EntranceClosedRotation;
        }

        if (pivot == null)
        {
            yield break;
        }

        NetworkDoorDevice[] doorDevices = FindObjectsOfType<NetworkDoorDevice>(true);
        foreach (NetworkDoorDevice doorDevice in doorDevices)
        {
            if (doorDevice != null && doorDevice.DoorPivot == pivot)
            {
                doorDevice.CloseFromRoomTransition();
            }
        }

        RoomProgressionDoorLock doorLock = pivot.GetComponent<RoomProgressionDoorLock>();
        if (doorLock == null)
        {
            doorLock = pivot.gameObject.AddComponent<RoomProgressionDoorLock>();
        }

        doorLock.Lock(closedRotation);
    }

    private Transform FindProgressionEntranceDoorPivot(int missionNumber)
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        Transform roomRoot = null;
        foreach (Transform candidate in transforms)
        {
            if (candidate != null && GetMissionNumberFromAreaName(candidate.name) == missionNumber)
            {
                roomRoot = candidate;
                break;
            }
        }

        if (roomRoot == null)
        {
            return null;
        }

        string expectedDoorName = missionNumber == 2 ? "Door (1)" : "Door";
        Transform[] roomChildren = roomRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in roomChildren)
        {
            if (candidate == null || candidate.name != expectedDoorName || candidate.parent == null || candidate.parent.name != "Office")
            {
                continue;
            }

            return candidate.childCount > 0 ? candidate.GetChild(0) : candidate;
        }

        return null;
    }

    public void SetStagePresentationPriority(bool hasPriority, bool openMissionAfterRelease = false, float openDelay = 1.25f)
    {
        stagePresentationHasPriority = hasPriority;

        if (delayedStageMissionOpen != null)
        {
            StopCoroutine(delayedStageMissionOpen);
            delayedStageMissionOpen = null;
        }

        if (hasPriority)
        {
            isExpanded = false;
            fadeTarget = 0f;
            if (expandedContentCanvasGroup != null)
            {
                expandedContentCanvasGroup.alpha = 0f;
                expandedContentCanvasGroup.blocksRaycasts = false;
                expandedContentCanvasGroup.interactable = false;
            }
            if (expandedContent != null) expandedContent.SetActive(false);
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.blocksRaycasts = false;
                panelCanvasGroup.interactable = false;
            }
            return;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.interactable = true;
        }

        if (openMissionAfterRelease)
        {
            delayedStageMissionOpen = StartCoroutine(OpenMissionAfterStagePresentation(openDelay));
        }
    }

    private IEnumerator OpenMissionAfterStagePresentation(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        delayedStageMissionOpen = null;
        if (!stagePresentationHasPriority) ExpandForDelay();
    }

    public void CompleteTask(string taskId)
    {
        SetTaskCompletion(taskId, true);
    }

    public void SetTaskCompletion(string taskId, bool complete)
    {
        if (currentMission == null)
        {
            return;
        }

        SetTaskCompletion(currentMission.Number, taskId, complete);
    }

    public void SetTaskCompletion(int missionNumber, string taskId, bool complete)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        if (!complete && persistentlyCompletedTaskIds.Contains(taskId))
        {
            return;
        }

        RebuildMissionLookup();
        if (!missionsByNumber.TryGetValue(missionNumber, out Mission mission))
        {
            return;
        }

        MissionTask task = mission.Tasks.Find(candidate => candidate.Id == taskId);
        if (task == null || task.IsComplete == complete)
        {
            return;
        }

        bool wasComplete = task.IsComplete;
        task.IsComplete = complete;
        if (IsNewCompletionTransition(wasComplete, complete))
        {
            // A conclusao e monotona: depois do primeiro false -> true, reavaliacoes
            // de objetos reversiveis nao podem apagar uma unidade de progresso.
            persistentlyCompletedTaskIds.Add(taskId);
            RedeLabProgressService.Instance?.TryQueueMissionCompletion(taskId);
        }
        if (currentMission == mission)
        {
            ExpandForDelay();
            RefreshUi();
            TryPresentStage2Completion();
        }
    }

    public static bool IsNewCompletionTransition(bool previousState, bool nextState)
    {
        return !previousState && nextState;
    }

    public void RestoreCompletedMissions(IEnumerable<string> completedTaskIds)
    {
        if (completedTaskIds == null) return;

        RebuildMissionLookup();
        foreach (string taskId in completedTaskIds)
        {
            if (string.IsNullOrWhiteSpace(taskId)) continue;
            foreach (Mission mission in missions)
            {
                if (mission == null || mission.Tasks == null) continue;
                MissionTask task = mission.Tasks.Find(candidate => candidate != null && candidate.Id == taskId);
                if (task == null) continue;
                task.IsComplete = true;
                persistentlyCompletedTaskIds.Add(taskId);
                break;
            }
        }

        RefreshUiIfStateChanged(true);
    }

    private void InitializeOnlineProgressSession()
    {
        Scene scene = GetMissionSceneContext();
        if (!RedeLabLoadContext.TryGetForScene(scene.name, out RedeLabLoadContextData context)) return;
        RedeLabProgressService.EnsureInstance().BeginOnlineGameplaySession(
            context.MissoesConcluidas,
            context.IsLoadGame);
    }

    public void RestoreStage2AggregateState(ISet<string> completedTaskIds)
    {
        if (!usingStage2MissionProfile || completedTaskIds == null) return;

        if (completedTaskIds.Contains("fabrica_bracos_roboticos")) stage2RoboticArmOperatingCount = 3;
        if (completedTaskIds.Contains("fabrica_limpar_entulhos_garra"))
        {
            stage2ScrapTotalCount = Mathf.Max(1, FindObjectsOfType<ScrapItem>(true).Length);
            stage2ScrapConsumedCount = stage2ScrapTotalCount;
        }
        if (completedTaskIds.Contains("fabrica_pallets_esteira_empilhadeira")) stage2PalletsPlacedCount = 4;
        if (completedTaskIds.Contains("fabrica_pallets_gerados_enviados")) stage2MachinePalletsSentCount = 3;

        RefreshUiIfStateChanged(true);
    }

    public static void CompleteCurrentTask(string taskId)
    {
        Instance?.CompleteTask(taskId);
    }

    public static void NotifyDevicePlaced(MovableDevice device, DeviceDropZone dropZone)
    {
        if (Instance == null || device == null || dropZone == null)
        {
            return;
        }

        int missionNumber = Instance.ResolvePlacementMissionNumber(dropZone);
        if (missionNumber == 3)
        {
            ComputerInteractable networkDevice = device.EnsureComputerInteractable();
            if (device.IsComputerCabinetDevice())
            {
                Instance.SetTaskCompletion(3, "sala3_colocar_gabinete", networkDevice != null && networkDevice.IsConnectedToNetworkJack);
            }
            else if (device.IsPrinterDevice())
            {
                Instance.SetTaskCompletion(3, "sala3_colocar_impressora", networkDevice != null && networkDevice.IsConnectedToNetworkJack);
            }
        }

        if (!device.IsComputerCabinetDevice())
        {
            return;
        }

        string taskId = Instance.ResolvePlacementTaskId(dropZone, missionNumber);
        if (missionNumber > 0 && !string.IsNullOrWhiteSpace(taskId))
        {
            Instance.SetTaskCompletion(missionNumber, taskId, true);
        }
    }

    public static void NotifyDeviceRemoved(MovableDevice device, DeviceDropZone dropZone)
    {
        if (Instance == null || device == null || dropZone == null)
        {
            return;
        }

        int missionNumber = Instance.ResolvePlacementMissionNumber(dropZone);
        if (missionNumber == 3)
        {
            if (device.IsComputerCabinetDevice())
            {
                Instance.SetTaskCompletion(3, "sala3_colocar_gabinete", false);
            }
            else if (device.IsPrinterDevice())
            {
                Instance.SetTaskCompletion(3, "sala3_colocar_impressora", false);
            }
        }

        if (!device.IsComputerCabinetDevice())
        {
            return;
        }

        string taskId = Instance.ResolvePlacementTaskId(dropZone, missionNumber);
        if (missionNumber > 0 && !string.IsNullOrWhiteSpace(taskId))
        {
            Instance.SetTaskCompletion(missionNumber, taskId, false);
        }
    }

    private int ResolvePlacementMissionNumber(DeviceDropZone dropZone)
    {
        if (dropZone == null)
        {
            return 0;
        }

        int missionNumber = dropZone.MissionNumber;
        if (missionNumber > 0)
        {
            return missionNumber;
        }

        if (dropZone.IsComputerPlacementZoneForMission(CurrentMissionNumber))
        {
            return CurrentMissionNumber;
        }

        return 0;
    }

    private string ResolvePlacementTaskId(DeviceDropZone dropZone, int missionNumber)
    {
        if (dropZone == null || !dropZone.IsComputerPlacementZoneForMission(missionNumber))
        {
            return string.Empty;
        }

        string taskId = dropZone.PlacementTaskId;
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            return taskId;
        }

        if (missionNumber == 1)
        {
            return "sala1_colocar_gabinete";
        }

        if (missionNumber == 2)
        {
            return "sala2_colocar_gabinete";
        }

        return string.Empty;
    }

    public static void NotifyNetworkDeviceConfigured(ComputerInteractable device)
    {
        NotifyNetworkDeviceStateChanged(device);
    }

    public static void NotifyNetworkDeviceStateChanged(ComputerInteractable device)
    {
        if (Instance == null || device == null)
        {
            return;
        }

        string lowerTitle = device.DeviceTitle.ToLowerInvariant();
        string lowerName = device.name.ToLowerInvariant();
        bool isComputer = lowerTitle.Contains("computador") || lowerTitle.Contains("computer") || lowerName.Contains("computer") || lowerName.Contains("gabinete");
        bool isDoorDevice = lowerTitle.Contains("porta") || lowerTitle.Contains("door") || lowerTitle.Contains("dispositivo") || lowerName.Contains("porta") || lowerName.Contains("door");
        bool isPrinter = lowerTitle.Contains("impressora") || lowerTitle.Contains("printer") || lowerName.Contains("impressora") || lowerName.Contains("printer");

        if (Instance.CurrentMissionNumber == 1 && isComputer)
        {
            Instance.SetTaskCompletion("sala1_configurar_ip_pc", device.IsNetworkOperational);
            return;
        }

        if (Instance.CurrentMissionNumber == 2)
        {
            if (isComputer)
            {
                Instance.SetTaskCompletion("sala2_configurar_ip_pc", device.IsNetworkOperational);
            }
            else if (isDoorDevice)
            {
                Instance.RefreshSala2DoorDeviceMission();
            }

            return;
        }

        if (Instance.CurrentMissionNumber != 3)
        {
            return;
        }

        if (isComputer)
        {
            Instance.SetTaskCompletion("sala3_colocar_gabinete", device.IsConnectedToNetworkJack);
            Instance.SetTaskCompletion("sala3_configurar_ip_pc", device.IsNetworkOperational);
        }
        else if (isPrinter)
        {
            Instance.SetTaskCompletion("sala3_colocar_impressora", device.IsConnectedToNetworkJack);
            Instance.SetTaskCompletion("sala3_configurar_ip_impressora", device.IsNetworkOperational);
        }
        else if (isDoorDevice)
        {
            Instance.RefreshSala3DoorDeviceMission();
        }
    }

    public static void NotifySingleDoorOpened()
    {
        NotifySingleDoorStateChanged(true);
    }

    public static void NotifySingleDoorStateChanged(bool open)
    {
        if (Instance == null)
        {
            return;
        }

        if (Instance.CurrentMissionNumber == 1)
        {
            Instance.SetTaskCompletion("sala1_abrir_porta", open);
        }

        if (Instance.CurrentMissionNumber == 3)
        {
            Instance.SetTaskCompletion("sala3_abrir_porta", open);
        }
    }

    public static void NotifySingleDoorStateChanged(NetworkDoorDevice doorDevice, bool open)
    {
        if (Instance != null && open && doorDevice != null && doorDevice.DoorPivot != null)
        {
            Instance.lastOpenedDoorPivot = doorDevice.DoorPivot;
            Instance.lastOpenedDoorClosedRotation = doorDevice.ClosedLocalRotation;
        }

        NotifySingleDoorStateChanged(open);
    }

    public static void NotifyDualDoorsOpened()
    {
        NotifyDualDoorsStateChanged(true);
    }

    public static void NotifyDualDoorsStateChanged(bool open)
    {
        if (Instance != null && Instance.CurrentMissionNumber == 2)
        {
            Instance.SetTaskCompletion("sala2_abrir_portas", open);
        }
    }

    public static void NotifyDocumentPrinted(NetworkPrinterDevice printer)
    {
        if (Instance == null || printer == null || Instance.CurrentMissionNumber != 3)
        {
            return;
        }

        Instance.SetTaskCompletion("sala3_imprimir_documento", printer.HasPrintedDocument);
    }

    public static void NotifyDocumentPickedUp(PrintedDocumentInteractable document)
    {
        if (Instance != null && Instance.CurrentMissionNumber == 3)
        {
            Instance.SetTaskCompletion("sala3_pegar_documento", document != null && document.IsCarried);
        }
    }

    public static void NotifyDocumentDelivered(PrintedDocumentInteractable document)
    {
        if (Instance != null && Instance.CurrentMissionNumber == 3)
        {
            Instance.SetTaskCompletion("sala3_entregar_documento", document != null && document.IsDelivered);
        }
    }

    public static bool CanOperateDoorCommand(NetworkDoorDevice doorDevice)
    {
        if (Instance == null || Instance.CurrentMissionNumber != 3)
        {
            return true;
        }

        return Instance.AreTasksComplete(
            "sala3_colocar_gabinete",
            "sala3_configurar_ip_pc",
            "sala3_colocar_impressora",
            "sala3_configurar_ip_impressora",
            "sala3_imprimir_documento",
            "sala3_pegar_documento",
            "sala3_entregar_documento",
            "sala3_configurar_ip_porta");
    }

    public static void NotifyStage2RoboticArmOperationChanged()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.QueueStage2RoboticArmMissionRefresh();
    }

    public static void NotifyStage2ScrapConsumed(GameObject scrap)
    {
        if (Instance == null || !Instance.usingStage2MissionProfile || scrap == null)
        {
            return;
        }

        Instance.RegisterStage2ConsumedScrap(scrap);
    }

    public static void NotifyStage2PalletPlacedOnConveyor(GameObject pallet)
    {
        if (Instance == null || !Instance.usingStage2MissionProfile || pallet == null)
        {
            return;
        }

        Instance.RegisterStage2PlacedPallet(pallet);
    }

    public static void NotifyStage2MachinePalletSent(GameObject pallet)
    {
        if (Instance == null || !Instance.usingStage2MissionProfile || pallet == null)
        {
            return;
        }

        Instance.RegisterStage2MachinePallet(pallet);
    }

    private void ConfigureMissionsForActiveScene()
    {
        missions.Clear();
        if (IsStage2Scene(GetMissionSceneContext()))
        {
            usingStage2MissionProfile = true;
            EnsureStage2Missions();
            updateMissionFromNearestRouter = false;
            startingMissionNumber = 1;
            return;
        }

        usingStage2MissionProfile = false;
        EnsureDefaultMissions();
    }

    private void EnsureStage2Missions()
    {
        EnsureMission(1, "A fábrica", new[]
        {
            new MissionTask { Id = "fabrica_bracos_roboticos", Description = "Fazer funcionar os três braços robóticos" },
            new MissionTask { Id = "fabrica_limpar_entulhos_garra", Description = "Limpar entulhos com a garra pelo terminal" },
            new MissionTask { Id = "fabrica_pallets_esteira_empilhadeira", Description = "Colocar os paletes na esteira final com a empilhadeira" },
            new MissionTask { Id = "fabrica_pallets_gerados_enviados", Description = "Enviar paletes gerados pela máquina" }
        });
    }

    private void ApplyMissionUiTypographyMigration()
    {
        if (missionUiTypographyVersion >= 6) return;
        taskFontSize = 16;
        taskRowHeight = 36.8f;
        minimumExpandedPanelHeight = 344f;
        panelOpacity = 0.86f;
        missionUiTypographyVersion = 6;
    }

    private void EnsureDefaultMissions()
    {
        EnsureMission(1, "Sala 1", new[]
        {
            new MissionTask { Id = "sala1_colocar_gabinete", Description = "Levar o gabinete até a mesa do computador" },
            new MissionTask { Id = "sala1_configurar_ip_pc", Description = "Configurar o IP do computador" },
            new MissionTask { Id = "sala1_abrir_porta", Description = "Enviar comando pelo computador para abrir a porta" }
        });

        EnsureMission(2, "Sala 2", new[]
        {
            new MissionTask { Id = "sala2_colocar_gabinete", Description = "Colocar o computador na mesa" },
            new MissionTask { Id = "sala2_configurar_ip_pc", Description = "Configurar o computador com IP" },
            new MissionTask { Id = "sala2_configurar_ip_portas", Description = "Configurar o dispositivo da porta com IP" },
            new MissionTask { Id = "sala2_abrir_portas", Description = "Enviar comando pelo computador para abrir a porta" }
        });

        EnsureMission(3, "Sala 3", new[]
        {
            new MissionTask { Id = "sala3_colocar_gabinete", Description = "Colocar o computador na rede" },
            new MissionTask { Id = "sala3_configurar_ip_pc", Description = "Configurar o computador com IP" },
            new MissionTask { Id = "sala3_colocar_impressora", Description = "Colocar a impressora na rede" },
            new MissionTask { Id = "sala3_configurar_ip_impressora", Description = "Configurar a impressora com IP" },
            new MissionTask { Id = "sala3_imprimir_documento", Description = "Imprimir documento pelo computador" },
            new MissionTask { Id = "sala3_pegar_documento", Description = "Pegar o documento na impressora" },
            new MissionTask { Id = "sala3_entregar_documento", Description = "Entregar o documento para o professor" },
            new MissionTask { Id = "sala3_configurar_ip_porta", Description = "Configurar o dispositivo da porta com IP" },
            new MissionTask { Id = "sala3_abrir_porta", Description = "Enviar comando pelo computador para abrir a porta" }
        });
    }

    private bool AreTasksComplete(params string[] taskIds)
    {
        if (currentMission == null)
        {
            return false;
        }

        foreach (string taskId in taskIds)
        {
            MissionTask task = currentMission.Tasks.Find(candidate => candidate.Id == taskId);
            if (task == null || !task.IsComplete)
            {
                return false;
            }
        }

        return true;
    }

    private void EnsureMission(int number, string title, MissionTask[] defaultTasks)
    {
        Mission mission = missions.Find(candidate => candidate.Number == number);
        if (mission == null)
        {
            mission = new Mission { Number = number };
            missions.Add(mission);
        }

        mission.Title = title;
        if (mission.Tasks == null)
        {
            mission.Tasks = new List<MissionTask>();
        }

        foreach (MissionTask defaultTask in defaultTasks)
        {
            MissionTask task = mission.Tasks.Find(candidate => candidate.Id == defaultTask.Id);
            if (task == null)
            {
                mission.Tasks.Add(new MissionTask { Id = defaultTask.Id, Description = defaultTask.Description });
            }
            else
            {
                task.Description = defaultTask.Description;
            }
        }
    }

    private void RefreshSala2DoorDeviceMission()
    {
        HashSet<ComputerInteractable> configuredDevices = new HashSet<ComputerInteractable>();
        NetworkDoorDevice[] doorDevices = FindObjectsOfType<NetworkDoorDevice>(true);
        foreach (NetworkDoorDevice doorDevice in doorDevices)
        {
            if (doorDevice == null || GetMissionNumberForTransform(doorDevice.transform) != 2)
            {
                continue;
            }

            ComputerInteractable networkDevice = doorDevice.GetComponent<ComputerInteractable>();
            if (networkDevice != null && networkDevice.IsNetworkOperational)
            {
                configuredDevices.Add(networkDevice);
            }
        }

        sala2ConfiguredDoorDeviceCount = Mathf.Clamp(configuredDevices.Count, 0, 2);
        SetTaskCompletion(2, "sala2_configurar_ip_portas", sala2ConfiguredDoorDeviceCount >= 2);
    }

    private void RefreshSala3DoorDeviceMission()
    {
        bool hasConfiguredDoorDevice = false;
        NetworkDoorDevice[] doorDevices = FindObjectsOfType<NetworkDoorDevice>(true);
        foreach (NetworkDoorDevice doorDevice in doorDevices)
        {
            if (doorDevice == null || GetMissionNumberForTransform(doorDevice.transform) != 3)
            {
                continue;
            }

            ComputerInteractable networkDevice = doorDevice.GetComponent<ComputerInteractable>();
            if (networkDevice != null && networkDevice.IsNetworkOperational)
            {
                hasConfiguredDoorDevice = true;
                break;
            }
        }

        SetTaskCompletion(3, "sala3_configurar_ip_porta", hasConfiguredDoorDevice);
    }

    private int GetMissionNumberForTransform(Transform target)
    {
        while (target != null)
        {
            int missionNumber = GetMissionNumberFromAreaName(target.name);
            if (missionNumber > 0)
            {
                return missionNumber;
            }

            target = target.parent;
        }

        return 0;
    }

    private void RebuildMissionLookup()
    {
        missionsByNumber.Clear();
        foreach (Mission mission in missions)
        {
            if (mission != null)
            {
                missionsByNumber[mission.Number] = mission;
            }
        }
    }

    private static bool IsMissionScene(Scene scene)
    {
        return scene.IsValid() && (scene.name == GameplaySceneName || IsStage2Scene(scene));
    }

    private static bool IsStage2Scene(Scene scene)
    {
        return scene.IsValid() && scene.name == Stage2SceneName;
    }

    private static void DestroySceneMissionUi()
    {
        MissionManager[] managers = FindObjectsOfType<MissionManager>(true);
        foreach (MissionManager manager in managers)
        {
            if (manager != null)
            {
                Destroy(manager.gameObject);
            }
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas candidate in canvases)
        {
            if (candidate != null && candidate.name == MissionCanvasName)
            {
                Destroy(candidate.gameObject);
            }
        }
    }

    private void RefreshMissionFromNearestRouter()
    {
        if (!updateMissionFromNearestRouter || Time.time < nextMissionAreaRefreshTime)
        {
            return;
        }

        nextMissionAreaRefreshTime = Time.time + Mathf.Max(missionAreaRefreshInterval, 1f);

        Transform player = FindPlayerTransform();
        if (player == null)
        {
            return;
        }

        int areaMissionNumber = FindMissionNumberForPlayerArea(player.position);
        if (areaMissionNumber > 0 && areaMissionNumber != CurrentMissionNumber)
        {
            SetMission(areaMissionNumber);
            return;
        }

        if (areaMissionNumber > 0)
        {
            return;
        }

        if (HasMissionAreaRoots())
        {
            return;
        }

        RouterInteractable nearestRouter = FindNearestRouter(player.position, out float nearestDistanceSqr);
        if (nearestRouter == null || nearestDistanceSqr > routerMissionDetectionRadius * routerMissionDetectionRadius)
        {
            return;
        }

        int missionNumber = GetMissionNumberForRouter(nearestRouter);
        if (missionNumber > 0 && missionNumber != CurrentMissionNumber)
        {
            SetMission(missionNumber);
        }
    }

    private void RefreshStage2MissionState()
    {
        if (!usingStage2MissionProfile || Time.time < nextStage2MissionStateRefreshTime)
        {
            return;
        }

        nextStage2MissionStateRefreshTime = Time.time + Mathf.Max(stage2MissionStateRefreshInterval, 0.1f);
        RefreshStage2ScrapMission();
    }

    private void RefreshStage2RoboticArmMission()
    {
        if (!usingStage2MissionProfile)
        {
            return;
        }

        RoboticArmNetworkAdapter[] adapters = FindObjectsOfType<RoboticArmNetworkAdapter>(true);
        HashSet<string> onlineDeviceIds = new HashSet<string>();
        int onlineCount = 0;
        for (int i = 0; i < adapters.Length; i++)
        {
            RoboticArmNetworkAdapter adapter = adapters[i];
            if (IsStage2RoboticArmOnline(adapter))
            {
                string key = !string.IsNullOrWhiteSpace(adapter.DeviceId) ? adapter.DeviceId : adapter.GetInstanceID().ToString();
                if (onlineDeviceIds.Add(key))
                {
                    onlineCount++;
                }
            }
        }

        int clampedOnlineCount = Mathf.Clamp(onlineCount, 0, 3);
        bool countChanged = stage2RoboticArmOperatingCount != clampedOnlineCount;
        stage2RoboticArmOperatingCount = clampedOnlineCount;

        SetTaskCompletion(1, "fabrica_bracos_roboticos", stage2RoboticArmOperatingCount >= 3);
        if (countChanged)
        {
            RefreshUiIfStateChanged(true);
        }
    }

    private void QueueStage2RoboticArmMissionRefresh()
    {
        RefreshStage2RoboticArmMission();
        if (!usingStage2MissionProfile || stage2RoboticArmRefreshQueued || !isActiveAndEnabled)
        {
            return;
        }

        stage2RoboticArmRefreshQueued = true;
        StartCoroutine(RefreshStage2RoboticArmMissionAtEndOfFrame());
    }

    private IEnumerator RefreshStage2RoboticArmMissionAtEndOfFrame()
    {
        yield return null;
        stage2RoboticArmRefreshQueued = false;
        RefreshStage2RoboticArmMission();
    }

    private bool IsStage2RoboticArmOnline(RoboticArmNetworkAdapter adapter)
    {
        return adapter != null
            && adapter.isActiveAndEnabled
            && adapter.CurrentNetworkState == RoboticArmNetworkAdapter.NetworkState.Operating;
    }

    private void RefreshStage2ScrapMission()
    {
        ScrapItem[] scraps = FindObjectsOfType<ScrapItem>(true);
        int remainingScraps = 0;
        for (int i = 0; i < scraps.Length; i++)
        {
            if (scraps[i] != null && !stage2ConsumedScrapIds.Contains(scraps[i].gameObject.GetInstanceID()))
            {
                remainingScraps++;
            }
        }

        stage2ScrapTotalCount = Mathf.Max(stage2ScrapTotalCount, remainingScraps + stage2ScrapConsumedCount);
        SetTaskCompletion(1, "fabrica_limpar_entulhos_garra",
            stage2ScrapTotalCount > 0 && stage2ScrapConsumedCount >= stage2ScrapTotalCount);
    }

    private void RegisterStage2ConsumedScrap(GameObject scrap)
    {
        if (!stage2ConsumedScrapIds.Add(scrap.GetInstanceID())) return;
        stage2ScrapConsumedCount = stage2ConsumedScrapIds.Count;
        RefreshStage2ScrapMission();
        RefreshUiIfStateChanged(true);
    }

    private void RefreshStage2PalletMission()
    {
        SetTaskCompletion(1, "fabrica_pallets_esteira_empilhadeira", stage2PalletsPlacedCount >= 4);
    }

    private void RegisterStage2PlacedPallet(GameObject pallet)
    {
        if (!stage2PlacedPalletIds.Add(pallet.GetInstanceID())) return;
        stage2PalletsPlacedCount = Mathf.Clamp(stage2PlacedPalletIds.Count, 0, 4);
        RefreshStage2PalletMission();
        RefreshUiIfStateChanged(true);
    }

    private void RegisterStage2MachinePallet(GameObject pallet)
    {
        if (!stage2MachinePalletIds.Add(pallet.GetInstanceID())) return;
        stage2MachinePalletsSentCount = Mathf.Clamp(stage2MachinePalletIds.Count, 0, 3);
        SetTaskCompletion(1, "fabrica_pallets_gerados_enviados", stage2MachinePalletsSentCount >= 3);
        RefreshUiIfStateChanged(true);
    }

    private void TryPresentStage2Completion()
    {
        if (!usingStage2MissionProfile || stage2CompletionPresentationStarted || !AreAllCurrentTasksComplete) return;
        StageTransitionUI transition = FindObjectOfType<StageTransitionUI>(true);
        if (transition != null && transition.TryCompleteStage())
        {
            stage2CompletionPresentationStarted = true;
        }
    }

    private void ApplyStage2TestCompletionIfRequested()
    {
        if (!usingStage2MissionProfile || !autoCompleteStage2ForTesting || stage2TestCompletionApplied) return;
        stage2TestCompletionApplied = true;
        stage2RoboticArmOperatingCount = 3;
        stage2ScrapTotalCount = Mathf.Max(1, stage2ScrapTotalCount);
        stage2ScrapConsumedCount = stage2ScrapTotalCount;
        stage2PalletsPlacedCount = 4;
        stage2MachinePalletsSentCount = 3;

        if (currentMission != null && currentMission.Tasks != null)
        {
            foreach (MissionTask task in currentMission.Tasks)
            {
                if (task != null) task.IsComplete = true;
            }
        }

        RefreshUi();
        TryPresentStage2Completion();
    }

    private bool IsTaskComplete(int missionNumber, string taskId)
    {
        RebuildMissionLookup();
        if (!missionsByNumber.TryGetValue(missionNumber, out Mission mission) || mission.Tasks == null)
        {
            return false;
        }

        MissionTask task = mission.Tasks.Find(candidate => candidate.Id == taskId);
        return task != null && task.IsComplete;
    }

    private Transform FindTransformByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
        {
            return null;
        }

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == targetName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private Transform FindPlayerTransform()
    {
        PlayerTopDownController player = FindObjectOfType<PlayerTopDownController>();
        if (player != null)
        {
            return player.transform;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject == null)
        {
            playerObject = GameObject.Find("Player");
        }

        return playerObject != null ? playerObject.transform : null;
    }

    private RouterInteractable FindNearestRouter(Vector3 point, out float nearestDistanceSqr)
    {
        RouterInteractable nearestRouter = null;
        nearestDistanceSqr = float.MaxValue;
        RouterInteractable[] routers = FindObjectsOfType<RouterInteractable>(true);

        foreach (RouterInteractable router in routers)
        {
            if (router == null)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(router.transform.position - point);
            if (sqrDistance < nearestDistanceSqr)
            {
                nearestDistanceSqr = sqrDistance;
                nearestRouter = router;
            }
        }

        return nearestRouter;
    }

    private int FindMissionNumberForPlayerArea(Vector3 playerPosition)
    {
        Transform bestArea = null;
        int bestMissionNumber = 0;
        float bestAreaSize = float.MaxValue;

        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            int missionNumber = GetMissionNumberFromAreaName(candidate.name);
            if (missionNumber <= 0 || !missionsByNumber.ContainsKey(missionNumber))
            {
                continue;
            }

            if (!TryGetAreaBounds(candidate, out Bounds bounds))
            {
                continue;
            }

            float padding = 0.05f;
            bool containsX = playerPosition.x >= bounds.min.x - padding && playerPosition.x <= bounds.max.x + padding;
            bool containsZ = playerPosition.z >= bounds.min.z - padding && playerPosition.z <= bounds.max.z + padding;
            if (!containsX || !containsZ)
            {
                continue;
            }

            float areaSize = bounds.size.x * bounds.size.z;
            if (areaSize < bestAreaSize)
            {
                bestArea = candidate;
                bestMissionNumber = missionNumber;
                bestAreaSize = areaSize;
            }
        }

        return bestArea != null ? bestMissionNumber : 0;
    }

    private bool HasMissionAreaRoots()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (GetMissionNumberFromAreaName(candidate.name) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private int GetMissionNumberFromAreaName(string areaName)
    {
        if (string.IsNullOrWhiteSpace(areaName))
        {
            return 0;
        }

        string lowerName = areaName.ToLowerInvariant();
        if (!lowerName.Contains("sala"))
        {
            return 0;
        }

        if (lowerName.Contains("sala 1"))
        {
            return 1;
        }

        if (lowerName.Contains("sala 2"))
        {
            return 2;
        }

        if (lowerName.Contains("sala 3"))
        {
            return 3;
        }

        return 0;
    }

    private bool TryGetAreaBounds(Transform areaRoot, out Bounds bounds)
    {
        if (TryGetFloorBounds(areaRoot, out bounds))
        {
            return true;
        }

        bounds = new Bounds(areaRoot.position, Vector3.zero);
        bool hasBounds = false;

        Renderer[] renderers = areaRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer areaRenderer in renderers)
        {
            if (areaRenderer == null || areaRenderer.GetComponentInParent<MovableDevice>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = areaRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(areaRenderer.bounds);
            }
        }

        Collider[] colliders = areaRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider areaCollider in colliders)
        {
            if (areaCollider == null || areaCollider.GetComponentInParent<MovableDevice>() != null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = areaCollider.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(areaCollider.bounds);
            }
        }

        return hasBounds;
    }

    private bool TryGetFloorBounds(Transform areaRoot, out Bounds bounds)
    {
        bounds = new Bounds(areaRoot.position, Vector3.zero);
        bool hasBounds = false;
        float largestFloorArea = 0f;

        Renderer[] renderers = areaRoot.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer areaRenderer in renderers)
        {
            if (areaRenderer == null || areaRenderer.GetComponentInParent<MovableDevice>() != null)
            {
                continue;
            }

            Bounds rendererBounds = areaRenderer.bounds;
            if (!LooksLikeRoomFloor(rendererBounds))
            {
                continue;
            }

            float floorArea = rendererBounds.size.x * rendererBounds.size.z;
            if (!hasBounds || floorArea > largestFloorArea)
            {
                bounds = rendererBounds;
                hasBounds = true;
                largestFloorArea = floorArea;
            }
        }

        Collider[] colliders = areaRoot.GetComponentsInChildren<Collider>(true);
        foreach (Collider areaCollider in colliders)
        {
            if (areaCollider == null || areaCollider.GetComponentInParent<MovableDevice>() != null)
            {
                continue;
            }

            Bounds colliderBounds = areaCollider.bounds;
            if (!LooksLikeRoomFloor(colliderBounds))
            {
                continue;
            }

            float floorArea = colliderBounds.size.x * colliderBounds.size.z;
            if (!hasBounds || floorArea > largestFloorArea)
            {
                bounds = colliderBounds;
                hasBounds = true;
                largestFloorArea = floorArea;
            }
        }

        return hasBounds;
    }

    private bool LooksLikeRoomFloor(Bounds bounds)
    {
        return bounds.size.y <= 0.35f && bounds.size.x >= 4f && bounds.size.z >= 4f;
    }

    private int GetMissionNumberForRouter(RouterInteractable router)
    {
        NetworkScope scope = router != null ? router.ActiveNetworkScope : null;
        string prefix = scope != null ? scope.NetworkPrefix : router?.RouterIpAddress;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return 0;
        }

        string[] parts = prefix.TrimEnd('.').Split('.');
        if (parts.Length < 3 || !int.TryParse(parts[2], out int subnet))
        {
            return 0;
        }

        int missionNumber = subnet + 1;
        return missionsByNumber.ContainsKey(missionNumber) ? missionNumber : 0;
    }

    private void EnsureUi()
    {
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(MissionCanvasName);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        EnsureEventSystem();

        Transform panel = canvas.transform.Find(MissionPanelName);
        bool panelAlreadyExists = panel != null;
        if (panel == null)
        {
            panel = CreatePanel(canvas.transform).transform;
        }

        panelRect = panel as RectTransform;
        panelCanvasGroup = panel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        toggleButton = panel.Find("ToggleButton")?.GetComponent<Button>();
        expandedContent = panel.Find("ExpandedContent")?.gameObject;
        expandedContentCanvasGroup = expandedContent != null ? expandedContent.GetComponent<CanvasGroup>() : null;
        if (expandedContent != null && expandedContentCanvasGroup == null)
        {
            expandedContentCanvasGroup = expandedContent.AddComponent<CanvasGroup>();
        }

        titleLabel = panel.Find("ExpandedContent/Title")?.GetComponent<Text>();
        taskListRoot = panel.Find("ExpandedContent/Tasks") as RectTransform;
        if (toggleButton == null || expandedContent == null || titleLabel == null || taskListRoot == null)
        {
            DestroyImmediateSafe(panel.gameObject);
            panel = CreatePanel(canvas.transform).transform;
            panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            panelAlreadyExists = false;
        }

        // Keep the same mission-card language in both stages, including panels
        // authored directly in their scenes.
        EnsurePanelVisualStyle(panel);

        if (panelAlreadyExists)
        {
            CaptureScenePanelLayout();
        }

        Text toggleLabel = panel.Find("ToggleButton/Text")?.GetComponent<Text>();
        if (toggleLabel != null)
        {
            toggleLabel.text = "Missão  Q";
            toggleLabel.fontSize = 16;
        }

        titleLabel.fontSize = 19;

        ApplyExpandedState();
    }

    private GameObject CreatePanel(Transform parent)
    {
        GameObject panelObject = new GameObject(MissionPanelName);
        panelObject.transform.SetParent(parent, false);

        panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = panelAnchorMin;
        panelRect.anchorMax = panelAnchorMax;
        panelRect.offsetMin = panelOffsetMin;
        panelRect.offsetMax = panelOffsetMax;

        Image panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, panelOpacity);
        panelImage.sprite = GetRoundedPanelSprite();
        panelImage.type = Image.Type.Sliced;
        Outline panelOutline = panelObject.AddComponent<Outline>();
        panelOutline.effectColor = new Color(0.88f, 0.95f, 1f, 0.8f);
        panelOutline.effectDistance = new Vector2(1.5f, -1.5f);
        panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();
        panelCanvasGroup.alpha = 1f;
        panelCanvasGroup.blocksRaycasts = true;
        panelCanvasGroup.interactable = true;

        GameObject toggleObject = new GameObject("ToggleButton");
        toggleObject.transform.SetParent(panelObject.transform, false);
        RectTransform toggleRect = toggleObject.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0f, 1f);
        toggleRect.anchorMax = new Vector2(1f, 1f);
        toggleRect.pivot = new Vector2(0.5f, 1f);
        toggleRect.anchoredPosition = Vector2.zero;
        toggleRect.sizeDelta = new Vector2(0f, 33.6f);

        Image toggleImage = toggleObject.AddComponent<Image>();
        toggleImage.color = new Color(0.08f, 0.08f, 0.08f, 0.94f);
        toggleImage.sprite = GetRoundedPanelSprite();
        toggleImage.type = Image.Type.Sliced;
        toggleButton = toggleObject.AddComponent<Button>();
        toggleButton.targetGraphic = toggleImage;
        toggleButton.onClick.AddListener(ToggleExpanded);

        GameObject toggleLabelObject = new GameObject("Text");
        toggleLabelObject.transform.SetParent(toggleObject.transform, false);
        RectTransform toggleLabelRect = toggleLabelObject.AddComponent<RectTransform>();
        toggleLabelRect.anchorMin = Vector2.zero;
        toggleLabelRect.anchorMax = Vector2.one;
        toggleLabelRect.offsetMin = new Vector2(12.8f, 0f);
        toggleLabelRect.offsetMax = new Vector2(-12.8f, 0f);

        Text toggleLabel = toggleLabelObject.AddComponent<Text>();
        toggleLabel.text = "Missão  Q";
        toggleLabel.font = GetDefaultFont();
        toggleLabel.fontStyle = FontStyle.Bold;
        toggleLabel.fontSize = 16;
        toggleLabel.alignment = TextAnchor.MiddleLeft;
        toggleLabel.color = Color.white;

        expandedContent = new GameObject("ExpandedContent");
        expandedContent.transform.SetParent(panelObject.transform, false);
        expandedContentCanvasGroup = expandedContent.AddComponent<CanvasGroup>();
        expandedContentCanvasGroup.alpha = isExpanded ? 1f : 0f;
        expandedContentCanvasGroup.blocksRaycasts = isExpanded;
        expandedContentCanvasGroup.interactable = isExpanded;

        RectTransform expandedRect = expandedContent.AddComponent<RectTransform>();
        expandedRect.anchorMin = Vector2.zero;
        expandedRect.anchorMax = Vector2.one;
        expandedRect.offsetMin = Vector2.zero;
        expandedRect.offsetMax = new Vector2(0f, -33.6f);

        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(expandedContent.transform, false);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -9.6f);
        titleRect.sizeDelta = new Vector2(-19.2f, 27.2f);

        titleLabel = titleObject.AddComponent<Text>();
        titleLabel.font = GetDefaultFont();
        titleLabel.fontStyle = FontStyle.Bold;
        titleLabel.fontSize = 19;
        titleLabel.alignment = TextAnchor.MiddleCenter;
        titleLabel.color = Color.white;

        titleLeftAccent = CreateTitleAccent(expandedContent.transform, "TitleAccentLeft");
        titleRightAccent = CreateTitleAccent(expandedContent.transform, "TitleAccentRight");

        GameObject tasksObject = new GameObject("Tasks");
        tasksObject.transform.SetParent(expandedContent.transform, false);
        taskListRoot = tasksObject.AddComponent<RectTransform>();
        taskListRoot.anchorMin = Vector2.zero;
        taskListRoot.anchorMax = Vector2.one;
        taskListRoot.offsetMin = new Vector2(0f, 9.6f);
        taskListRoot.offsetMax = new Vector2(0f, -44.8f);

        VerticalLayoutGroup layout = tasksObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 3.2f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return panelObject;
    }

    private void EnsurePanelVisualStyle(Transform panel)
    {
        if (panel == null) return;

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0f, 0f, 0f, panelOpacity);
            panelImage.sprite = GetRoundedPanelSprite();
            panelImage.type = Image.Type.Sliced;
        }

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null) outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.88f, 0.95f, 1f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Image toggleImage = panel.Find("ToggleButton")?.GetComponent<Image>();
        if (toggleImage != null)
        {
            toggleImage.color = new Color(0.025f, 0.025f, 0.025f, 0.96f);
            toggleImage.sprite = GetRoundedPanelSprite();
            toggleImage.type = Image.Type.Sliced;
        }

        if (titleLabel != null) titleLabel.alignment = TextAnchor.MiddleCenter;
        Transform left = panel.Find("ExpandedContent/TitleAccentLeft");
        Transform right = panel.Find("ExpandedContent/TitleAccentRight");
        titleLeftAccent = left as RectTransform ?? CreateTitleAccent(expandedContent.transform, "TitleAccentLeft");
        titleRightAccent = right as RectTransform ?? CreateTitleAccent(expandedContent.transform, "TitleAccentRight");
    }

    private RectTransform CreateTitleAccent(Transform parent, string accentName)
    {
        GameObject accent = new GameObject(accentName);
        accent.transform.SetParent(parent, false);
        RectTransform rect = accent.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 2f);
        Image image = accent.AddComponent<Image>();
        image.color = new Color(0.1f, 0.68f, 0.95f, 0.95f);
        return rect;
    }

    private static Sprite GetRoundedPanelSprite()
    {
        if (roundedPanelSprite != null) return roundedPanelSprite;

        const int size = 32;
        const float radius = 8f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "MissionPanelRoundedTexture";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, 0f, x - (size - 1 - radius));
                float dy = Mathf.Max(radius - y, 0f, y - (size - 1 - radius));
                float alpha = Mathf.Clamp01(radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, alpha > 0f ? new Color(1f, 1f, 1f, alpha) : clear);
            }
        }
        texture.Apply();
        roundedPanelSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(8f, 8f, 8f, 8f));
        roundedPanelSprite.name = "MissionPanelRoundedSprite";
        return roundedPanelSprite;
    }

    private void CaptureScenePanelLayout()
    {
        if (capturedScenePanelLayout || panelRect == null)
        {
            return;
        }

        panelAnchorMin = panelRect.anchorMin;
        panelAnchorMax = panelRect.anchorMax;
        if (usingStage2MissionProfile)
        {
            panelAnchorMax.x = 0.48f;
        }
        panelOffsetMin = panelRect.offsetMin;
        panelOffsetMax = panelRect.offsetMax;
        capturedScenePanelLayout = true;
    }

    private void RefreshUi()
    {
        EnsureUi();
        if (currentMission == null)
        {
            return;
        }

        lastUiStateSignature = BuildUiStateSignature();
        titleLabel.text = currentMission.Title;
        UpdateDynamicPanelLayout();
        ExpandForDelay();

        for (int i = taskListRoot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediateSafe(taskListRoot.GetChild(i).gameObject);
        }

        foreach (MissionTask task in currentMission.Tasks)
        {
            CreateTaskRow(task);
        }
        UpdateDynamicPanelLayout();
    }

    private void UpdateDynamicPanelLayout()
    {
        if (panelRect == null || currentMission == null || canvas == null) return;

        float spacing = 3.2f;
        float desiredHeight = 33.6f + 46.4f + 19.2f + currentMission.Tasks.Count * taskRowHeight
            + Mathf.Max(0, currentMission.Tasks.Count - 1) * spacing;
        RectTransform canvasRect = canvas.transform as RectTransform;
        float canvasHeight = canvasRect != null ? canvasRect.rect.height : Screen.height;
        float availableHeight = Mathf.Max(1f, canvasHeight * panelAnchorMax.y - 9.6f);
        desiredHeight = Mathf.Clamp(desiredHeight, 120f, availableHeight);

        Vector2 dynamicMin = panelAnchorMin;
        dynamicMin.y = panelAnchorMax.y - desiredHeight / Mathf.Max(canvasHeight, 1f);
        panelAnchorMin = dynamicMin;

        float titleWidth = titleLabel != null ? titleLabel.preferredWidth : 80f;
        float halfTitleWidth = titleWidth * 0.5f;
        float gap = 9.6f;
        float sideMargin = 14.4f;
        float centerY = -23.2f;
        if (titleLeftAccent != null)
        {
            titleLeftAccent.anchorMin = new Vector2(0f, 1f);
            titleLeftAccent.anchorMax = new Vector2(0.5f, 1f);
            titleLeftAccent.pivot = new Vector2(0.5f, 0.5f);
            titleLeftAccent.offsetMin = new Vector2(sideMargin, centerY - 1f);
            titleLeftAccent.offsetMax = new Vector2(-(halfTitleWidth + gap), centerY + 1f);
        }
        if (titleRightAccent != null)
        {
            titleRightAccent.anchorMin = new Vector2(0.5f, 1f);
            titleRightAccent.anchorMax = new Vector2(1f, 1f);
            titleRightAccent.pivot = new Vector2(0.5f, 0.5f);
            titleRightAccent.offsetMin = new Vector2(halfTitleWidth + gap, centerY - 1f);
            titleRightAccent.offsetMax = new Vector2(-sideMargin, centerY + 1f);
        }

        if (isExpanded) ApplyExpandedState();
    }

    private void RefreshUiIfStateChanged(bool expandPanel)
    {
        if (currentMission == null)
        {
            return;
        }

        string nextSignature = BuildUiStateSignature();
        if (nextSignature == lastUiStateSignature)
        {
            return;
        }

        if (expandPanel)
        {
            ExpandForDelay();
        }

        RefreshUi();
    }

    private string BuildUiStateSignature()
    {
        if (currentMission == null)
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(128);
        builder.Append(currentMission.Number);
        builder.Append('|');
        builder.Append(currentMission.Title);
        if (currentMission.Tasks != null)
        {
            for (int i = 0; i < currentMission.Tasks.Count; i++)
            {
                MissionTask task = currentMission.Tasks[i];
                if (task == null)
                {
                    continue;
                }

                builder.Append('|');
                builder.Append(task.Id);
                builder.Append(':');
                builder.Append(task.IsComplete ? '1' : '0');
                builder.Append(':');
                builder.Append(GetTaskDisplayDescription(task));
            }
        }

        return builder.ToString();
    }

    private void CreateTaskRow(MissionTask task)
    {
        if (taskRowTemplate == null)
        {
            Debug.LogError(
                "MissionManager: atribua o Mission Task Row Template no Inspector para exibir as tarefas.",
                this);
            return;
        }

        GameObject rowObject = Instantiate(taskRowTemplate, taskListRoot, false);
        rowObject.name = "Task_" + task.Id;
        rowObject.SetActive(true);

        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        if (rowRect != null) rowRect.sizeDelta = new Vector2(rowRect.sizeDelta.x, taskRowHeight);

        LayoutElement layout = rowObject.GetComponent<LayoutElement>();
        if (layout == null) layout = rowObject.AddComponent<LayoutElement>();
        layout.minHeight = taskRowHeight;
        layout.preferredHeight = taskRowHeight;

        Image checkboxBorder = rowObject.transform.Find("Checkbox")?.GetComponent<Image>();
        if (checkboxBorder != null)
        {
            checkboxBorder.color = task.IsComplete ? completedCheckColor : incompleteCircleColor;
        }

        Image checkmark = rowObject.transform.Find("Checkbox/Checkmark")?.GetComponent<Image>();
        if (checkmark != null)
        {
            checkmark.color = completedCheckColor;
            checkmark.gameObject.SetActive(task.IsComplete);
            checkmark.transform.SetAsLastSibling();
        }

        Text taskText = rowObject.transform.Find("Text")?.GetComponent<Text>();
        if (taskText == null)
        {
            Debug.LogError("Mission Task Row Template precisa de um filho Text com componente Text.", this);
            return;
        }
        taskText.fontSize = taskFontSize;
        taskText.color = task.IsComplete ? completedTaskTextColor : Color.white;
        taskText.text = GetTaskDisplayDescription(task);
    }

    private string GetTaskDisplayDescription(MissionTask task)
    {
        if (task == null)
        {
            return string.Empty;
        }

        if (usingStage2MissionProfile && task.Id == "fabrica_bracos_roboticos")
        {
            return task.Description + "  " + stage2RoboticArmOperatingCount + "/3";
        }

        if (usingStage2MissionProfile && task.Id == "fabrica_limpar_entulhos_garra")
        {
            return task.Description + "  " + stage2ScrapConsumedCount + "/" + stage2ScrapTotalCount;
        }

        if (usingStage2MissionProfile && task.Id == "fabrica_pallets_esteira_empilhadeira")
        {
            return task.Description + "  " + stage2PalletsPlacedCount + "/4";
        }

        if (usingStage2MissionProfile && task.Id == "fabrica_pallets_gerados_enviados")
        {
            return task.Description + "  " + stage2MachinePalletsSentCount + "/3";
        }

        if (!usingStage2MissionProfile && task.Id == "sala2_configurar_ip_portas")
        {
            return task.Description + "  " + sala2ConfiguredDoorDeviceCount + "/2";
        }

        return task.Description;
    }

    private void ExpandForDelay()
    {
        if (stagePresentationHasPriority)
        {
            return;
        }

        SetExpanded(true);
        collapseAtTime = Time.unscaledTime + autoCollapseDelay;
    }

    private void ToggleExpanded()
    {
        if (stagePresentationHasPriority)
        {
            return;
        }

        SetExpanded(!isExpanded);
        if (isExpanded)
        {
            collapseAtTime = Time.unscaledTime + autoCollapseDelay;
        }
    }

    private void SetExpanded(bool expanded)
    {
        if (stagePresentationHasPriority && expanded)
        {
            return;
        }

        if (isExpanded == expanded)
        {
            return;
        }

        isExpanded = expanded;
        ApplyExpandedState();
    }

    private void ApplyExpandedState()
    {
        if (panelRect != null)
        {
            panelRect.anchorMin = isExpanded ? ResolveExpandedPanelAnchorMin() : collapsedPanelAnchorMin;
            panelRect.anchorMax = isExpanded ? panelAnchorMax : collapsedPanelAnchorMax;
            panelRect.offsetMin = isExpanded ? panelOffsetMin : collapsedPanelOffsetMin;
            panelRect.offsetMax = isExpanded ? panelOffsetMax : collapsedPanelOffsetMax;
        }

        if (expandedContent != null)
        {
            expandedContent.SetActive(true);
        }

        fadeTarget = isExpanded ? 1f : 0f;
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.interactable = true;
        }

        if (expandedContentCanvasGroup != null)
        {
            expandedContentCanvasGroup.blocksRaycasts = isExpanded;
            expandedContentCanvasGroup.interactable = isExpanded;
            if (!Application.isPlaying)
            {
                expandedContentCanvasGroup.alpha = fadeTarget;
            }
        }
    }

    private Vector2 ResolveExpandedPanelAnchorMin()
    {
        return panelAnchorMin;
    }

    private void UpdateFade()
    {
        if (expandedContentCanvasGroup == null)
        {
            return;
        }

        float duration = Mathf.Max(fadeDuration, 0.01f);
        expandedContentCanvasGroup.alpha = Mathf.MoveTowards(expandedContentCanvasGroup.alpha, fadeTarget, Time.unscaledDeltaTime / duration);

        if (expandedContent != null)
        {
            expandedContent.SetActive(isExpanded || expandedContentCanvasGroup.alpha > 0.01f);
        }
    }

    private void EnsureEventSystem()
    {
        RuntimeEventSystemUtility.EnsureSingleEventSystem();
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

    private void DestroyImmediateSafe(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}

[DisallowMultipleComponent]
public sealed class RoomProgressionDoorLock : MonoBehaviour
{
    [SerializeField] private float closeSpeed = 240f;

    private Quaternion closedLocalRotation;
    private bool isLocked;

    public bool IsLocked => isLocked;

    public void Lock(Quaternion targetClosedLocalRotation)
    {
        closedLocalRotation = targetClosedLocalRotation;
        isLocked = true;

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider doorCollider in colliders)
        {
            if (doorCollider != null && !doorCollider.isTrigger)
            {
                doorCollider.enabled = true;
            }
        }
    }

    public void Unlock()
    {
        isLocked = false;
    }

    private void LateUpdate()
    {
        if (!isLocked)
        {
            return;
        }

        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            closedLocalRotation,
            closeSpeed * Time.deltaTime);
    }
}
