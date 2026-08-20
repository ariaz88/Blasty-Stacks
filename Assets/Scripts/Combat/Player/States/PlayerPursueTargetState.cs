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

            // Walk forward along facing
            pm.canMove = true;
            if (pm.playerRigidbody)
            {
                pm.playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
                Vector2 dir = pm.transform.up; // use transform.right if your sprite art faces right
                pm.playerRigidbody.linearVelocity = dir * pm.moveSpeed;
            }

            pm.SetAnimMoving(true);
            return this; // remain in pursue while roaming forward
        }
              

 
        float distanceFromTarget = Vector2.Distance(
        pm.playerRigidbody ? pm.playerRigidbody.position : (Vector2)pm.transform.position,
       (Vector2)pm.currentTarget.transform.position);



        pm.canMove = true;


        if (distanceFromTarget > pm.maxAttackRange)
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
