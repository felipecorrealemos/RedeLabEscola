using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class FallenChairInteractable : MonoBehaviour
{
    private enum ChairState
    {
        Upright,
        WaitingForFallenConfirmation,
        Fallen,
        BeingRaised
    }

    [Header("Fallen Detection")]
    [SerializeField, Range(1f, 179f)]
    [Tooltip("Inclination from the captured upright pose required before the chair can be considered fallen.")]
    private float fallenAngle = 55f;

    [SerializeField, Min(0f)]
    [Tooltip("How long the chair must remain beyond Fallen Angle before interaction is enabled.")]
    private float fallenConfirmationTime = 0.5f;

    [SerializeField]
    [Tooltip("Local axis that physically points upward. Leave at zero to derive it from the chair's initial upright pose.")]
    private Vector3 localUpAxis = Vector3.zero;

    [Header("Raising")]
    [SerializeField, Min(0.01f)]
    [Tooltip("Duration of the smooth rotation back to an upright pose.")]
    private float raiseDuration = 0.3f;

    [SerializeField, Min(0f)]
    [Tooltip("Maximum upward correction allowed to keep the upright collider from entering the floor. X and Z are never changed.")]
    private float maxVerticalCorrection = 0.5f;

    private Rigidbody chairRigidbody;
    private BoxCollider[] boxColliders;
    private Quaternion uprightReferenceRotation;
    private ChairState state = ChairState.Upright;
    private float fallenTimer;
    private float fallenDotThreshold;
    private InteractionPromptPresenter promptPresenter;
    private bool promptVisible;
    private Coroutine raiseRoutine;
    private bool restoreIsKinematic;
    private bool restoreUseGravity;

    public bool CanInteract => state == ChairState.Fallen;

    private void Awake()
    {
        chairRigidbody = GetComponent<Rigidbody>();
        boxColliders = GetComponentsInChildren<BoxCollider>();
        uprightReferenceRotation = transform.rotation;

        if (localUpAxis.sqrMagnitude < 0.0001f)
        {
            localUpAxis = Quaternion.Inverse(uprightReferenceRotation) * Vector3.up;
        }

        localUpAxis.Normalize();
        CacheFallenThreshold();
    }

    private void FixedUpdate()
    {
        if (state == ChairState.BeingRaised)
        {
            return;
        }

        bool beyondFallenAngle = Vector3.Dot(GetPhysicalUp(), Vector3.up) < fallenDotThreshold;

        switch (state)
        {
            case ChairState.Upright:
                if (beyondFallenAngle)
                {
                    fallenTimer = 0f;
                    state = ChairState.WaitingForFallenConfirmation;
                }
                break;

            case ChairState.WaitingForFallenConfirmation:
                if (!beyondFallenAngle)
                {
                    fallenTimer = 0f;
                    state = ChairState.Upright;
                    break;
                }

                fallenTimer += Time.fixedDeltaTime;
                if (fallenTimer >= fallenConfirmationTime)
                {
                    fallenTimer = 0f;
                    state = ChairState.Fallen;
                    Debug.Log($"{name}: Chair -> Fallen", this);
                }
                break;

            case ChairState.Fallen:
                if (!beyondFallenAngle)
                {
                    fallenTimer = 0f;
                    state = ChairState.Upright;
                    SetPromptVisible(false);
                    Debug.Log($"{name}: Chair -> Upright", this);
                }
                break;
        }
    }

    public bool TryRaise()
    {
        if (!CanInteract || raiseRoutine != null)
        {
            return false;
        }

        state = ChairState.BeingRaised;
        SetPromptVisible(false);
        raiseRoutine = StartCoroutine(RaiseRoutine());
        Debug.Log($"{name}: Chair -> Raising", this);
        return true;
    }

    public void SetPromptVisible(bool visible)
    {
        if (!visible || !CanInteract)
        {
            if (promptPresenter != null)
            {
                promptPresenter.Hide(this);
            }
            promptVisible = false;
            return;
        }

        if (promptVisible)
        {
            return;
        }

        EnsurePromptPresenter();
        if (promptPresenter != null)
        {
            promptPresenter.Show(this, "CADEIRA", new InteractionPromptAction("E", "Levantar cadeira"));
            promptVisible = true;
        }
    }

    private IEnumerator RaiseRoutine()
    {
        restoreIsKinematic = chairRigidbody.isKinematic;
        restoreUseGravity = chairRigidbody.useGravity;

        chairRigidbody.velocity = Vector3.zero;
        chairRigidbody.angularVelocity = Vector3.zero;
        chairRigidbody.isKinematic = true;

        Quaternion startRotation = transform.rotation;
        Quaternion targetRotation = CalculateUprightRotation(startRotation);
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition;
        targetPosition.y += CalculateRequiredUpwardCorrection(targetRotation);

        float elapsed = 0f;
        float duration = Mathf.Max(raiseDuration, 0.01f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float smoothT = t * t * (3f - 2f * t);
            chairRigidbody.rotation = Quaternion.Slerp(startRotation, targetRotation, smoothT);
            chairRigidbody.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            yield return null;
        }

        chairRigidbody.position = targetPosition;
        chairRigidbody.rotation = targetRotation;
        RestoreRigidbodyControl();

        fallenTimer = 0f;
        state = ChairState.Upright;
        raiseRoutine = null;
        Debug.Log($"{name}: Chair -> Upright", this);
    }

    private Quaternion CalculateUprightRotation(Quaternion currentRotation)
    {
        Vector3 currentPhysicalUp = currentRotation * localUpAxis;

        // For the rare exactly-upside-down case, FromToRotation has no unique yaw.
        // The captured upright rotation is the deterministic and safe fallback.
        if (Vector3.Dot(currentPhysicalUp, Vector3.up) <= -0.999f)
        {
            return uprightReferenceRotation;
        }

        return Quaternion.FromToRotation(currentPhysicalUp, Vector3.up) * currentRotation;
    }

    private float CalculateRequiredUpwardCorrection(Quaternion targetRotation)
    {
        if (boxColliders == null || boxColliders.Length == 0 || maxVerticalCorrection <= 0f)
        {
            return 0f;
        }

        float currentBottom = float.PositiveInfinity;
        for (int i = 0; i < boxColliders.Length; i++)
        {
            BoxCollider box = boxColliders[i];
            if (box != null && box.enabled && !box.isTrigger)
            {
                currentBottom = Mathf.Min(currentBottom, box.bounds.min.y);
            }
        }

        if (float.IsPositiveInfinity(currentBottom))
        {
            return 0f;
        }

        Matrix4x4 targetLocalToWorld = Matrix4x4.TRS(transform.position, targetRotation, transform.lossyScale);
        Matrix4x4 rootWorldToLocal = transform.worldToLocalMatrix;
        float targetBottom = float.PositiveInfinity;

        for (int i = 0; i < boxColliders.Length; i++)
        {
            BoxCollider box = boxColliders[i];
            if (box == null || !box.enabled || box.isTrigger)
            {
                continue;
            }

            Matrix4x4 colliderToRoot = rootWorldToLocal * box.transform.localToWorldMatrix;
            Vector3 halfSize = box.size * 0.5f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = box.center + Vector3.Scale(halfSize, new Vector3(x, y, z));
                        Vector3 rootLocalCorner = colliderToRoot.MultiplyPoint3x4(corner);
                        float worldY = targetLocalToWorld.MultiplyPoint3x4(rootLocalCorner).y;
                        targetBottom = Mathf.Min(targetBottom, worldY);
                    }
                }
            }
        }

        if (float.IsPositiveInfinity(targetBottom))
        {
            return 0f;
        }

        return Mathf.Clamp(currentBottom - targetBottom, 0f, maxVerticalCorrection);
    }

    private Vector3 GetPhysicalUp()
    {
        return transform.rotation * localUpAxis;
    }

    private void EnsurePromptPresenter()
    {
        if (promptPresenter != null)
        {
            return;
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == "InteractionCanvas")
            {
                promptPresenter = InteractionPromptPresenter.GetOrCreate(canvases[i]);
                return;
            }
        }
    }

    private void RestoreRigidbodyControl()
    {
        if (chairRigidbody == null)
        {
            return;
        }

        chairRigidbody.useGravity = restoreUseGravity;
        chairRigidbody.isKinematic = restoreIsKinematic;
        if (!chairRigidbody.isKinematic)
        {
            chairRigidbody.velocity = Vector3.zero;
            chairRigidbody.angularVelocity = Vector3.zero;
            chairRigidbody.WakeUp();
        }
    }

    private void OnDisable()
    {
        if (promptPresenter != null)
        {
            promptPresenter.Hide(this);
        }

        promptPresenter = null;
        promptVisible = false;

        if (state == ChairState.BeingRaised)
        {
            RestoreRigidbodyControl();
            raiseRoutine = null;
            state = ChairState.Upright;
        }
    }

    private void OnValidate()
    {
        fallenAngle = Mathf.Clamp(fallenAngle, 1f, 179f);
        fallenConfirmationTime = Mathf.Max(0f, fallenConfirmationTime);
        raiseDuration = Mathf.Max(0.01f, raiseDuration);
        maxVerticalCorrection = Mathf.Max(0f, maxVerticalCorrection);
        CacheFallenThreshold();
    }

    private void CacheFallenThreshold()
    {
        fallenDotThreshold = Mathf.Cos(fallenAngle * Mathf.Deg2Rad);
    }
}
