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
/// Timing is taken frame-by-frame from Assets/Arts/Reference videos/Stack movement.mp4:
/// the block collapses for 100 ms, then a dense cloud lives ~370 ms, barely travels,
/// and sinks about one cell.
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

    [Tooltip("Lifetime range. Upper bound sets the length of the falling / fading tail.")]
    [SerializeField] private Vector2 lifetimeRange = new Vector2(0.80f, 0.95f);

    [Tooltip("Initial outward speed. Damping stalls this within ~100 ms.")]
    [SerializeField] private Vector2 speedRange = new Vector2(1.6f, 3.8f);

    [Tooltip("Tuned so the longer lifetime still lands the cloud ~2 cells down rather than off-board.")]
    [SerializeField, Range(0f, 2f)] private float gravityModifier = 0.45f;

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

    /// <summary>Kept signature-compatible with FractureObject.Explode.</summary>
    public void Explode(Transform origin, int colorId)
    {
        if (!origin) return;
        Play(origin.position, Vector2.one, colorId);
    }

    public void ExplodeAtPosition(Vector3 worldPosition, int colorId)
    {
        Play(worldPosition, Vector2.one, colorId);
    }

    /// <summary>
    /// Fires one burst.
    /// </summary>
    /// <param name="centre">World centre of the cleared footprint.</param>
    /// <param name="footprintCells">Footprint in board cells, e.g. (2,1) for a 2-wide piece.</param>
    /// <param name="colorId">PieceSimple.ColorId of the block that broke.</param>
    /// <param name="tint">
    /// Exact colour sampled off the block's own sprite. Pass null to fall back to the
    /// <see cref="palette"/> lookup by colorId.
    /// </param>
    public void Play(Vector3 centre, Vector2 footprintCells, int colorId, Color? tint = null)
    {
        PieceTintSampler.TintBands? bands =
            tint.HasValue ? PieceTintSampler.TintBands.FromBody(tint.Value)
                          : (PieceTintSampler.TintBands?)null;
        Play(centre, footprintCells, colorId, bands);
    }

    /// <summary>
    /// Fires one burst using the block's own three painted colours, which is what makes the
    /// shards read as chips off the same flat 2D block.
    /// </summary>
    /// <param name="bands">
    /// Bevel / body / highlight sampled off the block's sprite. Pass null to fall back to
    /// the <see cref="palette"/> lookup by colorId - note that colorId COLLIDES across
    /// colours in this project, so that path is a last resort only.
    /// </param>
    public void Play(Vector3 centre, Vector2 footprintCells, int colorId,
                     PieceTintSampler.TintBands? bands)
    {
        if (!enabled || _pool.Count == 0) return;

        int slot = _next;
        var ps = _pool[slot];
        _next = (_next + 1) % _pool.Count;

        float cell = ResolveCellSize();

        centre.z = 0f;
        ps.transform.position = centre;

        var b = bands ?? PieceTintSampler.TintBands.FromBody(ColorFor(colorId));

        var mat = _materials[slot];
        if (mat)
        {
            mat.SetColor(IdColDark, b.dark);
            mat.SetColor(IdColBody, b.body);
            mat.SetColor(IdColLight, b.light);
        }

        // White, so colorOverLifetime contributes its alpha ramp and nothing else - the
        // shard colour comes from the material bands, not from the particle stream.
        var main = ps.main;
        main.startColor = Color.white;

        // Emit from a box matching the real block silhouette, flattened to the board plane.
        var shape = ps.shape;
        shape.scale = new Vector3(
            Mathf.Max(0.2f, footprintCells.x * cell * 0.9f),
            Mathf.Max(0.2f, footprintCells.y * cell * 0.9f),
            0.02f);

        int cells = Mathf.Max(1, Mathf.RoundToInt(footprintCells.x * footprintCells.y));
        int count = Mathf.Clamp(cells * shardsPerCell, 24, maxShardsPerBurst);

        var emission = ps.emission;
        emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)count));

        ps.Clear(true);
        ps.Play(true);
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
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetimeRange.x, lifetimeRange.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speedRange.x, speedRange.y);
        main.startSize = new ParticleSystem.MinMaxCurve(shardSizeRange.x, shardSizeRange.y);
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = gravityModifier;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = maxShardsPerBurst + 40;
        main.startColor = Color.white;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 1f, 0.02f);
        shape.randomDirectionAmount = 0.55f;

        // This is what makes the cloud stall instead of flying off like confetti.
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.separateAxes = false;
        limit.limit = new ParticleSystem.MinMaxCurve(0.6f);
        limit.dampen = 0.42f;

        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = true;
        rot.x = new ParticleSystem.MinMaxCurve(-480f * Mathf.Deg2Rad, 480f * Mathf.Deg2Rad);
        rot.y = new ParticleSystem.MinMaxCurve(-480f * Mathf.Deg2Rad, 480f * Mathf.Deg2Rad);
        rot.z = new ParticleSystem.MinMaxCurve(-700f * Mathf.Deg2Rad, 700f * Mathf.Deg2Rad);

        // Pops at full size, holds, then shrinks gradually across the whole fall.
        // The long ramp (0.30 -> 1.0) is the "sinking and getting smaller" tail; keep it
        // wide, a late cliff here is what made the burst read as vanishing too early.
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(0.30f, 1f, 0f, 0f),
            new Keyframe(0.70f, 0.62f, -1.0f, -1.0f),
            new Keyframe(1f, 0f, -1.6f, 0f));

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
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.40f),
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
