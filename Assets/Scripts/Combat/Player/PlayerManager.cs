using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-100)]

public class PlayerManager : MonoBehaviour
{    

    [HideInInspector]public List<ChildLocalSnapshot> childSnapshot;



    [Header("Assign a base stats asset")]
    public UnitStatsSO statsBase; 

    [Header("Runtime (readonly in Inspector)")]
    public UnitStatsRuntime unitStats = new UnitStatsRuntime();

    public CapsuleCollider2D playerDamageCollider;

    public PlayerStatsApplier playerStatsApplier;
    public TargetDetectionForPlayer targetDetectionForPlayer;
   
    public PlayerAnimatitorManager playerAnimatitorManager;
    public Rigidbody2D playerRigidbody;


    PlayerStats PlayerStats;
    EnemyStats enemyStats;
    private EnemyStats lastKnownTarget;

    public bool hasDetectedEnemyOnce = false;   // becomes true the first time we acquire a target
    private bool hasEverDetectedEnemy = false;


    public EnemyStats currentTarget;
    public EnemyGateStats currentGateTarget;     // NEW: gate target when colliding a gate

    [Header(" Player States")]
    public PlayerIdleState playerIdleState;
    public PlayerLockState PlayerLockState;
    public PlayerPursueTargetState PlayerPursueTargetState;
    public PlayerAttackState AttackState;
    public PlayerDeathState PlayerDeathState;
    public PlayerGameCompleteState playerGameCompleteState;
    public PlayerState currentState;

    [Header ("Booleans")]
    public bool isPerformingAction;
    public bool isInteracting;

    [Header(" AI setting")]
    public float detectionRadius = 20f;
    public float minimumDetectionAngle = -60f;
    public float maximumDetectionAngle = 60f;
    public float rotationSpeed = 15;
    public float currentRecoveryTimer = 0.5f;

    public float maxAttackRange = 1f;
    public float distanceFromTarget;

    [Header("  Player Movement Variables")]
    public float moveSpeed = 0.5F;
    public bool canMove = false;
    public bool isUnlocked = false;
    private float damageAppling;

    [Header("Facing/Targeting")]
    public bool faceCenterOnStart = true;

    public bool enemyIsInLeft;              // true if enemy.x < player.x
    public Transform chosenEnemyOffset;     // what we’ll actually chase

    public bool attackPlayerGate;

    [Header("Links")]
    public Transform visualRoot;  // assign the 'Visual' child in the prefab


    // NOTE: the old "Separation / Avoidance" and "Horizontal overlap fix" fields
    // were REMOVED on 2026-08-21. Unit spacing is now owned entirely by the
    // scene-level CrowdSeparation2D component, which handles players and enemies
    // with one consistent rule. See CrowdSeparation2D.cs for why the old two
    // systems were deleted rather than repaired.

    private void OnEnable()
    {
        EnemyGateStats.OnGateDestroyed += HandleGateDestroyed;
    }
    private void OnDisable()
    {
        EnemyGateStats.OnGateDestroyed -= HandleGateDestroyed;

        // Hand the attack spot back so a later attacker can use it.
        AttackSlotRegistry.Release(this);
        attackSlotTarget = null;
    }
    private void Awake()
    {
        playerStatsApplier = GetComponent<PlayerStatsApplier>();



        targetDetectionForPlayer = GetComponent<TargetDetectionForPlayer>();
        playerAnimatitorManager = GetComponentInChildren<PlayerAnimatitorManager>();
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        PlayerStats = GetComponent<PlayerStats>();
        enemyStats = FindAnyObjectByType<EnemyStats>();


    }
    private void Start()
    {
        if (playerRigidbody!= null)
        {

        playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
        }

        if (playerDamageCollider!=null)
        {
        playerDamageCollider.enabled = false;

        }

        if (faceCenterOnStart) SetFacingByScreenHalf();


        if (playerStatsApplier == null)
        {
            playerStatsApplier = GetComponent<PlayerStatsApplier>();

        }

        // Only adopt the applier's stats once it has actually computed them.
        // PlayerStatsApplier.ApplyNow() bails out and leaves CurrentStats NULL
        // whenever GameStartManager / PlayerUnits is missing - which is exactly
        // what happens when a gameplay scene is played directly instead of
        // booting through StarterScene. Assigning that null over the serialized
        // instance is what produced the NullReferenceException in
        // CombatMath.DamagePerHit (the enemy hits the gate, gate stats are null).
        if (playerStatsApplier != null && playerStatsApplier.CurrentStats != null)
        {
            unitStats = playerStatsApplier.CurrentStats;
        }
        else if (unitStats == null)
        {
            unitStats = new UnitStatsRuntime();
            Debug.LogWarning($"[PlayerManager] '{name}' has no computed stats yet - " +
                             "keeping serialized/default values. Boot through StarterScene " +
                             "so GameStartManager exists.", this);
        }

       
    }
    private void HandleGateDestroyed1()
    {
        // clean up gate engagement flags so we don’t try to attack anymore
        attackPlayerGate = false;
        currentGateTarget = null;

        // stop motion
        if (playerRigidbody)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
            playerRigidbody.bodyType = RigidbodyType2D.Static;
        }
        SetAnimMoving(false);
        HudCurrencyView.Instance?.PauseGameplay();



        // transition to Game Complete state
        ChangeStateSafe(playerGameCompleteState);
    }

    public void HandleGateDestroyed()
    {
        // IMPORTANT: NO global pause here. We just stop the player unit itself.

        attackPlayerGate = false;
        currentGateTarget = null;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = 0f;

            playerRigidbody.isKinematic = true;
        }

        SetAnimMoving(false);
        HudCurrencyView.Instance?.PauseGameplay();


        // Move player into GameComplete state
        if (playerGameCompleteState != null)
            ChangeStateSafe(playerGameCompleteState);
    }
    // helper to guard against null / same-state
    public void ChangeStateSafe(PlayerState next)
    {
        if (next == null) return;
        if (currentState == next) return;
        SwitchToNextState(next); // <-- use your real state-switch method name
    }

    #region Facing Player 

    public void SetVisualScale(float factor)
    {
        if (visualRoot != null)
            visualRoot.localScale = Vector3.one * factor;
    }
    public void UpdateFacing()
    {
        // 🔒 Freeze facing during attack or lock
        if (currentState == AttackState || isInteracting)
            return;

        // Priority 1: real enemy target
        if (currentTarget != null && currentState != PlayerLockState)
        {
            UpdateFacingAndOffset();
            return;
        }

        // Priority 2: no enemy → screen-half logic
        SetFacingByScreenHalf();
    }

    public void UpdateFacingAndOffset1()
    {
        if (currentTarget == null) { /*chosenEnemyOffset = null;*/ return; }

        Vector2 playerPos = playerRigidbody != null ? playerRigidbody.position : (Vector2)transform.position;
        Vector2 enemyPos = currentTarget.transform.position;

        // 1) Which side is the enemy relative to the PLAYER?
        enemyIsInLeft = (enemyPos.x <= playerPos.x);

        // 2) Flip player to face that side (sprite facing right by default → flip X)
        var ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);
        ls.x = enemyIsInLeft ? -absX : absX;
        transform.localScale = ls;

        // 3) Choose which enemy offset to chase.
        // Option A: nearest side (recommended)
        chosenEnemyOffset = currentTarget.GetOffsetFacingPlayer(playerPos);

    }
    public void UpdateFacingAndOffset()
    {
        if (currentTarget == null)
            return;

        Vector2 playerPos = playerRigidbody != null
            ? playerRigidbody.position
            : (Vector2)transform.position;

        Vector2 enemyPos = currentTarget.transform.position;

        float dx = enemyPos.x - playerPos.x;

        // Dead-zone to prevent micro jitter
        if (Mathf.Abs(dx) < 0.03f)
            return;

        bool enemyIsOnLeft = dx < 0f;

        FaceLeft(enemyIsOnLeft);

        // Choose which offset to chase (this stays correct)
        chosenEnemyOffset = currentTarget.GetOffsetFacingPlayer(playerPos);
    }


    /// Face toward the screen center based on current position.
    public void SetFacingByScreenHalf()
    {
        var cam = Camera.main;
        if (!cam) return;

        Vector3 vp = cam.WorldToViewportPoint(transform.position);
        bool isOnLeftHalf = vp.x <= 0.5f;

        // If on left half, face RIGHT (toward center). If on right half, face LEFT.
        FaceLeft(!isOnLeftHalf);
    }

    /// Flip sprite on X. Assumes your sprite faces RIGHT when scale.x is positive.
    public void FaceLeft(bool faceLeft)
    {
        Vector3 ls = transform.localScale;
        float absX = Mathf.Abs(ls.x);

        // faceLeft is a WORLD-space intent, but localScale is LOCAL.
        // While a hero waits on the gate, PlayerWaveManager parents it to a
        // "Top_0" under PlayerCastle - and PlayerCastle is MIRRORED
        // (lossyScale.x = -1), so a raw local flip renders BACKWARDS. Convert
        // the wanted world sign into the local sign that actually produces it.
        // Once the wave unlocks the hero is unparented, parentSign becomes +1,
        // and this collapses back to the original behaviour.
        float wantWorldSign = faceLeft ? -1f : 1f;

        float parentSign = 1f;
        if (transform.parent != null && transform.parent.lossyScale.x < 0f)
            parentSign = -1f;

        ls.x = absX * wantWorldSign * parentSign;
        transform.localScale = ls;
        if (currentTarget!=null)
        {
        chosenEnemyOffset = currentTarget.GetOffsetFacingPlayer(transform.position);

        }


    }


    #endregion

    private void Update()
    {
        if (GameplayPause.IsPaused)
            return;


        HandleRecoveryTimer();

        if (playerAnimatitorManager!=null)
        {
        isInteracting = playerAnimatitorManager.anim.GetBool("isInteracting");
        }


 
        if (PlayerStats!=null && PlayerStats.playerIsdead)
        {
            SwitchToNextState(PlayerDeathState);
        }


        UpdateTargetSelection();

        UpdateFacing(); // ONLY facing call



        // --- DEBUG: keep inspector distance always correct ---
        Vector3 targetPos = GetActiveTargetPosition();
        if (targetPos == transform.position)
        {
            distanceFromTarget = 0f;
        }
        else
        {
            distanceFromTarget = Vector2.Distance(transform.position, targetPos);
        }

    }

    public bool UpdateTargetSelection()
    {
        // =========================
        // Phase A: First detection
        // =========================
        if (!hasEverDetectedEnemy)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                detectionRadius,
                LayerMask.GetMask("EnemyLayer")
            );

            float bestDist = float.MaxValue;
            EnemyStats best = null;

            foreach (var h in hits)
            {
                EnemyStats es = h.GetComponent<EnemyStats>();
                if (es == null || es.enemyIsdead) continue;

                float d = Vector2.Distance(transform.position, es.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = es;
                }
            }

            if (best != null)
            {
                currentTarget = best;
                lastKnownTarget = best;
                hasEverDetectedEnemy = true;
                return true;
            }

            return false;
        }

        // =========================
        // Phase B: Persistent search
        // =========================
        EnemyStats nearest = null;
        float nearestDist = float.MaxValue;

        EnemyStats[] allEnemies = FindObjectsOfType<EnemyStats>();
        for (int i = 0; i < allEnemies.Length; i++)
        {
            EnemyStats es = allEnemies[i];
            if (es == null || es.enemyIsdead) continue;

            float d = Vector2.Distance(transform.position, es.transform.position);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = es;
            }
        }

        if (nearest != null)
        {
            if (currentTarget == null)
            {
                currentTarget = nearest;
                lastKnownTarget = nearest;
            }
            else
            {
                float currentDistSqr =
                    (transform.position - currentTarget.transform.position).sqrMagnitude;

                float nearestDistSqr =
                    (transform.position - nearest.transform.position).sqrMagnitude;

                if (nearestDistSqr < currentDistSqr)
                {
                    currentTarget = nearest;
                    lastKnownTarget = nearest;
                }
            }

            return true;
        }

        // No enemies left
        currentTarget = null;
        lastKnownTarget = null;
        hasEverDetectedEnemy = false;
        return false;
    }

    public Vector3 GetActiveTargetPosition()
    {
        if (attackPlayerGate && currentGateTarget != null)
            return currentGateTarget.transform.position;

        if (currentTarget != null)
            return currentTarget.transform.position;

        return transform.position; // fallback
    }

    [Header("Attack Spacing")]
    [Tooltip("Sideways gap between two heroes attacking the same target. Each " +
             "attacker claims its own spot on arrival - they never push to make room.")]
    [SerializeField] private float attackSlotSpacing = AttackSlotRegistry.DefaultSlotSpacing;

    // The target we currently hold an attack slot on, and which slot it is.
    private Object attackSlotTarget;
    private int attackSlotIndex;

    /// <summary>
    /// Lateral offset of this unit's own attack spot. Claims a slot the first
    /// time a target is engaged and keeps it until the target changes, so the
    /// unit stands still once it arrives instead of sliding around.
    /// </summary>
    private float CurrentAttackSlotOffset()
    {
        Object target = currentTarget != null ? (Object)currentTarget : (Object)currentGateTarget;

        if (target == null)
        {
            if (attackSlotTarget != null)
            {
                AttackSlotRegistry.Release(this);
                attackSlotTarget = null;
            }
            return 0f;
        }

        if (target != attackSlotTarget)
        {
            AttackSlotRegistry.Release(this);
            attackSlotIndex = AttackSlotRegistry.Claim(target, this);
            attackSlotTarget = target;
        }

        return AttackSlotRegistry.OffsetForSlot(attackSlotIndex, attackSlotSpacing);
    }

    public void HandleMoveToTarget(bool canMove)
{
    if (!canMove)
        return;

    if (playerRigidbody == null)
        return;

    if (chosenEnemyOffset == null)
        return;

    // Each attacker walks to its OWN spot beside the target instead of everyone
    // converging on one point. The slot is claimed once and kept for as long as
    // the target does not change, so nobody drifts sideways mid-fight - and no
    // unit ever pushes another to make room.
    Vector2 destination = (Vector2)chosenEnemyOffset.position
                        + new Vector2(CurrentAttackSlotOffset(), 0f);

    Vector2 toTarget = destination - (Vector2)transform.position;
    float dist = toTarget.magnitude;

    if (dist < 0.05f)
    {
        // Arrived at our own attack spot: stop dead and stay there. No shuffling,
        // no reacting to whoever else is standing nearby.
        playerRigidbody.linearVelocity = Vector2.zero;
        return;
    }

    Vector2 dir = toTarget.normalized;

    // PATH AVOIDANCE IS CONTEXT-LIMITED ON PURPOSE.
    //
    // Mid-battle, when a hero is chasing an enemy UNIT, there is NO avoidance at
    // all - units simply pass through each other. That is the plain behaviour
    // asked for, and it is also what stops a hero swerving around its own kill.
    //
    // It is only switched on when the hero is walking at the GATE, i.e. the end
    // of the battle, where several heroes converge on one structure and the ones
    // coming up from behind need to arc around the ones already hitting it.
    // The other place it runs is the pre-battle formation, handled in
    // FormationGapFiller.
    bool headingForGate = currentTarget == null && currentGateTarget != null;

    if (headingForGate && CrowdSeparation2D.Instance != null)
        dir = CrowdSeparation2D.Instance.SteerAroundBlockers(transform, dir);

    // NEW: Use velocity for smooth movement (no tunneling)
    playerRigidbody.linearVelocity = dir * moveSpeed;
    // Optional: Dampen if too far (for precision near target)
    if (dist < maxAttackRange)
        playerRigidbody.linearVelocity *= 0.7f;  // Slow down for attack
}

    // ApplyFriendlySeparation() and ResolveHorizontalOverlap() were DELETED on
    // 2026-08-21. Both tried to keep units apart and fought each other:
    //
    //   * ApplyFriendlySeparation bent the MOVE DIRECTION, so a crowded unit
    //     walked sideways instead of at its target. Worse, it ended with
    //     "(desiredDir + dodge.normalized).normalized" - the .normalized threw
    //     away every strength/crowd multiplier computed just above it, so the
    //     dodge was always a full-strength sideways shove.
    //
    //   * ResolveHorizontalOverlap ran MovePosition() every FixedUpdate,
    //     unconditionally, even mid-jump and while locked on the gate. And when
    //     two units sat at the SAME x, "pushDir = (dx >= 0f) ? 1f : -1f" gave
    //     BOTH of them +1, so they slid right together forever instead of
    //     separating - the exact stacking that was reported.
    //
    // Spacing is now owned by the scene-level CrowdSeparation2D component.

    // Drive your 2D Cartesian BlendTree (Horizontal/Vertical)
    public void SetAnimMoving(bool moving)
    {
        if (playerAnimatitorManager == null || playerAnimatitorManager.anim == null)
            return;
        playerAnimatitorManager.anim.SetFloat("Horizontal", 0f);
        playerAnimatitorManager.anim.SetFloat("Vertical", moving ? 1f : 0f);   // (0,1)=walk, (0,0)=idle in your setup
    }


 void FixedUpdate()
{
    HandleStateMachine();

    // Spacing used to be forced here every physics step via
    // ResolveHorizontalOverlap(). That is gone - CrowdSeparation2D owns it now.
    }

    private void HandleStateMachine()
    {
        if (currentState != null)
        {
            PlayerState nextState = currentState.Tick(this , PlayerStats, playerAnimatitorManager);
            if (nextState !=null)
            {
                SwitchToNextState(nextState);
            }
        }
    }

    public void SwitchToNextState(PlayerState playerState)
    {
        currentState = playerState;
    }
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

    public float DamageApplying(EnemyManager target)
    {
        damageAppling = CombatMath.DamagePerHit(unitStats, target.unitStats);

        return damageAppling;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("EnemyGate"))
        {
            attackPlayerGate = true;
            currentGateTarget = collision.collider.GetComponent<EnemyGateStats>(); // <-- set gate target

            Debug.Log(" Collision With Gate");
        }
    }
}
