using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Match-clear shatter effect. Replaces the per-particle GameObject spraying in
/// <see cref="FractureObject"/> with a small pool of mesh-particle systems.
///
/// Everything is built in code, so there is nothing to wire in the Inspector:
/// the shard meshes load from Resources/VFX, the material is created from the
/// Blasty/ShardUnlit shader, and the colour palette is copied off whatever
/// FractureObject is already in the scene (so the colours you tuned there carry over).
///
/// The arc, matched to the Blasty Stacks reference frame by frame:
///
///   1. MatchResolver collapses the block to a small but STILL-VISIBLE nub per cell and
///      destroys it. Nothing here has fired yet.
///   2. Each cell spawns a DENSE CLUMP of shards inside clusterRadiusCells - overlapping,
///      but individually readable. Two cells give two clumps, never one merged cloud.
///   3. The clumps rise and spread, bounded to spreadOutCells sideways and apexHeightCells
///      up. The bound is not tuned by hand: Play() derives gravity and launch speed FROM
///      those two numbers, so the box holds by construction.
///   4. Past the apex they fall fallDepthCells below the cell, shrinking and fading as
///      they go, and die.
/// </summary>
[DisallowMultipleComponent]
public class ShardBurst : MonoBehaviour
{
    public static ShardBurst Instance { get; private set; }

    [Header("Look")]
    [Tooltip("Shards spawned per board cell of the cleared piece.")]
    [SerializeField, Min(1)] private int shardsPerCell = 40;

    [Tooltip("Hard ceiling regardless of how big the cleared group was.")]
    [SerializeField, Min(1)] private int maxShardsPerBurst = 160;

    [Tooltip("Smallest / largest shard, in world units. Board cell is ~1.09.")]
    [SerializeField] private Vector2 shardSizeRange = new Vector2(0.10f, 0.28f);

    // Lifetime is NOT authored. It is solved per shard in Play() from that shard's own launch
    // speed, so every shard dies exactly on the floor of the box no matter how hard it was
    // thrown. See the note there.

    // ---- The arc, in the units the reference is described in --------------------------
    // Everything below is authored in BOARD CELLS and SECONDS, and the Shuriken velocities
    // and gravity are derived from it in Play(). That is deliberate: the reference bounds the
    // spread to a fixed box, and deriving the physics from the box is the only way to hold
    // that bound by construction rather than by trial and error.

    [Header("Arc (board cells / seconds)")]
    [Tooltip("Radius of the dense clump each cell spawns as, before it expands.")]
    [SerializeField, Min(0.01f)] private float clusterRadiusCells = 0.18f;

    [Tooltip("How far sideways a shard may travel from its cell. This is the width of the box.")]
    [SerializeField, Min(0f)] private float spreadOutCells = 1.00f;

    [Tooltip("How high above its cell a shard peaks. This is the top of the box.")]
    [SerializeField, Min(0.01f)] private float apexHeightCells = 1.20f;

    [Tooltip("How far below its cell a shard has fallen when it dies.")]
    [SerializeField, Min(0.01f)] private float fallDepthCells = 2.25f;

    [Tooltip("Seconds from the shards appearing to the last one vanishing. NOTE: apex height, " +
             "fall depth and this duration together FULLY determine gravity and the time to the " +
             "apex - there is no fourth knob, and adding one would let the three disagree.")]
    [SerializeField, Min(0.05f)] private float totalDuration = 0.85f;

    [Header("Sorting")]
    [Tooltip("Board pieces sit at negative orders on the Default layer.")]
    [SerializeField] private string sortingLayer = "Default";
    [SerializeField] private int sortingOrder = 20;

    [Header("Pool")]
    [SerializeField, Min(1)] private int poolSize = 6;

    [Header("Colours")]
    [Tooltip("Indexed by PieceSimple.ColorId. Auto-filled from a FractureObject in the scene if one exists.")]
    [SerializeField]
    private Color[] palette =
    {
        new Color(0.25f, 0.60f, 1.00f), // 0 blue
        new Color(0.90f, 0.20f, 0.28f), // 1 crimson
        new Color(0.35f, 0.80f, 0.30f), // 2 green
        new Color(1.00f, 0.45f, 0.70f), // 3 pink
        new Color(0.95f, 0.35f, 0.62f), // 4 mid pink
        new Color(0.80f, 0.20f, 0.50f), // 5 dark pink
        new Color(0.55f, 0.30f, 0.95f), // 6 purple
        new Color(0.45f, 0.25f, 0.85f), // 7 mid purple
        new Color(1.00f, 0.55f, 0.15f), // 8 orange
        new Color(1.00f, 0.83f, 0.10f)  // 9 yellow
    };

    private readonly List<ParticleSystem> _pool = new();
    private int _next;

    // One material per pooled system, NOT one shared material. The three band colours are
    // material properties (the particle COLOR stream carries only one colour, and we need
    // three), so two bursts of different colours alive at once must not share a material.
    // MaterialPropertyBlock is not an option: the properties live in UnityPerMaterial and
    // the SRP Batcher ignores per-renderer overrides of those.
    private readonly List<Material> _materials = new();

    private Shader _shader;
    private Mesh[] _meshes;

    // Outward impulse per cell of authored spread, calibrated by simulating a burst and
    // measuring where the shards actually end up. Only valid for the limitX / dampen values
    // in BuildSystem - change those and this has to be re-measured. It is linear in the
    // impulse, so one measurement is enough.
    private const float SpreadImpulseGain = 7.5f;

    private static readonly int IdColDark = Shader.PropertyToID("_ColDark");
    private static readonly int IdColBody = Shader.PropertyToID("_ColBody");
    private static readonly int IdColLight = Shader.PropertyToID("_ColLight");

    // ------------------------------------------------------------------
    // Boot
    // ------------------------------------------------------------------

    /// <summary>
    /// Creates the effect automatically on scene load so there is nothing to place by hand.
    /// Drop a ShardBurst component into a scene yourself only if you want to tune the
    /// serialized fields per level.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance) return;
        if (FindObjectOfType<ShardBurst>(true)) return;

        var go = new GameObject("~ShardBurst");
        go.AddComponent<ShardBurst>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
        Initialize();
    }

    /// <summary>
    /// Loads the shard meshes, builds the material and fills the pool. Called from Awake;
    /// safe to call earlier (e.g. on a loading screen) to pre-warm. Idempotent.
    /// </summary>
    public void Initialize()
    {
        if (_pool.Count > 0) return;

        _meshes = Resources.LoadAll<Mesh>("VFX");

        if (_meshes == null || _meshes.Length == 0)
        {
            Debug.LogError("[ShardBurst] No shard meshes found in Resources/VFX. Effect disabled.", this);
            enabled = false;
            return;
        }

        _shader = Shader.Find("Blasty/ShardUnlit");
        if (!_shader)
        {
            Debug.LogError("[ShardBurst] Shader 'Blasty/ShardUnlit' not found. Effect disabled.", this);
            enabled = false;
            return;
        }

        InheritPaletteFromLegacy();

        for (int i = 0; i < poolSize; i++)
            _pool.Add(BuildSystem(i));
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        for (int i = 0; i < _materials.Count; i++)
            if (_materials[i]) Destroy(_materials[i]);
        _materials.Clear();
    }

    /// <summary>
    /// Copies the colours off an existing FractureObject so the burst matches the
    /// tints already tuned in the scene rather than the defaults above.
    /// </summary>
    private void InheritPaletteFromLegacy()
    {
        var legacy = FindObjectOfType<FractureObject>(true);
        if (!legacy) return;

        palette = new[]
        {
            legacy.blueColor,     legacy.crimsonColor, legacy.greenColor,
            legacy.pinkColor,     legacy.midPinkColor, legacy.darkPinkColor,
            legacy.purpleColor,   legacy.midPurpleColor,
            legacy.orangeColor,   legacy.yellowColor
        };

        for (int i = 0; i < palette.Length; i++)
            if (palette[i].a <= 0.01f) palette[i] = legacy.defaultColor;
    }

    // ------------------------------------------------------------------
    // Public entry points
    // ------------------------------------------------------------------

    // Scratch, so a clear allocates nothing.
    private readonly List<Vector3> _singleCell = new(1);

    /// <summary>Kept signature-compatible with FractureObject.Explode.</summary>
    public void Explode(Transform origin, int colorId)
    {
        if (!origin) return;
        ExplodeAtPosition(origin.position, colorId);
    }

    public void ExplodeAtPosition(Vector3 worldPosition, int colorId)
    {
        _singleCell.Clear();
        _singleCell.Add(worldPosition);
        Play(_singleCell, colorId, null);
    }

    /// <summary>
    /// Fires one burst - a dense clump per cell, which then rises, spreads inside a bounded box
    /// and falls. See the class comment for the shape of the arc.
    /// </summary>
    /// <param name="cellCentres">One world position per cleared board cell.</param>
    /// <param name="colorId">PieceSimple.ColorId, used only if <paramref name="bands"/> is null.</param>
    /// <param name="bands">
    /// Bevel / body / highlight sampled off the block's sprite. Pass null to fall back to
    /// the <see cref="palette"/> lookup by colorId - note that colorId COLLIDES across
    /// colours in this project, so that path is a last resort only.
    /// </param>
    public void Play(IReadOnlyList<Vector3> cellCentres, int colorId,
                     PieceTintSampler.TintBands? bands)
    {
        if (!enabled || _pool.Count == 0) return;
        if (cellCentres == null || cellCentres.Count == 0) return;

        int slot = _next;
        var ps = _pool[slot];
        _next = (_next + 1) % _pool.Count;

        float cell = ResolveCellSize();

        var b = bands ?? PieceTintSampler.TintBands.FromBody(ColorFor(colorId));

        var mat = _materials[slot];
        if (mat)
        {
            mat.SetColor(IdColDark, b.dark);
            mat.SetColor(IdColBody, b.body);
            mat.SetColor(IdColLight, b.light);
        }

        // ---- Derive the physics from the authored box -------------------------------
        // Pure ballistics, no drag on any axis, so every bound below is exact rather than
        // tuned. A shard launched at vy under gravity g rises h = vy^2/2g in vy/g seconds,
        // then falls to depth d in sqrt(2(h+d)/g). Requiring rise + fall == totalDuration:
        //
        //     T = sqrt(2h/g) + sqrt(2(h+d)/g) = sqrt(2/g) * (sqrt(h) + sqrt(h+d))
        //  => g = 2 * (sqrt(h) + sqrt(h+d))^2 / T^2
        //
        // So apex height, fall depth and duration pin gravity exactly - which is why
        // riseTime is NOT authored. An earlier version did author it, and the resulting
        // gravity (65 u/s^2) sent the shards 16 cells below the board instead of 2.25.
        float h = apexHeightCells * cell;
        float d = fallDepthCells * cell;
        float T = Mathf.Max(0.05f, totalDuration);

        float sum = Mathf.Sqrt(h) + Mathf.Sqrt(h + d);
        float g = 2f * sum * sum / (T * T);
        float vy = Mathf.Sqrt(2f * g * h);

        var main = ps.main;
        main.gravityModifier = g / Mathf.Max(0.01f, Mathf.Abs(Physics.gravity.y));

        // Horizontal is an IMPULSE that the X/Z drag then spends, rather than a constant
        // drift, so the cloud reaches its full width by the apex instead of still opening as
        // it dies. Drag makes displacement non-analytic, so the gain below is calibrated by
        // measurement against the drag settings in BuildSystem - it is linear in the impulse,
        // so one measurement fixes it. Re-measure if limitX / dampen ever change.
        float vxImpulse = spreadOutCells * cell * SpreadImpulseGain;

        // White, so colorOverLifetime contributes its alpha ramp and nothing else - the
        // shard colour comes from the material bands, not from the particle stream.
        main.startColor = Color.white;

        // ---- Emit ------------------------------------------------------------------
        // Explicit per-particle emission rather than the shape module: it is the only way to
        // get one tight clump per cell AND a per-particle launch vector out of a single system.
        int perCell = Mathf.Max(6, Mathf.Min(shardsPerCell, maxShardsPerBurst / cellCentres.Count));
        float clusterRadius = clusterRadiusCells * cell;

        ps.Clear(true);
        ps.Play(true);

        var ep = new ParticleSystem.EmitParams();

        for (int c = 0; c < cellCentres.Count; c++)
        {
            Vector3 origin = cellCentres[c];
            origin.z = 0f;

            for (int i = 0; i < perCell; i++)
            {
                // Dense disc, not a ring: sqrt keeps the area distribution even.
                Vector2 disc = Random.insideUnitCircle;
                ep.position = origin + new Vector3(disc.x, disc.y, 0f) * clusterRadius;

                // Push every shard OFF CENTRE. A plain Random.Range(-1,1) leaves a fat band of
                // shards near zero that never leave the middle, which is most of why the cloud
                // stayed clumped; remapping the magnitude to [0.4, 1] scatters them properly.
                float side = Random.Range(-1f, 1f);
                side = Mathf.Sign(side) * Mathf.Lerp(0.40f, 1f, Mathf.Abs(side));

                // Wide launch spread, so the cloud breaks up instead of rising as one body.
                // Low-vy shards peak early and low; the per-shard lifetime below still lands
                // every one of them on the floor of the box.
                float vyi = vy * Random.Range(0.55f, 1f);
                ep.velocity = new Vector3(side * vxImpulse, vyi, 0f);

                // Each shard gets exactly the lifetime that lands it on the floor of the box,
                // solved from its OWN launch speed: d = vy*t - g*t^2/2, taking the positive root.
                // Without this, a slower shard peaks lower, therefore falls for longer, and ends
                // up well below the others - measured at -3.5 cells against a -2.25 target.
                ep.startLifetime = (vyi + Mathf.Sqrt(vyi * vyi + 2f * g * d)) / g;
                ep.startSize = Random.Range(shardSizeRange.x, shardSizeRange.y);
                ep.rotation3D = new Vector3(Random.Range(0f, 360f),
                                            Random.Range(0f, 360f),
                                            Random.Range(0f, 360f));
                ep.startColor = Color.white;

                ps.Emit(ep, 1);
            }
        }
    }

    private Color ColorFor(int id)
    {
        if (palette == null || palette.Length == 0) return Color.white;
        return palette[Mathf.Clamp(id, 0, palette.Length - 1)];
    }

    private float ResolveCellSize()
    {
        var board = FindObjectOfType<BoardGridXY>();
        return board ? board.CellSize : 1.086f;
    }

    // ------------------------------------------------------------------
    // System construction
    // ------------------------------------------------------------------

    private ParticleSystem BuildSystem(int index)
    {
        var go = new GameObject("ShardBurst_" + index);
        go.transform.SetParent(transform, false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        // Everything per-particle (lifetime, speed, size, rotation) is supplied through
        // EmitParams in Play(). These are only sane fallbacks if something ever emits without.
        main.startLifetime = new ParticleSystem.MinMaxCurve(totalDuration);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
        main.startSize = new ParticleSystem.MinMaxCurve(shardSizeRange.x, shardSizeRange.y);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxShardsPerBurst + 40;
        main.startColor = Color.white;

        // No automatic burst and no shape: Play() emits every particle explicitly so it can
        // place one clump per board cell and give each shard its own launch vector.
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;
        shape.enabled = false;

        // Drag on the SIDEWAYS axes only, so the outward impulse Play() gives each shard is
        // spent fast: the cloud opens to its full width by the apex and then coasts, which is
        // what the reference shows. Without this the spread is linear in time and the cloud is
        // still a tight blob at the top of the arc.
        //
        // !! Y is left effectively unlimited ON PURPOSE. Play() derives gravity and launch
        // !! speed analytically for the vertical arc, and damping Y invalidates that maths.
        // !! The original version damped all axes at once, which flattened the whole arc into
        // !! a cloud that stalled in place and sank.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.separateAxes = true;
        limit.limitX = new ParticleSystem.MinMaxCurve(0.25f);
        limit.limitY = new ParticleSystem.MinMaxCurve(10000f);
        limit.limitZ = new ParticleSystem.MinMaxCurve(0.25f);
        limit.dampen = 0.28f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-480f * Mathf.Deg2Rad, 480f * Mathf.Deg2Rad);
        rot.y = new ParticleSystem.MinMaxCurve(-480f * Mathf.Deg2Rad, 480f * Mathf.Deg2Rad);
        rot.z = new ParticleSystem.MinMaxCurve(-700f * Mathf.Deg2Rad, 700f * Mathf.Deg2Rad);

        // Full size through the clump and the whole rise, so the shards stay chunky and
        // readable while they are inside the box. Only the fall tapers them - that is the
        // "getting smaller on the way down" of the last reference frame.
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.45f, 1f, 0f, 0f),
            new Keyframe(0.80f, 0.66f, -1.1f, -1.1f),
            new Keyframe(1f, 0f, -1.8f, 0f));

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        // Colour stays put across the whole life. An earlier version cooled toward grey at
        // the tail, which was invisible at the old 0.42s lifetime but reads as dusty and
        // washed-out over the current ~0.95s - and it fights the exact sprite tint.
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                // Fully opaque through the clump, the rise and the apex; the fade belongs to
                // the fall. Fading at 0.40 made the cloud dissolve at the top of the arc.
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Modules deliberately left off: noise reads as smoke, collision is the most
        // expensive Shuriken module on mobile, and trails/sub-emitters double draw calls.
        var noise = ps.noise; noise.enabled = false;
        var collision = ps.collision; collision.enabled = false;
        var trails = ps.trails; trails.enabled = false;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh;
        r.SetMeshes(_meshes, _meshes.Length);
        r.alignment = ParticleSystemRenderSpace.World;
        r.sortMode = ParticleSystemSortMode.None;
        var mat = new Material(_shader) { name = "M_Shard_" + index + " (runtime)" };
        _materials.Add(mat);
        r.sharedMaterial = mat;

        r.sortingLayerName = sortingLayer;
        r.sortingOrder = sortingOrder;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.enableGPUInstancing = false; // custom shader has no instancing path yet

        return ps;
    }
}
