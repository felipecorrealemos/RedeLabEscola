using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ForkliftPalletDropZone : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private Transform palletPlacementPoint;
    [SerializeField] private float deliveryHeight = 1.05f;
    [SerializeField] private float heightTolerance = 0.18f;
    [SerializeField] private float distanceTolerance = 1.4f;
    [SerializeField] private float placementSpeed = 7f;

    [Header("Accepted Pallet")]
    [SerializeField] private bool acceptAnyPalletType = true;
    [SerializeField] private string acceptedPalletType = "Default";

    [Header("Conveyor Integration")]
    [SerializeField] private ConveyorController destinationConveyor;
    [SerializeField] private bool usePlacementPointRotation = true;
    [SerializeField] private bool keepDeliveredPalletKinematic = true;

    public Transform PalletPlacementPoint => palletPlacementPoint != null ? palletPlacementPoint : transform;
    public float DeliveryHeight => deliveryHeight;
    public float HeightTolerance => Mathf.Max(0.01f, heightTolerance);
    public float DistanceTolerance => Mathf.Max(0.05f, distanceTolerance);
    public float PlacementSpeed => Mathf.Max(0.01f, placementSpeed);

    private void Reset()
    {
        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
        {
            zoneCollider.isTrigger = true;
        }
    }

    private void Awake()
    {
        if (destinationConveyor == null)
        {
            destinationConveyor = GetComponentInParent<ConveyorController>();
        }
    }

    public bool CanAccept(ForkliftPallet pallet)
    {
        if (pallet == null)
        {
            return false;
        }

        return acceptAnyPalletType
            || string.Equals(pallet.PalletType, acceptedPalletType, System.StringComparison.OrdinalIgnoreCase);
    }

    public bool IsPalletCloseEnough(ForkliftPallet pallet)
    {
        if (pallet == null)
        {
            return false;
        }

        Vector3 palletPosition = pallet.transform.position;
        Vector3 targetPosition = PalletPlacementPoint.position;
        palletPosition.y = 0f;
        targetPosition.y = 0f;
        return Vector3.Distance(palletPosition, targetPosition) <= DistanceTolerance;
    }

    public void CompleteDrop(ForkliftPallet pallet)
    {
        if (pallet == null)
        {
            return;
        }

        StartCoroutine(CompleteDropRoutine(pallet));
    }

    private IEnumerator CompleteDropRoutine(ForkliftPallet pallet)
    {
        Transform placement = PalletPlacementPoint;
        while (pallet != null
            && (Vector3.Distance(pallet.transform.position, placement.position) > 0.01f
                || Quaternion.Angle(pallet.transform.rotation, usePlacementPointRotation ? placement.rotation : pallet.transform.rotation) > 1f))
        {
            pallet.transform.position = Vector3.Lerp(pallet.transform.position, placement.position, PlacementSpeed * Time.deltaTime);
            if (usePlacementPointRotation)
            {
                pallet.transform.rotation = Quaternion.Slerp(pallet.transform.rotation, placement.rotation, PlacementSpeed * Time.deltaTime);
            }

            yield return null;
        }

        if (pallet == null)
        {
            yield break;
        }

        ConveyorItem conveyorItem = pallet.GetComponent<ConveyorItem>();
        if (conveyorItem != null && destinationConveyor != null && destinationConveyor.ConveyorPath != null && destinationConveyor.ConveyorPath.IsValid())
        {
            pallet.RestoreAfterCarry(true);
            pallet.transform.SetParent(destinationConveyor.transform, true);
            pallet.transform.position = placement.position;
            if (usePlacementPointRotation)
            {
                pallet.transform.rotation = placement.rotation;
            }

            float dropDistance = destinationConveyor.ConveyorPath.GetClosestDistance(pallet.transform.position);
            destinationConveyor.RegisterItem(conveyorItem);
            conveyorItem.Initialize(destinationConveyor, destinationConveyor.ConveyorPath, conveyorItem.ProductId, dropDistance, 0f);
            yield break;
        }

        pallet.transform.SetParent(destinationConveyor != null ? destinationConveyor.transform : null, true);
        pallet.transform.SetPositionAndRotation(placement.position, usePlacementPointRotation ? placement.rotation : pallet.transform.rotation);
        pallet.RestoreAfterCarry(keepDeliveredPalletKinematic);
    }

    private void OnTriggerEnter(Collider other)
    {
        ForkliftController forklift = other != null ? other.GetComponentInParent<ForkliftController>() : null;
        forklift?.NotifyDropZoneEnter(this);
    }

    private void OnTriggerExit(Collider other)
    {
        ForkliftController forklift = other != null ? other.GetComponentInParent<ForkliftController>() : null;
        forklift?.NotifyDropZoneExit(this);
    }

    private void OnDrawGizmosSelected()
    {
        Transform placement = PalletPlacementPoint;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.85f);
        Gizmos.DrawWireSphere(placement.position, distanceTolerance);
        Gizmos.DrawLine(placement.position, new Vector3(placement.position.x, deliveryHeight, placement.position.z));
    }
}
