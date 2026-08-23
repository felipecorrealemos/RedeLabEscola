using UnityEngine;

[DisallowMultipleComponent]
public sealed class LocalSfxEmitter : MonoBehaviour
{
    [SerializeField, HideInInspector] private AudioSource audioSource;
    private AudioClip configuredClip;

    public AudioSource Source
    {
        get
        {
            EnsureSource();
            return audioSource;
        }
    }

    public AudioClip ConfiguredClip => configuredClip;

    private void Awake()
    {
        EnsureSource();
        if (configuredClip == null)
        {
            configuredClip = audioSource.clip;
        }
    }

    public AudioSource Play(
        AudioClip clip,
        float volume,
        bool loop,
        bool use3D,
        float minDistance,
        float maxDistance)
    {
        if (clip == null)
        {
            return null;
        }

        EnsureSource();
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.loop = loop;
        audioSource.spatialBlend = use3D ? 1f : 0f;
        audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.Play();
        return audioSource;
    }

    public void ApplyVolume(float volume, bool muted)
    {
        EnsureSource();
        audioSource.volume = Mathf.Clamp01(volume);
        audioSource.mute = muted;
    }

    private void EnsureSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
    }
}
