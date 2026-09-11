using System;
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
    /// This comparison is exact only in the ideal continuous-damage model with
    /// equal base cadence. Discrete hits, minimum damage, targeting and travel
    /// can reverse real outcomes. Tutorial assistance is separate from this score.
    ///
    /// moveSpeed and attackRange are deliberately absent - neither changes who
    /// wins a duel. There are no per-stat weights any more: the three factors
    /// enter multiplicatively. This is a strength score, not a physical-combat guarantee.
    ///
    /// <paramref name="level"/> and <paramref name="cfg"/> are kept so existing
    /// call sites still compile, but the formula needs neither.
    /// </summary>
    public static int UnitCP(UnitStatsRuntime s, int level, CPWeightsConfigSO cfg)
    {
        if (!LevelBattleRules.AppliesTo(level))
            return s == null ? 0 : Mathf.RoundToInt(Mathf.Max(0, s.attack) * Mathf.Max(0, s.attackSpeed) * EffectiveHP(s) / DisplayDivisor);
        return DisplayCP(UnitPower(s));
    }

    /// <summary>Unrounded score for comparisons and team budgets. No per-unit rounding loss.</summary>
    public static double UnitPower(UnitStatsRuntime s)
    {
        if (s == null) return 0;
        double cp = Math.Max(0, (double)s.attack) * Math.Max(0, (double)s.attackSpeed)
            * Math.Max(0, (double)s.maxHP) * (1 + Math.Max(0, (double)s.defense) / 100) / DisplayDivisor;
        return double.IsNaN(cp) || double.IsInfinity(cp) ? 0 : cp;
    }

    public static int DisplayCP(double value) => (int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(value)));

    public static double TeamPower(UnitStatsRuntime[] squad)
    {
        double total = 0;
        if (squad != null) foreach (var unit in squad) total += UnitPower(unit);
        return total;
    }

    public static int SquadCP(UnitStatsRuntime[] squad, int level, CPWeightsConfigSO cfg)
    {
        if (!LevelBattleRules.AppliesTo(level))
        {
            int sum = 0;
            if (squad != null) foreach (var unit in squad) sum += UnitCP(unit, level, cfg);
            return sum;
        }
        return DisplayCP(TeamPower(squad));
    }

    public static int SquadCP(UnitStatsRuntime[] squad, int[] levels, CPWeightsConfigSO cfg)
    {
        if (squad == null || levels == null || squad.Length != levels.Length) return 0;
        int sum = 0;
        for (int i = 0; i < squad.Length; i++) sum += UnitCP(squad[i], levels[i], cfg);
        return sum;
    }

    /// <summary>
    /// How much raw damage this unit can absorb, given that defense reduces every
    /// incoming hit by 100/(100+DEF). Mirrors <see cref="CombatMath.DamagePerHit"/>
    /// except when the minimum-one-damage floor applies.
    /// </summary>
    public static float EffectiveHP(UnitStatsRuntime s)
    {
        if (s == null) return 0f;
        float dmgFrac = 100f / (100f + Mathf.Max(0f, s.defense));
        return Mathf.Max(0f, s.maxHP) / dmgFrac;
    }
}
