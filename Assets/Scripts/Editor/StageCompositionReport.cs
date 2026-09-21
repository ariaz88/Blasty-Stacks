// StageCompositionReport.cs
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// EDITOR ONLY, READ ONLY. Prints, for a range of stages, exactly what
/// EnemySpawner will field: wave counts, enemy types, each unit's stage-scaled
/// stats and CP, the wave's total CP, and the X positions the grid layout will
/// put them at.
///
/// It exists because all three of those were previously only observable by
/// playing the stage and counting - which is how "level 6 has 4 enemies" and a
/// spacing complaint both had to be diagnosed by eye.
///
/// It authors NOTHING. Every number here is recomputed from the same assets and
/// the same code paths the game uses at runtime.
/// </summary>
public static class StageCompositionReport
{
    [MenuItem("Tools/Testing/Report Stage Composition (1-10)", priority = 40)]
    private static void Report1To10() => Report(1, 10);

    [MenuItem("Tools/Testing/Report Stage Composition (stage 6 only)", priority = 41)]
    private static void ReportStage6() => Report(6, 6);

    private static void Report(int first, int last) => Debug.Log(Build(first, last));

    /// <summary>The report as text, so tooling can read it without the console.</summary>
    public static string Build(int first, int last)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[StageCompositionReport] stages {first}-{last}");
        sb.AppendLine($"CP = ATK x AtkSpd x maxHP x (1 + DEF/100) / {CPCalculator.DisplayDivisor}");
        sb.AppendLine();

        for (int stage = first; stage <= last; stage++)
        {
            var config = LoadStageConfig(stage);
            if (!config) { sb.AppendLine($"stage {stage}: no Stage_{stage:00}.asset"); continue; }

            var counts = LevelBattleRules.EnemyWaveCounts(stage);
            sb.AppendLine($"--- stage {stage} ({config.name}) " +
                          $"rules say [{(counts == null ? "unauthored" : string.Join(",", counts))}] ---");

            double stageCP = 0;
            int stageUnits = 0;
            int waveCount = counts?.Length ?? config.waves.Count;

            for (int w = 0; w < waveCount; w++)
            {
                var template = config.waves[Mathf.Min(w, config.waves.Count - 1)];
                int want = counts != null ? counts[w] : TemplateTotal(template);

                var types = SpreadTypes(template, want);
                var xs = GridXOffsets(template, types.Count);

                double waveCP = 0;
                sb.AppendLine($"  wave {w + 1}: {types.Count} enem{(types.Count == 1 ? "y" : "ies")}" +
                              $"  (live box width {LiveBoxWidth(template)}," +
                              $" gridColumns {template.gridColumns}," +
                              $" minSlotSpacing.x {template.minSlotSpacing.x})");

                for (int i = 0; i < types.Count; i++)
                {
                    var stats = ScaledStats(types[i], stage);
                    double cp = CPCalculator.UnitPower(stats);
                    waveCP += cp;

                    sb.AppendLine($"    x centre{xs[i],+7:0.00}  {types[i].name,-28}" +
                                  $" ATK {stats.attack,7:0.00}  AS {stats.attackSpeed,5:0.000}" +
                                  $"  HP {stats.maxHP,7:0.0}  DEF {stats.defense,6:0.00}" +
                                  $"  CP {cp,7:0.0}");
                }

                if (xs.Count > 1)
                {
                    float gap = Mathf.Abs(xs[1] - xs[0]);
                    sb.AppendLine($"    spacing between adjacent enemies: {gap:0.00} world units");
                }

                sb.AppendLine($"    wave CP {waveCP:0.0}");
                stageCP += waveCP;
                stageUnits += types.Count;
            }

            sb.AppendLine($"  TOTAL stage {stage}: {stageUnits} enemies, CP {stageCP:0.0}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// The width the spawner will ACTUALLY lay out inside.
    ///
    /// LevelTemplate.prefab has spawnRelativeToEnemyGate ON, so for every stage
    /// built on it the live box is gateRelativeMin/Max measured from the enemy
    /// gate, and each Wave's own spawnMin/spawnMax is never read. Reporting the
    /// asset's width instead would be wrong wherever the two differ - and they
    /// differ a lot: 27 authored against 6 actually used. That mattered: with a
    /// 6-wide box a 4-enemy wave cannot hold 3-unit spacing and gets compressed
    /// to 2, which the asset width would have hidden.
    ///
    /// Falls back to the wave's own width if the template can't be read.
    /// </summary>
    private static float LiveBoxWidth(Wave wave)
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/PREFABS/Level Template/LevelTemplate.prefab");

        var spawner = template ? template.GetComponentInChildren<EnemySpawner>(true) : null;
        if (spawner)
        {
            var so = new SerializedObject(spawner);
            if (so.FindProperty("spawnRelativeToEnemyGate").boolValue)
            {
                float lo = so.FindProperty("gateRelativeMin").vector2Value.x;
                float hi = so.FindProperty("gateRelativeMax").vector2Value.x;
                return Mathf.Abs(hi - lo);
            }
        }
        return Mathf.Abs(wave.spawnMax.x - wave.spawnMin.x);
    }

    private static LevelConfig LoadStageConfig(int stage)
    {
        string path = $"Assets/Scriptable Objects/Spawner/Stage_{stage:00}.asset";
        return AssetDatabase.LoadAssetAtPath<LevelConfig>(path);
    }

    private static int TemplateTotal(Wave w)
    {
        int n = 0;
        foreach (var e in w.entries) if (e != null && e.enemyPrefab) n += e.count;
        return n;
    }

    /// <summary>
    /// Mirrors EnemySpawner.CopyWaveWithTotal's even spread, then
    /// SpawnAllTogetherGrid's round-robin interleave, so the order here is the
    /// order the spawner produces - which is what maps onto the X positions.
    /// </summary>
    private static List<UnitStatsSO> SpreadTypes(Wave template, int total)
    {
        var sources = new List<WaveEntry>();
        foreach (var e in template.entries)
            if (e != null && e.enemyPrefab && e.statsBase) sources.Add(e);

        var perEntry = new int[sources.Count];
        if (sources.Count > 0 && total > 0)
        {
            int authored = 0;
            foreach (var e in sources) authored += Mathf.Max(0, e.count);

            if (authored == total)
            {
                // Authored mix is used verbatim - same rule as CopyWaveWithTotal.
                for (int i = 0; i < sources.Count; i++) perEntry[i] = Mathf.Max(0, sources[i].count);
            }
            else
            {
                int each = total / sources.Count;
                int remainder = total % sources.Count;
                for (int i = 0; i < sources.Count; i++) perEntry[i] = each + (i < remainder ? 1 : 0);
            }
        }

        var order = new List<UnitStatsSO>();
        int max = 0;
        foreach (int n in perEntry) if (n > max) max = n;
        for (int i = 0; i < max; i++)
            for (int k = 0; k < sources.Count; k++)
                if (i < perEntry[k]) order.Add(sources[k].statsBase);

        return order;
    }

    /// <summary>
    /// Mirrors EnemySpawner.GenerateGridPositions for the X axis, as OFFSETS FROM
    /// THE BOX CENTRE.
    ///
    /// Offsets, not absolute X, because LevelTemplate.prefab has
    /// spawnRelativeToEnemyGate ON: the live box is gateRelativeMin/Max measured
    /// from the enemy gate, and the Wave's own spawnMin/spawnMax are not used at
    /// all for those stages. The box WIDTH and the spacing are the same either
    /// way, and the layout is centred, so offsets are true in both cases while an
    /// absolute X would be fiction for every stage on the template.
    /// </summary>
    private static List<float> GridXOffsets(Wave wave, int count)
    {
        var xs = new List<float>(count);
        if (count <= 0) return xs;

        float w = Mathf.Max(0f, LiveBoxWidth(wave));

        int cols = wave.gridColumns > 0 ? wave.gridColumns : Mathf.CeilToInt(Mathf.Sqrt(count));
        cols = Mathf.Clamp(cols, 1, count);
        int rows = Mathf.CeilToInt(count / (float)cols);

        float dx = SlotSpacing(w, cols, wave.minSlotSpacing.x);

        int placed = 0;
        for (int r = 0; r < rows && placed < count; r++)
        {
            int inRow = Mathf.Min(cols, count - placed);
            float rowStart = -(inRow - 1) * dx * 0.5f;
            for (int c = 0; c < inRow; c++) { xs.Add(rowStart + c * dx); placed++; }
        }
        return xs;
    }

    /// <summary>Mirrors EnemySpawner.SlotSpacing.</summary>
    private static float SlotSpacing(float span, int slots, float desired)
    {
        if (slots <= 1) return 0f;
        float widestThatFits = span / (slots - 1);
        if (desired <= 0f) return widestThatFits;
        return Mathf.Min(desired, widestThatFits);
    }

    /// <summary>
    /// The same build EnemyManager.RebuildFromBase does: base SO values times the
    /// compounded EnemyProgression growth for that stage.
    /// </summary>
    private static UnitStatsRuntime ScaledStats(UnitStatsSO so, int stage)
    {
        var s = new UnitStatsRuntime();
        s.FromSO(so);

        var progression = AssetDatabase.LoadAssetAtPath<ProgressionConfigSO>(
            "Assets/Scriptable Objects/Stats/Progression/EnemyProgression.asset");

        if (progression)
        {
            var g = ProgressionMath.GetGrowthMultipliers(stage, progression);
            s.attack *= g.gA;
            s.defense *= g.gD;
            s.maxHP *= g.gH;
            s.attackSpeed *= g.gAS;
        }
        return s;
    }
}
