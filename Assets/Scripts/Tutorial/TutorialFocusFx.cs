using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "juice" of a guided step, matching the onboarding reference video:
///
///   TARGET GLOW - a soft golden halo hugging the highlighted element, breathing
///                 gently for as long as the step points at it. In the reference
///                 the target reads about 19% brighter than when the step ends;
///                 the halo bleeds a few units INTO the target to give that warmth
///                 without laying a rectangle over an irregular button.
///
///   TAP BURST   - where the player's finger actually lands inside the highlight:
///                 a white soft disc that gives way to an expanding yellow ring,
///                 ~0.2s in total (reference fr.46-48 at 15 fps).
///
/// WHY UI IMAGES AND NOT A PARTICLESYSTEM: the house method (VFX-Playbook) builds
/// effects as ParticleSystems in world space. That cannot work here - the tutorial
/// overlay is a Screen-Space-OVERLAY canvas, which draws after every camera, and a
/// particle renderer is drawn BY a camera. A ParticleSystem on this canvas would be
/// invisible. So the same idea is applied with UI Graphics: built entirely in
/// code, no prefab, no asset, no shader (UI/Default only, so nothing to add to
/// Always Included Shaders).
///
/// Everything runs on unscaled time: the first beat of the onboarding plays on the
/// Lose panel, where GameplayPause has set Time.timeScale to 0.
///
/// It reads WHAT to decorate from TutorialFocusGate.TryGetFocus, so the glow and
/// the burst hit-test always agree with the hole the player can actually press.
/// </summary>
[DisallowMultipleComponent]
public class TutorialFocusFx : MonoBehaviour
{
    [Header("Target glow")]
    [SerializeField] private bool glowEnabled = true;
    [SerializeField] private Color glowColor = new Color(1f, 0.82f, 0.25f, 1f);

    [Tooltip("Canvas units between the target's edge and the brightest line of the halo.")]
    [SerializeField] private float glowGap = 2f;

    [Tooltip("Fade-in when a step starts pointing at something, unscaled seconds.")]
    [SerializeField] private float glowFadeIn = 0.18f;

    [SerializeField] private float glowFadeOut = 0.15f;

    [Tooltip("Breathing rate of the halo, cycles per second.")]
    [SerializeField] private float pulseHz = 1.2f;

    [Tooltip("Dimmest point of the breathing, as a fraction of full glow.")]
    [SerializeField, Range(0f, 1f)] private float pulseMin = 0.65f;

    [Tooltip("How far the halo swells outward at the peak of each breath, canvas units.")]
    [SerializeField] private float pulseGrow = 4f;

    [Header("Tap burst (reference fr.46-48)")]
    [SerializeField] private bool burstEnabled = true;
    [SerializeField] private Color discColor = Color.white;
    [SerializeField] private Color ringColor = new Color(1f, 0.86f, 0.3f, 1f);

    [Tooltip("fr.46: the white disc, full diameter in canvas units.")]
    [SerializeField] private float discDiameter = 85f;

    [SerializeField, Range(0.1f, 1f)] private float discStartScale = 0.6f;

    [Tooltip("0.00 -> this: the disc swells and fades.")]
    [SerializeField] private float discDuration = 0.08f;

    [Tooltip("The ring starts this long after the touch.")]
    [SerializeField] private float ringDelay = 0.03f;

    [Tooltip("fr.47: ring expands and fades over this long.")]
    [SerializeField] private float ringDuration = 0.19f;

    [SerializeField] private float ringStartDiameter = 70f;
    [SerializeField] private float ringEndDiameter = 160f;
    [SerializeField, Range(0f, 1f)] private float ringStartAlpha = 0.95f;

    [Tooltip("Bursts that can be alive at once. Two covers a fast double-tap.")]
    [SerializeField] private int burstPool = 2;

    // ------------------------------------------------------------------

    private TutorialFocusGate _gate;
    private RectTransform _self;

    private RectTransform _glowRt;
    private Image _glow;
    private float _glowVis;          // 0..1 envelope, separate from the pulse
    private Rect _lastTargetLocal;   // held while the glow fades out
    private bool _hasLastTarget;
    private float _pulseClock;

    private Burst[] _bursts;

    private class Burst
    {
        public RectTransform root;
        public RectTransform disc;
        public Image discImg;
        public RectTransform ring;
        public Image ringImg;
        public float t;
        public bool alive;
    }

    // Procedural art, built once and shared. HideFlags.DontSave so it never lands
    // in a scene; the `!x` checks rebuild it if the Editor has destroyed it.
    private static Texture2D s_glowTex, s_discTex, s_ringTex;
    private static Sprite s_glowSprite, s_discSprite, s_ringSprite;

    // The glow texture's geometry, in texture pixels == canvas units (see NOTES in
    // the doc: the sprite is created at 100 px/unit, matching the canvas default).
    private const int GlowOut = 34;              // halo reaches this far outside the target
    private const int GlowIn = 6;                // and bleeds this far into it
    private const int GlowBorder = GlowOut + GlowIn;
    private const int GlowSize = GlowBorder * 2 + 1;

    // ------------------------------------------------------------------
    //  Wiring
    // ------------------------------------------------------------------

    /// <summary>Called by TutorialOverlay once the gate exists.</summary>
    public void Bind(TutorialFocusGate gate)
    {
        _gate = gate;
    }

    private void Awake()
    {
        _self = (RectTransform)transform;
        EnsureArt();
        BuildGlow();
        BuildBursts();
    }

    private void OnDisable()
    {
        // Nothing should linger half-drawn if the overlay is switched off.
        _glowVis = 0f;
        if (_glow) _glow.gameObject.SetActive(false);
        if (_bursts != null)
            foreach (var b in _bursts) Kill(b);
    }

    // ------------------------------------------------------------------
    //  Per-frame
    // ------------------------------------------------------------------

    private void Update()
    {
        // Press detection lives in Update so the burst appears on the very frame
        // the finger lands (the reference shows the disc on the touch frame, not
        // on release). Legacy Input, like TutorialHand and TutorialFocusGate - the
        // project ships activeInputHandler = Both.
        if (!burstEnabled || !_gate) return;
        if (!TryGetPressThisFrame(out Vector2 screenPos)) return;

        // Only a press INSIDE the clickable hole gets a burst. A tap anywhere else
        // is swallowed by the scrim and must do nothing at all - no feedback either.
        if (!_gate.TryGetFocus(out _, out Rect hole)) return;
        if (!hole.Contains(screenPos)) return;

        SpawnBurst(screenPos);
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        TickGlow(dt);
        TickBursts(dt);
    }

    // ------------------------------------------------------------------
    //  Glow
    // ------------------------------------------------------------------

    private void TickGlow(float dt)
    {
        if (!_glow) return;

        bool focused = false;
        if (glowEnabled && _gate && _gate.TryGetFocus(out Rect targetScreen, out _))
        {
            if (TryScreenRectToLocal(targetScreen, out Rect local))
            {
                _lastTargetLocal = local;
                _hasLastTarget = true;
                focused = true;
            }
        }

        float rate = focused
            ? (glowFadeIn <= 0f ? 1f : dt / glowFadeIn)
            : (glowFadeOut <= 0f ? 1f : dt / glowFadeOut);
        _glowVis = Mathf.MoveTowards(_glowVis, focused ? 1f : 0f, rate);

        if (_glowVis <= 0f || !_hasLastTarget)
        {
            if (_glow.gameObject.activeSelf) _glow.gameObject.SetActive(false);
            if (!focused) _pulseClock = 0f;
            return;
        }

        if (!_glow.gameObject.activeSelf) _glow.gameObject.SetActive(true);

        _pulseClock += dt;
        float wave = 0.5f + 0.5f * Mathf.Sin(_pulseClock * pulseHz * Mathf.PI * 2f);
        float pulse = Mathf.Lerp(pulseMin, 1f, wave);

        // The sliced sprite already carries GlowOut units of halo outside the
        // target, so the frame is the target grown by exactly that much.
        float grow = GlowOut + glowGap + pulseGrow * wave;
        Rect r = _lastTargetLocal;
        _glowRt.anchoredPosition = r.center;
        _glowRt.sizeDelta = new Vector2(r.width + grow * 2f, r.height + grow * 2f);

        var c = glowColor;
        c.a *= _glowVis * pulse;
        _glow.color = c;
    }

    private void BuildGlow()
    {
        var go = new GameObject("TargetGlow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        _glowRt = (RectTransform)go.transform;
        _glowRt.SetParent(transform, false);
        Center(_glowRt);

        _glow = go.GetComponent<Image>();
        _glow.sprite = s_glowSprite;
        _glow.type = Image.Type.Sliced;
        _glow.fillCenter = false;           // the middle of the target stays untouched
        _glow.raycastTarget = false;        // must never steal the tap it decorates
        _glow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);

        go.SetActive(false);
    }

    // ------------------------------------------------------------------
    //  Burst
    // ------------------------------------------------------------------

    private void SpawnBurst(Vector2 screenPos)
    {
        if (_bursts == null || _bursts.Length == 0) return;
        if (!_gate || !_gate.CanvasRect) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_gate.CanvasRect, screenPos, _gate.UiCamera, out var local))
            return;

        // Reuse a free one, or recycle the oldest if both are busy.
        Burst pick = null;
        foreach (var b in _bursts)
            if (!b.alive) { pick = b; break; }
        if (pick == null)
        {
            pick = _bursts[0];
            foreach (var b in _bursts)
                if (b.t > pick.t) pick = b;
        }

        pick.alive = true;
        pick.t = 0f;
        pick.root.anchoredPosition = local;
        pick.root.gameObject.SetActive(true);
        ApplyBurst(pick);
    }

    private void TickBursts(float dt)
    {
        if (_bursts == null) return;

        float end = BurstDuration;
        foreach (var b in _bursts)
        {
            if (!b.alive) continue;
            b.t += dt;
            if (b.t >= end) { Kill(b); continue; }
            ApplyBurst(b);
        }
    }

    /// <summary>One sampled instant of the tap burst, in canvas units and alpha.</summary>
    public struct BurstFrame
    {
        public bool discOn;
        public float discDiameter;
        public float discAlpha;
        public bool ringOn;
        public float ringDiameter;
        public float ringAlpha;
    }

    /// <summary>Total lifetime of one burst, seconds.</summary>
    public float BurstDuration => Mathf.Max(discDuration, ringDelay + ringDuration);

    /// <summary>
    /// The whole burst timeline as ONE pure function of time. The renderer draws
    /// from it, and a test can sample it at the reference beat times and compare
    /// numbers instead of eyeballing a 0.2s effect.
    ///
    ///   fr.46  0.00 - discDuration              white disc swells and fades
    ///   fr.47  ringDelay - ringDelay+ringDuration  yellow ring expands and fades
    /// </summary>
    public BurstFrame EvaluateBurst(float t)
    {
        var f = new BurstFrame();

        // The disc stays bright for most of its life (1 - k^2): in the reference it
        // is solid on the touch frame and simply gone on the next.
        float k = discDuration <= 0f ? 1f : Mathf.Clamp01(t / discDuration);
        f.discOn = k < 1f;
        if (f.discOn)
        {
            f.discDiameter = discDiameter * Mathf.Lerp(discStartScale, 1f, EaseOutQuad(k));
            f.discAlpha = discColor.a * (1f - k * k);
        }

        float kr = ringDuration <= 0f ? 1f : (t - ringDelay) / ringDuration;
        f.ringOn = kr >= 0f && kr < 1f;
        if (f.ringOn)
        {
            f.ringDiameter = Mathf.Lerp(ringStartDiameter, ringEndDiameter, EaseOutCubic(kr));
            f.ringAlpha = ringColor.a * ringStartAlpha * Mathf.Pow(1f - kr, 1.5f);
        }

        return f;
    }

    /// <summary>Plays a burst at a screen position, as if the player had pressed there.</summary>
    public void PlayBurstAt(Vector2 screenPos) => SpawnBurst(screenPos);

    private void ApplyBurst(Burst b)
    {
        var f = EvaluateBurst(b.t);

        b.disc.gameObject.SetActive(f.discOn);
        if (f.discOn)
        {
            b.disc.sizeDelta = Vector2.one * f.discDiameter;
            var dc = discColor; dc.a = f.discAlpha;
            b.discImg.color = dc;
        }

        b.ring.gameObject.SetActive(f.ringOn);
        if (f.ringOn)
        {
            b.ring.sizeDelta = Vector2.one * f.ringDiameter;
            var rc = ringColor; rc.a = f.ringAlpha;
            b.ringImg.color = rc;
        }
    }

    private static void Kill(Burst b)
    {
        if (b == null) return;
        b.alive = false;
        b.t = 0f;
        if (b.root) b.root.gameObject.SetActive(false);
    }

    private void BuildBursts()
    {
        int n = Mathf.Max(1, burstPool);
        _bursts = new Burst[n];

        for (int i = 0; i < n; i++)
        {
            var root = new GameObject("TapBurst_" + i, typeof(RectTransform));
            root.layer = gameObject.layer;
            var rootRt = (RectTransform)root.transform;
            rootRt.SetParent(transform, false);
            Center(rootRt);
            rootRt.sizeDelta = Vector2.zero;

            var b = new Burst { root = rootRt };
            // ring first so the disc draws on top of it while they overlap
            b.ringImg = BuildBurstPart(rootRt, "Ring", s_ringSprite, out b.ring);
            b.discImg = BuildBurstPart(rootRt, "Disc", s_discSprite, out b.disc);

            root.SetActive(false);
            _bursts[i] = b;
        }
    }

    private Image BuildBurstPart(RectTransform parent, string partName, Sprite sprite, out RectTransform rt)
    {
        var go = new GameObject(partName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        Center(rt);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private bool TryScreenRectToLocal(Rect screen, out Rect local)
    {
        local = default;
        if (!_gate || !_gate.CanvasRect) return false;

        var canvas = _gate.CanvasRect;
        var cam = _gate.UiCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen.min, cam, out var lo)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, screen.max, cam, out var hi)) return false;

        local = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y),
                                Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
        return local.width > 0f && local.height > 0f;
    }

    private static bool TryGetPressThisFrame(out Vector2 screenPos)
    {
        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began)
            {
                screenPos = t.position;
                return true;
            }
        }

        screenPos = default;
        return false;
    }

    private static void Center(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
    }

    private static float EaseOutQuad(float x) => 1f - (1f - x) * (1f - x);
    private static float EaseOutCubic(float x) { float i = 1f - x; return 1f - i * i * i; }

    // ------------------------------------------------------------------
    //  Procedural art
    // ------------------------------------------------------------------

    private static void EnsureArt()
    {
        if (!s_glowTex || !s_glowSprite)
        {
            s_glowTex = BuildGlowTexture();
            s_glowSprite = Sprite.Create(s_glowTex, new Rect(0, 0, GlowSize, GlowSize),
                                         new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                         new Vector4(GlowBorder, GlowBorder, GlowBorder, GlowBorder));
            s_glowSprite.hideFlags = HideFlags.DontSave;
        }

        if (!s_discTex || !s_discSprite)
        {
            s_discTex = BuildRadialTexture(128, r => 1f - Smooth(0.45f, 1f, r));
            s_discSprite = MakeSprite(s_discTex);
        }

        if (!s_ringTex || !s_ringSprite)
        {
            // thin annulus at 86% of the radius with a soft, gaussian cross-section
            s_ringTex = BuildRadialTexture(128, r =>
            {
                float x = (r - 0.86f) / 0.06f;
                return Mathf.Exp(-x * x);
            });
            s_ringSprite = MakeSprite(s_ringTex);
        }
    }

    /// <summary>
    /// A 9-slice halo. The target box sits GlowOut px in from every edge; alpha is a
    /// function of the signed distance to that box, so the corners come out round
    /// on their own and the whole thing stretches to any button without distortion.
    /// </summary>
    private static Texture2D BuildGlowTexture()
    {
        var tex = NewTexture(GlowSize);
        var px = new Color32[GlowSize * GlowSize];

        float boxMin = GlowOut;
        float boxMax = GlowSize - GlowOut;

        for (int y = 0; y < GlowSize; y++)
        {
            for (int x = 0; x < GlowSize; x++)
            {
                float fx = x + 0.5f, fy = y + 0.5f;
                float dx = Mathf.Max(boxMin - fx, 0f, fx - boxMax);
                float dy = Mathf.Max(boxMin - fy, 0f, fy - boxMax);

                float d;   // signed: >0 outside the target, <0 inside
                if (dx > 0f || dy > 0f) d = Mathf.Sqrt(dx * dx + dy * dy);
                else d = -Mathf.Min(fx - boxMin, boxMax - fx, fy - boxMin, boxMax - fy);

                float a;
                if (d >= 0f)
                {
                    float halo = d < GlowOut ? Mathf.Pow(1f - d / GlowOut, 2f) * 0.8f : 0f;
                    float core = 0.55f * Mathf.Exp(-((d - 1.2f) * (d - 1.2f)) / 1.8f);
                    a = halo + core;
                }
                else
                {
                    a = d > -GlowIn ? 0.45f * (1f + d / GlowIn) : 0f;   // warm bleed inward
                }

                px[y * GlowSize + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D BuildRadialTexture(int size, System.Func<float, float> alphaOfRadius)
    {
        var tex = NewTexture(size);
        var px = new Color32[size * size];
        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = r >= 1f ? 0f : Mathf.Clamp01(alphaOfRadius(r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    private static Texture2D NewTexture(int size)
    {
        return new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
            name = "TutorialFocusFx (procedural)"
        };
    }

    private static Sprite MakeSprite(Texture2D tex)
    {
        var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }
}
