using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor half of the "press Play on any scene" workflow. See
/// <see cref="DirectPlayBootstrap"/> (Assets/Scripts/Core/Boot/) for the runtime half.
///
/// While enabled it:
///   - points EditorSceneManager.playModeStartScene at StarterScene, so entering
///     Play mode always boots the managers first, whatever scene is open;
///   - records the scene you actually had open, for the runtime half to load.
///
/// Toggle it from Tools/Testing/Play Any Scene Directly. The setting is per-user
/// (EditorPrefs), so it never travels with the repo.
/// </summary>
public static class DirectPlayMenu
{
    private const string ToggleMenuPath = "Tools/Testing/Play Any Scene Directly";
    private const string BuildScenesMenuPath = "Tools/Testing/Enable All Level Scenes In Build Settings";
    private const string EnabledPrefKey = "BlastyStacks.DirectPlay.Enabled";

    // Anchored on ".unity" so the stray duplicate "Level_1_Stage_1 1.unity" in the
    // GamePlay Scenes folder is NOT treated as a real stage.
    private static readonly Regex LevelScenePattern =
        new Regex(@"Level_(\d+)_Stage_(\d+)\.unity$", RegexOptions.IgnoreCase);

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledPrefKey, true);
        set
        {
            EditorPrefs.SetBool(EnabledPrefKey, value);
            ApplyPlayModeStartScene();
        }
    }

    /// <summary>
    /// Re-runs on every domain reload, which is exactly what is needed:
    /// playModeStartScene is a non-serialized editor field and resets to null
    /// each time scripts recompile, so it has to be re-applied every load.
    /// Applied inline rather than through EditorApplication.delayCall - delayCall
    /// is not guaranteed to have fired before the user hits Play, and a null
    /// playModeStartScene would make direct play silently do nothing.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        ApplyPlayModeStartScene();
    }

    [MenuItem(ToggleMenuPath)]
    private static void Toggle() => Enabled = !Enabled;

    [MenuItem(ToggleMenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(ToggleMenuPath, Enabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    private static void ApplyPlayModeStartScene()
    {
        if (!Enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        var starter = AssetDatabase.LoadAssetAtPath<SceneAsset>(DirectPlayBootstrap.StarterScenePath);
        if (starter == null)
        {
            Debug.LogWarning($"[DirectPlay] Boot scene not found at '{DirectPlayBootstrap.StarterScenePath}'. " +
                             "Direct play is off until the path is fixed in DirectPlayBootstrap.");
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        EditorSceneManager.playModeStartScene = starter;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        switch (state)
        {
            // Still in edit mode here, so the active scene is the one the user
            // pressed Play on - playModeStartScene has not swapped it out yet.
            case PlayModeStateChange.ExitingEditMode:
                if (Enabled)
                    SessionState.SetString(DirectPlayBootstrap.TargetScenePathKey,
                                           SceneManager.GetActiveScene().path ?? string.Empty);
                else
                    SessionState.EraseString(DirectPlayBootstrap.TargetScenePathKey);
                break;

            // Don't let a stale request survive into the next Play press.
            case PlayModeStateChange.EnteredEditMode:
                SessionState.EraseString(DirectPlayBootstrap.TargetScenePathKey);
                break;
        }
    }

    /// <summary>
    /// Repairs the Level_* rows in Build Settings.
    ///
    /// Stages 8-20 are NOT merely unticked - their rows are orphans. They point at
    /// "Assets/Scenes/Level_1_Stage_N.unity", where no file exists, carrying GUIDs
    /// that belong to scenes since deleted or replaced (the live scenes live in
    /// "Assets/Scenes/TestScenes/GamePlay Scenes/" with completely different GUIDs).
    /// Unity auto-unticks a row whose asset is missing, which is why they look
    /// "disabled". Just re-ticking them would do nothing.
    ///
    /// So: drop every dead row, add every real Level_*_Stage_* scene found on disk
    /// that is missing, and tick them all. Direct play does not need this (it uses
    /// EditorSceneManager.LoadSceneInPlayMode, which ignores Build Settings), but
    /// SceneManager.LoadScene does - so winning stage 7 and advancing does.
    ///
    /// MANUAL ONLY: this edits what a real build contains, so nothing calls it
    /// automatically.
    /// </summary>
    [MenuItem(BuildScenesMenuPath)]
    private static void EnableAllLevelScenes()
    {
        var rows = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        int dropped = 0, added = 0, ticked = 0;

        // 1) Drop rows whose asset no longer exists (the stage 8-20 orphans).
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(rows[i].path) != null) continue;

            Debug.Log($"[DirectPlay] Dropping dead Build Settings entry '{rows[i].path}'.");
            rows.RemoveAt(i);
            dropped++;
        }

        // 2) Add every real stage scene that is not listed, in stage order.
        var known = new HashSet<string>();
        foreach (var row in rows) known.Add(row.path);

        var missing = new List<(int level, int stage, string path)>();
        foreach (var guid in AssetDatabase.FindAssets("t:SceneAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var match = LevelScenePattern.Match(path);
            if (!match.Success) continue;
            if (!known.Add(path)) continue;

            missing.Add((int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), path));
        }

        // FindAssets returns no meaningful order; append stages in play order so
        // the Build Settings list stays readable.
        missing.Sort((a, b) => a.level != b.level
            ? a.level.CompareTo(b.level)
            : a.stage.CompareTo(b.stage));

        foreach (var entry in missing)
        {
            rows.Add(new EditorBuildSettingsScene(entry.path, true));
            Debug.Log($"[DirectPlay] Added '{entry.path}' to Build Settings.");
            added++;
        }

        // 3) Tick any real stage scene that is listed but unchecked.
        foreach (var row in rows)
        {
            if (row.enabled || !LevelScenePattern.IsMatch(row.path)) continue;

            row.enabled = true;
            ticked++;
        }

        if (dropped == 0 && added == 0 && ticked == 0)
        {
            Debug.Log("[DirectPlay] Build Settings already lists every Level_* stage scene, all enabled.");
            return;
        }

        EditorBuildSettings.scenes = rows.ToArray();
        Debug.Log($"[DirectPlay] Build Settings repaired: {dropped} dead row(s) dropped, " +
                  $"{added} scene(s) added, {ticked} re-enabled.");
    }
}
