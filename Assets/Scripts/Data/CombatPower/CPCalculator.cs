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
    ///
    /// THIS IS THE KNOB FOR "CP SHOULD READ ABOUT 100" (Arash, 2026-09-16).
    /// Stats are tuned for how a FIGHT plays - blows to kill, blows survived - and
    /// must never be bent just to make a CP number look right. When the resulting CP
    /// reads too high or too low, change this divisor instead: it moves every unit
    /// on both sides by the same factor, so no ratio, no ordering and no battle
    /// outcome can shift.
    ///
    /// 200 -> 168 -> 178 -> 190 on 2026-09-16, tracking two stat changes: hero and
    /// level-4 enemy HP raised to reach the 8-blow baseline, then hero ATK +10% so a
    /// lone hero stops losing level 4.
    ///
    /// 190 -> 380 on 2026-09-17, and this one is PURE BOOKKEEPING. Every live combat
    /// unit had its base maxHP DOUBLED (heroes, the level 1-3 tutorial enemies, and
    /// every shared enemy asset) so that nothing at the level-4 base state dies in
    /// fewer than eight blows - it was landing on four. HP is a linear factor of CP,
    /// so doubling the divisor with it leaves every displayed CP bit-identical:
    ///     hero 281->562 HP reads 116 before and after; 291->582 reads 121;
    ///     Reaper at stage 4 reads 92; Zombie 101.
    /// Because BOTH sides were scaled by the same factor, no time-to-kill ratio, no
    /// CP ratio and no battle outcome moved - only the DURATION of a fight, which is
    /// exactly twice what it was.
    ///
    /// Re-derive it the same way whenever the baseline moves:
    ///     divisor = average(ATK x AtkSpd x maxHP x (1 + DEF/100)) / 100
    /// </summary>
    public const float DisplayDivisor = 380f;

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
