using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ConveyorJamSensor : MonoBehaviour
{
    [SerializeField] private Collider jamSensorZone;
    [SerializeField] private int jamItemThreshold = 3;
    [SerializeField] private float jamDetectionTime = 3f;
    [SerializeField] private float restartDelay = 2f;
    [SerializeField] private bool stopConveyorOnJam = false;
    [SerializeField] private bool stopSpawnerOnJam = true;
    [SerializeField] private bool restartAutomatically = true;
    [SerializeField] private bool countOnlyStoppedItems = true;
    [SerializeField] private int restartItemThreshold = 1;
    [SerializeField] private float fallbackEndDistance = 2.5f;
    [SerializeField] private int currentItemCount;
    [SerializeField] private float currentCongestionTime;

    public Collider JamSensorZoneCollider => jamSensorZone;
    public int JamItemThreshold => Mathf.Max(1, jamItemThreshold);
    public float JamDetectionTime => Mathf.Max(0.01f, jamDetectionTime);
    public float RestartDelay => Mathf.Max(0f, restartDelay);
    public bool StopConveyorOnJam => stopConveyorOnJam;
    public bool StopSpawnerOnJam => stopSpawnerOnJam;
    public bool RestartAutomatically => restartAutomatically;
    public bool CountOnlyStoppedItems => countOnlyStoppedItems;
    public int RestartItemThreshold => Mathf.Max(0, restartItemThreshold);
    public int CurrentItemCount => currentItemCount;
    public float CurrentCongestionTime => currentCongestionTime;

    public void Configure(Collider sensorZone, int jamThreshold, float detectionTime, int restartThreshold, float delay)
    {
        jamSensorZone = sensorZone;
        jamItemThreshold = Mathf.Max(1, jamThreshold);
        jamDetectionTime = Mathf.Max(0.01f, detectionTime);
        restartItemThreshold = Mathf.Max(0, restartThreshold);
        restartDelay = Mathf.Max(0f, delay);
        stopConveyorOnJam = false;
        stopSpawnerOnJam = true;
        restartAutomatically = true;
        if (jamSensorZone != null)
        {
            jamSensorZone.isTrigger = true;
        }
    }

    public void SetCountOnlyStoppedItems(bool value)
    {
        countOnlyStoppedItems = value;
    }

    private void Reset()
    {
        jamSensorZone = GetComponent<Collider>();
        if (jamSensorZone != null)
        {
            jamSensorZone.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        jamItemThreshold = Mathf.Max(1, jamItemThreshold);
        jamDetectionTime = Mathf.Max(0.01f, jamDetectionTime);
        restartDelay = Mathf.Max(0f, restartDelay);
        restartItemThreshold = Mathf.Max(0, restartItemThreshold);
        fallbackEndDistance = Mathf.Max(0.1f, fallbackEndDistance);

        if (jamSensorZone != null)
        {
            jamSensorZone.isTrigger = true;
        }
    }

    public int CountItems(IReadOnlyList<ConveyorItem> activeItems, ConveyorPath path)
    {
        currentItemCount = 0;
        if (activeItems == null)
        {
            return currentItemCount;
        }

        Bounds bounds = jamSensorZone != null ? jamSensorZone.bounds : default;
        float pathLength = path != null ? path.TotalLength : 0f;
        float fallbackStartDistance = Mathf.Max(0f, pathLength - fallbackEndDistance);

        for (int i = 0; i < activeItems.Count; i++)
        {
            ConveyorItem item = activeItems[i];
            if (item == null || item.CurrentState == ConveyorItemState.Removed)
            {
                continue;
            }

            if (countOnlyStoppedItems && !item.IsStoppedForJamSensor)
            {
                continue;
            }

            bool inside = jamSensorZone != null
                ? bounds.Contains(item.transform.position)
                : item.ProgressDistance >= fallbackStartDistance;

            if (inside)
            {
                currentItemCount++;
            }
        }

        return currentItemCount;
    }

    public void SetCongestionTime(float value)
    {
        currentCongestionTime = Mathf.Max(0f, value);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.05f, 0.55f);
        if (jamSensorZone != null)
        {
            Gizmos.DrawWireCube(jamSensorZone.bounds.center, jamSensorZone.bounds.size);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, fallbackEndDistance);
        }
    }
}
