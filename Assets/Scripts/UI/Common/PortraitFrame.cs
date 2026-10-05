using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Frames a hero portrait the same way no matter how its PNG is padded.
/// Sits on the masking frame (Mask / RectMask2D); its child Image shows the sprite.
///
/// The visible body is measured from the sprite's TIGHT mesh (sprite.vertices), so the
/// sprite must import with Mesh Type = Tight. The body is scaled so its height fills
/// <see cref="bodyHeightFill"/> of the frame height, and its centre is placed on the frame
/// centre (+ <see cref="focusOffset"/>). Whatever spills outside is clipped by the mask.
/// The art itself is never touched - only the Image's size and position.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PortraitFrame : MonoBehaviour
{
    [SerializeField] private Image image;

    [Tooltip("Visible body height as a fraction of the frame height. 1 = head-to-feet exactly fills the frame.")]
    [Range(0.3f, 2f)]
    [SerializeField] private float bodyHeightFill = 0.9f;

    [Tooltip("Pixels to shift the body centre away from the frame centre.")]
    [SerializeField] private Vector2 focusOffset = Vector2.zero;

    public Image Image => image;

    public void Show(Sprite sprite)
    {
        if (!image) return;

        image.sprite = sprite;
        image.enabled = sprite;
        if (!sprite) return;

        image.preserveAspect = false;   // the size set below already keeps the aspect
        image.useSpriteMesh = false;

        var frame = (RectTransform)transform;
        var rt = image.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        Rect body = VisibleBodyNormalized(sprite);
        Vector2 rectPx = sprite.rect.size;

        float height = bodyHeightFill * frame.rect.height / Mathf.Max(0.01f, body.height);
        var size = new Vector2(height * rectPx.x / rectPx.y, height);

        rt.sizeDelta = size;
        rt.anchoredPosition = -Vector2.Scale(body.center - new Vector2(0.5f, 0.5f), size) + focusOffset;
    }

    /// <summary>Visible (opaque) area of the sprite, in 0..1 of its rect.</summary>
    private static Rect VisibleBodyNormalized(Sprite sprite)
    {
        Vector2[] verts = sprite.vertices;   // local units, relative to the pivot
        if (verts == null || verts.Length == 0) return new Rect(0f, 0f, 1f, 1f);

        Vector2 min = verts[0], max = verts[0];
        for (int i = 1; i < verts.Length; i++)
        {
            min = Vector2.Min(min, verts[i]);
            max = Vector2.Max(max, verts[i]);
        }

        float ppu = sprite.pixelsPerUnit;
        Vector2 rectPx = sprite.rect.size;
        Vector2 nMin = (min * ppu + sprite.pivot) / rectPx;
        Vector2 nMax = (max * ppu + sprite.pivot) / rectPx;
        return Rect.MinMaxRect(nMin.x, nMin.y, nMax.x, nMax.y);
    }
}
