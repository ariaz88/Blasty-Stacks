using UnityEngine;

/// <summary>
/// The glass capsule a hero stands in for the moment it appears on a deploy stage.
/// Rebuilt 1:1 from Arts/.../Stage VFX/Hero Appearance animation/Preview_mockup.png:
///
///   - the glow is exactly as wide as the stage's top cap and its bottom sits on the
///     cap's front rim (never below the stage);
///   - its top reaches the middle of the hero's head;
///   - the glow moves like the landing pillar in Arts/Reference videos/VFX ANIM: it
///     shoots up out of the cap, flickers like a flame at full height, then stretches
///     up, thins and fades out - fading in and out, colour always the sprite's own;
///   - four stars pop in BARE at spots measured off the mockup as the pillar reaches
///     them, then shoot up WITH their tails (star 2, the low one, has no tail).
///
/// Every number in mockup space is in mockup pixels, where the glow is 80 x 111 px -
/// the same size as the Main Glow sprite. Spawned and driven by PlayerWaveManager;
/// it destroys itself when its fade ends.
/// </summary>
public class GateArrivalCapsule : MonoBehaviour
{
    [System.Serializable]
    public class StarSlot
    {
        public string label;
        public Sprite star;
        [Tooltip("Hangs straight down from the star's centre and moves with it. Empty = no tail.")]
        public Sprite tail;
        [Tooltip("Resting spot in mockup px, measured from the capsule's bottom-centre (x right, y up).")]
        public Vector2 mockupPos;
        [Min(0f)] public float delay;
    }

    // The Main Glow sprite is drawn at mockup scale: 80 x 111 px at 100 PPU.
    private const float MockupGlowWidth = 80f;
    private const float MockupGlowHeight = 111f;
    private const float MockupPxToSpriteUnits = 0.01f;

    [SerializeField] private Sprite glowSprite;
    [SerializeField] private StarSlot[] stars;

    [Header("Fit to the stage and hero")]
    [Tooltip("Where the cap's front rim is, as a share of the cap sprite's height from its top " +
             "(Top.png: rim starts at row 56 of 82). The capsule's bottom sits there.")]
    [SerializeField, Range(0f, 1f)] private float rimFromCapTop = 56f / 82f;
    [Tooltip("The capsule's top sits this share of the hero's height below the hero's top. " +
             "0.2 = middle of the head (measured on the mockup).")]
    [SerializeField, Range(0f, 0.6f)] private float headCut = 0.2f;
    [Tooltip("How far the height may stretch away from the mockup's own proportions, so an odd " +
             "hero (tall weapon, tiny body) never gets a silly capsule.")]
    [SerializeField] private Vector2 heightStretchClamp = new Vector2(0.75f, 1.35f);

    [Header("Rounded bottom = the cap's front rim")]
    [Tooltip("The cap's widest row (ellipse equator), as a share of the cap sprite's height from " +
             "its top (Top.png: row 29 of 82). Rim minus equator = the half-ellipse the capsule's " +
             "bottom has to follow.")]
    [SerializeField, Range(0f, 1f)] private float capEquatorFromTop = 29f / 82f;
    [Tooltip("Depth of Main Glow's rounded bottom in sprite px (edge row 88 -> centre row 110). " +
             "Must match the sprite's bottom slice border (23 px), which is drawn unstretched.")]
    [SerializeField, Min(1f)] private float glowCurvePx = 22f;

    [Header("Hero shadow while in the capsule")]
    [Tooltip("Width of the hero's shadow while it stands in the capsule, as a share of the cap " +
             "width (mockup: 46 px of 80).")]
    [SerializeField, Range(0f, 1f)] private float shadowWidthOfCap = 0.57f;
    [Tooltip("The shadow eases back to its own size over this window (s) - around the moment " +
             "the hero leaps off (deployGateHold 0.5 / reinforcementGateHold 0.6), not at the " +
             "end of the effect, which outlives the stand.")]
    [SerializeField] private Vector2 shadowRestoreWindow = new Vector2(0.4f, 0.55f);

    [Header("Glow = a pillar of light shooting up (Reference videos/VFX ANIM, 0.25-0.6 s)")]
    [Tooltip("The glow shoots up out of the cap - stub to full height, fading in - in this time.")]
    [SerializeField, Min(0.01f)] private float shootUp = 0.14f;
    [Tooltip("Height the glow starts from, as a share of its full height.")]
    [SerializeField, Range(0f, 1f)] private float startHeight = 0.15f;
    [Tooltip("Overshoot at the end of the shoot-up (1.08 = 8% taller for a beat).")]
    [SerializeField, Min(1f)] private float overshoot = 1.08f;
    [Tooltip("While it stands, the top flickers up and down by this share of its height " +
             "(the flame look of the reference).")]
    [SerializeField, Range(0f, 0.3f)] private float flicker = 0.06f;
    [Tooltip("Flicker speed (changes per second).")]
    [SerializeField, Min(0f)] private float flickerRate = 14f;
    [Tooltip("When the pillar lifts off the cap and starts travelling up like a flame.")]
    [SerializeField, Min(0f)] private float riseStart = 0.22f;
    [Tooltip("How far the BOTTOM climbs by `end`, in full pillar heights (accelerating).")]
    [SerializeField, Min(0f)] private float riseBottom = 0.4f;
    [Tooltip("How far the TOP climbs by `end`, in full pillar heights. Bigger than riseBottom, " +
             "so the column stretches as it goes - a flame, not a rigid block.")]
    [SerializeField, Min(0f)] private float riseTop = 0.95f;
    [Tooltip("Blasty/CapsuleRiseFade material: while it rises the LOWER parts fade first (a soft, " +
             "wobbling fade line climbs from the bottom). Alpha only - colour untouched. " +
             "Empty = the whole pillar fades evenly.")]
    [SerializeField] private Material riseFadeMaterial;
    [Tooltip("Fade line (uv.y) at riseStart -> at end. Starts below the sprite (nothing faded) " +
             "and climbs to the top as it rises.")]
    [SerializeField] private Vector2 cutRange = new Vector2(-0.55f, 1f);
    [Tooltip("Width share the column thins to as it rises.")]
    [SerializeField, Range(0.3f, 1f)] private float riseThin = 0.75f;
    [Tooltip("Width flicker while rising (share of its width).")]
    [SerializeField, Range(0f, 0.3f)] private float widthFlicker = 0.06f;
    [Tooltip("Side-to-side sway while rising, as a share of the cap width.")]
    [SerializeField, Range(0f, 0.3f)] private float sway = 0.05f;
    [Tooltip("When the whole-pillar fade-out starts (the bottom-first fade runs from riseStart).")]
    [SerializeField, Min(0f)] private float fadeStart = 0.72f;
    [Tooltip("Gone; the capsule destroys itself.")]
    [SerializeField, Min(0.05f)] private float end = 0.95f;

    [Header("Stars")]
    [Tooltip("A star pops in (bare, no tail) the moment the shooting pillar reaches its spot.")]
    [SerializeField, Min(0.01f)] private float starPop = 0.12f;
    [Tooltip("How long a star sits on its spot before it shoots up.")]
    [SerializeField, Min(0f)] private float starHold = 0.08f;
    [Tooltip("How long a star takes to shoot up and fade out.")]
    [SerializeField, Min(0.05f)] private float starRiseTime = 0.5f;
    [Tooltip("How far each star shoots up, in mockup px.")]
    [SerializeField, Min(0f)] private float starRiseMockupPx = 60f;
    [Tooltip("Time a tail takes to fade in once its star starts moving.")]
    [SerializeField, Min(0.01f)] private float tailFadeIn = 0.1f;
    [Tooltip("Share of the climb a star stays fully visible before fading out.")]
    [SerializeField, Range(0f, 1f)] private float starHoldShare = 0.5f;

    private static readonly int CutId = Shader.PropertyToID("_Cut");
    private static readonly int FlameTimeId = Shader.PropertyToID("_FlameTime");

    private Transform glowRoot;
    private SpriteRenderer glowSr;
    private MaterialPropertyBlock glowProps;
    private float flickerSeed;
    private Transform[] starRoots;
    private SpriteRenderer[] starSrs, tailSrs;
    private Vector3[] starRest;
    private float[] starIgnite;
    private float sx, sy, glowYScale, glowFullHeight, t;
    private bool playing;

    private SpriteRenderer heroShadow;
    private Vector3 shadowOriginalScale, shadowSmallScale, shadowOriginalPos, shadowLiftedPos;
    private int shadowOriginalOrder, shadowOriginalLayer;
    private bool shadowRestored;

    /// <summary>
    /// Fits the capsule onto <paramref name="cap"/> (the stage's top cap sprite) around a
    /// hero whose sprite bounds top is <paramref name="heroTop"/> and height
    /// <paramref name="heroHeight"/> (heroHeight &lt;= 0 = keep mockup proportions),
    /// then plays it. Sorted just above <paramref name="sortingOrder"/>.
    /// <paramref name="shadow"/> (optional) is the hero's shadow sprite: shrunk to the
    /// mockup's size for the stand, eased back to its own size during the fade.
    /// </summary>
    public void Play(Bounds cap, float heroTop, float heroHeight, int sortingLayerId, int sortingOrder,
                     SpriteRenderer shadow = null)
    {
        Vector3 bottom = new Vector3(cap.center.x, cap.max.y - rimFromCapTop * cap.size.y, transform.position.z);
        transform.position = bottom;

        sx = cap.size.x / (MockupGlowWidth * MockupPxToSpriteUnits);
        sy = sx;
        if (heroHeight > 0f)
        {
            float wanted = heroTop - headCut * heroHeight - bottom.y;
            float natural = MockupGlowHeight * MockupPxToSpriteUnits;
            sy = Mathf.Clamp(wanted / natural, heightStretchClamp.x * sx, heightStretchClamp.y * sx);
        }

        // The glow is drawn SLICED: its rounded bottom (the slice border) is never stretched
        // by the height fit, only scaled so its curve is exactly the cap's front half-ellipse.
        // The straight column above it takes up whatever height is left.
        float capHalfDepth = Mathf.Max(0.001f, (rimFromCapTop - capEquatorFromTop) * cap.size.y);
        glowYScale = capHalfDepth / (glowCurvePx * MockupPxToSpriteUnits);
        float totalHeight = MockupGlowHeight * MockupPxToSpriteUnits * sy;           // world units
        float minHeight = glowSprite ? glowSprite.border.y / glowSprite.pixelsPerUnit : 0f;
        glowFullHeight = Mathf.Max(minHeight, totalHeight / glowYScale);             // sprite units

        if (shadow) TakeShadow(shadow, cap, bottom.y, capHalfDepth, sortingLayerId, sortingOrder);

        Build(sortingLayerId, sortingOrder);
        t = 0f;
        playing = true;
        Apply();
    }

    /// <summary>
    /// Puts the hero's shadow UNDER the capsule for the stand (Arash: the shadow must
    /// never show on top of the effect). Two things make that true:
    ///  - sorting: forced below the hero (and so below the glow, stars and tails);
    ///  - position: shrunk to the mockup size and lifted just enough that its whole
    ///    ellipse sits inside the glow's rounded bottom. The hero's feet stand on the
    ///    rim, so at its own spot the lower half of the shadow hung out BELOW the
    ///    capsule, over the rim, and read as drawn on top of the effect.
    /// Everything is put back over shadowRestoreWindow (and in OnDestroy).
    /// </summary>
    private void TakeShadow(SpriteRenderer shadow, Bounds cap, float rimY, float capHalfDepth, int layer, int order)
    {
        heroShadow = shadow;
        var tr = shadow.transform;
        shadowOriginalScale = tr.localScale;
        shadowOriginalPos = tr.localPosition;
        shadowOriginalOrder = shadow.sortingOrder;
        shadowOriginalLayer = shadow.sortingLayerID;

        float k = shadowWidthOfCap * cap.size.x / Mathf.Max(0.001f, shadow.bounds.size.x);
        shadowSmallScale = shadowOriginalScale * k;

        // Where the glow's bottom curve is at the shrunk shadow's left/right edge.
        float u = Mathf.Clamp01(shadowWidthOfCap);                              // shadow half-width / cap half-width
        float curveRise = capHalfDepth * (1f - Mathf.Sqrt(1f - u * u));
        float smallHalfH = shadow.bounds.extents.y * k;
        float lift = Mathf.Max(0f, rimY + curveRise + 0.01f + smallHalfH - shadow.bounds.center.y);
        Vector3 localLift = tr.parent ? tr.parent.InverseTransformVector(Vector3.up * lift) : Vector3.up * lift;
        shadowLiftedPos = shadowOriginalPos + localLift;

        shadow.sortingLayerID = layer;
        shadow.sortingOrder = Mathf.Min(shadowOriginalOrder, order - 1);
        shadowRestored = false;
    }

    private void RestoreShadow()
    {
        if (shadowRestored || !heroShadow) return;
        var tr = heroShadow.transform;
        tr.localScale = shadowOriginalScale;
        tr.localPosition = shadowOriginalPos;
        heroShadow.sortingLayerID = shadowOriginalLayer;
        heroShadow.sortingOrder = shadowOriginalOrder;
        shadowRestored = true;
    }

    private void Build(int layer, int order)
    {
        // Glow root on the rim, sprite above it. Apply() stretches it UP out of the cap by
        // its sliced height, so the rounded bottom stays exactly on the rim throughout.
        glowRoot = new GameObject("Glow").transform;
        glowRoot.SetParent(transform, false);
        glowRoot.localScale = new Vector3(sx, glowYScale, 1f);
        var glow = new GameObject("Main Glow");
        glow.transform.SetParent(glowRoot, false);
        glowSr = glow.AddComponent<SpriteRenderer>();
        glowSr.sprite = glowSprite;
        if (riseFadeMaterial) glowSr.sharedMaterial = riseFadeMaterial;
        glowProps = new MaterialPropertyBlock();
        glowSr.drawMode = SpriteDrawMode.Sliced;
        glowSr.sortingLayerID = layer;
        glowSr.sortingOrder = order + 1;
        flickerSeed = Random.Range(0f, 100f);   // no two capsules flicker in step

        int n = stars != null ? stars.Length : 0;
        starRoots = new Transform[n];
        starSrs = new SpriteRenderer[n];
        tailSrs = new SpriteRenderer[n];
        starRest = new Vector3[n];
        starIgnite = new float[n];
        for (int i = 0; i < n; i++)
        {
            var s = stars[i];
            var root = new GameObject(string.IsNullOrEmpty(s.label) ? "Star" : s.label).transform;
            root.SetParent(transform, false);
            root.localScale = Vector3.one * sx;     // stars never squash, even if the glow stretches
            starRest[i] = new Vector3(s.mockupPos.x * MockupPxToSpriteUnits * sx,
                                      s.mockupPos.y * MockupPxToSpriteUnits * sy, 0f);
            starRoots[i] = root;
            // Lit when the shooting pillar's top gets to its height.
            starIgnite[i] = ShootTimeAt(Mathf.Clamp01(s.mockupPos.y / MockupGlowHeight)) + s.delay;

            if (s.tail)
            {
                // Tail's top on the star's centre, hanging straight down behind it.
                var tail = new GameObject("Tail").AddComponent<SpriteRenderer>();
                tail.transform.SetParent(root, false);
                tail.sprite = s.tail;
                tail.transform.localPosition = new Vector3(0f, -s.tail.bounds.extents.y + s.tail.bounds.center.y, 0f);
                tail.sortingLayerID = layer;
                tail.sortingOrder = order + 2;
                tailSrs[i] = tail;
            }
            var star = new GameObject("Star").AddComponent<SpriteRenderer>();
            star.transform.SetParent(root, false);
            star.sprite = s.star;
            star.sortingLayerID = layer;
            star.sortingOrder = order + 3;
            starSrs[i] = star;
        }
    }

    // Longest step one frame may advance the effect. The FIRST heroes of a battle all
    // appear on the same frame (several heroes + capsules instantiated at once), and the
    // long frame that causes would otherwise jump t past most of the 1.3s burn - the
    // capsule then reads as missing on the first appearance only (Arash, 2026-10-05).
    private const float MaxStep = 1f / 30f;

    private void Update()
    {
        if (!playing) return;
        t += Mathf.Min(Time.deltaTime, MaxStep);
        Apply();
        if (t >= end) Destroy(gameObject);
    }

    // Destroyed early (scene change, hero removed) - never leave a hero with a tiny shadow.
    private void OnDestroy()
    {
        RestoreShadow();
    }

    private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);

    /// <summary>Seconds into the shoot-up at which the pillar's top reaches height v (share of full height).</summary>
    private float ShootTimeAt(float v)
    {
        float e = Mathf.Clamp01((v - startHeight) / Mathf.Max(0.0001f, overshoot - startHeight));
        return shootUp * (1f - Mathf.Pow(1f - e, 1f / 3f));               // inverse of EaseOutCubic
    }

    private void Apply()
    {
        // ---- Glow: a pillar of light shooting up, then rising away like a flame -------
        // 1. shoot-up: stub -> full height out of the cap (EaseOutCubic + a small
        //    overshoot), fading in - the reference's landing pillar;
        // 2. rise: it lifts off the cap and keeps travelling up, accelerating towards the
        //    shoot-up's speed. NOT as a rigid block: the top outruns the bottom so the
        //    column stretches, it thins, its height and width flicker and it sways;
        // 3. it fades out on the way up.
        // Only the glow's size/position and alpha move - the sprite's colour and its
        // sliced rounded bottom are never altered.
        float s = Mathf.Clamp01(t / shootUp);
        float height = s < 1f
            ? Mathf.Lerp(startHeight, overshoot, EaseOutCubic(s))                       // shoots past full height ...
            : Mathf.Lerp(overshoot, 1f, EaseOutCubic(Mathf.Clamp01((t - shootUp) / shootUp)));  // ... and settles back
        if (s >= 1f)
        {
            // Flame flicker (two noise rates so it never looks like a regular bounce).
            float n = Mathf.PerlinNoise(flickerSeed, t * flickerRate) * 0.7f
                    + Mathf.PerlinNoise(flickerSeed + 31f, t * flickerRate * 2.3f) * 0.3f;
            height *= 1f + (n - 0.5f) * 2f * flicker;
        }

        // Rise: e accelerates 0 -> 1 (ease-in), so it lifts off gently and speeds up.
        float p = Mathf.Clamp01((t - riseStart) / Mathf.Max(0.0001f, end - riseStart));
        float e = p * p;
        float fullWorldH = glowFullHeight * glowYScale;
        float lift = riseBottom * fullWorldH * e;                       // bottom climbs ...
        height *= 1f + (riseTop - riseBottom) * e;                      // ... the top climbs faster

        float width = Mathf.Lerp(1f, riseThin, e);
        float swayX = 0f;
        if (p > 0f)
        {
            width *= 1f + (Mathf.PerlinNoise(flickerSeed + 57f, t * flickerRate * 1.3f) - 0.5f) * 2f * widthFlicker;
            swayX = (Mathf.PerlinNoise(flickerSeed + 91f, t * flickerRate * 0.5f) - 0.5f) * 2f
                    * sway * MockupGlowWidth * MockupPxToSpriteUnits * sx * p;
        }

        float f = Mathf.Clamp01((t - fadeStart) / Mathf.Max(0.0001f, end - fadeStart));

        float minH = glowSprite.border.y / glowSprite.pixelsPerUnit;
        float h = Mathf.Max(minH, glowFullHeight * height);
        glowSr.size = new Vector2(MockupGlowWidth * MockupPxToSpriteUnits, h);
        glowSr.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
        glowRoot.localScale = new Vector3(sx * width, glowYScale, 1f);
        glowRoot.localPosition = new Vector3(swayX, lift, 0f);
        SetAlpha(glowSr, Mathf.Clamp01(s * 2.5f) * (1f - f * f));

        // Bottom-first fade: the higher it has risen, the more of its lower part is gone.
        if (riseFadeMaterial)
        {
            glowSr.GetPropertyBlock(glowProps);
            glowProps.SetFloat(CutId, Mathf.Lerp(cutRange.x, cutRange.y, p));
            glowProps.SetFloat(FlameTimeId, t + flickerSeed);
            glowSr.SetPropertyBlock(glowProps);
        }

        if (heroShadow && !shadowRestored)
        {
            float r = Mathf.InverseLerp(shadowRestoreWindow.x, Mathf.Max(shadowRestoreWindow.x + 0.0001f, shadowRestoreWindow.y), t);
            if (r >= 1f) RestoreShadow();
            else
            {
                heroShadow.transform.localScale = Vector3.Lerp(shadowSmallScale, shadowOriginalScale, r);
                heroShadow.transform.localPosition = Vector3.Lerp(shadowLiftedPos, shadowOriginalPos, r);
            }
        }

        // ---- Stars -----------------------------------------------------------------
        // Each pops in BARE (no tail) the moment the shooting pillar reaches its spot, sits
        // there a beat, then shoots straight up - quick, accelerating - growing its tail
        // under it, and fades out near the top of its climb.
        float rise = starRiseMockupPx * MockupPxToSpriteUnits * sy;
        for (int i = 0; i < starRoots.Length; i++)
        {
            float pop = Mathf.Clamp01((t - starIgnite[i]) / starPop);
            starRoots[i].localScale = Vector3.one * (sx * (pop > 0f ? EaseOutBack(pop) : 0f));

            float moveStart = starIgnite[i] + starPop + starHold;
            float k = Mathf.Clamp01((t - moveStart) / starRiseTime);
            starRoots[i].localPosition = starRest[i] + Vector3.up * (rise * k * k);

            float fadeOut = 1f - Mathf.Clamp01((k - starHoldShare) / Mathf.Max(0.0001f, 1f - starHoldShare));
            float a = Mathf.Clamp01(pop * 2f) * fadeOut;
            SetAlpha(starSrs[i], a);
            if (tailSrs[i])
                SetAlpha(tailSrs[i], a * Mathf.Clamp01((t - moveStart) / tailFadeIn));
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor-only: freezes a played capsule at <paramref name="time"/> s, for edit-mode previews.</summary>
    public void PreviewAt(float time)
    {
        playing = false;
        t = time;
        Apply();
    }
#endif

    private static float EaseOutBack(float x)
    {
        const float c1 = 1.2f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    private static void SetAlpha(SpriteRenderer sr, float a)
    {
        var c = sr.color;
        c.a = a;
        sr.color = c;
    }
}
