using UnityEngine;

[DisallowMultipleComponent]
public class ScrapCrusherIntakeTrigger : MonoBehaviour
{
    [SerializeField] private ScrapCrusherController crusher;

    public void AssignCrusher(ScrapCrusherController targetCrusher)
    {
        crusher = targetCrusher;
    }

    private void Awake()
    {
        if (crusher == null)
        {
            crusher = GetComponentInParent<ScrapCrusherController>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (crusher != null)
        {
            crusher.NotifyIntakeTrigger(other);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (crusher != null)
        {
            crusher.NotifyIntakeTrigger(other);
        }
    }
}
