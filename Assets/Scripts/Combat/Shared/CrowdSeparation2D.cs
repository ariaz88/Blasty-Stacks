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

    [Header("Personal Space (WHILE WALKING ONLY)")]
    [Tooltip("Closest two ALLIES may get while one of them is still walking. Set 0 " +
             "to disable. This is NOT the old separation system: it only runs from " +
             "the pursue state, so a unit that has stopped to attack is never moved.")]
    [SerializeField, Min(0f)] private float personalSpace = 0.55f;

    [Tooltip("Maximum correction speed, units/second. Keep it small - the point is " +
             "to stop two units merging, not to shove them apart.")]
    [SerializeField, Min(0f)] private float personalSpaceSpeed = 0.6f;

    private static readonly Collider2D[] SpaceBuffer = new Collider2D[8];

    /// <summary>Scene-wide access, so movement code can ask for a steered direction.</summary>
    public static CrowdSeparation2D Instance { get; private set; }

    /// <summary>
    /// A small positional correction that keeps a WALKING unit from sinking into
    /// an ally standing in the same spot. Returns the delta to apply this frame.
    ///
    /// WHY THIS IS NOT THE OLD SEPARATION SYSTEM, which was deleted twice:
    ///   * It is only called from PlayerPursueTargetState, so a unit that has
    ///     stopped to attack is never touched. Standing units keep the "zero
    ///     interaction" rule exactly.
    ///   * It is ALLIES ONLY. The old one also pushed heroes off enemies, which
    ///     is what made them orbit their target instead of hitting it.
    ///   * It is speed-capped and only acts inside personalSpace, so it nudges
    ///     rather than shoves. It never bends the travel direction.
    /// </summary>
    public Vector2 ResolveOverlap(Transform self)
    {
        if (personalSpace <= 0f || personalSpaceSpeed <= 0f) return Vector2.zero;

        int count = Physics2D.OverlapCircleNonAlloc((Vector2)self.position, personalSpace,
                                                    SpaceBuffer, unitLayers);
        if (count <= 1) return Vector2.zero;

        Vector2 push = Vector2.zero;
        Vector2 selfPos = self.position;

        for (int i = 0; i < count; i++)
        {
            var c = SpaceBuffer[i];
            if (!c) continue;

            var other = c.transform;
            if (other == self || other.IsChildOf(self) || self.IsChildOf(other)) continue;

            // Allies only. Never back away from an enemy.
            if (other.gameObject.layer != self.gameObject.layer) continue;

            Vector2 away = selfPos - (Vector2)other.position;
            float dist = away.magnitude;
            if (dist >= personalSpace) continue;

            if (dist < 0.0001f)
            {
                // Perfectly merged: no direction to work with. Break the tie by
                // instance id so the two pick opposite sides instead of drifting
                // together, and so the choice is stable frame to frame.
                push += new Vector2(self.GetInstanceID() < other.GetInstanceID() ? -1f : 1f, 0f);
                continue;
            }

            push += (away / dist) * (1f - dist / personalSpace);
        }

        if (push.sqrMagnitude < 0.000001f) return Vector2.zero;

        return Vector2.ClampMagnitude(push, 1f) * personalSpaceSpeed * Time.deltaTime;
    }

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

            var t = h.collider.transform;
            if (t == self || t.IsChildOf(self) || self.IsChildOf(t)) continue;

            // Allies only - see the summary above.
            if (t.gameObject.layer != self.gameObject.layer) continue;

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
            // to OPPOSITE sides, exactly as ResolveOverlap already does.
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
