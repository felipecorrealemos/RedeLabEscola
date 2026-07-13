using UnityEngine;

[ExecuteAlways]
public class VisualStyleLightController : MonoBehaviour
{
    [SerializeField] private Light[] controlledLights;
    [SerializeField] private float[] baseIntensities;
    [SerializeField, Range(0f, 3f)] private float intensityMultiplier = 1f;
    [SerializeField] private bool internalLightsEnabled = true;
    [SerializeField] private bool applyEnvironmentSettings = true;
    [SerializeField] private Color ambientSkyColor = new Color(0.82f, 0.87f, 0.88f, 1f);
    [SerializeField, Min(0f)] private float ambientIntensity = 1.08f;

    public float IntensityMultiplier
    {
        get => intensityMultiplier;
        set
        {
            intensityMultiplier = Mathf.Max(0f, value);
            ApplyIntensity();
        }
    }

    public bool InternalLightsEnabled
    {
        get => internalLightsEnabled;
        set
        {
            internalLightsEnabled = value;
            ApplyIntensity();
        }
    }

    public void SetControlledLights(Light[] lights)
    {
        controlledLights = lights;
        baseIntensities = new float[controlledLights.Length];

        for (int i = 0; i < controlledLights.Length; i++)
        {
            if (controlledLights[i] != null)
            {
                baseIntensities[i] = controlledLights[i].intensity;
            }
        }

        ApplyIntensity();
    }

    private void Awake()
    {
        ApplyVisualStyle();
    }

    private void OnEnable()
    {
        ApplyVisualStyle();
    }

    private void OnValidate()
    {
        ApplyVisualStyle();
    }

    [ContextMenu("Apply Visual Style")]
    public void ApplyVisualStyle()
    {
        EnsureBaseIntensities();
        ApplyIntensity();
        ApplyEnvironmentSettings();
    }

    private void EnsureBaseIntensities()
    {
        if (controlledLights == null)
        {
            baseIntensities = null;
            return;
        }

        if (baseIntensities == null || baseIntensities.Length != controlledLights.Length)
        {
            baseIntensities = new float[controlledLights.Length];
        }

        for (int i = 0; i < controlledLights.Length; i++)
        {
            if (controlledLights[i] != null && baseIntensities[i] <= 0f)
            {
                baseIntensities[i] = controlledLights[i].intensity;
            }
        }
    }

    private void ApplyIntensity()
    {
        if (controlledLights == null || baseIntensities == null)
        {
            return;
        }

        for (int i = 0; i < controlledLights.Length; i++)
        {
            if (controlledLights[i] != null)
            {
                controlledLights[i].enabled = internalLightsEnabled;
                controlledLights[i].intensity = baseIntensities[i] * intensityMultiplier;
            }
        }
    }

    private void ApplyEnvironmentSettings()
    {
        if (!applyEnvironmentSettings)
        {
            return;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientSkyColor = ambientSkyColor;
        RenderSettings.ambientIntensity = ambientIntensity;
        RenderSettings.fog = false;
    }
}
