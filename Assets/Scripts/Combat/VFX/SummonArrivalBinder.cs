using UnityEngine;

/// <summary>
/// Hangs the summon VFX off the arrival a unit already performs. Added at spawn
/// time by PlayerWaveManager.SpawnUnitAt, so BOTH arrival paths get it with no
/// prefab edits: the puzzle-blast wave unlock and the bought reinforcements.
///
/// It deliberately does not animate anything itself. The toss-in already exists:
/// the gate Animator's "Throw" leads to FrogJumpTransformOnly, which owns the
/// arc, the apex scale and the ground shadow. This only adds the three things
/// the reference clip has and this game did not:
///
///   Jumped  -> trail ribbon follows the unit through the arc
///   Landed  -> trail stops, flash + ground ring + light pillar fire on the frame
///
/// Hooking the jumper's events rather than polling IsJumping matters: the flash
/// has to land on the same frame as the unit, and a poll in Update would be one
/// frame late about half the time, which reads as a mistimed effect.
/// </summary>
[DisallowMultipleComponent]
public class SummonArrivalBinder : MonoBehaviour
{
    [Tooltip("Leave off to use the director's shared tint. Turn on for a per-unit colour.")]
    [SerializeField] private bool overrideTint;
    [SerializeField] private Color tint = new Color(1f, 0.88f, 0.29f, 1f);

    private FrogJumpTransformOnly _jumper;
    private Transform _followTarget;
    private ISummonEmitter _emitter;
    private bool _subscribed;
    private bool _circleFired;

    /// <summary>Per-unit colour, for callers that know the unit's element/rarity.</summary>
    public void SetTint(Color c)
    {
        overrideTint = true;
        tint = c;
    }

    private void Awake()
    {
        // GetComponentInChildren, not GetComponent: PlayerWaveManager looks for
        // the jumper on the root, but the runtime prefabs are free to put it on
        // the visual child and some do.
        _jumper = GetComponentInChildren<FrogJumpTransformOnly>(true);

        if (_jumper == null)
        {
            // No jump on this unit means no arrival to decorate. Silent by design -
            // stage scenery and gate props run through the same spawn funnel.
            enabled = false;
            return;
        }

        // Trail the VISUAL where there is one, so the ribbon lines up with the
        // sprite rather than the physics root's pivot.
        var pm = GetComponent<PlayerManager>();
        _followTarget = pm != null && pm.visualRoot != null ? pm.visualRoot : _jumper.transform;
    }

    private void OnEnable()
    {
        if (_jumper == null || _subscribed) return;

        _jumper.Jumped += HandleJumped;
        _jumper.Landed += HandleLanded;
        _subscribed = true;
    }

    private void OnDisable()
    {
        if (_jumper != null && _subscribed)
        {
            _jumper.Jumped -= HandleJumped;
            _jumper.Landed -= HandleLanded;
        }
        _subscribed = false;

        // Killed or pooled away mid-jump: drop the trail rather than leaving an
        // emitter following a dead transform forever.
        ReleaseEmitter();
    }

    private void HandleJumped()
    {
        var director = SummonVfxDirector.Instance;
        if (director == null) return;

        // A second Jumped without a Landed should not leak the first emitter.
        ReleaseEmitter();

        _emitter = director.Rent();
        _emitter.BeginTrail(_followTarget, overrideTint ? tint : director.DefaultTint);

        // Re-arm the ground telegraph for this jump.
        _circleFired = false;
    }

    /// <summary>
    /// Fires the ground telegraph while the unit is STILL IN THE AIR.
    ///
    /// This one genuinely has to be a poll. Every other part of the arrival hangs
    /// off Jumped/Landed, but "a moment BEFORE landing" is not an event the
    /// jumper raises - it is a threshold on how much flight is left. The jumper
    /// resolves its landing point up front (LandingPosition), so the disc can be
    /// placed on the exact cell the unit is about to hit.
    /// </summary>
    private void Update()
    {
        if (_circleFired || _jumper == null || !_jumper.IsJumping) return;

        var director = SummonVfxDirector.Instance;
        if (director == null || !director.GroundCircleEnabled) return;

        if (_jumper.TimeUntilLanding > director.CircleLeadTime) return;

        _circleFired = true;
        director.PlayGroundCircle(_jumper.LandingPosition);
    }

    private void HandleLanded()
    {
        var director = SummonVfxDirector.Instance;
        if (director == null) return;

        // Landed without a Jumped (jump started before this component woke up):
        // still play the impact, it is the more important half of the effect.
        _emitter ??= director.Rent();

        _emitter.EndTrail();

        Vector3 landing = _jumper != null ? _jumper.transform.position : transform.position;
        _emitter.PlayBurst(director.BuildParams(landing, overrideTint ? tint : (Color?)null));

        ReleaseEmitter();
    }

    private void ReleaseEmitter()
    {
        if (_emitter == null) return;

        _emitter.EndTrail();

        var director = SummonVfxDirector.Instance;
        if (director != null) director.Release(_emitter);

        _emitter = null;
    }
}
