using UnityEngine;

public class TargetDetectionForPlayer : MonoBehaviour
{
    private PlayerManager playerManager;

    [Header("Detection")]
    public LayerMask enemyDetectionLayer;
    [Tooltip("Use 2D physics (OverlapCircleAll) instead of 3D.")]
    public bool use2D = true;

    [Header("Retargeting")]
    [Tooltip("If true, will switch to a nearer enemy even if we already have a valid target.")]
    public bool allowNearestSwitch = false;

    [Tooltip("Only switch to a nearer enemy if it is inside our FOV.")]
    public bool onlySwitchIfInFront = true;

    [Tooltip("Apply FOV check when validating/searching targets.")]
    public bool respectFOV = false;

    [Tooltip("How much nearer (meters) another enemy must be before we switch targets (prevents jitter).")]
    [Min(0f)] public float retargetHysteresis = 0.35f;

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
    }

    /// Call this each Tick() at the top of your states.
    /// Returns true if we have a valid target after this pass.
    public bool EnsureTarget1(bool? overrideAllowNearestSwitch = null)
    {
        bool doSwitch = overrideAllowNearestSwitch ?? allowNearestSwitch;

        // 1) If no valid target, acquire one.
        if (!IsTargetValid(playerManager.currentTarget, respectFOV))
        {
            playerManager.currentTarget = AcquireNearestEnemy(respectFOV, null, out _);
            return playerManager.currentTarget != null;
        }

        // 2) If we have a valid target but retargeting is allowed, check if a meaningfully nearer one exists.
        if (doSwitch)
        {
            var current = playerManager.currentTarget;
            float currentDist = Distance2D3D(playerManager.transform.position, current.transform.position);

            // Find the nearest other target (can exclude current)
            EnemyStats best = AcquireNearestEnemy(respectFOV, exclude: current, out float bestDist);

            // Optionally require the candidate to be inside FOV before switching
            bool passesFOVGate = !onlySwitchIfInFront || IsWithinFOV(best?.transform.position ?? Vector3.zero);

            if (best != null && passesFOVGate)
            {
                // Switch only if it's closer by at least retargetHysteresis
                if (bestDist + retargetHysteresis < currentDist)
                {
                    playerManager.currentTarget = best;
                }
            }
        }

        return true;
    }
    public bool EnsureTarget(bool? overrideAllowNearestSwitch = null)
    {
        bool doSwitch = overrideAllowNearestSwitch ?? allowNearestSwitch;

        if (!IsTargetValid(playerManager.currentTarget, respectFOV))
        {
            playerManager.currentTarget = AcquireNearestEnemy(respectFOV, null, out _);
            if (playerManager.currentTarget != null)
                playerManager.hasDetectedEnemyOnce = true;   // <-- set once
            
           

            return playerManager.currentTarget != null;
        }

        if (doSwitch)
        {
            var current = playerManager.currentTarget;
            float currentDist = Distance2D3D(playerManager.transform.position, current.transform.position);
            EnemyStats best = AcquireNearestEnemy(respectFOV, exclude: current, out float bestDist);

            bool passesFOVGate = !onlySwitchIfInFront || IsWithinFOV(best?.transform.position ?? Vector3.zero);
            if (best != null && passesFOVGate && bestDist + retargetHysteresis < currentDist)
            {
                playerManager.currentTarget = best;
                playerManager.hasDetectedEnemyOnce = true;   // (already true, but safe)


            }
        }

        return true;
    }


    /// Conservative validity check (null, active, radius, optional FOV).
    public bool IsTargetValid(EnemyStats target, bool doFOV)
    {
        if (target == null) return false;
        if (!target.gameObject.activeInHierarchy) return false;

        float dist = Distance2D3D(playerManager.transform.position, target.transform.position);
        if (dist > playerManager.detectionRadius) return false;

        if (doFOV && !IsWithinFOV(target.transform.position)) return false;

        // If you track death on EnemyStats, add it here:
        // if (target.enemyIsdead) return false;

        return true;
    }

    /// Pick nearest enemy within radius (and FOV if requested).
    /// Optionally exclude one (e.g., the current target).
    public EnemyStats AcquireNearestEnemy(bool doFOV, EnemyStats exclude, out float bestDist)
    {
        Vector3 origin = playerManager.transform.position;
        EnemyStats best = null;
        float bestDistSq = float.PositiveInfinity;

        if (!use2D)
        {
            var hits = Physics.OverlapSphere(origin, playerManager.detectionRadius, enemyDetectionLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i]) continue;

                var es = hits[i].GetComponent<EnemyStats>() ?? hits[i].GetComponentInParent<EnemyStats>();
                if (!IsCandidate(es, exclude, doFOV)) continue;

                float dsq = (es.transform.position - origin).sqrMagnitude;
                if (dsq < bestDistSq)
                {
                    bestDistSq = dsq;
                    best = es;
                }
            }
        }
        else
        {
            var hits = Physics2D.OverlapCircleAll((Vector2)origin, playerManager.detectionRadius, enemyDetectionLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i]) continue;

                var es = hits[i].GetComponent<EnemyStats>() ?? hits[i].GetComponentInParent<EnemyStats>();
                if (!IsCandidate(es, exclude, doFOV)) continue;

                float dsq = ((Vector2)es.transform.position - (Vector2)origin).sqrMagnitude;
                if (dsq < bestDistSq)
                {
                    bestDistSq = dsq;
                    best = es;
                }
            }
        }

        bestDist = (best == null) ? float.PositiveInfinity
                                  : Mathf.Sqrt(bestDistSq);
        return best;
    }

    // ---------------- internals ----------------

    private bool IsCandidate(EnemyStats es, EnemyStats exclude, bool doFOV)
    {
        if (es == null || es == exclude) return false;
        if (!es.gameObject.activeInHierarchy) return false;
        // if (es.enemyIsdead) return false;

        if (doFOV && !IsWithinFOV(es.transform.position)) return false;

        // also ensure in radius (cheap early out)
        float dist = Distance2D3D(playerManager.transform.position, es.transform.position);
        if (dist > playerManager.detectionRadius) return false;

        return true;
    }

    private float Distance2D3D(Vector3 a, Vector3 b)
    {
        if (use2D)
            return Vector2.Distance(a, b);
        else
            return Vector3.Distance(a, b);
    }

    private bool IsWithinFOV(Vector3 targetPos)
    {
        if (!respectFOV) return true; // global gate

        if (!use2D)
        {
            Vector3 to = targetPos - transform.position;
            float angle = Vector3.Angle(to, transform.forward);
            return angle >= playerManager.minimumDetectionAngle && angle <= playerManager.maximumDetectionAngle;
        }
        else
        {
            // If your sprite faces RIGHT by default, use transform.right
            Vector2 to = (Vector2)(targetPos - transform.position);
            float angle = Vector2.Angle(to, (Vector2)transform.up);
            return angle >= playerManager.minimumDetectionAngle && angle <= playerManager.maximumDetectionAngle;
        }
    }

    public bool HasEnemyInRadius()
    {
        Vector3 origin = playerManager.transform.position;
        if (!use2D)
        {
            var hits = Physics.OverlapSphere(origin, playerManager.detectionRadius, enemyDetectionLayer);
            for (int i = 0; i < hits.Length; i++)
                if (hits[i] && (hits[i].GetComponent<EnemyStats>() || hits[i].GetComponentInParent<EnemyStats>()))
                    return true;
            return false;
        }
        else
        {
            var hits = Physics2D.OverlapCircleAll((Vector2)origin, playerManager.detectionRadius, enemyDetectionLayer);
            for (int i = 0; i < hits.Length; i++)
                if (hits[i] && (hits[i].GetComponent<EnemyStats>() || hits[i].GetComponentInParent<EnemyStats>()))
                    return true;
            return false;
        }
    }

    public bool AnyEnemyAliveInScene()
    {
        // If you track death via enemyIsdead, filter it; otherwise just activeInHierarchy.
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = enemies[i];
            if (e && e.gameObject.activeInHierarchy && !e.GetComponent<EnemyStats>().enemyIsdead)
                return true;
        }
        return false;
    }


}


public class TargetDetectionForPlayer1 : MonoBehaviour
{
    PlayerManager playerManager;
    private bool use2D = true;
    public LayerMask enemyDetectionLayer;

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
    }
    public bool EnsureValidTarget(bool respectFOV = true)
    {
        if (!IsTargetValid(playerManager.currentTarget, respectFOV))
        {
            playerManager.currentTarget = AcquireNearestEnemy(respectFOV);
        }
        return playerManager.currentTarget != null;
    }

    /// Conservative validity check (null, active, radius, optional FOV, optional death flag)
    public bool IsTargetValid(EnemyStats target, bool respectFOV = true)
    {
        if (target == null) return false;

        var go = target.gameObject;
        if (!go.activeInHierarchy) return false;

        // If you track death: if (TargetIsDead(target)) return false;

        float dist = Vector3.Distance(playerManager.transform.position, target.transform.position);
        if (dist > playerManager.detectionRadius) return false;

        //if (respectFOV && !IsWithinFOV(target.transform.position)) return false;

        return true;
    }

    /// Pick nearest enemy within radius (and FOV if requested).
    public EnemyStats AcquireNearestEnemy(bool respectFOV = true)
    {
        Vector3 origin = playerManager.transform.position;

        EnemyStats best = null;
        float bestDistSq = float.PositiveInfinity;

        if (!use2D)
        {
            // ------------- 3D -------------
            var hits = Physics.OverlapSphere(origin, playerManager.detectionRadius, enemyDetectionLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;

                EnemyStats es = hits[i].GetComponent<EnemyStats>() ?? hits[i].GetComponentInParent<EnemyStats>();
                if (es == null) continue;
                if (!IsTargetValidBasic(es)) continue; // quick checks without FOV first

                //if (respectFOV && !IsWithinFOV(es.transform.position)) continue;

                float dsq = (es.transform.position - origin).sqrMagnitude;
                if (dsq < bestDistSq)
                {
                    bestDistSq = dsq;
                    best = es;
                }
            }
        }
        else
        {
            // ------------- 2D -------------
            var hits = Physics2D.OverlapCircleAll((Vector2)origin, playerManager.detectionRadius, enemyDetectionLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] == null) continue;

                EnemyStats es = hits[i].GetComponent<EnemyStats>() ?? hits[i].GetComponentInParent<EnemyStats>();
                if (es == null) continue;
                if (!IsTargetValidBasic(es)) continue;

                //if (respectFOV && !IsWithinFOV(es.transform.position)) continue;

                float dsq = ((Vector2)es.transform.position - (Vector2)origin).sqrMagnitude;
                if (dsq < bestDistSq)
                {
                    bestDistSq = dsq;
                    best = es;
                }
            }
        }

        return best;
    }

    // --- internals ---

    // Fast checks used during scanning; no FOV here to keep it cheap.
    private bool IsTargetValidBasic(EnemyStats target)
    {
        if (target == null) return false;
        if (!target.gameObject.activeInHierarchy) return false;
        // if (TargetIsDead(target)) return false;
        return true;
    }

    private bool IsWithinFOV(Vector3 targetPos)
    {
        if (!use2D)
        {
            // 3D: use forward
            Vector3 to = targetPos - transform.position;
            float angle = Vector3.Angle(to, transform.forward);
            return angle >= playerManager.minimumDetectionAngle && angle <= playerManager.maximumDetectionAngle;
        }
        else
        {
            // 2D: choose which direction your sprite "faces"
            // If your sprite artwork faces UP, use transform.up; if it faces RIGHT, use transform.right
            Vector2 to = (Vector2)(targetPos - transform.position);
            float angle = Vector2.Angle(to, (Vector2)transform.up);
            return angle >= playerManager.minimumDetectionAngle && angle <= playerManager.maximumDetectionAngle;
        }
    }

}
