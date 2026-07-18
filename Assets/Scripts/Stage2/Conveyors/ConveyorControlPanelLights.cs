using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class ConveyorControlPanelLights : MonoBehaviour
{
    private const string GreenMaterialPath = "Assets/Prefabs/materiais/verde.mat";
    private const string YellowMaterialPath = "Assets/Prefabs/materiais/amarelo.mat";
    private const string RedMaterialPath = "Assets/Prefabs/materiais/vermelho.mat";

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
        Configure(controller, sensor, null, null, null);
    }

    public void Configure(ConveyorController controller, ConveyorJamSensor sensor, Material greenMaterial, Material yellowMaterial, Material redMaterial)
    {
        conveyorController = controller;
        jamSensor = sensor;
        greenOnMaterial = greenMaterial != null ? greenMaterial : greenOnMaterial;
        yellowOnMaterial = yellowMaterial != null ? yellowMaterial : yellowOnMaterial;
        redOnMaterial = redMaterial != null ? redMaterial : redOnMaterial;
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
            greenLightRenderer = FindChildRenderer("Light_Green", "Light Green", "Head_Green", "Head Green", "Green", "LightGreen");
        }

        if (yellowLightRenderer == null)
        {
            yellowLightRenderer = FindChildRenderer("Light_Yellow", "Light Yellow", "Head_Yellow", "Head Yellow", "Yellow", "LightYellow");
        }

        if (redLightRenderer == null)
        {
            redLightRenderer = FindChildRenderer("Light_Red", "Light Red", "Head_Red", "Head Red", "Headlight", "Red", "LightRed");
        }
    }

    private Renderer FindChildRenderer(params string[] childNames)
    {
        for (int i = 0; i < childNames.Length; i++)
        {
            Transform child = FindChildRecursive(transform, childNames[i]);
            Renderer renderer = child != null ? child.GetComponent<Renderer>() : null;
            if (renderer != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform result = FindChildRecursive(root.GetChild(i), childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void CacheOnMaterials()
    {
#if UNITY_EDITOR
        if (greenOnMaterial == null)
        {
            greenOnMaterial = AssetDatabase.LoadAssetAtPath<Material>(GreenMaterialPath);
        }

        if (yellowOnMaterial == null)
        {
            yellowOnMaterial = AssetDatabase.LoadAssetAtPath<Material>(YellowMaterialPath);
        }

        if (redOnMaterial == null)
        {
            redOnMaterial = AssetDatabase.LoadAssetAtPath<Material>(RedMaterialPath);
        }
#endif

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
