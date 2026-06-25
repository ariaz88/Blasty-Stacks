using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerPursueTargetState PlayerPursueTargetState;

    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        // Reacquire if needed (no duplicate code here)
        //pm.UpdateFacingAndOffset();
        pm.UpdateFacing();

        if (pm.targetDetectionForPlayer.EnsureTarget(true))
            return PlayerPursueTargetState;

        return this;
    }
}

public class PlayerIdleState1 : PlayerState
{
    public LayerMask enemyDetectionLayer;
    public PlayerPursueTargetState PlayerPursueTargetState;

    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
    {
        // 1) If current target is invalid, reacquire
        if (!IsTargetValid(playerManager, playerManager.currentTarget))
        {
            playerManager.currentTarget = AcquireNearestEnemy(playerManager);
        }

        // 2) If we have a valid target now, move to pursue
        if (IsTargetValid(playerManager, playerManager.currentTarget))
        {
            return PlayerPursueTargetState;
        }

        // Stay idle
        return this;
    }

    // ----------------- Helpers -----------------

    private EnemyStats AcquireNearestEnemy(PlayerManager pm)
    {
        Vector2 origin = pm.transform.position;
        float radius = pm.detectionRadius;

        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius, enemyDetectionLayer);

        EnemyStats best = null;
        float bestDistSq = float.PositiveInfinity;

        Vector2 forward2D = (Vector2)pm.transform.up; // use right if your sprite faces right

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;

            // EnemyStats might be on this object or a parent
            EnemyStats es;
            if (!hits[i].TryGetComponent(out es))
                es = hits[i].GetComponentInParent<EnemyStats>();

            if (!IsTargetValid(pm, es)) continue;

            Vector2 to = (Vector2)es.transform.position - origin;
            float distSq = to.sqrMagnitude;

            // FOV check
            float angle = Vector2.Angle(to, forward2D);
            if (angle < pm.minimumDetectionAngle || angle > pm.maximumDetectionAngle) continue;

            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = es;
            }
        }

        return best;
    }

    /// <summary>
    /// Conservative validity check that won’t throw if components are gone.
    /// </summary>
    private bool IsTargetValid(PlayerManager pm, EnemyStats target)
    {
        if (target == null) return false;
        var go = target.gameObject;
        if (!go.activeInHierarchy) return false;

        // Optional: if your EnemyStats exposes a health/death flag, include it here:
        // if (target.isDead) return false;      // or target.CurrentHP <= 0

        // Still within detection radius?
        float dist = Vector2.Distance(pm.transform.position, target.transform.position);
        if (dist > pm.detectionRadius) return false;

        return true;
    }
}

public class PlayerIdleState2 : PlayerState
{
    public LayerMask enemyDetectionLayer;
    public PlayerPursueTargetState PlayerPursueTargetState;
    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
    {

        #region Handle Player Target  Detection
        //Collider[] colliders = Physics.OverlapSphere(transform.position, playerManager.detectionRadius, enemyDetectionLayer);

        //for (int i = 0; i < colliders.Length; i++)
        //{
        //    EnemyStats enemyStats = colliders[i].GetComponent<EnemyStats>();

        //    if (enemyStats != null)
        //    {
        //        Vector3 targetDirection = enemyStats.transform.position - playerManager.transform.position;
        //        float viewableAngle = Vector3.Angle(targetDirection, transform.forward);
        //        if (viewableAngle > playerManager.minimumDetectionAngle && viewableAngle < playerManager.maximumDetectionAngle)
        //        {
        //            playerManager.currentTarget = enemyStats;
        //        }
        //    }
        //}
        #endregion
        // 2D overlap circle instead of 3D sphere
        Collider2D[] hits = Physics2D.OverlapCircleAll(
            (Vector2)playerManager.transform.position,
            playerManager.detectionRadius,
            enemyDetectionLayer
        );

        for (int i = 0; i < hits.Length; i++)
        {
            // EnemyStats might be on the same object or a parent
            EnemyStats enemyStats;
            if (!hits[i].TryGetComponent(out enemyStats))
                enemyStats = hits[i].GetComponentInParent<EnemyStats>();

            if (enemyStats == null) continue;

            // Work purely in 2D
            Vector2 toTarget = (Vector2)(enemyStats.transform.position - playerManager.transform.position);
            float viewableAngle = Vector2.Angle(toTarget, (Vector2)transform.up); // use right if your sprite faces right
                
            playerManager.currentTarget = enemyStats;

            if (viewableAngle > playerManager.minimumDetectionAngle &&
                viewableAngle < playerManager.maximumDetectionAngle)
            {
                // break; // uncomment if you only want the first match
            }
        }
        #region handle switching to next state 


        if (playerManager.currentTarget != null)
        {
            return PlayerPursueTargetState;
        }
        else
        {
        return this;
        }
        #endregion
    }
}
