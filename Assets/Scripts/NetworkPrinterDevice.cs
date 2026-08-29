using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class NetworkPrinterDevice : MonoBehaviour
{
    [SerializeField] private string deviceLabel = "Impressora";
    [SerializeField] private string readyStateLabel = "Pronta";
    [SerializeField] private string printedStateLabel = "Documento impresso";
    [SerializeField] private string waitingStateLabel = "Sem IP";
    [SerializeField] private string actionLabel = "Imprimir documento";
    [SerializeField] private Transform outputSlot;
    [SerializeField] private Vector2 documentSize = new Vector2(0.693872f, 19.54597f);
    [SerializeField] private Vector3 documentStartLocalPosition = new Vector3(0f, -3f, 10f);
    [SerializeField] private Vector3 documentEndLocalPosition = new Vector3(0f, -3f, -3.4f);
    [SerializeField] private Vector3 documentLocalEulerAngles = new Vector3(90f, 0f, 0f);
    [SerializeField] private float printDuration = 3f;

    private PrintedDocumentInteractable printedDocument;
    private Coroutine printRoutine;
    private AudioSource activePrintAudioSource;

    public string DeviceLabel => string.IsNullOrWhiteSpace(deviceLabel) ? "Impressora" : deviceLabel;
    public string StateLabel => HasPrintedDocument ? printedStateLabel : CanPrint ? readyStateLabel : waitingStateLabel;
    public string ActionLabel => actionLabel;
    public bool HasPrintedDocument { get; private set; }

    public bool CanPrint
    {
        get
        {
            ComputerInteractable networkDevice = GetComponent<ComputerInteractable>();
            return networkDevice != null && networkDevice.IsNetworkOperational;
        }
    }

    public bool PrintDocument()
    {
        if (!CanPrint || printRoutine != null || HasPrintedDocument)
        {
            return false;
        }

        HasPrintedDocument = true;
        printRoutine = StartCoroutine(PrintDocumentRoutine());
        MissionManager.NotifyDocumentPrinted(this);
        return true;
    }

    public PrintedDocumentInteractable RestorePrintedDocumentState()
    {
        HasPrintedDocument = true;
        EnsureOutputSlot();
        PrintedDocumentInteractable document = EnsurePrintedDocument();
        if (document == null) return null;

        document.gameObject.SetActive(true);
        document.PrepareForPrint();
        if (outputSlot != null)
        {
            document.transform.SetParent(outputSlot, false);
            document.transform.localPosition = documentEndLocalPosition;
            document.transform.localRotation = Quaternion.Euler(documentLocalEulerAngles);
        }
        document.SetPromptVisible(true);
        return document;
    }

    private IEnumerator PrintDocumentRoutine()
    {
        EnsureOutputSlot();
        PrintedDocumentInteractable document = EnsurePrintedDocument();
        if (document == null || outputSlot == null)
        {
            printRoutine = null;
            yield break;
        }

        document.gameObject.SetActive(true);
        document.PrepareForPrint();
        document.SetPromptVisible(false);
        activePrintAudioSource = AudioManager.StartPrinter(outputSlot);
        Transform documentTransform = document.transform;
        documentTransform.SetParent(outputSlot, false);
        documentTransform.localRotation = Quaternion.Euler(documentLocalEulerAngles);
        documentTransform.localScale = new Vector3(documentSize.x, documentSize.y, 1f);

        float elapsed = 0f;
        float duration = Mathf.Max(printDuration, 0.01f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            documentTransform.localPosition = Vector3.Lerp(documentStartLocalPosition, documentEndLocalPosition, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        documentTransform.localPosition = documentEndLocalPosition;
        AudioManager.StopPrinter(activePrintAudioSource);
        activePrintAudioSource = null;
        printRoutine = null;
    }

    private void OnDisable()
    {
        AudioManager.StopPrinter(activePrintAudioSource);
        activePrintAudioSource = null;
        printRoutine = null;
    }

    private PrintedDocumentInteractable EnsurePrintedDocument()
    {
        if (printedDocument != null)
        {
            return printedDocument;
        }

        EnsureOutputSlot();
        if (outputSlot == null)
        {
            return null;
        }

        Transform existingDocument = outputSlot.Find("DocumentoImpresso");
        if (existingDocument != null)
        {
            printedDocument = existingDocument.GetComponent<PrintedDocumentInteractable>();
            if (printedDocument != null)
            {
                return printedDocument;
            }
        }

        GameObject documentObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        documentObject.name = "DocumentoImpresso";
        documentObject.transform.SetParent(outputSlot, false);
        documentObject.transform.localPosition = documentStartLocalPosition;
        documentObject.transform.localRotation = Quaternion.Euler(documentLocalEulerAngles);
        documentObject.transform.localScale = new Vector3(documentSize.x, documentSize.y, 1f);

        Renderer renderer = documentObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = new Material(GetDefaultShader());
            material.name = "DocumentoImpresso_Material";
            material.color = new Color(1f, 1f, 0.94f, 1f);
            renderer.sharedMaterial = material;
        }

        Collider primitiveCollider = documentObject.GetComponent<Collider>();
        if (primitiveCollider != null)
        {
            Destroy(primitiveCollider);
        }

        BoxCollider collider = documentObject.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(1f, 1f, 0.08f);

        printedDocument = documentObject.AddComponent<PrintedDocumentInteractable>();
        documentObject.SetActive(false);
        return printedDocument;
    }

    private void EnsureOutputSlot()
    {
        if (outputSlot != null)
        {
            return;
        }

        outputSlot = FindChildByName(transform, "Printer_Output_Slot");
        if (outputSlot == null)
        {
            outputSlot = FindChildByName(transform, "Printer_Output_Tray");
        }

        if (outputSlot == null)
        {
            outputSlot = transform;
        }
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private Shader GetDefaultShader()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Lit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return shader;
    }
}
