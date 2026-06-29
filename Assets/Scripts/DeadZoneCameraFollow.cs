using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class DeadZoneCameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private string targetName = "Player";
    [SerializeField] private Vector3 focusOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField, Range(0.1f, 0.9f)] private float targetViewportY = 0.34f;
    [SerializeField] private bool useViewportVerticalFraming = true;

    [Header("Motion")]
    [SerializeField, Min(0.01f)] private float followSharpness = 8f;
    [SerializeField] private bool snapToTargetOnStart = true;
    [SerializeField] private bool useViewDirectionForInitialOffset = true;

    [Header("Zoom")]
    [SerializeField] private bool allowMouseWheelZoom = true;
    [SerializeField, Min(0.1f)] private float zoomStep = 1.5f;
    [SerializeField, Min(0.1f)] private float minZoomDistance = 8f;
    [SerializeField, Min(0.1f)] private float maxZoomDistance = 20f;
    [SerializeField, Min(0.01f)] private float zoomSharpness = 8f;

    [Header("Bounds")]
    [SerializeField] private bool useBounds = true;
    [SerializeField] private bool centerBoundsOnInitialTarget = true;
    [SerializeField] private Vector2 minTargetPosition = new Vector2(-9.5f, -3.0f);
    [SerializeField] private Vector2 maxTargetPosition = new Vector2(9.5f, 3.0f);

    private Camera followCamera;
    private Vector3 cameraOffset;
    private float targetZoomDistance;
    private bool zoomLocked;
    private bool initialized;

    private void Reset()
    {
        followCamera = GetComponent<Camera>();
        FindTarget();
        CaptureCurrentOffset();
    }

    private void Awake()
    {
        followCamera = GetComponent<Camera>();

        if (target == null)
        {
            FindTarget();
        }
    }

    private void Start()
    {
        InitializeForTarget(snapToTargetOnStart);
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            FindTarget();

            if (target == null)
            {
                return;
            }

            CaptureCurrentOffset();
            InitializeForTarget(true);
        }

        UpdateZoomInput();
        UpdateCameraOffset();

        Vector3 targetFocusPosition = GetTargetFocusPosition();
        Vector3 desiredPosition = targetFocusPosition + cameraOffset;

        if (useBounds)
        {
            Vector3 desiredTargetPosition = desiredPosition - cameraOffset;
            desiredTargetPosition.x = Mathf.Clamp(desiredTargetPosition.x, minTargetPosition.x, maxTargetPosition.x);
            desiredTargetPosition.z = Mathf.Clamp(desiredTargetPosition.z, minTargetPosition.y, maxTargetPosition.y);
            desiredPosition = desiredTargetPosition + cameraOffset;
        }

        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, t);
    }

    private void InitializeForTarget(bool snapCamera)
    {
        if (target == null)
        {
            return;
        }

        if (!initialized && centerBoundsOnInitialTarget)
        {
            CenterBoundsOnTarget();
        }

        CaptureInitialOffset();

        if (snapCamera)
        {
            transform.position = GetClampedDesiredPosition();
        }

        initialized = true;
    }

    private void CaptureCurrentOffset()
    {
        if (target != null)
        {
            cameraOffset = transform.position - GetTargetFocusPosition();
            targetZoomDistance = Mathf.Clamp(cameraOffset.magnitude, minZoomDistance, maxZoomDistance);
        }
    }

    private void CaptureInitialOffset()
    {
        CaptureCurrentOffset();

        if (useViewDirectionForInitialOffset && followCamera != null)
        {
            Vector3 viewOffsetDirection = -transform.forward;
            if (viewOffsetDirection.sqrMagnitude > 0.001f)
            {
                cameraOffset = viewOffsetDirection.normalized * targetZoomDistance;
            }
        }
        else if (cameraOffset.sqrMagnitude > 0.001f)
        {
            cameraOffset = cameraOffset.normalized * targetZoomDistance;
        }
    }

    private void UpdateZoomInput()
    {
        if (!allowMouseWheelZoom || zoomLocked)
        {
            return;
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) <= 0.001f)
        {
            return;
        }

        targetZoomDistance = Mathf.Clamp(
            targetZoomDistance - (scroll * zoomStep),
            minZoomDistance,
            maxZoomDistance);
    }

    private void UpdateCameraOffset()
    {
        if (cameraOffset.sqrMagnitude <= 0.001f)
        {
            CaptureCurrentOffset();
            return;
        }

        Vector3 desiredOffset = cameraOffset.normalized * targetZoomDistance;
        float t = 1f - Mathf.Exp(-zoomSharpness * Time.deltaTime);
        cameraOffset = Vector3.Lerp(cameraOffset, desiredOffset, t);
    }

    private Vector3 GetClampedDesiredPosition()
    {
        Vector3 desiredPosition = GetTargetFocusPosition() + cameraOffset;
        if (!useBounds)
        {
            return desiredPosition;
        }

        Vector3 desiredTargetPosition = desiredPosition - cameraOffset;
        desiredTargetPosition.x = Mathf.Clamp(desiredTargetPosition.x, minTargetPosition.x, maxTargetPosition.x);
        desiredTargetPosition.z = Mathf.Clamp(desiredTargetPosition.z, minTargetPosition.y, maxTargetPosition.y);
        return desiredTargetPosition + cameraOffset;
    }

    private Vector3 GetTargetFocusPosition()
    {
        if (target == null)
        {
            return transform.position;
        }

        Vector3 focusPosition = target.position + focusOffset;
        if (useViewportVerticalFraming)
        {
            focusPosition += GetViewportVerticalFramingOffset(focusPosition);
        }

        return focusPosition;
    }

    private Vector3 GetViewportVerticalFramingOffset(Vector3 focusPosition)
    {
        if (followCamera == null || Mathf.Approximately(targetViewportY, 0.5f))
        {
            return Vector3.zero;
        }

        Vector3 screenUpOnGround = Vector3.ProjectOnPlane(transform.up, Vector3.up);
        if (screenUpOnGround.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        float viewHeightAtTarget = GetViewHeightAt(focusPosition);
        float centerToTargetOffset = 0.5f - targetViewportY;
        return screenUpOnGround.normalized * (viewHeightAtTarget * centerToTargetOffset);
    }

    private float GetViewHeightAt(Vector3 focusPosition)
    {
        if (followCamera.orthographic)
        {
            return followCamera.orthographicSize * 2f;
        }

        float distanceToFocus = Mathf.Abs(Vector3.Dot(focusPosition - transform.position, transform.forward));
        return 2f * distanceToFocus * Mathf.Tan(followCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
    }

    private void CenterBoundsOnTarget()
    {
        Vector2 boundsSize = maxTargetPosition - minTargetPosition;
        Vector3 targetPosition = GetTargetFocusPosition();
        Vector2 targetCenter = new Vector2(targetPosition.x, targetPosition.z);

        minTargetPosition = targetCenter - boundsSize * 0.5f;
        maxTargetPosition = targetCenter + boundsSize * 0.5f;
    }

    private void FindTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null && !string.IsNullOrWhiteSpace(targetName))
        {
            player = GameObject.Find(targetName);
        }

        if (player != null)
        {
            target = player.transform;
        }
    }

    public void SetZoomLocked(bool locked)
    {
        zoomLocked = locked;
    }
}
