using UnityEngine;

public class TargetDetectionForPlayer : MonoBehaviour
{
    private PlayerManager playerManager;

    [Header("Detection")]
    public LayerMask enemyDetectionLayer;
    [Tooltip("Use 2D physics (OverlapCircleAll) instead of 3D.")]
    public bool use2D = true;

    [Header("Retargeting")]
    // REMOVED 2026-09-17: allowNearestSwitch, onlySwitchIfInFront and
    // retargetHysteresis, together with the branch that read them.
    //
    // All three served one behaviour - "drop the enemy you have for a nearer
    // one" - and that behaviour is now forbidden: a hero keeps a living target
    // until it dies, so that every OTHER hero can trust what
    // TargetClaimRegistry says about who is handling whom. PlayerIdleState was
    // the only caller and it passed `true`, so this was live, not dormant.
    //
    // PlayerManager.retargetHysteresis was deleted in the same pass for the
    // same reason; see rule 1 in PlayerManager.UpdateTargetSelection.

    [Tooltip("Apply FOV check when validating/searching targets.")]
    public bool respectFOV = false;

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
    }

    // REMOVED 2026-09-17: EnsureTarget1, a dead numbered sibling of EnsureTarget.
    // It had no call site and differed only in not setting hasDetectedEnemyOnce,
    // so reviving it would have left the "have I ever seen an enemy" flag false
    // forever. Both carried the same retarget branch, now deleted from the one
    // that is live.

    /// <summary>
    /// Call this each Tick() at the top of your states.
    /// Returns true if we have a valid target after this pass.
    ///
    /// ACQUIRE ONLY - this never swaps a living target for a nearer one. A hero
    /// keeps the enemy it has until that enemy dies, because every other hero
    /// reads PlayerManager.currentTarget as a claim (see TargetClaimRegistry) and
    /// a claim that can be dropped for 30cm of distance is not worth reading.
    ///
    /// The <paramref name="overrideAllowNearestSwitch"/> parameter is kept so
    /// PlayerIdleState still compiles; it is no longer read. It is not a silent
    /// no-op by accident - "switch to whatever is nearest" is precisely the
    /// behaviour that sent two heroes at the same enemy while the other one
    /// walked into the player base.
    /// </summary>
    public bool EnsureTarget(bool? overrideAllowNearestSwitch = null)
    {
        // A hero still on its deploy gate picks nothing. Same reason as rule 0 in
        // PlayerManager.UpdateTargetSelection: locked heroes are invisible to
        // TargetClaimRegistry, so letting a whole wave choose while it waits on
        // the gates makes every one of them pick the same enemy. This is the
        // second acquisition path, so it needs the same guard or it reopens the
        // hole on its own.
        if (!playerManager.isUnlocked) return false;

        if (!IsTargetValid(playerManager.currentTarget, respectFOV))
        {
            playerManager.currentTarget = AcquireBestEnemy(respectFOV, null, out _);
            if (playerManager.currentTarget != null)
                playerManager.hasDetectedEnemyOnce = true;   // <-- set once

            return playerManager.currentTarget != null;
        }

        return true;
    }


    /// <summary>
    /// Conservative validity check (null, active, alive, optional FOV).
    ///
    /// NO RADIUS TEST since 2026-09-17. It used to reject any target further away
    /// than detectionRadius, which now contradicts the rule above: a hero is
    /// deliberately sent across the field to cover an enemy nobody else is on, and
    /// a radius test would declare that target invalid on arrival and re-acquire
    /// the nearest one instead - exactly the pile-up the rule removes.
    ///
    /// The enemyIsdead test is NEW. Only activeInHierarchy was checked before, so
    /// a corpse still playing its death animation read as a valid target and the
    /// hero stood waiting for it.
    /// </summary>
    public bool IsTargetValid(EnemyStats target, bool doFOV)
    {
        if (target == null) return false;
        if (!target.gameObject.activeInHierarchy) return false;
        if (target.enemyIsdead) return false;

        if (doFOV && !IsWithinFOV(target.transform.position)) return false;


        return true;
    }

    /// <summary>
    /// Pick the BEST enemy within detectionRadius (and FOV if requested):
    /// fewest other heroes already handling it, then nearest, then instance id.
    /// Optionally exclude one (e.g., the current target).
    ///
    /// Renamed from AcquireNearestEnemy - "nearest" stopped being the whole rule
    /// on 2026-09-17 and a name that still said so would be a trap. The ranking
    /// itself lives in TargetClaimRegistry.Beats, shared with
    /// PlayerManager.UpdateTargetSelection so the two acquisition paths in this
    /// project can never disagree about who to pick.
    /// </summary>
    public EnemyStats AcquireBestEnemy(bool doFOV, EnemyStats exclude, out float bestDist)
    {
        Vector3 origin = playerManager.transform.position;
        EnemyStats best = null;
        int bestClaims = 0;
        float bestDistSq = 0f;

        if (!use2D)
        {
            var hits = Physics.OverlapSphere(origin, playerManager.detectionRadius, enemyDetectionLayer);
            for (int i = 0; i < hits.Length; i++)
            {
                if (!hits[i]) continue;

                var es = hits[i].GetComponent<EnemyStats>() ?? hits[i].GetComponentInParent<EnemyStats>();
                if (!IsCandidate(es, exclude, doFOV)) continue;

                int claims = TargetClaimRegistry.OtherClaimants(es, playerManager);
                float dsq = (es.transform.position - origin).sqrMagnitude;

                if (TargetClaimRegistry.Beats(es, claims, dsq, best, bestClaims, bestDistSq))
                {
                    best = es;
                    bestClaims = claims;
                    bestDistSq = dsq;
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

                int claims = TargetClaimRegistry.OtherClaimants(es, playerManager);
                float dsq = ((Vector2)es.transform.position - (Vector2)origin).sqrMagnitude;

                if (TargetClaimRegistry.Beats(es, claims, dsq, best, bestClaims, bestDistSq))
                {
                    best = es;
                    bestClaims = claims;
                    bestDistSq = dsq;
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

        // A corpse is not a target. Its collider survives the death animation, so
        // without this a hero could acquire something already dead and stand there.
        if (es.enemyIsdead) return false;

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
