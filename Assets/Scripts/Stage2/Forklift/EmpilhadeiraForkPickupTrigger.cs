using UnityEngine;

[DisallowMultipleComponent]
public class EmpilhadeiraForkPickupTrigger : MonoBehaviour
{
    [SerializeField] private EmpilhadeiraController controller;

    private void Awake()
    {
        ResolveReferences();
        ConfigureTrigger();
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureTrigger();
    }

    private void OnValidate()
    {
        ConfigureTrigger();
    }

    public void SetController(EmpilhadeiraController forkliftController)
    {
        controller = forkliftController;
        ConfigureTrigger();
    }

    private void OnTriggerEnter(Collider other)
    {
        controller?.NotifyForkSensorEnter(other);
    }

    private void OnTriggerStay(Collider other)
    {
        controller?.NotifyForkSensorStay(other);
    }

    private void OnTriggerExit(Collider other)
    {
        controller?.NotifyForkSensorExit(other);
    }

    private void ResolveReferences()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<EmpilhadeiraController>();
        }
    }

    private void ConfigureTrigger()
    {
        Collider trigger = GetComponent<Collider>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }
}
