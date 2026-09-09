using UnityEngine;


public static class ProgressionMath
{
    public struct Growth
    {
        public float gA;   // Attack
        public float gH;   // HP
        public float gAS;  // Attack Speed
        public float gD;   // Defense

        // There is NO gMv and NO gR.
        //
        // moveSpeed does not grow. Arash's directive (2026-09-10): movement is a
        // fixed 0.3 for every unit on both sides, and is not part of progression
        // at all. Growing it made enemies outpace heroes late in the campaign,
        // because enemy level = stage number while a hero only levels on upgrade.
        //
        // attackRange does not grow either: it plays no part in CP or in the duel
        // outcome, and the stat-block value is never read in combat (A2).
    }

    /// <summary>
    /// Compound percent growth from curves:
    /// product_{l=2..L} (1 + pct(l)), for each stat
    /// </summary>
    public static Growth GetGrowthMultipliers(int level, ProgressionConfigSO cfg)
    {
        Growth g = new Growth { gA = 1f, gH = 1f, gAS = 1f, gD = 1f };
        if (cfg == null || level <= 1) return g;

        int L = Mathf.Max(1, level);
        for (int l = 2; l <= L; l++)
        {
            float a = ClampPct(cfg.atkPctByLevel.Evaluate(l), cfg.pctClamp);
            float h = ClampPct(cfg.hpPctByLevel.Evaluate(l), cfg.pctClamp);
            float s = ClampPct(cfg.atkSpdPctByLevel.Evaluate(l), cfg.pctClamp);
            float d = ClampPct(cfg.defPctByLevel.Evaluate(l), cfg.pctClamp);

            g.gA *= (1f + a);
            g.gH *= (1f + h);
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
