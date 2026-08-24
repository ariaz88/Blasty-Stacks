using UnityEngine;

/// <summary>
/// Built-in ParticleSystem backend for the summon arrival - the one that runs
/// everywhere, including Android devices with no compute-shader support where
/// VFX Graph renders nothing at all.
///
/// It builds its four systems in code on Awake (trail / flash / ring / pillar)
/// so the effect needs no authored prefab and no imported art. See
/// <see cref="SummonVfxAssets"/> for where the textures and material come from.
///
/// Shape of one arrival, measured off the reference clip at 30fps:
///   trail   - runs the whole jump, warm ribbon following the unit
///   flash   - 0.12s white ellipse, punched on the landing frame
///   ring    - 0.25s bright annulus expanding outward along the ground
///   pillar  - ~0.34s column of upward streaks, ~1 cell wide, 2.5-3 cells tall
/// </summary>
[DisallowMultipleComponent]
public class SummonEmitterParticles : MonoBehaviour, ISummonEmitter
{
    [Header("Trail (while airborne)")]
    [SerializeField, Min(0f)] private float trailRate = 55f;
    [SerializeField, Min(0.01f)] private float trailLifetime = 0.15f;
    [SerializeField, Min(0.01f)] private float trailSize = 0.26f;

    [Header("Landing flash")]
    [SerializeField, Min(0.01f)] private float flashLifetime = 0.12f;
    [Tooltip("Flash diameter as a multiple of pillar radius.")]
    [SerializeField, Min(0.1f)] private float flashSizeMul = 2.8f;

    [Header("Ground ring")]
    [SerializeField, Min(0.01f)] private float ringLifetime = 0.25f;
    [SerializeField, Min(0.1f)] private float ringStartMul = 0.6f;
    [SerializeField, Min(0.1f)] private float ringEndMul = 2.2f;
    [Tooltip("Vertical squash. The board is viewed at a tilt, so a perfect circle " +
             "reads as a hoop standing up in the air instead of a ring lying on the ground.")]
    [SerializeField, Range(0.1f, 1f)] private float ringFlatten = 0.45f;

    [Header("Pillar")]
    [SerializeField, Min(1)] private int pillarCount = 42;
    [Tooltip("How long the column keeps FEEDING from the ground. Emitting the whole " +
             "count in one burst instead makes the cloud drift upward as a puff and " +
             "tear away from the unit's feet - it has to be a jet, not a burst.")]
    [SerializeField, Min(0.02f)] private float pillarJetDuration = 0.22f;
    [SerializeField, Min(0.01f)] private float pillarLifeMin = 0.20f;
    [SerializeField, Min(0.01f)] private float pillarLifeMax = 0.30f;
    [Tooltip("Sideways turbulence. This is what tears the top edge of the column " +
             "into flame-like tongues instead of a clean gradient.")]
    [SerializeField, Min(0f)] private float pillarNoise = 0.25f;
    [Tooltip("Vertical streak length. 0 = round blobs, higher = tall thin licks.")]
    [SerializeField, Min(0f)] private float pillarStretch = 2.6f;

    [Header("Sorting (URP 2D renderer)")]
    [Tooltip("The ParticleSystem backend DOES respect sorting layers - unlike the " +
             "VFX Graph one, which sorts by Z only.")]
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 50;

    private ParticleSystem _trail, _flash, _ring, _pillar;
    private Transform _follow;
    private bool _built;

    public GameObject Owner => gameObject;

    /// <summary>
    /// Busy while the unit is still airborne, or while any particle is alive.
    /// The director will not reclaim this emitter until both are false, so a
    /// pillar is never cut off mid-fade by a recycle.
    /// </summary>
    public bool IsBusy =>
        _follow != null ||
        (_trail != null && _trail.IsAlive(true)) ||
        (_flash != null && _flash.IsAlive(true)) ||
        (_ring != null && _ring.IsAlive(true)) ||
        (_pillar != null && _pillar.IsAlive(true));

    private void Awake() => Build();

    private void LateUpdate()
    {
        // Ride the unit while it is in the air. World simulation space means the
        // particles already emitted stay put, so moving the emitter draws the
        // arc as a ribbon behind the unit - which is exactly the curved trail in
        // the reference.
        if (_follow != null) transform.position = _follow.position;
    }

    // ---------------- ISummonEmitter ----------------

    public void BeginTrail(Transform follow, Color tint)
    {
        if (follow == null) return;
        Build();

        _follow = follow;
        transform.position = follow.position;

        var main = _trail.main;
        main.startColor = tint;

        _trail.Play();
    }

    public void EndTrail()
    {
        _follow = null;

        // StopEmitting, not Clear: the ribbon already in the air has to fade on
        // its own. Clearing it makes the trail vanish on the landing frame, which
        // reads as a bug rather than an effect.
        if (_trail != null)
            _trail.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void PlayBurst(in SummonBurstParams p)
    {
        Build();

        _follow = null;
        transform.position = p.position;

        Color tint = p.tint * Mathf.Max(0f, p.intensity);
        tint.a = p.tint.a;

        float radius = Mathf.Max(0.01f, p.radius);
        float height = Mathf.Max(0.01f, p.height);

        // --- flash ---
        var fMain = _flash.main;
        fMain.startColor = tint;
        fMain.startSize = radius * flashSizeMul;
        _flash.Play();

        // --- ground ring ---
        var rMain = _ring.main;
        rMain.startColor = tint;
        rMain.startSizeX = radius * ringStartMul;
        rMain.startSizeY = radius * ringStartMul * ringFlatten;
        var rSize = _ring.sizeOverLifetime;
        rSize.size = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.01f, ringEndMul / Mathf.Max(0.01f, ringStartMul)),
            AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));
        _ring.Play();

        // --- pillar ---
        var pMain = _pillar.main;
        pMain.startColor = tint;
        pMain.startLifetime = new ParticleSystem.MinMaxCurve(pillarLifeMin, pillarLifeMax);

        pMain.startSize = new ParticleSystem.MinMaxCurve(radius * 0.35f, radius * 0.7f);

        // Rise speed is derived from the height we were asked for, so the column
        // tops out where the caller wants rather than at whatever a hardcoded
        // speed happens to reach. The lifetime spread then makes some streaks
        // overshoot and some fall short - that is the torn top edge.
        float avgLife = Mathf.Max(0.01f, (pillarLifeMin + pillarLifeMax) * 0.5f);
        var pVel = _pillar.velocityOverLifetime;
        // Kept tight: the reference column is barely wider than one cell, and a
        // wider spread turns it into a bonfire rather than a beam.
        pVel.x = new ParticleSystem.MinMaxCurve(-radius * 0.25f, radius * 0.25f);
        pVel.y = new ParticleSystem.MinMaxCurve(height / avgLife * 0.75f,
                                                height / avgLife * 1.25f);
        pVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var pShape = _pillar.shape;
        pShape.radius = radius;

        var pNoise = _pillar.noise;
        pNoise.enabled = pillarNoise > 0f;
        pNoise.strength = pillarNoise;

        var pEmission = _pillar.emission;
        pEmission.rateOverTime = pillarCount / Mathf.Max(0.02f, pillarJetDuration);

        // Play(), not Emit(): the system feeds continuously for its duration so
        // the foot of the column stays lit at the unit's feet while the head
        // climbs. Emit() would launch every particle on the same frame and the
        // whole column would peel off the ground together.
        _pillar.Play();
    }

    public void ResetForReuse()
    {
        _follow = null;

        if (_trail != null) { _trail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        if (_flash != null) { _flash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        if (_ring != null) { _ring.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        if (_pillar != null) { _pillar.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
    }

    // ---------------- construction ----------------

    private void Build()
    {
        if (_built) return;
        _built = true;

        _trail = BuildTrail();
        _flash = BuildFlash();
        _ring = BuildRing();
        _pillar = BuildPillar();
    }

    private ParticleSystem NewSystem(string childName, Material mat, ParticleSystemRenderMode mode)
    {
        var go = new GameObject(childName);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        // World space is what lets the trail stay behind the moving emitter.
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = false;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = mat;
        r.renderMode = mode;
        r.sortingLayerName = sortingLayer;
        r.sortingOrder = sortingOrder;
        r.alignment = ParticleSystemRenderSpace.World;

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }

    private ParticleSystem BuildTrail()
    {
        var ps = NewSystem("Trail", SummonVfxAssets.GlowMaterial, ParticleSystemRenderMode.Billboard);

        var main = ps.main;
        main.loop = true;               // runs for as long as the jump lasts
        main.duration = 5f;
        main.startLifetime = trailLifetime;
        main.startSpeed = 0f;
        main.startSize = trailSize;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.rateOverTime = trailRate;

        FadeOutOverLife(ps);
        ShrinkOverLife(ps, 1f, 0f);
        return ps;
    }

    private ParticleSystem BuildFlash()
    {
        var ps = NewSystem("Flash", SummonVfxAssets.GlowMaterial, ParticleSystemRenderMode.Billboard);

        var main = ps.main;
        main.duration = 1f;
        main.startLifetime = flashLifetime;
        main.startSpeed = 0f;
        main.maxParticles = 4;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        FadeOutOverLife(ps);
        // Punches outward fast then dies - the "hit" of the landing.
        ShrinkOverLife(ps, 0.6f, 1.4f);
        return ps;
    }

    private ParticleSystem BuildRing()
    {
        var ps = NewSystem("GroundRing", SummonVfxAssets.RingMaterial, ParticleSystemRenderMode.Billboard);

        var main = ps.main;
        main.duration = 1f;
        main.startLifetime = ringLifetime;
        main.startSpeed = 0f;
        main.maxParticles = 4;
        // Non-uniform start size is the only way to get an ellipse out of a
        // billboard; sizeOverLifetime then scales both axes together and keeps
        // the squash while it expands.
        main.startSize3D = true;

        var emission = ps.emission;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        FadeOutOverLife(ps);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        return ps;
    }

    private ParticleSystem BuildPillar()
    {
        var ps = NewSystem("Pillar", SummonVfxAssets.GlowMaterial, ParticleSystemRenderMode.Stretch);

        var main = ps.main;
        // duration IS the jet duration: a non-looping system emits for exactly
        // this long and then stops on its own, which is the whole mechanism
        // keeping the column fed from the ground.
        main.duration = pillarJetDuration;
        // All motion comes from velocityOverLifetime below. Leaving startSpeed at
        // a real value would fire the Circle shape radially and flatten the
        // column into a splash.
        main.startSpeed = 0f;
        main.maxParticles = 256;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = pillarCount / Mathf.Max(0.02f, pillarJetDuration);

        // A flat disc at the foot of the unit: every streak starts on the ground
        // and travels straight up, which is what makes it read as a column
        // rather than an explosion.
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radiusThickness = 1f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

        // Straight up in world space, so the column stays vertical no matter how
        // the emitter's parent is oriented.
        //
        // All three axes MUST share one curve mode - mixing a constant X with a
        // curve Y makes Unity reject the whole module with "Particle Velocity
        // curves must all be in the same mode" on every emit. Hence the
        // two-constant range on all three, with the real values written in
        // PlayBurst once the caller's height is known.
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
        vel.y = new ParticleSystem.MinMaxCurve(4f, 8f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Light damping only - it tapers the top, but at 0.35 it also robbed the
        // column of roughly a third of the height the caller asked for.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.dampen = 0.12f;
        limit.limit = new ParticleSystem.MinMaxCurve(12f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = pillarNoise;
        noise.frequency = 2.2f;
        noise.scrollSpeed = 1.4f;
        noise.damping = true;

        FadeOutOverLife(ps);
        ShrinkOverLife(ps, 1f, 0.15f);

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.lengthScale = pillarStretch;
        r.velocityScale = 0.06f;

        // Draw each streak from its position UPWARD instead of centred on it.
        // Without this, half of every particle hangs below the emission point and
        // the foot of the column sinks through the floor the unit is standing on.
        // Positive Y here shifts the quad up - the sign is the opposite of what
        // the name suggests, confirmed by capture.
        r.pivot = new Vector3(0f, 0.5f, 0f);
        return ps;
    }

    /// <summary>Alpha 1 -> 0 with a brief hold, so nothing pops out of existence.</summary>
    private static void FadeOutOverLife(ParticleSystem ps)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;

        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.12f),
                new GradientAlphaKey(0.85f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            });

        col.color = new ParticleSystem.MinMaxGradient(grad);
    }

    private static void ShrinkOverLife(ParticleSystem ps, float from, float to)
    {
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, from, 1f, to));
    }
}
