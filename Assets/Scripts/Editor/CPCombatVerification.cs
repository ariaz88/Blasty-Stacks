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
        Begin(false);
    }
    [MenuItem("Tools/Combat Power/Verify Valkyrie Duel Only")]
    public static void StartValkyrieDuel() => Begin(true);
    static void Begin(bool duelOnly)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Save your scene and exit Play mode first.");
        SessionState.SetString(Key + ".restore", SceneManager.GetActiveScene().path);
        SessionState.SetString(Key + ".result", "Running");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".duelOnly", duelOnly);
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
            if (SessionState.GetBool(Key + ".duelOnly", false))
            {
                if (SessionState.GetBool(Key + ".hadSaveBeforeDuel", false)) PlayerPrefs.SetString("GAME_SAVE_V1", SessionState.GetString(Key + ".savedBeforeDuel", ""));
                else PlayerPrefs.DeleteKey("GAME_SAVE_V1");
                int savedStage = SessionState.GetInt(Key + ".stageBeforeDuel", -1);
                if (savedStage >= 0) PlayerPrefs.SetInt("LM.CurrentStage", savedStage); else PlayerPrefs.DeleteKey("LM.CurrentStage");
                PlayerPrefs.Save();
            }
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
        if (SessionState.GetBool(Key + ".duelOnly", false))
        {
            SessionState.SetString(Key + ".savedBeforeDuel", saved);
            SessionState.SetBool(Key + ".hadSaveBeforeDuel", PlayerPrefs.HasKey("GAME_SAVE_V1"));
            SessionState.SetInt(Key + ".stageBeforeDuel", stageSaved);
        }
        var db = AssetDatabase.LoadAssetAtPath<UnitsDatabaseSO>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:UnitsDatabaseSO")[0]));
        foreach (var def in db.Units)
            Check(AssetDatabase.GetAssetPath(def.runtimePrefab).Contains("/Characters/New Characters/"), "Old hero prefab: " + def.name);
        // Keep the real boot's unconditional save-reset Awake out of testing.
        var boot = new GameObject("CP test inert services"); boot.SetActive(false); Object.DontDestroyOnLoad(boot);
        var gsm = boot.AddComponent<GameStartManager>(); gsm.unitsDatabase = db;
        var model = new PlayerUnitsModel();
        var ids = db.Units.Select(x => x.unitId).ToArray();
        bool duelOnly = SessionState.GetBool(Key + ".duelOnly", false);
        if (duelOnly) ids = db.Units.Where(x => x.runtimePrefab.name == "Player_Valkyrie").Select(x => x.unitId).ToArray();
        Check(ids.Length > 0, "Missing Valkyrie definition");
        model.InitializeFromDatabase(db, ids); model.SeedInitialDeployed(ids);
        foreach (int id in ids) model.GetState(id).level = 1 + id % 4;
        SetProperty(typeof(GameStartManager), gsm, "PlayerUnits", model);
        SetProperty(typeof(GameStartManager), null, "Instance", gsm);

        for (int level = duelOnly ? 1 : SessionState.GetInt(Key + ".firstStage", 1); level <= (duelOnly ? 1 : 5); level++)
        for (int matches = 1; matches <= (duelOnly ? 1 : LevelBattleRules.TotalPairs(level)); matches++)
        {
            GameplayPause.SetPaused(false); Time.timeScale = duelOnly ? 1 : 5;
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
            if (duelOnly)
            {
                var hero = waves.ReleasedHeroes[0];
                hero.GetComponent<Rigidbody2D>().position = (Vector2)enemies[0].transform.position + Vector2.down * 2f;
                hero.transform.position = new Vector3(enemies[0].transform.position.x, enemies[0].transform.position.y - 2f, 0);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            }
            Check(enemies.Length == level, "Wrong enemy count");
            Check(Math.Abs(battle.PlayerCP - count * (level == 1 ? 100 : 125)) < 0.01, "Wrong player CP");
            Check(Math.Abs(enemies.Sum(e => CPCalculator.UnitPower(e.enemyManager.unitStats)) - LevelBattleRules.ReferenceEnemyCP(level)) < 0.01, "Wrong enemy CP");
            SessionState.SetString(Key + ".result", $"Running L{level} M{matches}\n" + string.Join("\n", rows));
            float slidingDistance = 0, movingDuringAttack = 0;
            Vector2 previousEnemyPosition = enemies[0].transform.position;
            int previousFrame = -1;
            float nextTrace = 0;
            var trace = new List<string>();
            // Let actual animation-driven weapon collisions resolve combat and siege.
            while (pg && !pg.isPlayerGateDestroyed && eg && !eg.isDestroyed)
            {
                if (duelOnly && enemies[0] && Time.frameCount != previousFrame)
                {
                    var e = enemies[0]; var motion = e.GetComponent<EnemyLocoMotion>();
                    var rb = e.GetComponent<Rigidbody2D>();
                    float moved = Vector2.Distance(previousEnemyPosition, rb.position);
                    if (motion.IsInAttackPosition() && rb.bodyType == RigidbodyType2D.Kinematic && rb.linearVelocity.sqrMagnitude > 0.001f) slidingDistance += moved;
                    if (e.enemyManager.isPerformingAction) movingDuringAttack += moved;
                    SessionState.SetString(Key + ".duelTelemetry", $"Valkyrie duel: enemy HP={e.currentHP:F2}, position={rb.position}, velocity={rb.linearVelocity}, body={rb.bodyType}, inPosition={motion.IsInAttackPosition()}, attacking={e.enemyManager.isPerformingAction}, sliding distance={slidingDistance:F3}, movement during attack={movingDuringAttack:F3}");
                    previousEnemyPosition = rb.position; previousFrame = Time.frameCount;
                    if (Time.time >= nextTrace && trace.Count < 50)
                    {
                        var animator = e.GetComponentInChildren<Animator>();
                        var clips = animator.GetCurrentAnimatorClipInfo(0);
                        trace.Add($"t={Time.time:F2} E={rb.position} H={waves.ReleasedHeroes[0].transform.position} v={rb.linearVelocity} in={motion.IsInAttackPosition()} swing={e.enemyManager.isPerformingAction} clip={(clips.Length > 0 ? clips.OrderByDescending(c => c.weight).First().clip.name : "none")}");
                        SessionState.SetString(Key + ".duelTrace", string.Join("\n", trace)); nextTrace = Time.time + 0.15f;
                    }
                }
                Limit($"combat L{level} M{matches}; alive enemies={spawner.AliveEnemyCount}, player gate={pg.currentHP}, enemy gate={eg.currentHP}");
                yield return null;
            }
            bool won = !eg || eg.isDestroyed;
            if (duelOnly)
            {
                var heroHealth = waves.ReleasedHeroes[0].GetComponent<PlayerStats>();
                Check(heroHealth.currentHP < heroHealth.maxHealth, "Enemy never landed a hit on Valkyrie");
                rows.Add($"Valkyrie HP after duel: {heroHealth.currentHP:F2}/{heroHealth.maxHealth:F2}; enemy successfully landed physical hits.");
            }
            Check(won == (matches >= LevelBattleRules.FirstWinningMatch(level)), "Wrong natural battle winner");
            Check(battle.FewestHitsBeforeDeath >= 4, "Unit died in fewer than four hits");
            rows.Add($"L{level} M{matches}: heroes={count}, enemies={level}, CP={battle.PlayerCP:F0}/{battle.EnemyCP:F0}, {(won ? "Win" : "Loss")} after gate destruction; deaths H/E={battle.HeroDeaths}/{battle.EnemyDeaths}, minimum hits={battle.FewestHitsBeforeDeath}");
            if (duelOnly) rows.Add($"Enemy sliding distance={slidingDistance:F3}; movement during attack={movingDuringAttack:F3}");
            yield return null;
        }
        if (!duelOnly) Check(saved == PlayerPrefs.GetString("GAME_SAVE_V1", "") && stageSaved == PlayerPrefs.GetInt("LM.CurrentStage", -1), "Save changed");
        rows.Add(duelOnly ? "Duel snapshot will be restored on return to Edit mode." : "Saved progress unchanged. Only current New Characters prefabs used.");
    }
}
