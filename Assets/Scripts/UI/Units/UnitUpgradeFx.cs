using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The two "you just levelled up" effects from the onboarding reference video
/// (OnBoarding 1.mkv, 10.90-11.90 s), rebuilt FRAME BY FRAME from the clip read one
/// frame per image at full size. Plays on EVERY successful hero upgrade.
///
/// t = 0 is reference frame n7. The recording repeats frames (n8 = n9, n10 = n11),
/// so the real key frames are n7, n8, n10, n12, n14, n16, n20, n28, n34.
///
/// HERO
///   n7   0.00  flat lemon-yellow column (~31% of the screen wide, hard-ish edges)
///              inside a wider pale column (~44%); top just above the head; a puffy
///              white-yellow CLOUD over the body
///   n8   0.03  the column shoots up off the top of the screen; cloud holds
///   n10  0.10  the column breaks into ~8 broad translucent BANDS of different
///              widths/brightness filling the wide column; hero shows through
///   n12  0.17  5 narrow bands left; 3 bold 4-point sparkles pop (above the head
///              on the left, at the raised hand, a small one below it)
///   n14  0.23  3 bands left (left edge, centre, right edge); sparkles RISE+SHRINK;
///   n16  0.30  a new small glint appears on the left
///   n20  0.43  one band left, on the left
///   n22  0.50  gone
///
/// STATS ROW
///   n7   0.00  needle-thin line starting ON the level number + 3 SOLID white
///              diamonds joined edge to edge (~24% of the bar wide, ~1.2x its height)
///              with a soft grey fringe
///   n8   0.03  diamonds turn translucent grey; a white LENS BEAM pointed at both ends
///              runs through them with a bulge on each value
///   n10  0.10  a lime-yellow SPINDLE (pointed both ends) on each value, blurring the
///              digits, + a yellow-white line
///   n12  0.17  spindles gone; thin lime line + a SMOKY WISPY haze along the row;
///              digits get a warm glow
///   n14-18     smoky wisps between icons and values, fading
///   n28-34     faint curly wisps; the glow around each number lingers; gone ~1.2 s
///
/// Hooks in without editing gameplay code: bootstraps itself, subscribes to
/// PlayerProgressionService.OnUnitUpgraded, plays ONE FRAME LATER (SetHeader
/// re-instantiates the hero and Destroy is deferred). The hero is a SpriteRenderer
/// rig at order 0, so the hero layer is a nested canvas at order 1.
/// </summary>
[DisallowMultipleComponent]
public class UnitUpgradeFx : MonoBehaviour
{
    [Header("General")]
    [SerializeField] private bool effectEnabled = true;
    [SerializeField] private float totalDuration = 1.4f;
    [SerializeField] private int heroSortingOrder = 1;

    [Header("Hero column")]
    [Tooltip("The reference column is about the character's body width (0.31 of the " +
             "screen for Anna). Ours is sized off the measured hero, clamped to this range.")]
    [SerializeField] private float innerOfHeroWidth = 0.80f;
    [SerializeField] private float minInnerOfScreen = 0.28f;
    [SerializeField] private float maxInnerOfScreen = 0.40f;
    [Tooltip("Outer pale column vs inner (reference: 0.44 / 0.31).")]
    [SerializeField] private float outerOfInner = 1.42f;

    [Header("Colours")]
    [SerializeField] private Color lemon = new Color(1f, 0.95f, 0.18f, 1f);
    [SerializeField] private Color paleYellow = new Color(1f, 0.92f, 0.50f, 1f);
    [SerializeField] private Color cloud = new Color(1f, 0.98f, 0.82f, 1f);
    [SerializeField] private Color band = new Color(1f, 0.93f, 0.35f, 1f);
    [SerializeField] private Color sparkle = new Color(1f, 0.97f, 0.70f, 1f);
    [SerializeField] private Color lime = new Color(0.90f, 1f, 0.35f, 1f);
    [SerializeField] private Color digitGlow = new Color(1f, 0.90f, 0.35f, 1f);

    // ------------------------------------------------------------------

    // MEASURED BRIGHTNESS CURVES. Mean luminance of the hero area and of the stats
    // row on every frame of the reference's SECOND upgrade (m66 -> m95, 30 fps),
    // expressed as a fraction of the peak excess over the resting frame. These drive
    // the intensity of the flash layers, so the timing is the reference's, not a
    // guess.
    //   hero : 165 -> 204 204 200 194 177 172 169 169 167 ... 165
    //   stats:  71 -> 197 197 156 129  95  98  93  93  87  86  85  84 ... 76 @0.93s
    private static readonly float[] HeroT = { 0f, 0.033f, 0.067f, 0.10f, 0.133f, 0.167f, 0.20f, 0.267f, 0.37f };
    private static readonly float[] HeroV = { 1f, 1f, 0.90f, 0.75f, 0.32f, 0.17f, 0.10f, 0.07f, 0f };
    private static readonly float[] StatT = { 0f, 0.033f, 0.067f, 0.10f, 0.133f };
    private static readonly float[] StatV = { 1f, 1f, 0.67f, 0.46f, 0.19f };

    private static float Curve(float t, float[] ts, float[] vs)
    {
        if (t <= ts[0]) return vs[0];
        for (int i = 1; i < ts.Length; i++)
            if (t <= ts[i]) return Mathf.Lerp(vs[i - 1], vs[i], (t - ts[i - 1]) / (ts[i] - ts[i - 1]));
        return vs[vs.Length - 1];
    }

    // Band layout read off the SECOND upgrade (m70-m78): x as a fraction of the
    // OUTER column's half-width, width as a fraction of the screen, peak alpha, and
    // the time each band is gone. Band 0 is THE band - one broad strip just left of
    // centre that outlives everything (m71-m78). 1-2 are faint side strips (m71-m73);
    // 3-5 are only the streaky texture inside the column on m70.
    private static readonly float[] BandX    = { -0.16f, -0.44f, 0.68f, -0.80f, 0.35f, 0.90f };
    private static readonly float[] BandW    = { 0.15f, 0.035f, 0.035f, 0.03f, 0.03f, 0.03f };
    private static readonly float[] BandA    = { 1.00f, 0.60f, 0.50f, 0.45f, 0.45f, 0.40f };
    private static readonly float[] BandEnd  = { 0.42f, 0.24f, 0.20f, 0.15f, 0.15f, 0.15f };

    // Sparkles read off m71..m78: offset from the hero centre as a fraction of the
    // screen width, height as a fraction of the hero, start time, peak size. They
    // rise and shrink.
    private static readonly float[] SparkDx    = { -0.09f, 0.12f, 0.14f, -0.17f };
    private static readonly float[] SparkH     = { 0.85f, 0.70f, 0.54f, 0.49f };
    // Started 0.04 s before n12 so they are ALREADY full size on n12, as in the
    // reference (starting on n12 put a half-grown hairline cross there).
    private static readonly float[] SparkStart = { 0.09f, 0.09f, 0.13f, 0.26f };
    // Peak size as a fraction of the screen width (m71: ~8.5% of the width visible,
    // the texture's arm tips are faint so it is authored larger).
    private static readonly float[] SparkSize  = { 0.12f, 0.12f, 0.05f, 0.045f };
    private const float SparkLife = 0.35f;
    private const float SparkRiseSpeed = 250f;   // canvas units / s (n12 -> n20: ~75 u)

    private static UnitUpgradeFx s_instance;

    private PlayerProgressionService _service;
    private bool _pending;
    private int _playAtFrame;

    private readonly Dictionary<UnitDetailView, Rig> _rigs = new Dictionary<UnitDetailView, Rig>();
    private Rig _last;

    private class Rig
    {
        public UnitDetailView view;

        public RectTransform heroRoot;
        public Image wash, outer, inner, cloudImg;
        public Image[] bands;
        public Image[] sparks;

        public RectTransform statsRoot;
        public Image[] digitGlows;
        public Image smoke;
        public Image[] wisps;
        public Image[] spindles;
        public Image beam;
        public Image[] bulges;
        public Image line;
        public Image rowGlow;
        public Image[] fringes, diamonds;

        // measured, in each root's local space
        public float W, cx, feet, heroTop, heroH, rootTop, rootBottom, innerW;
        public Vector2[] value;          // centres of the value glyphs
        public Vector2[] valueSize;      // sizes of the value glyphs
        public float rowY, lineX0, barRight, barW, barH, beamX0;

        public float t;
        public bool playing;
    }

    // ------------------------------------------------------------------
    //  Bootstrap + trigger
    // ------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (s_instance) return;
        var go = new GameObject("UnitUpgradeFx");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<UnitUpgradeFx>();
    }

    private void OnDestroy()
    {
        if (_service != null) _service.OnUnitUpgraded -= HandleUpgraded;
        if (s_instance == this) s_instance = null;
    }

    private void Update()
    {
        var svc = GameStartManager.Instance ? GameStartManager.Instance.ProgressionService : null;
        if (svc == _service) return;
        if (_service != null) _service.OnUnitUpgraded -= HandleUpgraded;
        _service = svc;
        if (_service != null) _service.OnUnitUpgraded += HandleUpgraded;
    }

    private void HandleUpgraded(int unitId, int oldLevel, int newLevel, int cost)
    {
        // One frame later: the hero is being re-instantiated this frame.
        _pending = true;
        _playAtFrame = Time.frameCount + 1;
    }

    private void LateUpdate()
    {
        if (_pending && Time.frameCount >= _playAtFrame)
        {
            _pending = false;
            var view = FindShownDetailView();
            if (view) PlayOn(view);
        }

        float dt = Time.unscaledDeltaTime;
        foreach (var rig in _rigs.Values)
        {
            if (!rig.playing) continue;
            rig.t += dt;
            if (rig.t >= totalDuration) Stop(rig);
            else Apply(rig, rig.t);
        }
    }

    private static UnitDetailView FindShownDetailView()
    {
        foreach (var v in FindObjectsOfType<UnitDetailView>())
        {
            if (!v.isActiveAndEnabled) continue;
            if (!v.VisualRoot || !v.VisualRoot.gameObject.activeInHierarchy) continue;
            if (!v.CurrentVisual) continue;
            return v;
        }
        return null;
    }

    // ------------------------------------------------------------------
    //  Public API
    // ------------------------------------------------------------------

    public float Duration => totalDuration;

    /// <summary>Play now on a detail view (restarts if already playing).</summary>
    public void PlayOn(UnitDetailView view)
    {
        if (!effectEnabled || !view) return;

        PruneDeadRigs();
        if (!_rigs.TryGetValue(view, out var rig) || !rig.heroRoot || !rig.statsRoot)
        {
            rig = BuildRig(view);
            if (rig == null) return;
            _rigs[view] = rig;
        }

        Measure(rig);
        rig.t = 0f;
        rig.playing = true;
        SetRootsActive(rig, true);
        Apply(rig, 0f);
        _last = rig;
    }

    /// <summary>Freeze the last-played rig at time t (tests / filmstrips, no Play mode).</summary>
    public void SampleAt(float t)
    {
        if (_last == null) return;
        _last.playing = false;
        SetRootsActive(_last, true);
        Apply(_last, Mathf.Clamp(t, 0f, totalDuration));
    }

    // ------------------------------------------------------------------
    //  The beats
    // ------------------------------------------------------------------

    private void Apply(Rig r, float t)
    {
        ApplyHero(r, t);
        ApplyStats(r, t);
    }

    private void ApplyHero(Rig r, float t)
    {
        // Intensity follows the reference's MEASURED brightness (HeroT/HeroV).
        float e = Curve(t, HeroT, HeroV);

        // Top of the screen down to the bottom of the portrait - over the CP bar,
        // which the reference washes out too (m67).
        float bottom = r.rootBottom;
        float colH = Mathf.Max(1f, r.rootTop - bottom);
        float colY = bottom + colH * 0.5f;
        float outerW = r.innerW * outerOfInner;

        // WIDE WASH over the whole portrait. This is most of why the reference peak
        // reads as covering ~70% of the screen: beyond the core and the pale column
        // the entire hero area brightens to cream.
        Place(r.wash, new Vector2(r.cx, colY), new Vector2(r.W * 1.1f, colH), cloud, 0.55f * e);

        // PALE outer column - also the lit area BEHIND the bands on m70-m72.
        Place(r.outer, new Vector2(r.cx, colY), new Vector2(outerW, colH), paleYellow, 0.75f * e);

        // LEMON core, near opaque at the peak (the hero is a faint ghost on m67-68),
        // narrowing as it breaks up (m69-m70) and gone by m71.
        float coreGone = 1f - Smooth(0.10f, 0.133f, t);
        float coreW = r.innerW * Mathf.Lerp(1f, 0.78f, Smooth(0.033f, 0.10f, t));
        Place(r.inner, new Vector2(r.cx, colY), new Vector2(coreW, colH), lemon, 0.95f * e * coreGone);

        // lumpy cloud over the body, same life as the core
        Place(r.cloudImg, new Vector2(r.cx, r.feet + 0.5f * r.heroH),
              new Vector2(r.innerW * 1.15f, r.heroH * 0.95f), cloud, 0.75f * e * coreGone);

        // m70 onwards: bands. They keep running off the TOP of the screen (m71-m78)
        // and lift off the CP bar to the feet; the main one narrows to ~40%.
        float bandBottom = Mathf.Lerp(bottom, r.feet - 20f, Smooth(0.10f, 0.20f, t));
        float bandTop = r.rootTop;
        float bandH = Mathf.Max(1f, bandTop - bandBottom);
        float half = outerW * 0.5f;
        for (int i = 0; i < r.bands.Length; i++)
        {
            float end = BandEnd[i];
            float appear = Smooth(0.05f, 0.10f, t);
            float gone = 1f - Smooth(end - 0.06f, end, t);
            float w = r.W * BandW[i] * Mathf.Lerp(1f, 0.30f, Smooth(0.17f, end, t));
            Place(r.bands[i], new Vector2(r.cx + BandX[i] * half, bandBottom + bandH * 0.5f),
                  new Vector2(w, bandH), band, BandA[i] * appear * gone);
        }

        // n12..n20: bold sparkles that pop, then rise and shrink.
        for (int i = 0; i < r.sparks.Length; i++)
        {
            float ts = t - SparkStart[i];
            if (ts <= 0f || ts >= SparkLife) { Place(r.sparks[i], Vector2.zero, Vector2.zero, sparkle, 0f); continue; }

            float pop = ts < 0.04f ? Mathf.Lerp(0f, 1.15f, ts / 0.04f) : Mathf.Lerp(1.15f, 0.3f, (ts - 0.04f) / (SparkLife - 0.04f));
            var pos = new Vector2(r.cx + SparkDx[i] * r.W, r.feet + SparkH[i] * r.heroH + SparkRiseSpeed * ts);
            float a = 1f - Smooth(SparkLife - 0.06f, SparkLife, ts);
            Place(r.sparks[i], pos, Vector2.one * SparkSize[i] * r.W * pop, sparkle, a);
        }
    }

    private void ApplyStats(Rig r, float t)
    {
        float lineW = r.barRight - r.lineX0;
        float lineMid = (r.lineX0 + r.barRight) * 0.5f;

        // Intensity of the white phase follows the MEASURED stats curve (StatT/StatV).
        float s = Curve(t, StatT, StatV);

        // --- m67-68: a big soft white glow swallows the WHOLE row - icons and
        // numbers alike - from the level number to past the end. Gone by m71.
        float peakGone = 1f - Smooth(0.10f, 0.133f, t);
        Place(r.rowGlow, new Vector2(lineMid, r.rowY), new Vector2(lineW + 60f, r.barH * 1.8f),
              Color.white, 0.9f * s * peakGone);

        // --- diamonds, taller than the bar (1.6x) and overlapping, so nothing shows
        // between them at the peak; translucent on m69-70; gone m71
        float diaA = t < 0.034f ? 1f : Curve(t, new[] { 0.034f, 0.067f, 0.10f, 0.133f }, new[] { 0.5f, 0.35f, 0.22f, 0f });
        var dia = new Vector2(r.barW * 0.30f, r.barH * 1.6f);
        for (int i = 0; i < 3; i++)
        {
            var p = new Vector2(r.value[i].x, r.rowY);
            Place(r.fringes[i], p, dia * 1.25f, Color.white, 0.4f * diaA);
            Place(r.diamonds[i], p, dia, Color.white, diaA);
        }

        // --- white lens beam + bulges, strongest on m69-70, gone m71
        float beamA = t < 0.034f ? 0.8f : Curve(t, new[] { 0.034f, 0.067f, 0.10f, 0.133f }, new[] { 0.95f, 1f, 0.9f, 0f });
        Place(r.beam, new Vector2((r.beamX0 + r.barRight) * 0.5f, r.rowY),
              new Vector2(r.barRight - r.beamX0, r.barH * 0.40f), Color.white, beamA);
        for (int i = 0; i < 3; i++)
            Place(r.bulges[i], new Vector2(r.value[i].x, r.rowY), new Vector2(r.barW * 0.28f, r.barH * 0.55f), Color.white, beamA);

        // --- line: white through m70, then lime; it runs on INSIDE the smoke to m75+
        Color lineC = t < 0.10f ? Color.white : Color.Lerp(Color.Lerp(Color.white, lime, 0.5f), lime, Smooth(0.10f, 0.17f, t));
        float lineThick = Mathf.Lerp(16f, 9f, Smooth(0.10f, 0.20f, t));
        float lineA = 1f - Smooth(0.30f, 0.60f, t);
        Place(r.line, new Vector2(lineMid, r.rowY), new Vector2(lineW, lineThick), lineC, lineA);

        // --- m71: lime spindles on each value; gone by m72-73
        float spA = Smooth(0.10f, 0.133f, t) * (1f - Smooth(0.167f, 0.20f, t));
        for (int i = 0; i < 3; i++)
            Place(r.spindles[i], new Vector2(r.value[i].x, r.rowY), new Vector2(r.barW * 0.22f, r.barH * 0.36f), lime, spA);

        // --- m72 onwards: the smoky lime band covers the FULL bar height and lingers
        // - the measured stats brightness is still 20% of its peak at 0.3 s and
        // ~10% at 0.9 s.
        float smokeA = Smooth(0.12f, 0.167f, t)
                     * Curve(t, new[] { 0.167f, 0.30f, 0.50f, 0.93f, 1.40f }, new[] { 0.70f, 0.50f, 0.35f, 0.15f, 0f });
        Place(r.smoke, new Vector2(lineMid, r.rowY), new Vector2(lineW, r.barH * 1.0f), lime, smokeA);

        // --- short wisps between each icon and its value, fading to the end
        float wispA = Smooth(0.20f, 0.30f, t) * Curve(t, new[] { 0.30f, 0.93f, 1.40f }, new[] { 0.45f, 0.20f, 0f });
        for (int i = 0; i < 3; i++)
            Place(r.wisps[i], new Vector2(r.value[i].x - r.barW * 0.07f, r.rowY),
                  new Vector2(r.barW * 0.17f, r.barH * 0.32f), lime, wispA);

        // --- a faint, TIGHT warm edge on each number (not a disc)
        float glowA = Smooth(0.13f, 0.167f, t) * Curve(t, new[] { 0.167f, 0.50f, 1.40f }, new[] { 0.30f, 0.20f, 0f });
        for (int i = 0; i < 3; i++)
            Place(r.digitGlows[i], new Vector2(r.value[i].x, r.rowY),
                  new Vector2(r.valueSize[i].x * 1.12f + 12f, r.valueSize[i].y * 1.25f + 8f), digitGlow, glowA);
    }

    private static void Place(Image img, Vector2 pos, Vector2 size, Color c, float alpha)
    {
        if (!img) return;
        var rt = img.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        c.a = Mathf.Clamp01(alpha);
        img.color = c;
    }

    private void Stop(Rig r)
    {
        r.playing = false;
        SetRootsActive(r, false);
    }

    private static void SetRootsActive(Rig r, bool on)
    {
        if (r.heroRoot) r.heroRoot.gameObject.SetActive(on);
        if (r.statsRoot) r.statsRoot.gameObject.SetActive(on);
    }

    // ------------------------------------------------------------------
    //  Measuring
    // ------------------------------------------------------------------

    private void Measure(Rig r)
    {
        Canvas.ForceUpdateCanvases();

        // --- hero: the COLUMN is sized off the screen (as measured in the
        // reference) and centred on the hero's feet; only its height and the cloud
        // and sparkle heights use the hero's own bounds.
        Rect root = r.heroRoot.rect;
        r.W = root.width;
        r.rootTop = root.yMax;
        r.rootBottom = root.yMin;

        Vector2 feet = r.view.VisualRoot ? (Vector2)r.heroRoot.InverseTransformPoint(r.view.VisualRoot.position) : Vector2.zero;
        r.cx = feet.x;

        Rect hero = MeasureHero(r.view, r.heroRoot, feet);
        r.feet = hero.yMin;
        r.heroTop = hero.yMax;
        r.heroH = Mathf.Max(1f, hero.height);
        r.innerW = Mathf.Clamp(hero.width * innerOfHeroWidth, r.W * minInnerOfScreen, r.W * maxInnerOfScreen);

        // --- stats
        Rect bar = r.statsRoot.rect;
        r.barW = bar.width;
        r.barH = bar.height;
        r.barRight = bar.xMax - 6f;

        var values = new[] { r.view.HpText, r.view.AtkText, r.view.DefText };
        float ySum = 0f; int yN = 0;
        for (int i = 0; i < 3; i++)
        {
            GlyphRect(values[i], r.statsRoot, out r.value[i], out r.valueSize[i]);
            if (values[i]) { ySum += r.value[i].y; yN++; }
        }
        r.rowY = yN > 0 ? ySum / yN : 0f;

        // The needle starts ON the level number (reference n7), not after it.
        if (r.view.LevelText)
        {
            GlyphRect(r.view.LevelText, r.statsRoot, out var lc, out var ls);
            r.lineX0 = lc.x + ls.x * 0.5f - 30f;
        }
        else r.lineX0 = bar.xMin + 20f;

        // The lens beam starts at the first stat icon (reference n8).
        r.beamX0 = Mathf.Min(r.value[0].x, r.value[1].x, r.value[2].x) - r.barW * 0.15f;
    }

    private static Rect MeasureHero(UnitDetailView view, RectTransform space, Vector2 feet)
    {
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;

        var visual = view.CurrentVisual;
        if (visual)
        {
            foreach (var sr in visual.GetComponentsInChildren<SpriteRenderer>(false))
            {
                if (!sr.enabled || !sr.sprite || sr.color.a < 0.05f) continue;
                if (sr.gameObject.name.IndexOf("FX", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                var b = sr.bounds;
                Vector2 a = space.InverseTransformPoint(b.min);
                Vector2 c = space.InverseTransformPoint(b.max);
                Vector2 lo = Vector2.Min(a, c), hi = Vector2.Max(a, c);
                if (!any) { min = lo; max = hi; any = true; }
                else { min = Vector2.Min(min, lo); max = Vector2.Max(max, hi); }
            }
        }

        if (any && max.y - min.y > 1f) return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return new Rect(feet.x - 150f, feet.y, 300f, 450f);
    }

    /// <summary>Centre and size of a text's RENDERED glyphs in `space`.</summary>
    private static void GlyphRect(TMP_Text text, RectTransform space, out Vector2 centre, out Vector2 size)
    {
        centre = Vector2.zero; size = new Vector2(80f, 40f);
        if (!text) return;

        var rt = text.rectTransform;
        text.ForceMeshUpdate();
        var b = text.textBounds;
        Vector3 lo, hi;
        if (b.size.x > 1f) { lo = rt.TransformPoint(b.min); hi = rt.TransformPoint(b.max); }
        else { lo = rt.TransformPoint(rt.rect.min); hi = rt.TransformPoint(rt.rect.max); }

        Vector2 l = space.InverseTransformPoint(lo), h = space.InverseTransformPoint(hi);
        centre = (l + h) * 0.5f;
        size = new Vector2(Mathf.Abs(h.x - l.x), Mathf.Abs(h.y - l.y));
    }

    // ------------------------------------------------------------------
    //  Building
    // ------------------------------------------------------------------

    private Rig BuildRig(UnitDetailView view)
    {
        if (!view.VisualRoot || !view.VisualRoot.parent) return null;
        var bar = CommonParent(view.HpText, view.AtkText, view.DefText);
        if (!bar) return null;

        EnsureArt();
        var r = new Rig { view = view, value = new Vector2[3], valueSize = new Vector2[3] };

        // HERO layer on a nested canvas, so it draws over the hero's sprites (order 0)
        // and stays under the stats popup (order 5).
        r.heroRoot = NewRoot("UpgradeFx_Hero", (RectTransform)view.VisualRoot.parent);
        var canvas = r.heroRoot.gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = heroSortingOrder;

        r.wash = NewImage("Wash", r.heroRoot, s_wash);   // first = furthest back
        r.outer = NewImage("OuterColumn", r.heroRoot, s_column);
        r.inner = NewImage("InnerColumn", r.heroRoot, s_column);
        r.bands = new Image[BandX.Length];
        for (int i = 0; i < r.bands.Length; i++) r.bands[i] = NewImage("Band" + i, r.heroRoot, s_band);
        r.cloudImg = NewImage("Cloud", r.heroRoot, s_cloud);
        r.sparks = new Image[SparkDx.Length];
        for (int i = 0; i < r.sparks.Length; i++) r.sparks[i] = NewImage("Sparkle" + i, r.heroRoot, s_sparkle);

        // STATS layer: the last child of the bar, so it draws over the values.
        // Draw order bottom -> top: digit glow, smoke, spindles, beam + bulges, line,
        // fringe, diamonds.
        r.statsRoot = NewRoot("UpgradeFx_Stats", bar);
        r.statsRoot.SetAsLastSibling();

        r.digitGlows = new Image[3];
        for (int i = 0; i < 3; i++) r.digitGlows[i] = NewImage("DigitGlow" + i, r.statsRoot, s_softRect);
        r.smoke = NewImage("Smoke", r.statsRoot, s_smoke);
        r.wisps = new Image[3];
        for (int i = 0; i < 3; i++) r.wisps[i] = NewImage("Wisp" + i, r.statsRoot, s_smoke);
        r.spindles = new Image[3];
        for (int i = 0; i < 3; i++) r.spindles[i] = NewImage("Spindle" + i, r.statsRoot, s_lens);
        r.rowGlow = NewImage("RowGlow", r.statsRoot, s_ellipse);
        r.beam = NewImage("LensBeam", r.statsRoot, s_lens);
        r.bulges = new Image[3];
        for (int i = 0; i < 3; i++) r.bulges[i] = NewImage("Bulge" + i, r.statsRoot, s_lens);
        r.line = NewImage("Needle", r.statsRoot, s_needle);
        r.fringes = new Image[3];
        for (int i = 0; i < 3; i++) r.fringes[i] = NewImage("Fringe" + i, r.statsRoot, s_fringe);
        r.diamonds = new Image[3];
        for (int i = 0; i < 3; i++) r.diamonds[i] = NewImage("Diamond" + i, r.statsRoot, s_diamond);

        SetRootsActive(r, false);
        return r;
    }

    private static RectTransform NewRoot(string rootName, RectTransform parent)
    {
        var go = new GameObject(rootName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<LayoutElement>().ignoreLayout = true;   // never shift the real UI
        return rt;
    }

    private static Image NewImage(string imageName, RectTransform parent, Sprite sprite)
    {
        var go = new GameObject(imageName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    private static RectTransform CommonParent(params TMP_Text[] texts)
    {
        Transform common = null;
        foreach (var t in texts)
        {
            if (!t) continue;
            if (!common) { common = t.transform.parent; continue; }
            while (common && !t.transform.IsChildOf(common)) common = common.parent;
        }
        return common as RectTransform;
    }

    private void PruneDeadRigs()
    {
        List<UnitDetailView> dead = null;
        foreach (var kv in _rigs)
            if (!kv.Key) (dead ??= new List<UnitDetailView>()).Add(kv.Key);
        if (dead != null) foreach (var k in dead) _rigs.Remove(k);
    }

    private static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    // ------------------------------------------------------------------
    //  Procedural art (built once, shared, never saved). x, y in [-1, 1].
    // ------------------------------------------------------------------

    private static Sprite s_column, s_band, s_cloud, s_sparkle;
    private static Sprite s_diamond, s_fringe, s_lens, s_needle, s_smoke, s_softRect;
    private static Sprite s_wash, s_ellipse;

    private static void EnsureArt()
    {
        // FLAT column: uniform across its width with only a thin soft edge (the
        // reference column has near-hard sides), soft at the feet, open at the top.
        if (!s_column) s_column = Build(64, 256, (x, y) =>
            (1f - Smooth(0.80f, 1f, Mathf.Abs(x))) * Smooth(-1f, -0.9f, y));

        // Band: a FLAT strip of light (the reference's main band has near-hard
        // sides) with a brighter core. Soft at the feet, open at the top - it runs
        // off the top of the screen.
        if (!s_band) s_band = Build(32, 256, (x, y) =>
            (0.65f * (1f - Smooth(0.55f, 1f, Mathf.Abs(x))) + 0.35f * Mathf.Exp(-(x / 0.35f) * (x / 0.35f)))
            * Smooth(-1f, -0.85f, y));

        // Puffy cloud: many SMALL lumps that do not fully merge, so the hero's outline
        // shows through the gaps (reference n7-n9), capped below opaque.
        if (!s_cloud) s_cloud = Build(128, 128, (x, y) =>
        {
            float a = 0f;
            a += Lump(x, y, -0.35f, 0.55f, 0.24f);
            a += Lump(x, y, 0.05f, 0.62f, 0.22f);
            a += Lump(x, y, 0.40f, 0.48f, 0.23f);
            a += Lump(x, y, -0.18f, 0.25f, 0.26f);
            a += Lump(x, y, 0.25f, 0.18f, 0.25f);
            a += Lump(x, y, -0.45f, -0.05f, 0.22f);
            a += Lump(x, y, 0.02f, -0.08f, 0.27f);
            a += Lump(x, y, 0.45f, -0.12f, 0.22f);
            a += Lump(x, y, -0.25f, -0.42f, 0.25f);
            a += Lump(x, y, 0.22f, -0.48f, 0.24f);
            a += Lump(x, y, -0.02f, -0.72f, 0.20f);
            return Mathf.Min(0.9f, a) * (1f - Smooth(0.85f, 1f, Mathf.Sqrt(x * x + y * y)));
        });

        // BOLD 4-point star: fat concave arms + a strong halo. The reference
        // sparkles are chunky; hairline arms read as a tiny "+".
        if (!s_sparkle) s_sparkle = Build(64, 64, (x, y) =>
        {
            float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
            float armH = ax < 1f ? Mathf.Clamp01(1f - ay / (0.36f * Mathf.Pow(1f - ax, 1.3f) + 0.001f)) : 0f;
            float armV = ay < 1f ? Mathf.Clamp01(1f - ax / (0.36f * Mathf.Pow(1f - ay, 1.3f) + 0.001f)) : 0f;
            float halo = 0.6f * Mathf.Exp(-(x * x + y * y) / 0.14f);
            return Mathf.Max(armH, armV) + halo;
        });

        // Portrait-wide wash: broad and soft, strongest in the middle third.
        if (!s_wash) s_wash = Build(64, 128, (x, y) =>
            Mathf.Exp(-(x / 0.75f) * (x / 0.75f)) * (1f - Smooth(0.80f, 1f, Mathf.Abs(y))));

        // Soft ellipse that swallows the stats row at the peak.
        if (!s_ellipse) s_ellipse = Build(128, 64, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return (1f - Smooth(0.55f, 1f, r)) * (0.6f + 0.4f * Mathf.Exp(-(y / 0.45f) * (y / 0.45f)));
        });

        // Soft rounded rectangle, for the tight warm edge on each number.
        if (!s_softRect) s_softRect = Build(64, 32, (x, y) =>
            (1f - Smooth(0.55f, 1f, Mathf.Abs(x))) * (1f - Smooth(0.35f, 1f, Mathf.Abs(y))));

        // SOLID diamond with a small anti-aliased rim (reference n7 is near-opaque).
        if (!s_diamond) s_diamond = Build(128, 64, (x, y) =>
            Mathf.Clamp01((1f - (Mathf.Abs(x) + Mathf.Abs(y))) * 6f));

        // Soft grey fringe around it.
        if (!s_fringe) s_fringe = Build(128, 64, (x, y) =>
            Mathf.Pow(Mathf.Clamp01(1f - (Mathf.Abs(x) + Mathf.Abs(y))), 0.7f));

        // Lens / spindle: pointed at BOTH ends, fattest in the middle.
        if (!s_lens) s_lens = Build(128, 32, (x, y) =>
        {
            float halfH = Mathf.Pow(Mathf.Max(0f, 1f - x * x), 1.1f);
            return halfH <= 0.001f ? 0f : Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(y) / halfH), 0.6f);
        });

        // Needle: a hairline that tapers to a point at the left end.
        if (!s_needle) s_needle = Build(256, 16, (x, y) =>
        {
            float taper = Smooth(-1f, -0.75f, x);
            float thick = 0.15f + 0.85f * taper;
            return Mathf.Exp(-(y / (0.45f * thick)) * (y / (0.45f * thick))) * taper * (1f - Smooth(0.96f, 1f, x));
        });

        // Smoke: SOFT, LOW-CONTRAST cloudy noise with a faint thin core line. The
        // first version used high y-frequency and hard contrast, which produced
        // parallel "speed lines" - the reference is a hazy smudge.
        if (!s_smoke) s_smoke = Build(256, 64, (x, y) =>
        {
            float n = Mathf.PerlinNoise(x * 2.2f + 11.3f, y * 2.6f + 4.1f) * 0.7f
                    + Mathf.PerlinNoise(x * 5.0f + 2.7f, y * 4.5f + 9.9f) * 0.3f;
            float haze = Mathf.Clamp01((n - 0.30f) * 1.6f);
            float core = 0.35f * Mathf.Exp(-(y / 0.12f) * (y / 0.12f));
            return (haze * Mathf.Exp(-(y / 0.6f) * (y / 0.6f)) + core) * (1f - Smooth(0.75f, 1f, Mathf.Abs(x)));
        });
    }

    private static float Lump(float x, float y, float cx, float cy, float r)
    {
        float dx = x - cx, dy = y - cy;
        return Mathf.Exp(-(dx * dx + dy * dy) / (r * r)) * 0.9f;
    }

    private static Sprite Build(int w, int h, System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
            name = "UnitUpgradeFx (procedural)"
        };

        var px = new Color32[w * h];
        for (int j = 0; j < h; j++)
        {
            float y = (j + 0.5f) / h * 2f - 1f;
            for (int i = 0; i < w; i++)
            {
                float x = (i + 0.5f) / w * 2f - 1f;
                px[j * w + i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x, y)) * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);

        var s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        s.hideFlags = HideFlags.DontSave;
        return s;
    }
}
