using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Explicit editor authoring only. Never runs during a battle.</summary>
public static class EarlyCampaignAuthoring
{
    const string Root = "Assets/Scriptable Objects/";
    public static UnitsDatabaseSO Database => AssetDatabase.LoadAssetAtPath<UnitsDatabaseSO>(
        AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:UnitsDatabaseSO")[0]));
    public static LevelConfig Stage(int n) => AssetDatabase.LoadAssetAtPath<LevelConfig>(Root + $"Spawner/Stage_{n:00}.asset");
    public static ProgressionConfigSO EnemyGrowth => AssetDatabase.LoadAssetAtPath<ProgressionConfigSO>(Root + "Stats/Progression/EnemyProgression.asset");
    public static ProgressionConfigSO HeroGrowth => AssetDatabase.LoadAssetAtPath<ProgressionConfigSO>(Root + "Stats/Progression/PlayerProgressionConfig.asset");

    [MenuItem("Tools/Balance/Author Stages 5-10")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode first.");
        var growth = EnemyGrowth;
        Undo.RecordObject(growth, "Author enemy growth after stage 4");
        // CP is multiplicative. Split the desired CP increment between ATK/HP;
        // retain every pre-stage-5 sample and the later unauthored curve samples.
        float[] increments = { .10f, .12f, .10f, .07f, .06f, .14f };
        growth.atkPctByLevel = Curve(growth.atkPctByLevel, increments, true);
        growth.hpPctByLevel = Curve(growth.hpPctByLevel, increments, true);
        growth.defPctByLevel = Curve(growth.defPctByLevel, increments, false);
        growth.atkSpdPctByLevel = Curve(growth.atkSpdPctByLevel, increments, false);
        EditorUtility.SetDirty(growth);

        var costs = AssetDatabase.LoadAssetAtPath<UpgradeCostSO>("Assets/Scripts/UI/UI-SOs/UgradeCost-SOs/UpgradeCostSO.asset");
        Undo.RecordObject(costs, "Author coin and hero XP costs");
        // Authored integer tables for the first 8 upgrades; past those the coin cost
        // continues from 2900 through the decaying ratio bands and the XP cost through
        // floor(level/2)+2. baseCost/ratio/firstHeroXpCost/heroXpCostStep are the legacy
        // fallback and are only reached if a table is emptied.
        costs.mode = UpgradeCostSO.Mode.Geometric;
        costs.baseCost = 40; costs.ratio = 1.5f;
        costs.firstHeroXpCost = 1; costs.heroXpCostStep = 1;
        costs.authoredCoinCosts = new System.Collections.Generic.List<int> { 50, 80, 200, 340, 580, 1000, 1700, 2900 };
        costs.authoredHeroXpCosts = new System.Collections.Generic.List<int> { 1, 2, 3, 4, 4, 5, 5, 6 };
        costs.pieces = new System.Collections.Generic.List<UpgradeCostSO.Piece>
        {
            new UpgradeCostSO.Piece{ fromLevelInclusive = 1,  toLevelInclusive = 15, ratio = 1.50f },
            new UpgradeCostSO.Piece{ fromLevelInclusive = 16, toLevelInclusive = 25, ratio = 1.35f },
            new UpgradeCostSO.Piece{ fromLevelInclusive = 26, toLevelInclusive = 50, ratio = 1.20f },
        };
        EditorUtility.SetDirty(costs);

        // Later heroes must not share a base asset with the starting roster.
        var db = Database;
        double strongest = db.Units.Where(d => d.startsDeployed).Max(d => Power(d.baseStats, HeroGrowth, 1));
        for (int id = 5; id <= 8; id++)
        {
            var d = db.GetById(id);
            Undo.RecordObject(d, "Author hero unlock milestone");
            d.requiredLevelIndex = 1;
            d.requiredStageIndexWithinLevel = 8 + (id - 5) * 4;
            var path = Root + $"Stats/BaseStats/New ChratersSTats/Campaign_Hero_{id}.asset";
            var stats = AssetDatabase.LoadAssetAtPath<UnitStatsSO>(path);
            if (!stats) { stats = UnityEngine.Object.Instantiate(d.baseStats); AssetDatabase.CreateAsset(stats, path); }
            // Expected existing-hero upgrade levels at those milestones: 2..5.
            var reference = db.Units.First(x => x.startsDeployed && Math.Abs(Power(x.baseStats, HeroGrowth, 1) - strongest) < .1);
            double target = Power(reference.baseStats, HeroGrowth, id - 3) * 1.10;
            ScaleAttackAndHp(stats, target / Power(stats, null, 1));
            d.baseStats = stats;
            EditorUtility.SetDirty(stats); EditorUtility.SetDirty(d);
        }

        var templates = Stage(4);
        var reaper = templates.waves[0].entries[0];
        var zombie = templates.waves[0].entries[1];
        var orc = FindEntry("Enemy_Orc");
        var skeleton = FindEntry("Enemy_Skeleton_Crusader_1");
        double oldAtSix = Math.Max(Power(reaper.statsBase, growth, 6), Power(zombie.statsBase, growth, 6));
        ScaleAttackAndHp(orc.statsBase, oldAtSix * 1.15 / Power(orc.statsBase, growth, 6));
        EditorUtility.SetDirty(orc.statsBase);
        ScaleAttackAndHp(skeleton.statsBase, Power(orc.statsBase, growth, 9) * 1.15 / Power(skeleton.statsBase, growth, 9));
        EditorUtility.SetDirty(skeleton.statsBase);

        for (int stage = 5; stage <= 10; stage++)
        {
            var cfg = Stage(stage);
            Undo.RecordObject(cfg, "Author stage waves");
            cfg.waves.Clear();
            var counts = LevelBattleRules.EnemyWaveCounts(stage);
            for (int wave = 0; wave < counts.Length; wave++)
            {
                var w = JsonUtility.FromJson<Wave>(JsonUtility.ToJson(templates.waves[Math.Min(wave, 1)]));
                w.name = $"Stage {stage} wave {wave + 1}";
                w.formation = SpawnFormation.AllTogetherGrid;
                w.gridColumns = counts[wave];
                w.minSlotSpacing = new Vector2(3f, .5f);
                w.entries.Clear();
                for (int i = 0; i < counts[wave]; i++)
                {
                    var source = (i + wave) % 2 == 0 ? reaper : zombie;
                    if (stage >= 6 && i == 0 && (wave == 1 || stage >= 7)) source = orc;
                    if (stage >= 9 && i == counts[wave] - 1) source = skeleton;
                    w.entries.Add(new WaveEntry { enemyPrefab = source.enemyPrefab, statsBase = source.statsBase, count = 1, unitLevel = 1 });
                }
                cfg.waves.Add(w);
            }
            EditorUtility.SetDirty(cfg);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[Balance] Authored stages 5-10, costs and unlock milestones. Stage 1-4 assets untouched.");
    }

    static AnimationCurve Curve(AnimationCurve original, float[] increments, bool grow)
    {
        var keys = Enumerable.Range(1, 50).Select(level => new Keyframe(level,
            level >= 5 && level <= 10 ? (grow ? Mathf.Sqrt(1f + increments[level - 5]) - 1f : 0f) : original.Evaluate(level))).ToArray();
        return new AnimationCurve(keys);
    }
    static WaveEntry FindEntry(string prefabName)
    {
        string g = AssetDatabase.FindAssets(prefabName + " t:Prefab", new[] { "Assets/PREFABS/Characters/New Characters/Enemies" }).First();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g));
        var stats = AssetDatabase.LoadAssetAtPath<UnitStatsSO>(Root + $"Stats/BaseStats/Enemy Base Stats/{prefabName}.asset");
        return new WaveEntry { enemyPrefab = prefab, statsBase = stats };
    }
    static void ScaleAttackAndHp(UnitStatsSO stats, double factor)
    {
        Undo.RecordObject(stats, "Author base combat stats");
        float scale = Mathf.Sqrt((float)factor);
        stats.attack *= scale; stats.maxHP *= scale;
    }
    public static double Power(UnitStatsSO stats, ProgressionConfigSO progression, int level)
    {
        var rt = new UnitStatsRuntime(); rt.FromSO(stats);
        var g = ProgressionMath.GetGrowthMultipliers(level, progression);
        rt.attack *= g.gA; rt.maxHP *= g.gH; rt.defense *= g.gD; rt.attackSpeed *= g.gAS;
        return CPCalculator.UnitPower(rt);
    }
}
