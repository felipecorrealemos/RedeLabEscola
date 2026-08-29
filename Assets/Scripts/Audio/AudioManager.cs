using System.Collections.Generic;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class AudioManager : MonoBehaviour
{
    private const string SettingsResourceName = "RedeLabAudioSettings";
    private static readonly HashSet<string> MissingClipWarnings = new HashSet<string>();

    private static AudioManager instance;
    [Header("Configuracao")]
    [SerializeField] private RedeLabAudioSettings settings;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource globalSfxSource;

    [Header("Controle de Volume")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float mainMenuMusicVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float gameplayMusicVolume = 0.18f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.9f;
    [SerializeField, Min(0f)] private float musicFadeInDuration = 3f;

    [Header("Mute e Debug")]
    [SerializeField] private bool muteMusic;
    [SerializeField] private bool muteSfx;
    [SerializeField, Tooltip("Desliga imediatamente musica e efeitos para testar o jogo em silencio.")]
    private bool disableAllAudioForTesting;

    private Coroutine musicFadeRoutine;
    private Coroutine musicStartRoutine;
    private string activeMusicSceneName;
    private int musicRequestVersion;

    public static AudioManager Instance => instance;
    public float MasterVolume => masterVolume;
    public float MainMenuMusicVolume => mainMenuMusicVolume;
    public float GameplayMusicVolume => gameplayMusicVolume;
    public float SfxVolume => sfxVolume;
    public bool IsMusicMuted => muteMusic;
    public bool IsSfxMuted => muteSfx;
    public bool IsAllAudioDisabledForTesting => disableAllAudioForTesting;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        if (settings == null)
        {
            settings = Resources.Load<RedeLabAudioSettings>(SettingsResourceName);
        }
        EnsureSources();

        if (musicSource == null || globalSfxSource == null)
        {
            enabled = false;
            return;
        }

        if (settings == null)
        {
            Debug.LogError(
                "AudioManager nao encontrou Resources/RedeLabAudioSettings. O sistema de audio ficara inativo ate o asset ser restaurado.",
                this);
        }

        ApplyVolumeSettings(false);

#if UNITY_WEBGL && !UNITY_EDITOR
        RedeLabAudio_InstallUnlockHandlers(gameObject.name);
#endif
    }

    private void OnValidate()
    {
        ClampVolumeSettings();
        if (Application.isPlaying)
        {
            ApplyVolumeSettings(true);
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void Start()
    {
        ApplyMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyMusicForScene(scene.name);
    }

    public static void PlayDoorOpen(Transform emitter)
    {
        if (!TryGetReadyInstance(out AudioManager manager)) return;
        manager.PlayAtEmitter(manager.settings.DoorOpen, emitter, "door_open", false);
    }

    public static void ResumeAfterUserInteraction()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (instance != null) RedeLabAudio_ResumeContext(instance.gameObject.name);
#endif
        if (instance != null)
        {
            instance.EnsureCurrentMusicPlaying();
        }
    }

    // Chamado pelo plugin WebGL somente depois que AudioContext.resume() for concluido.
    public void OnWebGLAudioUnlocked(string ignored)
    {
        EnsureCurrentMusicPlaying();
    }

    public static AudioSource StartPrinter(Transform emitter)
    {
        if (!TryGetReadyInstance(out AudioManager manager)) return null;
        LocalSfxEmitter localEmitter = manager.GetEmitter(emitter);
        AudioClip clip = localEmitter.ConfiguredClip != null
            ? localEmitter.ConfiguredClip
            : manager.settings.PrinterPrinting;
        if (!manager.ValidateClip(clip, "printer_printing")) return null;
        AudioSource source = localEmitter.Play(
            clip,
            manager.GetEffectiveSfxVolume(),
            manager.settings.LoopPrinterWhilePrinting,
            false,
            manager.settings.SpatialMinDistance,
            manager.settings.SpatialMaxDistance);
        localEmitter.ApplyVolume(
            manager.GetEffectiveSfxVolume(),
            manager.muteSfx || manager.disableAllAudioForTesting);
        return source;
    }

    public static void StopPrinter(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.loop = false;
    }

    public static void PlayNetworkConnect(Transform emitter)
    {
        if (!TryGetReadyInstance(out AudioManager manager)) return;
        manager.PlayAtEmitter(manager.settings.NetworkConnect, emitter, "network_connect", false, false);
    }

    public static void FadeOutMusic(float duration)
    {
        if (!TryGetReadyInstance(out AudioManager manager)) return;
        manager.StartMusicFade(0f, duration, true);
    }

    public static void PlayGlobalSfx(AudioClip clip, float volumeScale = 1f)
    {
        if (!TryGetReadyInstance(out AudioManager manager) || !manager.ValidateClip(clip, "SFX global")) return;
        manager.ApplyGlobalSfxVolume();
        manager.globalSfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings(true);
    }

    public void SetMainMenuMusicVolume(float value)
    {
        mainMenuMusicVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings(true);
    }

    public void SetGameplayMusicVolume(float value)
    {
        gameplayMusicVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings(true);
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        ApplyVolumeSettings(true);
    }

    public void SetMusicMuted(bool muted)
    {
        muteMusic = muted;
        ApplyVolumeSettings(false);
    }

    public void SetSfxMuted(bool muted)
    {
        muteSfx = muted;
        ApplyVolumeSettings(false);
    }

    public void SetAllAudioDisabledForTesting(bool disabled)
    {
        disableAllAudioForTesting = disabled;
        ApplyVolumeSettings(false);
    }

    private void ApplyMusicForScene(string sceneName)
    {
        if (settings == null || !settings.IsMusicScene(sceneName))
        {
            return;
        }

        AudioClip requestedClip = settings.GetMusicForScene(sceneName);
        if (!ValidateClip(requestedClip, "musica da cena " + sceneName))
        {
            musicSource.Stop();
            musicSource.clip = null;
            return;
        }

        activeMusicSceneName = sceneName;
        if (musicSource.clip == requestedClip && musicSource.isPlaying)
        {
            ApplyMuteSettings();
            return;
        }

        musicRequestVersion++;
        if (musicStartRoutine != null)
        {
            StopCoroutine(musicStartRoutine);
            musicStartRoutine = null;
        }
        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }
        musicSource.Stop();
        musicSource.clip = requestedClip;
        musicSource.volume = 0f;
        musicSource.loop = true;
        ApplyMuteSettings();
        musicStartRoutine = StartCoroutine(StartMusicWhenReady(
            requestedClip,
            sceneName,
            musicRequestVersion));
    }

    private IEnumerator StartMusicWhenReady(AudioClip clip, string sceneName, int requestVersion)
    {
        if (clip.loadState == AudioDataLoadState.Unloaded)
        {
            clip.LoadAudioData();
        }

        while (clip.loadState == AudioDataLoadState.Loading)
        {
            if (requestVersion != musicRequestVersion) yield break;
            yield return null;
        }

        musicStartRoutine = null;
        if (requestVersion != musicRequestVersion
            || musicSource == null
            || musicSource.clip != clip
            || activeMusicSceneName != sceneName)
        {
            yield break;
        }

        if (clip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogError("AudioManager nao conseguiu carregar a musica da cena " + sceneName + ".", this);
            yield break;
        }

        musicSource.Play();
        StartMusicFade(GetEffectiveMusicVolume(sceneName), musicFadeInDuration, false);
    }

    private void EnsureCurrentMusicPlaying()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (settings == null || !settings.IsMusicScene(sceneName)) return;

        AudioClip requestedClip = settings.GetMusicForScene(sceneName);
        if (musicSource != null && musicSource.clip == requestedClip && musicSource.isPlaying)
        {
            ApplyMuteSettings();
            if (musicFadeRoutine == null)
            {
                musicSource.volume = GetEffectiveMusicVolume(sceneName);
            }
            return;
        }

        ApplyMusicForScene(sceneName);
    }

    private void PlayAtEmitter(
        AudioClip clip,
        Transform emitter,
        string clipLabel,
        bool use3D,
        bool preferConfiguredClip = true)
    {
        if (emitter == null)
        {
            Debug.LogError("AudioManager recebeu um emissor nulo para " + clipLabel + ".", this);
            return;
        }

        LocalSfxEmitter localEmitter = GetEmitter(emitter);
        AudioClip resolvedClip = preferConfiguredClip && localEmitter.ConfiguredClip != null
            ? localEmitter.ConfiguredClip
            : clip;
        if (!ValidateClip(resolvedClip, clipLabel))
        {
            return;
        }

        localEmitter.Play(
            resolvedClip,
            GetEffectiveSfxVolume(),
            false,
            use3D,
            settings.SpatialMinDistance,
            settings.SpatialMaxDistance);
        localEmitter.ApplyVolume(GetEffectiveSfxVolume(), muteSfx || disableAllAudioForTesting);
    }

    private LocalSfxEmitter GetEmitter(Transform target)
    {
        LocalSfxEmitter emitter = target.GetComponent<LocalSfxEmitter>();
        if (emitter == null)
        {
            emitter = target.GetComponentInParent<LocalSfxEmitter>();
        }
        return emitter != null ? emitter : target.gameObject.AddComponent<LocalSfxEmitter>();
    }

    private void StartMusicFade(float targetVolume, float duration, bool stopWhenSilent)
    {
        if (musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
        }

        musicFadeRoutine = StartCoroutine(FadeMusicRoutine(
            Mathf.Clamp01(targetVolume),
            Mathf.Max(0f, duration),
            stopWhenSilent));
    }

    private IEnumerator FadeMusicRoutine(float targetVolume, float duration, bool stopWhenSilent)
    {
        float startVolume = musicSource.volume;
        if (duration <= 0f)
        {
            musicSource.volume = targetVolume;
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            musicSource.volume = targetVolume;
        }

        if (stopWhenSilent && targetVolume <= 0f)
        {
            musicSource.Stop();
            musicSource.clip = null;
        }

        musicFadeRoutine = null;
    }

    private float GetEffectiveMusicVolume(string sceneName)
    {
        float configuredVolume = settings != null
            && (sceneName == settings.Stage01Scene || sceneName == settings.Stage02Scene)
            ? gameplayMusicVolume
            : mainMenuMusicVolume;
        return Mathf.Clamp01(masterVolume * configuredVolume);
    }

    private float GetEffectiveSfxVolume()
    {
        return Mathf.Clamp01(masterVolume * sfxVolume);
    }

    private void ApplyVolumeSettings(bool stopActiveFade)
    {
        ClampVolumeSettings();

        if (stopActiveFade && musicFadeRoutine != null)
        {
            StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = null;
        }

        if (musicSource != null)
        {
            string sceneName = !string.IsNullOrWhiteSpace(activeMusicSceneName)
                ? activeMusicSceneName
                : SceneManager.GetActiveScene().name;
            if (musicSource.isPlaying && musicFadeRoutine == null)
            {
                musicSource.volume = GetEffectiveMusicVolume(sceneName);
            }
        }

        ApplyMuteSettings();
        ApplyGlobalSfxVolume();

        if (!Application.isPlaying)
        {
            return;
        }

        LocalSfxEmitter[] emitters = FindObjectsOfType<LocalSfxEmitter>(true);
        float localVolume = GetEffectiveSfxVolume();
        bool localMuted = muteSfx || disableAllAudioForTesting;
        foreach (LocalSfxEmitter emitter in emitters)
        {
            if (emitter != null)
            {
                emitter.ApplyVolume(localVolume, localMuted);
            }
        }

        ScrapCrusherController[] crushers = FindObjectsOfType<ScrapCrusherController>(true);
        foreach (ScrapCrusherController crusher in crushers)
        {
            if (crusher != null)
            {
                crusher.ApplyAudioVolumeSettings(localVolume, localMuted);
            }
        }

        EmpilhadeiraController[] forklifts = FindObjectsOfType<EmpilhadeiraController>(true);
        foreach (EmpilhadeiraController forklift in forklifts)
        {
            if (forklift != null)
            {
                forklift.ApplyAudioVolumeSettings(localVolume, localMuted);
            }
        }

        RoboticArmNetworkAdapter[] roboticArms = FindObjectsOfType<RoboticArmNetworkAdapter>(true);
        foreach (RoboticArmNetworkAdapter roboticArm in roboticArms)
        {
            if (roboticArm != null)
            {
                roboticArm.ApplyAudioVolumeSettings(localVolume, localMuted);
            }
        }
    }

    private void ApplyMuteSettings()
    {
        if (musicSource != null)
        {
            musicSource.mute = muteMusic || disableAllAudioForTesting;
        }

        if (globalSfxSource != null)
        {
            globalSfxSource.mute = muteSfx || disableAllAudioForTesting;
        }
    }

    private void ApplyGlobalSfxVolume()
    {
        if (globalSfxSource == null)
        {
            return;
        }

        globalSfxSource.volume = GetEffectiveSfxVolume();
        globalSfxSource.mute = muteSfx || disableAllAudioForTesting;
    }

    private void ClampVolumeSettings()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        mainMenuMusicVolume = Mathf.Clamp01(mainMenuMusicVolume);
        gameplayMusicVolume = Mathf.Clamp01(gameplayMusicVolume);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        musicFadeInDuration = Mathf.Max(0f, musicFadeInDuration);
    }

    private bool ValidateClip(AudioClip clip, string label)
    {
        if (clip != null)
        {
            return true;
        }

        if (MissingClipWarnings.Add(label))
        {
            Debug.LogWarning(
                "AudioManager: o AudioClip de " + label + " nao foi atribuido em Resources/RedeLabAudioSettings.",
                this);
        }
        return false;
    }

    private static bool TryGetReadyInstance(out AudioManager manager)
    {
        manager = instance;
        return manager != null
            && manager.settings != null
            && manager.musicSource != null
            && manager.globalSfxSource != null;
    }

    private void EnsureSources()
    {
        if (musicSource == null)
        {
            Transform musicChild = transform.Find("Music AudioSource");
            musicSource = musicChild != null ? musicChild.GetComponent<AudioSource>() : null;
        }

        if (globalSfxSource == null)
        {
            Transform sfxChild = transform.Find("SFX AudioSource");
            globalSfxSource = sfxChild != null ? sfxChild.GetComponent<AudioSource>() : null;
        }

        if (musicSource == null || globalSfxSource == null)
        {
            Debug.LogError(
                "AudioManager precisa das referencias Music AudioSource e SFX AudioSource configuradas no prefab.",
                this);
            return;
        }

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;

        globalSfxSource.playOnAwake = false;
        globalSfxSource.loop = false;
        globalSfxSource.spatialBlend = 0f;
        ApplyGlobalSfxVolume();
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RedeLabAudio_InstallUnlockHandlers(string receiver);

    [DllImport("__Internal")]
    private static extern void RedeLabAudio_ResumeContext(string receiver);
#endif
}
