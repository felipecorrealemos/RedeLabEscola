using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class ProfessorDocumentReceiver : MonoBehaviour
{
    [SerializeField] private string promptText = "F entregar documento";
    [SerializeField] private Transform documentAnchor;
    [SerializeField] private Vector3 generatedAnchorLocalPosition = new Vector3(0.42f, 1.18f, 0.34f);
    [SerializeField] private Vector3 generatedAnchorLocalEulerAngles = new Vector3(72f, 0f, 12f);
    [SerializeField] private Vector3 triggerSize = new Vector3(2f, 2f, 2f);
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 1f, 0f);
    [SerializeField] private Animator animator;
    [SerializeField] private string carryingParameter = "IsCarrying";
    [SerializeField] private string carryingStateName = "Idle";
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;

    private BoxCollider triggerCollider;
    private bool hasCarryingParameter;

    public Transform DocumentAnchor
    {
        get
        {
            EnsureDocumentAnchor();
            return documentAnchor;
        }
    }

    private void Awake()
    {
        EnsureTrigger();
        EnsureDocumentAnchor();
        EnsureAnimator();
        EnsurePrompt();
        SetPromptVisible(false);
    }

    private void OnValidate()
    {
        EnsureTrigger();
    }

    public void Receive(PrintedDocumentInteractable document)
    {
        if (document == null || !document.IsCarried)
        {
            return;
        }

        document.DeliverTo(DocumentAnchor);
        SetCarryingAnimation(true);
    }

    public void SetPromptVisible(bool visible)
    {
        EnsurePrompt();
        if (promptObject != null)
        {
            promptObject.SetActive(visible);
        }
    }

    private void EnsureTrigger()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<BoxCollider>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
            triggerCollider.size = triggerSize;
            triggerCollider.center = triggerCenter;
        }
    }

    private void EnsureDocumentAnchor()
    {
        if (documentAnchor != null)
        {
            return;
        }

        Transform existingAnchor = transform.Find("DocumentAnchor");
        if (existingAnchor != null)
        {
            documentAnchor = existingAnchor;
            ApplyDocumentAnchorDefaults();
            return;
        }

        GameObject anchorObject = new GameObject("DocumentAnchor");
        documentAnchor = anchorObject.transform;
        documentAnchor.SetParent(transform, false);
        ApplyDocumentAnchorDefaults();
    }

    private void ApplyDocumentAnchorDefaults()
    {
        if (documentAnchor == null)
        {
            return;
        }

        documentAnchor.localPosition = generatedAnchorLocalPosition;
        documentAnchor.localRotation = Quaternion.Euler(generatedAnchorLocalEulerAngles);
        documentAnchor.localScale = Vector3.one;
    }

    private void EnsureAnimator()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        hasCarryingParameter = false;
        if (animator == null || string.IsNullOrWhiteSpace(carryingParameter))
        {
            return;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == carryingParameter)
            {
                hasCarryingParameter = true;
                return;
            }
        }
    }

    private void SetCarryingAnimation(bool carrying)
    {
        EnsureAnimator();
        if (animator != null && hasCarryingParameter)
        {
            animator.SetBool(carryingParameter, carrying);
        }

        if (animator != null && carrying && !string.IsNullOrWhiteSpace(carryingStateName))
        {
            animator.CrossFade(carryingStateName, 0.12f);
        }
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

        EnsureEventSystem();

        if (promptObject == null)
        {
            Transform existingPrompt = canvas.transform.Find("ProfessorDocumentPrompt");
            if (existingPrompt != null)
            {
                promptObject = existingPrompt.gameObject;
                promptLabel = existingPrompt.GetComponentInChildren<Text>(true);
            }
        }

        if (promptObject == null)
        {
            promptObject = new GameObject("ProfessorDocumentPrompt");
            promptObject.transform.SetParent(canvas.transform, false);
            RectTransform promptRect = promptObject.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 0f);
            promptRect.anchoredPosition = new Vector2(0f, 132f);
            promptRect.sizeDelta = new Vector2(380f, 42f);

            Image background = promptObject.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.55f);

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
            promptLabel.text = promptText;
            promptLabel.alignment = TextAnchor.MiddleCenter;
            promptLabel.color = Color.white;
            promptLabel.font = GetDefaultFont();
            promptLabel.fontSize = 18;
        }
    }

    private Canvas FindCanvasByName(string canvasName)
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i].name == canvasName)
            {
                return canvases[i];
            }
        }

        return FindObjectOfType<Canvas>();
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
    }

    private Font GetDefaultFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }
}
