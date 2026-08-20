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

        if (pm.currentTarget == null)
            return PlayerPursueTargetState;




        float distanceFromTarget = Vector3.Distance(pm.currentTarget.transform.position, pm.transform.position);

        if (pm.currentRecoveryTimer <= 0 && distanceFromTarget <= pm.maxAttackRange)
            return PlayerAttackState;

        if (distanceFromTarget > pm.maxAttackRange)
            return PlayerPursueTargetState;

        return this;
    }
}
