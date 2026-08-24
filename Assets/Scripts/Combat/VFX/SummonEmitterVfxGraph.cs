// The VFX Graph backend only compiles when the package is present AND the
// SUMMON_VFX_GRAPH define is set (Project Settings > Player > Scripting Define
// Symbols). Guarding it this way means the project still builds on a machine
// where com.unity.visualeffectgraph has not been installed - without the guard,
// the `using UnityEngine.VFX` below is a hard compile error for everyone.
#if SUMMON_VFX_GRAPH

using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// GPU backend for the summon arrival, driving a hand-built SummonPillar.vfx
/// graph. Requires compute-shader support - on Android that means GLES 3.1+ or
/// Vulkan. <see cref="SummonVfxDirector"/> checks for that and falls back to
/// <see cref="SummonEmitterParticles"/> when it is missing, so this class never
/// has to worry about the unsupported case.
///
/// CONTRACT WITH THE GRAPH - these names must match exactly, see
/// "Assets/Documentation for scripts/SummonPillarVFX-Recipe.txt":
///   exposed properties : PillarColor (Color), PillarHeight (float),
///                        PillarRadius (float), Intensity (float),
///                        TrailColor (Color)
///   events             : OnSummon, OnTrailStart, OnTrailStop
///
/// NOTE ON SORTING: VFX Graph output does NOT participate in the URP 2D
/// renderer's sorting layers. Depth comes from the Z offset the director applies
/// plus each output's Sorting Priority inside the graph. Do not expect
/// sortingLayerName to do anything here - it does not exist on VisualEffect.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VisualEffect))]
public class SummonEmitterVfxGraph : MonoBehaviour, ISummonEmitter
{
    private static readonly int PillarColorId = Shader.PropertyToID("PillarColor");
    private static readonly int PillarHeightId = Shader.PropertyToID("PillarHeight");
    private static readonly int PillarRadiusId = Shader.PropertyToID("PillarRadius");
    private static readonly int IntensityId = Shader.PropertyToID("Intensity");
    private static readonly int TrailColorId = Shader.PropertyToID("TrailColor");

    private VisualEffect _vfx;
    private Transform _follow;
    private bool _warned;

    public GameObject Owner => gameObject;

    public bool IsBusy => _follow != null || (_vfx != null && _vfx.aliveParticleCount > 0);

    private void Awake()
    {
        _vfx = GetComponent<VisualEffect>();

        if (_vfx != null && _vfx.visualEffectAsset == null && !_warned)
        {
            _warned = true;
            Debug.LogWarning("[SummonVfx] VisualEffect has no asset assigned - the summon " +
                             "will be silent. Build SummonPillar.vfx from the recipe in " +
                             "'Assets/Documentation for scripts/SummonPillarVFX-Recipe.txt', " +
                             "or set the director's Backend to Particles.", this);
        }
    }

    private void LateUpdate()
    {
        if (_follow != null) transform.position = _follow.position;
    }

    public void BeginTrail(Transform follow, Color tint)
    {
        if (follow == null || !Ready()) return;

        _follow = follow;
        transform.position = follow.position;

        SetIfExists(TrailColorId, tint);
        _vfx.SendEvent("OnTrailStart");
    }

    public void EndTrail()
    {
        _follow = null;
        if (Ready()) _vfx.SendEvent("OnTrailStop");
    }

    public void PlayBurst(in SummonBurstParams p)
    {
        _follow = null;
        if (!Ready()) return;

        transform.position = p.position;

        SetIfExists(PillarColorId, p.tint);
        SetIfExists(PillarHeightId, p.height);
        SetIfExists(PillarRadiusId, p.radius);
        SetIfExists(IntensityId, p.intensity);

        _vfx.SendEvent("OnSummon");
    }

    public void ResetForReuse()
    {
        _follow = null;
        if (_vfx != null) _vfx.Reinit();
    }

    private bool Ready() => _vfx != null && _vfx.visualEffectAsset != null;

    // HasFloat/HasVector4 keep a half-finished graph from spamming "property not
    // found" every single summon while the effect is still being authored.
    private void SetIfExists(int id, float v) { if (_vfx.HasFloat(id)) _vfx.SetFloat(id, v); }
    private void SetIfExists(int id, Color v) { if (_vfx.HasVector4(id)) _vfx.SetVector4(id, v); }
}

#endif
