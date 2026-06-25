using UnityEngine;

// Pure data/config class for stage rewards.
// You set this once (for example on HomeManager) and re-use it anywhere.
[System.Serializable]
public class StageRewardConfig
{
    [Header("Base rewards for Stage 1 (coins, gems, XP)")]
    public WinPanel.RewardValues baseRewardStageValues;

    [Header("Per-stage growth (compounded)")]
    [Tooltip("Extra reward per stage, e.g. 0.10 = +10% per stage (Stage 2 = base * 1.10, Stage 3 ≈ base * 1.21, etc.).")]
    public float perStageBonusPercent = 0.05f;
    [Header("Per-stage growth (compounded)")]
    public float coinsPerStagePercent = 0.05f;
    public float gemsPerStagePercent = 0.20f; // you want ~20%
    public float xpPerStagePercent = 0.05f;


    [Header("HP tier multipliers")]
    [Tooltip("Multiplier for Tier1 reward (lowest HP case).")]
    public float tier1Multiplier = 1.0f;

    [Tooltip("Multiplier for Tier2 reward.")]
    public float tier2Multiplier = 1.2f;

    [Tooltip("Multiplier for Tier3 reward (full HP).")]
    public float tier3Multiplier = 1.5f;
}

// Shared calculator used by both Home screen and WinPanel.
public static class StageRewardCalculator
{
    public static WinPanel.RewardValues GetRewardForStageAndHpCase(
        int stage1Based,
        int hpCase,
        StageRewardConfig cfg)
    {
        if (cfg == null) return new WinPanel.RewardValues();

        if (stage1Based < 1) stage1Based = 1;
        if (hpCase < 1) hpCase = 1;
        if (hpCase > 3) hpCase = 3;

        float coinMul = Mathf.Pow(1f + cfg.coinsPerStagePercent, stage1Based - 1);
        float gemMul = Mathf.Pow(1f + cfg.gemsPerStagePercent, stage1Based - 1);
        float xpMul = Mathf.Pow(1f + cfg.xpPerStagePercent, stage1Based - 1);

        // Build a stageBase with separate multipliers
        var stageBase = new WinPanel.RewardValues
        {
            coins = Mathf.RoundToInt(cfg.baseRewardStageValues.coins * coinMul),
            gems = Mathf.RoundToInt(cfg.baseRewardStageValues.gems * gemMul),
            heroXP = Mathf.RoundToInt(cfg.baseRewardStageValues.heroXP * xpMul)
        };

        var r1 = WinPanel.RewardValues.FromScaled(stageBase, cfg.tier1Multiplier);
        var r2 = WinPanel.RewardValues.FromScaled(stageBase, cfg.tier2Multiplier);
        var r3 = WinPanel.RewardValues.FromScaled(stageBase, cfg.tier3Multiplier);

        var total = new WinPanel.RewardValues();
        if (hpCase >= 1) total.Add(r1);
        if (hpCase >= 2) total.Add(r2);
        if (hpCase >= 3) total.Add(r3);

        return total;
    }
}

public static class StageRewardCalculator1
{
    // Computes total reward for a given stage (1-based) and HP case (1..3)
    // using the provided StageRewardConfig.
    public static WinPanel.RewardValues GetRewardForStageAndHpCase(
        int stage1Based,
        int hpCase,
        StageRewardConfig cfg)
    {
        if (cfg == null)
        {
            Debug.LogWarning("[StageRewardCalculator] Config is null. Returning zero rewards.");
            return new WinPanel.RewardValues();
        }

        if (stage1Based < 1) stage1Based = 1;
        if (hpCase < 1) hpCase = 1;
        if (hpCase > 3) hpCase = 3;

        // Stage multiplier from compounded progression:
        //   Stage 1: base
        //   Stage 2: base * (1+p)
        //   Stage 3: base * (1+p)^2
        float stageMultiplier = Mathf.Pow(1f + cfg.perStageBonusPercent, stage1Based - 1);

        // Scale base reward for this stage
        var stageBase = WinPanel.RewardValues.FromScaled(cfg.baseRewardStageValues, stageMultiplier);

        // Tier rewards
        var r1 = WinPanel.RewardValues.FromScaled(stageBase, cfg.tier1Multiplier);
        var r2 = WinPanel.RewardValues.FromScaled(stageBase, cfg.tier2Multiplier);
        var r3 = WinPanel.RewardValues.FromScaled(stageBase, cfg.tier3Multiplier);

        // Sum tiers according to hpCase (1..3)
        var total = new WinPanel.RewardValues();
        if (hpCase >= 1) total.Add(r1);
        if (hpCase >= 2) total.Add(r2);
        if (hpCase >= 3) total.Add(r3);

        return total;
    }
}

