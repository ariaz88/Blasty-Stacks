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
    [Tooltip("FALLBACK ONLY. The real walking speed comes from unitStats.moveSpeed " +
             "(see CurrentMoveSpeed). This value is used only while the stat block " +
             "has not been built yet, or if a prefab has no UnitStatsSO at all.")]
    public float moveSpeed = 0.5F;

    /// <summary>
    /// The speed this hero actually walks at.
    ///
    /// Reads the LIVE stat block, so upgrades and roguelite buffs genuinely move
    /// the unit. Before B1 this Inspector field was the silent authority and
    /// unitStats.moveSpeed was never read at all, which is why the stat sheet
    /// said 3.5 while heroes walked at 0.5.
    ///
    /// Falls back to the serialized field only when the stat block is missing or
    /// not built yet, so a mis-configured prefab still moves instead of freezing.
    /// </summary>
    public float CurrentMoveSpeed =>
        (unitStats != null && unitStats.initialized && unitStats.moveSpeed > 0f)
            ? unitStats.moveSpeed
            : moveSpeed;

    /// <summary>
    /// Seconds this hero must wait between swings, given an attack's authored
    /// recoveryTime. Faster attackSpeed = shorter gap = more hits per second.
    ///
    /// Before B2, cadence was the raw recoveryTime (a flat 0.6 on all ten attack
    /// assets) and attackSpeed only set animator playback speed - so the stat did
    /// nothing at all to damage output. Dividing here is what makes it real, and
    /// it keeps animation and cadence in sync because the animator is scaled by
    /// the same attackSpeed.
    ///
    /// attackSpeed is floored at 0.05 so a zero or negative stat cannot produce an
    /// infinite or negative cooldown (which would let a unit attack every frame).
    /// </summary>
    public float AttackCadence(float recoveryTime)
    {
        float atkSpd = (unitStats != null && unitStats.initialized) ? unitStats.attackSpeed : 1f;
        return recoveryTime / Mathf.Max(0.05f, atkSpd);
    }
    public bool canMove = false;
    public bool isUnlocked = false;
    private float damageAppling;

    [Header("TEMPORARY - march speed boost")]
    [Tooltip("TEMPORARY WORKAROUND, NOT A DESIGN RULE. Multiplies walking speed " +
             "ONLY after every enemy is dead - the march on the enemy base. Set to " +
             "1 to switch it off entirely. " +
             "WHY IT IS HERE: the march was reported as feeling slower than the " +
             "pre-battle approach. It is NOT - both were measured at a steady 0.6, " +
             "with no damping anywhere and linearDamping 0 on every prefab. The " +
             "difference is distance and the total absence of course changes: the " +
             "approach covers 1-3 units with visible micro-corrections, the march " +
             "covers the whole lane in a dead-straight line. This boost papers over " +
             "that until the real cause of the PERCEPTION is understood. " +
             "Safe to leave on meanwhile: moveSpeed is not a CP input, and by the " +
             "time it applies there is no enemy left for it to affect.")]
    [SerializeField, Range(1f, 3f)] private float marchSpeedBoost = 1.3f;

    /// <summary>
    /// Speed for the march on the enemy base, once the field is clear.
    /// See <see cref="marchSpeedBoost"/> - this is a temporary measure.
    /// </summary>
    public float MarchSpeed => CurrentMoveSpeed * Mathf.Max(1f, marchSpeedBoost);

    [Header("Live speed readout (debug, read-only)")]
    [Tooltip("What this hero is ACTUALLY travelling at right now, measured from how " +
             "far it really moved last physics step. Compare it with Intended below.")]
    public float actualMoveSpeed;

    [Tooltip("What it is TRYING to travel at - unitStats.moveSpeed. If Actual matches " +
             "this but the unit still looks slow, it is not losing speed: ally " +
             "avoidance is bending its DIRECTION, so it covers less ground toward " +
             "where you are watching. See forwardProgress.")]
    public float intendedMoveSpeed;

    [Tooltip("How fast it is closing on the thing it is heading for - its target, or " +
             "straight up the lane when marching on the enemy base. This is the one " +
             "that drops when a hero is weaving around its allies.")]
    public float forwardProgress;

    Vector2 speedProbeLastPos;
    bool speedProbeStarted;

    /// <summary>
    /// Measures real movement instead of trusting the velocity we asked for.
    ///
    /// Runs BEFORE every early return in FixedUpdate, so it keeps reading during a
    /// MeleeContactRecovery sidestep too - that one drives the body with
    /// MovePosition, where linearVelocity stays near zero and would lie.
    /// </summary>
    private void MeasureSpeed()
    {
        Vector2 now = playerRigidbody ? playerRigidbody.position : (Vector2)transform.position;

        if (speedProbeStarted && Time.fixedDeltaTime > 0f)
        {
            Vector2 step = now - speedProbeLastPos;
            actualMoveSpeed = step.magnitude / Time.fixedDeltaTime;

            Vector2 heading = currentTarget
                ? ((Vector2)currentTarget.transform.position - now)
                : (Vector2)transform.up;
            forwardProgress = heading.sqrMagnitude > 0.000001f
                ? Vector2.Dot(step, heading.normalized) / Time.fixedDeltaTime
                : 0f;
        }

        // Report what this hero is CURRENTLY asking for, so actual-vs-intended stays
        // a valid comparison while the march boost is applied.
        intendedMoveSpeed = currentTarget ? CurrentMoveSpeed : MarchSpeed;
        speedProbeLastPos = now;
        speedProbeStarted = true;
    }

    /// <summary>
    /// TRUE while FormationGapFiller is walking this hero into a gap during the
    /// PRE-BATTLE phase. The hero is still in PlayerLockState at that point, and
    /// PlayerLockState.Tick forces the animator back to idle every FixedUpdate -
    /// which silently ate the walk cycle the gap filler had just started. While
    /// this flag is set, PlayerLockState leaves locomotion and the animator alone
    /// and lets the gap filler drive them.
    /// </summary>
    [HideInInspector] public bool isFormationStepping = false;

    [Header("Facing/Targeting")]
    public bool faceCenterOnStart = true;

    [Header("Anti-Jitter (hysteresis)")]
    [Tooltip("A NEW enemy must be this much closer, as a fraction, before we switch " +
             "target. 0.2 = must be 20% closer. Without a margin, two enemies at " +
             "almost equal distance make the unit swap target every single frame.")]
    [SerializeField, Range(0f, 0.9f)] private float retargetHysteresis = 0.2f;

    [Tooltip("The OTHER side of a target must be this much closer before we walk " +
             "around to it. Switching sides moves the destination ACROSS the target, " +
             "which reverses both movement and facing - the main cause of the " +
             "left-right spinning.")]
    [SerializeField, Range(0f, 0.9f)] private float anchorSwitchHysteresis = 0.3f;

    [Tooltip("How far the target must be off-centre in X before the sprite flips. " +
             "Larger = calmer facing.")]
    [SerializeField, Min(0f)] private float facingDeadZoneX = 0.12f;

    // The target our current anchor belongs to, so a target change forces a
    // fresh side choice instead of keeping a stale one.
    private Object anchorOwner;

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

    /// <summary>
    /// The unit type this hero was counted as in <see cref="HeroRoster"/>, or -1
    /// for anything that is not a countable hero (the player castle, mainly).
    /// Cached at registration because PlayerStatsApplier.unitId can be rewritten
    /// later and we must unregister from the SAME bucket we registered into.
    /// </summary>
    private int rosterUnitId = -1;

    /// <summary>
    /// Adds this hero to the per-type tally the battle HUD reads.
    ///
    /// Deliberately in Start, not Awake/OnEnable: PlayerWaveManager calls
    /// SetUnitId() on the applier straight after Instantiate, which is AFTER
    /// Awake has already run - registering any earlier would file every hero
    /// under the prefab's default unitId.
    /// </summary>
    private void RegisterInHeroRoster()
    {
        // The player castle also carries a PlayerManager; it is not a hero.
        if (CompareTag("PlayerGate")) return;
        if (playerStatsApplier == null) return;

        rosterUnitId = playerStatsApplier.unitId;
        HeroRoster.Register(rosterUnitId, this);
    }

    private void OnDestroy()
    {
        if (rosterUnitId >= 0)
            HeroRoster.Unregister(rosterUnitId, this);
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

        EnsureDepthSorter();
    }

    /// <summary>
    /// Gives this unit a Y-driven draw depth, so a hero standing in front of an
    /// enemy is actually PAINTED in front of it. Every character part in the game
    /// sits on sorting layer Default at order 1, so without this the tie between
    /// two overlapping units is resolved arbitrarily and an enemy's head can end up
    /// on top of the hero it is fighting.
    ///
    /// Added in code rather than on the prefabs deliberately: heroes are spawned
    /// from several places (PlayerWaveManager, the debug stage tools, the test
    /// scenes) and a component every unit must have is more reliably guaranteed
    /// here than re-authored into each prefab by hand.
    ///
    /// Skipped for the castle, which carries a PlayerManager but is not a unit.
    /// </summary>
    private void EnsureDepthSorter()
    {
        if (CompareTag("PlayerGate")) return;
        if (GetComponent<UnitDepthSorter>() == null)
            gameObject.AddComponent<UnitDepthSorter>();
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

        RegisterInHeroRoster();
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
    /// <summary>
    /// Picks which side of the target to approach, but STICKS to the current side
    /// unless the other one is clearly better.
    ///
    /// GetOffsetFacingPlayer just returns whichever side is nearer, with no margin.
    /// Standing roughly level with a target makes the two sides near-identical in
    /// distance, so it alternated every frame - and because the two anchors sit on
    /// OPPOSITE sides, the destination jumped across the target each time. The unit
    /// then reversed direction and flipped its sprite, over and over. That is the
    /// left-right spinning.
    /// </summary>
    private Transform PickAttackAnchor(Vector2 fromPos)
    {
        if (currentTarget == null) return chosenEnemyOffset;

        Transform candidate = currentTarget.GetOffsetFacingPlayer(fromPos);

        // A different target means the old anchor is meaningless - take the new one.
        if (chosenEnemyOffset == null || (Object)currentTarget != anchorOwner)
        {
            anchorOwner = currentTarget;
            return candidate;
        }

        if (candidate == chosenEnemyOffset) return chosenEnemyOffset;

        float dNew = ((Vector2)candidate.position - fromPos).sqrMagnitude;
        float dCur = ((Vector2)chosenEnemyOffset.position - fromPos).sqrMagnitude;

        // Compare in squared space, so square the linear margin too.
        float margin = 1f - anchorSwitchHysteresis;
        return dNew < dCur * margin * margin ? candidate : chosenEnemyOffset;
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
        if (Mathf.Abs(dx) < facingDeadZoneX)
            return;

        bool enemyIsOnLeft = dx < 0f;

        FaceLeft(enemyIsOnLeft);

        // Sticky side choice - see PickAttackAnchor.
        chosenEnemyOffset = PickAttackAnchor(playerPos);
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
        if (currentTarget != null)
        {
            // Sticky, not "whichever is nearest right now". FaceLeft runs every
            // frame, so an unfiltered re-pick here re-introduced the side flapping
            // even after UpdateFacingAndOffset was made sticky.
            chosenEnemyOffset = PickAttackAnchor(transform.position);
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

                // A new enemy must be CLEARLY closer before we switch. Comparing
                // raw distances meant two enemies at almost equal range swapped the
                // target every frame, which flipped the approach side and the
                // sprite with it. Dead current targets are handled above, so this
                // only ever delays a marginal upgrade.
                float retargetMargin = 1f - retargetHysteresis;

                if (currentTarget.enemyIsdead ||
                    nearestDistSqr < currentDistSqr * retargetMargin * retargetMargin)
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

    // NOTE: the serialized "attackSlotAngleStep" / "attackStandoffFactor" fields
    // were REMOVED when attack spacing moved into MeleeEngagement. The arc they
    // tuned fanned attackers AROUND the target, which let a hero come to rest
    // directly above or below it - inside range and unable to land a hit, because
    // the weapon hitboxes only reach sideways. Spacing is now defined once, for
    // both factions, in MeleeEngagement.

    // The target we currently hold an attack slot on, and which slot it is.
    private Object attackSlotTarget;
    private int attackSlotIndex;

    // Side of the target this hero committed to, +1/-1 (0 = not chosen yet).
    // Held so crossing the target's centre line does not reverse the approach.
    private float heldAttackSide;

    /// <summary>
    /// TRUE when this hero is standing somewhere its weapon can actually land on
    /// its current enemy target - beside it, not stacked above or below it.
    ///
    /// The states used to decide this with a bare radial distance, which cannot
    /// tell "beside" from "on top of". See MeleeEngagement for why that matters:
    /// the melee hitboxes are wide, flat boxes that reach sideways only.
    /// </summary>
    public bool IsInAttackPosition()
    {
        if (currentTarget == null) return false;

        Vector2 self = playerRigidbody ? playerRigidbody.position : (Vector2)transform.position;
        return MeleeEngagement.InAttackPosition(self, currentTarget.transform.position, maxAttackRange);
    }

    /// <summary>
    /// This unit's own attack spot: BESIDE the target, at standoff distance, inside
    /// the vertical band where its weapon can reach. Multiple attackers on one
    /// target take different slots and never push each other.
    ///
    /// CRITICAL INVARIANT - do not break this again:
    /// the returned point must ALWAYS satisfy MeleeEngagement.InAttackPosition.
    /// The pursue state decides pursue-vs-combat with that predicate while this
    /// method decides where the mover walks. If the two can disagree, the mover
    /// reports "arrived" and stops, the state reports "not there yet" and stays in
    /// pursue, and the unit freezes on the spot. That is exactly what happened when
    /// slots were a flat sideways offset - a +1.80 slot put a hero 2.45 away from an
    /// enemy with a range of 0.85. MeleeEngagement.StandPoint keeps the point on the
    /// standoff circle and inside the band precisely so this cannot recur.
    ///
    /// <paramref name="anchor"/> is the side marker the targeting code picked
    /// (EnemyOffsetLeft/Right). It is only a hint now: the side is re-derived from
    /// the hero's own position so that the ENEMY walking toward this hero arrives at
    /// a compatible spot instead of the two circling each other.
    /// </summary>
    private Vector2 ResolveAttackDestination(Vector2 anchor)
    {
        Object target = currentTarget != null ? (Object)currentTarget : (Object)currentGateTarget;

        if (target == null)
        {
            if (attackSlotTarget != null)
            {
                AttackSlotRegistry.Release(this);
                attackSlotTarget = null;
                heldAttackSide = 0f;
            }
            return anchor;
        }

        if (target != attackSlotTarget)
        {
            AttackSlotRegistry.Release(this);
            attackSlotIndex = AttackSlotRegistry.Claim(target, this);
            attackSlotTarget = target;
            heldAttackSide = 0f;   // a new target means a fresh side decision
        }

        Transform targetTf = currentTarget != null ? currentTarget.transform
                                                  : (currentGateTarget != null ? currentGateTarget.transform : null);
        if (targetTf == null) return anchor;

        // The gate is a wide building, not a duellist: walking to one of its flanks
        // would march heroes off the side of it. Keep the old head-on approach.
        if (currentTarget == null)
            return anchor;

        Vector2 targetPos = targetTf.position;
        Vector2 self = playerRigidbody ? playerRigidbody.position : (Vector2)transform.position;

        heldAttackSide = MeleeEngagement.ChooseSide(
            self, targetPos, MeleeEngagement.PlayerPreferredSide, heldAttackSide);

        return MeleeEngagement.StandPoint(targetPos, maxAttackRange, heldAttackSide, attackSlotIndex);
    }

    /// <summary>
    /// Walk straight ahead when there is no enemy to chase - i.e. the march on the
    /// enemy gate once the field is clear.
    ///
    /// This exists because PlayerPursueTargetState used to drive that march with a
    /// raw "linearVelocity = transform.up * moveSpeed" written inline. That path
    /// never went through HandleMoveToTarget, so it skipped ally avoidance AND
    /// personal space entirely - which is why heroes arriving at the gate walked
    /// straight into the ranks already hitting it, even though the very same
    /// avoidance worked fine before the battle.
    /// </summary>
    public void HandleRoamForward()
    {
        if (playerRigidbody == null) return;

        canMove = true;
        playerRigidbody.bodyType = RigidbodyType2D.Dynamic;

        Vector2 dir = transform.up;   // rotation is identity, so this is world up

        if (CrowdSeparation2D.Instance != null)
        {
            // Arc around allies already parked at the gate...
            dir = CrowdSeparation2D.Instance.SteerAroundBlockers(transform, dir);

            // ...and keep a little personal space if we still end up on top of one.
            transform.position += (Vector3)CrowdSeparation2D.Instance.ResolveOverlap(transform);
        }

        // MarchSpeed, not CurrentMoveSpeed: the temporary boost above applies only
        // here, on the walk to the enemy base with no enemies left alive.
        playerRigidbody.linearVelocity = dir * MarchSpeed;
        SetAnimMoving(true);
    }

    public void HandleMoveToTarget(bool canMove)
{
    if (!canMove)
        return;

    if (playerRigidbody == null)
        return;

    // An ENEMY target needs no anchor: the stand point is derived from the two
    // positions. Only the gate march still needs one.
    //
    // Bailing out on a null anchor used to be unconditional, and that was a real
    // freeze: UpdateFacingAndOffset only assigns chosenEnemyOffset once the target
    // is at least facingDeadZoneX off-centre, so a hero that acquired a target
    // sitting DEAD LEVEL above it never got an anchor at all - and then never moved
    // a step. Which is exactly the situation this whole fix is about.
    if (currentTarget == null && chosenEnemyOffset == null)
        return;

    // Keep a little personal space while closing in, so two heroes converging on
    // the same enemy do not sink into each other before they reach attack range.
    // This runs ONLY here, and HandleMoveToTarget is only ever called from the
    // pursue state - so the instant a hero stops to attack it is left alone
    // again, exactly as the zero-interaction rule requires.
    if (CrowdSeparation2D.Instance != null)
        transform.position += (Vector3)CrowdSeparation2D.Instance.ResolveOverlap(transform);

    // Each attacker walks to its OWN spot on an arc around the target instead of
    // everyone converging on one point. The slot is claimed once and kept while
    // the target does not change, so nobody drifts mid-fight - and no unit ever
    // pushes another to make room.
    Vector2 anchor = chosenEnemyOffset != null
        ? (Vector2)chosenEnemyOffset.position
        : (Vector2)transform.position;

    Vector2 destination = ResolveAttackDestination(anchor);

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

    // Walk AROUND an ally standing in the way, at ALL times - mid-battle too.
    //
    // This used to be restricted to the walk-up to the gate, which is why a hero
    // stuck directly behind another mid-fight could never get past: it pushed
    // straight into its ally's back, the personal-space correction pushed it
    // straight back, the two cancelled, and it played its walk animation on the
    // spot. Letting it route around is the whole fix.
    //
    // Note this can NEVER make a hero circle its own kill: SteerAroundBlockers
    // only considers units on the SAME layer, so enemies are never avoided.
    if (CrowdSeparation2D.Instance != null)
        dir = CrowdSeparation2D.Instance.SteerAroundBlockers(transform, dir);

    // Velocity, not MovePosition, so there is no tunnelling.
    //
    // NO DAMPING. There used to be a "linearVelocity *= 0.7f when inside
    // maxAttackRange" here, meant to give a cleaner stop. It made a hero visibly
    // decelerate on its final approach - at the authored 0.6 that is 0.42 - which
    // was reported as "the hero slows down". A hero moves at its stat speed at
    // every moment; the stop is already clean because the mover halts within 5cm
    // of its stand point (see the arrival check above).
    playerRigidbody.linearVelocity = dir * CurrentMoveSpeed;
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
        MeasureSpeed();   // before every early return, so the readout never stalls
        if (GetComponent<MeleeContactRecovery>() is { IsRepositioning: true }) return;
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
