using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class PressButtonInteractable : MonoBehaviour
{
    [SerializeField] private UnityEvent onPressed;

    public void Press()
    {
        onPressed?.Invoke();
    }
}
