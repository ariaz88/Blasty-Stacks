// CrowdSeparation2D.cs
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

    /// <summary>Scene-wide access, so movement code can ask for a steered direction.</summary>
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

            var t = h.collider.transform;
            if (t == self || t.IsChildOf(self) || self.IsChildOf(t)) continue;

            // Allies only - see the summary above.
            if (t.gameObject.layer != self.gameObject.layer) continue;

            if (h.distance >= nearest) continue;
            nearest = h.distance;
            blocker = t;
        }

        if (blocker == null) return desiredDir;   // path is clear, go straight

        Vector2 toBlocker = (Vector2)blocker.position - (Vector2)self.position;

        // Perpendicular to travel; flip it so we swerve AWAY from the blocker
        // rather than into it.
        Vector2 perp = new Vector2(-dir.y, dir.x);
        if (Vector2.Dot(perp, toBlocker) > 0f) perp = -perp;

        // The closer the blocker, the harder the swerve. At the edge of the
        // look-ahead the correction is nearly zero, so it eases in smoothly.
        float closeness = 1f - Mathf.Clamp01(nearest / lookAheadDistance);

        return (dir + perp * (avoidStrength * closeness)).normalized;
    }
}
