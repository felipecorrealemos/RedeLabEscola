using UnityEngine;

[DisallowMultipleComponent]
public sealed class AcceleratedClock : MonoBehaviour
{
    private const float MinutesPerHour = 60f;
    private const float MinutesPerTwelveHours = 12f * MinutesPerHour;

    [Header("Hands")]
    [SerializeField] private Transform hourHand;
    [SerializeField] private Transform minuteHand;

    [Header("Time")]
    [SerializeField, Min(0f)] private float gameMinutesPerRealSecond = 1f;
    [SerializeField, Range(0, 23)] private int startHour = 8;
    [SerializeField, Range(0, 59)] private int startMinute;

    [Header("Model Alignment")]
    [Tooltip("Rotation axis expressed in the clock parent's local space.")]
    [SerializeField] private Vector3 rotationAxisInParentSpace = Vector3.right;
    [Tooltip("Use 1 for this model. Change to -1 if a mirrored clock must rotate in the opposite direction.")]
    [SerializeField] private float rotationSign = 1f;
    [Tooltip("Clockwise angle represented by the imported hour-hand mesh before runtime rotation.")]
    [SerializeField, Range(0f, 360f)] private float hourHandModelAngle = 225f;
    [Tooltip("Clockwise angle represented by the imported minute-hand mesh before runtime rotation.")]
    [SerializeField, Range(0f, 360f)] private float minuteHandModelAngle;

    private Quaternion hourHandBaseLocalRotation;
    private Quaternion minuteHandBaseLocalRotation;
    private double simulatedMinutes;
    private bool isInitialized;

    public double SimulatedMinutes => simulatedMinutes;
    public float GameMinutesPerRealSecond => gameMinutesPerRealSecond;

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        if (!isInitialized)
        {
            return;
        }

        simulatedMinutes += Time.deltaTime * gameMinutesPerRealSecond;
        ApplyHandRotations();
    }

    private void Initialize()
    {
        if (hourHand == null || minuteHand == null)
        {
            Debug.LogError("AcceleratedClock requires both hand references.", this);
            enabled = false;
            return;
        }

        if (rotationAxisInParentSpace.sqrMagnitude < Mathf.Epsilon)
        {
            Debug.LogError("AcceleratedClock requires a non-zero rotation axis.", this);
            enabled = false;
            return;
        }

        hourHandBaseLocalRotation = hourHand.localRotation;
        minuteHandBaseLocalRotation = minuteHand.localRotation;
        simulatedMinutes = startHour * MinutesPerHour + startMinute;
        isInitialized = true;
        ApplyHandRotations();
    }

    private void ApplyHandRotations()
    {
        float minutesInTwelveHourCycle = Mathf.Repeat((float)simulatedMinutes, MinutesPerTwelveHours);
        float minuteAngle = Mathf.Repeat(minutesInTwelveHourCycle, MinutesPerHour) / MinutesPerHour * 360f;
        float hourAngle = minutesInTwelveHourCycle / MinutesPerTwelveHours * 360f;
        Vector3 axis = rotationAxisInParentSpace.normalized;
        float direction = rotationSign < 0f ? -1f : 1f;

        minuteHand.localRotation =
            Quaternion.AngleAxis(direction * (minuteAngle - minuteHandModelAngle), axis) *
            minuteHandBaseLocalRotation;

        hourHand.localRotation =
            Quaternion.AngleAxis(direction * (hourAngle - hourHandModelAngle), axis) *
            hourHandBaseLocalRotation;
    }

    private void OnValidate()
    {
        gameMinutesPerRealSecond = Mathf.Max(0f, gameMinutesPerRealSecond);
        rotationSign = rotationSign < 0f ? -1f : 1f;
    }
}
