// BattleBalanceProbe.cs  (Editor only)
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Plays ONE real stage battle unattended and records who won, so stage tuning
/// is judged by the actual combat simulation rather than by CP arithmetic.
///
///   BattleBalanceProbe.Begin(stage: 10, matches: 7, heroLevel: 2);
///   ... wait ...  BattleBalanceProbe.Status / BattleBalanceProbe.Results
///   BattleBalanceProbe.RestoreSave();   // when finished
///
/// One run: (1) the real save is backed up once (SessionState) and the four
/// deployed heroes' levels are overwritten with heroLevel; (2) the stage scene
/// is opened and played; (3) `matches` board matches are credited exactly the way
/// MatchResolver does it (PlayerWaveManager.HandleBlast); (4) once every earned
/// hero has been dealt, BATTLE is pressed; (5) the battle runs at 4x speed until
/// LevelGameManager reports Won or Lost; (6) the result is appended to Results
/// and Play mode stops.
///
/// State lives in SessionState because entering/leaving Play mode reloads the
/// script domain. RestoreSave puts the backed-up save back byte for byte.
/// </summary>
[InitializeOnLoad]
public static class BattleBalanceProbe
{
    private const string KPhase = "BBP.phase", KStage = "BBP.stage", KMatches = "BBP.matches",
                         KLevel = "BBP.level", KResults = "BBP.results", KBackup = "BBP.backup",
                         KStartT = "BBP.startT";
    private const string SaveKey = "GAME_SAVE_V1";
    private const float Speed = 4f;
    private const float TimeoutGameSeconds = 420f;

    /// <summary>Every result line is also appended here, for tooling that waits on a file.</summary>
    public static string LogPath => System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Temp", "BalanceProbe.log");

    public static string Status => SessionState.GetString(KPhase, "idle");
    public static string Results => SessionState.GetString(KResults, "");

    static BattleBalanceProbe() => EditorApplication.update += Tick;

    public static void ClearResults() => SessionState.SetString(KResults, "");

    public static void Begin(int stage, int matches, int heroLevel)
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Probe] Stop Play mode first."); return; }

        string save = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(SessionState.GetString(KBackup, "")))
            SessionState.SetString(KBackup, save);

        // Every run starts from the BACKED-UP save (a won run writes stars/coins),
        // with the deployed heroes' levels overwritten.
        string baseSave = SessionState.GetString(KBackup, save);
        string patched = Regex.Replace(baseSave,
            "(\"unitId\":(\\d+),\"unlocked\":true,\"level\":)(\\d+)(,\"isDeployed\":true)",
            m => m.Groups[1].Value + heroLevel + m.Groups[4].Value);
        PlayerPrefs.SetString(SaveKey, patched);
        PlayerPrefs.Save();

        SessionState.SetInt(KStage, stage);
        SessionState.SetInt(KMatches, matches);
        SessionState.SetInt(KLevel, heroLevel);
        SessionState.SetString(KPhase, "starting");

        EditorSceneManager.OpenScene($"Assets/Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_{stage}.unity",
                                     OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void File_AppendQueueEnd() =>
        System.IO.File.AppendAllText(LogPath, "QUEUE DONE\n");

    public static void RestoreSave()
    {
        string backup = SessionState.GetString(KBackup, "");
        if (string.IsNullOrEmpty(backup)) { Debug.Log("[Probe] No backup to restore."); return; }
        PlayerPrefs.SetString(SaveKey, backup);
        PlayerPrefs.Save();
        SessionState.EraseString(KBackup);
        Debug.Log("[Probe] Save restored.");
    }

    private const string KQueue = "BBP.queue";

    /// <summary>
    /// Queues runs as "stage,matches,heroLevel;stage,matches,heroLevel;..." and
    /// starts the first. Each next run starts once the previous one has left Play mode.
    /// </summary>
    public static void BeginQueue(string runs)
    {
        SessionState.SetString(KQueue, runs ?? "");
        SessionState.SetString(KPhase, "done");
        StartNextQueued();
    }

    private static bool StartNextQueued()
    {
        string q = SessionState.GetString(KQueue, "");
        if (string.IsNullOrWhiteSpace(q)) return false;

        int cut = q.IndexOf(';');
        string head = cut < 0 ? q : q.Substring(0, cut);
        SessionState.SetString(KQueue, cut < 0 ? "" : q.Substring(cut + 1));

        var p = head.Split(',');
        Begin(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
        return true;
    }

    private static void Tick()
    {
        string phase = Status;
        if (phase == "idle") return;
        if (phase == "done")
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (StartNextQueued()) return;

            // Queue empty: report once and go idle, so this branch never runs again.
            // (SessionState returns "" - not null - for an erased key, so "is the
            // queue key gone" cannot be the stop condition.)
            SessionState.SetString(KPhase, "idle");
            File_AppendQueueEnd();
            return;
        }
        if (!EditorApplication.isPlaying) return;

        var pwm = Object.FindObjectOfType<PlayerWaveManager>();
        if (!pwm) return;

        switch (phase)
        {
            case "starting":
                if (Time.timeSinceLevelLoad < 1.5f) return;
                pwm.SendMessage("HandleBlast", SessionState.GetInt(KMatches, 0));
                Time.timeScale = Speed;
                SessionState.SetString(KPhase, "dealing");
                break;

            case "dealing":
                if (pwm.MatchesReleased < pwm.MatchesCleared) { Time.timeScale = Speed; return; }
                Object.FindObjectOfType<BattleStartController>().StartBattle();
                Time.timeScale = Speed;
                SessionState.SetFloat(KStartT, Time.time);
                SessionState.SetString(KPhase, "battle");
                break;

            case "battle":
                var lgm = LevelGameManager.Instance;
                float elapsed = Time.time - SessionState.GetFloat(KStartT, Time.time);
                bool over = lgm && (lgm.CurrentState == LevelGameManager.GameState.Won ||
                                    lgm.CurrentState == LevelGameManager.GameState.Lost);
                if (!over && elapsed < TimeoutGameSeconds)
                {
                    if (lgm && lgm.CurrentState == LevelGameManager.GameState.Playing) Time.timeScale = Speed;
                    return;
                }

                // Margin: how much of each base is left. A win with the player's base
                // untouched is a landslide; a loss with the enemy base half down was close.
                var pGate = Object.FindObjectOfType<PlayerGateStats>();
                var eGate = Object.FindObjectOfType<EnemyGateStats>();
                string Pct(CharacterStats s) => s && s.maxHealth > 0f ? $"{100f * s.currentHP / s.maxHealth:0}%" : "?";

                string outcome = over ? lgm.CurrentState.ToString() : "TIMEOUT";
                string line = $"stage {SessionState.GetInt(KStage, 0)} matches {SessionState.GetInt(KMatches, 0)} " +
                              $"heroLv {SessionState.GetInt(KLevel, 0)} -> {outcome} after {elapsed:0}s " +
                              $"(player base {Pct(pGate)}, enemy base {Pct(eGate)})";
                SessionState.SetString(KResults, Results + line + "\n");
                System.IO.File.AppendAllText(LogPath, line + "\n");
                Debug.Log("[Probe] " + line);

                Time.timeScale = 1f;
                SessionState.SetString(KPhase, "done");
                EditorApplication.ExitPlaymode();
                break;
        }
    }
}
