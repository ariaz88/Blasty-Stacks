using UnityEngine;
using System.Collections;

[DefaultExecutionOrder(-100)]


public class EnemyManager : MonoBehaviour
{
    #region CP/Stats/Progrression

    [Header("Base stats")]
    public UnitStatsSO statsBase;

    [Header("Live stats (mutable)")]
    public UnitStatsRuntime unitStats = new UnitStatsRuntime();

    /// <summary>
    /// Seconds this enemy must wait between swings, given an attack's authored
    /// recoveryTime. Faster attackSpeed = shorter gap = more hits per second.
    ///
    /// Before B2, cadence was the raw recoveryTime and attackSpeed only set
    /// animator playback speed, so the stat contributed nothing to damage output.
    /// Mirrors PlayerManager.AttackCadence exactly - both sides must use the same
    /// rule or the two factions are silently on different clocks.
    /// </summary>
    public float AttackCadence(float recoveryTime)
    {
        float atkSpd = (unitStats != null && unitStats.initialized) ? unitStats.attackSpeed : 1f;
        return recoveryTime / Mathf.Max(0.05f, atkSpd);
    }

    [Header("Progression (curves)")]
    [Min(1)] public int unitLevel = 1;
    public ProgressionConfigSO progression;

    [Header("CP weights (stage lens)")]
    [Min(1)] public int stageLevel = 1;      // set by spawner
    public CPWeightsConfigSO cpWeights;      // assign in spawner or inspector

    [Header("Debug")]
    public int cp;

    // runtime multipliers
    float _atkM = 1f, _defM = 1f, _hpM = 1f, _mvM = 1f, _asM = 1f, _rngM = 1f;

    [Header("Roguelite")]
    [Tooltip("XP this enemy grants the roguelite bar when it dies, in 'basic enemy' units. " +
             "1 = a basic enemy. Give elites 2-3 so they visibly fill more of the bar.")]
    [Min(0f)] public float xpValue = 1f;

    /// <summary>XP granted on death. Read by RogueliteManager.NotifyEnemyKilled.</summary>
    public float XpValue => xpValue;

    /// <summary>
    /// Raised ONCE per enemy death, from the same guarded block that awards XP.
    ///
    /// Deliberately fired here rather than piggy-backing on
    /// RogueliteManager.NotifyEnemyKilled: that call is skipped entirely when a
    /// scene has no RogueliteManager (the old test scenes do not), so a kill
    /// counter hung off it would silently read zero. This fires either way.
    ///
    /// STATIC because the listener - a HUD counter - outlives every individual
    /// enemy and must not have to find them as they spawn. Subscribers MUST
    /// unsubscribe in OnDisable: with domain reload disabled the delegate
    /// survives entering play mode a second time and would otherwise leak.
    /// </summary>
    public static event System.Action OnAnyEnemyKilled;

    private RogueliteManager roguelite;

    // Guards the once-per-death work: awarding XP and starting the despawn.
    private bool xpGiven = false;

    // REMOVED 2026-09-16: Initialize1 / RebuildFromBase1, dead numbered siblings of
    // the two methods below. Neither had a call site. Initialize1 differed only in
    // NOT setting unitLevel from the stage, so an enemy built through it never grew
    // with the campaign at all - reviving it would silently disable enemy scaling.

    public void Initialize(int stageLevelFromSpawner)
    {
        // Stage level (additive index) comes from EnemySpawner,
        // which now reads LevelManager.CurrentStage.
        stageLevel = stageLevelFromSpawner;

        // IMPORTANT: use the stage as this enemy's "unit level" for progression.
        // This is what makes stats grow as you go to higher stages.
        unitLevel = stageLevelFromSpawner;

        RebuildFromBase();
    }

    public void RebuildFromBase()
    {
        // 1) Start from base SO values
        unitStats.FromSO(statsBase);

        // 2) Apply growth using the enemy's unitLevel (we just set this from stage)
        if (progression)
        {
            // unitLevel is now equal to stageLevelFromSpawner,
            // so each stage uses a higher point on the growth curves.
            var g = ProgressionMath.GetGrowthMultipliers(unitLevel, progression);
            unitStats.attack *= g.gA;
            unitStats.defense *= g.gD;
            unitStats.maxHP *= g.gH;
            unitStats.attackSpeed *= g.gAS;
        }

        // 3) Apply runtime multipliers (buffs / wave scaling)
        unitStats.ApplyMultipliers(_atkM, _defM, _hpM, _mvM, _asM, _rngM);

        // 4) Push HP into EnemyStats so the health bar matches
        var hp = GetComponent<EnemyStats>();
        if (hp)
        {
            hp.maxHealth = unitStats.maxHP;
            hp.currentHP = unitStats.maxHP;
        }

        // 5) Compute CP using stageLevel as the CP "lens"
        cp = UnitCP_WithFallback(unitStats, stageLevel, cpWeights);

    }


    public void SetRuntimeMultipliers(float atk = 1f, float def = 1f, float hp = 1f,
                                      float mv = 1f, float atkSpd = 1f, float rng = 1f,
                                      bool rebuildNow = true)
    {
        _atkM = atk; _defM = def; _hpM = hp; _mvM = mv; _asM = atkSpd; _rngM = rng;
        if (rebuildNow) RebuildFromBase();
    }

    int UnitCP_WithFallback(UnitStatsRuntime s, int stage, CPWeightsConfigSO cfg)
    {
        // There used to be a second, hard-coded weighted-sum formula here for the
        // case where cfg was null. It is gone: CPCalculator.UnitCP no longer needs
        // a weights config at all, so there is nothing left to fall back to and
        // exactly one CP formula exists in the project. Keeping the old branch
        // would now be actively wrong - it produced numbers on a completely
        // different scale from the new formula.
        return CPCalculator.UnitCP(s, stage, cfg);
    }

    #endregion

    RigidbodyType2D initialBodyType;
    EnemyLocoMotion enemyLocoMotion;
    EnemyAnimatorManager enemyAnimationManager;
    PlayerStats playerStats;
    EnemyStats enemyStats;
    TopDownMover2D topDownMover;   // <- from your screenshot

    public PlayerGateStats currentGateTarget;
    public bool attackPlayerGate = false;

    public CapsuleCollider2D enemyDamageCollider;

    [Header("Combat")]
    public bool IsAttacking { get; private set; }
    public EnemyAttackAction defaultAttack;
    EnemyAttackAction currentAttack;
    public bool isPerformingAction;
    public bool isInteracting;
    public float currentRecoveryTimer;


    [Header("AI")]
    public float detectionRadius = 20f;
    public float minimumDetectionAngle = -60f;
    public float maximumDetectionAngle = 60f;
    public float stoppingDistance = 1.25f;
    public float rotationSpeed = 12f;

    private float damageAppling;

    [Header("Facing/Targeting")]
    public bool faceCenterOnStart = true;
    public bool playerIsInLeft;

    [Header("Gate stop")]
    public bool reachedGate = false;      // true after first contact with gate
    public Vector2 gateStopPosition;      // where THIS enemy stopped at the gate
    public float gateStopDistance = 0.6f; // tweak in Inspector

    [SerializeField] private float facingDeadZoneX = 0.05f; // prevents micro jitter
    private bool facingLockedToPlayer;

    [Header("Live speed readout (debug, read-only)")]
    [Tooltip("What this enemy is ACTUALLY travelling at right now, measured from how " +
             "far it really moved last physics step. Measured rather than read off " +
             "the Rigidbody2D because enemies move with MovePosition, where " +
             "linearVelocity stays near zero and would read as a false 0.")]
    public float actualMoveSpeed;

    [Tooltip("What it is TRYING to travel at - unitStats.moveSpeed.")]
    public float intendedMoveSpeed;

    Vector2 speedProbeLastPos;
    bool speedProbeStarted;

    /// <summary>Measures real movement. Runs before every early return in FixedUpdate.</summary>
    private void MeasureSpeed()
    {
        var body = enemyLocoMotion ? enemyLocoMotion.enemyRigidbody2D : null;
        Vector2 now = body ? body.position : (Vector2)transform.position;

        if (speedProbeStarted && Time.fixedDeltaTime > 0f)
            actualMoveSpeed = Vector2.Distance(now, speedProbeLastPos) / Time.fixedDeltaTime;

        intendedMoveSpeed = enemyLocoMotion ? enemyLocoMotion.CurrentMoveSpeed : 0f;
        speedProbeLastPos = now;
        speedProbeStarted = true;
    }


    private void OnEnable()
    {
        PlayerGateStats.OnGateDestroyed += HandleGateDestroyed;
    }

    private void OnDisable()
    {
        PlayerGateStats.OnGateDestroyed -= HandleGateDestroyed;
    }

    void Awake()
    {
        enemyLocoMotion = GetComponent<EnemyLocoMotion>();
        enemyAnimationManager = GetComponentInChildren<EnemyAnimatorManager>();
        playerStats = GameObject.FindObjectOfType<PlayerStats>();
        enemyStats = GetComponent<EnemyStats>();
        topDownMover = GetComponent<TopDownMover2D>();

        // IMPORTANT: ensure we always have an attack to use (for gate hits)
        currentAttack = defaultAttack;

        if (currentGateTarget == null)
        {
            var go = GameObject.Find("PlayerCastle");
            if (go != null)
                currentGateTarget = go.GetComponent<PlayerGateStats>();
            else
                Debug.LogError("PlayerCastle not found in scene!");
        }

        if (enemyLocoMotion != null && enemyLocoMotion.enemyRigidbody2D != null)
            initialBodyType = enemyLocoMotion.enemyRigidbody2D.bodyType;
        else
            initialBodyType = RigidbodyType2D.Dynamic;   // safe default

        EnsureDepthSorter();
    }

    /// <summary>
    /// Gives this enemy a Y-driven draw depth, so the unit standing in FRONT is the
    /// one actually painted in front. Every character part in the game sits on
    /// sorting layer Default at order 1, so without this the tie between two
    /// overlapping units resolves arbitrarily - which is why an enemy's head could
    /// be drawn over the hero it was fighting. See UnitDepthSorter.
    ///
    /// Added in code rather than on the six enemy prefabs so that nothing can ship
    /// without it, including anything spawned by the debug stage tools.
    /// </summary>
    private void EnsureDepthSorter()
    {
        if (GetComponent<UnitDepthSorter>() == null)
            gameObject.AddComponent<UnitDepthSorter>();
    }

    private void Start()
    {
        if (enemyDamageCollider != null)
            enemyDamageCollider.enabled = false;

        if (faceCenterOnStart)
            SetInitialFacingByScreenHalf();

        // Cached once: the death path needs it too, and a scene may legitimately
        // have no roguelite manager (the old test scenes do not).
        roguelite = FindObjectOfType<RogueliteManager>();
        if (roguelite != null) roguelite.RegisterEnemy(this);
    }

    #region Facing

    public void UpdateFacing1()
    {
        var target = enemyLocoMotion.currentTarget;
        if (target == null) return;

        Vector2 enemyPos = (enemyLocoMotion.enemyRigidbody2D != null)
            ? enemyLocoMotion.enemyRigidbody2D.position
            : (Vector2)transform.position;

        Vector2 playerPos = (Vector2)target.transform.position;

        playerIsInLeft = playerPos.x < enemyPos.x;

        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = playerIsInLeft ? -absX : absX;
        transform.localScale = ls;
    }
    public void UpdateFacing()
    {
        // Priority 1: if we have a player target → face player
        if (enemyLocoMotion.currentTarget != null)
        {
            FacePlayerStable();
            return;
        }

        // Priority 2: no player target → screen-half logic
        SetInitialFacingByScreenHalf();
    }

    private void FacePlayerStable()
    {
        var target = enemyLocoMotion.currentTarget;
        if (target == null) return;

        Vector2 enemyPos = enemyLocoMotion.enemyRigidbody2D != null
            ? enemyLocoMotion.enemyRigidbody2D.position
            : (Vector2)transform.position;

        Vector2 playerPos = target.transform.position;

        float dx = playerPos.x - enemyPos.x;

        // Dead-zone to prevent micro jitter
        if (Mathf.Abs(dx) < facingDeadZoneX)
            return;

        bool faceLeft = dx < 0f;
        ApplyFacing(faceLeft);
    }
    private void ApplyFacing(bool faceLeft)
    {
        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = faceLeft ? -absX : absX;
        transform.localScale = ls;
    }



    public void SetInitialFacingByScreenHalf()
    {
        var cam = Camera.main;
        if (!cam) return;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        bool isOnLeftHalf = vp.x < 0.5f;
        FaceLeft(!isOnLeftHalf);
    }

    public void FaceLeft(bool faceLeft)
    {
        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = faceLeft ? -absX : absX;
        transform.localScale = ls;
    }

    #endregion

    void StopMovingEnemy()
    {
        enemyAnimationManager.anim.SetFloat("Vertical", 0, 0, 0);
        enemyAnimationManager.anim.SetFloat("Horizontal", 0, 0, 0);

        // HARD STOP on movement
        if (enemyLocoMotion != null && enemyLocoMotion.enemyRigidbody2D != null)
        {
            var rb = enemyLocoMotion.enemyRigidbody2D;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // also disable locomotion + mover scripts so they never write velocity again
        if (enemyLocoMotion != null) enemyLocoMotion.enabled = false;
        if (topDownMover != null) topDownMover.enabled = false;

        if (enemyStats.enemyIsdead)
        {
            enemyAnimationManager.anim.Play("Dying");
        }

    }

    private void Update()
    {
        if (GameplayPause.IsPaused)
            return;

        if (enemyAnimationManager == null || enemyLocoMotion == null)
            return;

        if (enemyLocoMotion.currentTarget != null)
        {
            if (enemyLocoMotion.distanceFromTarget < enemyLocoMotion.fairDistanceToPlayer)
            {
            UpdateFacing();
            }

        }

        if (enemyStats.enemyIsdead)
        {
            StopMovingEnemy();

            if (!xpGiven)
            {
                xpGiven = true;

                // This is what fills the roguelite bar. Without it the whole
                // level-up / buff-card loop never runs.
                if (roguelite != null) roguelite.NotifyEnemyKilled(this);

                // Inside the xpGiven guard, so it is exactly once per enemy, and
                // AFTER the roguelite call so the XP bar and the kill counter
                // never disagree about the same death.
                OnAnyEnemyKilled?.Invoke();

                StartCoroutine(DestroyAfterDelayRealtime(0.5f));
            }

            return;
        }

        // ADD THIS BLOCK HERE (right after enemy dead handling) SO IF PLAYER IS DEAD  WE HAVE NO MORE ATTACK!!!!!
        if (enemyLocoMotion.currentTarget != null && enemyLocoMotion.currentTarget.playerIsdead)
        {
            IsAttacking = false;
            isPerformingAction = false;
            currentRecoveryTimer = 0f; // recommended to avoid re-locking
                                       // optionally: currentAttack = null; (only if you want to force re-select next time)
        }



        // Detection is now here, not in EnemyLocoMotion
        DetectPlayerTargets();

        HandleRecoveryTimer();

        isInteracting = enemyAnimationManager.anim.GetBool("isInteracting");
    }


    /// <summary>
    /// Realtime so the corpse still clears while the game runs at any timeScale - but
    /// it now HOLDS while gameplay is paused.
    ///
    /// WaitForSecondsRealtime ignores pausing by definition, so an enemy that died as
    /// the roguelite card screen opened went on to despawn underneath it. Nothing on
    /// the battlefield may move once that screen is up.
    /// </summary>
    IEnumerator DestroyAfterDelayRealtime(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (!GameplayPause.IsPaused) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }

    void FixedUpdate1()
    {
        HandleCurrentAction();
    }
    void FixedUpdate()
    {
        MeasureSpeed();   // before every early return, so the readout never stalls
        if (GetComponent<MeleeContactRecovery>() is { IsRepositioning: true }) return;
        if (GameplayPause.IsPaused)
            return;

        HandleCurrentAction();
    }


    /// <summary>
    /// Picks the hero this enemy fights - and then KEEPS IT.
    ///
    /// THE BUG THIS FIXES (reported 2026-09-12, 4v4 and 5v5). This runs in Update, so
    /// it ran EVERY FRAME and every frame it overwrote currentTarget with whatever was
    /// nearest. An enemy in the middle of a crowded line has heroes on both sides at
    /// almost exactly equal range, so the winner of that comparison changed frame to
    /// frame. Each swap flips which side of the target the enemy walks to
    /// (MeleeEngagement.ChooseSide) and flips its facing with it, and the result on
    /// screen is a unit spinning in place instead of fighting.
    ///
    /// THE FIX IS A HARD LOCK, NOT A MARGIN. The hero side solved the same problem with
    /// hysteresis - PlayerManager only retargets when a new enemy is CLEARLY closer -
    /// but Arash asked for the stronger rule here: once an enemy has locked onto a
    /// hero it does not retarget at all while that hero can still be fought.
    ///
    /// CONSEQUENCE, ACCEPTED: an enemy will walk past a hero standing right next to it
    /// to reach the one it locked onto. In this game's lane layout the two armies meet
    /// as a group, so that is rare; the alternative is the spinning.
    ///
    /// The lock never blocks a re-acquire that matters: ResetAfterRevive clears
    /// currentTarget before calling this, and a dead, despawned or undeployed hero
    /// releases it below.
    /// </summary>
    void DetectPlayerTargets()
    {
        if (enemyLocoMotion == null) return;

        // TWO DIFFERENT DISTANCES, and confusing them is what caused the
        // 2026-09-14 "the enemy ignores the hero beside it" report:
        //
        //   detectionRadius (20)        how far this enemy can SEE. Sight only.
        //   fairDistanceToPlayer (4)    how close a hero must be before the enemy
        //                               COMMITS to it - the engage distance.
        //
        // Targeting used detectionRadius for BOTH, so an enemy locked onto a hero
        // most of a map away and then walked past the one standing next to it.
        // An enemy with no hero inside the engage distance has NO hero target at
        // all, and marches on the base - which is the intended behaviour.
        float engage = enemyLocoMotion.fairDistanceToPlayer;
        if (engage <= 0f)
        {
            enemyLocoMotion.currentTarget = null;
            return;
        }

        // THE LOCK holds only while the locked hero is still inside the engage
        // distance. Outside it the lock drops and the search below re-picks, so a
        // hero that walks right up to this enemy takes the target.
        if (StillFightable(enemyLocoMotion.currentTarget, engage))
        {
            float lockedDist = Vector2.Distance(
                enemyLocoMotion.currentTarget.transform.position, transform.position);

            if (lockedDist <= engage) return;
        }

        LayerMask mask = enemyLocoMotion.playerDetectionLayer;

        // Search the ENGAGE distance, not detectionRadius. A hero further away than
        // this is seen but not committed to, so the enemy keeps walking on the base.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, engage, mask);

        float bestDist = float.MaxValue;
        PlayerStats best = null;

        for (int i = 0; i < hits.Length; i++)
        {
            var ps = hits[i].GetComponent<PlayerStats>() ?? hits[i].GetComponentInParent<PlayerStats>();
            if (ps == null) continue;
            if (ps.playerIsdead) continue;

            float d = Vector2.Distance(ps.transform.position, transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                best = ps;
            }
        }

        enemyLocoMotion.currentTarget = best;
    }

    /// <summary>
    /// Whether a locked target is still a hero this enemy can actually fight. These are
    /// the ONLY three things that release the lock.
    /// </summary>
    bool StillFightable(PlayerStats hero, float radius)
    {
        // Dead, or the object is gone - Unity's null check covers a destroyed hero.
        if (!hero || hero.playerIsdead) return false;

        // An undeployed hero in the lock state is not a valid target: EnemyDamageCollider
        // refuses to hit one, so an enemy that stayed locked onto it would stand there
        // swinging at something it can never damage.
        var pm = hero.PlayerManager;
        if (pm && pm.currentState == pm.PlayerLockState) return false;

        // Out of detection entirely. Falling back to the ordinary search here is also
        // what lets an enemy give up on a hero and walk on the base instead.
        return ((Vector2)hero.transform.position - (Vector2)transform.position).sqrMagnitude
               <= radius * radius;
    }



    void HandleCurrentAction1()
    {
        if (enemyLocoMotion == null) return;

        // 1) Gate attack
        if (attackPlayerGate && currentGateTarget != null)
        {
            if (currentRecoveryTimer <= 0 && !isPerformingAction)
            {
                if (currentAttack == null)
                {
                    Debug.LogWarning($"{name}: Gate attack had no currentAttack, using defaultAttack.");
                    currentAttack = defaultAttack;
                }

                isPerformingAction = true;
                currentRecoveryTimer = AttackCadence(currentAttack.recoveryTime);

                StopMovingEnemy();

                float speedMultiplier = unitStats.attackSpeed;
                enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);
            }

            // when attacking gate we don't want any move logic at all
            return;
        }

        // 2) Normal attack behaviour against current target (player)
        if (enemyLocoMotion.distanceFromTarget <= enemyLocoMotion.stoppingDistance)
        {
            if (playerStats != null && playerStats.playerIsdead)
                return;

            if (!enemyStats.enemyIsdead)
            {
                AttackTarget();
                UpdateFacing();
            }
        }
        else
        {
            SetInitialFacingByScreenHalf();
        }
    }
    // ------------------------------------------------------------------
    //  FIRST-STRIKE RULE - the hero always opens an engagement
    // ------------------------------------------------------------------
    [Header("First strike")]
    [Tooltip("Safety valve for the first-strike rule. An enemy that has never been " +
             "struck holds its opening swing, but only for this long - after that it " +
             "attacks anyway so a hero that never arrives cannot freeze the battle. " +
             "Keep it well ABOVE the time a hero needs to close and swing.")]
    [SerializeField, Min(0f)] private float firstStrikeGrace = 3f;

    private CharacterStats firstStrikeTarget;
    private float firstStrikeEngagedAt;

    /// <summary>
    /// FALSE while this enemy is still owing the hero the opening blow.
    ///
    /// WHY THIS EXISTS. Arash reported that the enemy always swung first, and it did -
    /// not through any stat advantage. Both sides start with currentRecoveryTimer 0 on
    /// every prefab and their reach is all but identical (hero maxAttackRange 0.85,
    /// enemy stoppingDistance 0.83). The cause was pure SEQUENCE: the enemy spawns the
    /// moment BATTLE is pressed, walks down and is standing still, facing, cooldown
    /// ready, by the time the hero finishes its 6s deployment load and closes. The
    /// hero is still moving into its attack slot, so the enemy gets a free opening hit.
    ///
    /// That free hit decided whole duels, because blow counts are integers: a hero
    /// killing in 4.05 swings and dying in 4.52 both round to 5, so whoever lands
    /// first wins outright and a real 12% advantage counted for nothing.
    ///
    /// The rule: an enemy holds its opening swing until it has taken at least one hit.
    /// After that it fights completely normally - this is an OPENING rule, not a
    /// damage handicap, and it never changes how hard anyone hits.
    ///
    /// THE GRACE IS A DEADLOCK GUARD, not balance. If the hero never arrives - it died
    /// on the way, it is fighting someone else, it is stuck - the enemy must not stand
    /// there forever, so after firstStrikeGrace it swings regardless.
    /// </summary>
    private bool MayStrike(CharacterStats hero)
    {
        if (firstStrikeTarget != hero)
        {
            firstStrikeTarget = hero;
            firstStrikeEngagedAt = Time.time;
        }

        // Already struck: the hero has had its opening, fight normally from here.
        if (enemyStats && enemyStats.HitsTaken > 0) return true;

        return Time.time - firstStrikeEngagedAt >= firstStrikeGrace;
    }

    void HandleCurrentAction()
    {
        if (enemyLocoMotion == null) return;

        // 1) If we are standing where our weapon can reach the hero -> attack it.
        //
        // The test used to be the radial `distanceFromTarget <= stoppingDistance`,
        // which is TRUE for an enemy parked directly above its target - so the
        // enemy froze its locomotion and played swing after swing through a hero it
        // could not touch, because the weapon box only reaches sideways. Gating on
        // the position instead lets it finish walking round to the flank first.
        if (enemyLocoMotion.currentTarget != null &&
            enemyLocoMotion.IsInAttackPosition())
        {
            var ps = enemyLocoMotion.currentTarget;
            if (!ps.playerIsdead && !enemyStats.enemyIsdead)
            {
                // THE FIRST BLOW OF AN ENGAGEMENT BELONGS TO THE HERO (Arash, 2026-09-16).
                // Hold the swing until this enemy has actually been struck - see MayStrike.
                if (!MayStrike(ps)) { UpdateFacing(); return; }

                AttackTarget();
                UpdateFacing();
            }
            return;
        }

        // 2) Otherwise, if we are at the gate and no player in range -> attack gate
        //
        // !! A HERO NEARBY CANCELS THE GATE ATTACK. Without this an enemy that had
        // already started swinging at the castle kept swinging forever: the branch
        // below sets isPerformingAction every cycle, which re-pins the body, so
        // EnemyLocoMotion could never walk it over to the hero - and the hero was
        // left hitting an enemy that never hit back. Verified live 2026-09-14: a
        // gate-locked enemy sat at (2.96,4.02) with a hero targeted 1.36 away and
        // isPerformingAction permanently true.
        //
        // Same distance the locomotion uses to decide to break off, so "close
        // enough to walk to" and "close enough to stop hitting the gate" can never
        // disagree and leave the enemy oscillating.
        bool heroNearby =
            enemyLocoMotion != null &&
            enemyLocoMotion.currentTarget != null &&
            !enemyLocoMotion.currentTarget.playerIsdead &&
            Vector2.Distance(enemyLocoMotion.currentTarget.transform.position, transform.position)
                <= enemyLocoMotion.fairDistanceToPlayer;

        if (!heroNearby && attackPlayerGate && currentGateTarget != null && !currentGateTarget.isPlayerGateDestroyed)
        {
            if (currentRecoveryTimer <= 0 && !isPerformingAction)
            {
                if (currentAttack == null)
                {
                    Debug.LogWarning($"{name}: Gate attack had no currentAttack, using defaultAttack.");
                    currentAttack = defaultAttack;
                }

                // REQUIRED, not decoration: EnemyDamageCollider gates every hit on
                // IsAttacking, so without this the gate swing plays in full and the
                // castle takes ZERO damage. This branch cannot just call
                // AttackTarget() (which is where IsAttacking is normally raised)
                // because that method dereferences enemyLocoMotion.currentTarget,
                // and there is no hero target while attacking the gate.
                IsAttacking = true;

                isPerformingAction = true;
                currentRecoveryTimer = AttackCadence(currentAttack.recoveryTime);

                enemyLocoMotion.SetAnimMoving(false);

                float speedMultiplier = unitStats.attackSpeed;
                enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);
            }

            return;
        }

    }


    void HandleGateDestroyed1()
    {
        attackPlayerGate = false;
        currentGateTarget = null;

        if (enemyLocoMotion != null && enemyLocoMotion.enemyRigidbody2D != null)
        {
            var rb = enemyLocoMotion.enemyRigidbody2D;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.bodyType = RigidbodyType2D.Static;
        }

        if (enemyLocoMotion != null)
            enemyLocoMotion.SetAnimMoving(false);
    }
    void HandleGateDestroyed()
    {
        attackPlayerGate = false;
        currentGateTarget = null;
        reachedGate = false;

        if (enemyLocoMotion != null)
            enemyLocoMotion.SetAnimMoving(false);
    }


    public float DamageApplying(PlayerManager target)
    {
        damageAppling = CombatMath.DamagePerHit(unitStats, target.unitStats);
        return damageAppling;
    }

    void OnTriggerEnter2D1(Collider2D collision)
    {
        if (collision.CompareTag("PlayerGate"))
        {
            attackPlayerGate = true;
            currentGateTarget = collision.GetComponent<PlayerGateStats>();

            // First time we touch the gate: remember this position as stop threshold
            if (!reachedGate)
            {
                reachedGate = true;
                gateStopPosition = transform.position;
            }
        }
    }
    void HandleGateTrigger(PlayerGateStats gate)
    {
        if (gate == null) return;

        attackPlayerGate = true;
        currentGateTarget = gate;

        // First time we touch the gate: remember this position as stop threshold
        if (!reachedGate && enemyLocoMotion != null && enemyLocoMotion.enemyRigidbody2D != null)
        {
            reachedGate = true;
            gateStopPosition = enemyLocoMotion.enemyRigidbody2D.position;
        }
    }
    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerGate"))
        {
            var gate = collision.GetComponent<PlayerGateStats>();
            HandleGateTrigger(gate);
        }
    }
    void OnTriggerStay2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerGate"))
        {
            var gate = collision.GetComponent<PlayerGateStats>();
            HandleGateTrigger(gate);
        }
    }



    #region Attack

    void HandleRecoveryTimer()
    {
        if (currentRecoveryTimer > 0)
            currentRecoveryTimer -= Time.deltaTime;

        if (isPerformingAction && currentRecoveryTimer <= 0)
        {
            isPerformingAction = false;
            IsAttacking = false; // <-- CORRECT PLACE


        }
    }

    public void AttackTarget()
    {
        if (isPerformingAction)
        {
            enemyAnimationManager.anim.SetFloat("Vertical", 0f, 0f, 0f);
            return;
        }

        if (currentAttack == null)
        {
            GetNewAttack();
            return;
        }
        if (enemyLocoMotion.currentTarget.playerIsdead) return;
        IsAttacking = true;


        isPerformingAction = true;
        currentRecoveryTimer = AttackCadence(currentAttack.recoveryTime);

        enemyAnimationManager.anim.SetFloat("Vertical", 0f, 0f, 0f);

        float speedMultiplier = unitStats.attackSpeed;
        enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);
    }

    public void GetNewAttack()
    {
        if (enemyLocoMotion.distanceFromTarget < defaultAttack.maxDistanceNeededToAttack &&
            enemyLocoMotion.distanceFromTarget >= defaultAttack.minDistanceNeededToAttack)
        {
            currentAttack = defaultAttack;
        }

    }

    #endregion


    public void GetNewTargets()
    {
        if (this.enemyLocoMotion.currentTarget == null && this.currentGateTarget == null)
        {
            HandleCurrentAction();
        }
    }
    public void ResetAfterRevive1()
    {
        // Do not revive dead bodies
        if (enemyStats != null && enemyStats.enemyIsdead)
            return;

        // Not attacking gate anymore at the moment of revive
        attackPlayerGate = false;

        // Restore gate reference so they can attack it again in future
        if (currentGateTarget == null)
        {
            var go = GameObject.Find("PlayerCastle");
            if (go != null)
                currentGateTarget = go.GetComponent<PlayerGateStats>();
        }

        // Re-enable locomotion scripts
        if (enemyLocoMotion != null)
        {
            enemyLocoMotion.enabled = true;

            if (enemyLocoMotion.enemyRigidbody2D != null)
            {
                var rb = enemyLocoMotion.enemyRigidbody2D;
                rb.bodyType = initialBodyType;   // from when the enemy was spawned
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // force them to re-detect target in next Update
            enemyLocoMotion.currentTarget = null;
        }

     

        // Reset combat state so they're free to choose new actions
        isPerformingAction = false;
        isInteracting = false;
        currentRecoveryTimer = 0f;
    }
    public void ResetAfterRevive()
    {
        if (enemyStats != null && enemyStats.enemyIsdead)
            return;

        attackPlayerGate = false;
        reachedGate = false;

        // 1) Reconnect to the PlayerGate on revive
        //    (this is your "search for PlayerGate" on revive)
        PlayerGateStats gate = GameObject.FindObjectOfType<PlayerGateStats>();
        if (gate != null)
        {
            currentGateTarget = gate;
        }
        else
        {
            currentGateTarget = null;
            Debug.LogWarning($"{name}: PlayerGateStats not found on revive.");
        }

        if (currentGateTarget == null)
        {
            var go = GameObject.Find("PlayerCastle");
            if (go != null)
                currentGateTarget = go.GetComponent<PlayerGateStats>();
        }

        if (enemyLocoMotion != null && enemyLocoMotion.enemyRigidbody2D != null)
        {
            var rb = enemyLocoMotion.enemyRigidbody2D;
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (enemyLocoMotion != null)
        {
            enemyLocoMotion.enabled = true;
            enemyLocoMotion.currentTarget = null;
        }

        isPerformingAction = false;
        isInteracting = false;
        currentRecoveryTimer = 0f;

        // After resetting, immediately detect if there is any player around
        DetectPlayerTargets();
    }




}
