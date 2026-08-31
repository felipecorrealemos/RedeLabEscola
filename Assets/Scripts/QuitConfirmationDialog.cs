using RedeLabEscola.Auth;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuitConfirmationDialog : MonoBehaviour
{
    [Header("Destino")]
    [SerializeField] private string mainMenuSceneName = SceneNames.MainMenu;

    [Header("UI serializada")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject confirmationPanel;
    [SerializeField] private Button leaveGameButton;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private GraphicRaycaster modalRaycaster;

    [Header("Textos editaveis")]
    [SerializeField] private Text menuTitleLabel;
    [SerializeField] private Text leaveGameLabel;
    [SerializeField] private Text confirmationTitleLabel;
    [SerializeField] private Text confirmationHintLabel;
    [SerializeField] private string menuTitle = "MENU";
    [SerializeField] private string leaveGameText = "Sair do jogo";
    [SerializeField] private string confirmationTitle = "Deseja sair do jogo?";
    [SerializeField] private string confirmationHint = "Seu progresso salvo sera mantido.";

    private bool menuOpen;
    private bool confirmationOpen;

    private void Awake()
    {
        if (modalRaycaster == null) modalRaycaster = GetComponent<GraphicRaycaster>();
        ApplyInspectorText();
        BindButtons();
        CloseAll();
    }

    private void OnDestroy()
    {
        SetPlayerInputLocked(false);
    }

    private void OnValidate()
    {
        ApplyInspectorText();
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

        if (!IsGameplayPanelOpen()) ShowMenu();
    }

    public void ShowMenu()
    {
        SetModalRaycastsEnabled(true);
        menuOpen = true;
        confirmationOpen = false;
        if (menuPanel != null) menuPanel.SetActive(true);
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        SetPlayerInputLocked(true);
    }

    public void ShowConfirmation()
    {
        SetModalRaycastsEnabled(true);
        menuOpen = false;
        confirmationOpen = true;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (confirmationPanel != null) confirmationPanel.SetActive(true);
        SetPlayerInputLocked(true);
    }

    public void CloseAll()
    {
        menuOpen = false;
        confirmationOpen = false;
        if (menuPanel != null) menuPanel.SetActive(false);
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        SetModalRaycastsEnabled(false);
        SetPlayerInputLocked(false);
    }

    public void ReturnToMainMenu()
    {
        CloseAll();
        Time.timeScale = 1f;
        RedeLabLoadContext.Clear();
        CharacterSelectionState.ClearPendingGameplayScene();
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName)) SceneManager.LoadScene(mainMenuSceneName);
    }

    private void BindButtons()
    {
        if (leaveGameButton != null)
        {
            leaveGameButton.onClick.RemoveListener(ShowConfirmation);
            leaveGameButton.onClick.AddListener(ShowConfirmation);
        }
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ReturnToMainMenu);
            confirmButton.onClick.AddListener(ReturnToMainMenu);
        }
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(ShowMenu);
            cancelButton.onClick.AddListener(ShowMenu);
        }
    }

    private void ApplyInspectorText()
    {
        if (menuTitleLabel != null) menuTitleLabel.text = menuTitle;
        if (leaveGameLabel != null) leaveGameLabel.text = leaveGameText;
        if (confirmationTitleLabel != null) confirmationTitleLabel.text = confirmationTitle;
        if (confirmationHintLabel != null) confirmationHintLabel.text = confirmationHint;
    }

    private void SetModalRaycastsEnabled(bool enabled)
    {
        if (modalRaycaster == null) modalRaycaster = GetComponent<GraphicRaycaster>();
        if (modalRaycaster != null) modalRaycaster.enabled = enabled;
    }

    private static bool IsGameplayPanelOpen()
    {
        foreach (RouterInteractable router in FindObjectsOfType<RouterInteractable>(true))
        {
            if (router != null && router.IsOpen) return true;
        }
        foreach (ComputerInteractable computer in FindObjectsOfType<ComputerInteractable>(true))
        {
            if (computer != null && computer.IsOpen) return true;
        }
        return false;
    }

    private static void SetPlayerInputLocked(bool locked)
    {
        foreach (PlayerTopDownController player in FindObjectsOfType<PlayerTopDownController>(true))
        {
            if (player != null) player.SetExternalMovementLocked(locked);
        }
    }
}
