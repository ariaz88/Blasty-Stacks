using UnityEngine;

/// <summary>
/// Everything one arrival needs to describe its own light pillar. Passed by
/// reference so adding a knob later does not churn every call site.
/// </summary>
public struct SummonBurstParams
{
    /// <summary>World point the unit landed on - the foot of the pillar.</summary>
    public Vector3 position;

    /// <summary>Warm tint of the whole effect. Per-unit override lives here.</summary>
    public Color tint;

    /// <summary>Pillar height in WORLD UNITS (reference reads ~2.5-3 board cells).</summary>
    public float height;

    /// <summary>Pillar radius in WORLD UNITS (reference reads ~half a board cell).</summary>
    public float radius;

    /// <summary>Overall brightness multiplier, 1 = authored value.</summary>
    public float intensity;
}

/// <summary>
/// One arrival's worth of VFX. The director rents ONE emitter per arriving unit
/// and drives it across the whole arrival:
///
///     BeginTrail(visual)  -> while the unit is airborne
///     EndTrail()          -> the moment it lands
///     PlayBurst(...)      -> flash + ground ring + light pillar, same frame
///     IsBusy == false     -> director reclaims it
///
/// Two backends implement this: <see cref="SummonEmitterParticles"/> (built-in
/// ParticleSystem, runs anywhere) and SummonEmitterVfxGraph (GPU, needs compute
/// shaders). The director picks one at runtime; nothing above this interface
/// knows or cares which is live.
/// </summary>
public interface ISummonEmitter
{
    /// <summary>Start trailing <paramref name="follow"/>; the emitter moves itself each frame.</summary>
    void BeginTrail(Transform follow, Color tint);

    /// <summary>Stop trailing and let the already-emitted trail particles die off naturally.</summary>
    void EndTrail();

    /// <summary>Fire the landing flash, the expanding ground ring and the light pillar.</summary>
    void PlayBurst(in SummonBurstParams p);

    /// <summary>True while anything is still emitting or alive. The pool waits for false.</summary>
    bool IsBusy { get; }

    /// <summary>Reset to a clean state before the pool hands this out again.</summary>
    void ResetForReuse();

    /// <summary>The emitter's own GameObject, so the director can park and reposition it.</summary>
    GameObject Owner { get; }
}
