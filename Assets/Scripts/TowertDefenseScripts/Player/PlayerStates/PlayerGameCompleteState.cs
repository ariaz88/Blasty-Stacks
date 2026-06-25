using UnityEngine;

public class PlayerGameCompleteState : PlayerState
{
    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        // Freeze everything
        if (pm.playerRigidbody)
        {
            pm.playerRigidbody.linearVelocity = Vector2.zero;
            pm.playerRigidbody.angularVelocity = 0f;
            pm.playerRigidbody.bodyType = RigidbodyType2D.Static;
        }

        pm.canMove = false;
        pm.SetAnimMoving(false);

        if (am && am.anim) am.anim.applyRootMotion = false;

        // Remain here until higher-level game flow changes state
        return this;
    }
}
