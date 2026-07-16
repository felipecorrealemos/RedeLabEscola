using UnityEngine;

[DisallowMultipleComponent]
public class ForkliftPickupSensor : MonoBehaviour
{
    [SerializeField] private ForkliftController controller;

    private void Awake()
    {
        ForceTriggerCollider();
    }

    private void OnValidate()
    {
        ForceTriggerCollider();
    }

    public void Configure(ForkliftController owner)
    {
        controller = owner;
        ForceTriggerCollider();
    }

    private void Reset()
    {
        controller = GetComponentInParent<ForkliftController>();
        ForceTriggerCollider();
    }

    private void ForceTriggerCollider()
    {
        Collider sensorCollider = GetComponent<Collider>();
        if (sensorCollider != null)
        {
            sensorCollider.isTrigger = true;
#if UNITY_2022_2_OR_NEWER
            sensorCollider.providesContacts = false;
#endif
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        controller?.NotifyPickupSensorEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        controller?.NotifyPickupSensorExit(other);
    }
}
