using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class PrintedDocumentInteractable : MonoBehaviour
{
    [SerializeField] private string promptText = "E pegar documento";
    [SerializeField] private Vector3 carriedLocalPosition = new Vector3(0.08f, 0.093f, 0.04f);
    [SerializeField] private Vector3 carriedLocalEulerAngles = new Vector3(75f, 0f, 0f);
    [SerializeField] private Vector3 carriedLocalScale = new Vector3(0.48f, 0.68f, 1f);
    [SerializeField] private Canvas canvas;
    [SerializeField] private GameObject promptObject;
    [SerializeField] private Text promptLabel;
    private InteractionPromptPresenter promptPresenter;

    private BoxCollider triggerCollider;
    private Transform originalParent;
    private bool isCarried;
    private bool isDelivered;

    public bool CanPickUp => !isCarried && !isDelivered && gameObject.activeInHierarchy;
    public bool IsCarried => isCarried;
    public bool IsDelivered => isDelivered;

    public void PrepareForPrint()
    {
        originalParent = transform.parent;
        isCarried = false;
        isDelivered = false;
        SetPhysicsEnabled(true);
        SetPromptVisible(false);
    }

    private void Awake()
    {
        EnsureTrigger();
        EnsurePrompt();
        SetPromptVisible(false);
    }

    private void OnValidate()
    {
        EnsureTrigger();
    }

    public void PickUp(Transform carryAnchor)
    {
        if (!CanPickUp || carryAnchor == null)
        {
            return;
        }

        originalParent = transform.parent;
        isCarried = true;
        SetPromptVisible(false);
        SetPhysicsEnabled(false);
        transform.SetParent(carryAnchor, false);
        transform.localPosition = carriedLocalPosition;
        transform.localRotation = Quaternion.Euler(carriedLocalEulerAngles);
        transform.localScale = carriedLocalScale;
        MissionManager.NotifyDocumentPickedUp(this);
    }

    public void DeliverTo(Transform receiverAnchor)
    {
        if (!isCarried)
        {
            return;
        }

        isCarried = false;
        isDelivered = true;
        SetPromptVisible(false);
        if (receiverAnchor != null)
        {
            transform.SetParent(receiverAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = GetLocalScaleForWorldScale(carriedLocalScale);
        }
        else
        {
            transform.SetParent(originalParent, true);
        }

        SetPhysicsEnabled(false);
        MissionManager.NotifyDocumentDelivered(this);
    }

    public void RestoreDeliveredState(Transform receiverAnchor)
    {
        isCarried = false;
        isDelivered = true;
        SetPromptVisible(false);
        if (receiverAnchor != null)
        {
            transform.SetParent(receiverAnchor, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = GetLocalScaleForWorldScale(carriedLocalScale);
        }
        SetPhysicsEnabled(false);
    }

    private Vector3 GetLocalScaleForWorldScale(Vector3 worldScale)
    {
        Transform currentParent = transform.parent;
        if (currentParent == null)
        {
            return worldScale;
        }

        Vector3 parentScale = currentParent.lossyScale;
        return new Vector3(
            DivideScale(worldScale.x, parentScale.x),
            DivideScale(worldScale.y, parentScale.y),
            DivideScale(worldScale.z, parentScale.z));
    }

    private float DivideScale(float value, float parentScale)
    {
        if (Mathf.Approximately(parentScale, 0f))
        {
            return value;
        }

        return value / Mathf.Abs(parentScale);
    }

    public void SetPromptVisible(bool visible)
    {
        EnsurePrompt();
        if (visible && CanPickUp)
        {
            promptPresenter?.Show(this, "DOCUMENTO", new InteractionPromptAction("E", "Pegar"));
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
            Vector3 minimumSize = new Vector3(1f, 1f, 0.18f);
            triggerCollider.size = new Vector3(
                Mathf.Max(triggerCollider.size.x, minimumSize.x),
                Mathf.Max(triggerCollider.size.y, minimumSize.y),
                Mathf.Max(triggerCollider.size.z, minimumSize.z));
        }
    }

    private void SetPhysicsEnabled(bool enabled)
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider documentCollider in colliders)
        {
            if (documentCollider != null)
            {
                documentCollider.enabled = enabled;
            }
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

        GameObject legacyPrompt = promptObject;
        promptPresenter = InteractionPromptPresenter.GetOrCreate(canvas);
        promptObject = promptPresenter != null ? promptPresenter.gameObject : null;
        promptLabel = null;

        if (legacyPrompt != null && legacyPrompt != promptObject && legacyPrompt.name == "PrintedDocumentPrompt")
        {
            legacyPrompt.SetActive(false);
            Destroy(legacyPrompt);
        }

        Transform legacyCanvasPrompt = canvas.transform.Find("PrintedDocumentPrompt");
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
