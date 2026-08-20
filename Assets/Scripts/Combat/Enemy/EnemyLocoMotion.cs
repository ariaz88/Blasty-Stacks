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
    public float moveSpeed = 1.5f;          // units/sec
    public float stoppingDistance = 0.5f;


    // NEW: pursue player only when close enough and only if player is "opposite side"
    public float fairDistanceToPlayer = 1.6f;

    Animator anim;

    void Awake()
    {
        enemyManager = GetComponent<EnemyManager>();
        if (!enemyRigidbody2D) enemyRigidbody2D = GetComponent<Rigidbody2D>();
        anim = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        enemyRigidbody2D.gravityScale = 0f;
        enemyRigidbody2D.freezeRotation = true;
        enemyRigidbody2D.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    void Update1()
    {
        if (GameplayPause.IsPaused)
        {
            SetAnimMoving(false);
            return;
        }

        HandleMoveToTarget();

        // distance info for EnemyManager
        if (currentTarget != null)
        {
            distanceFromTarget = Vector2.Distance(currentTarget.transform.position, transform.position);
        }
        else if (enemyManager.currentGateTarget != null)
        {
            distanceFromTarget = Vector2.Distance(enemyManager.currentGateTarget.transform.position, transform.position);
        }
        else
        {
            distanceFromTarget = 20f;
        }
    }
    void Update()
    {
        if (GameplayPause.IsPaused)
        {
            SetAnimMoving(false);
            return;
        }

        HandleMoveToTarget();

        if (currentTarget != null)
        {
            distanceFromTarget = Vector2.Distance(currentTarget.transform.position, transform.position);
        }
        else
        {
            distanceFromTarget = 20f;
        }
    }


    // ---------- LOCOMOTION (2D) ----------
    public void HandleMoveToTarget1()
    {
        if (!enemyRigidbody2D) return;

        Vector2 pos = enemyRigidbody2D.position;

        // 1) Chase player if we have one
        if (currentTarget != null && !currentTarget.playerIsdead)
        {
            Vector2 toTarget = (Vector2)currentTarget.transform.position - pos;
            float dist = toTarget.magnitude;

            if (dist > stoppingDistance)
            {
                Vector2 dir = toTarget.normalized;
                Vector2 next = pos + dir * moveSpeed * Time.deltaTime;
                enemyRigidbody2D.MovePosition(next);
                SetAnimMoving(true);
            }
            else
            {
                enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                SetAnimMoving(false);
            }

            return;
        }

        // 2) No player target -> move toward gate if it exists and is not destroyed
        if (enemyManager.currentGateTarget != null && !enemyManager.currentGateTarget.isPlayerGateDestroyed)
        {
            // If we already reached the gate once, never pass that position
            if (enemyManager.reachedGate)
            {
                Vector2 toStop = enemyManager.gateStopPosition - pos;

                if (toStop.sqrMagnitude > 0.001f)
                {
                    Vector2 dir = toStop.normalized;
                    Vector2 next = pos + dir * moveSpeed * Time.deltaTime;

                    // clamp so we don't overshoot the stop point
                    Vector2 after = enemyManager.gateStopPosition - next;
                    if (after.sqrMagnitude > toStop.sqrMagnitude)
                        next = enemyManager.gateStopPosition;

                    enemyRigidbody2D.MovePosition(next);
                    SetAnimMoving(true);
                }
                else
                {
                    enemyRigidbody2D.position = enemyManager.gateStopPosition;
                    enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                    SetAnimMoving(false);
                }

                return;
            }

            // We haven't reached the gate yet
            Vector2 toGate = (Vector2)enemyManager.currentGateTarget.transform.position - pos;
            float distG = toGate.magnitude;

            if (distG > stoppingDistance)
            {
                Vector2 dir = toGate.normalized;
                Vector2 next = pos + dir * moveSpeed * Time.deltaTime;
                enemyRigidbody2D.MovePosition(next);
                SetAnimMoving(true);
            }
            else
            {
                // treat as reached gate even if trigger didn't fire
                enemyManager.reachedGate = true;
                enemyManager.gateStopPosition = enemyRigidbody2D.position;

                enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                SetAnimMoving(false);
            }

            return;
        }

        // 3) No targets at all
        SetAnimMoving(false);
    }
    public void HandleMoveToTarget()
    {
        if (!enemyRigidbody2D) return;

        Vector2 pos = enemyRigidbody2D.position;

        // 1) If we already reached the gate once, never move past that point
        if (enemyManager != null && enemyManager.reachedGate)
        {
            enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            enemyRigidbody2D.position = enemyManager.gateStopPosition;
            SetAnimMoving(false);
            return;
        }

        // 2) If we have a player target -> move straight forward until we are in attack range
        if (currentTarget != null && !currentTarget.playerIsdead)
        {
            float dist = Vector2.Distance(currentTarget.transform.position, transform.position);

            if (dist > stoppingDistance)
            {
                Vector2 laneDir = -(Vector2)transform.up;
                Vector2 toPlayer = (Vector2)currentTarget.transform.position - pos;

                bool isRealPlayer = currentTarget.CompareTag("Player"); // gate should be "PlayerGate"
                bool withinFair = dist <= fairDistanceToPlayer;

                // your rule: enemy tracks player only after it has passed the player in Y
                bool enemyPassedPlayerInY = transform.position.y > currentTarget.transform.position.y;

                bool shouldChasePlayer = isRealPlayer && withinFair/* && enemyPassedPlayerInY*/;

                Vector2 moveDir = laneDir;

                // IMPORTANT: when we chase, we move directly toward the player (diagonal)
                if (shouldChasePlayer && toPlayer.sqrMagnitude > 0.0001f)
                    moveDir = toPlayer.normalized;

                Vector2 next = pos + moveDir * moveSpeed * Time.deltaTime;

                enemyRigidbody2D.bodyType = RigidbodyType2D.Dynamic;
                enemyRigidbody2D.MovePosition(next);
                SetAnimMoving(true);
            }
            else
            {
                enemyRigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                SetAnimMoving(false);
            }




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
                Vector2 next = pos + dir * moveSpeed * Time.deltaTime;

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
