// StageSpawnerIndexBuilder.cs
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds <see cref="StageSpawnerIndexSO"/> by reading each stage scene's own
/// EnemySpawner, and prints the enemy CP the Home screen will show.
///
/// It opens the scenes rather than guessing from asset names on purpose: the
/// name mapping is wrong for stages 1-2 (they share "Spawner2"), and any future
/// stage that reuses another stage's config would be wrong too. The scene is the
/// only place that actually decides.
/// </summary>
public static class StageSpawnerIndexBuilder
{
    private const string IndexAssetPath = "Assets/Resources/StageSpawnerIndex.asset";
    private const string StageScenesFolder = "Assets/Scenes/TestScenes/GamePlay Scenes";

    // "Level_1_Stage_7.unity". Anchored on ".unity" so the stray duplicate
    // "Level_1_Stage_1 1.unity" is not treated as a real stage - same rule
    // DirectPlayMenu uses.
    private static readonly Regex StagePattern =
        new Regex(@"Level_(\d+)_Stage_(\d+)\.unity$", RegexOptions.IgnoreCase);

    [MenuItem("Tools/Blasty/Stage CP/Rebuild Stage Spawner Index", priority = 0)]
    private static void Rebuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Rebuild Stage Spawner Index",
                "Exit Play mode first - this opens every stage scene.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var setup = EditorSceneManager.GetSceneManagerSetup();
        var found = new Dictionary<int, LevelConfig>();
        var log = new StringBuilder();

        try
        {
            foreach (var path in Directory.GetFiles(StageScenesFolder, "*.unity", SearchOption.AllDirectories))
            {
                string unified = path.Replace('\\', '/');
                var m = StagePattern.Match(unified);
                if (!m.Success) continue;
                if (!int.TryParse(m.Groups[2].Value, out int stage)) continue;

                var scene = EditorSceneManager.OpenScene(unified, OpenSceneMode.Single);

                LevelConfig cfg = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var spawner = root.GetComponentInChildren<EnemySpawner>(true);
                    if (spawner && spawner.levelConfig) { cfg = spawner.levelConfig; break; }
                }

                found[stage] = cfg;
                log.Append("  stage ").Append(stage).Append(" -> ")
                   .Append(cfg ? cfg.name : "<NO SPAWNER CONFIG>").Append('\n');
            }
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }

        if (found.Count == 0)
        {
            Debug.LogError($"[StageSpawnerIndex] No stage scenes found under '{StageScenesFolder}'.");
            return;
        }

        int highest = 0;
        foreach (var kv in found) if (kv.Key > highest) highest = kv.Key;

        Directory.CreateDirectory(Path.GetDirectoryName(IndexAssetPath));
        var index = AssetDatabase.LoadAssetAtPath<StageSpawnerIndexSO>(IndexAssetPath);
        bool isNew = index == null;
        if (isNew) index = ScriptableObject.CreateInstance<StageSpawnerIndexSO>();

        index.byStage = new LevelConfig[highest];
        foreach (var kv in found)
            if (kv.Key >= 1 && kv.Key <= highest) index.byStage[kv.Key - 1] = kv.Value;

        if (isNew) AssetDatabase.CreateAsset(index, IndexAssetPath);
        EditorUtility.SetDirty(index);
        AssetDatabase.SaveAssets();
        StageSpawnerIndexSO.ClearCache();

        Debug.Log($"[StageSpawnerIndex] {(isNew ? "Created" : "Updated")} '{IndexAssetPath}' " +
                  $"for {found.Count} stage(s):\n{log}");

        PrintStageCP();
    }

    /// <summary>
    /// Prints what Home will show for every indexed stage, so the numbers can be
    /// sanity-checked against the authored roster without entering Play mode.
    /// </summary>
    [MenuItem("Tools/Blasty/Stage CP/Print Stage Enemy CP", priority = 20)]
    private static void PrintStageCP()
    {
        StageSpawnerIndexSO.ClearCache();
        var index = StageSpawnerIndexSO.Get();
        if (!index) return;

        var sb = new StringBuilder("[StageSpawnerIndex] Enemy CP the Home screen will show:\n");
        sb.Append("  stage   config                enemies   TOTAL CP\n");

        for (int stage = 1; stage <= index.byStage.Length; stage++)
        {
            var cfg = index.ConfigFor(stage);
            int cp = StageEnemyCP.TotalForStage(stage, cfg, index.cpWeights);
            int n = StageEnemyCP.CountForStage(stage, cfg);

            sb.Append("   ").Append(stage.ToString().PadLeft(2))
              .Append("     ").Append((cfg ? cfg.name : "<none>").PadRight(20))
              .Append("  ").Append(n.ToString().PadLeft(5))
              .Append("   ").Append(cp.ToString().PadLeft(8)).Append('\n');
        }

        Debug.Log(sb.ToString());
    }
}
