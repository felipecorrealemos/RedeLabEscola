using UnityEngine;

[DisallowMultipleComponent]
public class EmpilhadeiraInteractionTrigger : MonoBehaviour
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

    private void OnTriggerEnter(Collider other)
    {
        controller?.NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerStay(Collider other)
    {
        controller?.NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerExit(Collider other)
    {
        controller?.NotifyPlayerExitInteraction(other);
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
