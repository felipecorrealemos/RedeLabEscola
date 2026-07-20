using UnityEngine;

[DisallowMultipleComponent]
public class ScrapCraneInputController : MonoBehaviour
{
    [SerializeField] private ScrapCraneController craneController;
    [SerializeField] private KeyCode actionKey = KeyCode.Alpha1;
    [SerializeField] private bool allowKeypadNumbers = true;

    private bool inputEnabled;

    private void Awake()
    {
        if (craneController == null)
        {
            craneController = GetComponent<ScrapCraneController>();
        }
    }

    private void Update()
    {
        if (!inputEnabled || craneController == null)
        {
            return;
        }

        Vector2 horizontalInput = new Vector2(
            GetAxis(KeyCode.A, KeyCode.D),
            GetAxis(KeyCode.S, KeyCode.W));
        craneController.MoveHorizontal(horizontalInput, Time.deltaTime);

        if (Input.GetKeyDown(actionKey) || (allowKeypadNumbers && Input.GetKeyDown(KeyCode.Keypad1)))
        {
            craneController.StartPrimaryAction();
        }
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void AssignController(ScrapCraneController controller)
    {
        craneController = controller;
    }

    private static float GetAxis(KeyCode negative, KeyCode positive)
    {
        float value = 0f;
        if (Input.GetKey(negative))
        {
            value -= 1f;
        }

        if (Input.GetKey(positive))
        {
            value += 1f;
        }

        return value;
    }
}
