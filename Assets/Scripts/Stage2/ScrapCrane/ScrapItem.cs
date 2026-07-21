using UnityEngine;

[DisallowMultipleComponent]
public class ScrapItem : MonoBehaviour
{
    [SerializeField] private bool canBeGrabbed = true;
    [SerializeField] private Transform preferredGrabRoot;

    private bool waitingForCrusherDropContact;
    private bool touchedCrusherDropContact;

    public bool CanBeGrabbed => canBeGrabbed;
    public bool CanBeConsumedByCrusher => !waitingForCrusherDropContact || touchedCrusherDropContact;
    public Transform GrabRoot => preferredGrabRoot != null ? preferredGrabRoot : transform;

    public void SetCanBeGrabbed(bool value)
    {
        canBeGrabbed = value;
    }

    public void MarkReleasedForCrusherDrop()
    {
        waitingForCrusherDropContact = true;
        touchedCrusherDropContact = false;
    }

    public void MarkCrusherDropContact()
    {
        if (waitingForCrusherDropContact)
        {
            touchedCrusherDropContact = true;
        }
    }

    public void ClearCrusherDropState()
    {
        waitingForCrusherDropContact = false;
        touchedCrusherDropContact = false;
    }
}
