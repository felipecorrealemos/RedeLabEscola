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
    [SerializeField] private Vector3 triggerSize = new Vector3(2f, 2f, 2f);
    [SerializeField] private Vector3 triggerCenter = new Vector3(0f, 1f, 0f);
    [SerializeField] private Animator animator;
    [SerializeField, Tooltip("Animator Controller do professor, com Standard Idle/Pointing/Carrying e os parâmetros Point/IsCarrying.")]
    private RuntimeAnimatorController animatorController;
    [SerializeField] private string carryingParameter = "IsCarrying";
    [SerializeField] private string carryingStateName = "Carrying";
    [SerializeField] [Min(0f)] private float carryingTransitionDuration = 0.04f;
    [SerializeField] private bool useHandIk = false;
    [SerializeField] private AvatarIKGoal documentHandIkGoal = AvatarIKGoal.RightHand;
    [SerializeField] private Vector3 handIkLocalPosition = new Vector3(0.32f, 1.05f, 0.28f);
    [SerializeField] private Vector3 handIkLocalEulerAngles = new Vector3(75f, 0f, 0f);
    [SerializeField] [Range(0f, 1f)] private float handIkWeight = 1f;
    [SerializeField] [Min(0.05f)] private float receiveHandBlendDuration = 0.35f;
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;
    private InteractionPromptPresenter promptPresenter;
    private ProfessorSpeechBubbleUI speechBubbleUi;

    private BoxCollider triggerCollider;
    private bool hasCarryingParameter;
    private bool isHoldingDocument;
    private Coroutine handIkBlendRoutine;
    private float currentHandIkWeight;
    private bool hasShownReceivedSpeech;

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
        speechBubbleUi = FindObjectOfType<ProfessorSpeechBubbleUI>(true);
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

        Transform receiverAnchor = DocumentAnchor;
        if (receiverAnchor == null)
        {
            Debug.LogError("DocumentHoldPoint não está configurado no Professor.", this);
            return;
        }

        document.DeliverTo(receiverAnchor);
        if (!document.IsDelivered)
        {
            return;
        }

        isHoldingDocument = true;
        PlayReceiveDocumentAnimation();
        ShowReceivedDocumentSpeechOnce();
    }

    public void SetPromptVisible(bool visible)
    {
        EnsurePrompt();
        if (visible)
        {
            promptPresenter?.Show(this, "PROFESSOR", new InteractionPromptAction("F", "Entregar documento"));
        }
        else
        {
            promptPresenter?.Hide(this);
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
            return;
        }

        if (anchorParent != null && documentAnchor.parent != anchorParent)
        {
            documentAnchor.SetParent(anchorParent, false);
        }

    }

    private Transform FindExistingDocumentAnchor()
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null
                && (children[i].name == "DocumentHoldPoint" || children[i].name == "DocumentAnchor"))
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

    private void EnsureAnimator()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            if (animator.runtimeAnimatorController == null && animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
            }

            ProfessorDocumentIkRelay relay = animator.GetComponent<ProfessorDocumentIkRelay>();
            if (relay == null)
            {
                relay = animator.gameObject.AddComponent<ProfessorDocumentIkRelay>();
            }

            relay.Configure(this);
        }

        hasCarryingParameter = false;
        if (animator == null
            || animator.runtimeAnimatorController == null
            || !animator.isActiveAndEnabled
            || !animator.isInitialized
            || string.IsNullOrWhiteSpace(carryingParameter))
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
    }

    private void PlayReceiveDocumentAnimation()
    {
        EnsureAnimator();
        if (animator == null)
        {
            return;
        }

        animator.speed = 1f;
        if (hasCarryingParameter)
        {
            animator.SetBool(carryingParameter, true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(carryingStateName))
        {
            int stateHash = Animator.StringToHash(carryingStateName);
            int fullPathHash = Animator.StringToHash("Base Layer." + carryingStateName);
            if (animator.HasState(0, stateHash))
            {
                animator.CrossFade(stateHash, carryingTransitionDuration, 0);
            }
            else if (animator.HasState(0, fullPathHash))
            {
                animator.CrossFade(fullPathHash, carryingTransitionDuration, 0);
            }
        }
    }

    internal void ApplyDocumentHandIk(int layerIndex)
    {
        if (!isHoldingDocument || !useHandIk || animator == null || animator.avatar == null || !animator.avatar.isHuman)
        {
            return;
        }

        Vector3 targetPosition = transform.TransformPoint(handIkLocalPosition);
        Quaternion targetRotation = transform.rotation * Quaternion.Euler(handIkLocalEulerAngles);
        animator.SetIKPositionWeight(documentHandIkGoal, currentHandIkWeight);
        animator.SetIKRotationWeight(documentHandIkGoal, currentHandIkWeight);
        animator.SetIKPosition(documentHandIkGoal, targetPosition);
        animator.SetIKRotation(documentHandIkGoal, targetRotation);
    }

    private IEnumerator BlendDocumentHandIk()
    {
        float duration = Mathf.Max(receiveHandBlendDuration, 0.05f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            currentHandIkWeight = Mathf.SmoothStep(0f, handIkWeight, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        currentHandIkWeight = handIkWeight;
        handIkBlendRoutine = null;
    }

    private void ShowReceivedDocumentSpeechOnce()
    {
        if (hasShownReceivedSpeech)
        {
            return;
        }

        if (speechBubbleUi == null)
        {
            speechBubbleUi = FindObjectOfType<ProfessorSpeechBubbleUI>(true);
        }

        if (speechBubbleUi == null)
        {
            Debug.LogWarning("ProfessorSpeechBubbleUI não foi encontrado na cena.", this);
            return;
        }

        hasShownReceivedSpeech = true;
        speechBubbleUi.ShowOnce();
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

        GameObject legacyPrompt = promptObject;
        promptPresenter = InteractionPromptPresenter.GetOrCreate(canvas);
        promptObject = promptPresenter != null ? promptPresenter.gameObject : null;
        promptLabel = null;

        if (legacyPrompt != null && legacyPrompt != promptObject && legacyPrompt.name == "ProfessorDocumentPrompt")
        {
            legacyPrompt.SetActive(false);
            Destroy(legacyPrompt);
        }

        Transform legacyCanvasPrompt = canvas.transform.Find("ProfessorDocumentPrompt");
        if (legacyCanvasPrompt != null && legacyCanvasPrompt.gameObject != promptObject)
        {
            legacyCanvasPrompt.gameObject.SetActive(false);
            Destroy(legacyCanvasPrompt.gameObject);
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

        return null;
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

[DisallowMultipleComponent]
public sealed class ProfessorDocumentIkRelay : MonoBehaviour
{
    private ProfessorDocumentReceiver receiver;

    public void Configure(ProfessorDocumentReceiver target)
    {
        receiver = target;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        receiver?.ApplyDocumentHandIk(layerIndex);
    }
}
