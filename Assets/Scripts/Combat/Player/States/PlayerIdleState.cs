using UnityEngine;

public class PlayerIdleState : PlayerState
{
    public PlayerPursueTargetState PlayerPursueTargetState;

    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        pm.UpdateFacing();

        if (pm.targetDetectionForPlayer.EnsureTarget(true))
            return PlayerPursueTargetState;

        return this;
    }
}
