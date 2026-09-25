using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays a one-shot attack VFX prefab on every swing of a hero. Sits on the hero
/// root (next to PlayerManager); PlayerAnimatitorManager.EnableDamageCollier calls
/// Play(), so the effect lands on the same animation frame the hitbox opens.
///
/// Instances are CHILDREN of the hero: they move with it, and the hero's own
/// negative X scale (PlayerManager.FaceLeft) mirrors them for free - the child
/// systems use Hierarchy scaling so that sign reaches every layer.
///
/// Instances are pooled: a hero swings every ~0.5-0.7s and the effect lives ~0.5s,
/// so a few instances cover it with no per-swing Instantiate.
/// </summary>
[DisallowMultipleComponent]
public class HeroAttackVfx : MonoBehaviour
{
    [Tooltip("One-shot VFX prefab (e.g. Assets/VFXKit/Effects/valkyrie_slash). Its root " +
             "ParticleSystem's Play() plays every layer.")]
    [SerializeField] private GameObject vfxPrefab;

    [Tooltip("Local position of the effect's origin under the hero, for a hero facing " +
             "RIGHT (the hero's own flip mirrors it). For an arc effect this is the " +
             "centre of the swing.")]
    [SerializeField] private Vector2 offset = new Vector2(0.1f, 0.2f);

    [Tooltip("Uniform local scale of the effect under the hero.")]
    [SerializeField, Min(0.01f)] private float scale = 1f;

    [Tooltip("Sorting layer / order for every particle renderer in the effect. Hero " +
             "sprites draw at order 1 on Default, so the slash must sit above that.")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 20;

    [Tooltip("The hero's built-in slash sprites that this effect REPLACES. Their sprite " +
             "is cleared in Awake. Toggling 'enabled' would NOT stick: Spriter's " +
             "EntityRenderer.OnEnable re-enables every renderer of the rig, but nothing " +
             "animates or restores the sprite itself.")]
    [SerializeField] private SpriteRenderer[] replacedRenderers;

    [SerializeField, Min(1)] private int poolSize = 3;

    private readonly List<ParticleSystem> _pool = new List<ParticleSystem>();
    private int _next;

    private void Awake()
    {
        if (replacedRenderers == null) return;
        foreach (var r in replacedRenderers)
            if (r != null) r.sprite = null;
    }

    public void Play()
    {
        if (vfxPrefab == null) return;

        ParticleSystem ps = Rent();
        if (ps == null) return;

        Transform t = ps.transform;
        t.localPosition = new Vector3(offset.x, offset.y, 0f);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one * scale;

        ps.Clear(true);
        ps.Play(true);
    }

    private ParticleSystem Rent()
    {
        // Prefer an idle instance; otherwise recycle round-robin (oldest swing).
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i] != null && !_pool[i].IsAlive(true))
                return _pool[i];

        if (_pool.Count < poolSize)
        {
            ParticleSystem created = Create();
            if (created != null) _pool.Add(created);
            return created;
        }

        _next = (_next + 1) % _pool.Count;
        return _pool[_next];
    }

    private ParticleSystem Create()
    {
        GameObject go = Instantiate(vfxPrefab, transform, false);
        go.name = vfxPrefab.name;

        var ps = go.GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError($"[HeroAttackVfx] '{vfxPrefab.name}' has no ParticleSystem on its root.", this);
            Destroy(go);
            return null;
        }

        foreach (var child in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = child.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            // Hierarchy scaling so the hero's scale (and its mirror sign) reaches every layer.
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            r.sortingLayerName = sortingLayerName;
            r.sortingOrder = sortingOrder;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        return ps;
    }
}
