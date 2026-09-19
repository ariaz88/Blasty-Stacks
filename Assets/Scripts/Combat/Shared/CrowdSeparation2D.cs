// CrowdSeparation2D.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Path avoidance for units. Nothing else.
///
/// DESIGN RULE (decided 2026-08-21, after two failed attempts):
/// Units have ZERO interaction with each other. They do not collide, do not push,
/// and do not react in any way when they end up next to or on top of each other.
/// The ONE exception is this: while a unit is WALKING, if an ally is directly in
/// its path it bends its route around them, then straightens out again.
///
/// WHAT WAS REMOVED AND WHY - do not put it back:
///
///   * A "separation" pass used to shove neighbours apart every LateUpdate. With
///     sameTeamOnly off it also shoved heroes away from ENEMIES, so a hero that
///     got close enough to attack was pushed back out, approached again, and
///     ended up orbiting its target instead of fighting it.
///   * Even same-team-only, a constant shove is still an interaction, which is
///     exactly what the design forbids. Standing units must be left alone.
///
/// Spreading attackers out at a shared target is NOT solved here either. That is
/// AttackSlotRegistry's job: a hero claims its own attack spot ONCE on arrival
/// and then stands still. Choosing a spot on arrival is not an interaction;
/// continuously pushing is.
/// </summary>
public class CrowdSeparation2D : MonoBehaviour
{
    [Header("Who counts as a blocker")]
    [Tooltip("Layers searched when looking for someone in the way. Should be " +
             "PlayerLayer + EnemyLayer; the same-team check below does the filtering.")]
    [SerializeField] private LayerMask unitLayers = ~0;

    [Header("Path Avoidance (steer AROUND allies in the way)")]
    [Tooltip("How far ahead a unit looks for an ally blocking its path.")]
    [SerializeField, Min(0f)] private float lookAheadDistance = 1.3f;

    [Tooltip("Width of the look-ahead probe. Roughly the unit's body radius.")]
    [SerializeField, Min(0.05f)] private float lookAheadRadius = 0.4f;

    [Tooltip("How hard to swerve around a blocker. 0 disables path avoidance.")]
    [SerializeField, Range(0f, 2f)] private float avoidStrength = 0.9f;

    [Tooltip("How far off dead-ahead a blocker must be before we RE-DECIDE which " +
             "way to go around it (0 = dead ahead, 1 = straight to the side).\n" +
             "A blocker directly in front is equally passable on either side, so " +
             "without this the choice flips every frame and the unit visibly spins.")]
    [SerializeField, Range(0f, 0.6f)] private float swerveCommitThreshold = 0.25f;

    // Which way each unit last decided to go around: +1 or -1. Held so an
    // ambiguous, dead-ahead blocker does not make the unit flip-flop.
    private readonly Dictionary<Transform, float> swerveSide = new();

    /// <summary>Scene-wide path steering; never writes unit positions.</summary>
    public static CrowdSeparation2D Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Bends <paramref name="desiredDir"/> around an ALLY standing in the way, so
    /// the unit walks around it instead of straight into it, then straightens out
    /// again the moment the path is clear.
    ///
    /// Only same-layer (same team) units are avoided ON PURPOSE. Enemies must NOT
    /// be avoided - a hero walking at an enemy would otherwise swerve around its
    /// own target forever and never reach it. That was a real bug, not a theory.
    /// </summary>
    public Vector2 SteerAroundBlockers(Transform self, Vector2 desiredDir)
    {
        if (avoidStrength <= 0f || lookAheadDistance <= 0f) return desiredDir;
        if (desiredDir.sqrMagnitude < 0.0001f) return desiredDir;

        Vector2 dir = desiredDir.normalized;

        var hits = Physics2D.CircleCastAll((Vector2)self.position, lookAheadRadius,
                                           dir, lookAheadDistance, unitLayers);

        Transform blocker = null;
        float nearest = float.MaxValue;

        foreach (var h in hits)
        {
            if (!h.collider) continue;

            // Weapon triggers are not obstacles. Resolve child bodies to their owner.
            if (h.collider.isTrigger) continue;
            var owner = h.collider.GetComponentInParent<CharacterStats>();
            if (!owner || owner.currentHP <= 0f) continue;
            var t = owner.transform;
            if (t == self || t.IsChildOf(self) || self.IsChildOf(t)) continue;

            // Allies only - see the summary above.
            if (t.gameObject.layer != self.gameObject.layer) continue;

            // Initial overlaps beside/behind us must not bend the forward route.
            Vector2 offset = (Vector2)t.position - (Vector2)self.position;
            if (Vector2.Dot(offset, dir) <= 0f && offset.sqrMagnitude > 0.0004f) continue;

            if (h.distance >= nearest) continue;
            nearest = h.distance;
            blocker = t;
        }

        if (blocker == null)
        {
            // Path clear: forget the committed side so the next encounter decides
            // fresh, instead of inheriting a stale choice.
            swerveSide.Remove(self);
            return desiredDir;
        }

        Vector2 toBlockerRaw = (Vector2)blocker.position - (Vector2)self.position;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        float side;

        if (toBlockerRaw.sqrMagnitude < 0.0004f)   // < 2cm apart: effectively merged
        {
            // DEGENERATE CASE - this one actually bit us.
            // With the two units on top of each other there is no "which side"
            // to compute: the direction normalises to nothing, the dot lands on
            // zero, and `dot > 0f` is FALSE for BOTH of them. They then picked the
            // SAME side, swerved together, and travelled as one merged blob
            // instead of separating. Break the tie by instance id so they commit
            // to OPPOSITE sides, without moving either unit directly.
            side = self.GetInstanceID() < blocker.GetInstanceID() ? -1f : 1f;
            swerveSide[self] = side;
        }
        else
        {
            Vector2 toBlocker = toBlockerRaw.normalized;
            float dot = Vector2.Dot(perp, toBlocker);

            // A blocker DEAD AHEAD gives a dot near zero, where noise flips the
            // sign every frame and the unit spins on the spot. So once a side is
            // chosen we COMMIT to it until the blocker is clearly to one side.
            if (Mathf.Abs(dot) < swerveCommitThreshold && swerveSide.TryGetValue(self, out float held))
            {
                side = held;                   // too ambiguous to re-decide - hold course
            }
            else
            {
                side = dot > 0f ? -1f : 1f;    // clear enough: pick the far side
                swerveSide[self] = side;
            }
        }

        perp *= side;

        // The closer the blocker, the harder the swerve. At the edge of the
        // look-ahead the correction is nearly zero, so it eases in smoothly.
        float closeness = 1f - Mathf.Clamp01(nearest / lookAheadDistance);

        return (dir + perp * (avoidStrength * closeness)).normalized;
    }
}
