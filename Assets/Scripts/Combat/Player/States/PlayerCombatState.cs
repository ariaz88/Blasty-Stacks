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




        // Same rule as the pursue state uses to decide it has arrived, so the two
        // can never disagree and bounce the hero between them. A bare radial
        // distance would call "stacked directly on top of the enemy" a valid
        // fighting position - it is not; the melee hitboxes only reach sideways.
        bool inPosition = pm.IsInAttackPosition();

        if (pm.currentRecoveryTimer <= 0 && inPosition)
            return PlayerAttackState;

        if (!inPosition)
            return PlayerPursueTargetState;

        return this;
    }
}
