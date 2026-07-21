using UnityEngine;

[DisallowMultipleComponent]
public class ScrapCrusherDropCatchCollider : MonoBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        MarkScrapContact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        MarkScrapContact(collision);
    }

    private static void MarkScrapContact(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return;
        }

        ScrapItem item = collision.collider.GetComponentInParent<ScrapItem>();
        if (item != null)
        {
            item.MarkCrusherDropContact();
        }
    }
}
