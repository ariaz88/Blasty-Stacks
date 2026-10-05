using UnityEngine;

/// <summary>
/// Which stages actually SHIP in this build.
///
/// The campaign has 20 stages, but a build may carry only some of them (the first
/// release ships stages 1-10; 11-20 stay in the project, unticked in the build
/// list). Anything that is about to load a stage asks here first, and sends the
/// player to the "more stages coming" screen instead of trying to load a scene
/// that is not in the build.
///
/// The answer comes from the build list itself (Application.CanStreamedLevelBeLoaded),
/// so ticking a stage back in is the only thing a later release needs to do.
/// </summary>
public static class StageBuildAvailability
{
    /// <summary>The "you cleared everything in this build" scene.</summary>
    public const string ComingSoonScene = "ComingSoonScene";

    /// <summary>Same pattern HomeManager.sceneNamePattern uses ("Level_{0}_Stage_{1}").</summary>
    public static string StageSceneName(int levelId, int stage1Based) => $"Level_{levelId}_Stage_{stage1Based}";

    public static bool IsSceneInBuild(string sceneName)
        => !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);

    public static bool IsStageInBuild(int levelId, int stage1Based)
        => IsSceneInBuild(StageSceneName(levelId, stage1Based));

    /// <summary>Global stage index (1-based across all levels), as LevelManager counts it.</summary>
    public static bool IsGlobalStageInBuild(int globalStage)
    {
        int per = Mathf.Max(1, LevelManager.StagesPerLevel);
        int g = Mathf.Max(1, globalStage);
        return IsStageInBuild((g - 1) / per + 1, (g - 1) % per + 1);
    }

    /// <summary>True once the coming-soon scene is in the build - otherwise callers keep their old route.</summary>
    public static bool ComingSoonAvailable => IsSceneInBuild(ComingSoonScene);
}
