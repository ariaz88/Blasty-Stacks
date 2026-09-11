using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Real requested scenes and current character prefabs; no boot/save writes.</summary>
[InitializeOnLoad]
public static class CPCombatVerification
{
    const string Key = "CPCombatVerification";
    static IEnumerator routine;
    static readonly List<string> rows = new();
    static double deadline;
    static CPCombatVerification() { EditorApplication.playModeStateChanged += StateChanged; }
    [MenuItem("Tools/Combat Power/Verify Real Stages 1-5")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene and exit Play mode first.");
        SessionState.SetString(Key + ".restore", SceneManager.GetActiveScene().path);
        SessionState.SetString(Key + ".result", "Running");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }
    static void StateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            rows.Clear(); routine = Run(); EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool(Key, false);
            string path = SessionState.GetString(Key + ".restore", "");
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
        }
    }
    static void Tick()
    {
        try { if (routine.MoveNext()) return; Finish("PASS"); }
        catch (Exception e) { Finish("FAIL " + e); }
    }
    static void Finish(string status)
    {
        EditorApplication.update -= Tick;
        (routine as IDisposable)?.Dispose(); routine = null;
        SessionState.SetString(Key + ".result", status + "\n" + string.Join("\n", rows));
        Debug.Log("[CP Verification] " + status + "\n" + string.Join("\n", rows));
        GameplayPause.SetPaused(false);
        EditorApplication.isPlaying = false;
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Limit(string message)
    {
        if (EditorApplication.timeSinceStartup < deadline) return;
        string state = string.Join("; ", Object.FindObjectsOfType<PlayerStats>().Select(p =>
            $"{p.name} HP={p.currentHP} pos={p.transform.position} state={p.PlayerManager.currentState} target={p.PlayerManager.currentTarget} gate={p.PlayerManager.attackPlayerGate} action={p.PlayerManager.isPerformingAction}"));
        throw new Exception("Timeout: " + message + " " + state);
    }
    static void SetProperty(Type type, object target, string name, object value) =>
        type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance).SetValue(target, value);
    static IEnumerator Run()
    {
        Application.runInBackground = true;
        string saved = PlayerPrefs.GetString("GAME_SAVE_V1", "");
        int stageSaved = PlayerPrefs.GetInt("LM.CurrentStage", -1);
        var db = AssetDatabase.LoadAssetAtPath<UnitsDatabaseSO>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:UnitsDatabaseSO")[0]));
        foreach (var def in db.Units)
            Check(AssetDatabase.GetAssetPath(def.runtimePrefab).Contains("/Characters/New Characters/"), "Old hero prefab: " + def.name);
        // Keep the real boot's unconditional save-reset Awake out of testing.
        var boot = new GameObject("CP test inert services"); boot.SetActive(false); Object.DontDestroyOnLoad(boot);
        var gsm = boot.AddComponent<GameStartManager>(); gsm.unitsDatabase = db;
        var model = new PlayerUnitsModel();
        var ids = db.Units.Select(x => x.unitId).ToArray();
        model.InitializeFromDatabase(db, ids); model.SeedInitialDeployed(ids);
        foreach (int id in ids) model.GetState(id).level = 1 + id % 4;
        SetProperty(typeof(GameStartManager), gsm, "PlayerUnits", model);
        SetProperty(typeof(GameStartManager), null, "Instance", gsm);

        for (int level = SessionState.GetInt(Key + ".firstStage", 1); level <= 5; level++)
        for (int matches = 1; matches <= LevelBattleRules.TotalPairs(level); matches++)
        {
            GameplayPause.SetPaused(false); Time.timeScale = 5;
            UnityEngine.Random.InitState(level * 101 + matches);
            EditorSceneManager.LoadSceneInPlayMode($"Assets/Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_{level}.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            while (!Object.FindObjectOfType<EnemySpawner>()) yield return null;
            // Gate destruction is observed directly; disable progression/UI callbacks to avoid saving wins.
            foreach (var manager in Object.FindObjectsOfType<LevelGameManager>()) manager.enabled = false;
            var spawner = Object.FindObjectOfType<EnemySpawner>();
            foreach (var wave in spawner.levelConfig.waves) foreach (var entry in wave.entries)
                Check(AssetDatabase.GetAssetPath(entry.enemyPrefab).Contains("/Characters/New Characters/Enemies/"), "Old enemy prefab");
            var waves = Object.FindObjectOfType<PlayerWaveManager>();
            var pg = Object.FindObjectOfType<PlayerGateStats>();
            var eg = Object.FindObjectOfType<EnemyGateStats>();
            yield return null;
            for (int i = 0; i < matches; i++) MatchResolver.OnBlast?.Invoke(1);
            waves.SealForBattle(); spawner.StartBattle();
            deadline = EditorApplication.timeSinceStartup + 90;
            while (!CPBattleController.Instance || !spawner.AllWavesDispatched)
            {
                Check(!waves.DeploymentFailed, "Deployment failed"); Limit($"spawn L{level} M{matches}"); yield return null;
            }
            var battle = CPBattleController.Instance;
            int count = LevelBattleRules.TotalHeroes(level, matches);
            Check(waves.ReleasedHeroes.Count == count, "Wrong hero count");
            var enemies = Object.FindObjectsOfType<EnemyStats>();
            Check(enemies.Length == level, "Wrong enemy count");
            Check(Math.Abs(battle.PlayerCP - count * (level == 1 ? 100 : 125)) < 0.01, "Wrong player CP");
            Check(Math.Abs(enemies.Sum(e => CPCalculator.UnitPower(e.enemyManager.unitStats)) - LevelBattleRules.ReferenceEnemyCP(level)) < 0.01, "Wrong enemy CP");
            SessionState.SetString(Key + ".result", $"Running L{level} M{matches}\n" + string.Join("\n", rows));
            // Let actual animation-driven weapon collisions resolve combat and siege.
            while (pg && !pg.isPlayerGateDestroyed && eg && !eg.isDestroyed)
            {
                Limit($"combat L{level} M{matches}; alive enemies={spawner.AliveEnemyCount}, player gate={pg.currentHP}, enemy gate={eg.currentHP}");
                yield return null;
            }
            bool won = !eg || eg.isDestroyed;
            Check(won == (matches >= LevelBattleRules.FirstWinningMatch(level)), "Wrong natural battle winner");
            Check(battle.FewestHitsBeforeDeath >= 4, "Unit died in fewer than four hits");
            rows.Add($"L{level} M{matches}: heroes={count}, enemies={level}, CP={battle.PlayerCP:F0}/{battle.EnemyCP:F0}, {(won ? "Win" : "Loss")} after gate destruction; deaths H/E={battle.HeroDeaths}/{battle.EnemyDeaths}, minimum hits={battle.FewestHitsBeforeDeath}");
            yield return null;
        }
        Check(saved == PlayerPrefs.GetString("GAME_SAVE_V1", "") && stageSaved == PlayerPrefs.GetInt("LM.CurrentStage", -1), "Save changed");
        rows.Add("Saved progress unchanged. Only current New Characters prefabs used.");
    }
}
