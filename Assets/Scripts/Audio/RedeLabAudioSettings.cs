using UnityEngine;

[CreateAssetMenu(fileName = "RedeLabAudioSettings", menuName = "RedeLab Escola/Audio Settings")]
public sealed class RedeLabAudioSettings : ScriptableObject
{
    [Header("Cenas")]
    [SerializeField] private string mainMenuScene = "MainMenu";
    [SerializeField] private string stage01Scene = "SampleScene";
    [SerializeField] private string stage02Scene = "Stage2_Factory";

    [Header("Musicas")]
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField] private AudioClip stage01Music;
    [SerializeField] private AudioClip stage02Music;

    [Header("Efeitos")]
    [SerializeField] private AudioClip doorOpen;
    [SerializeField] private AudioClip printerPrinting;
    [SerializeField] private AudioClip networkConnect;
    [SerializeField] private bool loopPrinterWhilePrinting;

    [Header("Audio 3D")]
    [SerializeField, Min(0f)] private float spatialMinDistance = 1.5f;
    [SerializeField, Min(0.01f)] private float spatialMaxDistance = 14f;

    public string MainMenuScene => mainMenuScene;
    public string Stage01Scene => stage01Scene;
    public string Stage02Scene => stage02Scene;
    public AudioClip MainMenuMusic => mainMenuMusic;
    public AudioClip Stage01Music => stage01Music;
    public AudioClip Stage02Music => stage02Music;
    public AudioClip DoorOpen => doorOpen;
    public AudioClip PrinterPrinting => printerPrinting;
    public AudioClip NetworkConnect => networkConnect;
    public bool LoopPrinterWhilePrinting => loopPrinterWhilePrinting;
    public float SpatialMinDistance => spatialMinDistance;
    public float SpatialMaxDistance => Mathf.Max(spatialMaxDistance, spatialMinDistance + 0.01f);

    public AudioClip GetMusicForScene(string sceneName)
    {
        if (sceneName == mainMenuScene) return mainMenuMusic;
        if (sceneName == stage01Scene) return stage01Music;
        if (sceneName == stage02Scene) return stage02Music;
        return null;
    }

    public bool IsMusicScene(string sceneName)
    {
        return sceneName == mainMenuScene || sceneName == stage01Scene || sceneName == stage02Scene;
    }

    private void OnValidate()
    {
        spatialMinDistance = Mathf.Max(0f, spatialMinDistance);
        spatialMaxDistance = Mathf.Max(spatialMaxDistance, spatialMinDistance + 0.01f);
    }
}
