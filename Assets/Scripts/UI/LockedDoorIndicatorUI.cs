using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LockedDoorIndicatorUI : MonoBehaviour
{
    [Header("UI compartilhada da cena")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text label;
    [SerializeField] private Sprite blockedPathIcon;

    [Header("Detecção")]
    [SerializeField, Min(0.25f)] private float appearanceDistance = 1.8f;
    [SerializeField, Range(-1f, 1f)] private float minimumFacingDot = 0.45f;
    [SerializeField] private Vector3 fallbackWorldOffset = new Vector3(0f, 1.65f, 0f);

    [Header("Apresentação")]
    [SerializeField] private Vector2 screenOffset = Vector2.zero;
    [SerializeField, Min(0f)] private float remainVisibleSeconds = 1.5f;
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.25f;

    private NetworkDoorDevice[] doors;
    private NetworkDoorDevice displayedDoor;
    private Transform player;
    private Camera worldCamera;
    private float visibleUntil;
    private float nextDoorRefreshTime;

#if UNITY_EDITOR
    public void ConfigureEditor(Canvas targetCanvas, RectTransform targetPanel, CanvasGroup targetGroup,
        RectTransform targetIconRect, Image targetIconImage, Text targetLabel, Sprite icon)
    {
        canvas = targetCanvas;
        panel = targetPanel;
        group = targetGroup;
        iconRect = targetIconRect;
        iconImage = targetIconImage;
        label = targetLabel;
        blockedPathIcon = icon;
        if (iconImage != null) iconImage.sprite = blockedPathIcon;
        if (group != null) group.alpha = 0f;
    }
#endif

    private void Awake()
    {
        CacheUiReferences();
        CacheRuntimeReferences(true);
        if (canvas == null || panel == null || group == null || iconRect == null || label == null)
        {
            Debug.LogError("LockedDoorIndicatorCanvas está incompleto. Execute o configurador fora do Play Mode.", this);
            enabled = false;
            return;
        }
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void Update()
    {
        CacheRuntimeReferences(false);
        NetworkDoorDevice targetDoor = FindBestDoor();
        if (targetDoor != null)
        {
            displayedDoor = targetDoor;
            visibleUntil = Time.time + remainVisibleSeconds;
        }

        bool visible = displayedDoor != null && !displayedDoor.IsOpen
            && (targetDoor != null || Time.time < visibleUntil);
        if (visible) UpdateProjectedPosition(displayedDoor);
        float targetAlpha = visible && panel.gameObject.activeSelf ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, Time.deltaTime / Mathf.Max(fadeDuration, 0.05f));
        if (!visible && group.alpha <= 0.001f) displayedDoor = null;
    }

    private NetworkDoorDevice FindBestDoor()
    {
        if (player == null || doors == null) return null;
        NetworkDoorDevice best = null;
        float bestDistance = float.MaxValue;
        foreach (NetworkDoorDevice candidate in doors)
        {
            if (candidate == null || candidate.IsOpen) continue;
            Vector3 delta = GetAnchorPosition(candidate) - player.position;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance > appearanceDistance * appearanceDistance) continue;
            Vector3 forward = player.forward;
            forward.y = 0f;
            if (delta.sqrMagnitude > 0.0001f && Vector3.Dot(forward.normalized, delta.normalized) < minimumFacingDot) continue;
            if (sqrDistance < bestDistance) { bestDistance = sqrDistance; best = candidate; }
        }
        return best;
    }

    private Vector3 GetAnchorPosition(NetworkDoorDevice targetDoor)
    {
        if (targetDoor.UiTextAnchor != null) return targetDoor.UiTextAnchor.position;
        Transform pivot = targetDoor.DoorPivot != null ? targetDoor.DoorPivot : targetDoor.transform;
        return pivot.position + fallbackWorldOffset;
    }

    private void UpdateProjectedPosition(NetworkDoorDevice targetDoor)
    {
        if (worldCamera == null || canvas == null || panel == null) return;
        Vector3 screenPoint = worldCamera.WorldToScreenPoint(GetAnchorPosition(targetDoor));
        bool onScreen = screenPoint.z > 0f && screenPoint.x >= 0f && screenPoint.x <= Screen.width
            && screenPoint.y >= 0f && screenPoint.y <= Screen.height;
        panel.gameObject.SetActive(onScreen);
        if (!onScreen) return;
        RectTransform canvasRect = canvas.transform as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
        panel.anchoredPosition = localPoint + screenOffset;
    }

    private void CacheRuntimeReferences(bool force)
    {
        if (worldCamera == null) worldCamera = Camera.main;
        if (player == null)
        {
            PlayerTopDownController controller = FindObjectOfType<PlayerTopDownController>();
            if (controller != null) player = controller.transform;
        }
        if (force || doors == null || Time.time >= nextDoorRefreshTime)
        {
            doors = FindObjectsOfType<NetworkDoorDevice>(true);
            nextDoorRefreshTime = Time.time + 1f;
        }
    }

    private void CacheUiReferences()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        Transform panelTransform = transform.Find("LockedDoorIndicator");
        if (panel == null && panelTransform != null) panel = panelTransform as RectTransform;
        if (group == null && panelTransform != null) group = panelTransform.GetComponent<CanvasGroup>();
        Transform iconTransform = panelTransform != null ? panelTransform.Find("BlockedIcon") : null;
        if (iconRect == null && iconTransform != null) iconRect = iconTransform as RectTransform;
        if (iconImage == null && iconTransform != null) iconImage = iconTransform.GetComponent<Image>();
        Transform labelTransform = panelTransform != null ? panelTransform.Find("Label") : null;
        if (label == null && labelTransform != null) label = labelTransform.GetComponent<Text>();
    }
}
