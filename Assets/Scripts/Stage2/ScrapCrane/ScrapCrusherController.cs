using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ScrapCrusherController : MonoBehaviour
{
    private static readonly Vector3 DefaultIntakeTriggerLocalPosition = new Vector3(0f, 1.2f, 0f);
    private static readonly Vector3 DefaultIntakeTriggerCenter = new Vector3(0.13759f, -0.7868f, -0.0042f);
    private static readonly Vector3 DefaultIntakeTriggerSize = new Vector3(0.938131f, 0.82507f, 0.57663f);
    private static readonly Vector3 DefaultDropCatchColliderCenter = new Vector3(0.13759f, -1.18f, -0.0042f);
    private static readonly Vector3 DefaultDropCatchColliderSize = new Vector3(0.938131f, 0.08f, 0.57663f);

    [Header("References")]
    [SerializeField] private Transform bladeGroupA;
    [SerializeField] private Transform bladeGroupB;
    [SerializeField] private Collider intakeTrigger;
    [SerializeField] private Transform scrapContainer;

    [Header("Blades")]
    [SerializeField] private Vector3 bladeRotationAxis = Vector3.right;
    [SerializeField] private float bladeASpeed = 360f;
    [SerializeField] private float bladeBSpeed = -360f;
    [SerializeField, Min(0f)] private float spinAfterConsumeDuration = 0.8f;

    [Header("Intake")]
    [SerializeField] private LayerMask scrapLayers = ~0;
    [SerializeField, Min(0f)] private float grindHoldDuration = 2.5f;
    [SerializeField, Min(0f)] private float grindShakeAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float grindShakeFrequency = 12f;
    [SerializeField] private Vector3 grindShakeLocalAxis = Vector3.right;
    [SerializeField, Min(0.05f)] private float consumeDuration = 1.8f;
    [SerializeField, Min(0f)] private float sinkLocalDistance = 1.25f;
    [SerializeField, Range(0.01f, 1f)] private float finalScaleMultiplier = 0.08f;
    [SerializeField] private bool destroyConsumedScrap = true;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private Coroutine consumeRoutine;
    private float spinTimer;

    private void Reset()
    {
        ResolveReferences();
        EnsureIntakeTrigger();
    }

    private void Awake()
    {
        ResolveReferences();
        EnsureIntakeTrigger();
    }

    private void Update()
    {
        if (consumeRoutine != null)
        {
            spinTimer = spinAfterConsumeDuration;
        }
        else if (spinTimer > 0f)
        {
            spinTimer -= Time.deltaTime;
        }

        if (consumeRoutine != null || spinTimer > 0f)
        {
            RotateBlades(Time.deltaTime);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        TryConsumeFromCollider(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryConsumeFromCollider(other);
    }

    public void NotifyIntakeTrigger(Collider other)
    {
        TryConsumeFromCollider(other);
    }

    public bool TryConsumeReleasedScrap(ScrapItem item)
    {
        if (!CanAcceptReleasedScrap(item))
        {
            return false;
        }

        consumeRoutine = StartCoroutine(ConsumeScrap(item));
        return true;
    }

    public bool CanAcceptReleasedScrap(ScrapItem item)
    {
        if (item == null || consumeRoutine != null || intakeTrigger == null || !item.CanBeConsumedByCrusher)
        {
            return false;
        }

        Transform root = item.GrabRoot;
        return root != null && IsInsideLayerMask(root.gameObject.layer) && IsInsideIntake(root);
    }

    public bool IsDropRayOverIntake(Vector3 worldOrigin, float maxDistance)
    {
        if (intakeTrigger == null || maxDistance <= 0f)
        {
            return false;
        }

        Ray ray = new Ray(worldOrigin, Vector3.down);
        return intakeTrigger.Raycast(ray, out _, maxDistance);
    }

    public void AssignReferences(Transform firstBlades, Transform secondBlades, Collider trigger, Transform container)
    {
        bladeGroupA = firstBlades;
        bladeGroupB = secondBlades;
        intakeTrigger = trigger;
        scrapContainer = container != null ? container : transform;
        EnsureIntakeTrigger();
    }

    private void TryConsumeFromCollider(Collider other)
    {
        if (other == null || consumeRoutine != null || !IsInsideLayerMask(other.gameObject.layer))
        {
            return;
        }

        ScrapItem item = other.GetComponentInParent<ScrapItem>();
        if (item == null || !item.CanBeConsumedByCrusher)
        {
            return;
        }

        consumeRoutine = StartCoroutine(ConsumeScrap(item));
    }

    private IEnumerator ConsumeScrap(ScrapItem item)
    {
        Transform root = item != null ? item.GrabRoot : null;
        if (root == null)
        {
            consumeRoutine = null;
            yield break;
        }
        item.SetCanBeGrabbed(false);

        Rigidbody body = root.GetComponent<Rigidbody>();
        bool hadBody = body != null;
        bool wasKinematic = false;
        bool usedGravity = false;
        bool detectedCollisions = false;
        if (hadBody)
        {
            wasKinematic = body.isKinematic;
            usedGravity = body.useGravity;
            detectedCollisions = body.detectCollisions;
            ClearRigidbodyVelocityIfDynamic(body);
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        bool[] originalTriggerStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] == null)
            {
                continue;
            }

            originalTriggerStates[i] = colliders[i].isTrigger;
            colliders[i].isTrigger = true;
        }

        Transform parent = scrapContainer != null ? scrapContainer : transform;
        root.SetParent(parent, true);
        Vector3 startLocalPosition = root.localPosition;
        Vector3 targetLocalPosition = startLocalPosition + Vector3.down * sinkLocalDistance;
        Vector3 startScale = root.localScale;
        Vector3 targetScale = startScale * finalScaleMultiplier;

        float holdElapsed = 0f;
        Vector3 shakeAxis = grindShakeLocalAxis.sqrMagnitude > 0.001f ? grindShakeLocalAxis.normalized : Vector3.right;
        while (holdElapsed < grindHoldDuration && root != null)
        {
            holdElapsed += Time.deltaTime;
            float shake = Mathf.Sin(holdElapsed * grindShakeFrequency * Mathf.PI * 2f) * grindShakeAmplitude;
            root.localPosition = startLocalPosition + shakeAxis * shake;
            root.localScale = startScale;
            yield return null;
        }

        float elapsed = 0f;
        while (elapsed < consumeDuration && root != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / consumeDuration);
            t = t * t * (3f - 2f * t);
            root.localPosition = Vector3.Lerp(startLocalPosition, targetLocalPosition, t);
            root.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        if (root != null && destroyConsumedScrap)
        {
            Destroy(root.gameObject);
        }
        else if (root != null)
        {
            if (hadBody)
            {
                body.isKinematic = wasKinematic;
                body.useGravity = usedGravity;
                body.detectCollisions = detectedCollisions;
            }

            for (int i = 0; i < colliders.Length && i < originalTriggerStates.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].isTrigger = originalTriggerStates[i];
                }
            }
        }

        consumeRoutine = null;
        spinTimer = spinAfterConsumeDuration;
    }

    private bool IsInsideIntake(Transform root)
    {
        if (intakeTrigger == null || root == null)
        {
            return false;
        }

        Vector3 worldPosition = root.position;
        Vector3 closest = intakeTrigger.ClosestPoint(worldPosition);
        if (Vector3.SqrMagnitude(closest - worldPosition) <= 0.04f)
        {
            return true;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider scrapCollider = colliders[i];
            if (scrapCollider != null && intakeTrigger.bounds.Intersects(scrapCollider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearRigidbodyVelocityIfDynamic(Rigidbody body)
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private bool IsInsideLayerMask(int layer)
    {
        return (scrapLayers.value & (1 << layer)) != 0;
    }

    private void RotateBlades(float deltaTime)
    {
        Vector3 axis = bladeRotationAxis.sqrMagnitude > 0.001f ? bladeRotationAxis.normalized : Vector3.right;
        if (bladeGroupA != null)
        {
            bladeGroupA.Rotate(axis, bladeASpeed * deltaTime, Space.Self);
        }

        if (bladeGroupB != null)
        {
            bladeGroupB.Rotate(axis, bladeBSpeed * deltaTime, Space.Self);
        }
    }

    private void ResolveReferences()
    {
        if (bladeGroupA == null)
        {
            bladeGroupA = FindChildRecursive(transform, "laminas");
        }

        if (bladeGroupB == null)
        {
            bladeGroupB = FindChildRecursive(transform, "laminas.001");
        }

        if (scrapContainer == null)
        {
            scrapContainer = transform;
        }
    }

    private void EnsureIntakeTrigger()
    {
        if (intakeTrigger == null)
        {
            Transform existing = transform.Find("CrusherIntakeTrigger");
            if (existing != null)
            {
                intakeTrigger = existing.GetComponent<Collider>();
            }
        }

        if (intakeTrigger == null)
        {
            GameObject triggerObject = new GameObject("CrusherIntakeTrigger");
            triggerObject.transform.SetParent(transform, false);
            triggerObject.transform.localPosition = DefaultIntakeTriggerLocalPosition;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
            BoxCollider box = triggerObject.AddComponent<BoxCollider>();
            intakeTrigger = box;
        }

        intakeTrigger.transform.localPosition = DefaultIntakeTriggerLocalPosition;
        intakeTrigger.transform.localRotation = Quaternion.identity;
        intakeTrigger.transform.localScale = Vector3.one;
        if (intakeTrigger is BoxCollider intakeBox)
        {
            intakeBox.center = DefaultIntakeTriggerCenter;
            intakeBox.size = DefaultIntakeTriggerSize;
        }

        intakeTrigger.isTrigger = true;
        ScrapCrusherIntakeTrigger forwarder = intakeTrigger.GetComponent<ScrapCrusherIntakeTrigger>();
        if (forwarder == null)
        {
            forwarder = intakeTrigger.gameObject.AddComponent<ScrapCrusherIntakeTrigger>();
        }

        forwarder.AssignCrusher(this);
        EnsureDropCatchCollider();
    }

    private void EnsureDropCatchCollider()
    {
        Transform catchTransform = transform.Find("CrusherDropCatchCollider");
        bool created = false;
        if (catchTransform == null)
        {
            GameObject catchObject = new GameObject("CrusherDropCatchCollider");
            catchTransform = catchObject.transform;
            catchTransform.SetParent(transform, false);
            catchTransform.localPosition = DefaultIntakeTriggerLocalPosition;
            catchTransform.localRotation = Quaternion.identity;
            catchTransform.localScale = Vector3.one;
            created = true;
        }

        BoxCollider catchCollider = catchTransform.GetComponent<BoxCollider>();
        if (catchCollider == null)
        {
            catchCollider = catchTransform.gameObject.AddComponent<BoxCollider>();
            created = true;
        }

        catchCollider.isTrigger = false;
        if (created)
        {
            catchCollider.center = DefaultDropCatchColliderCenter;
            catchCollider.size = DefaultDropCatchColliderSize;
        }

        ScrapCrusherDropCatchCollider catchForwarder = catchTransform.GetComponent<ScrapCrusherDropCatchCollider>();
        if (catchForwarder == null)
        {
            catchForwarder = catchTransform.gameObject.AddComponent<ScrapCrusherDropCatchCollider>();
        }
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || intakeTrigger == null)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.45f, 0f, 0.25f);
        Gizmos.matrix = intakeTrigger.transform.localToWorldMatrix;
        if (intakeTrigger is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.45f, 0f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
