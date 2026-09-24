using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes exactly ONE thing on screen touchable, and everything else dead.
///
/// Four full-bleed raycast targets (top / bottom / left / right) are laid out so
/// that together they cover the whole canvas EXCEPT a rectangle around the focused
/// element. Nothing under the four plates can be clicked; the hole is a plain gap,
/// so the real button underneath receives the tap through its own wiring, with
/// nothing about it changed.
///
/// WHY A HOLE, and not "lift the target above a blocker": lifting means adding a
/// Canvas + GraphicRaycaster to somebody else's GameObject and taking them off
/// again, which then has to survive every abort path, and it leaves the whole
/// screen live for the moment between one beat being released and the next one
/// promoting its own target. This component owns nothing outside itself, so an
/// abort - or a scene load - cannot strand the player behind a locked screen.
///
/// The plates are built from code when they are not wired in the Inspector, so
/// the overlay prefab needs no surgery to gain this.
/// </summary>
[DisallowMultipleComponent]
public class TutorialFocusGate : MonoBehaviour, IPointerClickHandler
{
    [Header("Plates (leave empty - they are built at runtime)")]
    [SerializeField] private Image top;
    [SerializeField] private Image bottom;
    [SerializeField] private Image left;
    [SerializeField] private Image right;

    [Header("Look")]
    [Tooltip("Fade time of the scrim, unscaled seconds. Matches TutorialCaption.")]
    [SerializeField] private float fadeTime = 0.2f;

    [Header("Escape hatch")]
    [Tooltip("Taps on the BLOCKED area before the tutorial gives up and lets go. " +
             "DEFAULT 0 = OFF, and it should stay off: the whole point of the gate " +
             "is that a tap outside the highlight does NOTHING. Leaving this at 6 " +
             "meant a player checking that the lock worked unlocked the game. The " +
             "real safety net is time-based (TutorialRunner.hardStopSeconds).")]
    [SerializeField] private int blockedTapsToAbort = 0;

    /// <summary>Raised when the player has tapped the dead area too many times.</summary>
    public event Action OnAbortRequested;

    // How many gates are currently holding the screen shut. Static because
    // TutorialHand needs to know "input is gated right now" without being handed a
    // reference to a gate it otherwise has nothing to do with.
    private static int _blockingCount;

    /// <summary>True while ANY focus gate is holding the screen.</summary>
    public static bool AnyBlocking => _blockingCount > 0;

    private RectTransform _canvasRect;
    private Camera _uiCamera;

    // Re-asked every LateUpdate rather than resolved once: the first deployed hero
    // card is Instantiated fresh on every panel open, and the Upgrade button is two
    // different GameObjects that swap by affordability. Nothing is remembered, so
    // both stay correct without the gate knowing they happened.
    private Func<Rect?> _holeProvider;

    private Vector2 _holePadding;
    private bool _active;
    private int _blockedTaps;

    // The hole is FROZEN for the duration of a press. See LayoutPlates.
    private Rect _frozenHole;
    private bool _hasFrozenHole;

    private float _targetAlpha;
    private float _currentAlpha;

    // Visual-only fade after Release(): input is handed back on the SAME frame
    // (plates stop being raycast targets), and only the darkness lingers for
    // fadeTime. With a 0.42 dim, snapping it off in one frame read as a flicker.
    private bool _fadingOut;

    // What is focused right now, in SCREEN space. Written by LayoutPlates, read by
    // TutorialFocusFx for the glow and for the tap-burst hit test.
    private bool _hasFocus;
    private Rect _focusTargetScreen;   // the target itself
    private Rect _focusHoleScreen;     // the target + holePadding = what is clickable

    /// <summary>True while the screen is shut. Read by the steps for logging.</summary>
    public bool IsBlocking => _active;

    /// <summary>
    /// The focused target and the clickable hole around it, in screen space. False
    /// when the gate is open, fading out, or shut with no hole (between beats).
    /// </summary>
    public bool TryGetFocus(out Rect targetScreenRect, out Rect holeScreenRect)
    {
        targetScreenRect = _focusTargetScreen;
        holeScreenRect = _focusHoleScreen;
        return _active && _hasFocus;
    }

    /// <summary>The canvas the plates live on, and the camera to convert through.</summary>
    public RectTransform CanvasRect => _canvasRect;
    public Camera UiCamera => _uiCamera;

    // ------------------------------------------------------------------
    //  Lifecycle
    // ------------------------------------------------------------------

    private void Awake()
    {
        EnsurePlates();
        ApplyAlpha(0f);
        SetPlatesActive(false);
    }

    private void OnDisable()
    {
        // The single most important line in the feature: whatever went wrong, the
        // player gets their screen back. Instant, because a disabled component
        // cannot run the fade-out.
        Release(true);
    }

    /// <summary>Caches what screen->canvas conversion needs. Called by TutorialOverlay.</summary>
    public void Configure(Canvas canvas)
    {
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
    /// Shuts the whole screen - no hole. Used while a step is waiting for its
    /// target to appear, so there is never a frame in which the player can press
    /// something the tutorial has not sanctioned.
    /// </summary>
    public void BlockAll(float dimAlpha)
    {
        _holeProvider = null;
        _holePadding = Vector2.zero;
        Begin(dimAlpha);
    }

    /// <summary>
    /// Shuts the screen except for a rectangle around whatever `screenRect`
    /// returns. A null result means "the target is gone right now", which shuts
    /// the screen completely rather than opening it - failing closed is correct
    /// here, because the alternative hands the player a live menu mid-lesson.
    /// </summary>
    public void FocusOn(Func<Rect?> screenRect, Vector2 padding, float dimAlpha)
    {
        _holeProvider = screenRect;
        _holePadding = padding;
        _hasFrozenHole = false;   // a new target must be measured fresh
        Begin(dimAlpha);
    }

    /// <summary>
    /// Legacy Input, to match TutorialHand and BoardInputController (the project
    /// ships activeInputHandler = Both). Only used to know "is a press in progress",
    /// never to route a click - clicks stay on the EventSystem.
    /// </summary>
    private static bool PointerIsDown()
    {
        if (Input.GetMouseButton(0)) return true;

        if (Input.touchCount > 0)
        {
            var phase = Input.GetTouch(0).phase;
            return phase != TouchPhase.Ended && phase != TouchPhase.Canceled;
        }

        return false;
    }

    /// <summary>Gives the screen back, fading the dim out. Idempotent.</summary>
    public void Release() => Release(false);

    /// <summary>
    /// Gives the screen back. Input is returned IMMEDIATELY either way - the plates
    /// stop being raycast targets on this frame. `instant` only decides whether the
    /// darkness snaps off or fades over fadeTime.
    /// </summary>
    public void Release(bool instant)
    {
        bool wasActive = _active;
        if (_active) _blockingCount = Mathf.Max(0, _blockingCount - 1);

        _active = false;
        _hasFocus = false;
        _holeProvider = null;
        _hasFrozenHole = false;
        _blockedTaps = 0;
        _targetAlpha = 0f;

        // Already fading out from an earlier Release - let it finish.
        if (!wasActive && _fadingOut && !instant) return;

        bool canFade = !instant && wasActive && isActiveAndEnabled && _currentAlpha > 1.5f / 255f;
        if (!canFade)
        {
            FinishRelease();
            return;
        }

        _fadingOut = true;
        SetPlatesRaycast(false);   // the player has the screen back from this frame
    }

    private void FinishRelease()
    {
        _fadingOut = false;
        _currentAlpha = 0f;
        ApplyAlpha(0f);
        SetPlatesActive(false);
        SetPlatesRaycast(true);    // ready to block next time
    }

    private void Begin(float dimAlpha)
    {
        EnsurePlates();

        // A fully transparent Image has its mesh culled, and a culled mesh is
        // invisible to the GraphicRaycaster - which is exactly why the overlay
        // prefab's original Blocker never blocked anything. Belt and braces:
        // cullTransparentMesh is off on every plate (see EnsurePlates) AND the
        // alpha never actually reaches zero while the gate is up.
        _targetAlpha = Mathf.Max(dimAlpha, 1f / 255f);

        if (!_active)
        {
            _active = true;
            _blockedTaps = 0;
            _blockingCount++;

            // Re-shut during a fade-out: carry on from the current darkness rather
            // than dropping to 0 and fading in again, which would visibly blink.
            if (_fadingOut) _fadingOut = false;
            else _currentAlpha = 0f;

            SetPlatesRaycast(true);
            SetPlatesActive(true);
            LayoutPlates();
        }

        // Apply immediately rather than waiting for the fade: the plates are built
        // at alpha 0, and an un-applied alpha left them fully transparent.
        ApplyAlpha(Mathf.Max(_currentAlpha, 1f / 255f));
    }

    // ------------------------------------------------------------------
    //  Per-frame
    // ------------------------------------------------------------------

    private void LateUpdate()
    {
        if (_fadingOut)
        {
            // Layout is deliberately NOT refreshed here: the provider is gone, and
            // re-laying would collapse to "cover everything", briefly darkening the
            // target that the player has just pressed.
            float outStep = fadeTime <= 0f ? 1f : Time.unscaledDeltaTime / fadeTime;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, 0f, outStep);
            ApplyAlpha(_currentAlpha);
            if (_currentAlpha <= 0f) FinishRelease();
            return;
        }

        if (!_active) return;

        if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            float step = fadeTime <= 0f ? 1f : Time.unscaledDeltaTime / fadeTime;
            _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, step);
            ApplyAlpha(_currentAlpha);
        }

        LayoutPlates();
    }

    private void LayoutPlates()
    {
        if (!_canvasRect) return;

        Rect canvasLocal = _canvasRect.rect;

        bool hasHole = false;
        Vector2 lo = Vector2.zero;
        Vector2 hi = Vector2.zero;

        if (_holeProvider != null)
        {
            Rect? screen = null;

            // THE HOLE IS FROZEN WHILE A FINGER IS DOWN.
            //
            // Buttons in this project carry UIButtonPressScaler, which tweens the
            // button's own transform down to 0.9 on press. Because the hole is
            // measured from that same transform, the scrim used to close inward by
            // 10% the instant the player pressed - and unless they had hit the dead
            // centre, the scrim slid under their cursor, the button got
            // OnPointerExit, and the CLICK WAS NEVER DELIVERED. On screen that
            // looked like the press animation being broken.
            //
            // Nothing legitimately moves during a press, so holding the last rect
            // is both safe and the fix.
            if (_hasFrozenHole && PointerIsDown())
            {
                screen = _frozenHole;
            }
            else
            {
                try { screen = _holeProvider(); }
                catch (Exception e) { Debug.LogException(e, this); }

                if (screen.HasValue)
                {
                    _frozenHole = screen.Value;
                    _hasFrozenHole = true;
                }
            }

            if (screen.HasValue)
            {
                Rect s = screen.Value;
                Vector2 min = new Vector2(s.xMin - _holePadding.x, s.yMin - _holePadding.y);
                Vector2 max = new Vector2(s.xMax + _holePadding.x, s.yMax + _holePadding.y);

                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, min, _uiCamera, out lo) &&
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, max, _uiCamera, out hi))
                {
                    // clamp into the canvas so a target half off-screen cannot push
                    // a plate to a negative size
                    lo.x = Mathf.Clamp(lo.x, canvasLocal.xMin, canvasLocal.xMax);
                    lo.y = Mathf.Clamp(lo.y, canvasLocal.yMin, canvasLocal.yMax);
                    hi.x = Mathf.Clamp(hi.x, canvasLocal.xMin, canvasLocal.xMax);
                    hi.y = Mathf.Clamp(hi.y, canvasLocal.yMin, canvasLocal.yMax);
                    hasHole = hi.x > lo.x && hi.y > lo.y;

                    if (hasHole)
                    {
                        _focusTargetScreen = s;
                        _focusHoleScreen = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                    }
                }
            }
        }

        _hasFocus = hasHole;

        if (!hasHole)
        {
            // No hole: one plate covers everything, the rest collapse.
            SetBand(top, canvasLocal.xMin, canvasLocal.xMax, canvasLocal.yMin, canvasLocal.yMax);
            SetBand(bottom, 0f, 0f, 0f, 0f);
            SetBand(left, 0f, 0f, 0f, 0f);
            SetBand(right, 0f, 0f, 0f, 0f);
            return;
        }

        SetBand(top, canvasLocal.xMin, canvasLocal.xMax, hi.y, canvasLocal.yMax);
        SetBand(bottom, canvasLocal.xMin, canvasLocal.xMax, canvasLocal.yMin, lo.y);
        SetBand(left, canvasLocal.xMin, lo.x, lo.y, hi.y);
        SetBand(right, hi.x, canvasLocal.xMax, lo.y, hi.y);
    }

    private static void SetBand(Image plate, float xMin, float xMax, float yMin, float yMax)
    {
        if (!plate) return;

        float w = Mathf.Max(0f, xMax - xMin);
        float h = Mathf.Max(0f, yMax - yMin);

        var rt = (RectTransform)plate.transform;
        rt.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
        rt.sizeDelta = new Vector2(w, h);
    }

    // ------------------------------------------------------------------
    //  Escape hatch
    // ------------------------------------------------------------------

    // pointerClick bubbles up the hierarchy, so one handler on the gate covers all
    // four plates without a component on each.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_active || blockedTapsToAbort <= 0) return;

        _blockedTaps++;
        if (_blockedTaps < blockedTapsToAbort) return;

        Debug.LogWarning($"[Tutorial] Player tapped the blocked area {_blockedTaps} times - " +
                         "releasing the screen and aborting the tutorial.");
        _blockedTaps = 0;

        try { OnAbortRequested?.Invoke(); }
        catch (Exception e) { Debug.LogException(e, this); }
    }

    // ------------------------------------------------------------------
    //  Plate construction
    // ------------------------------------------------------------------

    private void EnsurePlates()
    {
        if (top && bottom && left && right) return;

        if (!top) top = BuildPlate("ScrimTop");
        if (!bottom) bottom = BuildPlate("ScrimBottom");
        if (!left) left = BuildPlate("ScrimLeft");
        if (!right) right = BuildPlate("ScrimRight");
    }

    private Image BuildPlate(string plateName)
    {
        var go = new GameObject(plateName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;

        var rt = (RectTransform)go.transform;
        rt.SetParent(transform, false);

        // centre-anchored so the band maths above is a plain position + size
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0f);
        img.raycastTarget = true;

        // see the comment in Begin() - this is the difference between a scrim that
        // blocks and one that silently does nothing
        img.canvasRenderer.cullTransparentMesh = false;

        return img;
    }

    private void SetPlatesRaycast(bool on)
    {
        if (top) top.raycastTarget = on;
        if (bottom) bottom.raycastTarget = on;
        if (left) left.raycastTarget = on;
        if (right) right.raycastTarget = on;
    }

    private void SetPlatesActive(bool on)
    {
        if (top) top.gameObject.SetActive(on);
        if (bottom) bottom.gameObject.SetActive(on);
        if (left) left.gameObject.SetActive(on);
        if (right) right.gameObject.SetActive(on);
    }

    private void ApplyAlpha(float a)
    {
        SetPlateAlpha(top, a);
        SetPlateAlpha(bottom, a);
        SetPlateAlpha(left, a);
        SetPlateAlpha(right, a);
    }

    private static void SetPlateAlpha(Image plate, float a)
    {
        if (!plate) return;
        var c = plate.color;
        c.a = a;
        plate.color = c;
    }
}
