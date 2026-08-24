using UnityEngine;

/// <summary>
/// The ground telegraph of a summon arrival: a filled white ellipse that pops in
/// just BEFORE the unit touches down, holds, then hollows out from the centre
/// into a ring, expands slightly and fades.
///
/// Measured off the reference clip at 30fps (the bee summon, which lands on f17):
///   f16       filled ellipse pops in - the unit is STILL IN THE AIR
///   f17-f19   holds as a solid filled disc          (~0.13s)
///   f20-f21   centre opens out into a ring          (~0.07s)
///   f21-f25   ring expands and fades                (~0.15s)
///   f26       gone                                  (~0.30s total)
///
/// It is NOT part of ISummonEmitter. That interface covers the LANDING burst;
/// this fires earlier, on its own clock, and has to look identical whichever
/// emitter backend is live - so SummonVfxDirector owns it separately and
/// SummonArrivalBinder triggers it from the jump's remaining time.
///
/// The disc-to-ring morph is one animated value: the shader's _InnerRadius,
/// 0 = filled disc, ~0.8 = thin ring. There is no separate "disc" state.
/// </summary>
[DisallowMultipleComponent]
public class SummonGroundCircle : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int InnerRadiusId = Shader.PropertyToID("_InnerRadius");
    private static readonly int OuterRadiusId = Shader.PropertyToID("_OuterRadius");
    private static readonly int EdgeId = Shader.PropertyToID("_Edge");
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    [Header("Timing (seconds) - reference totals ~0.30s")]
    [Tooltip("How long it stays a SOLID filled disc before the centre opens.")]
    [SerializeField, Min(0f)] private float fillHold = 0.13f;

    [Tooltip("How long the centre takes to open out from filled disc to ring.")]
    [SerializeField, Min(0.01f)] private float openDuration = 0.07f;

    [Tooltip("How long the finished ring takes to expand and fade away.")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.15f;

    [Header("Shape")]
    [Tooltip("How far the hole opens. 0 = never opens, 0.8 = thin ring.")]
    [SerializeField, Range(0f, 0.95f)] private float innerRadiusMax = 0.78f;

    [Tooltip("Outer growth across the whole life. The reference barely grows - " +
             "about 1.3x - so this is deliberately much smaller than the landing " +
             "burst's ring.")]
    [SerializeField, Min(1f)] private float endScale = 1.3f;

    [Tooltip("Vertical squash. The board is viewed at a tilt, so a true circle " +
             "reads as a hoop standing up in the air rather than lying on the ground.")]
    [SerializeField, Range(0.1f, 1f)] private float flatten = 0.45f;

    [Tooltip("Edge softness in the shader. Low = crisp rim, high = glow.")]
    [SerializeField, Range(0.001f, 0.5f)] private float edgeSoftness = 0.14f;

    [Header("Sorting (URP 2D renderer)")]
    [Tooltip("A MeshRenderer sorts by layer and order in the 2D renderer just like a SpriteRenderer. Kept just under the landing burst so the telegraph sits beneath the flame.")]
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 45;

    [Header("Fade shape")]
    [Tooltip("Alpha across the FADE phase only. The disc and open phases stay " +
             "at full alpha - in the reference the disc never dims while filled.")]
    [SerializeField]
    private AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    private MeshRenderer _renderer;
    private MaterialPropertyBlock _mpb;

    private float _t = -1f;          // < 0 means idle
    private float _total;
    private float _baseDiameter;
    private Color _tint = Color.white;

    /// <summary>True while the telegraph is mid-animation.</summary>
    public bool IsPlaying => _t >= 0f;

    private void Awake() => Build();

    /// <summary>
    /// Starts the telegraph at a world point. <paramref name="diameter"/> is the
    /// FILLED disc's width in world units; the ring grows from there by endScale.
    /// </summary>
    public void Play(Vector3 worldPos, float diameter, Color tint)
    {
        Build();

        transform.position = worldPos;
        _baseDiameter = Mathf.Max(0.01f, diameter);
        _tint = tint;

        _total = fillHold + openDuration + fadeDuration;
        _t = 0f;

        gameObject.SetActive(true);
        Apply(0f);
    }

    /// <summary>
    /// Jumps to an absolute time inside this telegraph's own ~0.35s animation and
    /// applies that frame immediately, the way Animation.time does. <see cref="Play"/>
    /// must have run first.
    ///
    /// Exists so the effect can be SCRUBBED - stepped through frame by frame in
    /// the editor to compare against the reference clip - without having to catch
    /// it live at 30fps.
    /// </summary>
    public void Seek(float t)
    {
        if (_t < 0f) return;

        _t = Mathf.Clamp(t, 0f, _total);
        Apply(_t);
    }

    /// <summary>Stops immediately and hides. Used when the pool reclaims it.</summary>
    public void StopAndHide()
    {
        _t = -1f;
        if (gameObject != null) gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_t < 0f) return;

        _t += Time.deltaTime;

        if (_t >= _total)
        {
            StopAndHide();
            return;
        }

        Apply(_t);
    }

    /// <summary>
    /// Maps elapsed time onto the three phases. Written as explicit phases rather
    /// than one blended curve because the reference's disc holds at FULL alpha and
    /// zero hole for a clearly readable beat before anything moves - a single
    /// eased curve smears that beat away and the telegraph stops reading as a
    /// deliberate "it lands HERE".
    /// </summary>
    private void Apply(float t)
    {
        float inner;
        float alpha;
        float growth01;

        if (t < fillHold)
        {
            // Phase 1 - solid filled disc, full alpha, no hole.
            inner = 0f;
            alpha = 1f;
            growth01 = 0f;
        }
        else if (t < fillHold + openDuration)
        {
            // Phase 2 - the centre opens outward.
            float k = (t - fillHold) / openDuration;
            inner = Mathf.SmoothStep(0f, innerRadiusMax, k);
            alpha = 1f;
            growth01 = k * 0.35f;   // barely grows while opening
        }
        else
        {
            // Phase 3 - finished ring expands the rest of the way and fades.
            float k = (t - fillHold - openDuration) / fadeDuration;
            inner = innerRadiusMax;
            alpha = Mathf.Clamp01(fadeCurve.Evaluate(k));
            growth01 = 0.35f + k * 0.65f;
        }

        float scale = _baseDiameter * Mathf.Lerp(1f, endScale, growth01);
        transform.localScale = new Vector3(scale, scale * flatten, 1f);

        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetColor(ColorId, _tint);
        _mpb.SetFloat(InnerRadiusId, inner);
        _mpb.SetFloat(OuterRadiusId, 0.9f);
        _mpb.SetFloat(EdgeId, edgeSoftness);
        _mpb.SetFloat(AlphaId, alpha);
        _renderer.SetPropertyBlock(_mpb);
    }

    // ---------------- construction ----------------

    private void Build()
    {
        if (_renderer != null) return;

        var mf = gameObject.GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = SummonVfxAssets.UnitQuad;

        _renderer = gameObject.GetComponent<MeshRenderer>();
        if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();

        _renderer.sharedMaterial = SummonVfxAssets.GroundCircleMaterial;
        _renderer.sortingLayerName = sortingLayer;
        _renderer.sortingOrder = sortingOrder;
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        _mpb = new MaterialPropertyBlock();
    }
}
