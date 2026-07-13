using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class ProfessorDocumentReceiver : MonoBehaviour
{
    [SerializeField] private string promptText = "F entregar documento";
    [SerializeField] private Transform documentAnchor;
    [SerializeField] private bool preferHandBoneAnchor = true;
    [SerializeField] private HumanBodyBones documentHandBone = HumanBodyBones.RightHand;
    [SerializeField] private Vector3 handAnchorLocalPosition = new Vector3(-0.00115f, 0.00169f, 0.00182f);
    [SerializeField] private Vector3 handAnchorLocalEulerAngles = new Vector3(-36.817f, 88.701f, -18.423f);
    [SerializeField] private Vector3 generatedAnchorLocalPosition = new Vector3(0.42f, 1.18f, 0.34f);
    [SerializeField] private Vector3 generatedAnchorLocalEulerAngles = new Vector3(72f, 0f, 12f);
    [SerializeField] private Vector3 triggerSize = new Vector3(2f, 2f, 2f);
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 1f, 0f);
    [SerializeField] private Animator animator;
    [SerializeField] private string carryingParameter = "IsCarrying";
    [SerializeField] private string carryingStateName = "Carrying";
    [SerializeField] private float carryingTransitionDuration = 0.04f;
    [SerializeField] private bool useHandIk = true;
    [SerializeField] private AvatarIKGoal documentHandIkGoal = AvatarIKGoal.RightHand;
    [SerializeField] private Vector3 handIkLocalPosition = new Vector3(0.32f, 1.05f, 0.28f);
    [SerializeField] private Vector3 handIkLocalEulerAngles = new Vector3(75f, 0f, 0f);
    [SerializeField] [Range(0f, 1f)] private float handIkWeight = 1f;
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;

    private BoxCollider triggerCollider;
    private bool hasCarryingParameter;
    private bool hasCarryingState;
    private int carryingStateHash;
    private int carryingStateLayer;
    private bool isHoldingDocument;
    private Coroutine carryingRoutine;

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
        isHoldingDocument = true;
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
        EnsureAnimator();
        Transform anchorParent = ResolveDocumentAnchorParent();

        if (documentAnchor == null)
        {
            documentAnchor = FindExistingDocumentAnchor();
        }

        if (documentAnchor == null)
        {
            GameObject anchorObject = new GameObject("DocumentAnchor");
            documentAnchor = anchorObject.transform;
        }

        if (anchorParent != null && documentAnchor.parent != anchorParent)
        {
            documentAnchor.SetParent(anchorParent, false);
        }

        ApplyDocumentAnchorDefaults(anchorParent);
    }

    private Transform FindExistingDocumentAnchor()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && children[i].name == "DocumentAnchor")
            {
                return children[i];
            }
        }

        return null;
    }

    private Transform ResolveDocumentAnchorParent()
    {
        if (preferHandBoneAnchor && TryGetDocumentHand(out Transform hand))
        {
            return hand;
        }

        return transform;
    }

    private bool TryGetDocumentHand(out Transform hand)
    {
        hand = null;
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            return false;
        }

        hand = animator.GetBoneTransform(documentHandBone);
        return hand != null;
    }

    private void ApplyDocumentAnchorDefaults(Transform anchorParent)
    {
        if (documentAnchor == null)
        {
            return;
        }

        bool anchoredToHand = anchorParent != null && anchorParent != transform;
        documentAnchor.localPosition = anchoredToHand ? handAnchorLocalPosition : generatedAnchorLocalPosition;
        documentAnchor.localRotation = Quaternion.Euler(anchoredToHand ? handAnchorLocalEulerAngles : generatedAnchorLocalEulerAngles);
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
                break;
            }
        }

        hasCarryingState = false;
        carryingStateLayer = 0;
        if (!string.IsNullOrWhiteSpace(carryingStateName))
        {
            for (int i = 0; i < animator.layerCount; i++)
            {
                int shortHash = Animator.StringToHash(carryingStateName);
                int fullPathHash = Animator.StringToHash(animator.GetLayerName(i) + "." + carryingStateName);
                if (animator.HasState(i, shortHash))
                {
                    carryingStateHash = shortHash;
                    hasCarryingState = true;
                    carryingStateLayer = i;
                    break;
                }

                if (animator.HasState(i, fullPathHash))
                {
                    carryingStateHash = fullPathHash;
                    hasCarryingState = true;
                    carryingStateLayer = i;
                    break;
                }
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

        if (animator != null && carrying && hasCarryingState)
        {
            animator.Play(carryingStateHash, carryingStateLayer, 0f);
            animator.Update(0f);

            if (carryingRoutine != null)
            {
                StopCoroutine(carryingRoutine);
            }

            carryingRoutine = StartCoroutine(ForceCarryingPoseNextFrame());
        }
    }

    private IEnumerator ForceCarryingPoseNextFrame()
    {
        yield return null;

        EnsureAnimator();
        if (isHoldingDocument && animator != null && hasCarryingState)
        {
            animator.Play(carryingStateHash, carryingStateLayer, 0f);
            animator.Update(0f);
        }

        carryingRoutine = null;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (!isHoldingDocument || !useHandIk || animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            return;
        }

        Vector3 targetPosition = transform.TransformPoint(handIkLocalPosition);
        Quaternion targetRotation = transform.rotation * Quaternion.Euler(handIkLocalEulerAngles);
        animator.SetIKPositionWeight(documentHandIkGoal, handIkWeight);
        animator.SetIKRotationWeight(documentHandIkGoal, handIkWeight);
        animator.SetIKPosition(documentHandIkGoal, targetPosition);
        animator.SetIKRotation(documentHandIkGoal, targetRotation);
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
        RuntimeEventSystemUtility.EnsureSingleEventSystem();
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
