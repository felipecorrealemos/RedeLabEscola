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
    [SerializeField] private Vector2 panelAnchorMin = new Vector2(0.02f, 0.42f);
    [SerializeField] private Vector2 panelAnchorMax = new Vector2(0.42f, 0.96f);
    [SerializeField] private Vector2 collapsedPanelAnchorMin = new Vector2(0.02f, 0.90f);
    [SerializeField] private Vector2 collapsedPanelAnchorMax = new Vector2(0.22f, 0.96f);
    [SerializeField] private float panelOpacity = 0.78f;
    [SerializeField] private float taskRowHeight = 46f;
    [SerializeField] private int taskFontSize = 18;
    [SerializeField] private float taskTextPaddingLeft = 50f;
    [SerializeField] private float taskTextPaddingRight = 50f;
    [SerializeField] private float autoCollapseDelay = 5f;
    [SerializeField] private bool updateMissionFromNearestRouter = true;
    [SerializeField] private float routerMissionDetectionRadius = 8f;
    [SerializeField] private float missionAreaRefreshInterval = 1f;
    [SerializeField] private float fadeDuration = 0.18f;

    private readonly Dictionary<int, Mission> missionsByNumber = new Dictionary<int, Mission>();
    private Mission currentMission;
    private bool isExpanded = true;
    private float collapseAtTime;
    private float nextMissionAreaRefreshTime;
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
        if (!IsGameplayScene(scene))
        {
            DestroySceneMissionUi();
            return;
        }

        if (Instance != null || FindObjectOfType<MissionManager>() != null)
        {
            return;
        }

        new GameObject("MissionManager").AddComponent<MissionManager>();
    }

    private void Awake()
    {
        if (!IsGameplayScene(SceneManager.GetActiveScene()))
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
        EnsureDefaultMissions();
        RebuildMissionLookup();
        EnsureUi();
        SetMission(startingMissionNumber);
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
        EnsureDefaultMissions();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            ToggleExpanded();
        }

        RefreshMissionFromNearestRouter();

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

        if (currentMission != mission)
        {
            ResetMission(mission);
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
        if (currentMission == null || string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        MissionTask task = currentMission.Tasks.Find(candidate => candidate.Id == taskId);
        if (task == null || task.IsComplete == complete)
        {
            return;
        }

        task.IsComplete = complete;
        RefreshUi();
    }

    public static void CompleteCurrentTask(string taskId)
    {
        Instance?.CompleteTask(taskId);
    }

    public static void NotifyDevicePlaced(MovableDevice device)
    {
        if (Instance == null || device == null)
        {
            return;
        }

        if (!device.IsComputerDevice())
        {
            return;
        }

        if (Instance.CurrentMissionNumber == 1)
        {
            Instance.SetTaskCompletion("sala1_colocar_gabinete", true);
        }
        else if (Instance.CurrentMissionNumber == 2)
        {
            Instance.SetTaskCompletion("sala2_colocar_gabinete", true);
        }
    }

    public static void NotifyDeviceRemoved(MovableDevice device)
    {
        if (Instance == null || device == null || !device.IsComputerDevice())
        {
            return;
        }

        if (Instance.CurrentMissionNumber == 1)
        {
            Instance.SetTaskCompletion("sala1_colocar_gabinete", false);
        }
        else if (Instance.CurrentMissionNumber == 2)
        {
            Instance.SetTaskCompletion("sala2_colocar_gabinete", false);
        }
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
        bool isLeftDoor = lowerTitle.Contains("esquerda") || lowerTitle.Contains("left") || lowerName.Contains("esquerda") || lowerName.Contains("left");
        bool isRightDoor = lowerTitle.Contains("direita") || lowerTitle.Contains("right") || lowerName.Contains("direita") || lowerName.Contains("right");

        if (Instance.CurrentMissionNumber == 1 && isComputer)
        {
            Instance.SetTaskCompletion("sala1_configurar_ip_pc", device.IsNetworkOperational);
            return;
        }

        if (Instance.CurrentMissionNumber != 2)
        {
            return;
        }

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
    }

    public static void NotifySingleDoorOpened()
    {
        NotifySingleDoorStateChanged(true);
    }

    public static void NotifySingleDoorStateChanged(bool open)
    {
        if (Instance != null && Instance.CurrentMissionNumber == 1)
        {
            Instance.SetTaskCompletion("sala1_abrir_porta", open);
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

        EnsureMission(3, "Sala 3", new MissionTask[0]);
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

    private void ResetMission(Mission mission)
    {
        if (mission == null)
        {
            return;
        }

        foreach (MissionTask task in mission.Tasks)
        {
            task.IsComplete = false;
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

    private static bool IsGameplayScene(Scene scene)
    {
        return scene.IsValid() && scene.name == GameplaySceneName;
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
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

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
        layout.spacing = 6f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        return panelObject;
    }

    private void RefreshUi()
    {
        EnsureUi();
        if (currentMission == null)
        {
            return;
        }

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
        markerRect.anchoredPosition = new Vector2(24f, 0f);
        markerRect.sizeDelta = new Vector2(18f, 18f);

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
        taskText.text = task.Description;
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
            panelRect.anchorMin = isExpanded ? panelAnchorMin : collapsedPanelAnchorMin;
            panelRect.anchorMax = isExpanded ? panelAnchorMax : collapsedPanelAnchorMax;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
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
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
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
