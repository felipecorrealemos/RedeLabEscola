using UnityEngine;

[DisallowMultipleComponent]
public class ForkliftPallet : MonoBehaviour
{
    [SerializeField] private string palletType = "Default";

    private Rigidbody palletRigidbody;
    private Collider[] palletColliders;
    private bool originalKinematic;
    private bool originalUseGravity;
    private RigidbodyConstraints originalConstraints;

    public string PalletType => palletType;
    public bool IsCarried { get; private set; }

    private void Awake()
    {
        RefreshReferences();
    }

    public void PrepareForCarry()
    {
        RefreshReferences();
        IsCarried = true;

        if (palletRigidbody != null)
        {
            originalKinematic = palletRigidbody.isKinematic;
            originalUseGravity = palletRigidbody.useGravity;
            originalConstraints = palletRigidbody.constraints;
            palletRigidbody.isKinematic = true;
            palletRigidbody.useGravity = false;
            palletRigidbody.velocity = Vector3.zero;
            palletRigidbody.angularVelocity = Vector3.zero;
            palletRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        SetCollidersTrigger(true);
    }

    public void RestoreAfterCarry(bool keepKinematic)
    {
        RefreshReferences();
        IsCarried = false;

        if (palletRigidbody != null)
        {
            palletRigidbody.isKinematic = keepKinematic ? true : originalKinematic;
            palletRigidbody.useGravity = keepKinematic ? false : originalUseGravity;
            palletRigidbody.constraints = originalConstraints;
            palletRigidbody.velocity = Vector3.zero;
            palletRigidbody.angularVelocity = Vector3.zero;
        }

        SetCollidersTrigger(false);
    }

    public Rigidbody EnsureRigidbody()
    {
        RefreshReferences();
        if (palletRigidbody == null)
        {
            palletRigidbody = gameObject.AddComponent<Rigidbody>();
            palletRigidbody.mass = 20f;
            palletRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        return palletRigidbody;
    }

    private void RefreshReferences()
    {
        if (palletRigidbody == null)
        {
            palletRigidbody = GetComponent<Rigidbody>();
        }

        palletColliders = GetComponentsInChildren<Collider>(true);
    }

    private void SetCollidersTrigger(bool isTrigger)
    {
        if (palletColliders == null)
        {
            return;
        }

        for (int i = 0; i < palletColliders.Length; i++)
        {
            if (palletColliders[i] != null)
            {
                palletColliders[i].isTrigger = isTrigger;
            }
        }
    }
}
