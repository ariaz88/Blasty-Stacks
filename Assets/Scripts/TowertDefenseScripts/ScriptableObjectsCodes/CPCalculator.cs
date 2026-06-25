// CPCalculator.cs
using UnityEngine;

public static class CPCalculator
{
    /// <summary>
    /// Compute CP for a single unit, using live stats (UnitStatsRuntime),
    /// a level (for CP-weights evaluation), and a curve-based CP weight config.
    /// </summary>
    public static int UnitCP(UnitStatsRuntime s, int level, CPWeightsConfigSO cfg)
    {
        if (s == null || cfg == null) return 0;

        var w = CPWeightMath.Evaluate(level, cfg);

        // Weighted sum of live stats (now includes Range)
        float baseScore =
              w.wA * s.attack
            + w.wH * s.maxHP
            + w.wMv * s.moveSpeed
            + w.wAS * s.attackSpeed
            + w.wD * s.defense
            + w.wR * s.attackRange;   // NEW

        // Map 4 classes → melee / ranged
        bool isRanged = s.type == FighterType.Archer || s.type == FighterType.Mage;
        float typeMult = isRanged ? w.rangedMult : w.meleeMult;

        return Mathf.RoundToInt(baseScore * typeMult);
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

    public static float EffectiveHP(UnitStatsRuntime s)
    {
        float dmgFrac = 100f / (100f + Mathf.Max(0f, s.defense));
        return s.maxHP / dmgFrac;
    }
}
