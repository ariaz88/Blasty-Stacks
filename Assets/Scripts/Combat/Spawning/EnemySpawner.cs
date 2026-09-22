using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Config")]
    public LevelConfig levelConfig;

    [Header("CP weighting (optional)")]
    public CPWeightsConfigSO cpWeights;

    [Header("Battle Gate")]
    [Tooltip("ON  = puzzle-only start; nothing spawns until StartBattle() is called " +
             "(the BATTLE button, through BattleStartController).\n" +
             "OFF = old behaviour, waves begin as soon as the level loads.")]
    [SerializeField] private bool waitForBattleStart = false;

    [Header("Spawn Area")]
    [Tooltip("ON  = enemies spawn in a box measured FROM THE ENEMY GATE, using the " +
             "offsets below. Survives moving the gate, and is per-scene so it does " +
             "not disturb other stages that share the same LevelConfig asset.\n" +
             "OFF = use each Wave's absolute spawnMin/spawnMax world coordinates.")]
    [SerializeField] private bool spawnRelativeToEnemyGate = false;

    [Tooltip("The enemy gate. Left empty = found by the 'EnemyGate' tag, else by name.")]
    [SerializeField] private Transform enemyGateAnchor;

    [Tooltip("Spawn box corner relative to the gate. Negative Y = below the gate, " +
             "i.e. in front of it, facing the player.")]
    [SerializeField] private Vector2 gateRelativeMin = new Vector2(-3f, -4f);
    [SerializeField] private Vector2 gateRelativeMax = new Vector2(3f, -2f);

    // Track wave-alive count if you still want to wait for clear
    int _alive;

    bool _battleStarted;
    private CPBattleController cpBattle;

    /// <summary>
    /// The hero side, cached at battle start. Only read for the stage-6 full-clear
    /// bonus on the final wave - see LevelBattleRules.FullClearBonusLevel.
    /// </summary>
    private PlayerWaveManager _heroes;

    /// <summary>True once the wave loop has actually been kicked off.</summary>
    public bool BattleStarted => _battleStarted;

    /// <summary>
    /// Enemies currently on the field, straight off the counter <see cref="SpawnOne"/>
    /// raises and <see cref="WatchDeath"/> lowers. Exposed for the mutual-wipe check in
    /// LevelGameManager - "no enemies left" is only half of a stalemate, the other half
    /// is <see cref="AllWavesDispatched"/> below.
    /// </summary>
    public int AliveEnemyCount => _alive;

    /// <summary>
    /// True once the LAST wave of the level has been spawned, i.e. nothing further will
    /// ever come out of this spawner.
    ///
    /// Set BEFORE that wave's "wait for clear", so it is already true while the final
    /// enemies are still fighting - a reader must therefore pair it with
    /// <see cref="AliveEnemyCount"/> rather than treat it as "the field is empty".
    /// </summary>
    public bool AllWavesDispatched { get; private set; }

    /// <summary>
    /// True once at least one enemy actually exists. Player units wait for this
    /// before advancing, so heroes never march at an empty field.
    /// </summary>
    public bool HasSpawnedFirstEnemy { get; private set; }

    /// <summary>Raised the moment the very first enemy of the stage is spawned.</summary>
    public event System.Action OnFirstEnemySpawned;

    /// <summary>
    /// Static mirror of the above, for things spawned at runtime that cannot hold
    /// a reference to this spawner - unit health bars, mainly.
    ///
    /// DEFAULTS TO TRUE so a stage that never gates anything behaves as it always
    /// did; Start() flips it to false only when this spawner is actually waiting
    /// for the BATTLE button.
    /// </summary>
    public static bool EnemiesHaveAppeared { get; private set; } = true;

    /// <summary>Static counterpart of OnFirstEnemySpawned.</summary>
    public static event System.Action OnAnyFirstEnemySpawned;

    /// <summary>
    /// Resolves a wave's spawn box, either as authored absolute world coordinates
    /// or as an offset box around the enemy gate.
    /// </summary>
    void ResolveSpawnArea(Wave wave, out Vector2 min, out Vector2 max)
    {
        if (!spawnRelativeToEnemyGate)
        {
            min = wave.spawnMin;
            max = wave.spawnMax;
            return;
        }

        if (!enemyGateAnchor) enemyGateAnchor = FindEnemyGate();

        Vector2 gate = enemyGateAnchor ? (Vector2)enemyGateAnchor.position : Vector2.zero;
        if (!enemyGateAnchor)
            Debug.LogWarning($"{name}: spawnRelativeToEnemyGate is ON but no gate was found - " +
                             "spawning around the world origin.", this);

        min = gate + gateRelativeMin;
        max = gate + gateRelativeMax;
    }

    Transform FindEnemyGate()
    {
        var tagged = GameObject.FindGameObjectWithTag("EnemyGate");
        if (tagged) return tagged.transform;

        var byName = GameObject.Find("EnemyCastle");
        return byName ? byName.transform : null;
    }

    /// <summary>
    /// Closes the "enemies are out" gate BEFORE any Start runs.
    ///
    /// This cannot wait for Start. PlayerWaveManager.Start calls BeginWaves, and
    /// WaveLoop's first segment runs synchronously up to its first yield - which,
    /// on a board that already has stacks, is AFTER it has spawned the first hero
    /// wave. Unity does not order two Starts, so if that happens first, every hero
    /// in that wave runs HealthBar.Awake while EnemiesHaveAppeared still holds its
    /// static default of true: the bar skips the gate, never subscribes to
    /// OnAnyFirstEnemySpawned, and sits visible on the deploy stage for the whole
    /// puzzle phase. Later waves looked correct, which is what made it read as
    /// "only the first heroes show their HP".
    ///
    /// Guarded on levelConfig so a stage with a broken spawner still behaves as it
    /// did before - gate left open rather than closed forever with nothing to open it.
    /// </summary>
    void Awake()
    {
        if (waitForBattleStart && levelConfig)
        {
            EnemiesHaveAppeared = false;
            HasSpawnedFirstEnemy = false;
        }
    }

    void Start()
    {
        if (!levelConfig)
        {
            Debug.LogWarning($"{name}: missing LevelConfig.");
            return;
        }

        // Puzzle-only phase: hold every wave until the player presses BATTLE.
        // The health-bar gate itself was already closed in Awake, above.
        if (waitForBattleStart) return;

        StartBattle();
    }

    /// <summary>
    /// Kicks off the wave loop. Safe to call repeatedly - only the first call runs.
    /// Called by BattleStartController when the BATTLE button is pressed.
    /// </summary>
    public void StartBattle()
    {
        if (_battleStarted) return;

        if (!levelConfig)
        {
            Debug.LogWarning($"{name}: missing LevelConfig.");
            return;
        }

        _battleStarted = true;
        StartCoroutine(StartPreparedBattle());
    }

    private IEnumerator StartPreparedBattle()
    {
        int stage = LevelBattleRules.ResolveLevel(gameObject, levelConfig);
        var heroes = FindObjectOfType<PlayerWaveManager>();
        _heroes = heroes;   // kept for the final-wave full-clear check in RunLevel

        if (LevelBattleRules.AppliesTo(stage) && heroes) heroes.SealForBattle();

        // The reporter is created for EVERY battle, at every stage, and it cannot
        // fail. It only measures - it never rescales a unit and never decides a
        // result - so there is nothing here that can abort the spawn.
        //
        // It used to be gated on !CardRewardsActive because the old Prepare()
        // normalised team CP and returned false on an empty roster, which aborted
        // this coroutine before RunLevel and meant no enemy ever spawned. Heroes
        // arrive progressively through HeroDeploymentSequencer's timed loads, so an
        // empty roster at battle start is normal; late arrivals register themselves
        // through CPBattleController.RegisterHero.
        cpBattle = gameObject.AddComponent<CPBattleController>();
        cpBattle.Prepare(this, heroes);

        yield return RunLevel();
    }

    /// <summary>
    /// Hard ceiling on the gap between one wave spawning and the next (Arash,
    /// 2026-09-21: "if it is more than 8 seconds, make it 8").
    ///
    /// Before this the gap was UNBOUNDED - each wave waited for the field to be
    /// completely cleared, so the delay was however long the kill happened to
    /// take, and an enemy that could not be reached stalled the stage forever.
    /// </summary>
    private const float MaxSecondsBetweenWaves = 8f;

    /// <summary>
    /// The gap between waves: honour the authored beat, then wait for the field to
    /// clear - but never longer than <see cref="MaxSecondsBetweenWaves"/> in total.
    /// Pause-aware, like every other wait here.
    /// </summary>
    IEnumerator WaitBetweenWaves(float authoredDelay)
    {
        float elapsed = 0f;
        float minDelay = Mathf.Clamp(authoredDelay, 0f, MaxSecondsBetweenWaves);

        while (elapsed < MaxSecondsBetweenWaves)
        {
            if (!IsGameplayPaused()) elapsed += Time.deltaTime;

            // Early out as soon as the authored beat has passed AND the field is
            // clear, so a wave that dies quickly still advances quickly.
            if (elapsed >= minDelay && _alive == 0) yield break;

            yield return null;
        }
    }

    bool IsGameplayPaused()
    {
        return HudCurrencyView.Instance != null && HudCurrencyView.Instance.IsGameplayPaused;
    }

    // Wait for duration seconds, but don�t advance while the game is paused
    IEnumerator WaitForSecondsGameplay(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!IsGameplayPaused())
            {
                elapsed += Time.deltaTime;
            }

            yield return null; // wait one frame
        }
    }


    IEnumerator RunLevel1()
    {
        yield return WaitForSecondsGameplay(levelConfig.startDelay);


        for (int i = 0; i < levelConfig.waves.Count; i++)
        {
            var wave = levelConfig.waves[i];

            if (wave.delayBeforeWave > 0f)
                yield return WaitForSecondsGameplay(wave.delayBeforeWave);


            switch (wave.formation)
            {
                case SpawnFormation.AllTogetherGrid:
                    yield return SpawnAllTogetherGrid(wave, levelConfig.levelNumber);
                    break;

                case SpawnFormation.TwoRows:
                    yield return SpawnTwoRows(wave, levelConfig.levelNumber);
                    break;
            }

            // Wait for wave clear (optional)
            yield return new WaitUntil(() => _alive == 0);
        }

    }
    IEnumerator RunLevel()
    {
        // REMOVED 2026-09-16: a short-circuit into RunReferenceLevel whenever a CP
        // battle existed. That path spawned exactly `stage` enemies in one clump to
        // hit a reference CP budget, ignoring the authored wave table entirely. Both
        // it and the budget are gone - enemy counts come from LevelBattleRules
        // .EnemyWaveCounts and enemy strength from each type's own growth curve.
        // cpBattle now exists in every battle and must not change how they spawn.

        // The SAME resolution StartPreparedBattle already uses, so the stage that
        // decides the wave counts and the enemy stat curve cannot disagree with the
        // stage the battle was set up for.
        //
        // This used to read LevelManager.CurrentStage first and only fall back to
        // levelConfig.levelNumber when the singleton was missing. Those two can
        // drift: pressing Play directly on a stage scene, or any flow that reaches
        // a gameplay scene without HomeManager/DirectPlayBootstrap calling SetStage,
        // leaves CurrentStage pointing somewhere else - and then this stage fields
        // ANOTHER stage's wave counts and enemy strength.
        //
        // ResolveLevel reads the "_Stage_N" suffix of the scene name first (the
        // gameplay scenes are Level_1_Stage_N), then the asset's own levelNumber -
        // both of which are authored per stage and cannot drift at runtime.
        int stageLevel = LevelBattleRules.ResolveLevel(gameObject, levelConfig);

        // LevelBattleRules.EnemyWaves is the authority on HOW MANY enemies arrive
        // and in how many waves (Arash, 2026-09-14). The LevelConfig asset still
        // supplies everything else - formation, spawn area, enemy types, delays -
        // so this reshapes the authored waves rather than replacing them.
        var waveList = BuildWavesFromRules(stageLevel);

        // The authored per-wave counts, kept for the final-wave bonus check below.
        // Null past the authored range, where the asset's own waves run unchanged.
        var counts = LevelBattleRules.EnemyWaveCounts(stageLevel);
        if (counts != null && counts.Length != waveList.Count) counts = null;

        // A level with no waves authored has, trivially, already sent everything it has.
        if (waveList.Count == 0)
            AllWavesDispatched = true;

        // Initial delay before first wave (respects pause)
        yield return WaitForSecondsGameplay(levelConfig.startDelay);

        for (int i = 0; i < waveList.Count; i++)
        {
            var wave = waveList[i];

            // FINAL-WAVE FULL-CLEAR BONUS (stage 6 only, temporary).
            // Resolved HERE rather than in BuildWavesFromRules because the player
            // goes on clearing board matches while the earlier waves are fought -
            // asking at battle start would always see zero matches cleared.
            if (i == waveList.Count - 1 && counts != null)
            {
                bool cleared = LevelBattleRules.AllMatchesCleared(stageLevel, _heroes);
                int wanted = LevelBattleRules.FinalWaveCount(stageLevel, counts[i], cleared);

                if (wanted != counts[i])
                {
                    var template = levelConfig.waves[Mathf.Min(i, levelConfig.waves.Count - 1)];
                    wave = CopyWaveWithTotal(template, wanted, i);
                    Debug.Log($"[EnemySpawner] Stage {stageLevel}: all {LevelBattleRules.TotalPairs(stageLevel)} " +
                              $"matches cleared - final wave {counts[i]} -> {wanted} enemies.", this);
                }
            }

            // The FIRST wave still honours its own authored beat after startDelay.
            // Later waves get their delay folded into the capped gap below instead,
            // so the gap never exceeds MaxSecondsBetweenWaves.
            if (i == 0 && wave.delayBeforeWave > 0f)
                yield return WaitForSecondsGameplay(wave.delayBeforeWave);

            switch (wave.formation)
            {
                case SpawnFormation.AllTogetherGrid:
                    // CP/difficulty for this wave comes from LevelManager.CurrentStage
                    yield return SpawnAllTogetherGrid(wave, stageLevel);
                    break;

                case SpawnFormation.TwoRows:
                    yield return SpawnTwoRows(wave, stageLevel);
                    break;
            }

            int spawned = 0;
            foreach (var e in wave.entries) if (e != null) spawned += e.count;
            Debug.Log($"[EnemySpawner] Stage {stageLevel}: spawned wave {i + 1}/{waveList.Count} " +
                      $"with {spawned} enem{(spawned == 1 ? "y" : "ies")}.", this);

            // Flagged HERE rather than after the loop: the last iteration parks on the
            // wait below and only falls through once the field is clear, so waiting
            // for the loop to end would make this useless to anyone asking "can more
            // enemies still arrive?" while the final wave is alive.
            if (i == waveList.Count - 1)
                AllWavesDispatched = true;

            if (i < waveList.Count - 1)
            {
                var next = waveList[i + 1];

                // EVERY remaining wave WILL arrive: on a fixed timer when the wave
                // asks for one, otherwise on a clear-gated but BOUNDED wait. Either
                // way a wave that cannot be killed can no longer stall the stage.
                if (next.spawnOnTimerOnly)
                    yield return WaitForSecondsGameplay(Mathf.Max(0f, next.delayBeforeWave));
                else
                    yield return WaitBetweenWaves(next.delayBeforeWave);
            }
            else
            {
                // Last wave: nothing further to spawn, so just let the field settle.
                yield return new WaitUntil(() => _alive == 0);
            }
        }

        // Stage complete � if you ever want to auto-advance directly from here,
        // you would call LevelManager.Instance?.MarkLevelWon();
        // (Right now WinPanel handles it after the player presses Claim.)
    }


    // ---------- WAVE SHAPING (LevelBattleRules.EnemyWaves) ----------

    /// <summary>
    /// The waves this stage will actually run: as many waves, with as many enemies
    /// each, as LevelBattleRules.EnemyWaves says - built from the LevelConfig's own
    /// authored waves so formation, spawn area, enemy types, delays and per-entry
    /// unit levels all survive.
    ///
    /// Returns the asset's waves UNCHANGED for any level past the authored range,
    /// so stages beyond the table keep working exactly as before.
    ///
    /// !! NEVER MUTATES THE ASSET. LevelConfig assets are SHARED between stages;
    /// writing counts into one would corrupt every stage using it, and would
    /// persist into the project file. Every wave here is a fresh copy.
    /// </summary>
    private List<Wave> BuildWavesFromRules(int stageLevel)
        => ResolveWavesForStage(levelConfig, stageLevel);

    /// <summary>
    /// The instance method above, as a pure function of (config, stage).
    ///
    /// Public and static so anything that needs to know WHAT A STAGE WILL FIELD
    /// without running it can ask the same code the spawner obeys - the Home
    /// screen's enemy-CP preview does exactly that. A second implementation of
    /// this reshaping would drift from the real one the first time either the
    /// wave table or the mix rule changed, and the preview would quietly start
    /// lying about the fight the player is about to walk into.
    ///
    /// !! NEVER MUTATES THE ASSET - see the note on BuildWavesFromRules.
    /// </summary>
    public static List<Wave> ResolveWavesForStage(LevelConfig config, int stageLevel)
    {
        if (!config) return new List<Wave>();

        var counts = LevelBattleRules.EnemyWaveCounts(stageLevel);
        if (counts == null || counts.Length == 0 || config.waves.Count == 0)
            return config.waves;

        var built = new List<Wave>(counts.Length);

        for (int i = 0; i < counts.Length; i++)
        {
            // Template: the authored wave at the same index when there is one,
            // otherwise the last authored wave - so a two-wave level built from a
            // one-wave asset reuses that wave's look for its second wave.
            var template = config.waves[Mathf.Min(i, config.waves.Count - 1)];
            built.Add(CopyWaveWithTotal(template, counts[i], i));
        }

        return built;
    }

    /// <summary>
    /// A copy of <paramref name="template"/> whose entry counts sum to exactly
    /// <paramref name="total"/>.
    ///
    /// WHEN THE AUTHORED COUNTS ALREADY SUM TO <paramref name="total"/> THEY ARE
    /// USED VERBATIM. That is what lets a stage author a specific MIX - "1 Reaper,
    /// 2 Zombies, 2 Skeletons" - instead of only a head count. Without it the even
    /// spread below silently overwrites the mix: a wave authored as 2 Skeletons +
    /// 1 Zombie would come out as 2 Zombies + 1 Skeleton purely because of entry
    /// order, which is impossible to author around and very hard to spot in play.
    ///
    /// Otherwise the total is spread across the template's entries as evenly as
    /// possible, remainder to the earliest ones, so a wave authored with two enemy
    /// types keeps both instead of collapsing to the first. An entry that rounds
    /// down to zero is DROPPED rather than kept at zero, because WaveEntry.count is
    /// [Min(1)] and a zero would be clamped back up to one and overshoot the total.
    /// </summary>
    private static Wave CopyWaveWithTotal(Wave template, int total, int waveIndex)
    {
        var copy = new Wave
        {
            name = $"{template.name} (rules {waveIndex + 1})",
            delayBeforeWave = template.delayBeforeWave,
            spawnOnTimerOnly = template.spawnOnTimerOnly,
            formation = template.formation,
            spawnMin = template.spawnMin,
            spawnMax = template.spawnMax,
            gridColumns = template.gridColumns,
            minSlotSpacing = template.minSlotSpacing,
            frontAnchor = template.frontAnchor,
            rowYOffset = template.rowYOffset,
            secondRowDelay = template.secondRowDelay,
            concurrencyCap = template.concurrencyCap,
            entries = new List<WaveEntry>()
        };

        var sources = new List<WaveEntry>();
        foreach (var e in template.entries)
            if (e != null && e.enemyPrefab) sources.Add(e);

        if (sources.Count == 0 || total <= 0) return copy;

        // Authored mix wins when it already fields the right number of enemies.
        int authored = 0;
        foreach (var e in sources) authored += Mathf.Max(0, e.count);

        if (authored == total)
        {
            foreach (var src in sources)
            {
                if (src.count <= 0) continue;
                copy.entries.Add(new WaveEntry
                {
                    enemyPrefab = src.enemyPrefab,
                    statsBase = src.statsBase,
                    count = src.count,
                    row = src.row,
                    unitLevel = src.unitLevel
                });
            }
            return copy;
        }

        int each = total / sources.Count;
        int remainder = total % sources.Count;

        for (int i = 0; i < sources.Count; i++)
        {
            int n = each + (i < remainder ? 1 : 0);
            if (n <= 0) continue;

            var src = sources[i];
            copy.entries.Add(new WaveEntry
            {
                enemyPrefab = src.enemyPrefab,
                statsBase = src.statsBase,
                count = n,
                row = src.row,
                unitLevel = src.unitLevel
            });
        }

        return copy;
    }

#if UNITY_EDITOR
    /// <summary>
    /// EDITOR ONLY. What BuildWavesFromRules would produce for one level against a
    /// given LevelConfig - wave by wave, entry by entry, with the totals - so the
    /// reshaping can be checked against the authored counts without entering Play
    /// mode.
    /// </summary>
    public static string EditorDescribeWaves(LevelConfig config, int stageLevel)
    {
        if (!config) return "no LevelConfig";

        var counts = LevelBattleRules.EnemyWaveCounts(stageLevel);
        if (counts == null || counts.Length == 0 || config.waves.Count == 0)
            return $"L{stageLevel}: unauthored - uses the asset's own {config.waves.Count} wave(s)";

        var sb = new System.Text.StringBuilder();
        sb.Append($"L{stageLevel} from '{config.name}' ({config.waves.Count} authored wave(s)): ");

        int grand = 0;
        for (int i = 0; i < counts.Length; i++)
        {
            var template = config.waves[Mathf.Min(i, config.waves.Count - 1)];
            var copy = CopyWaveWithTotal(template, counts[i], i);

            int sum = 0;
            var parts = new List<string>();
            foreach (var e in copy.entries)
            {
                sum += e.count;
                parts.Add($"{(e.enemyPrefab ? e.enemyPrefab.name : "?")}x{e.count}");
            }

            grand += sum;
            sb.Append($"[wave{i + 1} want {counts[i]} got {sum}: {string.Join(",", parts)}] ");
        }

        sb.Append($"TOTAL want {LevelBattleRules.TotalEnemies(stageLevel)} got {grand}");
        return sb.ToString();
    }
#endif

    // ---------- FORMATION HELPERS ----------

    IEnumerator SpawnAllTogetherGrid(Wave wave, int stageLevel)
    {
        // 1) Build a combined list of instances to spawn (interleaved types)
        var toSpawn = new List<(WaveEntry entry, int idx)>();
        int total = 0;
        foreach (var e in wave.entries)
        {
            if (!ValidateEntry(e)) continue;
            total += e.count;
        }
        if (total == 0) yield break;

        // Interleave by round-robin across entries so types are mixed visually
        int maxCount = 0; foreach (var e in wave.entries) if (e.count > maxCount) maxCount = e.count;
        for (int i = 0; i < maxCount; i++)
            foreach (var e in wave.entries)
                if (ValidateEntry(e) && i < e.count)
                    toSpawn.Add((e, i));

        // 2) Generate grid positions inside the wave�s spawn area
        ResolveSpawnArea(wave, out var areaMin, out var areaMax);
        var positions = GenerateGridPositions(areaMin, areaMax, total, wave.gridColumns, wave.minSlotSpacing);

        // 3) Instantiate all at once (tiny stagger just for VFX ordering if you like)
        for (int n = 0; n < toSpawn.Count; n++)
        {
            var (entry, _) = toSpawn[n];
            var pos = positions[n];
            SpawnOne(entry, pos, stageLevel);
        }
        yield return null;
    }

    IEnumerator SpawnTwoRows(Wave wave, int stageLevel)
    {
        // Split entries by row
        var front = new List<WaveEntry>();
        var back = new List<WaveEntry>();
        foreach (var e in wave.entries)
        {
            if (!ValidateEntry(e)) continue;
            if (e.row == RowIndex.FrontRow) front.Add(e); else back.Add(e);
        }



        ResolveSpawnArea(wave, out var areaMin, out var areaMax);

        var yMin = Mathf.Min(areaMin.y, areaMax.y);
        var yMax = Mathf.Max(areaMin.y, areaMax.y);
        float ySpan = Mathf.Max(0.01f, yMax - yMin);

        float gap = Mathf.Abs(wave.rowYOffset) > 0f ? Mathf.Abs(wave.rowYOffset) : (ySpan * 0.5f);

        float frontY, backY;
        if (wave.frontAnchor == FrontRowAnchor.MinY)
        {
            // Front at lower Y (closer to camera in your case)
            frontY = yMin;
            backY = Mathf.Min(yMax, frontY + gap);  // push back row upward
        }
        else
        {
            // Front at higher Y
            frontY = yMax;
            backY = Mathf.Max(yMin, frontY - gap);  // push back row downward
        }



        float xMin = Mathf.Min(areaMin.x, areaMax.x);
        float xMax = Mathf.Max(areaMin.x, areaMax.x);

        // ---- FRONT ROW ----
        int frontCount = 0; foreach (var e in front) frontCount += e.count;
        if (frontCount > 0)
        {
            var xs = DistributeAlongX(xMin, xMax, frontCount);
            int idx = 0;
            foreach (var e in front)
                for (int i = 0; i < e.count; i++)
                    SpawnOne(e, new Vector3(xs[idx++], frontY, 0f), stageLevel);
        }

        // optional delay between rows
        if (back.Count > 0 && wave.secondRowDelay > 0f)
            yield return WaitForSecondsGameplay(wave.secondRowDelay);


        // ---- BACK ROW ----
        int backCount = 0; foreach (var e in back) backCount += e.count;
        if (backCount > 0)
        {
            var xs = DistributeAlongX(xMin, xMax, backCount);
            int idx = 0;
            foreach (var e in back)
                for (int i = 0; i < e.count; i++)
                    SpawnOne(e, new Vector3(xs[idx++], backY, 0f), stageLevel);
        }

        yield return null;
    }

    // ---------- LOW-LEVEL UTILS ----------

    bool ValidateEntry(WaveEntry e)
    {
        if (!e.enemyPrefab) { Debug.LogWarning("WaveEntry missing enemyPrefab"); return false; }
        if (!e.statsBase) { Debug.LogWarning("WaveEntry missing statsBase"); return false; }
        return true;
    }

    void SpawnOne(WaveEntry entry, Vector3 pos, int stageLevel)
    {
        var go = Instantiate(entry.enemyPrefab, pos, Quaternion.identity);
        _alive++;

        if (!HasSpawnedFirstEnemy)
        {
            HasSpawnedFirstEnemy = true;
            OnFirstEnemySpawned?.Invoke();

            // Enemies are now on screen - this is the cue unit health bars use.
            EnemiesHaveAppeared = true;
            OnAnyFirstEnemySpawned?.Invoke();
        }

        var em = go.GetComponent<EnemyManager>();
        if (em)
        {
            em.statsBase = entry.statsBase;
            em.unitLevel = entry.unitLevel;         // growth (ProgressionConfigSO in EnemyManager)
            em.stageLevel = stageLevel;              // CP weights stage
            em.cpWeights = cpWeights;               // optional
            em.Initialize(stageLevel);               // builds stats, sets HP, computes CP
            if (cpBattle) cpBattle.RegisterEnemy(em);
        }

        var eh = go.GetComponent<EnemyStats>();
        if (eh) StartCoroutine(WatchDeath(eh));
    }

    IEnumerator WatchDeath(EnemyStats e)
    {
        while (e && !e.enemyIsdead) yield return null;
        _alive = Mathf.Max(0, _alive - 1);
    }

    /// <summary>
    /// N positions laid out as a CENTRED grid inside [min,max].
    ///
    /// REWRITTEN 2026-09-21 (Arash, from two annotated screenshots of stage 6).
    /// The old version started at the left edge and clamped anything that fell
    /// outside, which broke in two visible ways with the real gate-relative box
    /// (6 wide, minSlotSpacing.x 3):
    ///
    ///   2 enemies: dx = 6/3 = 2, raised to the minimum 3, first slot at
    ///              xMin + 3 = the box CENTRE -> the pair sat at centre and
    ///              centre+3, i.e. visibly shoved to the right.
    ///   3 enemies: dx = 6/4 = 1.5, raised to 3, slots at centre, centre+3 and
    ///              centre+6 - and centre+6 is outside the box, so Mathf.Clamp
    ///              pulled it back ONTO centre+3. TWO ENEMIES ON ONE SPOT.
    ///
    /// The clamp could only ever collapse slots together; it could not make them
    /// fit. So spacing is now CAPPED to what the box can actually hold and the
    /// block is CENTRED, which makes both of those impossible:
    ///
    ///   - minSpacing is honoured as a minimum only while it fits. Past that the
    ///     row is spread edge to edge instead of overflowing.
    ///   - every slot is distinct by construction, so nothing overlaps.
    ///   - the formation is symmetric about the box centre, so it reads as
    ///     deliberate placement rather than drift to one side.
    ///   - a short final row is centred on its own, not left-aligned.
    ///
    /// With the current box this gives exactly the arrangement Arash drew:
    ///   2 enemies -> centre-1.5, centre+1.5
    ///   3 enemies -> centre-3,   centre, centre+3
    /// both at the same 3-unit spacing.
    /// </summary>
    static List<Vector3> GenerateGridPositions(Vector2 min, Vector2 max, int count, int columns, Vector2 minSpacing)
    {
        var positions = new List<Vector3>(count);
        if (count <= 0) return positions;

        float xMin = Mathf.Min(min.x, max.x);
        float xMax = Mathf.Max(min.x, max.x);
        float yMin = Mathf.Min(min.y, max.y);
        float yMax = Mathf.Max(min.y, max.y);
        float w = Mathf.Max(0f, xMax - xMin);
        float h = Mathf.Max(0f, yMax - yMin);

        // Never ask for more columns than there are units - an empty trailing
        // column would push the row off centre.
        int cols = (columns > 0) ? columns : Mathf.CeilToInt(Mathf.Sqrt(count));
        cols = Mathf.Clamp(cols, 1, count);
        int rows = Mathf.CeilToInt(count / (float)cols);

        float dx = SlotSpacing(w, cols, minSpacing.x);
        float dy = SlotSpacing(h, rows, minSpacing.y);

        float xCentre = (xMin + xMax) * 0.5f;
        float yCentre = (yMin + yMax) * 0.5f;
        float yStart = yCentre - (rows - 1) * dy * 0.5f;

        int placed = 0;
        for (int r = 0; r < rows && placed < count; r++)
        {
            int inRow = Mathf.Min(cols, count - placed);
            float rowStart = xCentre - (inRow - 1) * dx * 0.5f;

            for (int c = 0; c < inRow; c++)
            {
                float x = rowStart + c * dx;
                float y = yStart + (rows - 1 - r) * dy;   // top-to-bottom
                positions.Add(new Vector3(x, y, 0f));
                placed++;
            }
        }
        return positions;
    }

    /// <summary>
    /// Gap between adjacent slots along one axis: the authored minimum when the
    /// box can hold it, otherwise the widest gap that still fits edge to edge.
    /// Returns 0 for a single slot, which centres it.
    /// </summary>
    static float SlotSpacing(float span, int slots, float desired)
    {
        if (slots <= 1) return 0f;

        float widestThatFits = span / (slots - 1);
        if (desired <= 0f) return widestThatFits;

        return Mathf.Min(desired, widestThatFits);
    }

    // Evenly spaced X positions between xMin..xMax (inclusive ends)
    static float[] DistributeAlongX(float xMin, float xMax, int count)
    {
        var xs = new float[count];
        if (count == 1) { xs[0] = (xMin + xMax) * 0.5f; return xs; }
        float step = (xMax - xMin) / (count - 1);
        for (int i = 0; i < count; i++) xs[i] = xMin + i * step;
        return xs;
    }
}
