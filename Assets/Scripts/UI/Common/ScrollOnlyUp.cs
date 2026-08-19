using UnityEngine;
using UnityEngine.UI;

public class ScrollOnlyUp : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;

    private RectTransform content;
    private float minY;   // starting Y (top position)

    private void Awake()
    {
        if (!scrollRect)
            scrollRect = GetComponent<ScrollRect>();

        content = scrollRect.content;
        // Save the initial anchored Y (this is the highest we allow)
        minY = content.anchoredPosition.y;
    }

    private void LateUpdate()
    {
        if (!content) return;

        Vector2 pos = content.anchoredPosition;

        // If content moved DOWN (Y < minY), clamp it back
        if (pos.y < minY)
        {
            pos.y = minY;
            content.anchoredPosition = pos;

            // Kill any velocity that tries to pull it downward again
            Vector2 vel = scrollRect.velocity;
            if (vel.y < 0)
                scrollRect.velocity = new Vector2(vel.x, 0);
        }
    }
}
