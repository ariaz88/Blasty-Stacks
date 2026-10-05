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

    [Tooltip("SHORTEST time a deployed hero stands on its stage before it leaps, in " +
             "seconds. Since 2026-10-05 a hero normally waits on its stage for its " +
             "whole card load, so this only applies when the load filled while the " +
             "hero was still appearing. Separate from reinforcementGateHold, which " +
             "belongs to the gem buy-back.\n\n" +
             "HISTORY, so this is not 'restored' by mistake: it was 0.75 -> 1.20 " +
             "(+60%) after the first deployment play-test, because the leap read as " +
             "instant. Arash reversed that on 2026-09-23 - the pause read as the " +
             "hero being STUCK on the turret - and set 0.30 once he saw the real " +
             "value was 1.20 and not the ~0.5 he had assumed. The leap still reads " +
             "because FrogJumpTransformOnly.jumpDuration (0.50s) is untouched: what " +
             "was removed is dead standing time, not the jump.")]
    [SerializeField, Min(0f)] private float deployGateHold = 0.3f;

    [Tooltip("Glass-capsule prefab (with a GateArrivalCapsule) played the moment a hero " +
             "appears on a deploy stage. As wide as the stage's top cap, up to the hero's " +
             "mid-head. Empty = no effect.")]
    [SerializeField] private GameObject gateArrivalVfx;

    [Tooltip("Where a hero stands on a deploy stage, as a share of the stage cap's (Top_0) " +
             "height from its top: 33/82 puts the hero and its shadow in the MIDDLE of the cap's " +
             "top circle, not on its lower edge (29/82 - the geometric centre row - read a touch " +
             "high; Arash asked for it a little lower).")]
    [SerializeField, Range(0f, 1f)] private float standFromCapTop = 33f / 82f;

    private readonly List<UnitDefinitionSO> earnedHeroes = new();

    /// <summary>
    /// The heroes earned by EACH match, kept separate rather than flattened.
    ///
    /// Deployment no longer cares which match a hero came from - since 2026-09-27
    /// HeroDeploymentSequencer releases heroes PER TYPE from the flat
    /// <see cref="EarnedHeroes"/> totals. This list stays because
    /// EarlyCampaignVerification (editor balance tool) counts cleared matches by it.
    /// </summary>
    private readonly List<List<UnitDefinitionSO>> earnedBatches = new();

    /// <summary>One list per cleared match, in the order the matches were cleared.</summary>
    public IReadOnlyList<IReadOnlyList<UnitDefinitionSO>> EarnedBatches => earnedBatches;

    /// <summary>
    /// Every hero earned so far this stage, in the order the matches awarded them.
    /// HeroDeploymentSequencer counts this per type when BATTLE is pressed - that
    /// count is the "/total" on each Hero Stats card.
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
    /// One hero TYPE's line onto its deploy stage (2026-10-05). Its next hero
    /// WAITS on the stage while the card loads, leaps the moment the load fills,
    /// and the one after it appears as soon as that leap has landed.
    /// </summary>
    private class StageLane
    {
        public UnitDefinitionSO def;

        /// <summary>Heroes of this type not spawned yet.</summary>
        public int toSpawn;

        /// <summary>Filled loads whose hero has not leapt yet (it was still appearing).</summary>
        public int releasesOwed;

        /// <summary>The hero standing on the stage, waiting for its load. Null = none.</summary>
        public PlayerManager staged;
        public float stagedAt;

        /// <summary>
        /// This type's stage. Chosen ONCE, the first time a hero of the type
        /// appears, and never changed for the rest of the level (Arash: a card's
        /// stage must not move between heroes).
        /// </summary>
        public Transform home;

        /// <summary>The last hero that leapt off the stage, until it has landed.</summary>
        public PlayerManager leaving;

        /// <summary>Earliest Time.time the next hero may appear (restageGap after a landing).</summary>
        public float nextStageAt;
    }

    private readonly List<StageLane> stageLanes = new();

    /// <summary>Time.time of the first StageHeroes call; -1 = staging not started.</summary>
    private float stagingStartedAt = -1f;

    [Tooltip("How long the first heroes wait for the HUD cards to exist before giving up " +
             "on 'stage in front of the card' and using the stage nearest the base centre. " +
             "The cards only appear once the battle camera has finished its pan " +
             "(BattlePhaseTransition, 1.1s + fade), so this must comfortably exceed that.")]
    [SerializeField, Min(0f)] private float cardWaitTimeout = 4f;

    [Tooltip("Seconds the stage stays EMPTY after a hero has landed in the field, before " +
             "the next hero of that type appears on it.")]
    [SerializeField, Min(0f)] private float restageGap = 0.3f;

    /// <summary>
    /// Runs every lane: puts the next hero on its stage, and sends the staged hero
    /// leaping once its load has filled.
    ///
    /// Deliberately in Update and NOT in a coroutine: a coroutine here can be
    /// killed by any StopAllCoroutines on this component, and a hero killed
    /// mid-hold stays locked on the platform for the rest of the battle because
    /// PlayerLockState re-asserts a Static body every FixedUpdate. Measured
    /// 2026-09-14: 4 heroes released, only 2 reached the field. This loop has no
    /// such failure mode - the worst case is a late release, never a permanent one.
    /// </summary>
    private void Update()
    {
        if (stageLanes.Count > 0) ServiceStageLanes();
    }

    // ---------- Deployment: heroes wait on their stage (2026-10-05) ----------

    [Tooltip("The battle HUD's deployment cards. Each hero spawns on the deploy stage " +
             "straight in front of its own card. Left empty = found in the scene at " +
             "Awake; none in the scene = the free stage nearest the base centre.")]
    [SerializeField] private HeroStatsPanel deployCards;

    // Scratch lists for ResolveHomeStage - reused, it runs on every deploy attempt.
    private readonly List<KeyValuePair<int, float>> cardXs = new();
    private readonly List<KeyValuePair<Transform, float>> stageXs = new();

    /// <summary>
    /// Which hero is standing on which deploy stage. A stage is RESERVED from the
    /// frame its hero spawns until that hero has finished jumping into the field.
    ///
    /// !! WHY NOT ONLY THE PHYSICS CHECK. GetFreeGateIndices also tests the stage
    /// with Physics2D.OverlapCircle, but a collider instantiated THIS frame is not
    /// in the physics world until the next simulation step. With every card
    /// loading in parallel, two types can finish on the same frame (7s and 8s
    /// cards both fire at 56s) - the physics check alone would see both stages
    /// empty and could put both heroes on one of them.
    /// </summary>
    private readonly Dictionary<Transform, PlayerManager> gateOccupants = new();

    /// <summary>
    /// Heroes whose load has filled but who have not leapt yet (still appearing on
    /// their stage, or waiting for it). Part of the sequencer's HeroesStillComing.
    /// </summary>
    public int HeroesWaitingForGate
    {
        get
        {
            int n = 0;
            foreach (var lane in stageLanes) n += lane.releasesOwed;
            return n;
        }
    }

    /// <summary>
    /// Announces how many heroes of this type will deploy. Called by
    /// HeroDeploymentSequencer for every card the moment BATTLE starts: the FIRST
    /// hero of the type appears on its stage straight away (arrival capsule) and
    /// stands there until its card's load fills - see <see cref="DeployOne"/>.
    /// </summary>
    public void StageHeroes(UnitDefinitionSO def, int count)
    {
        if (!def || count <= 0) return;

        if (stagingStartedAt < 0f) stagingStartedAt = Time.time;
        GetLane(def).toSpawn += count;
    }

    /// <summary>
    /// One load of this type's card has filled: the hero standing on its stage
    /// leaps into the battle NOW. If it is not on the stage yet (the previous one
    /// is still mid-leap) it leaps the moment it has appeared - a hero the player
    /// earned is never dropped, and two heroes never share a stage.
    ///
    /// Called by HeroDeploymentSequencer each time a card finishes loading. A
    /// type that was never announced through StageHeroes still works: it gets
    /// a hero spawned for the release.
    /// </summary>
    public void DeployOne(UnitDefinitionSO def)
    {
        if (!def) return;

        if (GetUsableGates().Count == 0)
        {
            Debug.LogError($"[PlayerWaveManager] No usable deploy stage - '{def.displayName}' " +
                           "cannot be deployed.", this);
            return;
        }

        if (stagingStartedAt < 0f) stagingStartedAt = Time.time;

        var lane = GetLane(def);
        lane.releasesOwed++;

        // Every owed release needs a hero to send: one on the stage or one to come.
        int heroesLeft = lane.toSpawn + (lane.staged ? 1 : 0);
        if (lane.releasesOwed > heroesLeft) lane.toSpawn += lane.releasesOwed - heroesLeft;

        ServiceStageLanes();
    }

    /// <summary>
    /// Stops every lane: nothing further appears and nothing further leaps. Called
    /// when the level ends, so nothing arrives after a win or a loss. A hero
    /// already standing on its stage stays there, locked.
    /// </summary>
    public void ClearGateQueue()
    {
        foreach (var lane in stageLanes)
            lane.toSpawn = lane.releasesOwed = 0;
    }

    private StageLane GetLane(UnitDefinitionSO def)
    {
        foreach (var lane in stageLanes)
            if (lane.def == def) return lane;

        var created = new StageLane { def = def };
        stageLanes.Add(created);
        return created;
    }

    /// <summary>
    /// Load time of one hero type's card in the CURRENT level: the level's
    /// override in the deployment plan if it has one, otherwise the type's own
    /// UnitDefinitionSO.deployInterval.
    /// </summary>
    public float ResolveDeployInterval(UnitDefinitionSO def)
    {
        if (!def) return 6f;

        if (deploymentPlan && deploymentPlan.TryGetDeployInterval(RuleLevel, def.unitId, out float seconds))
            return seconds;

        return Mathf.Max(0.1f, def.deployInterval);
    }

    private void ServiceStageLanes()
    {
        // No hero appears until the cards exist: the card panel only switches on
        // once the battle camera has finished its pan, and every stage is chosen
        // from the card positions ONCE and then kept. A hero placed earlier would
        // go to a fallback stage and its type would then sit on the wrong one.
        bool cardsReady = !deployCards || deployCards.HasDeployCards
                          || Time.time >= stagingStartedAt + cardWaitTimeout;
        if (!cardsReady) return;

        foreach (var lane in stageLanes)
        {
            // 1) The next hero appears - only once the stage is free, i.e. the
            //    previous one of this type has LANDED in the field.
            if (!lane.staged && lane.toSpawn > 0) TryStageNext(lane);

            // 2) Its load has filled: leap now. deployGateHold is only the shortest
            //    stand, for a hero whose load filled while it was still appearing.
            if (lane.staged && lane.releasesOwed > 0 && Time.time >= lane.stagedAt + deployGateHold)
                ReleaseStaged(lane);
        }
    }

    private void TryStageNext(StageLane lane)
    {
        // The previous hero must have LANDED, and then the stage stays visibly
        // empty for restageGap before the next one appears. Polled rather than
        // hooked on the jumper's Landed event: a hero destroyed mid-leap never
        // lands, and an event wait would block this lane for good.
        if (!ReferenceEquals(lane.leaving, null))
        {
            if (lane.leaving && StillOnGate(lane.leaving)) return;

            lane.leaving = null;
            lane.nextStageAt = Time.time + restageGap;
        }
        if (Time.time < lane.nextStageAt) return;

        // Chosen once, here, and kept - before the free check, so every lane
        // resolves from the same card snapshot on the same frame.
        if (!lane.home) lane.home = ResolveHomeStage(lane.def);

        var free = GetFreeGateIndices();
        if (free.Count == 0) return;   // retried next Update

        if (!lane.home) lane.home = PickFallbackStage(free);

        int gi = Array.IndexOf(gatePoints, lane.home);
        if (gi < 0 || !free.Contains(gi)) return;   // its own stage is busy - retried next Update

        var gate = gatePoints[gi];

        // Counted down even if the spawn fails: SpawnUnitAt only fails on a
        // missing prefab, which retrying every frame would never fix.
        lane.toSpawn--;
        lane.releasesOwed = Mathf.Min(lane.releasesOwed, lane.toSpawn + 1);

        var pm = SpawnUnitAt(lane.def, GetStageStandPoint(gate), Quaternion.identity);
        if (!pm) { lane.releasesOwed = Mathf.Min(lane.releasesOwed, lane.toSpawn); return; }

        // Reserved on the SAME frame as the spawn - see gateOccupants.
        gateOccupants[gate] = pm;

        // NOT isUnlocked yet: on the stage the hero is scenery. HeroRoster,
        // FormationGapFiller and the targeting all treat an unlocked hero as one
        // that is in the field - FormationGapFiller would pull it off the stage.
        // The lock (Lock state + Static body) keeps it in place, and enemies
        // never hit a hero in the Lock state.
        ApplyLock(pm, true);
        SetHealthBarsHiddenOnGate(pm, true);
        PlayGateArrivalVfx(pm, gate);

        lane.staged = pm;
        lane.stagedAt = Time.time;
    }

    private void ReleaseStaged(StageLane lane)
    {
        var pm = lane.staged;
        lane.staged = null;
        lane.leaving = pm;
        lane.releasesOwed--;

        pm.isUnlocked = true;
        releasedHeroes.Add(pm);

        ApplyLock(pm, false);
        SetHealthBarsHiddenOnGate(pm, false);

        // The jump itself starts INSIDE this call (TriggerJumpTo runs before the
        // coroutine's first yield), so a later StopAllCoroutines cannot strand
        // the hero on the stage - the stage frees itself when the jump lands.
        StartCoroutine(JumpThenSwitch(pm, GetRearLaneY()));
        PlayStageThrow(lane.home);   // same frame as the leap: the stage throws it
    }

    /// <summary>
    /// True while a deployed hero still owns this stage: it is waiting on it, or
    /// it is mid-jump off it. A dead/destroyed or landed hero frees the stage.
    /// </summary>
    private bool IsGateReserved(Transform gate)
    {
        if (!gate || !gateOccupants.TryGetValue(gate, out var pm)) return false;

        if (pm && StillOnGate(pm)) return true;

        gateOccupants.Remove(gate);
        return false;
    }

    private bool StillOnGate(PlayerManager pm)
    {
        foreach (var lane in stageLanes)
            if (lane.staged == pm) return true;

        var jump = pm.GetComponent<FrogJumpTransformOnly>();
        return jump && jump.IsJumping;
    }

    /// <summary>
    /// A type's stage when there is no card to go by (no HeroStatsPanel, or this
    /// type has no card): the free stage nearest the base centre. NEVER RANDOM
    /// (Arash, 2026-10-05), and like a card stage it is then kept for the level.
    /// </summary>
    private Transform PickFallbackStage(List<int> free)
    {
        float centreX = ResolveCardAnchorWorld().x;
        int best = -1;
        foreach (int i in free)
            if (best < 0 || Mathf.Abs(gatePoints[i].position.x - centreX) < Mathf.Abs(gatePoints[best].position.x - centreX))
                best = i;

        return best >= 0 ? gatePoints[best] : null;
    }

    /// <summary>
    /// The deploy stage straight in front of this type's card, or null when there
    /// is no card to go by.
    ///
    /// Cards and stages are both put in left-to-right SCREEN order, and the cards
    /// are matched onto the stages in that same order, each as close to its own
    /// card as the order allows (fewest total pixels off). On the 4-stage base:
    ///   1 card  -> a middle stage
    ///   2 cards -> the two middle stages
    ///   3 cards -> left / one middle / right
    ///   4 cards -> one stage each
    /// Computed from the real positions rather than a hard-coded table, so a
    /// moved stage or a re-sized card still lines up. More card types than
    /// stages: each card takes its nearest stage, and types sharing one wait
    /// their turn.
    /// </summary>
    private Transform ResolveHomeStage(UnitDefinitionSO def)
    {
        if (!deployCards || !def) return null;

        var cam = Camera.main;
        if (!cam) return null;

        deployCards.GetDeployCardScreenXs(cardXs);
        int card = cardXs.FindIndex(c => c.Key == def.unitId);
        if (card < 0) return null;

        // The same stages GetFreeGateIndices deploys onto, left to right.
        stageXs.Clear();
        int gateCount = UsesDeploymentRules ? Mathf.Min(4, gatePoints.Length) : gatePoints.Length;
        for (int i = 0; i < gateCount; i++)
            if (IsGateUsable(gatePoints[i]))
                stageXs.Add(new KeyValuePair<Transform, float>(gatePoints[i], cam.WorldToScreenPoint(gatePoints[i].position).x));

        if (stageXs.Count == 0) return null;
        stageXs.Sort((a, b) => a.Value.CompareTo(b.Value));

        int n = cardXs.Count, g = stageXs.Count;

        if (n > g)
        {
            int nearest = 0;
            for (int j = 1; j < g; j++)
                if (Mathf.Abs(stageXs[j].Value - cardXs[card].Value) < Mathf.Abs(stageXs[nearest].Value - cardXs[card].Value))
                    nearest = j;
            return stageXs[nearest].Key;
        }

        // cost[i, j] = least total distance putting the first i cards on the first
        // j stages, in order, one card per stage.
        const float Inf = 1e30f;
        var cost = new float[n + 1, g + 1];
        for (int i = 1; i <= n; i++) cost[i, 0] = Inf;

        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= g; j++)
            {
                float skip = j > i ? cost[i, j - 1] : Inf;
                float take = cost[i - 1, j - 1] + Mathf.Abs(cardXs[i - 1].Value - stageXs[j - 1].Value);
                cost[i, j] = Mathf.Min(skip, take);
            }

        // Walk back from the full match to find which stage `card` landed on.
        for (int i = n, j = g; i > 0; )
        {
            if (j > i && cost[i, j - 1] <= cost[i, j]) { j--; continue; }
            if (i - 1 == card) return stageXs[j - 1].Key;
            i--; j--;
        }

        return null;
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
        if (!deployCards) deployCards = FindObjectOfType<HeroStatsPanel>(true);

        _gsm = GameStartManager.Instance ? GameStartManager.Instance : FindObjectOfType<GameStartManager>();
        if (_gsm == null)
        {
            Debug.LogError("PlayerWaveManager: GameStartManager not found.");
            return;
        }

        _unitsDb = _gsm.unitsDatabase;
    }

    // ---------- FINAL-STRETCH HERO BIAS (Arash, 2026-09-22) ----------

    /// <summary>How many matches at the END of a stage favour the strongest heroes.</summary>
    private const int FavourStrongestInLastMatches = 2;

    /// <summary>How many of the deck's best heroes get the raised odds.</summary>
    private const int StrongHeroCount = 2;

    /// <summary>Their draw weight against every other hero's 1.</summary>
    private const int StrongHeroWeight = 2;

    /// <summary>
    /// Picks one hero type for a deployment slot.
    ///
    /// Normally a flat random draw over the deployed roster, exactly as before.
    /// In the LAST TWO matches of a stage the two HIGHEST-CP heroes are drawn at
    /// 2:1 against the others, so the final push skews toward the deck's best -
    /// with four heroes that is 2+2+1+1 = 6 shares, i.e. 33% each for the top two
    /// and 17% each for the rest. It is a BIAS, not a guarantee: a weaker hero can
    /// still turn up, which keeps the last matches from being fully deterministic.
    ///
    /// Ranking uses each type's BASE stats. Every hero shares one progression
    /// curve, so the base ordering is the same as the upgraded ordering and this
    /// stays correct at any upgrade level.
    ///
    /// With three or fewer deployed types the top two still get the weight, which
    /// is what Arash asked for; at two or fewer it degenerates to a flat draw.
    /// </summary>
    private UnitDefinitionSO DrawHeroType(List<UnitDefinitionSO> pool, bool favourStrongest)
    {
        if (pool == null || pool.Count == 0) return null;
        if (!favourStrongest || pool.Count <= StrongHeroCount)
            return pool[UnityEngine.Random.Range(0, pool.Count)];

        // Indices of the StrongHeroCount highest-CP entries. A selection pass
        // rather than a sort, so the caller's pool order is left alone.
        var strong = new List<int>(StrongHeroCount);
        for (int pick = 0; pick < StrongHeroCount; pick++)
        {
            int best = -1;
            double bestCp = double.MinValue;
            for (int i = 0; i < pool.Count; i++)
            {
                if (strong.Contains(i)) continue;
                double cp = BaseCpOf(pool[i]);
                if (cp > bestCp) { bestCp = cp; best = i; }
            }
            if (best >= 0) strong.Add(best);
        }

        int totalWeight = 0;
        for (int i = 0; i < pool.Count; i++)
            totalWeight += strong.Contains(i) ? StrongHeroWeight : 1;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        for (int i = 0; i < pool.Count; i++)
        {
            roll -= strong.Contains(i) ? StrongHeroWeight : 1;
            if (roll < 0) return pool[i];
        }
        return pool[pool.Count - 1];
    }

    /// <summary>CP from a type's authored base stats, 0 when it has none.</summary>
    private static double BaseCpOf(UnitDefinitionSO def)
    {
        if (!def || def.baseStats == null) return 0;
        var s = new UnitStatsRuntime();
        s.FromSO(def.baseStats);
        return CPCalculator.UnitPower(s);
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
        stageLanes.Clear();
        stagingStartedAt = -1f;
        gateOccupants.Clear();

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
        // Rolled PER MATCH rather than as one flat list, because the LAST TWO
        // matches of a stage bias the draw toward the strongest heroes - see
        // DrawHeroType. plannedHeroes is still consumed in order by
        // plannedSpawnIndex, so the totals are unchanged.
        int totalMatches = LevelBattleRules.TotalPairs(RuleLevel);
        for (int match = 1; match <= totalMatches; match++)
        {
            bool finalStretch = match > totalMatches - FavourStrongestInLastMatches;

            int forThisMatch = LevelBattleRules.HeroesForMatch(RuleLevel, match);
            for (int i = 0; i < forThisMatch; i++)
            {
                var def = DrawHeroType(deployed, finalStretch);
                if (!def.runtimePrefab || !def.runtimePrefab.GetComponent<PlayerManager>() ||
                    !def.runtimePrefab.GetComponent<PlayerStatsApplier>())
                {
                    FailDeployment("A deployed type is missing its runtime prefab or stats components.");
                    yield break;
                }
                plannedHeroes.Add(def);
            }
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

            var pm = SpawnUnitAt(def, GetStageStandPoint(gate), gate.rotation);
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
            PlayGateArrivalVfx(pm, gate);

            // Per-hero coroutine rather than an inline wait: the hold has to run
            // ALONGSIDE the stagger, otherwise a squad of four takes
            // 4 * (hold + stagger) to walk out instead of overlapping.
            StartCoroutine(HoldOnGateThenJump(pm, laneY, reinforcementGateHold, gate));

            if (reinforcementStagger > 0f && i < count - 1)
                yield return new WaitForSeconds(reinforcementStagger);
        }
    }

    /// <summary>
    /// Plays the stage-arrival capsule (<see cref="GateArrivalCapsule"/>) around a hero
    /// that has just appeared on <paramref name="gate"/>. The capsule is sized from the
    /// STAGE, not the hero: as wide as the stage's top cap, standing on its front rim.
    /// Only its height follows the hero (up to mid-head). It stays on the stage
    /// (unparented) and fades by itself while the hero leaps off.
    /// </summary>
    private void PlayGateArrivalVfx(PlayerManager pm, Transform gate)
    {
        if (!gateArrivalVfx || !pm || !gate) return;

        // The cap is the stage sprite drawn on top (Top_0 at -1, over Mid -2 / Bot -3).
        SpriteRenderer cap = null;
        foreach (var r in gate.GetComponentsInChildren<SpriteRenderer>())
            if (r.enabled && r.sprite && (!cap || r.sortingOrder > cap.sortingOrder)) cap = r;
        if (!cap) return;

        // Hero body only - a health bar above the head would make the capsule too tall,
        // and the ground shadow is handed to the capsule (shrunk for the stand) instead.
        bool any = false;
        Bounds b = default;
        int topOrder = int.MinValue, topLayer = 0;
        SpriteRenderer shadow = null;
        foreach (var r in pm.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!r.enabled || !r.gameObject.activeInHierarchy || !r.sprite) continue;
            if (r.GetComponentInParent<HealthBar>(true)) continue;
            if (r.name == "Shadow") { shadow = r; continue; }
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            if (r.sortingOrder > topOrder) { topOrder = r.sortingOrder; topLayer = r.sortingLayerID; }
        }

        var fx = Instantiate(gateArrivalVfx, gate.position, Quaternion.identity);
        var capsule = fx.GetComponent<GateArrivalCapsule>();
        if (!capsule) { Destroy(fx); return; }

        // Glass over the hero: just above its top sprite, same sorting layer.
        capsule.Play(cap.bounds,
                     any ? b.max.y : 0f, any ? b.size.y : 0f,
                     any ? topLayer : cap.sortingLayerID, any ? topOrder : cap.sortingOrder,
                     shadow);
    }

    /// <summary>
    /// Keeps one bought hero standing on the gate for <see cref="reinforcementGateHold"/>
    /// seconds, then releases it into the normal jump-and-pursue flow.
    /// </summary>
    private IEnumerator HoldOnGateThenJump(PlayerManager pm, float? laneY, float hold, Transform gate = null)
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

        // Same frame as the leap: a yielded coroutine starts at once, and
        // JumpThenSwitch triggers the jump before its first yield.
        PlayStageThrow(gate);
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

            // A deployed hero still posing on / jumping off this stage owns it,
            // even on the frame it spawned - see gateOccupants.
            if (IsGateReserved(gatePoints[i])) continue;

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

    /// <summary>
    /// Where a hero stands on a deploy stage: the CENTRE of the stage's top circle
    /// (Arash, 2026-10-05: heroes stood on the circle's lower edge with the shadow
    /// below it). The circle is the top face of the cap sprite (Top_0, the stage's
    /// highest-order SpriteRenderer); its centre row is <see cref="standFromCapTop"/>
    /// of the cap's height down from its top. Falls back to the stage's own position.
    /// </summary>
    private Vector3 GetStageStandPoint(Transform stage)
    {
        if (!stage) return Vector3.zero;

        SpriteRenderer cap = null;
        foreach (var r in stage.GetComponentsInChildren<SpriteRenderer>())
            if (r.enabled && r.sprite && (!cap || r.sortingOrder > cap.sortingOrder)) cap = r;
        if (!cap) return stage.position;

        var b = cap.bounds;
        return new Vector3(stage.position.x, b.max.y - standFromCapTop * b.size.y, stage.position.z);
    }

    /// <summary>
    /// Plays the stage's own throw animation (Top_0's Animator, trigger "Throw" -
    /// the cap dips, kicks up and settles) on the SAME frame the hero leaps off it,
    /// so the stage throws the hero into the field. The clip's animation event
    /// (StageUnlockRelay -> OnOneStageUnlockEventFired) is a no-op outside the old
    /// wave flow: it returns early unless unlockAnimInProgress.
    /// </summary>
    private static void PlayStageThrow(Transform stage)
    {
        if (!stage) return;
        var anim = stage.GetComponentInChildren<Animator>();
        if (!anim || !anim.isActiveAndEnabled) return;
        anim.ResetTrigger("Throw");
        anim.SetTrigger("Throw");
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
