using UnityEngine;

[DisallowMultipleComponent]
public class ScrapItem : MonoBehaviour
{
    [SerializeField] private bool canBeGrabbed = true;
    [SerializeField] private Transform preferredGrabRoot;

    public bool CanBeGrabbed => canBeGrabbed;
    public Transform GrabRoot => preferredGrabRoot != null ? preferredGrabRoot : transform;
}
