using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Progression/Upgrade Cost", fileName = "UpgradeCostSO")]
public class UpgradeCostSO : ScriptableObject
{

    // *** Defines what an upgrade costs to go from Level L → L+1, in BOTH resources:
    //     coins (GetCostForLevel) and Hero XP (GetHeroXpCostForLevel).
    //     The first 8 upgrades come from authored integer tables; past those the coin cost
    //     continues through decaying ratio bands and the XP cost through floor(L/2)+2.
    //     Both wallets are SHARED across heroes, so the rising per-hero cost is what makes
    //     spreading upgrades across the deck cheaper than funnelling them into one hero. ***
    public enum Mode
    {
        Geometric,          // cost(L) = ceil(base * ratio^(L-1))
        PiecewiseGeometric  // cost(L) = ceil(base * Π_{k=1..L-1} ratioAt(k))
    }

    [Header("General")]
    public Mode mode = Mode.Geometric;
    [Min(1)] public int levelCap = 50;
    [Tooltip("Cost for the first upgrade (Level 1 → 2).")]
    [Min(1)] public int baseCost = 2;

    [Header("Hero XP (shared wallet)")]
    [Tooltip("Authored Hero XP cost for the first N upgrades, in order (1->2, 2->3, ...). " +
             "When non-empty this WINS over firstHeroXpCost/heroXpCostStep. Past the end of the " +
             "table the cost continues as floor(level/2) + 2, which extends the authored shape.")]
    public List<int> authoredHeroXpCosts = new List<int> { 1, 2, 3, 4, 4, 5, 5, 6 };
    [Tooltip("Legacy linear fallback, used only when authoredHeroXpCosts is empty.")]
    [Min(1)] public int firstHeroXpCost = 1;
    [Min(1)] public int heroXpCostStep = 1;

    public int GetHeroXpCostForLevel(int currentLevel)
    {
        if (IsAtCap(currentLevel)) return 0;

        int L = Mathf.Max(1, currentLevel);
        if (authoredHeroXpCosts != null && authoredHeroXpCosts.Count > 0)
        {
            if (L <= authoredHeroXpCosts.Count) return Mathf.Max(1, authoredHeroXpCosts[L - 1]);
            // Continues 1,2,3,4,4,5,5,6 -> 6,7,7,8,8,... one more XP every second upgrade.
            return Mathf.Max(1, L / 2 + 2);
        }
        return Mathf.Max(1, firstHeroXpCost) + (L - 1) * Mathf.Max(1, heroXpCostStep);
    }

    [Header("Coins")]
    [Tooltip("Authored coin cost for the first N upgrades, in order (1->2, 2->3, ...). " +
             "When non-empty this WINS over mode/baseCost/ratio. Past the end of the table the " +
             "cost continues from the LAST authored value using the 'pieces' ratio bands below, " +
             "which decay so the curve stays finite up to levelCap.")]
    public List<int> authoredCoinCosts = new List<int> { 50, 80, 200, 340, 580, 1000, 1700, 2900 };

    [Header("Geometric")]
    [Tooltip("Applied only in Geometric mode.")]
    [Range(1.0f, 2.0f)] public float ratio = 1.20f;

    [Header("Piecewise Geometric")]
    [Tooltip("Ratio bands per level range. Used by PiecewiseGeometric mode, and as the TAIL " +
             "that continues authoredCoinCosts past its last entry.")]
    public List<Piece> pieces = new List<Piece>
    {
        new Piece{ fromLevelInclusive = 1,  toLevelInclusive = 15, ratio = 1.50f },
        new Piece{ fromLevelInclusive = 16, toLevelInclusive = 25, ratio = 1.35f },
        new Piece{ fromLevelInclusive = 26, toLevelInclusive = 50, ratio = 1.20f },
    };

    [Serializable]
    public struct Piece
    {
        [Min(1)] public int fromLevelInclusive;
        [Min(1)] public int toLevelInclusive;
        [Range(1.0f, 2.0f)] public float ratio;
    }

    /// <summary>
    /// Returns true if currentLevel is already at or above the cap.
    /// </summary>
    public bool IsAtCap(int currentLevel) => currentLevel >= Mathf.Max(1, levelCap);

    /// <summary>
    /// Returns the COIN cost to upgrade from currentLevel → currentLevel+1.
    /// (Not gems - gems are never spent on unit upgrades; see PlayerProgressionService.TryUpgrade.)
    /// If at level cap, returns 0 (no upgrade available).
    /// </summary>
    public int GetCostForLevel(int currentLevel)
    {
        if (IsAtCap(currentLevel)) return 0;

        int L = Mathf.Max(1, currentLevel);

        // Authored integer table wins for the early upgrades - those are the ones the
        // early-campaign economy is tuned around, and round numbers read better in the UI.
        if (authoredCoinCosts != null && authoredCoinCosts.Count > 0)
        {
            if (L <= authoredCoinCosts.Count) return Mathf.Max(1, authoredCoinCosts[L - 1]);

            // Tail: continue from the last authored value with the decaying band ratios.
            // A flat ~1.7 would reach ~10^13 by level 50; the bands keep it finite.
            float tail = Mathf.Max(1, authoredCoinCosts[authoredCoinCosts.Count - 1]);
            for (int k = authoredCoinCosts.Count + 1; k <= L; k++)
                tail *= GetPiecewiseRatioForLevel(k);
            return Mathf.CeilToInt(tail);
        }

        switch (mode)
        {
            case Mode.Geometric:
                return Mathf.CeilToInt(baseCost * Mathf.Pow(ratio, L - 1));

            case Mode.PiecewiseGeometric:
                float mult = 1f;
                for (int k = 1; k <= L - 1; k++)
                    mult *= GetPiecewiseRatioForLevel(k);
                return Mathf.CeilToInt(baseCost * mult);

            default:
                return baseCost;
        }
    }

    float GetPiecewiseRatioForLevel(int level)
    {
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (level >= p.fromLevelInclusive && level <= p.toLevelInclusive)
                return Mathf.Clamp(p.ratio, 1.0f, 2.0f);
        }
        // Fallback: last piece or 1.0 if nothing matches
        return pieces.Count > 0 ? Mathf.Clamp(pieces[pieces.Count - 1].ratio, 1.0f, 2.0f) : 1.0f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (levelCap < 1) levelCap = 1;
        if (baseCost < 1) baseCost = 1;

        // Authored tables are spent directly as costs - a zero or negative entry would
        // hand out a free upgrade, so clamp both lists here rather than at every read.
        for (int i = 0; authoredCoinCosts != null && i < authoredCoinCosts.Count; i++)
            if (authoredCoinCosts[i] < 1) authoredCoinCosts[i] = 1;
        for (int i = 0; authoredHeroXpCosts != null && i < authoredHeroXpCosts.Count; i++)
            if (authoredHeroXpCosts[i] < 1) authoredHeroXpCosts[i] = 1;

        // Normalize segments (optional guardrails)
        for (int i = 0; i < pieces.Count; i++)
        {
            var p = pieces[i];
            if (p.toLevelInclusive < p.fromLevelInclusive)
                p.toLevelInclusive = p.fromLevelInclusive;
            p.ratio = Mathf.Clamp(p.ratio, 1.0f, 2.0f);
            pieces[i] = p;
        }
    }
#endif
}
