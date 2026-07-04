using UnityEngine;

public class VisualStyleLightController : MonoBehaviour
{
    [SerializeField] private Light[] controlledLights;
    [SerializeField] private float[] baseIntensities;
    [SerializeField, Range(0f, 3f)] private float intensityMultiplier = 1f;

    public float IntensityMultiplier
    {
        get => intensityMultiplier;
        set
        {
            intensityMultiplier = Mathf.Max(0f, value);
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
        EnsureBaseIntensities();
        ApplyIntensity();
    }

    private void OnValidate()
    {
        EnsureBaseIntensities();
        ApplyIntensity();
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
                controlledLights[i].intensity = baseIntensities[i] * intensityMultiplier;
            }
        }
    }
}
