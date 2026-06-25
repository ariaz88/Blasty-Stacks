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

    private bool xpGiven = false;

    public void Initialize1(int stageLevelFromSpawner)
    {
        stageLevel = stageLevelFromSpawner;
        RebuildFromBase();
    }

    public void RebuildFromBase1()
    {
        unitStats.FromSO(statsBase);

        if (progression)
        {
            var g = ProgressionMath.GetGrowthMultipliers(unitLevel, progression);
            unitStats.attack *= g.gA;
            unitStats.maxHP *= g.gH;
            unitStats.moveSpeed *= g.gMv;
            unitStats.attackSpeed *= g.gAS;
        }

        unitStats.ApplyMultipliers(_atkM, _defM, _hpM, _mvM, _asM, _rngM);

        var hp = GetComponent<EnemyStats>();
        if (hp)
        {
            hp.maxHealth = unitStats.maxHP;
            hp.currentHP = unitStats.maxHP;
        }

        cp = UnitCP_WithFallback(unitStats, stageLevel, cpWeights);
    }

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
            unitStats.maxHP *= g.gH;
            unitStats.moveSpeed *= g.gMv;
            unitStats.attackSpeed *= g.gAS;
            // (Add defense/range growth here if you want later)
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

        // Optional debug to verify things really change per stage:
        // Debug.Log($"{name} | Stage={stageLevel} UnitLevel={unitLevel} | ATK={unitStats.attack} HP={unitStats.maxHP} CP={cp}");
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
        if (cfg != null)
            return CPCalculator.UnitCP(s, stage, cfg);

        float wA = 1.00f, wH = 0.15f, wMv = 0.25f, wAS = 0.40f, wD = 0.00f;
        float typeMult = (s.type == FighterType.Archer) ? 1.05f : 1.00f;

        float baseScore = wA * s.attack +
                          wH * s.maxHP +
                          wMv * s.moveSpeed +
                          wAS * s.attackSpeed +
                          wD * s.defense;

        return Mathf.RoundToInt(baseScore * typeMult);
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

    }

    private void Start()
    {
        if (enemyDamageCollider != null)
            enemyDamageCollider.enabled = false;

        if (faceCenterOnStart)
            SetInitialFacingByScreenHalf();

        GameObject.FindObjectOfType<RogueliteManager>().RegisterEnemy(this);
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

        // Debug to verify this runs for ALL enemies
        //Debug.Log($"{name}: StopMovingEnemy()");
    }

    private void Update1()
    {

        if (GameplayPause.IsPaused)
            return;


        if (enemyAnimationManager == null || enemyLocoMotion == null)
            return;

        HandleRecoveryTimer();

        isInteracting = enemyAnimationManager.anim.GetBool("isInteracting");

        if (enemyStats.enemyIsdead)
        {
            StopMovingEnemy();

            if (!xpGiven)
            {
                xpGiven = true;
                StartCoroutine(DestroyAfterDelayRealtime(3f));
            }
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


    IEnumerator DestroyAfterDelayRealtime(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Destroy(gameObject);
    }

    void FixedUpdate1()
    {
        HandleCurrentAction();
    }
    void FixedUpdate()
    {
        if (GameplayPause.IsPaused)
            return;

        HandleCurrentAction();
    }


    void DetectPlayerTargets()
    {
        if (enemyLocoMotion == null) return;

        float radius = enemyLocoMotion.detectionRadius;
        if (radius <= 0f)
        {
            enemyLocoMotion.currentTarget = null;
            return;
        }

        LayerMask mask = enemyLocoMotion.playerDetectionLayer;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius, mask);

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
                currentRecoveryTimer = currentAttack.recoveryTime;

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
    void HandleCurrentAction()
    {
        if (enemyLocoMotion == null) return;

        // 1) If we have a player target in melee range -> attack player
        if (enemyLocoMotion.currentTarget != null &&
            enemyLocoMotion.distanceFromTarget <= enemyLocoMotion.stoppingDistance)
        {
            var ps = enemyLocoMotion.currentTarget;
            if (!ps.playerIsdead && !enemyStats.enemyIsdead)
            {
                AttackTarget();
                UpdateFacing();
            }
            return;
        }

        // 2) Otherwise, if we are at the gate and no player in range -> attack gate
        if (attackPlayerGate && currentGateTarget != null && !currentGateTarget.isPlayerGateDestroyed)
        {
            if (currentRecoveryTimer <= 0 && !isPerformingAction)
            {
                if (currentAttack == null)
                {
                    Debug.LogWarning($"{name}: Gate attack had no currentAttack, using defaultAttack.");
                    currentAttack = defaultAttack;
                }

                isPerformingAction = true;
                currentRecoveryTimer = currentAttack.recoveryTime;

                enemyLocoMotion.SetAnimMoving(false);

                float speedMultiplier = unitStats.attackSpeed;
                enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);
            }

            return;
        }

        // 3) Otherwise let locomotion move, but keep general facing
        //SetInitialFacingByScreenHalf();
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
            //Debug.Log($"{name}: Collision With Enemy Gate");
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
        currentRecoveryTimer = currentAttack.recoveryTime;

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


public class EnemyManager2 : MonoBehaviour
{
    #region CP/Stats/Progrression


    [Header("Base stats")]
    public UnitStatsSO statsBase;

    [Header("Live stats (mutable)")]
    public UnitStatsRuntime unitStats = new UnitStatsRuntime();

    [Header("Progression (curves)")]
    [Min(1)] public int unitLevel = 1;
    public ProgressionConfigSO progression;

    [Header("CP weights (stage lens)")]
    [Min(1)] public int stageLevel = 1;      // set by spawner
    public CPWeightsConfigSO cpWeights;      // assign in spawner or inspector

    [Header("Debug")]
    public int cp;

    // Optional: a place to store temporary multipliers (buffs/wave scaling)
    float _atkM = 1f, _defM = 1f, _hpM = 1f, _mvM = 1f, _asM = 1f, _rngM = 1f;

    // --- call this from the spawner right after Instantiate ---

    private bool xpGiven = false;

    public void Initialize(int stageLevelFromSpawner)
    {
        stageLevel = stageLevelFromSpawner;
        RebuildFromBase();      // idempotent; safe to call anytime
    }

    // If you use pooling, you can also call RebuildFromBase() in OnEnable AFTER setting stageLevel/statsBase.

    // Idempotent: always rebuild from SO, then apply growth, then temporary multipliers.
    public void RebuildFromBase()
    {
        // 1) Base stats
        unitStats.FromSO(statsBase);

        // 2) Growth / progression (per-level curves)
        if (progression)
        {
            var g = ProgressionMath.GetGrowthMultipliers(unitLevel, progression);
            unitStats.attack *= g.gA;
            unitStats.maxHP *= g.gH;
            unitStats.moveSpeed *= g.gMv;
            unitStats.attackSpeed *= g.gAS;
            // Add defense/range growth too if you decide to (usually left flat)
        }

        // 3) Runtime multipliers (buffs/auras/wave)
        unitStats.ApplyMultipliers(_atkM, _defM, _hpM, _mvM, _asM, _rngM);

        // 4) Sync health to HP scripts
        var hp = GetComponent<EnemyStats>();
        if (hp) { hp.maxHealth = unitStats.maxHP; hp.currentHP = unitStats.maxHP; }

        // 5) Compute CP (stage-weighted). If cpWeights is null, use built-in defaults.
        cp = UnitCP_WithFallback(unitStats, stageLevel, cpWeights);

        // Debug:
        // Debug.Log($"{name}  UnitL{unitLevel}  StageL{stageLevel}  CP={cp} | ATK={unitStats.attack} HP={unitStats.maxHP} MS={unitStats.moveSpeed} AS={unitStats.attackSpeed}");
    }

    // Optional: let other systems set temporary modifiers, then rebuild.
    public void SetRuntimeMultipliers(float atk = 1f, float def = 1f, float hp = 1f,
                                      float mv = 1f, float atkSpd = 1f, float rng = 1f,
                                      bool rebuildNow = true)
    {
        _atkM = atk; _defM = def; _hpM = hp; _mvM = mv; _asM = atkSpd; _rngM = rng;
        if (rebuildNow) RebuildFromBase();
    }

    // --- CP helper with a safe fallback when cpWeights is missing ---
    int UnitCP_WithFallback(UnitStatsRuntime s, int stage, CPWeightsConfigSO cfg)
    {
        if (cfg != null)
            return CPCalculator.UnitCP(s, stage, cfg);

        // Fallback constants match your earlier CPConfig defaults
        float wA = 1.00f, wH = 0.15f, wMv = 0.25f, wAS = 0.40f, wD = 0.00f;
        float typeMult = (s.type == FighterType.Archer) ? 1.05f : 1.00f;

        float baseScore = wA * s.attack + wH * s.maxHP + wMv * s.moveSpeed + wAS * s.attackSpeed + wD * s.defense;
        return Mathf.RoundToInt(baseScore * typeMult);
    }

    #endregion


    EnemyLocoMotion enemyLocoMotion;
    EnemyAnimatorManager enemyAnimationManager;
    PlayerStats playerStats;
    EnemyStats enemyStats;

    public PlayerGateStats currentGateTarget;
    public bool attackPlayerGate = false;

    public CapsuleCollider2D enemyDamageCollider;

    [Header("Combat")]
    public EnemyAttackAction defaultAttack;   // ScriptableObject like your PlayerAttackAction
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
    public bool playerIsInLeft;              // true if enemy.x < player.x

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
        //enemyLocomotionManager = GetComponent<EnemyLocomotionManager>();
        enemyAnimationManager = GetComponentInChildren<EnemyAnimatorManager>();
        playerStats = GameObject.FindObjectOfType<PlayerStats>();
        enemyStats = GetComponent<EnemyStats>();
        if (currentGateTarget == null)
        {
            var go = GameObject.Find("PlayerCastle");      // finds ACTIVE object by exact name
            if (go != null)
                currentGateTarget = go.GetComponent<PlayerGateStats>();
            else
                Debug.LogError("PlayerCastle not found in scene!");
        }
    }
    private void Start()
    {
        if (enemyDamageCollider != null)
        {
            enemyDamageCollider.enabled = false;
        }
        if (faceCenterOnStart) SetInitialFacingByScreenHalf();

        //GameObject.FindObjectOfType<RogueliteManager>().RegisterEnemy(this);

    }
    #region Facing Enemy at the start

    public void UpdateFacing()
    {
        // Grab the current target (whatever type it is, we only need its transform)
        var target = enemyLocoMotion.currentTarget;
        if (target == null)
            return;

        // Enemy's own position (this script is on the enemy)
        Vector2 enemyPos = (enemyLocoMotion.enemyRigidbody2D != null)
            ? enemyLocoMotion.enemyRigidbody2D.position
            : (Vector2)transform.position;

        // Player's position (the target we are facing)
        Vector2 playerPos = (Vector2)target.transform.position;

        // Is the player to the LEFT of the enemy?
        // (player.x < enemy.x) -> playerIsInLeft = true
        playerIsInLeft = playerPos.x < enemyPos.x;

        // Flip sprite on X so we face the player
        // Assumes your sprite faces RIGHT when scale.x is positive
        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = playerIsInLeft ? -absX : absX;
        transform.localScale = ls;
    }

    /// Face toward the screen center based on current position.
    public void SetInitialFacingByScreenHalf()
    {
        var cam = Camera.main;
        if (!cam) return;

        // Viewport (0..1, 0..1). x<0.5 => left half, x>=0.5 => right half.
        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        bool isOnLeftHalf = vp.x < 0.5f;

        // If on left half, face RIGHT (toward center). If on right half, face LEFT.
        FaceLeft(!isOnLeftHalf);
    }

    /// Flip sprite on X. Assumes your sprite faces RIGHT when scale.x is positive.
    public void FaceLeft(bool faceLeft)
    {
        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = faceLeft ? -absX : absX;
        transform.localScale = ls;
    }
    #endregion

    private void StopMovingEnemy()
    {
        enemyAnimationManager.anim.SetFloat("Vertical", 0, 0, 0);
        enemyAnimationManager.anim.SetFloat("Horizontal", 0, 0, 0);

        if (enemyLocoMotion != null)
        {
            var rb = enemyLocoMotion.enemyRigidbody2D;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;        // ← correct property
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
                if (enemyStats.enemyIsdead)
                {
                    rb.bodyType = RigidbodyType2D.Static;
                    enemyAnimationManager.anim.Play("Dying");

                }
            }

            //enemyLocoMotion.SetAnimMoving(false);
       
        }
    }
    private void Update()
    {
        if (enemyAnimationManager == null || enemyLocoMotion == null)
        {
            return;
        }
        HandleRecoveryTimer();

        isInteracting = enemyAnimationManager.anim.GetBool("isInteracting");

        if (enemyStats.enemyIsdead)
        {
            StopMovingEnemy();
            
            //Destroy(gameObject , 1f);

            if (!xpGiven)
            {
                xpGiven = true;
                //var manager = FindObjectOfType<RogueliteManager>();
                //if (manager != null) manager.NotifyEnemyKilled(this);

                // Use real‑time delay so the destroy still occurs during pause
                StartCoroutine(DestroyAfterDelayRealtime(3f));
            }
        }
    }
    private IEnumerator DestroyAfterDelayRealtime(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Destroy(gameObject);
    }
    void FixedUpdate()
    {
        HandleCurrentAction();

    }

    private void HandleCurrentAction()
    {
        if (enemyLocoMotion == null /*|| playerStats == null*/)
        {
            return;
        }
        if (attackPlayerGate && currentGateTarget != null)
        {
           

            Debug.Log("ATTACKING PLAYER GATE");
            if (currentRecoveryTimer <= 0 && !isPerformingAction)
            {
                isPerformingAction = true;
                currentRecoveryTimer = currentAttack.recoveryTime;

                //// neutralize locomotion params
                //enemyAnimationManager.anim.SetFloat("Vertical", 0, 0, 0);
                //enemyAnimationManager.anim.SetFloat("Horizontal", 0, 0, 0);
                StopMovingEnemy();

                //StopMovingEnemy();

                float speedMultiplier = unitStats.attackSpeed;
                enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);

                // After damage, if destroyed → complete
                if (currentGateTarget.isPlayerGateDestroyed)
                    enemyLocoMotion.enemyRigidbody2D.bodyType = RigidbodyType2D.Static;

                // consume the gate flag if you want single-shot
                // pm.attackGate = false;
            }
        }

        if (enemyLocoMotion.distanceFromTarget <= enemyLocoMotion.stoppingDistance)
        {
            // Handle Attack
            if (playerStats != null && playerStats.playerIsdead)
            {

                return;
            }
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

    private void HandleGateDestroyed()
    {
        // clean up gate engagement flags so we don’t try to attack anymore
        attackPlayerGate = false;
        currentGateTarget = null;

        // stop motion (null-safe)
        //enemyAnimationManager.anim.SetFloat("Vertical", 0, 0, 0);
        //enemyAnimationManager.anim.SetFloat("Horizontal", 0, 0, 0);
        if (enemyLocoMotion != null)
        {
            var rb = enemyLocoMotion.enemyRigidbody2D;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;        // ← correct property
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Static;
            }

            enemyLocoMotion.SetAnimMoving(false);
        }

        // Optionally play a "victory/idle" anim here or change state if you have one
    }


    public float DamageApplying(PlayerManager target)
    {
        damageAppling = CombatMath.DamagePerHit(unitStats, target.unitStats);

        return damageAppling;
    }

    //private void OnCollisionEnter2D(Collision2D collision)
    //{
    //    if (collision.gameObject.CompareTag("PlayerGate"))
    //    {
    //        attackPlayerGate = true;
    //        currentGateTarget = collision.collider.GetComponent<PlayerGateStats>(); // <-- set gate target

    //        Debug.Log(" Collision With Enemy Gate");
    //    }
    //}

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerGate"))
        {
            attackPlayerGate = true;
            currentGateTarget = collision.GetComponent<PlayerGateStats>(); // <-- set gate target

            Debug.Log(" Collision With Enemy Gate");

        }
    }

    #region Attack Region


    private void HandleRecoveryTimer()
    {
        if (currentRecoveryTimer > 0)
        {
            currentRecoveryTimer -= Time.deltaTime;

        }
        if (isPerformingAction)
        {
            if (currentRecoveryTimer <= 0)
            {

                isPerformingAction = false;
            }
        }
    }
    public void AttackTarget()
    {
        //enemyLocoMotion.HandleMoveToTarget();

        if (isPerformingAction)
        {
            enemyAnimationManager.anim.SetFloat("Vertical", 0f, 0f, 0f);

            return;
        }

        if (currentAttack == null)
        {
            GetNewAttack();
            Debug.Log("Attack");
            return;
        }
        else
        {
            isPerformingAction = true;
            currentRecoveryTimer = currentAttack.recoveryTime;

            // Store the player's position before the attack
            Vector3 attackPosition = transform.position;

            // 1) Snap locomotion to idle instantly


            enemyAnimationManager.anim.SetFloat("Vertical", 0f, 0f, 0f);


            //// 2) Freeze movement
            //var rb = playerLocomotionManager.playerRigidbody;
            //rb.linearVelocity = Vector3.zero;

            // set attack speed just for this clip
            var anim = enemyAnimationManager.anim;

            float speedMultiplier = unitStats.attackSpeed;


            // Play the attack animation
            enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);



        }
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
}


public class EnemyManager1 : MonoBehaviour
{
    #region CP/Stats/Progrression


    [Header("Base stats")]
    public UnitStatsSO statsBase;

    [Header("Live stats (mutable)")]
    public UnitStatsRuntime unitStats = new UnitStatsRuntime();

    [Header("Progression (curves)")]
    [Min(1)] public int unitLevel = 1;
    public ProgressionConfigSO progression;

    [Header("CP weights (stage lens)")]
    [Min(1)] public int stageLevel = 1;      // set by spawner
    public CPWeightsConfigSO cpWeights;      // assign in spawner or inspector

    [Header("Debug")]
    public int cp;

    // Optional: a place to store temporary multipliers (buffs/wave scaling)
    float _atkM = 1f, _defM = 1f, _hpM = 1f, _mvM = 1f, _asM = 1f, _rngM = 1f;

    // --- call this from the spawner right after Instantiate ---

    private bool xpGiven = false;

    public void Initialize(int stageLevelFromSpawner)
    {
        stageLevel = stageLevelFromSpawner;
        RebuildFromBase();      // idempotent; safe to call anytime
    }

    // If you use pooling, you can also call RebuildFromBase() in OnEnable AFTER setting stageLevel/statsBase.

    // Idempotent: always rebuild from SO, then apply growth, then temporary multipliers.
    public void RebuildFromBase()
    {
        // 1) Base stats
        unitStats.FromSO(statsBase);

        // 2) Growth / progression (per-level curves)
        if (progression)
        {
            var g = ProgressionMath.GetGrowthMultipliers(unitLevel, progression);
            unitStats.attack *= g.gA;
            unitStats.maxHP *= g.gH;
            unitStats.moveSpeed *= g.gMv;
            unitStats.attackSpeed *= g.gAS;
            // Add defense/range growth too if you decide to (usually left flat)
        }

        // 3) Runtime multipliers (buffs/auras/wave)
        unitStats.ApplyMultipliers(_atkM, _defM, _hpM, _mvM, _asM, _rngM);

        // 4) Sync health to HP scripts
        var hp = GetComponent<EnemyStats>();
        if (hp) { hp.maxHealth = unitStats.maxHP; hp.currentHP = unitStats.maxHP; }

        // 5) Compute CP (stage-weighted). If cpWeights is null, use built-in defaults.
        cp = UnitCP_WithFallback(unitStats, stageLevel, cpWeights);

        // Debug:
        // Debug.Log($"{name}  UnitL{unitLevel}  StageL{stageLevel}  CP={cp} | ATK={unitStats.attack} HP={unitStats.maxHP} MS={unitStats.moveSpeed} AS={unitStats.attackSpeed}");
    }

    // Optional: let other systems set temporary modifiers, then rebuild.
    public void SetRuntimeMultipliers(float atk = 1f, float def = 1f, float hp = 1f,
                                      float mv = 1f, float atkSpd = 1f, float rng = 1f,
                                      bool rebuildNow = true)
    {
        _atkM = atk; _defM = def; _hpM = hp; _mvM = mv; _asM = atkSpd; _rngM = rng;
        if (rebuildNow) RebuildFromBase();
    }

    // --- CP helper with a safe fallback when cpWeights is missing ---
    int UnitCP_WithFallback(UnitStatsRuntime s, int stage, CPWeightsConfigSO cfg)
    {
        if (cfg != null)
            return CPCalculator.UnitCP(s, stage, cfg);

        // Fallback constants match your earlier CPConfig defaults
        float wA = 1.00f, wH = 0.15f, wMv = 0.25f, wAS = 0.40f, wD = 0.00f;
        float typeMult = (s.type == FighterType.Archer) ? 1.05f : 1.00f;

        float baseScore = wA * s.attack + wH * s.maxHP + wMv * s.moveSpeed + wAS * s.attackSpeed + wD * s.defense;
        return Mathf.RoundToInt(baseScore * typeMult);
    }

    #endregion


    EnemyLocoMotion enemyLocoMotion;
    EnemyAnimatorManager enemyAnimationManager;
    PlayerStats playerStats;
    EnemyStats enemyStats;

    public PlayerGateStats currentGateTarget;
    public bool attackPlayerGate = false;

    public CapsuleCollider2D enemyDamageCollider;

    [Header("Combat")]
    public EnemyAttackAction defaultAttack;   // ScriptableObject like your PlayerAttackAction
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
    public bool playerIsInLeft;              // true if enemy.x < player.x

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
        //enemyLocomotionManager = GetComponent<EnemyLocomotionManager>();
        enemyAnimationManager = GetComponentInChildren<EnemyAnimatorManager>();
        playerStats = GameObject.FindObjectOfType<PlayerStats>();
        enemyStats = GetComponent<EnemyStats>();
        if (currentGateTarget == null)
        {
            var go = GameObject.Find("PlayerCastle");      // finds ACTIVE object by exact name
            if (go != null)
                currentGateTarget = go.GetComponent<PlayerGateStats>();
            else
                Debug.LogError("PlayerCastle not found in scene!");
        }
    }
    private void Start()
    {
        if (enemyDamageCollider !=null)
        {
        enemyDamageCollider.enabled = false;
        }
        if (faceCenterOnStart) SetInitialFacingByScreenHalf();

        //GameObject.FindObjectOfType<RogueliteManager>().RegisterEnemy(this);

    }
    #region Facing Enemy at the start

    public void UpdateFacing()
    {
        // Grab the current target (whatever type it is, we only need its transform)
        var target = enemyLocoMotion.currentTarget;
        if (target == null)
            return;

        // Enemy's own position (this script is on the enemy)
        Vector2 enemyPos = (enemyLocoMotion.enemyRigidbody2D != null)
            ? enemyLocoMotion.enemyRigidbody2D.position
            : (Vector2)transform.position;

        // Player's position (the target we are facing)
        Vector2 playerPos = (Vector2)target.transform.position;

        // Is the player to the LEFT of the enemy?
        // (player.x < enemy.x) -> playerIsInLeft = true
        playerIsInLeft = playerPos.x < enemyPos.x;

        // Flip sprite on X so we face the player
        // Assumes your sprite faces RIGHT when scale.x is positive
        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = playerIsInLeft ? -absX : absX;
        transform.localScale = ls;
    }

    /// Face toward the screen center based on current position.
    public void SetInitialFacingByScreenHalf()
    {
        var cam = Camera.main;
        if (!cam) return;

        // Viewport (0..1, 0..1). x<0.5 => left half, x>=0.5 => right half.
        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        bool isOnLeftHalf = vp.x < 0.5f;

        // If on left half, face RIGHT (toward center). If on right half, face LEFT.
        FaceLeft(!isOnLeftHalf);
    }

    /// Flip sprite on X. Assumes your sprite faces RIGHT when scale.x is positive.
    public void FaceLeft(bool faceLeft)
    {
        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = faceLeft ? -absX : absX;
        transform.localScale = ls;
    }
    #endregion

    private void StopMovingEnemy()
    {
        enemyAnimationManager.anim.SetFloat("Vertical", 0, 0, 0);
        enemyAnimationManager.anim.SetFloat("Horizontal", 0, 0, 0);

        if (enemyLocoMotion != null)
        {
            var rb = enemyLocoMotion.enemyRigidbody2D;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;        // ← correct property
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Static;
            }

            //enemyLocoMotion.SetAnimMoving(false);
            if (enemyStats.enemyIsdead)
            {
                enemyAnimationManager.anim.Play("Dying");
            }
        }
    }
    private void Update()
    {
        if (enemyAnimationManager==null || enemyLocoMotion == null)
        {
            return;
        }
        HandleRecoveryTimer();

        isInteracting = enemyAnimationManager.anim.GetBool("isInteracting");

        if (enemyStats.enemyIsdead)
        {
            StopMovingEnemy();


            //// Award XP and remove from activeEnemies
            //var manager = GameObject.FindObjectOfType<RogueliteManager>();
            //if (manager != null) manager.NotifyEnemyKilled(this);

            //Destroy(gameObject , 1f);

            if (!xpGiven)
            {
                xpGiven = true;
                //var manager = FindObjectOfType<RogueliteManager>();
                //if (manager != null) manager.NotifyEnemyKilled(this);

                // Use real‑time delay so the destroy still occurs during pause
                StartCoroutine(DestroyAfterDelayRealtime(3f));
            }
        }
    }
    private IEnumerator DestroyAfterDelayRealtime(float seconds)
    {
        yield return new WaitForSecondsRealtime(seconds);
        Destroy(gameObject);
    }
    void FixedUpdate()
    {
        HandleCurrentAction();

    }

    private void HandleCurrentAction()
    {
        if (enemyLocoMotion == null /*|| playerStats == null*/)
        {
            return;
        }
        bool gateEngagement = (attackPlayerGate == true && currentGateTarget != null);
        if (gateEngagement)
        {
            //if (enemyLocoMotion.enemyRigidbody2D)
            //{
            //    enemyLocoMotion.enemyRigidbody2D.linearVelocity = Vector2.zero;
            //    enemyLocoMotion.enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            //}

            Debug.Log("ATTACKING PLAYER GATE");
            if (currentRecoveryTimer <= 0 && !isPerformingAction)
            {
                isPerformingAction = true;
                currentRecoveryTimer = currentAttack.recoveryTime;

                //// neutralize locomotion params
                //enemyAnimationManager.anim.SetFloat("Vertical", 0, 0, 0);
                //enemyAnimationManager.anim.SetFloat("Horizontal", 0, 0, 0);
                StopMovingEnemy();

                //StopMovingEnemy();

                float speedMultiplier = unitStats.attackSpeed;
                enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);

                // After damage, if destroyed → complete
                if (currentGateTarget.isPlayerGateDestroyed)
                    enemyLocoMotion.enemyRigidbody2D.bodyType = RigidbodyType2D.Static;

                // consume the gate flag if you want single-shot
                // pm.attackGate = false;
            }
        }

        if (enemyLocoMotion.distanceFromTarget <= enemyLocoMotion.stoppingDistance)
        {
            // Handle Attack
            if ( playerStats != null && playerStats.playerIsdead)
            {

                return;
            }
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

        //if (enemyLocomotionManager.currentTarget != null)
        //{
        //    enemyLocomotionManager.distanceFromTarget = Vector3.Distance(enemyLocomotionManager.currentTarget.transform.position, transform.position);
        //}

        //if (enemyLocomotionManager.currentTarget == null)
        //{
        //    enemyLocomotionManager.HandleDetection();
        //}

        //else if (enemyLocomotionManager.distanceFromTarget > enemyLocomotionManager.stoppingDistance)
        //{
        //    enemyLocomotionManager.HandleMoveToTarget();

        //}
        //else if (enemyLocomotionManager.distanceFromTarget <= enemyLocomotionManager.stoppingDistance)
        //{
        //    // Handle Attack
        //    if (playerStats.playerIsdead)
        //    {

        //        return;
        //    }
        //    if (!enemyStats.enemyIsdead)
        //    {
        //    AttackTarget();

        //    }

        //}

    }

    private void HandleGateDestroyed1()
    {
        // clean up gate engagement flags so we don’t try to attack anymore
        attackPlayerGate = false;
        currentGateTarget = null;

        // stop motion
        if (enemyLocoMotion.enemyRigidbody2D)
        {
            enemyLocoMotion.enemyRigidbody2D.linearVelocity = Vector2.zero;
            enemyLocoMotion.enemyRigidbody2D.angularVelocity = 0f;
            enemyLocoMotion.enemyRigidbody2D.bodyType = RigidbodyType2D.Static;
        }
        enemyLocoMotion.SetAnimMoving(false);

        // transition to Game Complete state
        //ChangeStateSafe(playerGameCompleteState);
    }
    private void HandleGateDestroyed()
    {
        // clean up gate engagement flags so we don’t try to attack anymore
        attackPlayerGate = false;
        currentGateTarget = null;

        // stop motion (null-safe)
        //enemyAnimationManager.anim.SetFloat("Vertical", 0, 0, 0);
        //enemyAnimationManager.anim.SetFloat("Horizontal", 0, 0, 0);
        if (enemyLocoMotion != null)
        {
            var rb = enemyLocoMotion.enemyRigidbody2D;
            if (rb != null)
            {
                rb.linearVelocity = Vector2.zero;        // ← correct property
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Static;
            }

            enemyLocoMotion.SetAnimMoving(false);
        }



        

        // Optionally play a "victory/idle" anim here or change state if you have one
        // e.g., ChangeStateSafe(playerGameCompleteState);
    }


    public float DamageApplying(PlayerManager target)
    {
        damageAppling = CombatMath.DamagePerHit(unitStats, target.unitStats);

        return damageAppling;
    }

    //private void OnCollisionEnter2D(Collision2D collision)
    //{
    //    if (collision.gameObject.CompareTag("PlayerGate"))
    //    {
    //        attackPlayerGate = true;
    //        currentGateTarget = collision.collider.GetComponent<PlayerGateStats>(); // <-- set gate target

    //        Debug.Log(" Collision With Enemy Gate");
    //    }
    //}

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("PlayerGate"))
        {
            attackPlayerGate = true;
            currentGateTarget = collision.GetComponent<PlayerGateStats>(); // <-- set gate target

            Debug.Log(" Collision With Enemy Gate");

        }
    }

    #region Attack Region


    private void HandleRecoveryTimer()
    {
        if (currentRecoveryTimer > 0)
        {
            currentRecoveryTimer -= Time.deltaTime;

        }
        if (isPerformingAction)
        {
            if (currentRecoveryTimer <= 0)
            {

                isPerformingAction = false;
            }
        }
    }
    public void AttackTarget()
    {
        //enemyLocoMotion.HandleMoveToTarget();

        if (isPerformingAction)
        {
            enemyAnimationManager.anim.SetFloat("Vertical", 0f, 0f, 0f);

            return;
        }

        if (currentAttack == null)
        {
            GetNewAttack();
            Debug.Log("Attack");
            return;
        }
        else
        {
            isPerformingAction = true;
            currentRecoveryTimer = currentAttack.recoveryTime;

            // Store the player's position before the attack
            Vector3 attackPosition = transform.position;

            // 1) Snap locomotion to idle instantly


            enemyAnimationManager.anim.SetFloat("Vertical", 0f, 0f, 0f);


            //// 2) Freeze movement
            //var rb = playerLocomotionManager.playerRigidbody;
            //rb.linearVelocity = Vector3.zero;

            // set attack speed just for this clip
            var anim = enemyAnimationManager.anim;

            float speedMultiplier = unitStats.attackSpeed;


            // Play the attack animation
            enemyAnimationManager.PlayTargetAnimation(currentAttack.animationName, true , speedMultiplier);



        }
    }


    public void GetNewAttack()
    {

        //Vector2 targetDirection = (Vector2)(enemyLocoMotion.currentTarget.transform.position - transform.position);
        //float viewableAngle = Vector3.Angle(targetDirection, transform.forward);
        //enemyLocoMotion.distanceFromTarget = Vector3.Distance(enemyLocoMotion.currentTarget.transform.position, transform.position);

        if (enemyLocoMotion.distanceFromTarget < defaultAttack.maxDistanceNeededToAttack &&
            enemyLocoMotion.distanceFromTarget >= defaultAttack.minDistanceNeededToAttack)
        {
            currentAttack = defaultAttack;

            //if (viewableAngle >= playerAttackAction.minAttackAngle && viewableAngle <= playerAttackAction.minAttackAngle)
            //{
            //    //if (currentAttack  != null)
            //    //{
            //    //    return;
            //    //}

            //    currentAttack = playerAttackAction;
            //}
        }

    }

    #endregion
}
