using UnityEngine;

[DisallowMultipleComponent]
public sealed class ComputerWorkstation : MonoBehaviour
{
    [SerializeField] private DeviceDropZone cabinetDropZone;
    [SerializeField] private Renderer monitorScreenRenderer;
    [SerializeField] private Light monitorScreenSpotlight;
    [SerializeField] private Material screenOffMaterial;
    [SerializeField] private Material screenOnMaterial;

    public DeviceDropZone CabinetDropZone => cabinetDropZone;

    private void Awake()
    {
        SetPowered(false);
    }

    public void Configure(DeviceDropZone dropZone, Renderer screenRenderer, Light spotlight)
    {
        cabinetDropZone = dropZone;
        monitorScreenRenderer = screenRenderer;
        monitorScreenSpotlight = spotlight;
        if (monitorScreenRenderer != null && screenOffMaterial == null)
        {
            screenOffMaterial = monitorScreenRenderer.sharedMaterial;
        }
    }

    public void SetPowered(bool powered, Material poweredMaterial = null)
    {
        if (monitorScreenRenderer != null)
        {
            if (screenOffMaterial == null && monitorScreenRenderer.sharedMaterial != screenOnMaterial)
            {
                screenOffMaterial = monitorScreenRenderer.sharedMaterial;
            }

            Material onMaterial = poweredMaterial != null ? poweredMaterial : screenOnMaterial;
            Material target = powered && onMaterial != null ? onMaterial : screenOffMaterial;
            if (target != null)
            {
                monitorScreenRenderer.sharedMaterial = target;
            }
        }

        if (monitorScreenSpotlight != null)
        {
            if (monitorScreenSpotlight.gameObject.activeSelf != powered)
            {
                monitorScreenSpotlight.gameObject.SetActive(powered);
            }
            monitorScreenSpotlight.enabled = powered;
        }
    }
}
