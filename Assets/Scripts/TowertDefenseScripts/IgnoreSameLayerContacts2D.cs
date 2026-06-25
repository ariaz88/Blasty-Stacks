using UnityEngine;

public class IgnoreSameLayerContacts2D : MonoBehaviour
{
    private Collider2D[] myColliders;

    void Awake()
    {
        // Grab every collider under this character (parent + children, active or inactive)
        myColliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        TryIgnore(c.gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryIgnore(other.gameObject);
    }

    private void TryIgnore(GameObject other)
    {
        // Only ignore if we're on the same layer
        if (other.layer != gameObject.layer) return;

        var otherCols = other.GetComponentsInChildren<Collider2D>(includeInactive: true);
        if (otherCols == null || otherCols.Length == 0) return;

        // Permanently ignore every collider pair between these two characters
        foreach (var a in myColliders)
        {
            if (!a) continue;
            foreach (var b in otherCols)
            {
                if (!b) continue;
                Physics2D.IgnoreCollision(a, b, true);
            }
        }
    }
}
