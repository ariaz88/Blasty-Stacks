using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real scenes, deployment queue, animations and hitboxes. No outcome overrides.</summary>
[InitializeOnLoad]
public static class EarlyCampaignVerification
{
    const string Key = "EarlyCampaignVerification";
    static IEnumerator routine;
    static List<string> rows;
    static double deadline;
    static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static EarlyCampaignVerification() => EditorApplication.playModeStateChanged += Changed;

    public static void Start(int first = 5, int last = 10, int repeats = 1, int onlyMatch = 0)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save the open scene and exit Play mode first.");
        SessionState.SetString(Key + ".scene", SceneManager.GetActiveScene().path);
        SessionState.SetBool(Key + ".direct", DirectPlayMenu.Enabled);
        SessionState.SetBool(Key + ".hadSave", PlayerPrefs.HasKey("GAME_SAVE_V1"));
        SessionState.SetString(Key + ".save", PlayerPrefs.GetString("GAME_SAVE_V1", ""));
        SessionState.SetInt(Key + ".stage", PlayerPrefs.GetInt("LM.CurrentStage", -1));
        SessionState.SetInt(Key + ".first", first); SessionState.SetInt(Key + ".last", last);
        SessionState.SetInt(Key + ".repeats", repeats); SessionState.SetInt(Key + ".match", onlyMatch);
        SessionState.SetBool(Key, true); SessionState.SetString(Key + ".status", "Starting");
        DirectPlayMenu.Enabled = false;
        // The harness drives scenarios through saves that must survive the play-mode
        // transition. That is now always true (persistence is unconditional), so the
        // real save is snapshotted above and restored in EnteredEditMode below.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            rows = new List<string> { "stage,matches,seed,heroLevel,heroes,enemyCount,heroCP,enemyCP,result,battleSeconds,firstHitSeconds,armyDefeatedSeconds,blows,heroDeaths,enemyDeaths" };
            routine = Run(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            if (SessionState.GetBool(Key + ".hadSave", false)) PlayerPrefs.SetString("GAME_SAVE_V1", SessionState.GetString(Key + ".save", ""));
            else PlayerPrefs.DeleteKey("GAME_SAVE_V1");
            int stage = SessionState.GetInt(Key + ".stage", -1);
            if (stage < 0) PlayerPrefs.DeleteKey("LM.CurrentStage"); else PlayerPrefs.SetInt("LM.CurrentStage", stage);
            PlayerPrefs.Save();
            typeof(SaveSystem).GetField("_cache", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
            DirectPlayMenu.Enabled = SessionState.GetBool(Key + ".direct", true);
            SessionState.SetBool(Key, false);
            string path = SessionState.GetString(Key + ".scene", "");
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
        }
    }
    static void Tick()
    {
        try { if (routine.MoveNext()) return; Finish("Completed"); }
        catch (Exception e) { Finish("FAILED: " + e); }
    }
    static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        SessionState.SetString(Key + ".status", status);
        WriteRows(); Debug.Log("[Early campaign verification] " + status);
        GameplayPause.SetPaused(false); Time.timeScale = 1;
        EditorApplication.isPlaying = false;
    }
    static void WriteRows()
    {
        Directory.CreateDirectory("Docs/balance");
        File.WriteAllLines("Docs/balance/latest-playmode.csv", rows);
    }
    static void SetProperty(Type t, object target, string field, object value) =>
        t.GetProperty(field, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).SetValue(target, value);
    static void Limit(string description)
    {
        if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout " + description);
    }
    static IEnumerator Run()
    {
        Application.runInBackground = true;
        CPBattleController.LogEveryBlow = false;
        var db = EarlyCampaignAuthoring.Database;
        var boot = new GameObject("Balance test services (inert)"); boot.SetActive(false); Object.DontDestroyOnLoad(boot);
        var gsm = boot.AddComponent<GameStartManager>(); gsm.unitsDatabase = db;
        var model = new PlayerUnitsModel();
        int[] ids = db.Units.Where(d => d.startsDeployed).Select(d => d.unitId).ToArray();
        model.InitializeFromDatabase(db, ids); model.SeedInitialDeployed(ids);
        SetProperty(typeof(GameStartManager), gsm, "PlayerUnits", model);
        SetProperty(typeof(GameStartManager), null, "Instance", gsm);
        var lm = boot.AddComponent<LevelManager>();
        SetProperty(typeof(LevelManager), null, "Instance", lm);
        for (int stage = SessionState.GetInt(Key + ".first", 5); stage <= SessionState.GetInt(Key + ".last", 10); stage++)
        for (int seed = 0; seed < SessionState.GetInt(Key + ".repeats", 1); seed++)
        for (int matches = 1; matches <= LevelBattleRules.TotalPairs(stage); matches++)
        {
            int only = SessionState.GetInt(Key + ".match", 0);
            if (only > 0 && matches != only) continue;
            int heroLevel = stage < 6 ? 1 : stage < 10 ? 2 : 3;
            foreach (int id in ids) model.GetState(id).level = heroLevel;
            typeof(LevelManager).GetField("_currentStage", PrivateInstance).SetValue(lm, stage);
            GameplayPause.SetPaused(false); Time.timeScale = 5;
            UnityEngine.Random.InitState(1000 + stage * 100 + seed);
            EditorSceneManager.LoadSceneInPlayMode($"Assets/Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_{stage}.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            foreach (var manager in Object.FindObjectsByType<LevelGameManager>(FindObjectsSortMode.None)) manager.enabled = false;
            var waves = Object.FindFirstObjectByType<PlayerWaveManager>();
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            var start = Object.FindFirstObjectByType<BattleStartController>();
            var seq = Object.FindFirstObjectByType<HeroDeploymentSequencer>();
            var pg = Object.FindFirstObjectByType<PlayerGateStats>();
            var eg = Object.FindFirstObjectByType<EnemyGateStats>();
            if (!waves || !spawner || !start || !seq || !pg || !eg) throw new Exception("Missing stage services " + stage);
            yield return null;
            for (int m = 0; m < matches; m++) MatchResolver.OnBlast?.Invoke(1);
            deadline = EditorApplication.timeSinceStartup + 30;
            while (waves.EarnedBatches.Count < matches)
            {
                if (waves.DeploymentFailed) throw new Exception("Deployment failed " + stage);
                Limit("awarding heroes"); yield return null;
            }
            int expectedHeroes = waves.EarnedBatches.Sum(b => b.Count);
            if (expectedHeroes != LevelBattleRules.TotalHeroes(stage, matches)) throw new Exception("Authored roster mismatch");
            double heroCp = waves.EarnedBatches.Sum(b => b.Sum(d => EarlyCampaignAuthoring.Power(d.baseStats, EarlyCampaignAuthoring.HeroGrowth, heroLevel)));
            var enemies = new Dictionary<int, double>();
            float began = Time.time, firstHit = -1, armyDefeated = -1;
            start.StartBattle(); // Starts the real six-second deployment queue as well as the spawner.
            deadline = EditorApplication.timeSinceStartup + 100;
            SessionState.SetString(Key + ".status", $"Stage {stage}, match {matches}, seed {seed}");
            while (pg && !pg.isPlayerGateDestroyed && eg && !eg.isDestroyed && Time.time - began < 360)
            {
                GameplayPause.SetPaused(false); Time.timeScale = 5;
                foreach (var e in Object.FindObjectsByType<EnemyStats>(FindObjectsSortMode.None))
                    if (e.enemyManager && e.enemyManager.unitStats.initialized) enemies[e.GetInstanceID()] = CPCalculator.UnitPower(e.enemyManager.unitStats);
                var battle = CPBattleController.Instance;
                if (battle && battle.TotalBlowsThisBattle > 0 && firstHit < 0) firstHit = Time.time - began;
                if (spawner.AllWavesDispatched && spawner.AliveEnemyCount == 0 && armyDefeated < 0) armyDefeated = Time.time - began;
                Limit("combat " + stage + "/" + matches); yield return null;
            }
            var report = CPBattleController.Instance;
            string outcome = !eg || eg.isDestroyed ? "Win" : !pg || pg.isPlayerGateDestroyed ? "Loss" : "Timeout";
            string row = string.Join(",", new[] { stage.ToString(), matches.ToString(), seed.ToString(), heroLevel.ToString(), expectedHeroes.ToString(), enemies.Count.ToString(),
                heroCp.ToString("F2", CultureInfo.InvariantCulture), enemies.Values.Sum().ToString("F2", CultureInfo.InvariantCulture), outcome,
                (Time.time - began).ToString("F2", CultureInfo.InvariantCulture), firstHit.ToString("F2", CultureInfo.InvariantCulture), armyDefeated.ToString("F2", CultureInfo.InvariantCulture),
                (report ? report.TotalBlowsThisBattle : 0).ToString(), (report ? report.HeroDeaths : 0).ToString(), (report ? report.EnemyDeaths : 0).ToString() });
            rows.Add(row); WriteRows(); Debug.Log("[Balance result] " + row);
            if (outcome == "Timeout") Debug.LogWarning("Combat did not resolve; do not count this as a loss.");
            yield return null;
        }
    }
}
