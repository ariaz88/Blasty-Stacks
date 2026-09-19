using UnityEngine;

public class PlayerPursueTargetState : PlayerState
{
    public PlayerCombatState PlayerCombatState;
    public PlayerAttackState playerAttackState;
    public PlayerState playerLockState;   // optional: assign if you use a lock state gate
    [SerializeField] private float distanceToFaceOff = 3f;   // you asked to set it to 4

    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        // 0) Hard-lock gate
        if (playerLockState != null && !pm.isUnlocked)
            return playerLockState;

        if (pm.isPerformingAction || pm.isInteracting)
            return this;

        ////Vector3 ls = transform.localScale;
        
        ////ls = new Vector3(1,1,1);
        ////transform.localScale = ls;


        // 1) If we hit the EnemyGate, go directly to Attack (your special case #4)
        // Gate attack ONLY if there are no enemies in the scene
        bool noEnemiesLeft = !pm.targetDetectionForPlayer.AnyEnemyAliveInScene();



        if (pm.attackPlayerGate && noEnemiesLeft)
        {
            // either you use a trigger flag (pm.attackGate) or also check distance to gate if you keep one
            pm.HandleMoveToTarget(false);
            pm.SetAnimMoving(false);

            if (pm.playerRigidbody)
            {
                pm.playerRigidbody.linearVelocity = Vector2.zero;
                pm.playerRigidbody.bodyType = RigidbodyType2D.Static;
            }
            return playerAttackState;
        }

        //// 2) Keep/refresh target (allows retargeting if you enabled it in the detector)

        if (/*!haveTarget ||*/ pm.currentTarget == null)
        {
            // Marching on the gate with no enemies left. This MUST go through
            // HandleRoamForward, not a raw velocity write: that is where ally
            // ally path steering lives. Setting linearVelocity directly
            // here is what made heroes walk into the ranks already at the gate.
            pm.HandleRoamForward();
            return this; // remain in pursue while roaming forward
        }
              

 
        pm.canMove = true;

        // Close contact is a valid attack position too. Never back out of
        // an overlapping target just to satisfy a minimum stand-off radius.
        if (!pm.IsInAttackPosition())
        {
            // pursue
            if (pm.playerRigidbody) pm.playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
            pm.HandleMoveToTarget(true);     // your mover drives velocity
            pm.SetAnimMoving(true);
            return this;
        }
        else
        {
            // in attack zone -> hand off to Combat (rule #3)
            pm.HandleMoveToTarget(false);
            pm.SetAnimMoving(false);
            if (pm.playerRigidbody) { pm.playerRigidbody.linearVelocity = Vector2.zero; pm.playerRigidbody.bodyType = RigidbodyType2D.Static; }
            return PlayerCombatState;
        }
    }


}
