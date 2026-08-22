using UnityEngine;

public class PlayerLockState : PlayerState
{
    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        // A hero stepping into a formation gap is still LOCKED (the battle has
        // not started), but the gap filler owns its position, facing and walk
        // animation for the duration of that step. Forcing idle here ran every
        // FixedUpdate and overwrote the walk cycle the instant it was set, so the
        // hero slid to its slot with no animation at all - while the exact same
        // walk looked correct once the battle began and the hero had moved on to
        // PlayerPursueTargetState.
        if (!pm.isFormationStepping)
        {
            // stop any current motion
            if (pm.playerRigidbody != null)
            {
                // Only touch velocity on a body that can actually have one.
                // Writing linearVelocity on an already-Static body logs
                // "Cannot use 'linearVelocity' on a static body" every step.
                if (pm.playerRigidbody.bodyType != RigidbodyType2D.Static)
                {
                    pm.playerRigidbody.linearVelocity = Vector2.zero;
                    pm.playerRigidbody.angularVelocity = 0f;
                    pm.playerRigidbody.bodyType = RigidbodyType2D.Static;  // fully static (no movement)
                }
            }

            // disable locomotion/animation drive
            pm.canMove = false;
            pm.SetAnimMoving(false);
        }

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
