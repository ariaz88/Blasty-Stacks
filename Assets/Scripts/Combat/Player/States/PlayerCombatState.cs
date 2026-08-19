using UnityEngine;

public class PlayerCombatState : PlayerState
{
    public PlayerAttackState PlayerAttackState;
    public PlayerPursueTargetState PlayerPursueTargetState;
    public PlayerLockState playerLockState;

    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        if (playerLockState && !pm.isUnlocked)
        {
            return playerLockState;
        }

        //// If target died/vanished, try to reacquire; if found but out of range, pursue; if none, fall back to pursue/idle elsewhere.
        //if (!pm.targetDetectionForPlayer.EnsureTarget(true))
        //    return PlayerPursueTargetState;

        if (pm.currentTarget == null)
            return PlayerPursueTargetState;


        //pm.UpdateFacing();


        float distanceFromTarget = Vector3.Distance(pm.currentTarget.transform.position, pm.transform.position);

        if (pm.currentRecoveryTimer <= 0 && distanceFromTarget <= pm.maxAttackRange)
            return PlayerAttackState;

        if (distanceFromTarget > pm.maxAttackRange)
            return PlayerPursueTargetState;

        return this;
    }
}

public class PlayerCombatState1 : PlayerState
{
    public PlayerAttackState PlayerAttackState;
    public PlayerPursueTargetState PlayerPursueTargetState;
    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
    {
        // Check For Attack Range

        // potenitially circle player or walk around  them

        // if in attack range  return attack  state


        float distanceFromTarget = Vector3.Distance(playerManager.currentTarget.transform.position, playerManager.transform.position);

        if (playerManager.currentRecoveryTimer<= 0  && distanceFromTarget <= playerManager.maxAttackRange)
        {
            return PlayerAttackState;
        }
        else if (distanceFromTarget > playerManager.maxAttackRange )
        {
            return PlayerPursueTargetState;
        }
        else
        {
        return this;

        }

    }
}
