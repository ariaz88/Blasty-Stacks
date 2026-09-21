using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]

public class EnemyLocoMotion : MonoBehaviour
{
    EnemyManager enemyManager;

    [Header("Refs")]
    public Rigidbody2D enemyRigidbody2D;    // 2D body

    [HideInInspector] public PlayerStats currentTarget;
    public float distanceFromTarget;

    [Header("Detection (visual only, logic in EnemyManager)")]
    public LayerMask playerDetectionLayer;
    public float detectionRadius = 6f;

    [Header("Movement")]
    [Tooltip("FALLBACK ONLY. The real walking speed comes from " +
             "enemyManager.unitStats.moveSpeed (see CurrentMoveSpeed). This value is " +
             "used only while the stat block has not been built yet.")]
    public float moveSpeed = 1.5f;          // units/sec
    public float stoppingDistance = 0.5f;

    /// <summary>
    /// The speed this enemy actually walks at.
    ///
    /// Reads the LIVE stat block, so per-stage growth genuinely moves the unit.
    /// Before B1 this Inspector field was the silent authority and
    /// unitStats.moveSpeed was never read, which is why the stat sheet said
    /// 2.9-3.5 while enemies crawled at the prefab's 0.2.
    ///
    /// Falls back to the serialized field only when the stat block is missing or
    /// not built yet, so a mis-configured prefab still moves.
    /// </summary>
    public float CurrentMoveSpeed
    {
        get
        {
            var s = enemyManager != null ? enemyManager.unitStats : null;
            return (s != null && s.initialized && s.moveSpeed > 0f) ? s.moveSpeed : moveSpeed;
        }
    }


    // NEW: pursue player only when close enough and only if player is "opposite side"
    public float fairDistanceToPlayer = 1.6f;

    Animator anim;
    MeleeContactRecovery contactRecovery;

    // Side of the target this enemy committed to, +1/-1 (0 = not chosen yet), and
    // the target that choice belongs to. Held so crossing the hero's centre line
    // does not reverse the approach halfway.
    float heldAttackSide;
    Object attackSlotTarget;
    int attackSlotIndex;

    void Awake()
    {
        enemyManager = GetComponent<EnemyManager>();
        if (!enemyRigidbody2D) enemyRigidbody2D = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
        contactRecovery = GetComponent<MeleeContactRecovery>();
    }

    /// <summary>
    /// Called by MeleeContactRecovery from its own Awake.
    ///
    /// Needed because the recovery is ADDED AT RUNTIME by CPBattleController, long
    /// after this Awake has run, so the GetComponent above finds nothing on a
    /// stage 1-5 enemy. Without this hand-off the yield check below would be
    /// permanently false and the sidestep would keep being overridden.
    /// </summary>
    public void BindContactRecovery(MeleeContactRecovery recovery)
    {
        contactRecovery = recovery;
    }

    void OnDisable()
    {
        // Hand the attack spot back so another enemy can use it.
        AttackSlotRegistry.Release(this);
        attackSlotTarget = null;
    }

    /// <summary>
    /// TRUE while MeleeContactRecovery is physically stepping this enemy sideways.
    ///
    /// THIS CHECK IS THE FIX FOR THE "ENEMY SLIDES INTO THE HERO" BUG.
    /// EnemyManager.FixedUpdate and PlayerManager.FixedUpdate both already stand
    /// down while the recovery owns the body - but enemy MOVEMENT does not live in
    /// EnemyManager, it lives here. So the recovery was calling MovePosition toward
    /// a spot beside the hero in FixedUpdate while this script called MovePosition
    /// straight AT the hero in the same frame. Update runs after FixedUpdate, so
    /// this script won the race every time: the enemy ignored the sidestep, glided
    /// into the hero, and the two sprites ended up on top of each other. Heroes
    /// never showed it because their mover is inside the FixedUpdate that yields.
    /// </summary>
    bool RecoveryOwnsBody => contactRecovery != null && contactRecovery.IsRepositioning;

    void Start()
    {
        enemyRigidbody2D.gravityScale = 0f;
        enemyRigidbody2D.freezeRotation = true;
        enemyRigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    // NOTE: the dead numbered sibling "Update1" was DELETED on 2026-09-11. Unity
    // never called it (Update1 is not a message), it still drove movement from the
    // frame clock, and leaving it there invited someone to "fix" the wrong copy.

    /// <summary>
    /// Update keeps the DISTANCE readout current (EnemyManager reads it every frame
    /// to decide whether to swing) and nothing else.
    ///
    /// Movement used to live here too, and that was a real defect, not a style
    /// nit: MovePosition is a physics call, and driving it from Update with
    /// Time.deltaTime made the step size depend on the frame rate. Each physics
    /// step moved the body by whatever the LAST Update wrote, so the enemy
    /// travelled speed * (deltaTime * 50) per second - about 83% of its stat speed
    /// at 60fps and about 167% of it, in steps twice as long, whenever the frame
    /// rate dipped to 30. Those oversized steps are the forward "skating" that was
    /// reported. See FixedUpdate.
    /// </summary>
    void Update()
    {
        if (GameplayPause.IsPaused)
        {
            SetAnimMoving(false);
            return;
        }

        if (currentTarget != null)
        {
            distanceFromTarget = Vector2.Distance(currentTarget.transform.position, transform.position);
        }
        else
        {
            distanceFromTarget = 20f;
        }
    }

    /// <summary>
    /// All movement, on the physics clock, in fixed-size steps - so an enemy walks
    /// at exactly its unitStats.moveSpeed no matter what the frame rate is doing.
    /// </summary>
    void FixedUpdate()
    {
        if (GameplayPause.IsPaused)
        {
            SetAnimMoving(false);
            return;
        }

        // The sidestep recovery is driving the body this frame - stay out of its way.
        if (RecoveryOwnsBody) return;

        HandleMoveToTarget();
    }


    // ---------- LOCOMOTION (2D) ----------
    // NOTE: the dead numbered sibling "HandleMoveToTarget1" was DELETED on
    // 2026-09-11. It had no call sites, drove the body from the frame clock,
    // and carried its own separate (also radial) stopping rule - a second place
    // to get the enemy approach wrong.

    public void HandleMoveToTarget()
    {
        if (!enemyRigidbody2D) return;

        Vector2 pos = enemyRigidbody2D.position;

        // A committed hero takes priority over the gate, regardless of distance.
        if (enemyManager != null && enemyManager.reachedGate)
        {
            bool hasHeroTarget =
                currentTarget != null &&
                !currentTarget.playerIsdead && currentTarget.gameObject.activeInHierarchy;

            if (!hasHeroTarget)
            {
                Vector2 stop = enemyManager.gateStopPosition;

                // WALK back, never snap: an enemy that just chased a hero a metre
                // off the line would otherwise teleport back onto it.
                if (Vector2.Distance(pos, stop) > 0.05f)
                {
                    enemyRigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                    enemyRigidbody2D.MovePosition(
                        Vector2.MoveTowards(pos, stop, CurrentMoveSpeed * Time.fixedDeltaTime));
                    SetAnimMoving(true);
                }
                else
                {
                    enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                    enemyRigidbody2D.position = stop;
                    SetAnimMoving(false);
                }

                return;
            }

            // A hero IS in reach. AT THE BASE THE ENEMY DOES NOT CHASE (Arash,
            // 2026-09-21): it has arrived, so it holds its spot, turns to the hero
            // and fights from there. Previously this fell through to the pursuit
            // branch, which walked the enemy off the gate line after any hero that
            // came near - including one standing behind it.
            //
            // Facing is handled by EnemyManager.UpdateFacing (a sprite flip), and the
            // attack controller hits from here when the hero is in reach, so holding
            // position costs nothing but the chase.
            Vector2 hold = enemyManager.gateStopPosition;
            if (Vector2.Distance(pos, hold) > 0.05f)
            {
                enemyRigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                enemyRigidbody2D.MovePosition(
                    Vector2.MoveTowards(pos, hold, CurrentMoveSpeed * Time.fixedDeltaTime));
                SetAnimMoving(true);
            }
            else
            {
                StopAtCurrentPosition();
            }
            return;
        }

        // 2) Pursue the locked hero until it is in attack range.

        if (currentTarget != null && !currentTarget.playerIsdead)
        {
            Vector2 targetPos = currentTarget.transform.position;

            // Use exactly the same eligibility rule as the attack controller.
            if (IsInAttackPosition())
            {
                StopAtCurrentPosition();
                return;
            }

            // Acquisition already validated this hero. Pursue the lock even
            // outside the original acquisition radius, including at the gate.
            Vector2 stand = ResolveStandPoint(targetPos);
            Vector2 next = Vector2.MoveTowards(pos, stand, CurrentMoveSpeed * Time.fixedDeltaTime);

            // NO BACKTRACKING (Arash, 2026-09-21). An enemy advances on the base and
            // may step sideways - or forward - onto a hero, but it must never reverse
            // to chase one that has passed it. Rather than cancelling the whole step,
            // only the BACKWARD COMPONENT is removed: the lateral part survives, so
            // the enemy still slides across to a hero beside it, and a hero directly
            // behind simply produces no movement at all.
            next = StripBackwardMotion(pos, next);

            if ((next - pos).sqrMagnitude <= 1e-8f)
            {
                // Nothing left to walk - hold position and let the attack logic face
                // and hit the hero from here if it is in reach.
                StopAtCurrentPosition();
                return;
            }

            enemyRigidbody2D.bodyType = RigidbodyType2D.Dynamic;
            enemyRigidbody2D.MovePosition(next);
            SetAnimMoving(true);
            return;
        }

        // 3) No player target -> march straight toward the gate (lane-based)
        if (enemyManager != null &&
            enemyManager.currentGateTarget != null &&
            !enemyManager.currentGateTarget.isPlayerGateDestroyed)
        {
            // Distance-based fallback: if we are close enough to the gate, treat as "reached"
            float gateDist = Vector2.Distance(
                enemyManager.currentGateTarget.transform.position,
                transform.position
            );

            if (!enemyManager.reachedGate && gateDist <= enemyManager.gateStopDistance)
            {
                enemyManager.reachedGate = true;
                enemyManager.gateStopPosition = enemyRigidbody2D.position;

                enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                SetAnimMoving(false);
                return;
            }

            // Otherwise keep marching straight forward along the lane
            if (!enemyManager.reachedGate)
            {
                Vector2 dir = -(Vector2)transform.up;
                Vector2 next = pos + dir * CurrentMoveSpeed * Time.fixedDeltaTime;

                enemyRigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                enemyRigidbody2D.MovePosition(next);
                SetAnimMoving(true);
                return;
            }

            // If reachedGate is already true, clamp to stop position
            enemyRigidbody2D.position = enemyManager.gateStopPosition;
            enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            SetAnimMoving(false);
            return;
        }

    }

    /// <summary>
    /// The same step with any motion AGAINST the march direction removed, so an
    /// enemy can advance and strafe but never reverse. Returns the step unchanged
    /// when there is no manager to ask for a march direction.
    /// </summary>
    Vector2 StripBackwardMotion(Vector2 from, Vector2 to)
    {
        if (enemyManager == null) return to;

        Vector2 march = enemyManager.MarchDirection;
        if (march.sqrMagnitude < 1e-6f) return to;
        march.Normalize();

        Vector2 delta = to - from;
        float along = Vector2.Dot(delta, march);

        // Negative = moving back the way we came. Project that part out and keep
        // the perpendicular (sideways) part.
        if (along < 0f) delta -= march * along;

        return from + delta;
    }

    void StopAtCurrentPosition()
    {
        enemyRigidbody2D.linearVelocity = Vector2.zero;
        enemyRigidbody2D.angularVelocity = 0f;
        enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        SetAnimMoving(false);
    }

    /// <summary>
    /// Where this enemy should stand to fight the hero at <paramref name="targetPos"/>:
    /// beside it, at standoff distance, level enough for the weapon box to overlap.
    ///
    /// The side is re-derived from this enemy's OWN position every step, using the
    /// same rule the hero runs against this enemy. That is what makes the two agree
    /// instead of chasing each other's flank across the map - see MeleeEngagement.
    ///
    /// A slot is claimed once per target so two enemies on one hero take opposite
    /// sides rather than the same one. Nobody is ever pushed: claiming a spot and
    /// walking to it is the project's rule for spacing (see AttackSlotRegistry).
    /// </summary>
    Vector2 ResolveStandPoint(Vector2 targetPos)
    {
        Object target = currentTarget;

        if (target != attackSlotTarget)
        {
            AttackSlotRegistry.Release(this);
            attackSlotIndex = AttackSlotRegistry.Claim(target, this);
            attackSlotTarget = target;
            heldAttackSide = 0f;   // a new target means a fresh side decision
        }

        Vector2 pos = enemyRigidbody2D ? enemyRigidbody2D.position : (Vector2)transform.position;

        heldAttackSide = MeleeEngagement.ChooseSide(
            pos, targetPos, MeleeEngagement.EnemyPreferredSide, heldAttackSide);

        return MeleeEngagement.StandPoint(targetPos, stoppingDistance, heldAttackSide, attackSlotIndex);
    }

    /// <summary>
    /// TRUE when this enemy is standing somewhere its weapon can actually land on
    /// its current hero target. EnemyManager gates its swing on this, so it cannot
    /// start an attack animation while still walking round to the hero's flank.
    /// </summary>
    public bool IsInAttackPosition()
    {
        if (currentTarget == null) return false;

        Vector2 pos = enemyRigidbody2D ? enemyRigidbody2D.position : (Vector2)transform.position;
        if (MeleeEngagement.InAttackPosition(pos, currentTarget.transform.position, stoppingDistance)) return true;
        // Valkyrie uses a slightly wider alignment band than this enemy (0.85
        // versus 0.83 range). Accept that tiny alignment difference while still
        // requiring actual melee range, rather than sliding a planted enemy.
        var hero = currentTarget.PlayerManager;
        Vector2 offset = (Vector2)currentTarget.transform.position - pos;
        return hero && hero.currentTarget && hero.currentTarget.enemyManager == enemyManager && hero.IsInAttackPosition() &&
            offset.magnitude <= stoppingDistance && offset.magnitude >= MeleeEngagement.Standoff(stoppingDistance) * MeleeEngagement.ArrivalSlack &&
            Mathf.Abs(offset.y) <= MeleeEngagement.Band(stoppingDistance) + 0.02f;
    }


    public void SetAnimMoving(bool moving)
    {
        if (!anim) return;

        anim.SetFloat("Horizontal", 0f);
        anim.SetFloat("Vertical", moving ? 1f : 0f);   // (0,1)=walk, (0,0)=idle
    }

    // Optional: visualize detection radius
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
