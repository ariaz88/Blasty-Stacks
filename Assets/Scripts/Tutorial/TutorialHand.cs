using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Where one demonstration cycle starts and ends, in SCREEN space.
/// `valid` false means "nothing to show right now" - the hand hides for that
/// cycle and asks again, which is what happens between a blast and the next hint.
/// </summary>
public struct TutorialDragPoints
{
    public bool valid;
    public Vector2 from;
    public Vector2 to;

    public static TutorialDragPoints At(Vector2 from, Vector2 to)
        => new TutorialDragPoints { valid = true, from = from, to = to };

    public static readonly TutorialDragPoints None = default;
}

/// <summary>Timings of one hand demonstration cycle. All seconds, all unscaled.</summary>
[Serializable]
public struct TutorialHandLoopTimings
{
    [Tooltip("Fade-in when the hand (re)appears at the start point.")]
    public float appearTime;

    [Tooltip("Finger press: hand shrinks slightly, touch ripple blooms.")]
    public float pressTime;

    [Tooltip("The drag itself, start point -> end point.")]
    public float travelTime;

    [Tooltip("Release: ripple and hand fade out at the end point.")]
    public float releaseTime;

    [Tooltip("Dead time before the cycle restarts.")]
    public float pauseTime;

    public static TutorialHandLoopTimings Default => new TutorialHandLoopTimings
    {
        appearTime = 0.15f,
        pressTime = 0.12f,
        travelTime = 0.85f,
        releaseTime = 0.20f,
        pauseTime = 0.45f,
    };

    /// <summary>A zeroed struct (what Unity gives a fresh field) is replaced by the default.</summary>
    public TutorialHandLoopTimings OrDefault()
    {
        bool empty = appearTime <= 0f && pressTime <= 0f && travelTime <= 0f
                  && releaseTime <= 0f && pauseTime <= 0f;
        return empty ? Default : this;
    }
}

/// <summary>
/// The pointing-hand view. Lives on the TutorialOverlay prefab and is driven by
/// TutorialRunner: the runner resolves targets to SCREEN positions and hands
/// them here; this component owns the whole look of the gesture.
///
/// Two sprites, both from Assets/Arts/UI-Tutorial/:
///   Hand_Tutorial.png                 - the hand itself
///   Hand_Tutorial_FingerAnimation.png - the soft ellipse used as touch ripple
///
/// Neither is a frame sequence (both are single sprites), so the motion is done
/// on the transform.
///
/// Hierarchy this component expects (built by the overlay prefab):
///   Hand            <- this RectTransform IS the touch point
///     Ripple        <- centred on the touch point
///     HandSprite    <- pivot set to the FINGERTIP, so scaling presses at the tip
/// </summary>
[DisallowMultipleComponent]
public class TutorialHand : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private RectTransform handSprite;
    [SerializeField] private CanvasGroup handGroup;
    [SerializeField] private RectTransform ripple;
    [SerializeField] private CanvasGroup rippleGroup;

    [Header("Placement")]
    [Tooltip("Final nudge of the touch point, in canvas units. Use this if the " +
             "fingertip does not sit exactly on the target.")]
    [SerializeField] private Vector2 fingerTipNudge = Vector2.zero;

    [Header("Press look")]
    [Tooltip("Hand scale while pressed.")]
    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.92f;

    [SerializeField] private float rippleMinScale = 0.55f;
    [SerializeField] private float rippleMaxScale = 1.15f;
    [SerializeField, Range(0f, 1f)] private float rippleAlpha = 0.55f;

    [Tooltip("Gentle in/out pulse of the ripple while dragging, in cycles per second.")]
    [SerializeField] private float ripplePulseSpeed = 1.6f;

    [Tooltip("How much the ripple pulses while dragging, as a fraction of its scale.")]
    [SerializeField, Range(0f, 0.5f)] private float ripplePulseAmount = 0.12f;

    [Header("Player input")]
    [Tooltip("Hide the hand while the player holds a touch / mouse button. " +
             "While a finger is down the piece is mid-drag, so any position the " +
             "hand resolved is already stale - showing it would point at where " +
             "the stack USED to be.")]
    [SerializeField] private bool hideWhilePointerDown = true;

    private RectTransform _self;
    private RectTransform _canvasRect;
    private Camera _uiCamera;
    private Coroutine _loop;

    // true while the player is holding a touch - set in Update so every phase of
    // the cycle can bail out the moment it happens
    private bool _suppressed;

    public bool IsLooping => _loop != null;

    private void Awake()
    {
        _self = (RectTransform)transform;
        SetHandAlpha(0f);
        SetRippleAlpha(0f);
    }

    private void Update()
    {
        if (!hideWhilePointerDown) { _suppressed = false; return; }

        bool down = PointerIsDown();

        // hide on the frame the finger goes down, not a frame later
        if (down && !_suppressed)
        {
            SetHandAlpha(0f);
            SetRippleAlpha(0f);
        }

        _suppressed = down;
    }

    /// <summary>
    /// Legacy Input, to match BoardInputController and the rest of the game
    /// (the project ships with activeInputHandler = Both).
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

    /// <summary>
    /// Caches what screen->canvas conversion needs. Called by TutorialOverlay so
    /// the hand works on an Overlay canvas and a Camera-space canvas alike.
    /// </summary>
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

    /// <summary>Moves the touch point (this RectTransform) onto a screen position.</summary>
    public void SetScreenPosition(Vector2 screenPos)
    {
        if (!_self) _self = (RectTransform)transform;
        if (!_canvasRect) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, _uiCamera, out var local))
            _self.anchoredPosition = local + fingerTipNudge;
    }

    /// <summary>
    /// Loops "press at `from`, drag to `to`, release, pause" until StopAndHide.
    ///
    /// The provider is asked again at the START OF EVERY CYCLE rather than once,
    /// which is the whole point: if the player moves the hinted stack somewhere
    /// else, the next demonstration starts from where the stack now IS, and once
    /// a pair blasts the provider can hand back the next pair instead.
    /// </summary>
    public void StartDragLoop(Func<TutorialDragPoints> provider, TutorialHandLoopTimings timings)
    {
        if (provider == null) { StopAndHide(); return; }

        StopLoop();
        gameObject.SetActive(true);
        _loop = StartCoroutine(DragLoop(provider, timings.OrDefault()));
    }

    /// <summary>Fixed-endpoint convenience for a gesture that never moves.</summary>
    public void StartDragLoop(Vector2 fromScreen, Vector2 toScreen, TutorialHandLoopTimings timings)
        => StartDragLoop(() => TutorialDragPoints.At(fromScreen, toScreen), timings);

    /// <summary>Loops a tap-in-place on one screen position.</summary>
    public void StartTapLoop(Vector2 atScreen, TutorialHandLoopTimings timings)
        => StartDragLoop(() => TutorialDragPoints.At(atScreen, atScreen), timings);

    /// <summary>Loops a tap-in-place on a position that may move between cycles.</summary>
    public void StartTapLoop(Func<TutorialDragPoints> provider, TutorialHandLoopTimings timings)
        => StartDragLoop(provider, timings);

    public void StopAndHide()
    {
        StopLoop();
        SetHandAlpha(0f);
        SetRippleAlpha(0f);
    }

    private void StopLoop()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
    }

    // ------------------------------------------------------------------
    //  The cycle
    // ------------------------------------------------------------------

    private IEnumerator DragLoop(Func<TutorialDragPoints> provider, TutorialHandLoopTimings t)
    {
        while (true)
        {
            // Never demonstrate while the player has a finger down. The stack is
            // mid-drag, so its anchor has not been committed yet and anything we
            // resolve now points at where it USED to be. Wait for the release,
            // then ask again - by then the board knows the new position.
            while (_suppressed)
            {
                SetHandAlpha(0f);
                SetRippleAlpha(0f);
                yield return null;
            }

            // ask where the gesture goes THIS time round
            var points = provider();

            if (!points.valid)
            {
                // nothing to point at yet (mid-blast, or the board is done):
                // stay hidden and check again shortly
                SetHandAlpha(0f);
                SetRippleAlpha(0f);
                yield return WaitUnscaled(Mathf.Max(0.1f, t.pauseTime));
                continue;
            }

            Vector2 from = points.from;
            Vector2 to = points.to;

            // appear at the start point
            SetScreenPosition(from);
            SetHandScale(1f);
            SetRipple(rippleMinScale, 0f);
            yield return FadeHand(0f, 1f, t.appearTime);
            if (_suppressed) continue;

            // press
            yield return Press(t.pressTime);
            if (_suppressed) continue;

            // drag
            yield return Travel(from, to, t.travelTime);
            if (_suppressed) continue;

            // release
            yield return Release(t.releaseTime);

            // pause before the next demonstration
            yield return WaitUnscaled(t.pauseTime);
        }
    }

    private IEnumerator Press(float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.unscaledDeltaTime;
            float k = duration <= 0f ? 1f : Mathf.Clamp01(e / duration);

            SetHandScale(Mathf.Lerp(1f, pressedScale, k));
            SetRipple(Mathf.Lerp(rippleMinScale, rippleMaxScale, k), Mathf.Lerp(0f, rippleAlpha, k));
            yield return null;

            if (_suppressed) yield break;   // player took over
        }

        SetHandScale(pressedScale);
        SetRipple(rippleMaxScale, rippleAlpha);
    }

    private IEnumerator Travel(Vector2 from, Vector2 to, float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.unscaledDeltaTime;
            float k = duration <= 0f ? 1f : Mathf.Clamp01(e / duration);

            // smoothstep: eases out of the press and into the drop
            float s = k * k * (3f - 2f * k);
            SetScreenPosition(Vector2.LerpUnclamped(from, to, s));

            // subtle breathing on the touch ripple so a long drag is not static
            float pulse = 1f + Mathf.Sin(e * ripplePulseSpeed * Mathf.PI * 2f) * ripplePulseAmount;
            SetRipple(rippleMaxScale * pulse, rippleAlpha);

            yield return null;

            if (_suppressed) yield break;   // player took over mid-drag
        }

        SetScreenPosition(to);
    }

    private IEnumerator Release(float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.unscaledDeltaTime;
            float k = duration <= 0f ? 1f : Mathf.Clamp01(e / duration);

            SetHandScale(Mathf.Lerp(pressedScale, 1f, k));
            SetHandAlpha(1f - k);
            SetRipple(Mathf.Lerp(rippleMaxScale, rippleMaxScale * 1.25f, k), Mathf.Lerp(rippleAlpha, 0f, k));
            yield return null;
        }

        SetHandAlpha(0f);
        SetRippleAlpha(0f);
        SetHandScale(1f);
    }

    private IEnumerator FadeHand(float a, float b, float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.unscaledDeltaTime;
            SetHandAlpha(Mathf.Lerp(a, b, duration <= 0f ? 1f : Mathf.Clamp01(e / duration)));
            yield return null;

            if (_suppressed) yield break;   // player took over
        }
        SetHandAlpha(b);
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        float e = 0f;
        while (e < seconds)
        {
            e += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    // ------------------------------------------------------------------
    //  Setters (all null-safe so a half-wired prefab warns instead of throwing)
    // ------------------------------------------------------------------

    private void SetHandAlpha(float a)
    {
        if (handGroup) handGroup.alpha = a;
    }

    private void SetHandScale(float s)
    {
        if (handSprite) handSprite.localScale = new Vector3(s, s, 1f);
    }

    private void SetRippleAlpha(float a)
    {
        if (rippleGroup) rippleGroup.alpha = a;
    }

    private void SetRipple(float scale, float alpha)
    {
        if (ripple) ripple.localScale = new Vector3(scale, scale, 1f);
        SetRippleAlpha(alpha);
    }
}
