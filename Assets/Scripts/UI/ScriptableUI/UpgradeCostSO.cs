using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Progression/Upgrade Cost", fileName = "UpgradeCostSO")]
public class UpgradeCostSO : ScriptableObject
{

    // *** It’s a ScriptableObject that defines how many gems an upgrade costs to go from Level L → L+1. ***
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

    [Header("Geometric")]
    [Tooltip("Applied only in Geometric mode.")]
    [Range(1.0f, 2.0f)] public float ratio = 1.20f;

    [Header("Piecewise Geometric")]
    [Tooltip("Only used in PiecewiseGeometric mode: segments specifying which ratio applies per level range.")]
    public List<Piece> pieces = new List<Piece>
    {
        new Piece{ fromLevelInclusive = 1, toLevelInclusive = 5,  ratio = 1.22f },
        new Piece{ fromLevelInclusive = 6, toLevelInclusive = 10, ratio = 1.20f },
        new Piece{ fromLevelInclusive = 11, toLevelInclusive = 20, ratio = 1.18f },
        new Piece{ fromLevelInclusive = 21, toLevelInclusive = 50, ratio = 1.16f },
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
    /// Returns the gem cost to upgrade from currentLevel → currentLevel+1.
    /// If at level cap, returns 0 (no upgrade available).
    /// </summary>
    public int GetCostForLevel(int currentLevel)
    {
        if (IsAtCap(currentLevel)) return 0;

        int L = Mathf.Max(1, currentLevel);
        switch (mode)
        {
            case Mode.Geometric:
                // cost(L) = ceil(base * ratio^(L-1))
                return Mathf.CeilToInt(baseCost * Mathf.Pow(ratio, L - 1));

            case Mode.PiecewiseGeometric:
                // cost(L) = ceil(base * Π_{k=1..L-1} ratioAt(k))
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
