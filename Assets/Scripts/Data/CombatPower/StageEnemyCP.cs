// StageEnemyCP.cs
using UnityEngine;

/// <summary>
/// "How strong is the enemy side of stage N?" - answered WITHOUT loading the
/// stage, so the Home screen can show the real number for the stage the player
/// is about to start (Arash, 2026-09-22).
///
/// It is a preview, not a second implementation of anything:
///   - the WAVE SHAPE comes from EnemySpawner.ResolveWavesForStage, the same
///     call the spawner itself makes;
///   - each enemy's STATS are built the same way EnemyManager.RebuildFromBase
///     builds them (base UnitStatsSO, then that type's own ProgressionConfigSO
///     growth evaluated AT THE STAGE NUMBER - which is what makes a Reaper at
///     stage 9 stronger than the same Reaper at stage 3);
///   - the SCORE is CPCalculator.UnitCP, the same one the battle reports.
/// Change any of those and this preview follows automatically.
///
/// Two things it deliberately does NOT model, because neither is known before
/// the battle runs: the stage-6 full-clear bonus enemy (decided when the final
/// wave spawns, from how much of the board the player cleared) and any runtime
/// multiplier applied to a spawned enemy. Both make the real fight equal to or
/// harder than this number, never easier.
/// </summary>
public static class StageEnemyCP
{
    /// <summary>
    /// Total CP of every enemy stage <paramref name="stage1Based"/> will field.
    /// Returns 0 when the stage has no config, which the caller should treat as
    /// "unknown" rather than "harmless".
    /// </summary>
    public static int TotalForStage(int stage1Based, LevelConfig config, CPWeightsConfigSO weights)
    {
        if (!config || stage1Based < 1) return 0;

        int total = 0;

        foreach (var wave in EnemySpawner.ResolveWavesForStage(config, stage1Based))
        {
            if (wave?.entries == null) continue;

            foreach (var entry in wave.entries)
            {
                if (entry == null || entry.count <= 0) continue;

                int unitCp = UnitCpFor(entry, stage1Based, weights);
                if (unitCp > 0) total += unitCp * entry.count;
            }
        }

        return total;
    }

    /// <summary>
    /// How many enemies the stage fields. Useful next to the CP - 1400 CP across
    /// four enemies is a very different fight from 1400 across eleven.
    /// </summary>
    public static int CountForStage(int stage1Based, LevelConfig config)
    {
        if (!config || stage1Based < 1) return 0;

        int count = 0;
        foreach (var wave in EnemySpawner.ResolveWavesForStage(config, stage1Based))
        {
            if (wave?.entries == null) continue;
            foreach (var entry in wave.entries)
                if (entry != null && entry.count > 0) count += entry.count;
        }
        return count;
    }

    /// <summary>
    /// One enemy's CP at this stage. Mirrors EnemyManager.RebuildFromBase steps
    /// 1, 2 and 5; step 3 (runtime multipliers) and step 4 (pushing HP into the
    /// health bar) have no meaning before the enemy exists.
    /// </summary>
    private static int UnitCpFor(WaveEntry entry, int stage1Based, CPWeightsConfigSO weights)
    {
        // The wave entry may override the prefab's stats asset; the spawner
        // honours that override, so the preview has to as well.
        var statsSo = entry.statsBase;
        ProgressionConfigSO progression = null;

        if (entry.enemyPrefab)
        {
            var manager = entry.enemyPrefab.GetComponent<EnemyManager>();
            if (manager)
            {
                if (!statsSo) statsSo = manager.statsBase;
                progression = manager.progression;
                if (!weights) weights = manager.cpWeights;
            }
        }

        if (!statsSo) return 0;

        var stats = new UnitStatsRuntime();
        stats.FromSO(statsSo);

        if (progression)
        {
            var g = ProgressionMath.GetGrowthMultipliers(stage1Based, progression);
            stats.attack *= g.gA;
            stats.defense *= g.gD;
            stats.maxHP *= g.gH;
            stats.attackSpeed *= g.gAS;
        }

        return CPCalculator.UnitCP(stats, stage1Based, weights);
    }
}
