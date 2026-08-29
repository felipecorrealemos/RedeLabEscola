using UnityEngine;

public static class CharacterSelectionState
{
    private const string PlayerPrefsKey = "RedeLabEscola.SelectedCharacter";

    public static CharacterSelectionChoice CurrentChoice { get; private set; } = CharacterSelectionChoice.None;
    private static string pendingGameplayScene;

    public static bool HasChoice => CurrentChoice != CharacterSelectionChoice.None;

    public static void SaveChoice(CharacterSelectionChoice choice)
    {
        CurrentChoice = choice;
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)choice);
        PlayerPrefs.Save();
    }

    public static void SetRuntimeChoice(CharacterSelectionChoice choice)
    {
        CurrentChoice = choice;
    }

    public static void ClearChoice()
    {
        CurrentChoice = CharacterSelectionChoice.None;
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
    }

    public static void SyncFromServer(int characterId)
    {
        if (characterId != (int)CharacterSelectionChoice.Aluno
            && characterId != (int)CharacterSelectionChoice.Aluna)
        {
            return;
        }

        SaveChoice((CharacterSelectionChoice)characterId);
    }

    public static void SetPendingGameplayScene(string sceneName)
    {
        pendingGameplayScene = sceneName;
    }

    public static string ConsumePendingGameplayScene(string fallback)
    {
        string result = string.IsNullOrWhiteSpace(pendingGameplayScene)
            ? fallback
            : pendingGameplayScene;
        pendingGameplayScene = null;
        return result;
    }

    public static void ClearPendingGameplayScene()
    {
        pendingGameplayScene = null;
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
