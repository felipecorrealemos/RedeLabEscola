using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelectionController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "SampleScene";
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private SceneFadeTransition sceneTransition;
    [SerializeField] private CharacterSelectionOption alunoOption;
    [SerializeField] private CharacterSelectionOption alunaOption;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Text confirmationLabel;
    [SerializeField] private CanvasGroup loadingGroup;
    [SerializeField] private Text loadingLabel;
    [SerializeField, Min(0.1f)] private float fadeToBlackDuration = 1.5f;
    [SerializeField, Min(0f)] private float minimumLoadingDuration = 5f;

    private CharacterSelectionChoice selectedChoice = CharacterSelectionChoice.None;
    private bool transitionStarted;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        ApplySelection(CharacterSelectionChoice.None);
    }

    public void BackToMainMenu()
    {
        if (transitionStarted)
        {
            return;
        }

        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (sceneTransition != null) sceneTransition.LoadScene(mainMenuSceneName);
        else SceneManager.LoadScene(mainMenuSceneName);
    }

    public void Configure(CharacterSelectionOption newAlunoOption, CharacterSelectionOption newAlunaOption, Button newConfirmButton, Text newConfirmationLabel, CanvasGroup newLoadingGroup = null, Text newLoadingLabel = null)
    {
        alunoOption = newAlunoOption;
        alunaOption = newAlunaOption;
        confirmButton = newConfirmButton;
        confirmationLabel = newConfirmationLabel;
        loadingGroup = newLoadingGroup;
        loadingLabel = newLoadingLabel;
        SetLoadingImmediate(0f, false);
        ApplySelection(CharacterSelectionChoice.None);
    }

    public void SelectAluno()
    {
        Select(CharacterSelectionChoice.Aluno);
    }

    public void SelectAluna()
    {
        Select(CharacterSelectionChoice.Aluna);
    }

    public void Select(CharacterSelectionChoice choice)
    {
        if (choice == CharacterSelectionChoice.None)
        {
            return;
        }

        ApplySelection(choice);
    }

    public void ConfirmAndStart()
    {
        if (selectedChoice == CharacterSelectionChoice.None || transitionStarted)
        {
            return;
        }

        CharacterSelectionState.SaveChoice(selectedChoice);
        transitionStarted = true;
        if (confirmButton != null) confirmButton.interactable = false;
        alunoOption?.SetInteractionEnabled(false);
        alunaOption?.SetInteractionEnabled(false);
        StartCoroutine(FadeAndLoadGameplay());
    }

    private IEnumerator FadeAndLoadGameplay()
    {
        AudioManager.FadeOutMusic(Mathf.Max(0.1f, fadeToBlackDuration));
        if (loadingLabel != null) loadingLabel.gameObject.SetActive(false);
        if (loadingGroup != null)
        {
            loadingGroup.transform.SetAsLastSibling();
            loadingGroup.gameObject.SetActive(true);
            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, fadeToBlackDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                loadingGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            loadingGroup.alpha = 1f;
        }

        if (loadingLabel != null) loadingLabel.gameObject.SetActive(true);
        AsyncOperation operation = SceneManager.LoadSceneAsync(gameplaySceneName);
        if (operation == null) yield break;
        operation.allowSceneActivation = false;
        float startedAt = Time.realtimeSinceStartup;
        while (operation.progress < 0.9f || Time.realtimeSinceStartup - startedAt < minimumLoadingDuration)
        {
            yield return null;
        }
        operation.allowSceneActivation = true;
    }

    private void SetLoadingImmediate(float alpha, bool visible)
    {
        if (loadingGroup == null) return;
        loadingGroup.alpha = alpha;
        loadingGroup.blocksRaycasts = visible;
        loadingGroup.interactable = false;
        loadingGroup.gameObject.SetActive(visible);
    }

    private void ApplySelection(CharacterSelectionChoice choice)
    {
        selectedChoice = choice;

        if (alunoOption != null)
        {
            alunoOption.SetSelected(choice == CharacterSelectionChoice.Aluno);
        }

        if (alunaOption != null)
        {
            alunaOption.SetSelected(choice == CharacterSelectionChoice.Aluna);
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = choice != CharacterSelectionChoice.None;
        }

        if (confirmationLabel != null)
        {
            confirmationLabel.text = choice switch
            {
                CharacterSelectionChoice.Aluno => "Aluno selecionado",
                CharacterSelectionChoice.Aluna => "Aluna selecionada",
                _ => "Escolha um personagem"
            };
        }
    }
}
