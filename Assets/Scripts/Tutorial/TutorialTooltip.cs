using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Which side of the target the bubble sits on.</summary>
public enum TutorialTooltipSide
{
    Auto = 0,
    Above = 1,
    Below = 2,
}

/// <summary>
/// A speech bubble that FOLLOWS a target, as opposed to TutorialCaption, which is
/// one fixed line under the board.
///
/// Both live on the overlay and neither knows about the other: a board lesson goes
/// on using the caption, a guided chain uses this, and TutorialOverlay.ClearAll
/// hides both. Keeping them separate is why adding this needed no change to the
/// board tutorial.
///
/// Auto side picks BELOW when the target sits high on screen and ABOVE otherwise,
/// so the bubble never leaves the screen. The bubble is then clamped inward at the
/// screen edges - the Units nav button sits in the bottom-left corner - and the
/// tail slides along the bubble's edge to stay pointed at the target even after
/// that clamp has moved the bubble off-centre.
///
/// Fades are a manual unscaled loop, copying TutorialCaption: the first beat of
/// this chain plays on the Lose panel, where Time.timeScale is 0.
/// </summary>
[DisallowMultipleComponent]
public class TutorialTooltip : MonoBehaviour
{
    [Header("Parts (leave empty - they are built at runtime)")]
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TMP_Text label;
    [SerializeField] private RectTransform tail;
    [SerializeField] private CanvasGroup group;

    [Header("Look")]
    [Tooltip("Optional 9-sliced rounded sprite for the bubble. Empty = sharp rectangle.")]
    [SerializeField] private Sprite bubbleSprite;

    [Tooltip("Optional triangle sprite for the tail. Empty = a small square.")]
    [SerializeField] private Sprite tailSprite;

    [SerializeField] private Color bubbleColor = Color.white;
    [SerializeField] private Color textColor = new Color(0.10f, 0.10f, 0.13f, 1f);

    [Tooltip("Font for the bubble. Empty = TMP's default.")]
    [SerializeField] private TMP_FontAsset font;

    [SerializeField] private float fontSize = 44f;

    [Header("Layout (canvas units)")]
    [SerializeField] private float maxWidth = 760f;
    [SerializeField] private Vector2 padding = new Vector2(46f, 30f);

    [Tooltip("Distance between the target's edge and the tip of the tail.")]
    [SerializeField] private float gap = 28f;

    [Tooltip("Closest the bubble may come to the canvas edge.")]
    [SerializeField] private float edgeMargin = 40f;

    [SerializeField] private Vector2 tailSize = new Vector2(34f, 20f);
    [SerializeField] private float fadeTime = 0.22f;

    private RectTransform _self;
    private RectTransform _canvasRect;
    private Camera _uiCamera;

    // re-asked every LateUpdate, for the same reason the focus gate re-asks: the
    // card the bubble points at is destroyed and rebuilt under us
    private Func<Rect?> _targetProvider;
    private TutorialTooltipSide _side = TutorialTooltipSide.Auto;

    private string _current = "";
    private float _targetAlpha;

    private void Awake()
    {
        _self = (RectTransform)transform;
        EnsureParts();
        if (group) group.alpha = 0f;
        SetPartsActive(false);
    }

    /// <summary>Caches what screen->canvas conversion needs. Called by TutorialOverlay.</summary>
    public void Configure(Canvas canvas)
    {
        if (!_self) _self = (RectTransform)transform;

        if (!canvas)
        {
            _canvasRect = null;
            _uiCamera = null;
            return;
        }

        _canvasRect = canvas.transform as RectTransform;
        _uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // ------------------------------------------------------------------
    //  API
    // ------------------------------------------------------------------

    /// <summary>
    /// Shows `text` pinned to whatever `targetScreenRect` returns. A null result
    /// hides the bubble for that frame rather than leaving it stranded over an
    /// element that has gone away.
    /// </summary>
    public void Show(string text, Func<Rect?> targetScreenRect, TutorialTooltipSide side)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        EnsureParts();
        SetPartsActive(true);

        _targetProvider = targetScreenRect;
        _side = side;

        if (text != _current)
        {
            _current = text;
            if (label) label.text = text;
            ResizeBubble(text);
        }

        _targetAlpha = 1f;
        Reposition();
    }

    public void Hide()
    {
        _current = "";
        _targetProvider = null;
        _targetAlpha = 0f;
    }

    // ------------------------------------------------------------------
    //  Per-frame
    // ------------------------------------------------------------------

    private void LateUpdate()
    {
        if (group)
        {
            if (!Mathf.Approximately(group.alpha, _targetAlpha))
            {
                float step = fadeTime <= 0f ? 1f : Time.unscaledDeltaTime / fadeTime;
                group.alpha = Mathf.MoveTowards(group.alpha, _targetAlpha, step);
            }

            // Deliberately OUTSIDE the fade branch. It used to live inside it, so
            // the moment the fade was skipped for any reason the bubble was never
            // switched off - which is how "Go back" stayed on screen for the rest
            // of the session.
            if (_targetAlpha <= 0f && group.alpha <= 0f) SetPartsActive(false);
        }
        else
        {
            // No CanvasGroup at all: still honour Hide(), just without the fade.
            SetPartsActive(_targetAlpha > 0f);
        }

        if (_targetAlpha > 0f) Reposition();
    }

    private void Reposition()
    {
        if (!_canvasRect || !bubble || _targetProvider == null) return;

        Rect? screen = null;
        try { screen = _targetProvider(); }
        catch (Exception e) { Debug.LogException(e, this); }

        if (!screen.HasValue)
        {
            // target gone this frame - fade out rather than point at nothing
            _targetAlpha = 0f;
            return;
        }

        Rect s = screen.Value;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, s.min, _uiCamera, out var lo) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, s.max, _uiCamera, out var hi))
            return;

        Rect canvasLocal = _canvasRect.rect;
        float targetCenterX = (lo.x + hi.x) * 0.5f;

        // Auto: put the bubble on the roomier side. The nav bar is at the bottom of
        // the screen and the lose panel's buttons are low too, so in practice this
        // resolves to Above most of the time - but the hero card sits high.
        bool above = _side == TutorialTooltipSide.Above;
        if (_side == TutorialTooltipSide.Auto)
        {
            float centerY = (lo.y + hi.y) * 0.5f;
            above = centerY < canvasLocal.center.y;
        }

        Vector2 size = bubble.sizeDelta;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        float y = above
            ? hi.y + gap + tailSize.y + halfH
            : lo.y - gap - tailSize.y - halfH;

        // keep the whole bubble on screen, horizontally and vertically
        float minX = canvasLocal.xMin + edgeMargin + halfW;
        float maxX = canvasLocal.xMax - edgeMargin - halfW;
        float x = maxX >= minX ? Mathf.Clamp(targetCenterX, minX, maxX) : canvasLocal.center.x;

        float minY = canvasLocal.yMin + edgeMargin + halfH;
        float maxY = canvasLocal.yMax - edgeMargin - halfH;
        if (maxY >= minY) y = Mathf.Clamp(y, minY, maxY);

        bubble.anchoredPosition = new Vector2(x, y);

        if (!tail) return;

        // The tail stays over the TARGET, not over the bubble's centre - that is the
        // whole point of clamping the bubble instead of letting it run off-screen.
        float tailX = Mathf.Clamp(targetCenterX - x, -halfW + tailSize.x, halfW - tailSize.x);
        float tailY = above ? -halfH - tailSize.y * 0.5f : halfH + tailSize.y * 0.5f;

        tail.anchoredPosition = new Vector2(x + tailX, y + tailY);
        tail.localRotation = Quaternion.Euler(0f, 0f, above ? 180f : 0f);
        tail.sizeDelta = tailSize;
    }

    private void ResizeBubble(string text)
    {
        if (!bubble || !label) return;

        float innerMax = Mathf.Max(64f, maxWidth - padding.x * 2f);
        Vector2 preferred = label.GetPreferredValues(text, innerMax, 0f);

        float w = Mathf.Min(preferred.x, innerMax) + padding.x * 2f;
        float h = preferred.y + padding.y * 2f;

        bubble.sizeDelta = new Vector2(w, h);
        ((RectTransform)label.transform).sizeDelta = new Vector2(w - padding.x * 2f, h - padding.y * 2f);
    }

    // ------------------------------------------------------------------
    //  Construction
    // ------------------------------------------------------------------

    private void EnsureParts()
    {
        if (!_self) _self = (RectTransform)transform;

        // NEVER use ?? here. It bypasses UnityEngine.Object's overloaded ==, so a
        // "fake null" component reference is accepted as real and AddComponent is
        // never called - which left this object with NO CanvasGroup, made every
        // "if (group)" false, and so the fade in LateUpdate never ran and the
        // bubble stayed on screen forever after Hide().
        if (!group)
        {
            group = gameObject.GetComponent<CanvasGroup>();
            if (!group) group = gameObject.AddComponent<CanvasGroup>();
        }

        if (!bubble)
        {
            var go = new GameObject("Bubble", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            bubble = (RectTransform)go.transform;
            bubble.SetParent(transform, false);
            Center(bubble);

            var img = go.GetComponent<Image>();
            img.color = bubbleColor;
            img.raycastTarget = false;
            if (bubbleSprite)
            {
                img.sprite = bubbleSprite;
                img.type = Image.Type.Sliced;
            }
        }

        if (!label)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.layer = gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(bubble, false);
            Center(rt);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font) tmp.font = font;
            tmp.fontSize = fontSize;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            label = tmp;
        }

        if (!tail)
        {
            var go = new GameObject("Tail", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            tail = (RectTransform)go.transform;
            tail.SetParent(transform, false);
            Center(tail);
            tail.sizeDelta = tailSize;

            var img = go.GetComponent<Image>();
            img.color = bubbleColor;
            img.raycastTarget = false;
            if (tailSprite) img.sprite = tailSprite;
        }
    }

    private static void Center(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private void SetPartsActive(bool on)
    {
        if (bubble) bubble.gameObject.SetActive(on);
        if (tail) tail.gameObject.SetActive(on);
    }
}
