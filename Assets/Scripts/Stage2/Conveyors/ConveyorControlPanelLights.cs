using UnityEngine;

[DisallowMultipleComponent]
public class ConveyorControlPanelLights : MonoBehaviour
{
    [SerializeField] private ConveyorController conveyorController;
    [SerializeField] private ConveyorJamSensor jamSensor;
    [SerializeField] private Renderer greenLightRenderer;
    [SerializeField] private Renderer yellowLightRenderer;
    [SerializeField] private Renderer redLightRenderer;
    [SerializeField] private Material greenOnMaterial;
    [SerializeField] private Material yellowOnMaterial;
    [SerializeField] private Material redOnMaterial;
    [SerializeField] private float blinkFrequency = 4f;
    [SerializeField] private bool blinkActiveLights = false;
    [SerializeField] private float stoppedSpeedThreshold = 0.01f;

    private NetworkStatusLightBlinker greenBlinker;

    public void Configure(ConveyorController controller, ConveyorJamSensor sensor)
    {
        conveyorController = controller;
        jamSensor = sensor;
        ResolveRenderers();
        CacheOnMaterials();
        ConfigureBlinker();
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveRenderers();
        CacheOnMaterials();
        ConfigureBlinker();
    }

    private void LateUpdate()
    {
        ResolveReferences();
        ResolveRenderers();
        CacheOnMaterials();
        ConfigureBlinker();
        UpdateLights();
    }

    private void ResolveReferences()
    {
        if (conveyorController == null)
        {
            conveyorController = GetComponentInParent<ConveyorController>();
        }

        if (jamSensor == null && conveyorController != null)
        {
            jamSensor = conveyorController.GetComponentInChildren<ConveyorJamSensor>();
        }
    }

    private void ResolveRenderers()
    {
        if (greenLightRenderer == null)
        {
            greenLightRenderer = FindChildRenderer("Light_Green");
        }

        if (yellowLightRenderer == null)
        {
            yellowLightRenderer = FindChildRenderer("Light_Yellow");
        }

        if (redLightRenderer == null)
        {
            redLightRenderer = FindChildRenderer("Light_Red");
        }
    }

    private Renderer FindChildRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Renderer>() : null;
    }

    private void CacheOnMaterials()
    {
        if (greenOnMaterial == null && greenLightRenderer != null)
        {
            greenOnMaterial = greenLightRenderer.sharedMaterial;
        }

        if (yellowOnMaterial == null && yellowLightRenderer != null)
        {
            yellowOnMaterial = yellowLightRenderer.sharedMaterial;
        }

        if (redOnMaterial == null && redLightRenderer != null)
        {
            redOnMaterial = redLightRenderer.sharedMaterial;
        }
    }

    private void ConfigureBlinker()
    {
        if (greenLightRenderer == null)
        {
            return;
        }

        greenBlinker = greenLightRenderer.GetComponent<NetworkStatusLightBlinker>();
        if (greenBlinker != null)
        {
            greenBlinker.BlinkFrequency = 0f;
        }
    }

    private void UpdateLights()
    {
        bool hasStoppedItemInJamSensor = jamSensor != null && jamSensor.CurrentItemCount > 0;
        bool jamLimitReached = conveyorController != null && conveyorController.CurrentState == ConveyorState.Jammed;
        bool conveyorStopped = conveyorController == null
            || conveyorController.CurrentState == ConveyorState.Stopped
            || (conveyorController.CurrentState == ConveyorState.Jammed && jamSensor != null && jamSensor.StopConveyorOnJam)
            || conveyorController.CurrentSpeed <= stoppedSpeedThreshold && conveyorController.CurrentState != ConveyorState.Starting;

        bool redOn = conveyorStopped || jamLimitReached;
        bool yellowOn = !redOn && hasStoppedItemInJamSensor;
        bool greenOn = !redOn && !yellowOn && conveyorController != null && conveyorController.CurrentSpeed > stoppedSpeedThreshold;

        bool blinkVisible = !blinkActiveLights || blinkFrequency <= 0f || Mathf.Repeat(Time.time * blinkFrequency, 1f) < 0.5f;

        ApplyLight(greenLightRenderer, greenOnMaterial, greenOn, blinkVisible);
        ApplyLight(yellowLightRenderer, yellowOnMaterial, yellowOn, blinkVisible);
        ApplyLight(redLightRenderer, redOnMaterial, redOn, blinkVisible);
    }

    private void ApplyLight(Renderer targetRenderer, Material onMaterial, bool isOn, bool blinkVisible)
    {
        if (targetRenderer == null)
        {
            return;
        }

        if (onMaterial != null && targetRenderer.sharedMaterial != onMaterial)
        {
            targetRenderer.sharedMaterial = onMaterial;
        }

        targetRenderer.enabled = isOn && blinkVisible;
    }
}
