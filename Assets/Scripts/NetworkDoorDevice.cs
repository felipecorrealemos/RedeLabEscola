using UnityEngine;

[DisallowMultipleComponent]
public class NetworkDoorDevice : MonoBehaviour
{
    [SerializeField] private string deviceLabel = "Dispositivo da porta";
    [SerializeField] private bool autoAssignPreferredIp = false;
    [SerializeField] private string preferredIpAddress = "";
    [SerializeField] private Transform doorPivot;
    [SerializeField, Tooltip("Ponto 3D projetado na tela para posicionar o aviso de porta trancada. Procura automaticamente um filho chamado 'UI Text'.")]
    private Transform uiTextAnchor;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField, Tooltip("Estado atual comandado para a porta. Visível para diagnóstico durante o Play Mode.")]
    private bool isOpen;

    [Header("Interação local")]
    [SerializeField, Min(0.15f)] private float interactionRadius = 0.85f;
    [SerializeField, Range(-1f, 1f)] private float minimumFacingDot = 0.35f;
    [SerializeField] private Vector3 interactionCenterOffset = new Vector3(0f, 0.45f, 0f);

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool controlledByAccessGroup;
    private bool capturedClosedRotation;
    private Transform capturedDoorPivot;

    public string DeviceLabel => deviceLabel;
    public bool AutoAssignPreferredIp => autoAssignPreferredIp;
    public string PreferredIpAddress => preferredIpAddress;
    public bool IsControlledByAccessGroup => controlledByAccessGroup;
    public bool IsOpen
    {
        get => isOpen;
        private set => isOpen = value;
    }
    public string StateLabel => IsOpen ? "Aberta" : "Fechada";
    public string ActionLabel => IsOpen ? "Fechar" : "Abrir";
    public bool CanOperate
    {
        get
        {
            ComputerInteractable networkDevice = GetComponent<ComputerInteractable>();
            return doorPivot != null
                && networkDevice != null
                && networkDevice.IsNetworkOperational
                && !IsProgressionLocked();
        }
    }

    public Transform DoorPivot => doorPivot;
    public Quaternion ClosedLocalRotation
    {
        get
        {
            EnsureRotationTargetsForAssignedPivot();
            return closedRotation;
        }
    }
    public Transform UiTextAnchor
    {
        get
        {
            ResolveUiTextAnchor();
            return uiTextAnchor;
        }
    }

    public bool CanPlayerInteract(Transform player)
    {
        if (player == null)
        {
            return false;
        }

        Vector3 center = transform.TransformPoint(interactionCenterOffset);
        Vector3 playerPosition = player.position;
        center.y = playerPosition.y;
        Vector3 toDevice = center - playerPosition;
        if (toDevice.sqrMagnitude > interactionRadius * interactionRadius)
        {
            return false;
        }

        toDevice.y = 0f;
        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        if (toDevice.sqrMagnitude <= 0.0001f || playerForward.sqrMagnitude <= 0.0001f)
        {
            return true;
        }

        return Vector3.Dot(playerForward.normalized, toDevice.normalized) >= minimumFacingDot;
    }

    private void Awake()
    {
        ValidateDoorPivotReference();
        ResolveUiTextAnchor();
        CaptureClosedRotation();
    }

    private void Update()
    {
        if (doorPivot == null)
        {
            return;
        }

        EnsureRotationTargetsForAssignedPivot();

        Quaternion targetRotation = IsOpen ? openRotation : closedRotation;
        doorPivot.localRotation = Quaternion.RotateTowards(doorPivot.localRotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    public void Toggle()
    {
        EnsureDoorPivot();
        if (doorPivot == null || !CanOperate || !MissionManager.CanOperateDoorCommand(this))
        {
            return;
        }

        EnsureRotationTargetsForAssignedPivot();

        bool isOpening = !IsOpen;
        IsOpen = isOpening;
        if (isOpening)
        {
            AudioManager.PlayDoorOpen(doorPivot);
        }
        MissionManager.NotifySingleDoorStateChanged(this, IsOpen);
    }

    public void SetControlledByAccessGroup(bool controlled)
    {
        controlledByAccessGroup = controlled;
    }

    public void RestoreOpenState(bool open)
    {
        EnsureDoorPivot();
        if (doorPivot == null) return;
        EnsureRotationTargetsForAssignedPivot();
        IsOpen = open;
        doorPivot.localRotation = open ? openRotation : closedRotation;
    }

    // Restaura somente o efeito funcional derivado das missoes persistidas.
    // Nao chama MissionManager e, portanto, nao conclui a missao de abertura.
    public void RestoreFunctionalStateAfterLoad(bool open)
    {
        EnsureDoorPivot();
        if (doorPivot == null) return;

        RoomProgressionDoorLock progressionLock = doorPivot.GetComponent<RoomProgressionDoorLock>();
        progressionLock?.Unlock();
        RestoreOpenState(open);
    }

    public void SetOpenFromAccessGroup(bool open)
    {
        EnsureDoorPivot();
        if (doorPivot == null || !CanOperate || !MissionManager.CanOperateDoorCommand(this))
        {
            return;
        }

        EnsureRotationTargetsForAssignedPivot();

        IsOpen = open;
        MissionManager.NotifySingleDoorStateChanged(this, IsOpen);
    }

    public void CloseFromRoomTransition()
    {
        if (doorPivot == null)
        {
            return;
        }

        EnsureRotationTargetsForAssignedPivot();
        IsOpen = false;
    }

    private void CaptureClosedRotation()
    {
        EnsureDoorPivot();
        if (doorPivot == null)
        {
            return;
        }

        closedRotation = doorPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        capturedClosedRotation = true;
        capturedDoorPivot = doorPivot;
    }

    private void EnsureRotationTargetsForAssignedPivot()
    {
        if (doorPivot != null && (!capturedClosedRotation || capturedDoorPivot != doorPivot))
        {
            CaptureClosedRotation();
        }
    }

    private void EnsureDoorPivot()
    {
        // Door Pivot é uma referência obrigatória e deliberadamente não possui
        // resolução automática. Cada dispositivo deve apontar explicitamente
        // para a porta física que controla.
    }

    private void ValidateDoorPivotReference()
    {
        if (doorPivot == null)
        {
            Debug.LogError(
                $"NetworkDoorDevice '{name}' está sem Door Pivot. Atribua explicitamente o Pivot da porta no Inspector.",
                this);
        }
    }

    private Transform FindNearestUiTextAnchor()
    {
        Transform areaRoot = FindAreaRoot(transform);
        Transform nearest = null;
        float nearestDistance = float.MaxValue;
        foreach (Transform candidate in FindObjectsOfType<Transform>(true))
        {
            if (candidate == null
                || !candidate.name.Equals("UI Text", System.StringComparison.OrdinalIgnoreCase)
                || !IsInSameArea(candidate, areaRoot))
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(candidate.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private Transform FindPivotForUiTextAnchor(Transform anchor)
    {
        if (anchor == null || anchor.parent == null)
        {
            return null;
        }

        Transform directPivot = anchor.parent.Find("Pivot");
        if (directPivot != null)
        {
            return directPivot;
        }

        return FindNearestPivotUnder(anchor.parent);
    }

    private bool IsProgressionLocked()
    {
        EnsureDoorPivot();
        RoomProgressionDoorLock progressionLock = doorPivot != null
            ? doorPivot.GetComponent<RoomProgressionDoorLock>()
            : null;
        return progressionLock != null && progressionLock.IsLocked;
    }

    private void ResolveUiTextAnchor()
    {
        if (uiTextAnchor != null)
        {
            return;
        }

        uiTextAnchor = FindNearestUiTextAnchor();
        if (uiTextAnchor != null)
        {
            return;
        }

        // A âncora da UI normalmente é irmã do Pivot:
        // Door
        //   Pivot
        //   UI Text
        // Portanto a busca deve começar no objeto Door externo, não no Pivot.
        Transform searchRoot = doorPivot != null && doorPivot.parent != null
            ? doorPivot.parent
            : doorPivot != null ? doorPivot : transform;
        Transform[] candidates = searchRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in candidates)
        {
            if (candidate != null && candidate.name.Equals("UI Text", System.StringComparison.OrdinalIgnoreCase))
            {
                uiTextAnchor = candidate;
                return;
            }
        }
    }

    private void OnValidate()
    {
        // Não percorre a cena aqui. OnValidate também é executado durante a
        // desserialização de prefabs, quando alguns Transforms ainda não passaram
        // pelo Awake. A resolução opcional do UI Text acontece somente em runtime.
    }

    private Transform FindNearestDoorPivot()
    {
        Transform[] transforms = FindObjectsOfType<Transform>(true);
        Transform areaRoot = FindAreaRoot(transform);
        Transform nearestPivot = null;
        float nearestDistance = float.MaxValue;

        foreach (Transform candidate in transforms)
        {
            if (candidate == null
                || candidate.IsChildOf(transform)
                || !IsInSameArea(candidate, areaRoot)
                || !IsPivotTransform(candidate)
                || !ContainsDoorTransform(candidate))
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(candidate.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestPivot = candidate;
            }
        }

        if (nearestPivot != null)
        {
            return nearestPivot;
        }

        foreach (Transform candidate in transforms)
        {
            if (candidate == null
                || candidate.IsChildOf(transform)
                || !IsInSameArea(candidate, areaRoot)
                || !IsDoorTransform(candidate))
            {
                continue;
            }

            Transform pivot = ResolvePivotForDoorTransform(candidate);
            if (pivot == null)
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(pivot.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestPivot = pivot;
            }
        }

        return nearestPivot;
    }

    private bool IsDoorTransform(Transform candidate)
    {
        string lowerName = candidate.name.ToLowerInvariant();
        return lowerName == "door" || lowerName.StartsWith("door ");
    }

    private Transform ResolvePivotForDoorTransform(Transform candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        if (IsPivotTransform(candidate))
        {
            return candidate;
        }

        Transform parent = candidate.parent;
        while (parent != null)
        {
            if (IsPivotTransform(parent))
            {
                return parent;
            }

            parent = parent.parent;
        }

        return FindNearestPivotUnder(candidate);
    }

    private Transform FindNearestPivotUnder(Transform root)
    {
        Transform nearestPivot = null;
        float nearestDistance = float.MaxValue;
        Transform[] children = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in children)
        {
            if (child == null || !IsPivotTransform(child))
            {
                continue;
            }

            float sqrDistance = Vector3.SqrMagnitude(child.position - transform.position);
            if (sqrDistance < nearestDistance)
            {
                nearestDistance = sqrDistance;
                nearestPivot = child;
            }
        }

        return nearestPivot;
    }

    private bool ContainsDoorTransform(Transform root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child != root && IsDoorTransform(child))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPivotTransform(Transform candidate)
    {
        string lowerName = candidate.name.ToLowerInvariant();
        return lowerName == "pivot" || lowerName == "pivo" || lowerName.StartsWith("piv");
    }

    private Transform FindAreaRoot(Transform origin)
    {
        Transform current = origin;
        while (current != null)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("sala "))
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private bool IsInSameArea(Transform candidate, Transform areaRoot)
    {
        return areaRoot == null || candidate == areaRoot || candidate.IsChildOf(areaRoot);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.TransformPoint(interactionCenterOffset);
        Gizmos.color = new Color(0.1f, 0.75f, 1f, 0.9f);
        Gizmos.DrawWireSphere(center, Mathf.Max(interactionRadius, 0.15f));
    }
}
