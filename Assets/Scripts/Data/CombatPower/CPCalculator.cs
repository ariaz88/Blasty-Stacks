// CPCalculator.cs
using UnityEngine;

public static class CPCalculator
{
    /// <summary>
    /// Cosmetic display divisor. CP is a raw product of three stats, so it lands
    /// in the thousands; dividing every unit by the same constant keeps the
    /// displayed number near the magnitudes the menus already showed. Because it
    /// is the SAME constant for every unit it cannot change any ordering -
    /// raising or lowering it rescales the whole roster and nothing else.
    /// </summary>
    public const float DisplayDivisor = 200f;

    /// <summary>
    /// CP for a single unit:  (ATK x AtkSpd) x EffectiveHP / K
    ///
    /// This is the 1v1 duel condition rearranged, not a scoring heuristic. A duel
    /// is won by whoever kills first, i.e. whoever has the smaller
    ///
    ///     time-to-kill = enemy EffectiveHP / (own ATK x own AtkSpd)
    ///
    /// Cross-multiplying that comparison turns it into
    ///
    ///     ATK_a x AtkSpd_a x EffectiveHP_a   >   ATK_b x AtkSpd_b x EffectiveHP_b
    ///
    /// so in a duel the unit with the higher CP wins by construction.
    ///
    /// moveSpeed and attackRange are deliberately absent - neither changes who
    /// wins a duel. There are no per-stat weights any more: the three factors
    /// enter multiplicatively, which is what makes the comparison exact.
    ///
    /// <paramref name="level"/> and <paramref name="cfg"/> are kept so existing
    /// call sites still compile, but the formula needs neither.
    /// </summary>
    public static int UnitCP(UnitStatsRuntime s, int level, CPWeightsConfigSO cfg)
    {
        if (s == null) return 0;

        // Damage per second, ignoring the shared attack cadence: that cadence is
        // the same constant for both sides of any comparison, so it cancels.
        float dps = Mathf.Max(0f, s.attack) * Mathf.Max(0f, s.attackSpeed);

        return Mathf.RoundToInt(dps * EffectiveHP(s) / DisplayDivisor);
    }

    public static int SquadCP(UnitStatsRuntime[] squad, int level, CPWeightsConfigSO cfg)
    {
        if (squad == null) return 0;
        int sum = 0;
        for (int i = 0; i < squad.Length; i++)
            sum += UnitCP(squad[i], level, cfg);
        return sum;
    }

    public static int SquadCP(UnitStatsRuntime[] squad, int[] levels, CPWeightsConfigSO cfg)
    {
        if (squad == null || levels == null || squad.Length != levels.Length) return 0;
        int sum = 0;
        for (int i = 0; i < squad.Length; i++)
            sum += UnitCP(squad[i], levels[i], cfg);
        return sum;
    }

    /// <summary>
    /// How much raw damage this unit can absorb, given that defense reduces every
    /// incoming hit by 100/(100+DEF). Mirrors <see cref="CombatMath.DamagePerHit"/>
    /// exactly, so it reflects the damage the unit will really take in battle.
    /// </summary>
    public static float EffectiveHP(UnitStatsRuntime s)
    {
        if (s == null) return 0f;
        float dmgFrac = 100f / (100f + Mathf.Max(0f, s.defense));
        return s.maxHP / dmgFrac;
    }
}
