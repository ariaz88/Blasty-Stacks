using UnityEngine;


public static class ProgressionMath
{
    public struct Growth
    {
        public float gA;   // Attack
        public float gH;   // HP
        public float gMv;  // Move Speed
        public float gAS;  // Attack Speed
        public float gD;   // Defense
        // Attack Range has no growth: range plays no part in CP or in the duel
        // outcome, and the stat-block value is never read in combat. See A2 in
        // Docs/cp-analysis/NEXT_VERSION_CHANGES.md.
    }

    /// <summary>
    /// Compound percent growth from curves:
    /// product_{l=2..L} (1 + pct(l)), for each stat
    /// </summary>
    public static Growth GetGrowthMultipliers(int level, ProgressionConfigSO cfg)
    {
        Growth g = new Growth { gA = 1f, gH = 1f, gMv = 1f, gAS = 1f, gD = 1f };
        if (cfg == null || level <= 1) return g;

        int L = Mathf.Max(1, level);
        for (int l = 2; l <= L; l++)
        {
            float a = ClampPct(cfg.atkPctByLevel.Evaluate(l), cfg.pctClamp);
            float h = ClampPct(cfg.hpPctByLevel.Evaluate(l), cfg.pctClamp);
            float m = ClampPct(cfg.movePctByLevel.Evaluate(l), cfg.pctClamp);
            float s = ClampPct(cfg.atkSpdPctByLevel.Evaluate(l), cfg.pctClamp);

            float d = ClampPct(cfg.defPctByLevel.Evaluate(l), cfg.pctClamp);

            g.gA *= (1f + a);
            g.gH *= (1f + h);
            g.gMv *= (1f + m);
            g.gAS *= (1f + s);
            g.gD *= (1f + d);
        }
        return g;
    }

    static float ClampPct(float v, Vector2 clampRange)
    {
        return Mathf.Clamp(v, clampRange.x, clampRange.y);
    }
}
