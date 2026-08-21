// AttackSlotRegistry.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stops attackers piling onto one point, WITHOUT any runtime interaction.
///
/// The rule this project follows is that units never push or react to each
/// other. So instead of shoving attackers apart every frame, each one CLAIMS a
/// numbered spot beside the target the first time it locks on, and simply walks
/// to that spot. Picking a spot once is not an interaction; a continuous shove
/// is, and that is what made heroes orbit their target instead of hitting it.
///
/// Slots fan out alternately from the centre, so a crowd forms a tidy line:
///
///     index :   4     2     0     1     3
///     offset: -2s   -1s     0   +1s   +2s      (s = slotSpacing)
///
/// Slots are per-target, so ten heroes on two different gates do not interfere.
/// </summary>
public static class AttackSlotRegistry
{
    /// <summary>Lateral gap between neighbouring attackers, in world units.</summary>
    public const float DefaultSlotSpacing = 0.9f;

    // target -> (claimant -> slot index). Object, so it works for both an
    // EnemyStats and an EnemyGateStats without a shared base type.
    private static readonly Dictionary<Object, Dictionary<Object, int>> Claims = new();

    /// <summary>
    /// Returns the slot index this claimant holds on the target, claiming the
    /// lowest free one if it does not hold any yet. Re-claiming is safe: a unit
    /// keeps the same slot for as long as it stays on the same target, so it
    /// never drifts sideways mid-fight.
    /// </summary>
    public static int Claim(Object target, Object claimant)
    {
        if (target == null || claimant == null) return 0;

        if (!Claims.TryGetValue(target, out var slots))
        {
            slots = new Dictionary<Object, int>();
            Claims[target] = slots;
        }

        if (slots.TryGetValue(claimant, out int existing)) return existing;

        PruneDead(slots);

        // Lowest index not currently held.
        int index = 0;
        var taken = new HashSet<int>(slots.Values);
        while (taken.Contains(index)) index++;

        slots[claimant] = index;
        return index;
    }

    /// <summary>Gives up whatever slot this claimant holds, on any target.</summary>
    public static void Release(Object claimant)
    {
        if (claimant == null) return;

        foreach (var kv in Claims)
            kv.Value.Remove(claimant);
    }

    /// <summary>Frees every slot on a target - call when the target dies.</summary>
    public static void ReleaseTarget(Object target)
    {
        if (target == null) return;
        Claims.Remove(target);
    }

    /// <summary>
    /// Lateral offset for a slot index: 0, +s, -s, +2s, -2s, ...
    /// Odd indices go right, even (non-zero) go left, so the line grows evenly
    /// on both sides of the target instead of trailing off to one side.
    /// </summary>
    public static float OffsetForSlot(int index, float slotSpacing = DefaultSlotSpacing)
    {
        if (index <= 0) return 0f;

        int step = (index + 1) / 2;              // 1,1,2,2,3,3...
        float side = (index % 2 == 1) ? 1f : -1f; // odd right, even left
        return side * step * slotSpacing;
    }

    /// <summary>Drops entries whose claimant was destroyed, so slots are not leaked.</summary>
    private static void PruneDead(Dictionary<Object, int> slots)
    {
        List<Object> dead = null;

        foreach (var kv in slots)
        {
            if (kv.Key == null)
                (dead ??= new List<Object>()).Add(kv.Key);
        }

        if (dead == null) return;
        foreach (var d in dead) slots.Remove(d);
    }

    /// <summary>Wipes everything. Call between stages so slots do not survive a reload.</summary>
    public static void Clear() => Claims.Clear();
}
