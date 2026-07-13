using UnityEngine;

public static class CharacterSelectionState
{
    private const string PlayerPrefsKey = "RedeLabEscola.SelectedCharacter";

    public static CharacterSelectionChoice CurrentChoice { get; private set; } = CharacterSelectionChoice.None;

    public static bool HasChoice => CurrentChoice != CharacterSelectionChoice.None;

    public static void SaveChoice(CharacterSelectionChoice choice)
    {
        CurrentChoice = choice;
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)choice);
        PlayerPrefs.Save();
    }

    public static CharacterSelectionChoice GetChoiceOrDefault(CharacterSelectionChoice fallback)
    {
        if (CurrentChoice != CharacterSelectionChoice.None)
        {
            return CurrentChoice;
        }

        int savedValue = PlayerPrefs.GetInt(PlayerPrefsKey, (int)fallback);
        return System.Enum.IsDefined(typeof(CharacterSelectionChoice), savedValue)
            ? (CharacterSelectionChoice)savedValue
            : fallback;
    }
}
