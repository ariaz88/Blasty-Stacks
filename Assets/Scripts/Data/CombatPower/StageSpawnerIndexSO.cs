// StageSpawnerIndexSO.cs
using UnityEngine;

/// <summary>
/// Which LevelConfig each stage fights, in one asset the MENU can read.
///
/// WHY THIS EXISTS. A stage's enemies are authored on the EnemySpawner inside
/// that stage's SCENE, so the only way to know stage 7's roster is normally to
/// load Level_1_Stage_7. The Home screen has to show that number before the
/// player starts the stage, and it cannot load twenty scenes to do it.
///
/// The mapping is NOT derivable from the name: stages 1 and 2 share one config
/// ("Spawner2") while stages 3+ use their own Stage_NN asset. So it is built
/// from ground truth - Tools/Blasty/Stage CP/Rebuild Stage Spawner Index reads
/// each stage scene's spawner and writes the result here. Re-run it whenever a
/// stage's spawner config changes.
///
/// It lives in Assets/Resources so Resources.Load finds it from any scene with
/// no inspector wiring: a menu that silently shows nothing because someone
/// forgot to drag an asset is worse than one that logs a missing index.
/// </summary>
[CreateAssetMenu(fileName = "StageSpawnerIndex", menuName = "Blasty/Stage Spawner Index", order = 10)]
public class StageSpawnerIndexSO : ScriptableObject
{
    /// <summary>The filename under Assets/Resources, without extension.</summary>
    public const string ResourcePath = "StageSpawnerIndex";

    [Tooltip("Index 0 = stage 1. Rebuilt by Tools/Blasty/Stage CP/Rebuild Stage Spawner Index.")]
    public LevelConfig[] byStage = new LevelConfig[0];

    [Tooltip("CP weights used for the Home preview. Left empty, each enemy " +
             "prefab's own cpWeights is used - which is what the battle does.")]
    public CPWeightsConfigSO cpWeights;

    private static StageSpawnerIndexSO _cached;
    private static bool _lookedUp;

    /// <summary>
    /// The index asset, or null. Cached including the MISS, so a project without
    /// the asset does not hit Resources on every stage the player scrolls past.
    /// </summary>
    public static StageSpawnerIndexSO Get()
    {
        if (_lookedUp) return _cached;

        _lookedUp = true;
        _cached = Resources.Load<StageSpawnerIndexSO>(ResourcePath);

        if (!_cached)
            Debug.LogWarning($"[StageSpawnerIndex] No '{ResourcePath}' in a Resources folder - " +
                             "Home cannot show real enemy CP. Build it with " +
                             "Tools/Blasty/Stage CP/Rebuild Stage Spawner Index.");
        return _cached;
    }

    /// <summary>Config for a 1-based stage, or null when out of range/unassigned.</summary>
    public LevelConfig ConfigFor(int stage1Based)
    {
        int i = stage1Based - 1;
        if (byStage == null || i < 0 || i >= byStage.Length) return null;
        return byStage[i];
    }

    /// <summary>Drops the cache so a rebuild is picked up without a domain reload.</summary>
    public static void ClearCache() { _cached = null; _lookedUp = false; }
}
