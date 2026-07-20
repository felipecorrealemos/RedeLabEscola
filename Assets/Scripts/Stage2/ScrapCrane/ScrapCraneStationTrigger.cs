using UnityEngine;

[DisallowMultipleComponent]
public class ScrapCraneStationTrigger : MonoBehaviour
{
    [SerializeField] private ScrapCraneControlStation controlStation;

    private void Awake()
    {
        if (controlStation == null)
        {
            controlStation = GetComponentInParent<ScrapCraneControlStation>();
        }
    }

    public void AssignStation(ScrapCraneControlStation station)
    {
        controlStation = station;
    }

    private void OnTriggerEnter(Collider other)
    {
        controlStation?.NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerStay(Collider other)
    {
        controlStation?.NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerExit(Collider other)
    {
        controlStation?.NotifyPlayerExitInteraction(other);
    }
}
