using UnityEngine;

public class PlayerLockState : PlayerState
{
    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        // stop any current motion
        if (pm.playerRigidbody != null)
        {
            pm.playerRigidbody.linearVelocity = Vector2.zero;
            pm.playerRigidbody.angularVelocity = 0f;
            pm.playerRigidbody.bodyType = RigidbodyType2D.Static;  // fully static (no movement)
        }

        // disable locomotion/animation drive
        pm.canMove = false;
        pm.SetAnimMoving(false);

        // make sure root motion doesn't try to move the body
        if (am != null && am.anim != null)
            am.anim.applyRootMotion = false;

        // optionally clamp Z in case something nudged it
        var p = pm.transform.position;
        if (p.z != 0f) pm.transform.position = new Vector3(p.x, p.y, 0f);

        // remain in lock until some external code changes the state
        return this;
    }
}
