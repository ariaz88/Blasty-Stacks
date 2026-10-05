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

    [Header("Gem holder art (Gameplay_Gem-holder_H3P) - replaces bubble + tail sprites")]
    [Tooltip("Left cap of the holder (columns 0-13). When ALL holder sprites are set, the " +
             "bubble is drawn from the holder art and bubbleSprite / tailSprite are ignored.")]
    [SerializeField] private Sprite holderLeftCap;
    [Tooltip("1 px plain body column (column 14). Stretched sideways on BOTH sides of the " +
             "centre, so the tail never widens however wide the bubble gets.")]
    [SerializeField] private Sprite holderStrip;
    [Tooltip("Fixed-width centre piece WITH the tail (columns 15-57).")]
    [SerializeField] private Sprite holderCenter;
    [Tooltip("Right cap of the holder (columns 59-72).")]
    [SerializeField] private Sprite holderRightCap;
    [Tooltip("Canvas units per holder pixel. The art is 73 x 121 px.")]
    [SerializeField, Min(0.1f)] private float holderScale = 3f;
    [Tooltip("Height of the tail under the body, in holder px (rows 104-120).")]
    [SerializeField, Min(0f)] private float holderTailPx = 17f;
    [Tooltip("Text colour on the holder (its fill is dark navy).")]
    [SerializeField] private Color holderTextColor = Color.white;

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

    // Holder art: a root matching the bubble (flipped upside down when the bubble sits
    // BELOW its target, so the tail points up) and its five sliced pieces.
    private RectTransform _art;
    private RectTransform _leftCap, _stripL, _center, _stripR, _rightCap;

    private bool UsesHolder => holderLeftCap && holderStrip && holderCenter && holderRightCap;

    // Canvas units of each fixed piece and of the tail under the body.
    private float CapW(Sprite s) => s.rect.width * holderScale;
    private float HolderTailH => holderTailPx * holderScale;

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
        float tailH = UsesHolder ? HolderTailH : tailSize.y;

        float y = above
            ? hi.y + gap + tailH + halfH
            : lo.y - gap - tailH - halfH;

        // keep the whole bubble on screen, horizontally and vertically
        float minX = canvasLocal.xMin + edgeMargin + halfW;
        float maxX = canvasLocal.xMax - edgeMargin - halfW;
        float x = maxX >= minX ? Mathf.Clamp(targetCenterX, minX, maxX) : canvasLocal.center.x;

        float minY = canvasLocal.yMin + edgeMargin + halfH;
        float maxY = canvasLocal.yMax - edgeMargin - halfH;
        if (maxY >= minY) y = Mathf.Clamp(y, minY, maxY);

        bubble.anchoredPosition = new Vector2(x, y);

        if (UsesHolder)
        {
            LayoutHolder(targetCenterX - x, above);
            return;
        }

        if (!tail) return;

        // The tail stays over the TARGET, not over the bubble's centre - that is the
        // whole point of clamping the bubble instead of letting it run off-screen.
        float tailX = Mathf.Clamp(targetCenterX - x, -halfW + tailSize.x, halfW - tailSize.x);
        float tailY = above ? -halfH - tailSize.y * 0.5f : halfH + tailSize.y * 0.5f;

        tail.anchoredPosition = new Vector2(x + tailX, y + tailY);
        tail.localRotation = Quaternion.Euler(0f, 0f, above ? 180f : 0f);
        tail.sizeDelta = tailSize;
    }

    /// <summary>
    /// Lays the five holder pieces across the bubble's width:
    ///   [left cap][strip ......][centre WITH tail][...... strip][right cap]
    /// Caps and centre keep their pixel width (x holderScale); only the two 1-px strips
    /// stretch, so the tail is never widened. The centre slides by `tailOffset` so the
    /// tail stays over the target when the bubble was clamped at a screen edge.
    /// Every piece runs from the bubble's top to the tail's tip below its bottom, and
    /// is 9-sliced vertically so only the plain middle rows stretch. Below its target
    /// the whole art is flipped upside down so the tail points up; the label is not.
    /// </summary>
    private void LayoutHolder(float tailOffset, bool above)
    {
        if (!_art) return;

        float w = bubble.sizeDelta.x;
        float capL = CapW(holderLeftCap), capR = CapW(holderRightCap), cw = CapW(holderCenter);
        float tailH = HolderTailH;

        float maxLeft = Mathf.Max(0f, w * 0.5f - capL - cw * 0.5f);
        float maxRight = Mathf.Max(0f, w * 0.5f - capR - cw * 0.5f);
        float off = Mathf.Clamp(tailOffset, -maxLeft, maxRight);
        float c0 = w * 0.5f + off - cw * 0.5f;

        Span(_leftCap, 0f, capL, tailH);
        Span(_stripL, capL, c0, tailH);
        Span(_center, c0, c0 + cw, tailH);
        Span(_stripR, c0 + cw, w - capR, tailH);
        Span(_rightCap, w - capR, w, tailH);

        _art.localScale = new Vector3(1f, above ? 1f : -1f, 1f);
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: lays the bubble out for `text` at the canvas centre, for edit-mode captures.</summary>
    public void PreviewLayout(string text, float tailOffset, bool above)
    {
        EnsureParts();
        SetPartsActive(true);
        if (group) group.alpha = 1f;
        _current = text;
        if (label) label.text = text;
        ResizeBubble(text);
        bubble.anchoredPosition = Vector2.zero;
        if (UsesHolder) LayoutHolder(tailOffset, above);
    }
#endif

    private static void Span(RectTransform rt, float x0, float x1, float tailH)
    {
        if (!rt) return;
        rt.offsetMin = new Vector2(x0, -tailH);
        rt.offsetMax = new Vector2(Mathf.Max(x0, x1), 0f);
    }

    private void ResizeBubble(string text)
    {
        if (!bubble || !label) return;

        float innerMax = Mathf.Max(64f, maxWidth - padding.x * 2f);
        Vector2 preferred = label.GetPreferredValues(text, innerMax, 0f);

        float w = Mathf.Min(preferred.x, innerMax) + padding.x * 2f;
        float h = preferred.y + padding.y * 2f;

        if (UsesHolder)
        {
            // Never narrower than caps + centre (+1 px of strip each side), never
            // shorter than the top and bottom slice borders of the body.
            float minW = CapW(holderLeftCap) + CapW(holderRightCap) + CapW(holderCenter) + 2f * holderScale;
            float minH = (holderCenter.border.w + holderCenter.border.y - holderTailPx) * holderScale;
            w = Mathf.Max(w, minW);
            h = Mathf.Max(h, minH);
        }

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
            if (UsesHolder) img.enabled = false;   // the holder pieces draw the bubble
            else if (bubbleSprite)
            {
                img.sprite = bubbleSprite;
                img.type = Image.Type.Sliced;
            }
        }

        if (UsesHolder && !_art)
        {
            var go = new GameObject("HolderArt", typeof(RectTransform));
            go.layer = gameObject.layer;
            _art = (RectTransform)go.transform;
            _art.SetParent(bubble, false);
            _art.SetAsFirstSibling();             // under the label
            _art.anchorMin = Vector2.zero;
            _art.anchorMax = Vector2.one;
            _art.pivot = new Vector2(0.5f, 0.5f); // flips about the bubble's centre
            _art.offsetMin = _art.offsetMax = Vector2.zero;

            _leftCap = HolderPiece("LeftCap", holderLeftCap);
            _stripL = HolderPiece("StripL", holderStrip);
            _center = HolderPiece("CenterWithTail", holderCenter);
            _stripR = HolderPiece("StripR", holderStrip);
            _rightCap = HolderPiece("RightCap", holderRightCap);
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
            tmp.color = UsesHolder ? holderTextColor : textColor;
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

    private RectTransform HolderPiece(string name, Sprite sprite)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(_art, false);
        rt.anchorMin = new Vector2(0f, 0f);       // x from the bubble's left edge, y full height
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / holderScale;   // border px -> holderScale canvas units
        img.raycastTarget = false;
        return rt;
    }

    private static void Center(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private void SetPartsActive(bool on)
    {
        if (bubble) bubble.gameObject.SetActive(on);
        if (tail) tail.gameObject.SetActive(on && !UsesHolder);   // the holder has its own tail
    }
}
