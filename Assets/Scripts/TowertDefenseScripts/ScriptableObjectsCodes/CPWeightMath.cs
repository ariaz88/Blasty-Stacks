// CPWeightMath.cs
using UnityEngine;

public static class CPWeightMath
{
    public struct Weights
    {
        public float wA, wH, wMv, wAS, wD, wR;  // NEW: wR = attackRange weight
        public float meleeMult, rangedMult;
    }

    public static Weights Evaluate(int level, CPWeightsConfigSO cfg)
    {
        Weights w = new Weights
        {
            wA = 1f,
            wH = 0.15f,
            wMv = 0.25f,
            wAS = 0.40f,
            wD = 0f,
            wR = 0.05f,
            meleeMult = 1f,
            rangedMult = 1.05f
        };
        if (cfg == null) return w;

        int L = Mathf.Max(1, level);
        float clampMin = cfg.wClamp.x, clampMax = cfg.wClamp.y;

        float s = Mathf.Clamp(cfg.globalWeightScaleByLevel.Evaluate(L), 1f, 10f);

        w.wA = Mathf.Clamp(cfg.wAttackByLevel.Evaluate(L), clampMin, clampMax) * s;
        w.wH = Mathf.Clamp(cfg.wHPByLevel.Evaluate(L), clampMin, clampMax) * s;
        w.wMv = Mathf.Clamp(cfg.wMoveSpeedByLevel.Evaluate(L), clampMin, clampMax) * s;
        w.wAS = Mathf.Clamp(cfg.wAttackSpeedByLevel.Evaluate(L), clampMin, clampMax) * s;
        w.wD = Mathf.Clamp(cfg.wDefenseByLevel.Evaluate(L), clampMin, clampMax) * s;

        // NEW: range
        w.wR = Mathf.Clamp(cfg.wRangeByLevel.Evaluate(L), clampMin, clampMax) * s;

        //w.meleeMult = Mathf.Clamp(cfg.meleeMultByLevel.Evaluate(L), 0.1f, 5f);
        //w.meleeMult = Mathf.Clamp(cfg.meleeMultByLevel.Evaluate(L), 0.1f, 5f);
        w.rangedMult = Mathf.Clamp(cfg.rangedMultByLevel.Evaluate(L), 1, 5f);
        w.rangedMult = Mathf.Clamp(cfg.rangedMultByLevel.Evaluate(L), 1, 5f);

        return w;
    }
}
