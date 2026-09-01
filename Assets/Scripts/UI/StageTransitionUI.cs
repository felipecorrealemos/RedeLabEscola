using System.Collections;
using RedeLabEscola.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class StageTransitionUI : MonoBehaviour
{
    [Header("Conteudo da fase")]
    [SerializeField] private string stageLabel = "Estágio 1";
    [SerializeField] private string stageName = "O escritório";
    [SerializeField] private string nextSceneName = SceneNames.Factory;

    [Header("Elementos editaveis da cena")]
    [SerializeField] private CanvasGroup announcementGroup;
    [SerializeField] private Text stageLabelText;
    [SerializeField] private Text stageNameText;
    [SerializeField] private Text statusText;
    [SerializeField] private CanvasGroup loadingGroup;

    [Header("Pré-visualização no Editor")]
    [Tooltip("Exibe somente a tela preta de carregamento fora do Play Mode para permitir ajustes visuais.")]
    [SerializeField] private bool showLoadingScreenInEditMode;
    [Tooltip("Margem em pixels além dos limites da tela para impedir frestas por arredondamento ou escala.")]
    [SerializeField, Min(0f)] private float loadingScreenOverscan = 2f;

    [Header("Encerramento da versão de teste")]
    [SerializeField] private string mainMenuSceneName = SceneNames.MainMenu;
    [TextArea(3, 7)]
    [SerializeField] private string testVersionThankYouMessage =
        "Obrigado por participar da experiência Rede Lab Escola!\n\n" +
        "Esta versão de teste ainda está em desenvolvimento e foi criada para unir aprendizado e diversão, " +
        "transformando conhecimentos de tecnologia e redes em uma experiência para jogar, explorar e aprender.\n\n" +
        "Sua participação faz parte da construção deste projeto.";

    [Header("Tempos")]
    [Min(0f)] [SerializeField] private float fadeInDuration = 0.55f;
    [Min(0f)] [SerializeField] private float introHoldDuration = 1.8f;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.65f;
    [Min(0f)] [SerializeField] private float completionHoldDuration = 2.3f;
    [Min(5f)] [SerializeField] private float minimumLoadingDuration = 5f;
    [Min(3f)] [SerializeField] private float sceneRevealDuration = 3f;
    [Min(0.1f)] [SerializeField] private float sceneFadeToBlackDuration = 1.5f;
    [Min(0f)] [SerializeField] private float missionAutoOpenDelay = 1.25f;

    [Header("Comemoracao (opcional)")]
    [SerializeField] private AudioSource celebrationAudioSource;
    [SerializeField] private AudioClip celebrationClip;

    private bool transitionStarted;
    [SerializeField, HideInInspector] private int portugueseTextVersion;
    [SerializeField, HideInInspector] private int fullScreenLayoutVersion;

    public bool TransitionStarted => transitionStarted;
    public static bool HasPresentationPriority { get; private set; }

    private void Awake()
    {
        ApplyPortugueseTextMigration();
        NormalizeFullScreenLayout();
        Canvas stageCanvas = GetComponent<Canvas>();
        if (stageCanvas != null) stageCanvas.enabled = true;
        HasPresentationPriority = true;
        SetCanvasGroupImmediate(announcementGroup, 0f);
        SetCanvasGroupImmediate(loadingGroup, 0f);
    }

    private void OnDestroy()
    {
        HasPresentationPriority = false;
    }

    private void OnValidate()
    {
        ApplyPortugueseTextMigration();
        ApplyFullScreenLayoutMigration();
        NormalizeFullScreenLayout();
        Canvas stageCanvas = GetComponent<Canvas>();
        if (!Application.isPlaying && stageCanvas != null)
        {
            stageCanvas.enabled = showLoadingScreenInEditMode;
        }

        if (!Application.isPlaying)
        {
            if (announcementGroup != null && announcementGroup.gameObject.activeSelf)
            {
                announcementGroup.gameObject.SetActive(false);
            }

            if (loadingGroup != null)
            {
                loadingGroup.alpha = 1f;
                loadingGroup.interactable = false;
                loadingGroup.blocksRaycasts = false;
                if (loadingGroup.gameObject.activeSelf != showLoadingScreenInEditMode)
                {
                    loadingGroup.gameObject.SetActive(showLoadingScreenInEditMode);
                }
            }
        }
    }

    private void ApplyFullScreenLayoutMigration()
    {
        if (fullScreenLayoutVersion >= 1) return;
        showLoadingScreenInEditMode = gameObject.scene.IsValid() && gameObject.scene.name == SceneNames.Factory;
        fullScreenLayoutVersion = 1;
    }

    private void NormalizeFullScreenLayout()
    {
        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect != null)
        {
            canvasRect.localScale = Vector3.one;
            canvasRect.localRotation = Quaternion.identity;
        }

        if (loadingGroup == null) return;
        RectTransform loadingRect = loadingGroup.transform as RectTransform;
        if (loadingRect == null) return;
        loadingRect.anchorMin = Vector2.zero;
        loadingRect.anchorMax = Vector2.one;
        loadingRect.pivot = new Vector2(0.5f, 0.5f);
        loadingRect.anchoredPosition = Vector2.zero;
        loadingRect.sizeDelta = Vector2.zero;
        Vector2 overscan = Vector2.one * Mathf.Max(0f, loadingScreenOverscan);
        loadingRect.offsetMin = -overscan;
        loadingRect.offsetMax = overscan;
        loadingRect.localScale = Vector3.one;
        loadingRect.localRotation = Quaternion.identity;

        Image loadingBackground = loadingGroup.GetComponent<Image>();
        if (loadingBackground != null)
        {
            // Sprites arredondados/sliced possuem pixels transparentes nas bordas.
            // Uma Image sem sprite produz um retângulo sólido que cobre toda a tela.
            loadingBackground.sprite = null;
            loadingBackground.type = Image.Type.Simple;
            loadingBackground.preserveAspect = false;
            loadingBackground.color = Color.black;
        }
    }

    private IEnumerator Start()
    {
        SetMissionUiPriority(true);
        ApplyText(false);
        PrepareBlackOverlay(false);
        loadingGroup.transform.SetAsFirstSibling();
        SetCanvasGroupImmediate(loadingGroup, 1f);
        SetCanvasGroupImmediate(announcementGroup, 0f);
        yield return Fade(announcementGroup, 0f, 1f, fadeInDuration);
        yield return Fade(loadingGroup, 1f, 0f, Mathf.Max(3f, sceneRevealDuration));
        yield return WaitRealtime(introHoldDuration);
        yield return Fade(announcementGroup, 1f, 0f, fadeOutDuration);
        ReleaseMissionUiAfterIntro();
    }

    public bool TryCompleteStage(bool bypassMissionRequirements = false)
    {
        if (transitionStarted)
        {
            return false;
        }

        MissionManager manager = MissionManager.Instance;
        bool hasIncompleteMission = manager != null && !manager.AreAllCurrentTasksComplete;
        if (!bypassMissionRequirements && hasIncompleteMission)
        {
            Debug.Log("A saída da fase ainda está bloqueada: existem tarefas pendentes.", this);
            return false;
        }

        // O sinalizador apenas autoriza pular tarefas pendentes durante desenvolvimento.
        // Uma conclusão normal não pode desligar a sessão online só porque o campo está
        // habilitado no Inspector.
        if (bypassMissionRequirements && hasIncompleteMission)
        {
            RedeLabEscola.Auth.RedeLabProgressService.Instance?.DisablePersistenceForDebugBypass();
        }

        transitionStarted = true;
        HasPresentationPriority = true;
        SetMissionUiPriority(true);
        PrepareVehiclesForGameplayLock();
        LockGameplayAndHideOtherUi();
        StopAllCoroutines();
        StartCoroutine(CompleteAndLoadNextStage());
        return true;
    }

    private IEnumerator CompleteAndLoadNextStage()
    {
        ApplyText(true);
        SetCanvasGroupImmediate(announcementGroup, 0f);
        yield return Fade(announcementGroup, 0f, 1f, fadeInDuration);

        if (celebrationAudioSource != null && celebrationClip != null)
        {
            celebrationAudioSource.PlayOneShot(celebrationClip);
        }

        yield return WaitRealtime(completionHoldDuration);

        // Uma fase sem destino configurado encerra a versao jogavel atual.
        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            yield return ShowTestVersionThankYou();
            yield break;
        }

        PrepareBlackOverlay(false);
        AudioManager.FadeOutMusic(Mathf.Max(0.1f, sceneFadeToBlackDuration));
        yield return FadePair(
            announcementGroup, 1f, 0f,
            loadingGroup, 0f, 1f,
            Mathf.Max(0.1f, sceneFadeToBlackDuration));
        PrepareBlackOverlay(true);

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(nextSceneName);
        if (loadOperation == null)
        {
            Debug.LogError($"Não foi possível carregar a cena '{nextSceneName}'. Confira o nome e o Build Settings.", this);
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        float loadingStartedAt = Time.realtimeSinceStartup;
        float requiredLoadingDuration = Mathf.Max(5f, minimumLoadingDuration);
        while (loadOperation.progress < 0.9f || Time.realtimeSinceStartup - loadingStartedAt < requiredLoadingDuration)
        {
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
    }

    private IEnumerator ShowTestVersionThankYou()
    {
        PrepareBlackOverlay(false);
        CanvasGroup thankYouGroup = CreateThankYouPanel();
        SetCanvasGroupImmediate(thankYouGroup, 0f);

        AudioManager.FadeOutMusic(Mathf.Max(0.35f, sceneFadeToBlackDuration));
        yield return FadePair(
            announcementGroup, 1f, 0f,
            loadingGroup, 0f, 0.86f,
            Mathf.Max(0.35f, sceneFadeToBlackDuration));

        yield return Fade(thankYouGroup, 0f, 1f, fadeInDuration);
        thankYouGroup.interactable = true;
        thankYouGroup.blocksRaycasts = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private CanvasGroup CreateThankYouPanel()
    {
        Transform existing = transform.Find("TestVersionThankYouPanel");
        if (existing != null)
        {
            return existing.GetComponent<CanvasGroup>();
        }

        GameObject root = new GameObject("TestVersionThankYouPanel", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        root.transform.SetAsLastSibling();
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        GameObject card = new GameObject("ThankYouCard", typeof(RectTransform), typeof(Image), typeof(Outline));
        card.transform.SetParent(root.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(1160f, 700f);
        Image cardImage = card.GetComponent<Image>();
        cardImage.color = new Color(0.12f, 0.14f, 0.16f, 0.97f);
        Outline cardOutline = card.GetComponent<Outline>();
        cardOutline.effectColor = new Color(0.75f, 0.88f, 0.96f, 0.9f);
        cardOutline.effectDistance = new Vector2(2f, -2f);

        Text title = CreateEndText(card.transform, "Title", "Obrigado por jogar!", 38, FontStyle.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -58f);
        titleRect.sizeDelta = new Vector2(-70f, 58f);

        Text message = CreateEndText(card.transform, "Message", testVersionThankYouMessage, 22, FontStyle.Normal);
        RectTransform messageRect = message.rectTransform;
        messageRect.anchorMin = new Vector2(0f, 0f);
        messageRect.anchorMax = new Vector2(0.5f, 1f);
        messageRect.offsetMin = new Vector2(54f, 128f);
        messageRect.offsetMax = new Vector2(-38f, -132f);
        message.alignment = TextAnchor.MiddleCenter;
        message.horizontalOverflow = HorizontalWrapMode.Wrap;
        message.verticalOverflow = VerticalWrapMode.Overflow;

        GameObject buttonObject = new GameObject("BackToMenuButton", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(card.transform, false);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0f);
        buttonRect.anchorMax = new Vector2(0.5f, 0f);
        buttonRect.pivot = new Vector2(0.5f, 0f);
        buttonRect.anchoredPosition = new Vector2(-290f, 42f);
        buttonRect.sizeDelta = new Vector2(260f, 58f);
        Image buttonImage = buttonObject.GetComponent<Image>();
        buttonImage.color = new Color(0.12f, 0.58f, 0.82f, 1f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonImage;
        button.onClick.AddListener(ReturnToMainMenu);

        Text buttonLabel = CreateEndText(buttonObject.transform, "Text", "Voltar ao menu", 22, FontStyle.Bold);
        buttonLabel.rectTransform.anchorMin = Vector2.zero;
        buttonLabel.rectTransform.anchorMax = Vector2.one;
        buttonLabel.rectTransform.offsetMin = Vector2.zero;
        buttonLabel.rectTransform.offsetMax = Vector2.zero;

        RedeLabFeedbackPanel feedbackPanel = card.AddComponent<RedeLabFeedbackPanel>();
        feedbackPanel.Build();

        RuntimeEventSystemUtility.EnsureSingleEventSystem();
        return root.GetComponent<CanvasGroup>();
    }

    private static Text CreateEndText(Transform parent, string objectName, string content, int fontSize, FontStyle style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text label = textObject.GetComponent<Text>();
        label.text = content;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.raycastTarget = false;
        return label;
    }

    private void ReturnToMainMenu()
    {
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            StartCoroutine(FadeOutAndReturnToMainMenu());
        }
    }

    private IEnumerator FadeOutAndReturnToMainMenu()
    {
        CanvasGroup thankYouGroup = transform.Find("TestVersionThankYouPanel")?.GetComponent<CanvasGroup>();
        if (thankYouGroup != null)
        {
            thankYouGroup.interactable = false;
            thankYouGroup.blocksRaycasts = true;
        }

        PrepareBlackOverlay(true);
        yield return FadePair(
            thankYouGroup, 1f, 0f,
            loadingGroup, loadingGroup != null ? loadingGroup.alpha : 0.86f, 1f,
            Mathf.Max(0.35f, sceneFadeToBlackDuration));

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(mainMenuSceneName);
        if (loadOperation == null)
        {
            Debug.LogError($"Não foi possível carregar a cena '{mainMenuSceneName}'.", this);
            yield break;
        }

        loadOperation.allowSceneActivation = false;
        float loadingStartedAt = Time.realtimeSinceStartup;
        float requiredLoadingDuration = Mathf.Max(2f, minimumLoadingDuration);
        while (loadOperation.progress < 0.9f || Time.realtimeSinceStartup - loadingStartedAt < requiredLoadingDuration)
        {
            yield return null;
        }

        loadOperation.allowSceneActivation = true;
    }

    private void ApplyText(bool completed)
    {
        if (stageLabelText != null) stageLabelText.text = stageLabel;
        if (stageNameText != null) stageNameText.text = stageName;
        if (statusText != null) statusText.text = completed ? "Missão concluída" : string.Empty;
    }

    private void ApplyPortugueseTextMigration()
    {
        if (portugueseTextVersion >= 1) return;
        bool isFactory = gameObject.scene.IsValid() && gameObject.scene.name == SceneNames.Factory;
        stageLabel = isFactory ? "Estágio 2" : "Estágio 1";
        stageName = isFactory ? "A fábrica" : "O escritório";
        portugueseTextVersion = 1;
    }

    private void PrepareBlackOverlay(bool showLoadingLabel)
    {
        if (loadingGroup == null)
        {
            return;
        }

        Transform loadingLabel = loadingGroup.transform.Find("LoadingLabel_EDITAR");
        if (loadingLabel != null)
        {
            loadingLabel.gameObject.SetActive(showLoadingLabel);
        }
    }

    private void ReleaseMissionUiAfterIntro()
    {
        HasPresentationPriority = false;
        MissionManager manager = ResolveMissionManager();
        if (manager != null)
        {
            manager.SetStagePresentationPriority(false, true, missionAutoOpenDelay);
        }
    }

    private void SetMissionUiPriority(bool hasPriority)
    {
        MissionManager manager = ResolveMissionManager();
        if (manager != null)
        {
            manager.SetStagePresentationPriority(hasPriority);
        }
    }

    private static MissionManager ResolveMissionManager()
    {
        return MissionManager.Instance != null
            ? MissionManager.Instance
            : FindObjectOfType<MissionManager>(true);
    }

    private void LockGameplayAndHideOtherUi()
    {
        PlayerTopDownController[] players = FindObjectsOfType<PlayerTopDownController>(true);
        foreach (PlayerTopDownController player in players)
        {
            if (player != null)
            {
                player.SetExternalMovementLocked(true);
            }
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        foreach (Canvas candidate in canvases)
        {
            bool belongsToStagePresentation = candidate != null
                && (candidate.transform == transform || candidate.transform.IsChildOf(transform));
            if (candidate == null || belongsToStagePresentation)
            {
                continue;
            }

            candidate.enabled = false;
        }
    }

    private static void PrepareVehiclesForGameplayLock()
    {
        EmpilhadeiraController[] forklifts = FindObjectsOfType<EmpilhadeiraController>(true);
        foreach (EmpilhadeiraController forklift in forklifts)
        {
            forklift?.PrepareForStageCompletion();
        }
    }

    private static IEnumerator Fade(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null) yield break;
        if (duration <= 0f)
        {
            SetCanvasGroupImmediate(group, to);
            yield break;
        }

        group.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SetCanvasGroupImmediate(group, to);
    }

    private static IEnumerator FadePair(
        CanvasGroup first, float firstFrom, float firstTo,
        CanvasGroup second, float secondFrom, float secondTo,
        float duration)
    {
        if (first != null) first.gameObject.SetActive(true);
        if (second != null) second.gameObject.SetActive(true);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            if (first != null) first.alpha = Mathf.Lerp(firstFrom, firstTo, progress);
            if (second != null) second.alpha = Mathf.Lerp(secondFrom, secondTo, progress);
            yield return null;
        }

        SetCanvasGroupImmediate(first, firstTo);
        SetCanvasGroupImmediate(second, secondTo);
    }

    private static IEnumerator WaitRealtime(float duration)
    {
        if (duration > 0f) yield return new WaitForSecondsRealtime(duration);
    }

    private static void SetCanvasGroupImmediate(CanvasGroup group, float alpha)
    {
        if (group == null) return;
        group.alpha = alpha;
        group.interactable = false;
        group.blocksRaycasts = alpha > 0.01f;
        group.gameObject.SetActive(alpha > 0.001f);
    }
}
