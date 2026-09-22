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
    // REMOVED 2026-09-16: "marchSpeedBoost = 1.3" and the MarchSpeed property.
    //
    // A HERO NOW MOVES AT ONE CONSTANT SPEED (Arash's directive). The only thing
    // that may change it is a full STOP - combat, attacking, the deploy lock.
    //
    // What the boost did: HandleRoamForward used MarchSpeed while HandleMoveToTarget
    // used CurrentMoveSpeed, so a hero travelled at 0.78 with no target and dropped
    // to 0.60 the instant it locked one. Killing an enemy therefore produced a
    // visible 23% slowdown as the hero turned to chase the next - reported by Arash
    // and confirmed in play.
    //
    // WHY IT MUST NOT COME BACK. It was added because the long march READ as slower
    // than the short approach, even though both were measured at a steady 0.6 - the
    // real cause being distance and the absence of course changes. Fixing a
    // perception problem with a real speed change produced three concrete faults:
    //   * a unit that changes speed on invisible state reads as broken;
    //   * moveSpeed is deliberately excluded from CP on the grounds that it does not
    //     decide a fight - only true while it is CONSTANT. A hero that closes 30%
    //     faster while untargeted reaches the next enemy sooner and takes less fire
    //     on the way, which CP cannot see;
    //   * enemies have no equivalent, so the hero was 20% faster than an enemy while
    //     chasing but 56% faster while roaming.
    // If the march ever feels sluggish again, shorten the lane or raise the authored
    // moveSpeed on BOTH sides - a visible number, not a hidden state multiplier.

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
        // One speed, with or without a target - see the note on the removed boost.
        intendedMoveSpeed = CurrentMoveSpeed;
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
    // REMOVED 2026-09-17: retargetHysteresis (0.2). It let a hero swap to an enemy
    // that was 20% nearer while it still had a living target, which the
    // unhandled-enemy rule now forbids outright - UpdateTargetSelection only picks
    // when it has nothing. The margin it provided is no longer needed either: the
    // jitter it was fighting came from re-picking every frame, and a sticky target
    // cannot jitter. Do not reintroduce it without reading rule 1 in
    // UpdateTargetSelection; with claim-aware ranking a distance re-pick makes two
    // heroes trade targets with each other indefinitely.

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

        // File this hero so other heroes can see which enemy it is handling.
        TargetClaimRegistry.Register(this);
    }
    private void OnDisable()
    {
        EnemyGateStats.OnGateDestroyed -= HandleGateDestroyed;

        // Hand the attack spot back so a later attacker can use it.
        AttackSlotRegistry.Release(this);
        attackSlotTarget = null;

        // Stop reserving whatever enemy this hero was on, so the next hero to
        // pick a target sees it as free again.
        TargetClaimRegistry.Unregister(this);
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

    /// <summary>
    /// Picks and keeps this hero's enemy. Runs every frame from Update.
    ///
    /// TWO RULES, in this order (Arash, 2026-09-17):
    ///
    /// 1. A LIVE TARGET IS NEVER ABANDONED. Selection only happens when the hero
    ///    has nothing, or when what it had has died. This replaced a
    ///    distance-hysteresis re-pick that could hand a hero a nearer enemy
    ///    mid-approach; with rule 2 below that re-pick became an oscillator,
    ///    because two heroes comparing the same pair of enemies would swap
    ///    targets with each other forever.
    ///
    /// 2. AN UNHANDLED ENEMY BEATS A CLOSER ONE. See TargetClaimRegistry - the
    ///    ranking is fewest-other-claimants, then nearest, then instance id.
    ///
    /// The scenario that produced the rule: hero A spawns, walks at the left
    /// enemy; hero B spawns on the same side and, being nearest-driven, walks at
    /// the SAME enemy, while the right-hand enemy reaches the player base
    /// unopposed. Under rule 2 hero B crosses to the right enemy even though it
    /// is much farther away, because nobody else is on it.
    /// </summary>
    public bool UpdateTargetSelection()
    {
        // =========================
        // Rule 0: locked heroes do not choose
        // =========================
        // A hero still standing on its deploy gate picks NOTHING, and drops
        // anything it was holding.
        //
        // THIS IS THE FIX FOR THE FIRST REPORTED FAILURE OF RULE 2 (2026-09-17).
        // Update() runs this every frame from the moment a hero is instantiated,
        // which is ~1.2s BEFORE PlayerWaveManager releases it - and
        // TargetClaimRegistry.IsHandling deliberately ignores locked heroes,
        // because a hero stranded on a gate must not reserve an enemy forever.
        // Put together, an entire wave sitting on the gates saw "nobody is on
        // anything", every hero of that wave picked the same nearest enemy, and
        // rule 1 then froze that choice for the rest of the battle. Two heroes
        // walked at one enemy while the other one strolled at the base - the
        // exact behaviour rule 2 was written to stop.
        //
        // Choosing at RELEASE instead makes the claims real: heroes released in
        // the same frame still run their Update one after another, and
        // OtherClaimants is counted live, so the second hero sees the first
        // hero's fresh claim and moves on to the free enemy.
        if (!isUnlocked)
        {
            currentTarget = null;
            hasEverDetectedEnemy = false;
            return false;
        }

        // =========================
        // Rule 1: stickiness, with ONE bounded exception
        // =========================
        // A live target is never given up for a nearer one. That is what makes a
        // claim stable enough for every OTHER hero to reason about: a claim that
        // can be dropped for a marginal distance gain is not information anyone
        // can act on.
        if (currentTarget != null && !currentTarget.enemyIsdead)
        {
            lastKnownTarget = currentTarget;
            TryRebalance();
            return true;
        }

        // =========================
        // Phase A: First detection
        // =========================
        // Radius-limited, so a hero still walking in does not lock onto something
        // on the far side of the field before it has seen anything at all.
        if (!hasEverDetectedEnemy)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                detectionRadius,
                LayerMask.GetMask("EnemyLayer")
            );

            EnemyStats best = null;
            int bestClaims = 0;
            float bestDistSq = 0f;

            foreach (var h in hits)
            {
                // GetComponentInParent fallback: an enemy's overlap collider is
                // not always on the object that carries EnemyStats. Plain
                // GetComponent silently dropped those enemies from the candidate
                // list, which made this pass pick a crowded enemy while a free
                // one stood two metres away. TargetDetectionForPlayer.IsCandidate
                // has always had the fallback; this one did not.
                EnemyStats es = h.GetComponent<EnemyStats>() ?? h.GetComponentInParent<EnemyStats>();
                if (es == null || es.enemyIsdead) continue;

                int claims = TargetClaimRegistry.OtherClaimants(es, this);
                float dsq = ((Vector2)(es.transform.position - transform.position)).sqrMagnitude;

                if (TargetClaimRegistry.Beats(es, claims, dsq, best, bestClaims, bestDistSq))
                {
                    best = es;
                    bestClaims = claims;
                    bestDistSq = dsq;
                }
            }

            if (best != null)
            {
                currentTarget = best;
                lastKnownTarget = best;
                hasEverDetectedEnemy = true;
                TargetClaimRegistry.LogPick(this, best, bestClaims, bestDistSq, "first detection");
                return true;
            }

            return false;
        }

        // =========================
        // Phase B: Persistent search
        // =========================
        // No radius here on purpose. Once a hero has seen the battle it may be
        // sent right across the field to cover an enemy nobody else is on - that
        // is the whole point of the rule, and in the reported case the free enemy
        // WAS the far one.
        EnemyStats chosen = RankLivingEnemies(out int chosenClaims, out float chosenDistSq);

        if (chosen != null)
        {
            currentTarget = chosen;
            lastKnownTarget = chosen;
            TargetClaimRegistry.LogPick(this, chosen, chosenClaims, chosenDistSq, "persistent search");
            return true;
        }

        // No enemies left
        currentTarget = null;
        lastKnownTarget = null;
        hasEverDetectedEnemy = false;
        return false;
    }

    /// <summary>
    /// Ranks every living enemy on the field for THIS hero and returns the best
    /// one, by TargetClaimRegistry.Beats. Shared by the persistent search and by
    /// the re-balance so they can never disagree.
    /// </summary>
    private EnemyStats RankLivingEnemies(out int bestClaims, out float bestDistSq)
    {
        EnemyStats best = null;
        bestClaims = 0;
        bestDistSq = 0f;

        EnemyStats[] allEnemies = FindObjectsOfType<EnemyStats>();
        for (int i = 0; i < allEnemies.Length; i++)
        {
            EnemyStats es = allEnemies[i];
            if (es == null || es.enemyIsdead) continue;

            int claims = TargetClaimRegistry.OtherClaimants(es, this);
            float dsq = ((Vector2)(es.transform.position - transform.position)).sqrMagnitude;

            if (TargetClaimRegistry.Beats(es, claims, dsq, best, bestClaims, bestDistSq))
            {
                best = es;
                bestClaims = claims;
                bestDistSq = dsq;
            }
        }

        return best;
    }

    /// <summary>How often a doubled-up hero may look for somewhere better to be.</summary>
    private const float RebalanceInterval = 0.5f;
    private float nextRebalanceTime;

    /// <summary>
    /// TRUE once this hero has actually ENGAGED its target - it is in melee, or
    /// swinging, or waiting out the recovery between swings. From this moment the
    /// fight belongs to that enemy and nothing may take the hero off it; only the
    /// enemy's death ends it (Arash, 2026-09-22).
    ///
    /// WHY ALL FOUR TERMS. "Am I attacking?" cannot be answered by the state alone,
    /// because a hero in a fight is only in AttackState for the frames it is
    /// actually starting a swing: PlayerAttackState sets isPerformingAction and
    /// PlayerCombatState pulls it back until currentRecoveryTimer expires. So a
    /// hero standing in range trading blows spends MOST of its time in
    /// PlayerCombatState, and a state-only test would read "not attacking" in
    /// exactly the gaps where the re-balance timer is most likely to fire.
    ///   * IsInAttackPosition() - the fight has started: same predicate the combat
    ///     and pursue states use to agree on "arrived", so this can never disagree
    ///     with them about whether the hero is engaged.
    ///   * currentState == AttackState - mid-swing.
    ///   * isPerformingAction / isInteracting - the swing animation is still
    ///     playing; leaving now would slide the hero away mid-animation.
    ///
    /// NOT included: attackPlayerGate. A gate engagement is judged on its own
    /// target (currentGateTarget) and must not freeze this hero's ENEMY choice.
    /// </summary>
    public bool IsEngagedWithTarget
    {
        get
        {
            if (currentTarget == null || currentTarget.enemyIsdead) return false;
            if (isPerformingAction || isInteracting) return true;
            if (currentState == AttackState) return true;
            return IsInAttackPosition();
        }
    }

    /// <summary>
    /// RULE 1b - the only way a hero ever leaves a living enemy.
    ///
    /// WHY IT EXISTS. Rule 2 only fires at the moment a hero picks, and that
    /// moment is often too early to be right. Measured on level 4 (2026-09-17,
    /// from the [TARGET] log): enemies arrive in TWO waves, so the heroes of the
    /// first deployment correctly doubled up on the only enemy that existed -
    /// there was nothing else to choose - and rule 1 then held them there after
    /// the second wave walked in unopposed. The rule was working; "decide once,
    /// at spawn" was simply not enough information.
    ///
    /// THE CONDITION, and why it cannot oscillate:
    ///   * It never runs for a hero that has ENGAGED (IsEngagedWithTarget). A
    ///     started fight is finished - see the engagement lock below.
    ///   * It only runs when this hero's target is SHARED (myOthers > 0). A hero
    ///     fighting alone never moves, so a fight is never abandoned on a whim.
    ///   * It never runs for the hero CLOSEST to that shared target
    ///     (TargetClaimRegistry.IsNearestClaimant) - a fight may only be handed
    ///     to somebody nearer, never to somebody further back.
    ///   * It only moves to an enemy with STRICTLY fewer other claimants.
    ///   * OtherClaimants excludes self and is counted LIVE, so the instant this
    ///     hero leaves, its old target reads one claimant fewer for everybody.
    /// Together those make it a strict descent: every switch lowers the crowding
    /// of the hero that moves, and the hero left behind is now alone, so IT will
    /// not move. Two heroes cannot trade places - the second one re-reads the
    /// board after the first has already moved and finds nothing to improve.
    ///
    /// Worked example, 3 heroes on E1 and 1 on E2: the first to evaluate sees
    /// myOthers=2, finds E2 at 1, moves. The next sees myOthers=1 and E2 at 2 -
    /// no strict improvement, stays. Settles at 2/2 and stops.
    ///
    /// Throttled to RebalanceInterval because the scan behind it is a full-scene
    /// FindObjectsOfType. It runs only for heroes that are actually doubled up,
    /// and at most twice a second each.
    /// </summary>
    private void TryRebalance()
    {
        if (Time.time < nextRebalanceTime) return;
        nextRebalanceTime = Time.time + RebalanceInterval;

        // =========================
        // ENGAGEMENT LOCK (Arash, 2026-09-22) - re-balance is an APPROACH-time
        // decision only.
        // =========================
        // Reported: a hero already in melee walked away from the enemy it was
        // hitting the moment a new enemy spawned. That is this method - a fresh
        // enemy enters with ZERO claimants, so any hero whose target happens to be
        // shared sees a strict improvement and leaves, mid-fight, with the enemy
        // it was beating on still alive and now free to hit its back.
        //
        // The rule this restores: a fight, once JOINED, is finished. Re-balancing
        // is still exactly as free as it was for heroes that are walking - which
        // is the case rule 1b was actually written for (heroes of deployment 1
        // doubled up while wave 2 walked in unopposed; they were marching, not
        // fighting). So this keeps the spread-out behaviour and removes only the
        // half of it that abandons a live engagement.
        //
        // It cannot deadlock: the lock lasts only while the target lives, and the
        // target dies. On its death rule 1 falls through to the persistent search
        // and the hero re-picks with full claim-aware ranking - including the new
        // enemy that triggered this in the first place.
        if (IsEngagedWithTarget) return;

        // Alone on this enemy: nothing to fix, and this is also the guard that
        // makes the whole thing terminate.
        int myOthers = TargetClaimRegistry.OtherClaimants(currentTarget, this);
        if (myOthers <= 0) return;

        // =========================
        // NEAREST-CLAIMANT LOCK (Arash, 2026-09-22) - only leave a fight to
        // somebody CLOSER than you.
        // =========================
        // Reported after the engagement lock above went in, and it is the same
        // bug one step earlier: the hero that turns around had not reached melee
        // yet, so nothing held it. A hero walking at an enemy it is about to
        // reach would abandon it for a brand-new spawn on the far side of the
        // field, handing the enemy to an ally that was standing further back.
        //
        // Spreading out is the POINT of this method and Arash asked for it - but
        // it is only spread if the enemy left behind ends up covered at least as
        // well. Handing it to a hero further away is a straight downgrade: the
        // enemy gets free time while the new owner walks in, and the hero that
        // left spends that time walking too. Nobody gains.
        //
        // Exactly one hero per target passes this, so a doubled-up group still
        // splits - the nearest one keeps the fight and everybody behind it is
        // free to go cover the new wave, which is the distribution that was
        // wanted in the first place.
        if (TargetClaimRegistry.IsNearestClaimant(currentTarget, this)) return;

        EnemyStats best = RankLivingEnemies(out int bestClaims, out float bestDistSq);
        if (best == null || best == currentTarget) return;

        // STRICT improvement only. ">=" here would let two heroes swap targets
        // with each other forever, which is the bug SESSIONS.md has registered
        // five separate times in this codebase.
        if (bestClaims >= myOthers) return;

        currentTarget = best;
        lastKnownTarget = best;
        TargetClaimRegistry.LogPick(this, best, bestClaims, bestDistSq,
                                    $"re-balance, left an enemy shared with {myOthers}");
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
    /// True in melee range, including close contact. Missed weapon contact is
    /// handled by recovery, never by pushing a unit out of attack range.
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
    /// Uses the same ally path steering as pursuit, without position corrections.
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


        }

        // THE SAME CurrentMoveSpeed as HandleMoveToTarget. This line used to read
        // MarchSpeed, which was 1.3x faster, so a hero sped up the moment its target
        // died and slowed down again as soon as it locked the next one.
        playerRigidbody.linearVelocity = dir * CurrentMoveSpeed;
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

    // Each attacker walks to its OWN spot on an arc around the target instead of
    // everyone converging on one point. The slot is claimed once and kept while
    // the target does not change, so nobody drifts mid-fight - and no unit ever
    // pushes another to make room.
    Vector2 anchor = chosenEnemyOffset != null
        ? (Vector2)chosenEnemyOffset.position
        : (Vector2)transform.position;

    Vector2 destination = ResolveAttackDestination(anchor);

    Vector2 toTarget = destination - playerRigidbody.position;
    float dist = toTarget.magnitude;

    if (dist < 0.05f)
    {
        // Arrived at our own attack spot: stop dead and stay there. No shuffling,
        // no reacting to whoever else is standing nearby.
        playerRigidbody.linearVelocity = Vector2.zero;
        return;
    }

    Vector2 dir = toTarget.normalized;

    // Route around allies without adding any backwards position correction.
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
