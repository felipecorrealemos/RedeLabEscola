using UnityEngine;

[DisallowMultipleComponent]
public class RoboticArmGripper : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform leftClawPivot;
    [SerializeField] private Transform rightClawPivot;
    [SerializeField] private Transform itemSocket;

    [Header("Claw Positions")]
    [SerializeField] private Vector3 leftOpenLocalPosition = new Vector3(-0.32f, 0f, -0.3f);
    [SerializeField] private Vector3 leftClosedLocalPosition = new Vector3(-0.16f, 0f, -0.3f);
    [SerializeField] private Vector3 rightOpenLocalPosition = new Vector3(0.32f, 0f, -0.3f);
    [SerializeField] private Vector3 rightClosedLocalPosition = new Vector3(0.16f, 0f, -0.3f);
    [SerializeField, Min(0.001f)] private float positionTolerance = 0.01f;

    public Transform ItemSocket => itemSocket;
    public bool IsOpen => IsAt(leftClawPivot, leftOpenLocalPosition) && IsAt(rightClawPivot, rightOpenLocalPosition);
    public bool IsClosed => IsAt(leftClawPivot, leftClosedLocalPosition) && IsAt(rightClawPivot, rightClosedLocalPosition);

    public void Configure(Transform leftPivot, Transform rightPivot, Transform socket)
    {
        leftClawPivot = leftPivot;
        rightClawPivot = rightPivot;
        itemSocket = socket;
    }

    public void CaptureCurrentAsOpen()
    {
        if (leftClawPivot != null)
        {
            leftOpenLocalPosition = leftClawPivot.localPosition;
        }

        if (rightClawPivot != null)
        {
            rightOpenLocalPosition = rightClawPivot.localPosition;
        }
    }

    public void SetClosedFromOpen(float clawTravel)
    {
        leftClosedLocalPosition = leftOpenLocalPosition + Vector3.right * Mathf.Abs(clawTravel);
        rightClosedLocalPosition = rightOpenLocalPosition + Vector3.left * Mathf.Abs(clawTravel);
    }

    public bool MoveOpen(float speed, float deltaTime)
    {
        return MoveTo(leftOpenLocalPosition, rightOpenLocalPosition, speed, deltaTime);
    }

    public bool MoveClosed(float speed, float deltaTime)
    {
        return MoveTo(leftClosedLocalPosition, rightClosedLocalPosition, speed, deltaTime);
    }

    public void Attach(ConveyorItem item, Vector3 localPosition, Vector3 localEulerAngles, bool snapToSocket)
    {
        if (item != null && itemSocket != null)
        {
            item.BeginRoboticCarry(itemSocket, localPosition, localEulerAngles, snapToSocket);
        }
    }

    public void Release(ConveyorItem item, ConveyorController destinationConveyor, Transform dropPoint, bool useDropRotation)
    {
        item?.CompleteRoboticDrop(destinationConveyor, dropPoint, useDropRotation);
    }

    private bool MoveTo(Vector3 leftTarget, Vector3 rightTarget, float speed, float deltaTime)
    {
        float step = Mathf.Max(0.001f, speed) * deltaTime;

        if (leftClawPivot != null)
        {
            leftClawPivot.localPosition = Vector3.MoveTowards(leftClawPivot.localPosition, leftTarget, step);
        }

        if (rightClawPivot != null)
        {
            rightClawPivot.localPosition = Vector3.MoveTowards(rightClawPivot.localPosition, rightTarget, step);
        }

        return IsAt(leftClawPivot, leftTarget) && IsAt(rightClawPivot, rightTarget);
    }

    private bool IsAt(Transform target, Vector3 localPosition)
    {
        return target == null || Vector3.Distance(target.localPosition, localPosition) <= positionTolerance;
    }
}
