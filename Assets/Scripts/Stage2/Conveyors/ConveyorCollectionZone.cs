using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ConveyorCollectionZone : MonoBehaviour
{
    [SerializeField] private Transform collectionPoint;
    [SerializeField] private Collider collectionZone;
    [SerializeField] private int maximumItemsInCollectionZone = 10;

    [Header("Dual Queue")]
    [SerializeField] private bool useDualQueue = true;
    [SerializeField] private float leftQueueOffset = -0.22f;
    [SerializeField] private float rightQueueOffset = 0.22f;
    [SerializeField] private float queueItemSpacing = 0.72f;
    [SerializeField] private int maximumItemsPerQueue = 5;
    [SerializeField] private QueueDistributionMode distributionMode = QueueDistributionMode.ShortestQueue;
    [SerializeField] private float queueAlignmentSpeed = 2.5f;
    [SerializeField] private float queueRotationSpeed = 8f;
    [SerializeField] private bool rotateQueuedItemsToSlot = true;
    [SerializeField] private bool alignSlotsToPath = true;
    [SerializeField] private bool useSlotVariation = true;
    [SerializeField] private float slotLateralNoise = 0.035f;
    [SerializeField] private float slotLongitudinalNoise = 0.055f;

    [Header("Runtime Debug")]
    [SerializeField] private int availableItems;
    [SerializeField] private int leftQueueCount;
    [SerializeField] private int rightQueueCount;
    [SerializeField] private int totalQueuedItems;
    [SerializeField] private string nextQueueToUse = "Left";
    [SerializeField] private int waitingBeforeCollectionZone;

    private readonly List<ConveyorItem> items = new List<ConveyorItem>();
    private readonly List<ConveyorItem> leftQueue = new List<ConveyorItem>();
    private readonly List<ConveyorItem> rightQueue = new List<ConveyorItem>();
    private readonly HashSet<ConveyorItem> reservedItems = new HashSet<ConveyorItem>();
    private bool nextTieGoesLeft = true;
    private bool nextAvailableCheckLeft = true;
    private ConveyorPath path;

    public Transform CollectionPoint => collectionPoint;
    public Collider CollectionZoneCollider => collectionZone;
    public bool UseDualQueue => useDualQueue;
    public float QueueItemSpacing => Mathf.Max(0.05f, queueItemSpacing);
    public float QueueAssignmentDistance => QueueItemSpacing * MaximumItemsPerQueue;
    public int MaximumItemsPerQueue => Mathf.Max(1, maximumItemsPerQueue);
    public int MaximumItemsInCollectionZone => useDualQueue ? MaximumItemsPerQueue * 2 : Mathf.Max(1, maximumItemsInCollectionZone);
    public int AvailableItems => availableItems;
    public int LeftQueueCount => leftQueueCount;
    public int RightQueueCount => rightQueueCount;
    public int TotalQueuedItems => totalQueuedItems;
    public int WaitingBeforeCollectionZone => waitingBeforeCollectionZone;
    public QueueDistributionMode DistributionMode => distributionMode;
    public float LeftQueueOffset => leftQueueOffset;
    public float RightQueueOffset => rightQueueOffset;
    public bool AlignSlotsToPath => alignSlotsToPath;
    public bool RotateQueuedItemsToSlot => rotateQueuedItemsToSlot;

    public void Configure(Transform point, Collider zone, int maximumItems)
    {
        collectionPoint = point;
        collectionZone = zone;
        maximumItemsInCollectionZone = Mathf.Max(1, maximumItems);
        maximumItemsPerQueue = Mathf.Max(1, maximumItems / 2);
        useDualQueue = true;

        if (collectionZone != null)
        {
            collectionZone.isTrigger = true;
        }
    }

    public void ConfigureDualQueue(float leftOffset, float rightOffset, float spacing, int maxPerQueue, QueueDistributionMode mode)
    {
        useDualQueue = true;
        leftQueueOffset = leftOffset;
        rightQueueOffset = rightOffset;
        queueItemSpacing = Mathf.Max(0.05f, spacing);
        maximumItemsPerQueue = Mathf.Max(1, maxPerQueue);
        maximumItemsInCollectionZone = maximumItemsPerQueue * 2;
        distributionMode = mode;
        RefreshDebugCount();
    }

    public void ConfigureSingleQueue(float centerOffset, float spacing, int maxItems)
    {
        useDualQueue = false;
        leftQueueOffset = centerOffset;
        rightQueueOffset = centerOffset;
        queueItemSpacing = Mathf.Max(0.05f, spacing);
        maximumItemsInCollectionZone = Mathf.Max(1, maxItems);
        maximumItemsPerQueue = Mathf.Max(1, maxItems);
        distributionMode = QueueDistributionMode.ShortestQueue;
        RefreshDebugCount();
    }

    public void SetQueueRotation(bool rotateItemsToSlot)
    {
        rotateQueuedItemsToSlot = rotateItemsToSlot;
    }

    public void ConfigureSingleStop(Transform point, Collider zone)
    {
        collectionPoint = point;
        collectionZone = zone;
        maximumItemsInCollectionZone = 1;
        maximumItemsPerQueue = 1;
        useDualQueue = false;
        if (collectionZone != null)
        {
            collectionZone.isTrigger = true;
        }

        RefreshDebugCount();
    }

    public void SetPath(ConveyorPath conveyorPath)
    {
        path = conveyorPath;
    }

    private void Reset()
    {
        collectionPoint = transform;
        collectionZone = GetComponent<Collider>();
        if (collectionZone != null)
        {
            collectionZone.isTrigger = true;
        }
    }

    private void OnValidate()
    {
        maximumItemsInCollectionZone = Mathf.Max(1, maximumItemsInCollectionZone);
        queueItemSpacing = Mathf.Max(0.05f, queueItemSpacing);
        maximumItemsPerQueue = Mathf.Max(1, maximumItemsPerQueue);
        queueAlignmentSpeed = Mathf.Max(0.01f, queueAlignmentSpeed);
        queueRotationSpeed = Mathf.Max(0.01f, queueRotationSpeed);
        slotLateralNoise = Mathf.Max(0f, slotLateralNoise);
        slotLongitudinalNoise = Mathf.Max(0f, slotLongitudinalNoise);

        if (collectionZone != null)
        {
            collectionZone.isTrigger = true;
        }
    }

    public bool TryRegisterItem(ConveyorItem item, ConveyorPath conveyorPath)
    {
        if (item == null)
        {
            RefreshDebugCount();
            return false;
        }

        path = conveyorPath != null ? conveyorPath : path;

        if (!useDualQueue)
        {
            CleanupNullItems();
            if (items.Count >= MaximumItemsInCollectionZone)
            {
                waitingBeforeCollectionZone = 1;
                RefreshDebugCount();
                return false;
            }

            if (!items.Contains(item))
            {
                items.Add(item);
            }

            if (!leftQueue.Contains(item))
            {
                leftQueue.Add(item);
            }

            item.AssignCollectionQueue(0, leftQueue.IndexOf(item));
            waitingBeforeCollectionZone = 0;
            RefreshQueueAssignments();
            return true;
        }

        CleanupNullItems();

        if (items.Contains(item))
        {
            RefreshQueueAssignments();
            return true;
        }

        int queueIndex = ChooseQueueIndex();
        if (queueIndex < 0)
        {
            waitingBeforeCollectionZone = 1;
            RefreshDebugCount();
            return false;
        }

        List<ConveyorItem> targetQueue = GetQueue(queueIndex);
        targetQueue.Add(item);
        items.Add(item);
        item.AssignCollectionQueue(queueIndex, targetQueue.Count - 1);
        waitingBeforeCollectionZone = 0;
        RefreshQueueAssignments();
        return true;
    }

    public void RegisterItem(ConveyorItem item)
    {
        if (item == null || items.Contains(item))
        {
            RefreshDebugCount();
            return;
        }

        items.Add(item);
        RefreshDebugCount();
    }

    public void UnregisterItem(ConveyorItem item)
    {
        if (item == null)
        {
            return;
        }

        items.Remove(item);
        leftQueue.Remove(item);
        rightQueue.Remove(item);
        reservedItems.Remove(item);
        RefreshQueueAssignments();
    }

    public void MoveQueuedItemToSlot(ConveyorItem item, Rigidbody itemRigidbody, float deltaTime)
    {
        if (item == null)
        {
            return;
        }

        Vector3 slotPosition = GetSlotPosition(item.CollectionQueueIndex, item.CollectionQueueSlotIndex);
        Quaternion slotRotation = GetQueueRotation();
        float moveStep = queueAlignmentSpeed * Mathf.Max(0.01f, deltaTime);
        float rotateStep = queueRotationSpeed * Mathf.Max(0.01f, deltaTime);

        if (itemRigidbody != null)
        {
            itemRigidbody.MovePosition(Vector3.MoveTowards(itemRigidbody.position, slotPosition, moveStep));
            if (rotateQueuedItemsToSlot)
            {
                itemRigidbody.MoveRotation(Quaternion.Slerp(itemRigidbody.rotation, slotRotation, rotateStep));
            }
        }
        else
        {
            Quaternion targetRotation = rotateQueuedItemsToSlot
                ? Quaternion.Slerp(item.transform.rotation, slotRotation, rotateStep)
                : item.transform.rotation;

            item.transform.SetPositionAndRotation(
                Vector3.MoveTowards(item.transform.position, slotPosition, moveStep),
                targetRotation);
        }
    }

    public float GetSlotPathDistance(ConveyorPath conveyorPath, int slotIndex)
    {
        if (conveyorPath == null)
        {
            return 0f;
        }

        return Mathf.Clamp(conveyorPath.TotalLength - Mathf.Max(0, slotIndex) * QueueItemSpacing, 0f, conveyorPath.TotalLength);
    }

    public GameObject GetAvailableItem()
    {
        CleanupNullItems();

        ConveyorItem leftItem = GetFrontAvailableItem(leftQueue);
        ConveyorItem rightItem = GetFrontAvailableItem(rightQueue);
        ConveyorItem selected = null;

        if (leftItem != null && rightItem != null)
        {
            selected = nextAvailableCheckLeft ? leftItem : rightItem;
            nextAvailableCheckLeft = !nextAvailableCheckLeft;
        }
        else
        {
            selected = leftItem != null ? leftItem : rightItem;
        }

        if (selected != null)
        {
            return selected.gameObject;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ConveyorItem item = items[i];
            if (item != null && item.IsAvailableForCollection && !reservedItems.Contains(item))
            {
                return item.gameObject;
            }
        }

        return null;
    }

    public bool ReserveItem(GameObject itemObject)
    {
        ConveyorItem item = itemObject != null ? itemObject.GetComponent<ConveyorItem>() : null;
        if (item == null || !items.Contains(item) || reservedItems.Contains(item))
        {
            return false;
        }

        if (useDualQueue && item.CollectionQueueSlotIndex != 0)
        {
            return false;
        }

        reservedItems.Add(item);
        item.Reserve();
        RefreshDebugCount();
        return true;
    }

    public void ReleaseReservation(GameObject itemObject)
    {
        ConveyorItem item = itemObject != null ? itemObject.GetComponent<ConveyorItem>() : null;
        if (item == null)
        {
            return;
        }

        reservedItems.Remove(item);
        item.ReleaseReservation();
        RefreshDebugCount();
    }

    public void RemoveItem(GameObject itemObject)
    {
        ConveyorItem item = itemObject != null ? itemObject.GetComponent<ConveyorItem>() : null;
        if (item == null)
        {
            return;
        }

        NotifyItemCollected(itemObject);
    }

    public void NotifyItemCollected(GameObject itemObject)
    {
        ConveyorItem item = itemObject != null ? itemObject.GetComponent<ConveyorItem>() : null;
        if (item == null)
        {
            return;
        }

        ConveyorController controller = item.GetComponentInParent<ConveyorController>();
        if (controller == null)
        {
            controller = FindObjectOfType<ConveyorController>();
        }

        controller?.NotifyItemCollected(item);
    }

    public bool Contains(ConveyorItem item)
    {
        return item != null && items.Contains(item);
    }

    public bool CanAcceptItem()
    {
        CleanupNullItems();
        if (!useDualQueue)
        {
            return items.Count < MaximumItemsInCollectionZone;
        }

        return leftQueue.Count < MaximumItemsPerQueue || rightQueue.Count < MaximumItemsPerQueue;
    }

    private int ChooseQueueIndex()
    {
        bool leftAvailable = leftQueue.Count < MaximumItemsPerQueue;
        bool rightAvailable = rightQueue.Count < MaximumItemsPerQueue;

        if (!leftAvailable && !rightAvailable)
        {
            nextQueueToUse = "None";
            return -1;
        }

        if (leftAvailable && !rightAvailable)
        {
            nextQueueToUse = "Left";
            return 0;
        }

        if (!leftAvailable)
        {
            nextQueueToUse = "Right";
            return 1;
        }

        int selected;
        switch (distributionMode)
        {
            case QueueDistributionMode.Alternate:
                selected = nextTieGoesLeft ? 0 : 1;
                nextTieGoesLeft = !nextTieGoesLeft;
                break;
            case QueueDistributionMode.RandomAvailable:
                selected = Random.value < 0.5f ? 0 : 1;
                break;
            default:
                if (leftQueue.Count == rightQueue.Count)
                {
                    selected = nextTieGoesLeft ? 0 : 1;
                    nextTieGoesLeft = !nextTieGoesLeft;
                }
                else
                {
                    selected = leftQueue.Count < rightQueue.Count ? 0 : 1;
                }
                break;
        }

        nextQueueToUse = selected == 0 ? "Left" : "Right";
        return selected;
    }

    private List<ConveyorItem> GetQueue(int queueIndex)
    {
        return queueIndex == 0 ? leftQueue : rightQueue;
    }

    private ConveyorItem GetFrontAvailableItem(List<ConveyorItem> queue)
    {
        if (queue.Count == 0)
        {
            return null;
        }

        ConveyorItem item = queue[0];
        return item != null && item.IsAvailableForCollection && !reservedItems.Contains(item) ? item : null;
    }

    private void CleanupNullItems()
    {
        RemoveNullItems(items);
        RemoveNullItems(leftQueue);
        RemoveNullItems(rightQueue);
        reservedItems.RemoveWhere(item => item == null);
        RefreshQueueAssignments();
    }

    private void RemoveNullItems(List<ConveyorItem> queue)
    {
        for (int i = queue.Count - 1; i >= 0; i--)
        {
            if (queue[i] == null)
            {
                queue.RemoveAt(i);
            }
        }
    }

    private void RefreshQueueAssignments()
    {
        for (int i = 0; i < leftQueue.Count; i++)
        {
            if (leftQueue[i] != null)
            {
                leftQueue[i].AssignCollectionQueue(0, i);
            }
        }

        for (int i = 0; i < rightQueue.Count; i++)
        {
            if (rightQueue[i] != null)
            {
                rightQueue[i].AssignCollectionQueue(1, i);
            }
        }

        RefreshDebugCount();
    }

    private void RefreshDebugCount()
    {
        availableItems = 0;
        for (int i = 0; i < items.Count; i++)
        {
            ConveyorItem item = items[i];
            if (item != null && item.IsAvailableForCollection && !reservedItems.Contains(item))
            {
                availableItems++;
            }
        }

        leftQueueCount = leftQueue.Count;
        rightQueueCount = rightQueue.Count;
        totalQueuedItems = leftQueueCount + rightQueueCount;
        maximumItemsInCollectionZone = useDualQueue ? MaximumItemsPerQueue * 2 : maximumItemsInCollectionZone;
        if (nextQueueToUse == "None" && (leftQueue.Count < MaximumItemsPerQueue || rightQueue.Count < MaximumItemsPerQueue))
        {
            nextQueueToUse = leftQueue.Count <= rightQueue.Count ? "Left" : "Right";
        }
    }

    public float GetQueueOffset(int queueIndex)
    {
        return queueIndex == 0 ? leftQueueOffset : rightQueueOffset;
    }

    private Vector3 GetSlotPosition(int queueIndex, int slotIndex)
    {
        Vector3 finalDirection = GetFinalDirection();
        float queueOffset = queueIndex == 0 ? leftQueueOffset : rightQueueOffset;
        float longitudinalOffset = Mathf.Max(0, slotIndex) * QueueItemSpacing;
        float lateralVariation = 0f;
        float longitudinalVariation = 0f;

        if (useSlotVariation)
        {
            lateralVariation = GetSignedSlotNoise(queueIndex, slotIndex, 13) * slotLateralNoise;
            longitudinalVariation = GetSignedSlotNoise(queueIndex, slotIndex, 29) * slotLongitudinalNoise;
            longitudinalOffset = Mathf.Max(0f, longitudinalOffset + longitudinalVariation);
        }

        if (alignSlotsToPath && path != null && path.IsValid())
        {
            float slotDistance = Mathf.Clamp(path.TotalLength - longitudinalOffset, 0f, path.TotalLength);
            ConveyorPathSample sample = path.GetSample(slotDistance);
            return sample.Position + sample.Lateral * (queueOffset + lateralVariation);
        }

        Vector3 basePosition = collectionPoint != null ? collectionPoint.position : transform.position;
        Vector3 lateral = GetLateral(finalDirection);
        return basePosition - finalDirection * longitudinalOffset + lateral * (queueOffset + lateralVariation);
    }

    private Quaternion GetQueueRotation()
    {
        Vector3 finalDirection = GetFinalDirection();
        return finalDirection.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(finalDirection, Vector3.up) : transform.rotation;
    }

    private Vector3 GetFinalDirection()
    {
        Vector3 finalDirection = path != null ? path.GetEndDirection() : transform.forward;
        finalDirection.y = 0f;
        return finalDirection.sqrMagnitude > 0.0001f ? finalDirection.normalized : transform.forward;
    }

    private Vector3 GetLateral(Vector3 finalDirection)
    {
        Vector3 lateral = Vector3.Cross(Vector3.up, finalDirection.normalized);
        return lateral.sqrMagnitude > 0.0001f ? lateral.normalized : transform.right;
    }

    private float GetSignedSlotNoise(int queueIndex, int slotIndex, int salt)
    {
        float value = Mathf.Sin((queueIndex + 1) * 37.17f + (slotIndex + 1) * 11.31f + salt * 3.73f) * 43758.5453f;
        return Mathf.Repeat(value, 1f) * 2f - 1f;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 finalDirection = GetFinalDirection();
        Vector3 center = collectionPoint != null ? collectionPoint.position : transform.position;
        Vector3 lateral = GetLateral(finalDirection);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        if (collectionZone != null)
        {
            Gizmos.DrawWireCube(collectionZone.bounds.center, collectionZone.bounds.size);
        }
        else
        {
            Gizmos.DrawWireSphere(center, 0.5f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawLine(center, center - finalDirection * QueueItemSpacing * MaximumItemsPerQueue);
        Gizmos.DrawLine(center, center + finalDirection * 0.5f);

        DrawQueueGizmos(center, finalDirection, lateral, leftQueueOffset, MaximumItemsPerQueue, new Color(0.2f, 0.7f, 1f, 0.85f));
        DrawQueueGizmos(center, finalDirection, lateral, rightQueueOffset, MaximumItemsPerQueue, new Color(1f, 0.75f, 0.15f, 0.85f));
    }

    private void DrawQueueGizmos(Vector3 center, Vector3 finalDirection, Vector3 lateral, float queueOffset, int capacity, Color color)
    {
        Gizmos.color = color;
        Vector3 lineStart = center + lateral * queueOffset;
        Vector3 lineEnd = lineStart - finalDirection * QueueItemSpacing * Mathf.Max(0, capacity - 1);
        Gizmos.DrawLine(lineStart, lineEnd);

        for (int i = 0; i < capacity; i++)
        {
            Vector3 slot = lineStart - finalDirection * QueueItemSpacing * i;
            Gizmos.DrawWireCube(slot, new Vector3(0.4f, 0.05f, 0.4f));
        }
    }
}
