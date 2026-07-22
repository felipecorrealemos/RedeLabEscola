using System.Collections;
using System.Collections.Generic;
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
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.02f, 0.34f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.42f, 0.96f);
    [SerializeField] private Vector2 panelOffsetMin = Vector2.zero;
    [SerializeField] private Vector2 panelOffsetMax = Vector2.zero;
    [SerializeField] private Vector2 collapsedPanelAnchorMin = new Vector2(0.02f, 0.90f);
    [SerializeField] private Vector2 collapsedPanelAnchorMax = new Vector2(0.22f, 0.96f);
    [SerializeField] private Vector2 collapsedPanelOffsetMin = Vector2.zero;
    [SerializeField] private Vector2 collapsedPanelOffsetMax = Vector2.zero;
    [SerializeField] private float panelOpacity = 0.78f;
    [SerializeField] private float taskRowHeight = 34f;
    [SerializeField] private int taskFontSize = 13;
    [SerializeField] private float taskTextPaddingLeft = 42f;
    [SerializeField] private float taskTextPaddingRight = 18f;
    [SerializeField] private float minimumExpandedPanelHeight = 430f;
    [SerializeField] private float autoCollapseDelay = 5f;
    [SerializeField] private bool updateMissionFromNearestRouter = true;
    [SerializeField] private float routerMissionDetectionRadius = 8f;
    [SerializeField] private float missionAreaRefreshInterval = 1f;
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float stage2MissionStateRefreshInterval = 0.5f;

    private readonly Dictionary<int, Mission> missionsByNumber = new Dictionary<int, Mission>();
    private Mission currentMission;
    private bool isExpanded = true;
    private bool usingStage2MissionProfile;
    private bool stage2RoboticArmRefreshQueued;
    private bool capturedScenePanelLayout;
    [SerializeField] private int stage2RoboticArmOperatingCount;
    private string lastUiStateSignature;
    private float collapseAtTime;
    private float nextMissionAreaRefreshTime;
    private float nextStage2MissionStateRefreshTime;
    private float fadeTarget = 1f;

    public static MissionManager Instance { get; private set; }
    public int CurrentMissionNumber => currentMission != null ? currentMission.Number : 0;

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

        ConfigureMissionsForActiveScene();
        RebuildMissionLookup();
        EnsureUi();
        SetMission(startingMissionNumber);

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
        ConfigureMissionsForActiveScene();
        RebuildMissionLookup();
        EnsureUi();
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
        if (Application.isPlaying)
        {
            RebuildMissionLookup();
            RefreshUiIfStateChanged(true);
            return;
        }

        ConfigureMissionsForActiveScene();
    }

    private Scene GetMissionSceneContext()
    {
        return gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ToggleExpanded();
        }

        RefreshMissionFromNearestRouter();
        RefreshStage2MissionState();
        RefreshUiIfStateChanged(false);

        if (isExpanded && autoCollapseDelay > 0f && Time.unscaledTime >= collapseAtTime)
        {
            SetExpanded(false);
        }

        UpdateFade();
    }

    public void SetMission(int missionNumber)
    {
        RebuildMissionLookup();
        if (!missionsByNumber.TryGetValue(missionNumber, out Mission mission))
        {
            Debug.LogWarning($"Mission {missionNumber} was not found.");
            return;
        }

        currentMission = mission;
        RefreshUi();
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

        task.IsComplete = complete;
        if (currentMission == mission)
        {
            ExpandForDelay();
            RefreshUi();
        }
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
        bool isLeftDoor = lowerTitle.Contains("esquerda") || lowerTitle.Contains("left") || lowerName.Contains("esquerda") || lowerName.Contains("left");
        bool isRightDoor = lowerTitle.Contains("direita") || lowerTitle.Contains("right") || lowerName.Contains("direita") || lowerName.Contains("right");

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
            else if (isLeftDoor)
            {
                Instance.SetTaskCompletion("sala2_configurar_ip_porta_esquerda", device.IsNetworkOperational);
            }
            else if (isRightDoor)
            {
                Instance.SetTaskCompletion("sala2_configurar_ip_porta_direita", device.IsNetworkOperational);
            }
            else if (isDoorDevice)
            {
                if (device.IsNetworkOperational)
                {
                    Instance.CompleteFirstIncompleteTask("sala2_configurar_ip_porta_esquerda", "sala2_configurar_ip_porta_direita");
                }
                else
                {
                    Instance.SetLastCompleteTask(false, "sala2_configurar_ip_porta_direita", "sala2_configurar_ip_porta_esquerda");
                }
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
            "sala3_entregar_documento");
    }

    public static void NotifyStage2RoboticArmOperationChanged()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.QueueStage2RoboticArmMissionRefresh();
    }

    public static void NotifyStage2ScrapConsumed()
    {
        if (Instance == null || !Instance.usingStage2MissionProfile)
        {
            return;
        }

        Instance.RefreshStage2ScrapMission();
    }

    public static void NotifyStage2PalletPlacedOnConveyor()
    {
        if (Instance == null || !Instance.usingStage2MissionProfile)
        {
            return;
        }

        Instance.RefreshStage2PalletMission();
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
        EnsureMission(1, "Fabrica", new[]
        {
            new MissionTask { Id = "fabrica_bracos_roboticos", Description = "Fazer funcionar os tres bracos roboticos" },
            new MissionTask { Id = "fabrica_limpar_entulhos_garra", Description = "Limpar entulhos com a garra pelo terminal" },
            new MissionTask { Id = "fabrica_pallets_esteira_empilhadeira", Description = "Colocar os pallets na esteira com a empilhadeira" }
        });
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
            new MissionTask { Id = "sala2_configurar_ip_porta_esquerda", Description = "Configurar o IP do dispositivo da porta esquerda" },
            new MissionTask { Id = "sala2_configurar_ip_porta_direita", Description = "Configurar o IP do dispositivo da porta direita" },
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

    private void CompleteFirstIncompleteTask(params string[] taskIds)
    {
        if (currentMission == null)
        {
            return;
        }

        foreach (string taskId in taskIds)
        {
            MissionTask task = currentMission.Tasks.Find(candidate => candidate.Id == taskId);
            if (task != null && !task.IsComplete)
            {
                CompleteTask(taskId);
                return;
            }
        }
    }

    private void SetLastCompleteTask(bool complete, params string[] taskIds)
    {
        if (currentMission == null)
        {
            return;
        }

        foreach (string taskId in taskIds)
        {
            MissionTask task = currentMission.Tasks.Find(candidate => candidate.Id == taskId);
            if (task != null && task.IsComplete != complete)
            {
                SetTaskCompletion(taskId, complete);
                return;
            }
        }
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
        RefreshStage2PalletMission();
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
        if (IsTaskComplete(1, "fabrica_limpar_entulhos_garra"))
        {
            return;
        }

        Transform scrapsRoot = FindTransformByName("entulhos");
        if (scrapsRoot != null && scrapsRoot.childCount == 0)
        {
            SetTaskCompletion(1, "fabrica_limpar_entulhos_garra", true);
        }
    }

    private void RefreshStage2PalletMission()
    {
        if (IsTaskComplete(1, "fabrica_pallets_esteira_empilhadeira"))
        {
            return;
        }

        Transform palletMachine = FindTransformByName("PalletMachine");
        Transform palletsRoot = palletMachine != null ? FindChildRecursive(palletMachine, "Pallets") : FindTransformByName("Pallets");
        if (palletsRoot != null && palletsRoot.childCount == 0)
        {
            SetTaskCompletion(1, "fabrica_pallets_esteira_empilhadeira", true);
        }
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

        if (panelAlreadyExists)
        {
            CaptureScenePanelLayout();
        }

        Text toggleLabel = panel.Find("ToggleButton/Text")?.GetComponent<Text>();
        if (toggleLabel != null)
        {
            toggleLabel.text = "Missao  Q";
        }

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
        toggleRect.sizeDelta = new Vector2(0f, 42f);

        Image toggleImage = toggleObject.AddComponent<Image>();
        toggleImage.color = new Color(0.08f, 0.08f, 0.08f, 0.94f);
        toggleButton = toggleObject.AddComponent<Button>();
        toggleButton.targetGraphic = toggleImage;
        toggleButton.onClick.AddListener(ToggleExpanded);

        GameObject toggleLabelObject = new GameObject("Text");
        toggleLabelObject.transform.SetParent(toggleObject.transform, false);
        RectTransform toggleLabelRect = toggleLabelObject.AddComponent<RectTransform>();
        toggleLabelRect.anchorMin = Vector2.zero;
        toggleLabelRect.anchorMax = Vector2.one;
        toggleLabelRect.offsetMin = new Vector2(16f, 0f);
        toggleLabelRect.offsetMax = new Vector2(-16f, 0f);

        Text toggleLabel = toggleLabelObject.AddComponent<Text>();
        toggleLabel.text = "Missao  Q";
        toggleLabel.font = GetDefaultFont();
        toggleLabel.fontStyle = FontStyle.Bold;
        toggleLabel.fontSize = 20;
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
        expandedRect.offsetMax = new Vector2(0f, -42f);

        GameObject titleObject = new GameObject("Title");
        titleObject.transform.SetParent(expandedContent.transform, false);
        RectTransform titleRect = titleObject.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -12f);
        titleRect.sizeDelta = new Vector2(-24f, 34f);

        titleLabel = titleObject.AddComponent<Text>();
        titleLabel.font = GetDefaultFont();
        titleLabel.fontStyle = FontStyle.Bold;
        titleLabel.fontSize = 24;
        titleLabel.alignment = TextAnchor.MiddleLeft;
        titleLabel.color = Color.white;

        GameObject tasksObject = new GameObject("Tasks");
        tasksObject.transform.SetParent(expandedContent.transform, false);
        taskListRoot = tasksObject.AddComponent<RectTransform>();
        taskListRoot.anchorMin = Vector2.zero;
        taskListRoot.anchorMax = Vector2.one;
        taskListRoot.offsetMin = new Vector2(0f, 12f);
        taskListRoot.offsetMax = new Vector2(0f, -56f);

        VerticalLayoutGroup layout = tasksObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.spacing = 4f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return panelObject;
    }

    private void CaptureScenePanelLayout()
    {
        if (capturedScenePanelLayout || panelRect == null)
        {
            return;
        }

        panelAnchorMin = panelRect.anchorMin;
        panelAnchorMax = panelRect.anchorMax;
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
        ExpandForDelay();

        for (int i = taskListRoot.childCount - 1; i >= 0; i--)
        {
            DestroyImmediateSafe(taskListRoot.GetChild(i).gameObject);
        }

        foreach (MissionTask task in currentMission.Tasks)
        {
            CreateTaskRow(task);
        }
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
        GameObject rowObject = new GameObject("Task_" + task.Id);
        rowObject.transform.SetParent(taskListRoot, false);

        RectTransform rowRect = rowObject.AddComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, taskRowHeight);

        LayoutElement layout = rowObject.AddComponent<LayoutElement>();
        layout.minHeight = taskRowHeight;
        layout.preferredHeight = taskRowHeight;

        GameObject markerObject = new GameObject("Marker");
        markerObject.transform.SetParent(rowObject.transform, false);
        RectTransform markerRect = markerObject.AddComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0f, 0.5f);
        markerRect.anchorMax = new Vector2(0f, 0.5f);
        markerRect.pivot = new Vector2(0.5f, 0.5f);
        markerRect.anchoredPosition = new Vector2(20f, 0f);
        markerRect.sizeDelta = new Vector2(12f, 12f);

        Image markerImage = markerObject.AddComponent<Image>();
        markerImage.color = task.IsComplete ? new Color(0.1f, 0.85f, 0.32f, 1f) : new Color(0.9f, 0.72f, 0.16f, 1f);

        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(rowObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(taskTextPaddingLeft, 0f);
        textRect.offsetMax = new Vector2(-taskTextPaddingRight, 0f);

        Text taskText = textObject.AddComponent<Text>();
        taskText.font = GetDefaultFont();
        taskText.fontSize = taskFontSize;
        taskText.alignment = TextAnchor.MiddleLeft;
        taskText.horizontalOverflow = HorizontalWrapMode.Wrap;
        taskText.verticalOverflow = VerticalWrapMode.Overflow;
        taskText.color = task.IsComplete ? new Color(0.75f, 1f, 0.8f, 1f) : Color.white;
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

        return task.Description;
    }

    private void ExpandForDelay()
    {
        SetExpanded(true);
        collapseAtTime = Time.unscaledTime + autoCollapseDelay;
    }

    private void ToggleExpanded()
    {
        SetExpanded(!isExpanded);
        if (isExpanded)
        {
            collapseAtTime = Time.unscaledTime + autoCollapseDelay;
        }
    }

    private void SetExpanded(bool expanded)
    {
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
