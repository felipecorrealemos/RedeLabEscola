using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneFadeTransition : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.45f;
    [SerializeField] private bool fadeInOnStart = true;

    private bool transitionRunning;

    private void Awake()
    {
        if (fadeGroup == null) return;
        fadeGroup.alpha = fadeInOnStart ? 1f : 0f;
        fadeGroup.blocksRaycasts = fadeInOnStart;
        fadeGroup.interactable = false;
    }

    private void Start()
    {
        if (fadeInOnStart && fadeGroup != null) StartCoroutine(FadeIn());
    }

    public void LoadScene(string sceneName)
    {
        if (transitionRunning || string.IsNullOrWhiteSpace(sceneName)) return;
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    private IEnumerator FadeIn()
    {
        yield return Fade(1f, 0f);
        fadeGroup.blocksRaycasts = false;
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        transitionRunning = true;
        if (fadeGroup != null)
        {
            fadeGroup.transform.SetAsLastSibling();
            fadeGroup.blocksRaycasts = true;
            yield return Fade(fadeGroup.alpha, 1f);
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        if (operation == null) transitionRunning = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, fadeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        fadeGroup.alpha = to;
    }
}
