using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ScrapCraneControlStation : MonoBehaviour
{
    public enum ControlState
    {
        Inactive,
        EnteringControl,
        Active,
        ExitingControl
    }

    [Header("References")]
    [SerializeField] private ScrapCraneController craneController;
    [SerializeField] private ScrapCraneInputController inputController;
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private PlayerTopDownController currentPlayer;
    [SerializeField] private DeadZoneCameraFollow cameraFollow;
    [SerializeField] private Transform cameraTarget;

    [Header("Interaction")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private bool allowExitByInteractionKey = true;
    [SerializeField] private bool allowExitByEscape = true;
    [SerializeField] private string enterPromptText = "Aperte E para controlar a garra";

    [Header("Camera")]
    [SerializeField] private float cameraTransitionDuration = 0.35f;
    [SerializeField] private bool useSmoothCameraTransition = true;

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;
    [SerializeField] private GameObject commandsPanelObject;
    [SerializeField] private Text commandsPanelLabel;
    [SerializeField] private Sprite craneIconSprite;

    [Header("Debug")]
    [SerializeField] private ControlState currentState = ControlState.Inactive;
    [SerializeField] private bool playerNearby;
    [SerializeField] private bool logWarnings = true;

    private PlayerTopDownController nearbyPlayer;
    private Transform previousCameraTarget;
    private Coroutine transitionRoutine;
    private InteractionPromptPresenter promptPresenter;

    public ControlState CurrentState => currentState;

    private void Awake()
    {
        ResolveReferences();
        EnsureInteractionTrigger();
        EnsureUI();
        SetPromptVisible(false);
        SetCommandsPanelVisible(false);
        SetCraneActive(false);
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Update()
    {
        ValidateNearbyPlayer();
        UpdatePrompt();

        if (currentState == ControlState.Active)
        {
            if ((allowExitByInteractionKey && Input.GetKeyDown(interactionKey)) || (allowExitByEscape && Input.GetKeyDown(KeyCode.Escape)))
            {
                if (Input.GetKeyDown(KeyCode.Escape)) EscapeInputGuard.Consume();
                ExitControl();
            }

            return;
        }

        if (currentState != ControlState.Inactive)
        {
            return;
        }

        if (playerNearby && Input.GetKeyDown(interactionKey))
        {
            EnterControl();
        }
    }

    public void NotifyPlayerEnterInteraction(Collider other)
    {
        PlayerTopDownController player = ResolvePlayer(other);
        if (player == null)
        {
            return;
        }

        nearbyPlayer = player;
        playerNearby = true;
    }

    public void NotifyPlayerExitInteraction(Collider other)
    {
        PlayerTopDownController player = ResolvePlayer(other);
        if (player == null || player != nearbyPlayer)
        {
            return;
        }

        nearbyPlayer = null;
        playerNearby = false;
        SetPromptVisible(false);
    }

    public void AssignReferences(ScrapCraneController crane, ScrapCraneInputController input, Collider trigger, DeadZoneCameraFollow camera, Transform target, Canvas uiCanvas, GameObject prompt, Text promptText, GameObject commandsPanel, Text commandsText)
    {
        if (crane != null)
        {
            craneController = crane;
        }

        if (input != null)
        {
            inputController = input;
        }

        if (trigger != null)
        {
            interactionTrigger = trigger;
        }

        if (camera != null)
        {
            cameraFollow = camera;
        }

        if (target != null)
        {
            cameraTarget = target;
        }

        if (uiCanvas != null)
        {
            canvas = uiCanvas;
        }

        if (prompt != null)
        {
            promptObject = prompt;
        }

        if (promptText != null)
        {
            promptLabel = promptText;
        }

        if (commandsPanel != null)
        {
            commandsPanelObject = commandsPanel;
        }

        if (commandsText != null)
        {
            commandsPanelLabel = commandsText;
        }
    }

    private void EnterControl()
    {
        if (currentState != ControlState.Inactive || nearbyPlayer == null)
        {
            return;
        }

        currentState = ControlState.EnteringControl;
        currentPlayer = nearbyPlayer;
        currentPlayer.SetExternalMovementLocked(true);
        SetPromptVisible(false);
        SetCommandsPanelVisible(true);
        SetCraneActive(true);

        if (cameraFollow != null)
        {
            previousCameraTarget = cameraFollow.Target;
            ChangeCameraTarget(cameraTarget);
        }

        currentState = ControlState.Active;
    }

    private void ExitControl()
    {
        if (currentState != ControlState.Active)
        {
            return;
        }

        currentState = ControlState.ExitingControl;
        SetCraneActive(false);
        SetCommandsPanelVisible(false);

        if (cameraFollow != null)
        {
            ChangeCameraTarget(previousCameraTarget);
        }

        if (currentPlayer != null)
        {
            currentPlayer.SetExternalMovementLocked(false);
        }

        currentPlayer = null;
        previousCameraTarget = null;
        currentState = ControlState.Inactive;
    }

    private void ChangeCameraTarget(Transform target)
    {
        if (cameraFollow == null)
        {
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        if (useSmoothCameraTransition && cameraTransitionDuration > 0f)
        {
            transitionRoutine = StartCoroutine(ChangeCameraTargetAfterFrame(target));
        }
        else
        {
            cameraFollow.SetTarget(target, true);
        }
    }

    private IEnumerator ChangeCameraTargetAfterFrame(Transform target)
    {
        cameraFollow.SetTarget(target, false);
        yield return new WaitForSeconds(cameraTransitionDuration);
        transitionRoutine = null;
    }

    private void SetCraneActive(bool active)
    {
        craneController?.SetControlActive(active);
        inputController?.SetInputEnabled(active);
    }

    private void ResolveReferences()
    {
        if (craneController == null)
        {
            craneController = GetComponentInParent<ScrapCraneController>();
        }

        if (inputController == null && craneController != null)
        {
            inputController = craneController.GetComponent<ScrapCraneInputController>();
        }

        if (cameraFollow == null && Camera.main != null)
        {
            cameraFollow = Camera.main.GetComponent<DeadZoneCameraFollow>();
        }
    }

    private void EnsureInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            Transform existing = transform.Find("CraneInteractionTrigger");
            if (existing != null)
            {
                interactionTrigger = existing.GetComponent<Collider>();
            }
        }

        if (interactionTrigger == null)
        {
            GameObject triggerObject = new GameObject("CraneInteractionTrigger");
            triggerObject.transform.SetParent(transform, false);
            triggerObject.transform.localPosition = Vector3.zero;
            triggerObject.transform.localRotation = Quaternion.identity;
            triggerObject.transform.localScale = Vector3.one;
            BoxCollider box = triggerObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3f, 2.2f, 3f);
            interactionTrigger = box;
        }

        interactionTrigger.isTrigger = true;
        ScrapCraneStationTrigger forwarder = interactionTrigger.GetComponent<ScrapCraneStationTrigger>();
        if (forwarder == null)
        {
            forwarder = interactionTrigger.gameObject.AddComponent<ScrapCraneStationTrigger>();
        }

        forwarder.AssignStation(this);
    }

    private void EnsureUI()
    {
        if (canvas == null)
        {
            canvas = FindCanvasByName("InteractionCanvas");
        }

        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("InteractionCanvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        RuntimeEventSystemUtility.EnsureSingleEventSystem();
        EnsurePrompt();
        EnsureCommandsPanel();
    }

    private void EnsurePrompt()
    {
        GameObject legacyPrompt = promptObject;
        promptPresenter = InteractionPromptPresenter.GetOrCreate(canvas);
        promptObject = promptPresenter != null ? promptPresenter.gameObject : null;
        promptLabel = null;
        if (legacyPrompt != null && legacyPrompt != promptObject && legacyPrompt.name == "ScrapCranePrompt")
        {
            legacyPrompt.SetActive(false);
            Destroy(legacyPrompt);
        }

        Transform legacyCanvasPrompt = canvas != null ? canvas.transform.Find("ScrapCranePrompt") : null;
        if (legacyCanvasPrompt != null && legacyCanvasPrompt.gameObject != promptObject)
        {
            legacyCanvasPrompt.gameObject.SetActive(false);
            Destroy(legacyCanvasPrompt.gameObject);
        }
    }

    private void EnsureCommandsPanel()
    {
        if (commandsPanelObject == null && canvas != null)
        {
            Transform existing = canvas.transform.Find("ScrapCraneCommandsPanel");
            if (existing != null)
            {
                commandsPanelObject = existing.gameObject;
                commandsPanelLabel = existing.GetComponentInChildren<Text>(true);
            }
        }

        bool createdPanel = false;
        if (commandsPanelObject == null && canvas != null)
        {
            commandsPanelObject = new GameObject("ScrapCraneCommandsPanel");
            commandsPanelObject.transform.SetParent(canvas.transform, false);
            RectTransform rect = commandsPanelObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -96f);
            rect.sizeDelta = new Vector2(285f, 280f);

            Image background = commandsPanelObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.62f);

            GameObject labelObject = new GameObject("Text");
            labelObject.transform.SetParent(commandsPanelObject.transform, false);
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12f, 10f);
            labelRect.offsetMax = new Vector2(-12f, -10f);
            commandsPanelLabel = labelObject.AddComponent<Text>();
            createdPanel = true;
        }

        EnsureCommandsPanelChildren(createdPanel);

        RectTransform commandsRect = commandsPanelObject != null ? commandsPanelObject.GetComponent<RectTransform>() : null;
        if (commandsRect != null)
        {
            commandsRect.anchorMin = new Vector2(1f, 1f);
            commandsRect.anchorMax = new Vector2(1f, 1f);
            commandsRect.pivot = new Vector2(1f, 1f);
            commandsRect.anchoredPosition = new Vector2(-24f, -96f);
        }

        if (commandsPanelLabel != null && createdPanel)
        {
            commandsPanelLabel.alignment = TextAnchor.UpperLeft;
            commandsPanelLabel.color = Color.white;
            commandsPanelLabel.font = GetDefaultFont();
            commandsPanelLabel.fontSize = 15;
            commandsPanelLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            commandsPanelLabel.verticalOverflow = VerticalWrapMode.Truncate;
            commandsPanelLabel.text =
                "CONTROLE DA GARRA\n\n" +
                "W A S D - Movimentar\n" +
                "1 - Coletar/Soltar\n" +
                "E ou Esc - Sair";
        }
    }

    private void EnsureCommandsPanelChildren(bool applyDefaultLayout)
    {
        if (commandsPanelObject == null)
        {
            return;
        }

        ConfigureCraneIcon(commandsPanelObject.transform, craneIconSprite, applyDefaultLayout);

        if (commandsPanelLabel != null && applyDefaultLayout)
        {
            RectTransform labelRect = commandsPanelLabel.GetComponent<RectTransform>();
            if (labelRect != null)
            {
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 10f);
                labelRect.offsetMax = new Vector2(-12f, -118f);
            }
        }
    }

    private static void ConfigureCraneIcon(Transform panel, Sprite sprite, bool applyDefaultLayout)
    {
        Transform iconTransform = panel.Find("CraneIcon");
        bool createdIcon = false;
        if (iconTransform == null)
        {
            GameObject iconObject = new GameObject("CraneIcon", typeof(RectTransform));
            iconObject.transform.SetParent(panel, false);
            iconTransform = iconObject.transform;
            createdIcon = true;
        }

        RectTransform iconRect = iconTransform.GetComponent<RectTransform>();
        if (iconRect == null)
        {
            iconRect = iconTransform.gameObject.AddComponent<RectTransform>();
            iconTransform = iconRect.transform;
            createdIcon = true;
        }

        if (applyDefaultLayout || createdIcon)
        {
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.anchoredPosition = new Vector2(0f, -12f);
            iconRect.sizeDelta = new Vector2(104f, 104f);
        }

        Image icon = iconTransform.GetComponent<Image>();
        if (icon == null)
        {
            icon = iconTransform.gameObject.AddComponent<Image>();
        }

        if (sprite != null && icon.sprite == null)
        {
            icon.sprite = sprite;
        }

        icon.preserveAspect = true;
        icon.raycastTarget = false;
    }

    private void UpdatePrompt()
    {
        if (currentState != ControlState.Inactive)
        {
            SetPromptVisible(false);
            return;
        }

        SetPromptVisible(playerNearby);
    }

    private void ValidateNearbyPlayer()
    {
        if (nearbyPlayer != null)
        {
            return;
        }

        playerNearby = false;
    }

    private PlayerTopDownController ResolvePlayer(Collider candidate)
    {
        return candidate != null ? candidate.GetComponentInParent<PlayerTopDownController>() : null;
    }

    private Canvas FindCanvasByName(string canvasName)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == canvasName)
            {
                return canvases[i];
            }
        }

        return FindObjectOfType<Canvas>();
    }

    private void SetPromptVisible(bool visible)
    {
        if (visible)
        {
            promptPresenter?.ShowAmbient(this, "GARRA INDUSTRIAL", new InteractionPromptAction(GetInteractionKeyLabel(), "Controlar"));
        }
        else
        {
            promptPresenter?.Hide(this);
        }
    }

    private string GetInteractionKeyLabel()
    {
        string value = interactionKey.ToString();
        return value.StartsWith("Alpha") ? value.Substring("Alpha".Length) : value.ToUpperInvariant();
    }

    private void SetCommandsPanelVisible(bool visible)
    {
        if (commandsPanelObject != null)
        {
            commandsPanelObject.SetActive(visible);
        }
    }

    private Font GetDefaultFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnDrawGizmosSelected()
    {
        if (interactionTrigger != null)
        {
            Gizmos.color = new Color(0.2f, 1f, 0.25f, 0.2f);
            Gizmos.matrix = interactionTrigger.transform.localToWorldMatrix;
            if (interactionTrigger is BoxCollider box)
            {
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }

        if (cameraTarget != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(cameraTarget.position, 0.35f);
        }
    }

    private void Warn(string message)
    {
        if (logWarnings)
        {
            Debug.LogWarning($"{name}: {message}", this);
        }
    }
}
