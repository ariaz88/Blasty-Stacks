// Editor-only. The whole file is compiled out of player builds, so shipping
// behaviour is byte-for-byte unchanged.
#if UNITY_EDITOR
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Lets you press Play on ANY scene (a Level_1_Stage_N gameplay scene, the
/// tutorial, a throwaway test scene) instead of always having to start from
/// StarterScene.
///
/// Why it is needed: the DontDestroyOnLoad managers the gameplay scenes depend on
/// only exist in the boot scenes -
///   StarterScene -> GameStartManager, CurrencyManager, AdManager, MenuLoader
///   MenuScene    -> LevelManager
/// so a gameplay scene opened on its own has no GameStartManager, which makes
/// PlayerWaveManager.Awake() log "GameStartManager not found." and bail out
/// (no player units ever spawn), and leaves LevelManager/CurrencyManager null
/// for every stage-scaling and HUD lookup.
///
/// How it works: <see cref="DirectPlayMenu"/> (Editor folder) points Unity's
/// EditorSceneManager.playModeStartScene at StarterScene and records which scene
/// you actually had open. StarterScene therefore always boots first and builds
/// its managers exactly like the real game does; this class then intercepts the
/// boot before the splash loader can route to the menu, fills in the one manager
/// StarterScene is missing (LevelManager), points the progression state at the
/// stage you asked for, and loads your scene.
///
/// Net effect: the scene you pressed Play on is the scene you land in, with the
/// same manager set the real game would have handed it.
/// </summary>
public static class DirectPlayBootstrap
{
    /// <summary>SessionState key holding the scene the user pressed Play on.</summary>
    public const string TargetScenePathKey = "BlastyStacks.DirectPlay.TargetScenePath";

    public const string StarterScenePath = "Assets/Scenes/StarterScene.unity";
    public const string MenuScenePath = "Assets/Scenes/MenuScene.unity";

    // "Level_1_Stage_7", and also tolerates the duplicated "Level_1_Stage_1 1".
    private static readonly Regex LevelStagePattern =
        new Regex(@"Level_(\d+)_Stage_(\d+)", RegexOptions.IgnoreCase);

    /// <summary>
    /// Runs after StarterScene's objects have Awake()d but before any Start(),
    /// which is the only window where MenuLoader can still be cancelled and where
    /// LevelManager can be created early enough for the managers that look for it.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RouteToRequestedScene()
    {
        // One-shot: consume the request so a mid-session scene reload can't re-trigger it.
        string targetPath = SessionState.GetString(TargetScenePathKey, string.Empty);
        SessionState.EraseString(TargetScenePathKey);

        if (string.IsNullOrEmpty(targetPath)) return;

        // Nothing to do when the boot scene IS what the user wanted - let the
        // normal StarterScene -> MenuLoader -> MenuScene flow play out untouched.
        if (targetPath == StarterScenePath) return;

        // Safety: only intercept when playModeStartScene actually put us in the
        // boot scene. If it didn't, we are already in the target scene and a second
        // load would just restart it.
        if (SceneManager.GetActiveScene().path != StarterScenePath) return;

        // MenuLoader.Start() is a coroutine that async-loads MenuScene behind an
        // 8 second progress bar. Destroying the component now (Start has not run
        // yet) cancels that route without touching the scene asset.
        foreach (var loader in Object.FindObjectsByType<MenuLoader>(FindObjectsSortMode.None))
            Object.Destroy(loader);

        // LevelManager normally arrives with MenuScene. Its serialized values in
        // MenuScene are all defaults (20 / 1 / 999 / empty scene list), so creating
        // one here reproduces it exactly. Skipped when MenuScene is the target,
        // so that scene's own instance stays authoritative.
        if (targetPath != MenuScenePath)
            EnsureLevelManager();

        if (TryParseLevelStage(targetPath, out int levelId, out int stage1Based))
            PrepareProgressionForStage(levelId, stage1Based);

        Debug.Log($"[DirectPlay] Booted managers from StarterScene, now loading '{targetPath}'. " +
                  "Turn this off with Tools/Testing/Play Any Scene Directly.");

        // LoadSceneInPlayMode rather than SceneManager.LoadScene: stages 8-20 are
        // present in Build Settings but disabled, and LoadScene cannot reach those.
        EditorSceneManager.LoadSceneInPlayMode(
            targetPath, new LoadSceneParameters(LoadSceneMode.Single));
    }

    private static void EnsureLevelManager()
    {
        if (LevelManager.Instance) return;

        // AddComponent on an active GameObject runs Awake() immediately, which is
        // what sets Instance and marks it DontDestroyOnLoad.
        var go = new GameObject("LevelManager (DirectPlay)");
        go.AddComponent<LevelManager>();
    }

    /// <summary>
    /// Mirrors what HomeManager.LoadSelectedStage() does before it loads a stage
    /// scene, so gameplay, stage scaling and the win panel all agree on which
    /// stage this is.
    /// </summary>
    private static void PrepareProgressionForStage(int levelId, int stage1Based)
    {
        int stagesPerLevel = LevelManager.StagesPerLevel;

        HomeManager.CurrentLevelId = levelId;
        HomeManager.CurrentStage1Based = stage1Based;

        // Unlock everything up to this stage. GameStartManager wipes the save on
        // every boot (resetBool is hard-coded true), so without this a direct run
        // of stage 12 would be sitting on a save that says "stage 1 is as far as
        // you got" - which the win panel and the menu would both act on.
        var progress = SaveSystem.EnsureLevel(levelId, stagesPerLevel);
        int wantedHighest = Mathf.Clamp(stage1Based - 1, 0, stagesPerLevel - 1);
        if (progress.highestUnlocked < wantedHighest)
        {
            progress.highestUnlocked = wantedHighest;
            SaveSystem.Save();
        }

        if (LevelManager.Instance)
        {
            int globalStage = (levelId - 1) * stagesPerLevel + stage1Based;
            LevelManager.Instance.SetStage(globalStage, loadScene: false);
        }
    }

    private static bool TryParseLevelStage(string scenePath, out int levelId, out int stage1Based)
    {
        levelId = 1;
        stage1Based = 1;

        var match = LevelStagePattern.Match(scenePath);
        if (!match.Success) return false;

        return int.TryParse(match.Groups[1].Value, out levelId)
            && int.TryParse(match.Groups[2].Value, out stage1Based);
    }
}
#endif
