using UnityEngine;

[DisallowMultipleComponent]
public class KeyboardTerminalInteractable : MonoBehaviour
{
    private const string InteractionTriggerName = "KeyboardInteractionTrigger";
    private const string InteractionIndicatorName = "KeyboardInteractionIndicator";

    [SerializeField] private ComputerInteractable computer;
    [SerializeField] private float interactionRadius = 1.2f;
    [SerializeField] private Vector3 generatedColliderSize = new Vector3(1.2f, 0.35f, 0.85f);
    [SerializeField] private Vector3 indicatorWorldOffset = new Vector3(0f, -0.08f, 0f);
    [SerializeField] private Vector2 indicatorSize = new Vector2(0.75f, 0.45f);
    [SerializeField] private Color indicatorColor = new Color(1f, 0.85f, 0.15f, 0.45f);
    [SerializeField] private float indicatorPulseAmount = 0.12f;
    [SerializeField] private float indicatorPulseSpeed = 5f;

    private Collider interactionCollider;
    private Transform indicator;
    private Material indicatorMaterial;
    private Vector3 indicatorBaseScale;

    public bool CanUse => computer != null && computer.IsNetworkOperational;
    public ComputerInteractable Computer
    {
        get
        {
            EnsureComputer();
            return computer;
        }
    }
    public Vector3 InteractionPosition => transform.position;

    private void Awake()
    {
        EnsureComputer();
        EnsureInteractionCollider();
        EnsureIndicator();
    }

    private void Update()
    {
        UpdateInteractionColliderScale();
        UpdateIndicator();
    }

    public void Open(PlayerTopDownController player)
    {
        if (CanUse)
        {
            computer.OpenTerminal(player);
        }
    }

    public bool ContainsCollider(Collider candidate)
    {
        EnsureInteractionCollider();
        return candidate != null && candidate == interactionCollider;
    }

    public bool IsPlayerNear(Vector3 playerPosition)
    {
        Vector3 keyboardPosition = transform.position;
        keyboardPosition.y = playerPosition.y;
        return Vector3.SqrMagnitude(keyboardPosition - playerPosition) <= interactionRadius * interactionRadius;
    }

    public void SetPromptVisible(bool visible)
    {
        if (computer != null)
        {
            computer.SetTerminalPromptVisible(visible && CanUse);
        }
    }

    private void EnsureComputer()
    {
        if (computer != null)
        {
            return;
        }

        computer = GetComponentInParent<ComputerInteractable>();
        if (computer == null && transform.parent != null)
        {
            computer = transform.parent.GetComponentInChildren<ComputerInteractable>(true);
        }
    }

    private void EnsureInteractionCollider()
    {
        if (interactionCollider != null)
        {
            UpdateInteractionColliderScale();
            return;
        }

        Transform trigger = transform.Find(InteractionTriggerName);
        if (trigger == null)
        {
            GameObject triggerObject = new GameObject(InteractionTriggerName);
            trigger = triggerObject.transform;
            trigger.SetParent(transform, false);
        }

        trigger.localPosition = Vector3.zero;
        trigger.localRotation = Quaternion.identity;

        BoxCollider triggerCollider = trigger.GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = trigger.gameObject.AddComponent<BoxCollider>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.size = generatedColliderSize;
        interactionCollider = triggerCollider;
        UpdateInteractionColliderScale();
    }

    private void EnsureIndicator()
    {
        if (indicator != null)
        {
            return;
        }

        Transform existingIndicator = transform.Find(InteractionIndicatorName);
        if (existingIndicator != null)
        {
            indicator = existingIndicator;
        }
        else
        {
            GameObject indicatorObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            indicatorObject.name = InteractionIndicatorName;
            indicator = indicatorObject.transform;
            Destroy(indicatorObject.GetComponent<Collider>());
        }

        indicator.SetParent(null, true);
        indicator.rotation = Quaternion.Euler(-90f, 0f, 0f);
        indicatorBaseScale = new Vector3(indicatorSize.x, indicatorSize.y, 1f);
        indicator.localScale = indicatorBaseScale;

        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        indicatorMaterial = new Material(GetIndicatorShader());
        indicatorMaterial.color = new Color(indicatorColor.r, indicatorColor.g, indicatorColor.b, 0f);
        indicatorRenderer.sharedMaterial = indicatorMaterial;
        indicator.gameObject.SetActive(false);
    }

    private void UpdateIndicator()
    {
        EnsureComputer();
        EnsureIndicator();

        bool shouldShow = CanUse && IsPlayerNear(GetPlayerPosition());
        indicator.gameObject.SetActive(shouldShow);
        if (!shouldShow)
        {
            return;
        }

        indicator.position = transform.position + indicatorWorldOffset;
        indicator.rotation = Quaternion.Euler(-90f, 0f, 0f);

        float pulse = 1f + (Mathf.Sin(Time.time * indicatorPulseSpeed) * 0.5f + 0.5f) * indicatorPulseAmount;
        indicator.localScale = indicatorBaseScale * pulse;
        indicatorMaterial.color = indicatorColor;
    }

    private void UpdateInteractionColliderScale()
    {
        if (interactionCollider == null)
        {
            return;
        }

        interactionCollider.transform.localScale = GetInverseLossyScale(transform);
    }

    private Vector3 GetInverseLossyScale(Transform target)
    {
        Vector3 scale = target != null ? target.lossyScale : Vector3.one;
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 1f : 1f / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 1f : 1f / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 1f : 1f / scale.z);
    }

    private Vector3 GetPlayerPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            player = GameObject.Find("Player");
        }

        return player != null ? player.transform.position : transform.position + Vector3.one * 999f;
    }

    private Shader GetIndicatorShader()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }
}
