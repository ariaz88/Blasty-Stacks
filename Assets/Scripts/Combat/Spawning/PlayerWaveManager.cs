using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerWaveManager : MonoBehaviour
{
 


    [Header("Refs")]
    [SerializeField] private MatchResolver matchResolver;    // assign your MatchResolver
    [SerializeField] private GameObject[] playerPrefabs;     // assign multiple Player prefabs here
    [SerializeField] private GameObject playerRootPrefab;


    [Tooltip("Every deploy stage on the castle, INCLUDING the two purchasable side " +
             "slots. A stage carrying a locked DeployStageSlot is skipped - do not " +
             "remove it from this array, the lock is checked live on every wave.")]
    [SerializeField] private Transform[] gatePoints = new Transform[4]; // spawn points

    [Header("Wave Settings")]
    [SerializeField, Min(0f)] private float nextWaveDelay = 0.75f;
    [SerializeField] private int minPerWave = 1;   // inclusive
    [SerializeField] private int maxPerWave = 4;   // inclusive


    [Tooltip("If true, we also freeze physics while locked (Rigidbody2D.Static).")]
    [SerializeField] private bool freezePhysicsWhileLocked = true;


    [Header("Spawn Safety")]
    [SerializeField] private LayerMask playerLayer;                 // layer used by players
    [SerializeField, Min(0f)] private float occupyCheckRadius = 0.3f;

    // runtime
    private readonly List<PlayerManager> currentWave = new();
    private bool waveLocked = false;
    private bool running = false;

    [SerializeField, Min(0.1f)] private float jumpWaitTimeout = 0.5f; // failsafe

    [Header("Jump Lanes")]
    [Tooltip("Where each successive wave LANDS. 1st match -> element 0, 2nd -> element 1, " +
             "and so on, so waves form ranks instead of piling onto each other. " +
             "Leave empty to keep the old fixed-distance jump.")]
    [SerializeField] private Transform[] jumpLanes;

    [Tooltip("Repacks the formation after each rank lands. Left empty = found in " +
             "the scene at Awake; none in the scene = no gap filling.")]
    [SerializeField] private FormationGapFiller gapFiller;

    [Tooltip("ON = waves past the last lane all reuse the last one. OFF = they fall back " +
             "to the fixed-distance jump.")]
    [SerializeField] private bool reuseLastLane = true;

    [Header("Battle Gate")]
    [Tooltip("ON = heroes still JUMP into the field on every match, but hold position " +
             "until the battle has started and the first enemy has actually spawned. " +
             "OFF = old behaviour, they advance the moment they land.")]
    [SerializeField] private bool holdUntilFirstEnemySpawns = false;

    [Tooltip("Used only by the flag above. Left empty = found in the scene at Awake.")]
    [SerializeField] private EnemySpawner enemySpawner;

    // How many waves have been released so far - this is the lane index.
    private int waveUnlockIndex = 0;

    /// <summary>
    /// True when heroes are allowed to advance: either the gate is off, or the
    /// spawner has put at least one enemy on the field.
    /// </summary>
    private bool CombatLive =>
        !holdUntilFirstEnemySpawns ||
        (enemySpawner != null && enemySpawner.HasSpawnedFirstEnemy);

    [Header("Stage Animation")]
    private bool unlockAnimInProgress = false;
    private int pendingStageEvents = 0;


    private GameStartManager _gsm;
    private PlayerUnitsModel PlayerUnits => _gsm ? _gsm.PlayerUnits : null;
    private UnitsDatabaseSO _unitsDb;

    public int RuleLevel => LevelBattleRules.ResolveLevel(gameObject, enemySpawner ? enemySpawner.levelConfig : null);
    public bool UsesDeploymentRules => LevelBattleRules.AppliesTo(RuleLevel);
    public int MatchesCleared { get; private set; }
    public int MatchesReleased { get; private set; }
    public bool DeploymentFailed { get; private set; }
    private bool sealedForBattle;
    private readonly List<UnitDefinitionSO> plannedHeroes = new();
    private readonly List<PlayerManager> releasedHeroes = new();
    private int plannedSpawnIndex;
    public IReadOnlyList<PlayerManager> ReleasedHeroes => releasedHeroes;
    public bool DeploymentsReady
    {
        get
        {
            if (DeploymentFailed || MatchesReleased < MatchesCleared) return false;
            foreach (var hero in releasedHeroes)
            {
                if (!hero) continue;
                var jump = hero.GetComponent<FrogJumpTransformOnly>();
                if (jump && jump.IsJumping) return false;
            }
            return true;
        }
    }

    public void SealForBattle()
    {
        if (UsesDeploymentRules) sealedForBattle = true;
    }

    public double ReleasedPlayerCP
    {
        get
        {
            double cp = 0;
            foreach (var hero in releasedHeroes) if (hero) cp += CPCalculator.UnitPower(hero.unitStats);
            return cp;
        }
    }

    // ======================================================================
    //  PHASE 2 - heroes are EARNED into a roster, not spawned onto the gates
    // ======================================================================

    [Header("PHASE 2 - Card Rewards")]
    [Tooltip("ON (the PHASE 2 default): no hero is ever instantiated onto a gate " +
             "platform - not at level start and not after a match. The per-match " +
             "counts are unchanged; the heroes are recorded in EarnedHeroes and " +
             "announced by the card animation instead. OFF restores the old " +
             "spawn-and-leap behaviour.")]
    [SerializeField] private bool suppressStageSpawning = true;

    [Tooltip("WHICH hero types each match awards. Optional - a level with no entry " +
             "in this asset keeps the old behaviour (counts from LevelBattleRules, " +
             "types drawn at random). Left empty = every level behaves that way.")]
    [SerializeField] private StageDeploymentPlanSO deploymentPlan;

    [Tooltip("How long a deployed hero STANDS ON THE GATE before it leaps into the " +
             "battlefield, in seconds. Separate from reinforcementGateHold (0.75) " +
             "which belongs to the gem buy-back: 1.20 is that beat +60%, asked for " +
             "after the first deployment play-test because the leap read as instant.")]
    [SerializeField, Min(0f)] private float deployGateHold = 1.2f;

    private readonly List<UnitDefinitionSO> earnedHeroes = new();

    /// <summary>
    /// The heroes earned by EACH match, kept separate rather than flattened.
    ///
    /// HeroDeploymentSequencer releases them one BATCH at a time - one 6s load per
    /// match - so which hero came from which match is load-bearing, not
    /// bookkeeping. earnedHeroes stays as the flat roster for anything that only
    /// needs the total.
    /// </summary>
    private readonly List<List<UnitDefinitionSO>> earnedBatches = new();

    /// <summary>One list per cleared match, in the order the matches were cleared.</summary>
    public IReadOnlyList<IReadOnlyList<UnitDefinitionSO>> EarnedBatches => earnedBatches;

    /// <summary>
    /// Every hero earned so far this stage, in the order the matches awarded them.
    /// This is the roster the (still to be designed) deployment step will draw
    /// from - nothing consumes it yet.
    /// </summary>
    public IReadOnlyList<UnitDefinitionSO> EarnedHeroes => earnedHeroes;

    /// <summary>True while PHASE 2 card rewards are in force instead of gate spawns.</summary>
    public bool CardRewardsActive => suppressStageSpawning;

    /// <summary>
    /// Raised once per cleared match with the heroes that match earned and a world
    /// position to anchor the presentation to (the centre of the player base).
    ///
    /// STATIC, and deliberately never cleared here - HeroCardRevealDirector
    /// unsubscribes in its own OnDisable, the same contract MatchResolver.OnBlast
    /// already has in this file.
    /// </summary>
    public static event System.Action<IReadOnlyList<UnitDefinitionSO>, Vector3> HeroesEarned;

    /// <summary>
    /// Takes the next <paramref name="count"/> heroes off the planned roster,
    /// records them as earned and announces them.
    ///
    /// Draws from plannedHeroes rather than re-rolling, so the heroes shown on the
    /// cards are the SAME ones the old code would have put on the gates - the
    /// selection logic is untouched, only its presentation changed.
    /// </summary>
    private void AwardHeroesForMatch(int count) => AwardHeroesForMatch(count, MatchesReleased + 1);

    /// <summary>
    /// Takes this match's heroes and records + announces them.
    ///
    /// TYPES come from the authored StageDeploymentPlanSO when the level has one,
    /// so a designer decides that match 3 gives an archer and a brute. Without a
    /// plan it falls back to the pre-PHASE-2 behaviour unchanged: the pre-rolled
    /// plannedHeroes list, then a fresh random draw if that runs short.
    /// </summary>
    private void AwardHeroesForMatch(int count, int matchNumber)
    {
        var batch = new List<UnitDefinitionSO>(Mathf.Max(0, count));

        var authored = deploymentPlan ? deploymentPlan.EntriesFor(RuleLevel, matchNumber) : null;
        if (authored != null)
        {
            // The plan's counts win outright - see StageDeploymentPlanSO. A unitId
            // with no matching definition is skipped loudly rather than silently
            // shrinking the wave.
            foreach (var entry in authored)
            {
                var def = FindUnitDefinition(entry.unitId);
                if (!def)
                {
                    Debug.LogError($"[PlayerWaveManager] Deployment plan for level {RuleLevel} " +
                                   $"match {matchNumber} names unitId {entry.unitId}, which is " +
                                   "not in the units database. Skipped.", this);
                    continue;
                }

                for (int i = 0; i < Mathf.Max(1, entry.count); i++) batch.Add(def);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                UnitDefinitionSO def = plannedSpawnIndex < plannedHeroes.Count
                    ? plannedHeroes[plannedSpawnIndex++]
                    : null;

                // Falls back to a fresh draw only if the plan ran short, which can
                // happen on stages 6+ where no plan is built at all.
                if (!def)
                {
                    var pool = GetDeployedUnitDefinitions();
                    if (pool.Count == 0) break;
                    def = pool[UnityEngine.Random.Range(0, pool.Count)];
                }

                batch.Add(def);
            }
        }

        if (batch.Count == 0) return;

        earnedHeroes.AddRange(batch);
        earnedBatches.Add(batch);
        MatchesReleased++;

        HeroesEarned?.Invoke(batch, ResolveCardAnchorWorld());
    }

    private UnitDefinitionSO FindUnitDefinition(int unitId)
    {
        if (_unitsDb == null) return null;

        foreach (var def in _unitsDb.Units)
            if (def && def.unitId == unitId) return def;

        return null;
    }

    /// <summary>
    /// Puts ONE earned batch on the field: every hero spawns on a free gate, then
    /// jumps to the REAR lane - the rank closest to the player base - rather than
    /// walking forward one rank per wave the way pre-PHASE-2 waves did.
    ///
    /// Called by HeroDeploymentSequencer the moment that batch's 6s load finishes.
    /// Deliberately does NOT touch waveLocked or currentWave: there is no lock
    /// step any more, the load bar IS the wait.
    /// </summary>
    /// <summary>One hero posed on a gate, waiting for its leap.</summary>
    private class PendingDeploy
    {
        public PlayerManager hero;
        public float releaseAt;
        public float? laneY;
    }

    private readonly List<PendingDeploy> pendingDeploys = new();

    /// <summary>
    /// Releases every deployed hero whose gate pose has expired.
    ///
    /// Deliberately in Update and NOT in a coroutine: a coroutine here can be
    /// killed by any StopAllCoroutines on this component, and a hero killed
    /// mid-hold stays locked on the platform for the rest of the battle because
    /// PlayerLockState re-asserts a Static body every FixedUpdate. This loop has
    /// no such failure mode - the worst case is a late release, never a permanent
    /// one.
    /// </summary>
    private void Update()
    {
        for (int i = pendingDeploys.Count - 1; i >= 0; i--)
        {
            var d = pendingDeploys[i];

            if (d == null || d.hero == null) { pendingDeploys.RemoveAt(i); continue; }
            if (Time.time < d.releaseAt) continue;

            pendingDeploys.RemoveAt(i);

            ApplyLock(d.hero, false);
            SetHealthBarsHiddenOnGate(d.hero, false);
            StartCoroutine(JumpThenSwitch(d.hero, d.laneY));
        }
    }

    public void DeployBatch(IReadOnlyList<UnitDefinitionSO> batch)
    {
        if (batch == null || batch.Count == 0) return;

        StartCoroutine(DeployBatchRoutine(batch));
    }

    private IEnumerator DeployBatchRoutine(IReadOnlyList<UnitDefinitionSO> batch)
    {
        float? laneY = GetRearLaneY();

        for (int i = 0; i < batch.Count; i++)
        {
            var def = batch[i];
            if (!def) continue;

            // Random free gate, so two heroes of the same type do not stack on one
            // platform. Falls back to round-robin when every gate is busy - a
            // deployment must never silently drop a hero the player earned.
            var free = GetFreeGateIndices();
            Transform gate = free.Count > 0
                ? gatePoints[free[UnityEngine.Random.Range(0, free.Count)]]
                : gatePoints[i % gatePoints.Length];

            if (!gate) continue;

            var pm = SpawnUnitAt(def, gate.position, Quaternion.identity);
            if (!pm) continue;

            pm.isUnlocked = true;
            releasedHeroes.Add(pm);

            // STAND ON THE GATE FIRST, then leap. The first version jumped on the
            // spawn frame, which read as the hero teleporting past the platform
            // entirely. The lock here is the same one a normal wave sits in
            // (Lock state + Static body), so the hero cannot drift while it poses.
            ApplyLock(pm, true);
            SetHealthBarsHiddenOnGate(pm, true);

            // !! THE HOLD IS A WATCHDOG ENTRY, NOT A COROUTINE. It used to be
            // StartCoroutine(HoldOnGateThenJump(...)), and ANY StopAllCoroutines on
            // this component during that 1.2s wait left the hero locked on the
            // platform FOREVER - PlayerLockState re-asserts a Static body every
            // FixedUpdate, so it can never recover by itself. Measured 2026-09-14:
            // 4 heroes released, only 2 reached the field; the other 2 sat on the
            // deploy stages at y=3.21 for the rest of the battle, which is what
            // "the hero only spawned once" looked like on screen.
            //
            // ReleaseDueDeployments in Update owns the wait instead, so there is no
            // coroutine to interrupt and a stranded hero cannot happen.
            pendingDeploys.Add(new PendingDeploy
            {
                hero = pm,
                releaseAt = Time.time + deployGateHold,
                laneY = laneY
            });

            if (i < batch.Count - 1)
                yield return new WaitForSeconds(reinforcementStagger);
        }
    }

    /// <summary>
    /// Centre of the player base - the average of the gate points. The card deal is
    /// placed a couple of card heights ABOVE this, never on it.
    /// </summary>
    private Vector3 ResolveCardAnchorWorld()
    {
        Vector3 sum = Vector3.zero;
        int n = 0;

        if (gatePoints != null)
            foreach (var g in gatePoints)
                if (g) { sum += g.position; n++; }

        return n > 0 ? sum / n : transform.position;
    }


    private void OnEnable()
    {
        if (matchResolver)
            MatchResolver.OnBlast += HandleBlast;   // <-- static subscribe
    }

    private void OnDisable()
    {
        if (matchResolver)
            MatchResolver.OnBlast -= HandleBlast;   // <-- static unsubscribe
    }

    private void Awake()
    {
        // Resolved first: the early return below must not leave this null, or
        // CombatLive could never become true and heroes would freeze forever.
        if (!enemySpawner) enemySpawner = FindObjectOfType<EnemySpawner>(true);
        if (!gapFiller) gapFiller = FindObjectOfType<FormationGapFiller>(true);

        _gsm = GameStartManager.Instance ? GameStartManager.Instance : FindObjectOfType<GameStartManager>();
        if (_gsm == null)
        {
            Debug.LogError("PlayerWaveManager: GameStartManager not found.");
            return;
        }

        _unitsDb = _gsm.unitsDatabase;
    }

    private List<UnitDefinitionSO> GetDeployedUnitDefinitions()
    {
        var list = new List<UnitDefinitionSO>();
        if (PlayerUnits == null || _unitsDb == null) return list;

        foreach (var def in _unitsDb.Units)
        {
            if (!def) continue;
            if (!PlayerUnits.IsUnlocked(def.unitId)) continue;
            if (!PlayerUnits.IsDeployed(def.unitId)) continue;

            list.Add(def);
        }

        return list;
    }

    private void Start()
    {
        BeginWaves();
  

    }
    public void RestartAfterRevive()
    {
        StopAllCoroutines();
        running = false;
        waveLocked = false;
        currentWave.Clear();
        waveUnlockIndex = 0;   // lanes restart from the front rank after a revive
        MatchesCleared = MatchesReleased = plannedSpawnIndex = 0;
        sealedForBattle = DeploymentFailed = unlockAnimInProgress = false;
        plannedHeroes.Clear();
        releasedHeroes.Clear();
        earnedHeroes.Clear();
        earnedBatches.Clear();
        pendingDeploys.Clear();

        BeginWaves();   // uses WaveLoop that checks puzzle again
    }

    // Call this to start the loop (e.g., at level start)
    public void BeginWaves()
    {
        if (running) return;
        running = true;

        if (UsesDeploymentRules) StartCoroutine(PlannedWaveLoop());
        else if (!suppressStageSpawning) StartCoroutine(WaveLoop());
        // else: stages 6+ under PHASE 2 need no wave loop at all - there is nothing
        // to spawn or unlock, so HandleBlast awards the heroes directly.
    }

    private IEnumerator PlannedWaveLoop()
    {
        while (running && !PuzzleHasStacks() && MatchesCleared == 0) yield return null;
        if (!running) yield break;

        // Same deployed roster and uniform random selection as SpawnOneAt.
        // Reserve the attempt's order without changing hero-type eligibility.
        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            FailDeployment("No deployed hero types are available.");
            yield break;
        }
        int max = LevelBattleRules.TotalHeroes(RuleLevel, LevelBattleRules.TotalPairs(RuleLevel));
        for (int i = 0; i < max; i++)
        {
            var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];
            if (!def.runtimePrefab || !def.runtimePrefab.GetComponent<PlayerManager>() ||
                !def.runtimePrefab.GetComponent<PlayerStatsApplier>())
            {
                FailDeployment("A deployed type is missing its runtime prefab or stats components.");
                yield break;
            }
            plannedHeroes.Add(def);
        }

        for (int match = 1; running && match <= LevelBattleRules.TotalPairs(RuleLevel); match++)
        {
            if (sealedForBattle && match > MatchesCleared) break;
            int count = LevelBattleRules.HeroesForMatch(RuleLevel, match);

            // PHASE 2: no gate spawn, no lock, no release jump. Wait for the match,
            // award its heroes into the roster, let the card animation announce
            // them. Everything below this block is the pre-PHASE-2 path.
            if (suppressStageSpawning)
            {
                while (running && MatchesCleared < match && !sealedForBattle) yield return null;
                if (!running) yield break;
                if (MatchesCleared < match) break;

                AwardHeroesForMatch(count);

                if (match < LevelBattleRules.TotalPairs(RuleLevel))
                    yield return new WaitForSeconds(nextWaveDelay);

                continue;
            }

            float waited = 0;
            while (running && GetFreeGateIndices().Count < count && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (!running) yield break;
            if (GetFreeGateIndices().Count < count)
            {
                FailDeployment("Not enough free permanent deployment stages for the required wave.");
                yield break;
            }
            // Queued matches remain valid after the final pieces disappear or the
            // battle camera hides the board. Never discard those earned summons.
            SpawnLockedWave(count, true);
            if (currentWave.Count != count)
            {
                FailDeployment("Could not spawn every hero in the required wave.");
                yield break;
            }
            while (running && MatchesCleared < match && !sealedForBattle) yield return null;
            if (!running) yield break;
            if (MatchesCleared < match)
            {
                foreach (var hero in currentWave) if (hero) Destroy(hero.gameObject);
                currentWave.Clear();
                waveLocked = false;
                break;
            }
            PlayStageUnlockAnimations();
            float timer = 0;
            while (running && waveLocked)
            {
                timer += Time.deltaTime;
                if (timer >= 1.5f)
                {
                    unlockAnimInProgress = false;
                    pendingStageEvents = 0;
                    UnlockCurrentWaveViaAnimation();
                }
                yield return null;
            }
            if (match < LevelBattleRules.TotalPairs(RuleLevel)) yield return new WaitForSeconds(nextWaveDelay);
        }
        running = false;
    }

    private void FailDeployment(string reason)
    {
        DeploymentFailed = true;
        running = false;
        Debug.LogError("[PlayerWaveManager] " + reason, this);
    }

    // Call this when level ends (win/lose)
    public void StopWaves()
    {
        running = false;
        StopAllCoroutines();
    }


    // ---------------- Wave Loop ----------------

    private IEnumerator WaveLoop2()
    {
        // Initial wave: only if puzzle has stacks
        if (PuzzleHasStacks())
        {
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
        else
        {
            // Board already empty → no reason to run the wave system
            running = false;
            yield break;
        }

        while (running)
        {
            // Wait until this wave is unlocked by a puzzle match.
            // HandleBlast will flip waveLocked -> false
            while (running && waveLocked)
                yield return null;

            if (!running) yield break;

            // After the wave is unlocked, check if the puzzle is now empty.
            // If yes, do NOT spawn any more players.
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // A little delay before next wave
            yield return new WaitForSeconds(nextWaveDelay);

            // Double-check right before spawning the next wave
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // Spawn next wave (locked again)
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
    }


    private void SpawnLockedWave(int count, bool earnedOrPlanned = false)
    {
        // Safety: don't spawn if puzzle is empty
        if (!earnedOrPlanned && !PuzzleHasStacks())
            return;

        currentWave.Clear();

        // choose up to 'count' free gate indices without replacement
        var freeIndices = GetFreeGateIndices();
        Shuffle(freeIndices);

        int toSpawn = Mathf.Clamp(count, 0, freeIndices.Count);
        for (int i = 0; i < toSpawn; i++)
        {
            int gi = freeIndices[i];


            var stage = gatePoints[gi];
            var pm = SpawnOneAt(stage.position, stage.rotation);

            if (pm != null)
            {
                pm.childSnapshot = new List<ChildLocalSnapshot>();

                foreach (Transform child in pm.transform)
                {
                    pm.childSnapshot.Add(new ChildLocalSnapshot
                    {
                        t = child,
                        localPos = child.localPosition,
                        localRot = child.localRotation,
                        localScale = child.localScale
                    });
                }

                Transform topAnchor = GetStageTopAnchor(stage);
                pm.transform.SetParent(topAnchor, true);


                ApplyLock(pm, true);
                currentWave.Add(pm);
            }

        }

        waveLocked = true; // this wave starts locked
    }



    // ---------------- Wave Loop Without Checking Blocks Present in The Board ----------------
    private IEnumerator WaveLoop1()
    {

        // initial wave
        SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));

        while (running)
        {
            // Wait here until this wave is unlocked by a puzzle match.
            // HandleBlast will flip waveLocked -> false
            while (running && waveLocked) yield return null;

            if (!running) yield break;

            // A little delay before next wave
            yield return new WaitForSeconds(nextWaveDelay);

            // Spawn next wave (locked again)
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
    }

    private void SpawnLockedWave1(int count)
    {
        currentWave.Clear();

        // choose up to 'count' free gate indices without replacement
        var freeIndices = GetFreeGateIndices();
        Shuffle(freeIndices);

        int toSpawn = Mathf.Clamp(count, 0, freeIndices.Count);
        for (int i = 0; i < toSpawn; i++)
        {
            int gi = freeIndices[i];
            var pm = SpawnOneAt(gatePoints[gi].position, gatePoints[gi].rotation);

            if (pm != null)
            {
                // Registering catches this unit up on every buff already picked
                // for its hero type, so a late arrival is never under-buffed.
                var roguelite = FindObjectOfType<RogueliteManager>();
                if (roguelite != null)
                    roguelite.RegisterPlayer(pm.GetComponent<PlayerStatsApplier>());

                ApplyLock(pm, true);
                currentWave.Add(pm);
            }
        }

        waveLocked = true; // this wave starts locked
    }


    private IEnumerator WaveLoop()
    {
        // 1) Wait until there is at least one active block on the board
        //    (MatchResolver will dynamically search PieceSimple for us).
        while (running && (matchResolver == null || !matchResolver.HasAnyStacksLeft()))
        {
            // Wait one frame and try again
            yield return null;
        }

        // If something stopped waves externally while we were waiting, bail out.
        if (!running)
            yield break;

        // 2) Now we know there are blocks on the board → spawn the first wave
        SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));

        while (running)
        {
            // Wait until this wave is unlocked by a puzzle match.
            // HandleBlast will flip waveLocked -> false
            while (running && waveLocked)
                yield return null;

            if (!running)
                yield break;

            // After the wave is unlocked, check if the puzzle is now empty.
            // If yes, do NOT spawn any more players.
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // Small delay before next wave
            yield return new WaitForSeconds(nextWaveDelay);

            // Double-check right before spawning the next wave
            if (!PuzzleHasStacks())
            {
                running = false;
                yield break;
            }

            // Spawn next wave (locked again)
            SpawnLockedWave(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
        }
    }

    // ---------------- End Of Wave Loop ----------------


    private PlayerManager SpawnOneAt1(Vector3 pos, Quaternion rot)
    {
        if (playerPrefabs[0] == null) return null;
        var go = Instantiate(playerPrefabs[0], pos, rot);

        Vector3 ls = go.transform.localScale;
        ls *= 1.1f;
        go.transform.localScale = ls;

        var pm = go.GetComponent<PlayerManager>();

        if (pm == null)
        {
            Debug.LogWarning("Spawned prefab has no PlayerManager.", go);
            return null;
        }

        return pm;
    }
    private PlayerManager SpawnOneAt2(Vector3 pos, Quaternion rot)
    {
        // safety: no prefabs assigned
        if (playerPrefabs == null || playerPrefabs.Length == 0)
        {
            Debug.LogWarning("PlayerWaveManager: No playerPrefabs assigned in the inspector.");
            return null;
        }

        // pick a random prefab from the array
        GameObject randomPrefab = playerPrefabs[UnityEngine.Random.Range(0, playerPrefabs.Length)];
        if (randomPrefab == null)
        {
            Debug.LogWarning("PlayerWaveManager: Selected random player prefab is null.");
            return null;
        }

        // instantiate the chosen prefab
        var go = Instantiate(randomPrefab, pos, rot);

        // same scale tweak as before
        Vector3 ls = go.transform.localScale;
        ls *= 1.1f;
        go.transform.localScale = ls;

        var pm = go.GetComponent<PlayerManager>();
        if (pm == null)
        {
            Debug.LogWarning("Spawned prefab has no PlayerManager.", go);
            return null;
        }

        return pm;
    }

    private PlayerManager SpawnOneAt3(Vector3 pos, Quaternion rot)
    {
        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        // Choose which unit to spawn (random or ordered)
        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        if (!def.visualPrefab)
        {
            Debug.LogWarning($"Unit {def.displayName} has no runtimePrefab.");
            return null;
        }

        var go = Instantiate(def.visualPrefab, pos, rot);

        // Scale tweak (keep your existing logic)
        go.transform.localScale *= 1.1f;

        var pm = go.GetComponent<PlayerManager>();
        if (!pm)
        {
            Debug.LogWarning("Spawned prefab missing PlayerManager.", go);
            Destroy(go);
            return null;
        }


        return pm;
    }

    private PlayerManager SpawnOneAt4(Vector3 pos, Quaternion rot)
    {
        if (playerRootPrefab == null)
        {
            Debug.LogError("PlayerWaveManager: playerRootPrefab not assigned.");
            return null;
        }

        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        // Choose which deployed unit to spawn
        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        // 1) Spawn player root (brain, physics, FSM)
        var root = Instantiate(playerRootPrefab, pos, rot);
        var pm = root.GetComponent<PlayerManager>();
        if (pm == null)
        {
            Debug.LogError("Player root prefab has no PlayerManager.");
            Destroy(root);
            return null;
        }

        // 2) Inject visual
        if (pm.visualRoot != null && def.visualPrefab != null)
        {
            for (int i = pm.visualRoot.childCount - 1; i >= 0; i--)
                Destroy(pm.visualRoot.GetChild(i).gameObject);

            var visual = Instantiate(def.visualPrefab, pm.visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = def.uiVisualScale;
        }

        // 3) Configure PlayerStatsApplier (THIS IS THE CRITICAL PART)
        var statsApplier = pm.playerStatsApplier != null
            ? pm.playerStatsApplier
            : pm.GetComponent<PlayerStatsApplier>();

        if (statsApplier != null)
        {
            statsApplier.enabled = true;

            // 🔑 Single source of truth
            typeof(PlayerStatsApplier)
                .GetField("unitId", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(statsApplier, def.unitId);

            //// Force immediate recompute (safe even if Awake already ran)

            //// Sync PlayerManager runtime cache
            statsApplier.SetUnitId(def.unitId);
            statsApplier.ApplyNow();
            pm.unitStats = statsApplier.CurrentStats;

        }
        else
        {
            Debug.LogWarning("Spawned player has no PlayerStatsApplier.");
        }

        return pm;
    }
    private PlayerManager SpawnOneAt5(Vector3 pos, Quaternion rot)
    {
        if (playerRootPrefab == null)
        {
            Debug.LogError("PlayerWaveManager: playerRootPrefab not assigned.");
            return null;
        }

        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        var root = Instantiate(playerRootPrefab, pos, rot);
        var pm = root.GetComponent<PlayerManager>();
        if (!pm)
        {
            Destroy(root);
            return null;
        }

        // Inject visual
        if (pm.visualRoot != null && def.visualPrefab != null)
        {
            for (int i = pm.visualRoot.childCount - 1; i >= 0; i--)
                Destroy(pm.visualRoot.GetChild(i).gameObject);

            var visual = Instantiate(def.visualPrefab, pm.visualRoot);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = def.uiVisualScale;
        }

        // Configure stats
        var applier = pm.playerStatsApplier;
        if (applier != null)
        {
            applier.SetUnitId(def.unitId);
            applier.ApplyNow();
            pm.unitStats = applier.CurrentStats;
        }

        return pm;
    }
    private PlayerManager SpawnOneAt(Vector3 pos, Quaternion rot)
    {
        if (UsesDeploymentRules)
        {
            if (plannedSpawnIndex >= plannedHeroes.Count) return null;
            return SpawnUnitAt(plannedHeroes[plannedSpawnIndex++], pos, rot);
        }
        var deployed = GetDeployedUnitDefinitions();
        if (deployed.Count == 0)
        {
            Debug.LogWarning("No deployed units available to spawn.");
            return null;
        }

        // Pick one deployed unit
        var def = deployed[UnityEngine.Random.Range(0, deployed.Count)];

        return SpawnUnitAt(def, pos, rot);
    }

    /// <summary>
    /// Instantiates ONE specific unit and stamps its identity onto the stats
    /// system. Split out of SpawnOneAt so the reinforcement flow below can spawn
    /// a CHOSEN type through exactly the same path the wave system uses - the
    /// unitId set here is also what PlayerManager files itself under in
    /// <see cref="HeroRoster"/>.
    /// </summary>
    private PlayerManager SpawnUnitAt(UnitDefinitionSO def, Vector3 pos, Quaternion rot)
    {
        if (def == null) return null;

        if (def.runtimePrefab == null)
        {
            Debug.LogError($"Unit '{def.displayName}' has no runtimePrefab assigned.");
            return null;
        }

        // Instantiate THAT unit directly
        var go = Instantiate(def.runtimePrefab, pos, rot);

        var pm = go.GetComponent<PlayerManager>();
        if (pm == null)
        {
            Debug.LogError($"Runtime prefab '{def.runtimePrefab.name}' has no PlayerManager.");
            Destroy(go);
            return null;
        }

        // Summon arrival VFX. Added here rather than on each runtime prefab so
        // every unit gets it through the one funnel both arrival paths already
        // share - the wave unlock and SpawnReinforcements. It attaches itself to
        // the unit's FrogJumpTransformOnly and no-ops on units that have none.
        if (!go.GetComponent<SummonArrivalBinder>())
            go.AddComponent<SummonArrivalBinder>();

        // Apply unit identity to stats system
        if (pm.playerStatsApplier != null)
        {
            pm.playerStatsApplier.SetUnitId(def.unitId);
            pm.playerStatsApplier.ApplyNow();

            // ApplyNow leaves CurrentStats null when GameStartManager/PlayerUnits
            // is missing; never overwrite the prefab's stats with that null.
            if (pm.playerStatsApplier.CurrentStats != null)
            {
                pm.unitStats = pm.playerStatsApplier.CurrentStats;
                // A fresh summon starts with its actual upgraded maximum HP.
                var health = pm.GetComponent<PlayerStats>();
                if (UsesDeploymentRules && health)
                    health.currentHP = health.maxHealth = pm.unitStats.maxHP;
            }
            else
                Debug.LogWarning($"[PlayerWaveManager] Spawned '{def.displayName}' with no " +
                                 "computed stats - keeping prefab defaults.", pm);
        }

        // Unconditional, at EVERY stage. This registers the hero for CP reporting and
        // attaches MeleeContactRecovery. It used to be gated on UsesDeploymentRules
        // (stages 1-5) AND bailed out again inside for any other level, so heroes from
        // stage 6 on never got the melee unstick helper at all.
        CPBattleController.RegisterHero(pm);
        return pm;
    }

    // ---------------- Mid-battle reinforcements ----------------

    [Header("Reinforcements")]
    [Tooltip("Beat between two reinforcements of the same purchase, so a squad " +
             "walks out of the castle instead of appearing as one clump.")]
    [SerializeField, Min(0f)] private float reinforcementStagger = 0.12f;

    [Tooltip("How long a bought hero STANDS ON THE GATE before it leaps into the " +
             "field, so the arrival reads as 'lands on the castle, then jumps in' " +
             "instead of appearing and vanishing on the same frame. " +
             "0 = the old instant behaviour.")]
    [SerializeField, Min(0f)] private float reinforcementGateHold = 0.75f;

    /// <summary>
    /// True when a gem purchase for this unit type could actually put heroes on
    /// the field. The HUD checks this BEFORE charging gems, so a missing prefab
    /// can never take the player's money and deliver nothing.
    /// </summary>
    public bool CanSpawnReinforcements(int unitId)
    {
        if (_unitsDb == null) return false;

        // Counts only OPEN stages: with every side slot locked and the four
        // permanent ones somehow unassigned there is nowhere to land, and the HUD
        // must find that out before it charges the player.
        if (GetUsableGates().Count == 0) return false;

        var def = _unitsDb.GetById(unitId);
        return def != null && def.runtimePrefab != null;
    }

    /// <summary>
    /// Buys a wiped-out hero type back into the fight: spawns <paramref name="count"/>
    /// of it at the castle gates and sends each one jumping into the REAR rank,
    /// from where it marches forward under its own state machine.
    ///
    /// Unlike a normal wave these are NOT locked - the battle is already running,
    /// so they skip the puzzle gate entirely.
    /// </summary>
    public void SpawnReinforcements(int unitId, int count)
    {
        if (count <= 0) return;

        if (!CanSpawnReinforcements(unitId))
        {
            Debug.LogError($"[PlayerWaveManager] Cannot spawn reinforcements for unitId={unitId}.", this);
            return;
        }

        StartCoroutine(SpawnReinforcementsRoutine(_unitsDb.GetById(unitId), count));
    }

    private IEnumerator SpawnReinforcementsRoutine(UnitDefinitionSO def, int count)
    {
        // Rear rank: reinforcements arrive BEHIND the survivors and walk up, they
        // never materialise in the middle of the fight.
        float? laneY = GetRearLaneY();

        // Resolved ONCE for the whole batch: round-robin over the stages that are
        // actually open, so an unbought side slot never eats a reinforcement.
        var usableGates = GetUsableGates();
        if (usableGates.Count == 0)
        {
            Debug.LogWarning("[PlayerWaveManager] No usable deploy stage for reinforcements.", this);
            yield break;
        }

        for (int i = 0; i < count; i++)
        {
            var gate = usableGates[i % usableGates.Count];
            if (gate == null) continue;

            var pm = SpawnUnitAt(def, gate.position, gate.rotation);
            if (pm == null) continue;

            // Marked unlocked HERE, not after the gate hold: HeroRoster.AliveCount
            // only counts unlocked heroes, so deferring this would leave the cell
            // reading 0 alive for the whole hold and let the player buy the same
            // squad a second time while it is already on its way.
            pm.isUnlocked = true;

            // Stand on the gate first, leap a beat later. The lock is the same one
            // a normal wave sits in (Lock state + Static body), so the hero cannot
            // drift or be shoved while it waits.
            if (reinforcementGateHold > 0f) ApplyLock(pm, true);

            // Per-hero coroutine rather than an inline wait: the hold has to run
            // ALONGSIDE the stagger, otherwise a squad of four takes
            // 4 * (hold + stagger) to walk out instead of overlapping.
            StartCoroutine(HoldOnGateThenJump(pm, laneY, reinforcementGateHold));

            if (reinforcementStagger > 0f && i < count - 1)
                yield return new WaitForSeconds(reinforcementStagger);
        }
    }

    /// <summary>
    /// Keeps one bought hero standing on the gate for <see cref="reinforcementGateHold"/>
    /// seconds, then releases it into the normal jump-and-pursue flow.
    /// </summary>
    private IEnumerator HoldOnGateThenJump(PlayerManager pm, float? laneY, float hold)
    {
        // On the gate the hero is scenery, not a combatant - no HP bar. The
        // prefab's own hideUntilBattleStarts cannot do this for reinforcements:
        // it keys off the FIRST ENEMY, which appeared long before the purchase.
        SetHealthBarsHiddenOnGate(pm, true);

        if (hold > 0f)
        {
            yield return new WaitForSeconds(hold);

            // Destroyed while it waited (stage cleared, revive reset, ...).
            if (pm == null) yield break;
        }

        ApplyLock(pm, false);

        yield return JumpThenSwitch(pm, laneY);

        // Landed and marching - the bar is its own again. Null-guarded inside:
        // the hero can die to a stray hit before the jump resolves.
        SetHealthBarsHiddenOnGate(pm, false);
    }

    /// <summary>
    /// Shows/hides every HP bar under one hero for the castle-gate pose.
    /// GetComponentsInChildren rather than PlayerStats.healthBar: a prefab may
    /// carry more than one bar (unit + shield), and the inactive flag matters
    /// because the canvas is already disabled by the time we come back.
    /// </summary>
    private static void SetHealthBarsHiddenOnGate(PlayerManager pm, bool hidden)
    {
        if (pm == null) return;

        foreach (var bar in pm.GetComponentsInChildren<HealthBar>(true))
            if (bar) bar.SetHiddenOnGate(hidden);
    }

    /// <summary>
    /// The world Y of the BACK rank. jumpLanes is authored front-first (index 0 is
    /// closest to the enemy), so the last entry is the one furthest back.
    /// Null means "no lanes authored" - the jumper keeps its fixed distance.
    /// </summary>
    private float? GetRearLaneY()
    {
        if (jumpLanes == null || jumpLanes.Length == 0) return null;

        for (int i = jumpLanes.Length - 1; i >= 0; i--)
            if (jumpLanes[i]) return jumpLanes[i].position.y;

        return null;
    }


    private void ApplyLock(PlayerManager pm, bool locked)
    {
        // Option A: switch to your Lock state if provided
        if (locked /*&& lockState != null*/)
        {
            pm.SwitchToNextState(pm.PlayerLockState);  // <-- use your actual API to change states
        }

        // Option B: fallback flags if no state provided
        pm.canMove = !locked;
        pm.SetAnimMoving(false);

        if (pm.playerRigidbody != null && freezePhysicsWhileLocked)
            pm.playerRigidbody.bodyType = locked ? RigidbodyType2D.Static : RigidbodyType2D.Dynamic;

    }

    private void UnlockCurrentWave1()
    {
        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

                pm.SwitchToNextState(pm.PlayerPursueTargetState); // <-- use your state-machine API
               pm.isUnlocked = true;

            // Option B: unfreeze flags
            ApplyLock(pm, false);
        }

        waveLocked = false;
    }


    private void UnlockCurrentWave()
    {
        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

            // Unfreeze/unlock flags first if you need
            pm.isUnlocked = true;
            ApplyLock(pm, false);

            // Kick off: jump now, then pursue when done
            StartCoroutine(JumpThenSwitch(pm, null)); // dead sibling: no lane
        }

        waveLocked = false;
    }




    // Called by MatchResolver when a group clears
    private void HandleBlast(int _)
    {
        if (UsesDeploymentRules)
        {
            if (sealedForBattle || DeploymentFailed) return;
            MatchesCleared = Mathf.Min(LevelBattleRules.TotalPairs(RuleLevel), MatchesCleared + Mathf.Max(0, _));
            return; // the planned loop drains every earned match, even during animation
        }

        // PHASE 2, stages 6+: no wave was ever spawned, so there is nothing to
        // unlock - the match awards its heroes straight into the roster. The size
        // range is the same one the old random wave drew from.
        if (suppressStageSpawning)
        {
            if (!running) return;
            AwardHeroesForMatch(UnityEngine.Random.Range(minPerWave, maxPerWave + 1));
            return;
        }

        if (!running) return;
        if (!waveLocked) return; // already unlocked; ignore extra blasts
        if (unlockAnimInProgress) return; // ignore extra blasts while animation is running

        PlayStageUnlockAnimations();

    }


    private void PlayStageUnlockAnimations()
    {
        // prevent retriggering while we are waiting for animation events
        if (unlockAnimInProgress)
            return;

        pendingStageEvents = 0;

        for (int i = 0; i < gatePoints.Length; i++)
        {
            Transform gate = gatePoints[i];
            if (gate == null) continue;

            // only trigger the stage if it currently has a player on it
            Collider2D c = Physics2D.OverlapCircle((Vector2)gate.position, occupyCheckRadius, playerLayer);
            if (c == null) continue;

            Animator anim = gate.GetComponentInChildren<Animator>();
            if (anim == null) continue;

            pendingStageEvents++;

            // THIS IS THE ONLY CORRECT PLACE
            anim.ResetTrigger("Throw");
            anim.SetTrigger("Throw");

        }

        // If for any reason no stage detected (shouldn’t happen), unlock immediately as a fallback
        if (pendingStageEvents == 0)
        {
            UnlockCurrentWaveViaAnimation();
            return;
        }

        unlockAnimInProgress = true;
    }
    public void OnOneStageUnlockEventFired()
    {
        if (!unlockAnimInProgress)
            return;

        pendingStageEvents--;

        if (pendingStageEvents > 0)
            return;

        // all relevant stages fired the event -> now unlock the wave
        unlockAnimInProgress = false;
        UnlockCurrentWaveViaAnimation();
    }


    private void UnlockCurrentWaveViaAnimation1()
    {
        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

            pm.isUnlocked = true;
            ApplyLock(pm, false);

            StartCoroutine(JumpThenSwitch(pm, null)); // dead sibling: no lane
        }

        waveLocked = false;
    }
    private void UnlockCurrentWaveViaAnimation()
    {
        if (!waveLocked) return;
        if (UsesDeploymentRules) MatchesReleased++;
        // One lane per match: 1st match -> lane 0, 2nd -> lane 1, ...
        // Resolved once for the whole wave so everyone lands on the same rank.
        float? laneY = GetLaneYForNextWave();
        waveUnlockIndex++;

        foreach (var pm in currentWave)
        {
            if (pm == null) continue;

            // 1) DETACH from stage
            pm.transform.SetParent(null, true);

            // 2) RESTORE CHILD LOCAL TRANSFORMS (THIS IS YOUR CODE)
            if (pm.childSnapshot != null)
            {
                foreach (var s in pm.childSnapshot)
                {
                    if (s.t == null) continue;
                    s.t.localPosition = s.localPos;
                    s.t.localRotation = s.localRot;
                    s.t.localScale = s.localScale;
                }
            }

            // 3) UNLOCK + JUMP
            pm.isUnlocked = true;
            if (UsesDeploymentRules) releasedHeroes.Add(pm);
            ApplyLock(pm, false);
            StartCoroutine(JumpThenSwitch(pm, laneY));
        }

        // Once the whole rank is down, repack the formation so this wave steps
        // into any holes the ranks ahead of it left behind.
        StartCoroutine(CompactWhenWaveHasLanded(new List<PlayerManager>(currentWave)));

        waveLocked = false;
    }

    /// <summary>
    /// Waits for every hero of a wave to finish its jump, then runs one
    /// formation compaction pass for the whole rank.
    /// </summary>
    private IEnumerator CompactWhenWaveHasLanded(List<PlayerManager> wave)
    {
        if (!gapFiller) yield break;

        float t0 = Time.time;

        // Everyone jumps together, so waiting on the slowest is enough.
        bool StillJumping()
        {
            foreach (var pm in wave)
            {
                if (pm == null) continue;
                var j = pm.GetComponent<FrogJumpTransformOnly>();
                if (j != null && j.IsJumping) return true;
            }
            return false;
        }

        while (StillJumping() && Time.time - t0 < jumpWaitTimeout + 1f)
            yield return null;

        gapFiller.Compact();
    }

    /// <summary>
    /// The world Y the next wave should land on, or null to keep the jumper's
    /// own fixed-distance behaviour.
    /// </summary>
    private float? GetLaneYForNextWave()
    {
        if (jumpLanes == null || jumpLanes.Length == 0) return null;

        int index = waveUnlockIndex;
        if (index >= jumpLanes.Length)
        {
            if (!reuseLastLane) return null;
            index = jumpLanes.Length - 1;
        }

        var lane = jumpLanes[index];
        return lane ? lane.position.y : (float?)null;
    }


    private System.Collections.IEnumerator JumpThenSwitch(PlayerManager pm, float? laneY)
    {
        // Find the jumper on this player (root or children)
        var jumper = pm.GetComponent<FrogJumpTransformOnly>();
        if (jumper == null)
        {
            // No jump component: still respect the battle gate before advancing.
            yield return WaitForCombatLive();
            pm.SwitchToNextState(pm.PlayerPursueTargetState);
            yield break;
        }

        // Start a jump if not already mid-jump. With a lane assigned the unit
        // LANDS on it, so successive waves form ranks instead of stacking.
        if (!jumper.IsJumping)
        {
            if (laneY.HasValue) jumper.TriggerJumpTo(laneY.Value);
            else jumper.TriggerJump();
        }

        // Wait until jump completes (with timeout failsafe)
        float t0 = Time.time;
        while (jumper.IsJumping && (Time.time - t0) < jumpWaitTimeout)
            yield return null;

        // Landed - but do NOT advance until the battle is actually running.
        // Heroes accumulate in the field during the puzzle phase and only march
        // once BATTLE has been pressed and the first enemy exists.
        yield return WaitForCombatLive();

        pm.SwitchToNextState(pm.PlayerPursueTargetState);
    }

    private System.Collections.IEnumerator WaitForCombatLive()
    {
        while (!CombatLive)
            yield return null;
    }



    // ==== helpers ====

    private List<int> GetFreeGateIndices()
    {
        var list = new List<int>(gatePoints.Length);
        int gateCount = UsesDeploymentRules ? Mathf.Min(4, gatePoints.Length) : gatePoints.Length;
        for (int i = 0; i < gateCount; i++)
        {
            if (!IsGateUsable(gatePoints[i])) continue;

            // If something sits there, skip it (prevents stacking)
            var c = Physics2D.OverlapCircle((Vector2)gatePoints[i].position, occupyCheckRadius, playerLayer);
            if (c == null) list.Add(i);
        }
        return list;
    }

    /// <summary>
    /// A stage is usable unless it is one of the two purchasable side slots and
    /// has not been bought yet.
    ///
    /// Checked LIVE on every wave rather than cached at Awake, so a slot bought
    /// mid-battle starts receiving heroes on the very next wave. A stage with no
    /// DeployStageSlot component is one of the four permanent ones and is always
    /// usable.
    /// </summary>
    private bool IsGateUsable(Transform gate)
    {
        if (!gate) return false;

        var slot = gate.GetComponent<DeployStageSlot>();
        return slot == null || slot.IsUnlocked;
    }

    /// <summary>
    /// The stages that can take a hero right now, in array order. Used where a
    /// COMPACT list is needed (reinforcements round-robin across it) rather than
    /// indices into gatePoints.
    /// </summary>
    private List<Transform> GetUsableGates()
    {
        var list = new List<Transform>(gatePoints.Length);
        foreach (var g in gatePoints)
            if (IsGateUsable(g)) list.Add(g);

        return list;
    }

    private static void Shuffle<T>(IList<T> arr)
    {
        for (int i = arr.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (arr[i], arr[j]) = (arr[j], arr[i]);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (gatePoints == null) return;
        Gizmos.color = new Color(0, 1, 0, 0.35f);
        foreach (var t in gatePoints)
        {
            if (!t) continue;
            Gizmos.DrawWireSphere(t.position, occupyCheckRadius);
        }
    }


    private bool PuzzleHasStacks()
    {
        return matchResolver != null && matchResolver.HasAnyStacksLeft();
    }

    // Adding  a helper to find Top child  on a stage
    private Transform GetStageTopAnchor(Transform stage)
    {
        if (stage == null) return null;

        var top = stage.Find("Top_0");
        if (top == null)
        {
            Debug.LogWarning($"Stage '{stage.name}' has no child named 'Top_0'.");
            return stage; // fallback: parent to stage root
        }

        return top;
    }


}
