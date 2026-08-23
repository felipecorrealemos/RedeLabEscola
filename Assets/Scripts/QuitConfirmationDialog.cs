using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class QuitConfirmationDialog : MonoBehaviour
{
    private const string DialogCanvasName = "QuitConfirmationCanvas";

    [SerializeField] private float panelOpacity = 0.82f;
    [SerializeField] private Vector2 panelSize = new Vector2(420f, 210f);

    private Canvas canvas;
    private GameObject menuPanel;
    private GameObject confirmationPanel;
    private bool menuOpen;
    private bool confirmationOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (FindObjectOfType<QuitConfirmationDialog>() != null)
        {
            return;
        }

        GameObject dialogObject = new GameObject("QuitConfirmationDialog");
        DontDestroyOnLoad(dialogObject);
        dialogObject.AddComponent<QuitConfirmationDialog>();
    }

    private void Awake()
    {
        if (FindObjectsOfType<QuitConfirmationDialog>().Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        EnsureUi();
        CloseAll();
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (canvas != null) canvas.enabled = true;
        CloseAll();
    }

    private void LateUpdate()
    {
        if (!Input.GetKeyDown(KeyCode.Escape)) return;
        if (EscapeInputGuard.WasConsumedThisFrame) return;
        if (StageTransitionUI.HasPresentationPriority) return;

        if (confirmationOpen)
        {
            ShowMenu();
            return;
        }

        if (menuOpen)
        {
            CloseAll();
            return;
        }

        if (!IsGameplayPanelOpen())
        {
            ShowMenu();
        }
    }

    private void ShowMenu()
    {
        EnsureUi();
        menuOpen = true;
        confirmationOpen = false;
        menuPanel.SetActive(true);
        confirmationPanel.SetActive(false);
        SetPlayerInputLocked(true);
    }

    private void ShowConfirmation()
    {
        menuOpen = false;
        confirmationOpen = true;
        menuPanel.SetActive(false);
        confirmationPanel.SetActive(true);
    }

    private void CloseAll()
    {
        menuOpen = false;
        confirmationOpen = false;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        SetPlayerInputLocked(false);
    }

    private bool IsGameplayPanelOpen()
    {
        RouterInteractable[] routers = FindObjectsOfType<RouterInteractable>(true);
        foreach (RouterInteractable router in routers)
        {
            if (router != null && router.IsOpen)
            {
                return true;
            }
        }

        ComputerInteractable[] computers = FindObjectsOfType<ComputerInteractable>(true);
        foreach (ComputerInteractable computer in computers)
        {
            if (computer != null && computer.IsOpen)
            {
                return true;
            }
        }

        return false;
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureUi()
    {
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject(DialogCanvasName);
            DontDestroyOnLoad(canvasObject);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        EnsureEventSystem();

        if (menuPanel != null && confirmationPanel != null)
        {
            return;
        }

        menuPanel = CreatePanel("PauseMenuPanel", new Vector2(420f, 190f));
        CreateText(menuPanel.transform, "Title", "MENU", new Vector2(0f, 48f), new Vector2(360f, 44f), 24, FontStyle.Bold);
        CreateButton(menuPanel.transform, "QuitButton", "Sair do jogo", new Vector2(0f, -35f), ShowConfirmation, new Vector2(220f, 46f));

        confirmationPanel = CreatePanel("QuitConfirmationPanel", panelSize);
        float textWidth = Mathf.Max(160f, panelSize.x - 48f);
        CreateText(confirmationPanel.transform, "Title", "Deseja sair do jogo?", new Vector2(0f, 48f), new Vector2(textWidth, 44f), 24, FontStyle.Bold);
        CreateText(confirmationPanel.transform, "Hint", "Esc para voltar", new Vector2(0f, 8f), new Vector2(textWidth, 30f), 15, FontStyle.Normal);
        CreateButton(confirmationPanel.transform, "SimButton", "Sim", new Vector2(-82f, -58f), QuitGame, new Vector2(120f, 42f));
        CreateButton(confirmationPanel.transform, "NoButton", "Não", new Vector2(82f, -58f), ShowMenu, new Vector2(120f, 42f));
    }

    private GameObject CreatePanel(string objectName, Vector2 size)
    {
        GameObject panel = new GameObject(objectName);
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = size;

        Image panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, panelOpacity);
        return panel;
    }

    private void CreateText(Transform parent, string objectName, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, FontStyle style)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text label = textObject.AddComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
    }

    private void CreateButton(Transform parent, string objectName, string text, Vector2 anchoredPosition, UnityEngine.Events.UnityAction action, Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        GameObject labelObject = new GameObject("Text");
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        Text label = labelObject.AddComponent<Text>();
        label.text = text;
        label.font = GetDefaultFont();
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.08f, 0.08f, 0.08f, 1f);
    }

    private void SetPlayerInputLocked(bool locked)
    {
        foreach (PlayerTopDownController player in FindObjectsOfType<PlayerTopDownController>(true))
        {
            if (player != null) player.SetExternalMovementLocked(locked);
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
}
