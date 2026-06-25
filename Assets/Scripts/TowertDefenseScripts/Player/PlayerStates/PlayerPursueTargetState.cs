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

        // ★ If there are no enemies at all, always face by screen half
        //if (noEnemiesLeft)
        //    pm.SetFacingByScreenHalf();


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
        //bool haveTarget = pm.targetDetectionForPlayer.EnsureTarget(true);

        // 3) No target => walk forward (rule #1)
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

        //if (distanceFromTarget <= distanceToFaceOff)
        //    pm.UpdateFacing();


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


//public class PlayerPursueTargetState3 : PlayerState
//{
//    public PlayerCombatState PlayerCombatState;
//    public PlayerIdleState PlayerIdleState;
//    public PlayerAttackState playerAttackState;
//    public PlayerLockState playerLockState;

//    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
//    {
//        if (playerLockState && !pm.isUnlocked )
//        {
//            return playerLockState;
//        }

//        if (pm.isPerformingAction || pm.isInteracting)
//            return this;

//        // Try to keep/refresh target
//        bool haveTarget = pm.targetDetectionForPlayer.EnsureTarget(true);

//        // ==== NEW: before first-ever detection, if no enemy in radius -> walk forward ====
//        if (!haveTarget && !pm.hasDetectedEnemyOnce)
//        {
//            // If literally nobody is around, just move forward (toward facing)
//            bool anyEnemyAround = pm.targetDetectionForPlayer.HasEnemyInRadius();
//            if (!anyEnemyAround)
//            {
//                pm.canMove = true;
//                // simple forward drift along facing (sprite faces "up")
//                Vector2 dir = pm.transform.up;            // use right if your art faces right
//                pm.playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
//                pm.playerRigidbody.linearVelocity = dir * pm.moveSpeed;

//                pm.SetAnimMoving(true);
//                return this; // keep roaming until we detect the first enemy
//            }
//            else
//            {
//                // someone is technically in radius, but not valid (FOV/blocked/etc.) � you can choose:
//                // either idle, or still roam. Here we idle:
//                pm.HandleMoveToTarget(false);
//                pm.SetAnimMoving(false);
//                return PlayerIdleState;
//            }
//        }
//        // ==== END NEW ====

//        // If no current target (after first detection behavior is established), go idle
//        if (!haveTarget)
//        {
//            pm.HandleMoveToTarget(false);
//            pm.SetAnimMoving(false);
//            return PlayerIdleState;
//        }

//        pm.UpdateFacingAndOffset();

//        pm.distanceFromTarget = Vector3.Distance(pm.currentTarget.transform.position, pm.transform.position);
//        pm.canMove = true;

//        if (pm.attackGate)
//        {
//            pm.HandleMoveToTarget(false);
//            pm.SetAnimMoving(false);
//            pm.playerRigidbody.bodyType = RigidbodyType2D.Static;
//            return playerAttackState;
//        }

//        if (pm.distanceFromTarget > pm.maxAttackRange)
//        {
//            pm.playerRigidbody.bodyType = RigidbodyType2D.Dynamic;
//            pm.HandleMoveToTarget(true);
//            pm.SetAnimMoving(true);
//        }
//        else
//        {
//            pm.HandleMoveToTarget(false);
//            pm.SetAnimMoving(false);
//            pm.playerRigidbody.bodyType = RigidbodyType2D.Static;
//            return PlayerCombatState;
//        }

//        return this;
//    }
//}

//public class PlayerPursueTargetState2 : PlayerState
//{
//    public PlayerCombatState PlayerCombatState;
//    public PlayerIdleState PlayerIdleState;
//    public PlayerAttackState playerAttackState;


//    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
//    {
//        if (pm.isPerformingAction || pm.isInteracting)
//            return this;

//        // Keep target fresh; if none, go idle
//        if (!pm.targetDetectionForPlayer.EnsureTarget(true))
//        {
//            pm.HandleMoveToTarget(false);
//            pm.SetAnimMoving(false);
//            return PlayerIdleState;
//        }

//        pm.UpdateFacingAndOffset();


//        pm.distanceFromTarget = Vector3.Distance(pm.currentTarget.transform.position, pm.transform.position);
//        //pm.navMeshAgent.enabled = true;

//        pm.canMove = true; // your testing toggle

//        if (pm.attackGate)
//        {
//            //pm.attackGate = false;         // consume once (optional, but prevents sticky state)
//            pm.HandleMoveToTarget(false);
//            pm.SetAnimMoving(false);
//            pm.playerRigidbody.bodyType = RigidbodyType2D.Static;
//            return playerAttackState;
//        }
//        if (pm.distanceFromTarget > pm.maxAttackRange)
//        {
//            pm.HandleMoveToTarget(true);
//            pm.SetAnimMoving(true);
//            pm.playerRigidbody.bodyType = RigidbodyType2D.Dynamic; 
//        }
//        else
//        {
//            pm.HandleMoveToTarget(false);
//            pm.SetAnimMoving(false);
//            pm.playerRigidbody.bodyType = RigidbodyType2D.Static;

//            return PlayerCombatState;
//        }


//        return this;
//    }

   
//}

//public class PlayerPursueTargetState1 : PlayerState
//{
//    public PlayerCombatState PlayerCombatState;
//    public PlayerIdleState PlayerIdleState;
    
//    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
//    {
//        // Chase the Enemy 
//        // If within attack range  , return combat state

//        if (playerManager.isPerformingAction)
//        {
//            return this;
//        }

//        if (playerManager.isInteracting)
//        {
//            return this;
//        }
//        if (playerManager.currentTarget == null)
//        {
//            return PlayerIdleState;
//        }
//        Vector3 targetDirection = playerManager.currentTarget.transform.position - playerManager.transform.position;
//        playerManager.distanceFromTarget = Vector3.Distance(playerManager.currentTarget.transform.position, playerManager.transform.position);
//        float viewableAngle = Vector3.Angle(targetDirection, playerManager.transform.forward);

//        //playerManager.navMeshAgent.enabled = true;


//        // *** player can move automatically at the start
//        // but for testing I used this condition:  ***
//        if (playerManager.currentTarget != null)
//        {
//            playerManager.canMove = true;
//        }


//        if (playerManager.distanceFromTarget > playerManager.maxAttackRange)
//        {
//            playerManager.HandleMoveToTarget(true);

//            playerManager.SetAnimMoving(true);

//            //playerAnimatitorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime);
//        }
//        else if (playerManager.distanceFromTarget <= playerManager.maxAttackRange)
//        {
//            //playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime);
//            playerManager.HandleMoveToTarget(false);
//            playerManager.SetAnimMoving(false);
//        }

//        //HandleRotatesTowardTarget(playerManager);

//        if (playerManager.currentTarget != null && playerManager.distanceFromTarget <= playerManager.maxAttackRange)
//        {            
//            Debug.Log("Combat State");
//            return PlayerCombatState;

//        }
//        else
//        {
//            return this;

//        }

//        //if (playerManager.currentTarget !=null)
//        //{
           
//        //}
//        //else
//        //{
//        //    playerAnimatitorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime);

//        //    return this;
//        //}
        
//    }

//    //public void HandleRotatesTowardTarget1(PlayerManager playerManager)
//    //{
//    //public float movementSpeed = 0.5f;     // tune
//    //public float rotationOffsetDeg = -90; // if your sprite faces +Y by default use 0; if +X use 0; if up/down adjust
//    //    if (playerManager.currentTarget == null) return;

//    //    // Direction on XY plane
//    //    Vector3 to = playerManager.currentTarget.transform.position - playerManager.transform.position;
//    //    to.z = 0f;

//    //    if (to.sqrMagnitude < 0.0001f) return;

//    //    // Face direction (rotate around Z)
//    //    float angle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg + rotationOffsetDeg;
//    //    Quaternion targetRot = Quaternion.AngleAxis(angle, Vector3.forward);
//    //    playerManager.transform.rotation = Quaternion.RotateTowards(
//    //        playerManager.transform.rotation,
//    //        targetRot,
//    //        playerManager.rotationSpeed * Time.deltaTime
//    //    );

//    //    // Move (only if not attacking and not in range)
//    //    if (!playerManager.isPerformingAction && to.magnitude > playerManager.maxAttackRange)
//    //    {
//    //        Vector3 step = to.normalized * movementSpeed * Time.deltaTime;
//    //        playerManager.transform.position += step;
//    //    }
//    //}

//    //public void HandleRotatesTowardTarget(PlayerManager playerManager)
//    //{
//    //    //Rotate Normally

//    //    if (playerManager.isPerformingAction)
//    //    {
//    //        Vector3 direction = playerManager.currentTarget.transform.position - playerManager.transform.position;
//    //        direction.y = 0;
//    //        direction.Normalize();
//    //        if (direction == Vector3.zero)
//    //        {
//    //            direction = playerManager.transform.forward;
//    //        }

//    //        Quaternion targetRotation = Quaternion.LookRotation(direction);

//    //        transform.rotation = Quaternion.Slerp(playerManager.transform.rotation, targetRotation, playerManager.rotationSpeed / Time.deltaTime);
//    //    }
//    //    else
//    //    {
//    //        // Use agent for path planning ONLY
//    //        playerManager.navMeshAgent.updatePosition = false;
//    //        playerManager.navMeshAgent.updateRotation = false;

//    //        playerManager.navMeshAgent.nextPosition = playerManager.transform.position;
//    //        playerManager.navMeshAgent.SetDestination(playerManager.currentTarget.transform.position);

//    //        // Face the desired movement on XY (Z-only rotation)
//    //        Vector3 dir = playerManager.navMeshAgent.desiredVelocity;
//    //        dir.z = 0f;

//    //        if (dir.sqrMagnitude > 0.001f)
//    //        {
//    //            // 2D: look �up� toward dir; zero x & y Euler
//    //            Quaternion want = Quaternion.LookRotation(Vector3.forward, dir.normalized);
//    //            Quaternion smoothed = Quaternion.Slerp(
//    //                playerManager.transform.rotation, want, playerManager.rotationSpeed * Time.deltaTime);

//    //            Vector3 e = smoothed.eulerAngles;
//    //            playerManager.transform.rotation = Quaternion.Euler(0f, 0f, e.z);  // clamp to Z only
//    //        }

//    //    }


//    //    // Rotate with NavMesh (Pathfinding)
//    //    //else
//    //    //{
//    //    //    Vector3 relativeDirection = transform.InverseTransformDirection(playerManager.navMeshAgent.desiredVelocity);

//    //    //    Vector3 targetVelocity = playerManager.playerRigidbody.linearVelocity;

//    //    //    playerManager.navMeshAgent.enabled = true;

//    //    //    playerManager.navMeshAgent.SetDestination(playerManager.currentTarget.transform.position);
//    //    //    playerManager.playerRigidbody.linearVelocity = targetVelocity;

//    //    //    transform.rotation = Quaternion.Slerp(playerManager.transform.rotation, playerManager.navMeshAgent.transform.rotation, playerManager.rotationSpeed / Time.deltaTime);
//    //    //    playerManager.navMeshAgent.transform.localPosition = Vector3.zero;
//    //    //    playerManager.navMeshAgent.transform.localRotation = Quaternion.identity;
//    //    //}
//    //}

//}

//public class PlayerPursueTargetState0 : PlayerState
//{
//    public PlayerCombatState PlayerCombatState;

//    [SerializeField] private float movementSpeed = 3f; // Tune in Inspector (e.g., 2-5 for NavMesh-like speed)
//    [SerializeField] private float rotationSpeed = 180f; // Tune in Inspector (degrees per second, e.g., 120-360)
//    [SerializeField] private float rotationOffsetDeg = -90f; // Adjust for sprite facing (e.g., 0 for +Y, -90 for +X)

//    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
//    {
//        if (playerManager.isPerformingAction || playerManager.isInteracting || playerManager.currentTarget == null)
//        {
//            playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime); // Stop animation if no target
//            return this;
//        }

//        Vector3 targetDirection = playerManager.currentTarget.transform.position - playerManager.transform.position;
//        float distanceFromTarget = targetDirection.magnitude; // Use magnitude for efficiency
//        float viewableAngle = Vector3.Angle(targetDirection, playerManager.transform.right); // Use right for 2D

//        if (distanceFromTarget > playerManager.maxAttackRange)
//        {
//            playerAnimatitorManager.anim.SetFloat("Vertical", 1, 0.1f, Time.deltaTime); // Move animation
//        }
//        else
//        {
//            playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0.1f, Time.deltaTime); // Idle animation
//        }


//        if (distanceFromTarget <= playerManager.maxAttackRange)
//        {
//            return PlayerCombatState;
//        }

//        return this;
//    }

//}


