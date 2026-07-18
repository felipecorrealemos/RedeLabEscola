using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class EmpilhadeiraController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool playerNearby;
    [SerializeField] private bool playerDriving;
    [SerializeField] private PlayerTopDownController nearbyPlayer;
    [SerializeField] private PlayerTopDownController currentPlayer;

    [Header("Movimento")]
    [SerializeField] private float forwardForce = 8f;
    [SerializeField] private float reverseForce = 5f;
    [SerializeField] private Vector3 localMovementDirection = Vector3.forward;

    [Header("Interacao")]
    [SerializeField] private Collider interactionTrigger;
    [SerializeField] private Transform driverSeatPoint;
    [SerializeField] private Transform playerExitPoint;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private string enterPromptText = "Aperte E para entrar na empilhadeira";
    [SerializeField] private string exitPromptText = "Aperte E para sair da empilhadeira";

    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;

    private Rigidbody body;
    private Transform originalPlayerParent;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        playerNearby = false;
        playerDriving = false;
        nearbyPlayer = null;
        currentPlayer = null;
        ResolveReferences();
        ConfigureInteractionTrigger();
        EnsurePrompt();
        SetPromptVisible(false);
    }

    private void Reset()
    {
        ResolveReferences();
        ConfigureInteractionTrigger();
    }

    private void Update()
    {
        ValidateNearbyPlayer();
        UpdatePrompt();

        if (!Input.GetKeyDown(interactionKey))
        {
            return;
        }

        if (playerDriving)
        {
            ExitForklift();
        }
        else if (playerNearby)
        {
            EnterForklift();
        }
    }

    private void FixedUpdate()
    {
        if (!playerDriving)
        {
            return;
        }

        Vector3 direction = transform.TransformDirection(localMovementDirection.normalized);

        if (Input.GetKey(KeyCode.W))
        {
            body.AddForce(direction * forwardForce, ForceMode.Force);
        }

        if (Input.GetKey(KeyCode.S))
        {
            body.AddForce(-direction * reverseForce, ForceMode.Force);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyPlayerEnterInteraction(other);
    }

    private void OnTriggerExit(Collider other)
    {
        NotifyPlayerExitInteraction(other);
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
    }

    private void ValidateNearbyPlayer()
    {
        if (!playerNearby || nearbyPlayer == null || playerDriving)
        {
            return;
        }

        if (interactionTrigger == null)
        {
            playerNearby = false;
            nearbyPlayer = null;
            return;
        }

        Vector3 playerPosition = nearbyPlayer.transform.position;
        Vector3 closestPoint = interactionTrigger.ClosestPoint(playerPosition);
        float maxDistance = 0.45f;
        if (Vector3.SqrMagnitude(closestPoint - playerPosition) > maxDistance * maxDistance)
        {
            playerNearby = false;
            nearbyPlayer = null;
        }
    }

    private void EnterForklift()
    {
        if (nearbyPlayer == null || driverSeatPoint == null)
        {
            return;
        }

        currentPlayer = nearbyPlayer;
        originalPlayerParent = currentPlayer.transform.parent;
        currentPlayer.SetExternalMovementLocked(true);
        currentPlayer.SetForkliftDrivingAnimation(true);
        currentPlayer.transform.SetParent(driverSeatPoint, true);
        currentPlayer.transform.SetPositionAndRotation(driverSeatPoint.position, driverSeatPoint.rotation);

        playerDriving = true;
        playerNearby = false;
        SetPromptVisible(false);
    }

    private void ExitForklift()
    {
        if (currentPlayer == null)
        {
            playerDriving = false;
            SetPromptVisible(false);
            return;
        }

        Transform exitPoint = playerExitPoint != null ? playerExitPoint : transform;
        currentPlayer.transform.SetParent(originalPlayerParent, true);
        currentPlayer.transform.SetPositionAndRotation(exitPoint.position, exitPoint.rotation);
        currentPlayer.SetForkliftDrivingAnimation(false);
        currentPlayer.SetExternalMovementLocked(false);

        currentPlayer = null;
        originalPlayerParent = null;
        playerDriving = false;
        SetPromptVisible(false);
    }

    private PlayerTopDownController ResolvePlayer(Collider candidate)
    {
        if (candidate == null)
        {
            return null;
        }

        PlayerTopDownController player = candidate.GetComponentInParent<PlayerTopDownController>();
        if (player != null)
        {
            return player;
        }

        return candidate.CompareTag("Player") ? candidate.GetComponentInParent<PlayerTopDownController>() : null;
    }

    private void ResolveReferences()
    {
        if (interactionTrigger == null)
        {
            Transform triggerTransform = transform.Find("InteractionTrigger");
            interactionTrigger = triggerTransform != null ? triggerTransform.GetComponent<Collider>() : null;
        }

        if (driverSeatPoint == null)
        {
            driverSeatPoint = transform.Find("DriverSeatPoint");
        }

        if (playerExitPoint == null)
        {
            playerExitPoint = transform.Find("PlayerExitPoint");
        }
    }

    private void ConfigureInteractionTrigger()
    {
        if (interactionTrigger == null)
        {
            return;
        }

        interactionTrigger.isTrigger = true;
        if (interactionTrigger.GetComponent<EmpilhadeiraInteractionTrigger>() == null)
        {
            interactionTrigger.gameObject.AddComponent<EmpilhadeiraInteractionTrigger>();
        }
    }

    private void UpdatePrompt()
    {
        if (playerDriving)
        {
            SetPromptText(exitPromptText);
            SetPromptVisible(true);
            return;
        }

        SetPromptText(enterPromptText);
        SetPromptVisible(playerNearby);
    }

    private void EnsurePrompt()
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

        if (promptObject == null)
        {
            Transform existingPrompt = canvas.transform.Find("EmpilhadeiraPrompt");
            if (existingPrompt != null)
            {
                promptObject = existingPrompt.gameObject;
                promptLabel = existingPrompt.GetComponentInChildren<Text>(true);
            }
        }

        if (promptObject == null)
        {
            promptObject = new GameObject("EmpilhadeiraPrompt");
            promptObject.transform.SetParent(canvas.transform, false);

            RectTransform promptRect = promptObject.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 176f);
            promptRect.sizeDelta = new Vector2(420f, 42f);

            Image background = promptObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.65f);

            GameObject labelObject = new GameObject("Text");
            labelObject.transform.SetParent(promptObject.transform, false);
            RectTransform labelRect = labelObject.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            promptLabel = labelObject.AddComponent<Text>();
        }

        if (promptLabel != null)
        {
            promptLabel.alignment = TextAnchor.MiddleCenter;
            promptLabel.color = Color.white;
            promptLabel.font = GetDefaultFont();
            promptLabel.fontSize = 18;
        }
    }

    private Font GetDefaultFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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

    private void SetPromptText(string text)
    {
        EnsurePrompt();
        if (promptLabel != null)
        {
            promptLabel.text = text;
        }
    }

    private void SetPromptVisible(bool visible)
    {
        EnsurePrompt();
        if (promptObject != null)
        {
            promptObject.SetActive(visible);
        }
    }
}
