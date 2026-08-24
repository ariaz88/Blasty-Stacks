using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the summon-arrival VFX: which backend is live, the emitter pool, and the
/// shared look settings every arrival starts from.
///
/// Follows the project's hand-rolled singleton pattern (see LevelManager /
/// CurrencyManager), with one difference: if nothing in the scene provides one,
/// <see cref="Instance"/> creates it. This system has no serialized dependencies
/// and no scene references, so requiring a manual drop-in would only mean the
/// summon silently does nothing in any scene someone forgot to wire. Drop a
/// SummonVfxDirector into StarterScene when you want to TUNE the values.
/// </summary>
[DisallowMultipleComponent]
public class SummonVfxDirector : MonoBehaviour
{
    public enum Backend
    {
        /// <summary>VFX Graph where the GPU allows it, ParticleSystem otherwise.</summary>
        Auto = 0,
        /// <summary>Force the built-in ParticleSystem backend.</summary>
        Particles = 1,
        /// <summary>Force VFX Graph. Renders nothing without compute-shader support.</summary>
        VfxGraph = 2,
    }

    private static SummonVfxDirector _instance;

    public static SummonVfxDirector Instance
    {
        get
        {
            if (_instance != null) return _instance;

            _instance = FindFirstObjectByType<SummonVfxDirector>();
            if (_instance != null) return _instance;

            var go = new GameObject("SummonVfxDirector (auto)");
            _instance = go.AddComponent<SummonVfxDirector>();
            return _instance;
        }
    }

    [Header("Backend")]
    [SerializeField] private Backend backend = Backend.Auto;

    [Tooltip("Prefab carrying a VisualEffect + SummonEmitterVfxGraph. Only used by " +
             "the VFX Graph backend; leave empty until SummonPillar.vfx exists.")]
    [SerializeField] private GameObject vfxGraphEmitterPrefab;

    [Tooltip("Optional. Leave empty and the ParticleSystem emitter is built in code, " +
             "which needs no prefab and no imported art.")]
    [SerializeField] private GameObject particleEmitterPrefab;

    [Header("Pillar shape (world units)")]
    [Tooltip("Reference reads ~2.5-3 board cells tall.")]
    [SerializeField, Min(0.05f)] private float pillarHeight = 2.6f;
    [Tooltip("Reference reads ~half a board cell.")]
    [SerializeField, Min(0.01f)] private float pillarRadius = 0.45f;
    [SerializeField, Min(0f)] private float intensity = 1f;

    [Tooltip("Default warm tint. A unit can override it per arrival.")]
    [SerializeField] private Color tint = new Color(1f, 0.88f, 0.29f, 1f);

    [Tooltip("Pushed toward the camera so the effect draws over the board. This is " +
             "the ONLY depth control the VFX Graph backend has - it ignores sorting layers.")]
    [SerializeField] private float zOffset = -0.1f;

    [Header("Landing telegraph (the ground circle)")]
    [Tooltip("Filled white disc that pops in just BEFORE the unit lands, then opens " +
             "out into a ring and fades. Separate from the landing burst's ring: it " +
             "starts earlier and on its own clock.")]
    [SerializeField] private bool groundCircleEnabled = true;

    [Tooltip("How far AHEAD of touchdown the disc appears. The reference shows it " +
             "roughly one frame early at 30fps, so ~0.03-0.08s.")]
    [SerializeField, Min(0f)] private float circleLeadTime = 0.05f;

    [Tooltip("Diameter of the FILLED disc in world units, before it grows.")]
    [SerializeField, Min(0.01f)] private float circleDiameter = 0.9f;

    [Tooltip("The reference telegraph is WHITE, not the gold of the pillar - which " +
             "is why it has its own colour rather than reusing tint.")]
    [SerializeField] private Color circleTint = Color.white;

    [Header("Pool")]
    [SerializeField, Min(0)] private int prewarm = 4;

    private readonly List<ISummonEmitter> _free = new List<ISummonEmitter>();
    private readonly List<ISummonEmitter> _draining = new List<ISummonEmitter>();
    private readonly List<SummonGroundCircle> _circles = new List<SummonGroundCircle>();

    private Backend _resolved;
    private bool _resolvedOnce;

    public Color DefaultTint => tint;
    public float PillarHeight => pillarHeight;
    public float PillarRadius => pillarRadius;
    public float Intensity => intensity;
    public float ZOffset => zOffset;

    /// <summary>How far ahead of touchdown the ground telegraph should appear.</summary>
    public float CircleLeadTime => circleLeadTime;

    /// <summary>False when the telegraph is switched off in the Inspector.</summary>
    public bool GroundCircleEnabled => groundCircleEnabled;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        ResolveBackend();

        for (int i = 0; i < prewarm; i++)
            _free.Add(CreateEmitter());
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    private void Update()
    {
        // Emitters go back into rotation only once their last particle has died,
        // otherwise recycling one would cut a pillar off mid-fade.
        for (int i = _draining.Count - 1; i >= 0; i--)
        {
            var e = _draining[i];
            if (e == null || e.Owner == null) { _draining.RemoveAt(i); continue; }
            if (e.IsBusy) continue;

            _draining.RemoveAt(i);
            e.ResetForReuse();
            e.Owner.SetActive(false);
            _free.Add(e);
        }
    }

    // ---------------- public API ----------------

    /// <summary>
    /// Takes an emitter out of the pool for one arrival. Give it back with
    /// <see cref="Release"/> once the burst has been fired.
    /// </summary>
    public ISummonEmitter Rent()
    {
        ISummonEmitter e = null;

        while (_free.Count > 0 && e == null)
        {
            int last = _free.Count - 1;
            e = _free[last];
            _free.RemoveAt(last);

            // Scene reloads can leave destroyed entries behind.
            if (e != null && e.Owner == null) e = null;
        }

        e ??= CreateEmitter();

        e.Owner.SetActive(true);
        e.ResetForReuse();
        return e;
    }

    /// <summary>Hands an emitter back; it is reclaimed once it stops emitting.</summary>
    public void Release(ISummonEmitter e)
    {
        if (e == null || e.Owner == null) return;
        if (_draining.Contains(e) || _free.Contains(e)) return;

        _draining.Add(e);
    }

    /// <summary>
    /// One-shot pillar at a world point, no trail. For callers that do not have a
    /// jump to hang the effect off.
    /// </summary>
    public void PlayBurstAt(Vector3 worldPos, Color? tintOverride = null)
    {
        var e = Rent();
        e.PlayBurst(BuildParams(worldPos, tintOverride));
        Release(e);
    }

    /// <summary>Fills in this director's shared look settings for one arrival.</summary>
    public SummonBurstParams BuildParams(Vector3 worldPos, Color? tintOverride = null)
    {
        worldPos.z += zOffset;

        return new SummonBurstParams
        {
            position = worldPos,
            tint = tintOverride ?? tint,
            height = pillarHeight,
            radius = pillarRadius,
            intensity = intensity,
        };
    }

    /// <summary>
    /// Plays the pre-landing ground telegraph at a world point: a filled white
    /// ellipse that holds, opens out into a ring, then expands and fades.
    ///
    /// Called by SummonArrivalBinder while the unit is still AIRBORNE - it is
    /// timed off FrogJumpTransformOnly.TimeUntilLanding, not off the Landed
    /// event, because the whole point is that it lands first.
    /// </summary>
    public void PlayGroundCircle(Vector3 worldPos)
    {
        if (!groundCircleEnabled) return;

        worldPos.z += zOffset;
        GetFreeCircle().Play(worldPos, circleDiameter, circleTint);
    }

    /// <summary>
    /// Circles are recycled by asking each one whether it is still animating,
    /// rather than by a drain list. There is at most a handful alive at once and
    /// SummonGroundCircle already deactivates itself when it finishes, so the
    /// extra bookkeeping would buy nothing.
    /// </summary>
    private SummonGroundCircle GetFreeCircle()
    {
        for (int i = _circles.Count - 1; i >= 0; i--)
        {
            var c = _circles[i];
            if (c == null) { _circles.RemoveAt(i); continue; }
            if (!c.IsPlaying) return c;
        }

        var go = new GameObject("SummonGroundCircle");
        go.transform.SetParent(transform, false);
        var circle = go.AddComponent<SummonGroundCircle>();
        _circles.Add(circle);
        return circle;
    }

    [ContextMenu("Preview Summon")]
    private void PreviewSummon()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SummonVfx] Preview Summon only works in Play mode.", this);
            return;
        }

        PlayBurstAt(transform.position);
    }

    // ---------------- internals ----------------

    private void ResolveBackend()
    {
        if (_resolvedOnce) return;
        _resolvedOnce = true;

        _resolved = backend;

        if (_resolved == Backend.Auto)
        {
#if SUMMON_VFX_GRAPH
            _resolved = SystemInfo.supportsComputeShaders && vfxGraphEmitterPrefab != null
                ? Backend.VfxGraph
                : Backend.Particles;
#else
            _resolved = Backend.Particles;
#endif
        }

#if !SUMMON_VFX_GRAPH
        if (_resolved == Backend.VfxGraph)
        {
            Debug.LogWarning("[SummonVfx] Backend forced to VfxGraph but SUMMON_VFX_GRAPH is not " +
                             "defined - falling back to Particles. Install " +
                             "com.unity.visualeffectgraph and add SUMMON_VFX_GRAPH to " +
                             "Player Settings > Scripting Define Symbols.", this);
            _resolved = Backend.Particles;
        }
#endif

        if (_resolved == Backend.VfxGraph && !SystemInfo.supportsComputeShaders)
        {
            Debug.LogWarning("[SummonVfx] This device has no compute-shader support; VFX Graph " +
                             "would render nothing. Falling back to Particles.", this);
            _resolved = Backend.Particles;
        }
    }

    private ISummonEmitter CreateEmitter()
    {
        ResolveBackend();

        GameObject go;

        if (_resolved == Backend.VfxGraph && vfxGraphEmitterPrefab != null)
        {
            go = Instantiate(vfxGraphEmitterPrefab, transform);
        }
        else if (particleEmitterPrefab != null)
        {
            go = Instantiate(particleEmitterPrefab, transform);
        }
        else
        {
            // No prefab anywhere: build the code-configured ParticleSystem emitter.
            go = new GameObject("SummonEmitter");
            go.transform.SetParent(transform, false);
            go.AddComponent<SummonEmitterParticles>();
        }

        var emitter = go.GetComponent<ISummonEmitter>();
        if (emitter == null)
        {
            Debug.LogError($"[SummonVfx] Emitter prefab '{go.name}' has no ISummonEmitter " +
                           "component. Falling back to the built-in particle emitter.", go);
            emitter = go.AddComponent<SummonEmitterParticles>();
        }

        go.SetActive(false);
        return emitter;
    }
}
