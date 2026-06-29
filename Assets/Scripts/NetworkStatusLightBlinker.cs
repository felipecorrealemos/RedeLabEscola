using UnityEngine;

[DisallowMultipleComponent]
public class NetworkStatusLightBlinker : MonoBehaviour
{
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private float blinkFrequency = 4f;

    public float BlinkFrequency
    {
        get => blinkFrequency;
        set => blinkFrequency = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }
    }

    private void OnDisable()
    {
        SetRendererVisible(true);
    }

    private void Update()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<Renderer>();
        }

        if (targetRenderer == null)
        {
            return;
        }

        if (!IsGreenStatusMaterial(targetRenderer.sharedMaterial) || blinkFrequency <= 0f)
        {
            SetRendererVisible(true);
            return;
        }

        bool visible = Mathf.Repeat(Time.time * blinkFrequency, 1f) < 0.5f;
        SetRendererVisible(visible);
    }

    public static NetworkStatusLightBlinker Ensure(Renderer renderer, float frequency)
    {
        if (renderer == null)
        {
            return null;
        }

        NetworkStatusLightBlinker blinker = renderer.GetComponent<NetworkStatusLightBlinker>();
        if (blinker == null)
        {
            blinker = renderer.gameObject.AddComponent<NetworkStatusLightBlinker>();
        }

        blinker.targetRenderer = renderer;
        blinker.BlinkFrequency = frequency;
        return blinker;
    }

    public static void EnsureOnGreenLightRenderers(Transform root, float frequency)
    {
        if (root == null)
        {
            return;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !LooksLikeStatusLight(renderer) || !IsGreenStatusMaterial(renderer.sharedMaterial))
            {
                continue;
            }

            Ensure(renderer, frequency);
        }
    }

    public static void EnsureOnSceneGreenLightRenderers(float frequency)
    {
        Renderer[] renderers = FindObjectsOfType<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !LooksLikeStatusLight(renderer) || !IsGreenStatusMaterial(renderer.sharedMaterial))
            {
                continue;
            }

            Ensure(renderer, frequency);
        }
    }

    private static bool LooksLikeStatusLight(Renderer renderer)
    {
        string lowerName = renderer.name.ToLowerInvariant();
        return lowerName.Contains("light")
            || lowerName.Contains("luz")
            || lowerName.Contains("lamp")
            || lowerName.Contains("status");
    }

    private static bool IsGreenStatusMaterial(Material material)
    {
        if (material == null)
        {
            return false;
        }

        string lowerName = material.name.ToLowerInvariant();
        if (lowerName.Contains("verde") || lowerName.Contains("green"))
        {
            return true;
        }

        if (!material.HasProperty("_Color"))
        {
            return false;
        }

        Color color = material.color;
        return color.g > 0.45f && color.g > color.r * 1.35f && color.g > color.b * 1.15f;
    }

    private void SetRendererVisible(bool visible)
    {
        if (targetRenderer != null && targetRenderer.enabled != visible)
        {
            targetRenderer.enabled = visible;
        }
    }
}
