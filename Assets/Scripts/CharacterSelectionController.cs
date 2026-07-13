using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterSelectionController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "SampleScene";
    [SerializeField] private CharacterSelectionOption alunoOption;
    [SerializeField] private CharacterSelectionOption alunaOption;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Text confirmationLabel;

    private CharacterSelectionChoice selectedChoice = CharacterSelectionChoice.None;

    private void Awake()
    {
        ApplySelection(CharacterSelectionChoice.None);
    }

    public void Configure(CharacterSelectionOption newAlunoOption, CharacterSelectionOption newAlunaOption, Button newConfirmButton, Text newConfirmationLabel)
    {
        alunoOption = newAlunoOption;
        alunaOption = newAlunaOption;
        confirmButton = newConfirmButton;
        confirmationLabel = newConfirmationLabel;
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
        if (selectedChoice == CharacterSelectionChoice.None)
        {
            return;
        }

        CharacterSelectionState.SaveChoice(selectedChoice);
        SceneManager.LoadScene(gameplaySceneName);
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
