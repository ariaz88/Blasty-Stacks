using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Campaign stages 1-5 only. Spreadsheet "Level" means stage, not chapter.</summary>
public static class LevelBattleRules
{
    private static readonly int[][] Deployments =
    {
        new[] { 1, 1, 1 }, new[] { 1, 1, 2 }, new[] { 1, 1, 2, 1 },
        new[] { 1, 1, 2, 4 }, new[] { 1, 1, 2, 2, 2, 4 }
    };
    public static bool AppliesTo(int level) => level >= 1 && level <= Deployments.Length;
    public static int TotalPairs(int level) => AppliesTo(level) ? Deployments[level - 1].Length : 0;
    public static int HeroesForMatch(int level, int match) =>
        AppliesTo(level) && match >= 1 && match <= TotalPairs(level) ? Deployments[level - 1][match - 1] : 0;
    public static int TotalHeroes(int level, int matches)
    {
        int total = 0;
        for (int i = 1; i <= Math.Min(matches, TotalPairs(level)); i++) total += HeroesForMatch(level, i);
        return total;
    }
    public static int FirstWinningMatch(int level) => !AppliesTo(level) ? 0 : level == 4 ? 3 : level == 5 ? 4 : 1;
    public static double ReferenceEnemyCP(int level) => level == 1 ? 80 : level == 2 ? 100 : level == 3 ? 115 : level == 4 ? 400 : level == 5 ? 650 : 0;
    public static int ResolveLevel(GameObject owner, LevelConfig config)
    {
        string name = owner.scene.name;
        int marker = name.LastIndexOf("_Stage_", StringComparison.Ordinal);
        if (marker >= 0 && int.TryParse(name.Substring(marker + 7), out int stage)) return stage;
        return config ? config.levelNumber : LevelManager.CurrentStage;
    }
}
