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


    [Header("Separation / Avoidance")]
    [Tooltip("Radius to check for other friendly units.")]
    public float separationRadius = 0.5f;

    [Tooltip("How strongly we steer sideways when trying to avoid.")]
    public float separationStrength = 1.0f;

    [Tooltip("LayerMask for friendly units (e.g. Player layer).")]
    public LayerMask friendlyLayerMask;

    [Header("Horizontal overlap fix")]
    [SerializeField] private float minHorizontalSpacing = 0.35f; // how far apart in X
    [SerializeField] private float spacingCheckRadius = 0.4f;    // radius to look for neighbors

    private void OnEnable()
    {
        EnemyGateStats.OnGateDestroyed += HandleGateDestroyed;
    }
    private void OnDisable()
    {
        EnemyGateStats.OnGateDestroyed -= HandleGateDestroyed;
    }
    private void Awake()
    {
        playerStatsApplier = GetComponent<PlayerStatsApplier>();

        //if (statsBase == null)
        //    Debug.LogWarning($"{name}: missing UnitStatsSO.");
        //unitStats.FromSO(statsBase);


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

        //unitStats = playerStatsApplier.CurrentStats;

        if (playerStatsApplier == null)
        {
            playerStatsApplier = GetComponent<PlayerStatsApplier>();
           
        }

        unitStats = playerStatsApplier.CurrentStats;

       
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
        //Time.timeScale = 0f;
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

        //isMoving = false;
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

        // Option B (deterministic by side): uncomment this line and remove Option A above
        // chosenEnemyOffset = currentTarget.GetSideByFacing(enemyIsInLeft);
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

        // Viewport (0..1, 0..1). x<0.5 => left half, x>=0.5 => right half.
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
        ls.x = faceLeft ? -absX : absX;
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

        //if (warrior.IsDetached && currentTarget == null)
        //{
        //    SwitchToNextState(playerUnlcock);
        //    CapsuleCollider.enabled = true;
        //}

 
        if (PlayerStats!=null && PlayerStats.playerIsdead)
        {
            SwitchToNextState(PlayerDeathState);
        }
        //if (enemyStats != null && enemyStats.enemyIsdead)
        //{
        //    SwitchToNextState(playerIdleState);

        //}

        // 🔥 SINGLE SOURCE OF TRUTH
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
            // Update target ONLY if changed or closer
            //if (currentTarget == null || nearest != currentTarget)
            //{
            //    currentTarget = nearest;
            //    lastKnownTarget = nearest;
            //}
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

    public void HandleMoveToTarget(bool canMove)
{
    if (!canMove)
        return;

    if (playerRigidbody == null)
        return;

    if (chosenEnemyOffset == null)
        return;

    Vector2 toTarget = (Vector2)(chosenEnemyOffset.position - transform.position);
    float dist = toTarget.magnitude;

    if (dist < 0.0001f)
    {
        // Even when "at target," apply idle separation to unstick
        playerRigidbody.linearVelocity = ApplyFriendlySeparation(Vector2.right) * moveSpeed * 0.5f;  // Half-speed idle nudge
        return;
    }

    Vector2 dir = toTarget.normalized;
    dir = ApplyFriendlySeparation(dir);  // Always apply

    // NEW: Use velocity for smooth movement (no tunneling)
    playerRigidbody.linearVelocity = dir * moveSpeed;
    // Optional: Dampen if too far (for precision near target)
    if (dist < maxAttackRange)
        playerRigidbody.linearVelocity *= 0.7f;  // Slow down for attack
}

    private Vector2 ApplyFriendlySeparation(Vector2 desiredDir)
    {
        if (separationRadius <= 0f || friendlyLayerMask.value == 0)
            return desiredDir;

        Vector2 myPos = transform.position;
        float totalSidePush = 0f;  // Scalar for X-push (simpler, avoids vector bloat)
        float totalRepel = 0f;
        int overlapCount = 0;  // NEW: Count for scaling

        Collider2D[] hits = Physics2D.OverlapCircleAll(myPos, separationRadius, friendlyLayerMask);
        if (hits == null || hits.Length == 0)
            return desiredDir;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.attachedRigidbody == playerRigidbody) continue;
            if (!hit.CompareTag(gameObject.tag)) continue;

            Vector2 otherPos = hit.transform.position;
            Vector2 diff = myPos - otherPos;
            float sqrDist = diff.sqrMagnitude;

            if (sqrDist > separationRadius * separationRadius || sqrDist < 0.0001f)
                continue;

            // Lower/equal Y dodges (mutual for equals)
            if (myPos.y > otherPos.y)
                continue;

            overlapCount++;  // Count valid overlaps

            // Scale by closeness
            float avoidForce = 1f - Mathf.Sqrt(sqrDist) / separationRadius;

            // Horizontal side-push (away in X)
            float horizSign = Mathf.Sign(diff.x);
            if (horizSign == 0f) horizSign = 1f;
            totalSidePush += horizSign * avoidForce;

            // X-repel bonus
            totalRepel += diff.normalized.x * avoidForce * 0.5f;
        }

        if (overlapCount == 0)
            return desiredDir;

        // NEW: Scale by crowd density (e.g., 1.5x for 3+ overlaps)
        float crowdMultiplier = Mathf.Lerp(1f, 2f, (overlapCount - 1f) / 3f);  // Caps at 2x for 4+ units
        float pushStrength = separationStrength * crowdMultiplier;

        // Blend: Forward + horizontal dodge (Y=0)
        Vector2 dodge = new Vector2(totalSidePush + totalRepel, 0f) * pushStrength;
        Vector2 newDir = (desiredDir + dodge.normalized).normalized;
        return newDir;
    }

    private void ResolveHorizontalOverlap()
    {
        if (playerRigidbody == null)
            return;

        Vector2 myPos = playerRigidbody.position;

        // Find nearby same-type units
        Collider2D[] hits = Physics2D.OverlapCircleAll(myPos, spacingCheckRadius, friendlyLayerMask);
        if (hits == null || hits.Length == 0)
            return;

        float totalXAdjustment = 0f;
        int count = 0;

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            if (hit.attachedRigidbody == playerRigidbody) continue;
            if (!hit.CompareTag(gameObject.tag)) continue;  // only same type (Player vs Player)

            Vector2 otherPos = hit.attachedRigidbody.position;
            float dx = myPos.x - otherPos.x;
            float absDx = Mathf.Abs(dx);

            // Only care if they are too close in X
            if (absDx >= minHorizontalSpacing)
                continue;

            // Also check they are roughly same row in Y (so we don't push guys far in front/back)
            float dy = Mathf.Abs(myPos.y - otherPos.y);
            if (dy > spacingCheckRadius)
                continue;

            // We’re too close; figure out which side to push to
            float pushDir = (dx >= 0f) ? 1f : -1f;  // if we are right of them, move more right; else more left
            float needed = minHorizontalSpacing - absDx; // how much distance we need to gain

            totalXAdjustment += pushDir * needed;
            count++;
        }

        if (count == 0 || Mathf.Approximately(totalXAdjustment, 0f))
            return;

        // Average adjustment and apply a bit of smoothing
        float avgAdjust = (totalXAdjustment / count) * 0.8f; // 0.8 = damp factor to avoid overshoot
        Vector2 targetPos = new Vector2(myPos.x + avgAdjust, myPos.y);

        playerRigidbody.MovePosition(Vector2.Lerp(myPos, targetPos, 0.5f));
    }


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

     

        ResolveHorizontalOverlap();

        //// Gentle horizontal unstick: Only for extreme overlaps, X-only nudge
        //if (separationRadius > 0f)  // Guard
        //{
        //    Collider2D[] tightOverlaps = Physics2D.OverlapCircleAll(transform.position, 0.05f, friendlyLayerMask);  // Tiny radius
        //    if (tightOverlaps.Length > 1)  // Heavily overlapped
        //    {
        //        // Pure X-separation (no forward bias, no Y)
        //        Vector2 xNudge = ApplyFriendlySeparation(Vector2.right);  // Horizontal arbitrary dir
        //        xNudge.y = 0f;  // Force zero Y
        //        playerRigidbody.MovePosition(playerRigidbody.position + xNudge * 0.02f);  // Tiny step (1/5th of original)
        //    }
        //}
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
