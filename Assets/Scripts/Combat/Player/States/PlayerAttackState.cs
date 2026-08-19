using UnityEngine;

public class PlayerAttackState : PlayerState
{
    public PlayerDeathState PlayerDeathState;
    public PlayerCombatState PlayerCombatState;

    public PlayerAttackAction playerAttackAction;
    private PlayerAttackAction currentAttack;
    public PlayerLockState playerLockState;
    public PlayerGameCompleteState PlayerGameCompleteState; // <-- NEW


    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        // Lock gate
        if (playerLockState && !pm.isUnlocked)
            return playerLockState;

        // --- Pick which targeting path we’re on ---
        bool gateEngagement = pm.attackPlayerGate && pm.currentGateTarget != null;

        //// Keep/refresh regular target when NOT in a gate engagement
        //if (!gateEngagement)
        //{
        //    if (!pm.targetDetectionForPlayer.EnsureTarget(true))
        //        return PlayerCombatState; // no valid enemy to attack → leave Attack state
        //}

        if (!gateEngagement && pm.currentTarget == null)
        {
            return PlayerCombatState; 
        }




        // Safety: dead/self-states etc.
        if (pm.isPerformingAction) return PlayerCombatState;
        if (ps.playerIsdead) return PlayerDeathState;

        // Distance/angle to the *active* target (gate or enemy)
        Vector3 targetPos = pm.GetActiveTargetPosition();
        float distanceFromTarget = Vector3.Distance(targetPos, pm.transform.position);

        Vector3 targetDirection = targetPos - pm.transform.position;
        float viewableAngle = Vector3.Angle(targetDirection, pm.transform.forward);



        //staying in AttackState with a far target        
        //the “stands still after kill” bug
        if (!gateEngagement && distanceFromTarget > pm.maxAttackRange)
        {
            currentAttack = null;
            return PlayerCombatState;
        }




        // Ensure we have an attack selected
        if (currentAttack == null)
            GetNewAttack(pm, gateEngagement);

        if (currentAttack != null)
        {
            // --- GATE ATTACK PATH (only for EnemyGateStats target) ---
            if (gateEngagement)
            {
                // Optional: clamp movement before attacking
                if (pm.playerRigidbody)
                {
                    pm.playerRigidbody.linearVelocity = Vector2.zero;
                    pm.playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
                }

                if (pm.currentRecoveryTimer <= 0 && !pm.isPerformingAction)
                {
                    pm.isPerformingAction = true;
                    pm.currentRecoveryTimer = currentAttack.recoveryTime;

                    // neutralize locomotion params
                    pm.playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0, 0);
                    pm.playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);

                    float speedMultiplier = pm.unitStats.attackSpeed;
                    pm.playerAnimatitorManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);

                    // After damage, if destroyed → complete
                    if (pm.currentGateTarget.isDestroyed)
                        return PlayerGameCompleteState;

                    // consume the gate flag if you want single-shot
                    // pm.attackGate = false;
                }

                return this;
            }

            // --- REGULAR ENEMY ATTACK PATH ---
            if (distanceFromTarget < currentAttack.maxDistanceNeededToAttack)
            {
                if (pm.currentRecoveryTimer <= 0 && !pm.isPerformingAction)
                {
                    pm.isPerformingAction = true;
                    pm.currentRecoveryTimer = currentAttack.recoveryTime;

                    pm.playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0, 0);
                    pm.playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);

                    float speedMultiplier = pm.unitStats.attackSpeed;
                    pm.playerAnimatitorManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);

                    return this;
                }

                // Angle gating if you want it (currently not used)
                // if (viewableAngle <= currentAttack.maxAttackAngle && viewableAngle >= currentAttack.minAttackAngle) { ... }
            }
        }
        else
        {
            // Couldn’t get an attack → try again next tick
            GetNewAttack(pm, gateEngagement);
        }

        return this;
    }

    public void GetNewAttack(PlayerManager pm, bool gateEngagement)
    {
        if (pm == null) { currentAttack = null; return; }

        // A) Gate-triggered attack takes priority ONLY when we actually have a gate target
        if (gateEngagement)
        {
            currentAttack = playerAttackAction;
            return;
        }

        // B) Normal target-based selection
        if (pm.currentTarget == null) { currentAttack = null; return; }

        Vector3 enemyPos = pm.currentTarget.transform.position;
        float distance = Vector3.Distance(enemyPos, pm.transform.position);

        if (distance < playerAttackAction.maxDistanceNeededToAttack &&
            distance >= playerAttackAction.minDistanceNeededToAttack)
        {
            pm.playerAnimatitorManager.anim.SetFloat("Vertical", 0f, 0f, 0f);
            pm.playerAnimatitorManager.anim.SetFloat("Horizontal", 0f, 0f, 0f);

            currentAttack = playerAttackAction;
        }
        else
        {
            currentAttack = null; // no valid attack at this distance yet
        }
    }
}


public class PlayerAttackState2 : PlayerState
{
    public PlayerDeathState PlayerDeathState;
    public PlayerCombatState PlayerCombatState;

    public PlayerAttackAction playerAttackAction;
    private PlayerAttackAction currentAttack;
    public PlayerLockState playerLockState;
    public PlayerGameCompleteState PlayerGameCompleteState; // <-- NEW


    public override PlayerState Tick(PlayerManager pm, PlayerStats ps, PlayerAnimatitorManager am)
    {
        // Lock gate
        if (playerLockState && !pm.isUnlocked)
            return playerLockState;

        // --- Pick which targeting path we’re on ---
        bool gateEngagement = pm.attackPlayerGate && pm.currentGateTarget != null;

        // Keep/refresh regular target when NOT in a gate engagement
        if (!gateEngagement)
        {
            if (!pm.targetDetectionForPlayer.EnsureTarget(true))
                return PlayerCombatState; // no valid enemy to attack → leave Attack state
        }

        // Safety: dead/self-states etc.
        if (pm.isPerformingAction) return PlayerCombatState;
        if (ps.playerIsdead) return PlayerDeathState;

        // Distance/angle to the *active* target (gate or enemy)
        Vector3 targetPos = pm.GetActiveTargetPosition();
        float distanceFromTarget = Vector3.Distance(targetPos, pm.transform.position);

        Vector3 targetDirection = targetPos - pm.transform.position;
        float viewableAngle = Vector3.Angle(targetDirection, pm.transform.forward);

        // Ensure we have an attack selected
        if (currentAttack == null)
            GetNewAttack(pm, gateEngagement);

        if (currentAttack != null)
        {
            // --- GATE ATTACK PATH (only for EnemyGateStats target) ---
            if (gateEngagement)
            {
                // Optional: clamp movement before attacking
                if (pm.playerRigidbody)
                {
                    pm.playerRigidbody.linearVelocity = Vector2.zero;
                    pm.playerRigidbody.bodyType = RigidbodyType2D.Kinematic;
                }

                if (pm.currentRecoveryTimer <= 0 && !pm.isPerformingAction)
                {
                    pm.isPerformingAction = true;
                    pm.currentRecoveryTimer = currentAttack.recoveryTime;

                    // neutralize locomotion params
                    pm.playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0, 0);
                    pm.playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);

                    float speedMultiplier = pm.unitStats.attackSpeed;
                    pm.playerAnimatitorManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);

                    // After damage, if destroyed → complete
                    if (pm.currentGateTarget.isDestroyed)
                        return PlayerGameCompleteState;

                    // consume the gate flag if you want single-shot
                    // pm.attackGate = false;
                }

                return this;
            }

            // --- REGULAR ENEMY ATTACK PATH ---
            if (distanceFromTarget < currentAttack.maxDistanceNeededToAttack)
            {
                if (pm.currentRecoveryTimer <= 0 && !pm.isPerformingAction)
                {
                    pm.isPerformingAction = true;
                    pm.currentRecoveryTimer = currentAttack.recoveryTime;

                    pm.playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0, 0);
                    pm.playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);

                    float speedMultiplier = pm.unitStats.attackSpeed;
                    pm.playerAnimatitorManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);

                    return this;
                }

                // Angle gating if you want it (currently not used)
                // if (viewableAngle <= currentAttack.maxAttackAngle && viewableAngle >= currentAttack.minAttackAngle) { ... }
            }
        }
        else
        {
            // Couldn’t get an attack → try again next tick
            GetNewAttack(pm, gateEngagement);
        }

        return this;
    }

    public void GetNewAttack(PlayerManager pm, bool gateEngagement)
    {
        if (pm == null) { currentAttack = null; return; }

        // A) Gate-triggered attack takes priority ONLY when we actually have a gate target
        if (gateEngagement)
        {
            currentAttack = playerAttackAction;
            return;
        }

        // B) Normal target-based selection
        if (pm.currentTarget == null) { currentAttack = null; return; }

        Vector3 enemyPos = pm.currentTarget.transform.position;
        float distance = Vector3.Distance(enemyPos, pm.transform.position);

        if (distance < playerAttackAction.maxDistanceNeededToAttack &&
            distance >= playerAttackAction.minDistanceNeededToAttack)
        {
            pm.playerAnimatitorManager.anim.SetFloat("Vertical", 0f, 0f, 0f);
            pm.playerAnimatitorManager.anim.SetFloat("Horizontal", 0f, 0f, 0f);

            currentAttack = playerAttackAction;
        }
        else
        {
            currentAttack = null; // no valid attack at this distance yet
        }
    }
}

public class PlayerAttackState1 : PlayerState
{
    public PlayerDeathState PlayerDeathState;
    public PlayerCombatState PlayerCombatState;

    public PlayerAttackAction playerAttackAction;
    PlayerAttackAction currentAttack;
    public PlayerLockState playerLockState;
    public override PlayerState Tick(PlayerManager playerManager, PlayerStats playerStats, PlayerAnimatitorManager playerAnimatitorManager)
    {
        // Select one of the attacks
        // if the selected attack is not usable because of distance and angle select another attack
        // if the attack is viable , stop the moveemnt and attack  our target

        // set teh attack  recovery timer to the attacks recovery time 

        // retuen to the combat state

        if (playerLockState && !playerManager.isUnlocked)
        {
            return playerLockState;
        }

        if (!playerManager.targetDetectionForPlayer.EnsureTarget(true))
            return PlayerCombatState;

        float distanceFromTarget = Vector3.Distance(playerManager.currentTarget.transform.position, playerManager.transform.position);

        Vector3 targetDirection = playerManager.currentTarget.transform.position - playerManager.transform.position;
        float viewableAngle = Vector3.Angle(targetDirection, playerManager.transform.forward);

        if (playerManager.isPerformingAction)
        {
            return PlayerCombatState;
        }
        if (playerStats.playerIsdead)
        {
          
            //return deathState;

        }

        //if (playerManager.currentTarget == null)
        //{
        //    return PlayerDeathState;
        //}
        if (currentAttack != null)
        {
            //if (distanceFromTarget < currentAttack.minDistanceNeededToAttack)
            //{
            //    return this;
            //}
            if (playerManager.attackPlayerGate)
            {
                playerManager.isPerformingAction = true;
                playerManager.currentRecoveryTimer = currentAttack.recoveryTime;
                playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0, 0);
                playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);
                var anim = playerAnimatitorManager.anim;

                float speedMultiplier = playerManager.unitStats.attackSpeed;

                playerAnimatitorManager.PlayTargetAnimation(currentAttack.animationName, true, speedMultiplier);

                playerManager.attackPlayerGate = false;            // consume once (avoid “sticky” attack)

                //return PlayerCombatState;
                return this;
            }
            if (distanceFromTarget < currentAttack.maxDistanceNeededToAttack)
            {
                if (playerManager.currentRecoveryTimer <= 0 && playerManager.isPerformingAction == false)
                {


                    //currentAttack = null;
                    playerManager.isPerformingAction = true;
                    playerManager.currentRecoveryTimer = currentAttack.recoveryTime;
                    playerAnimatitorManager.anim.SetFloat("Vertical", 0, 0, 0);
                    playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);
                    var anim = playerAnimatitorManager.anim;

                    float speedMultiplier = playerManager.unitStats.attackSpeed;

                    playerAnimatitorManager.PlayTargetAnimation(currentAttack.animationName, true , speedMultiplier);
                   

                    //return PlayerCombatState;
                    return this;

                }

                if (viewableAngle <= currentAttack.maxAttackAngle && viewableAngle >= currentAttack.minAttackAngle)
                {
                    

                }

            }
        }
        else
        {
            GetNewAttack(playerManager);
           
        }

        return this;
    }

    public void GetNewAttack(PlayerManager playerManager )
    {
        if (playerManager == null) return;

        // A) Gate-triggered attack takes priority
        if (playerManager.attackPlayerGate)
        {
            currentAttack = playerAttackAction;
            //playerManager.attackGate = false;            // consume once (avoid “sticky” attack)
            return;
        }

        // B) Normal target-based selection
        if (playerManager.currentTarget == null) { currentAttack = null; return; }

        Vector3 targetDirection = playerManager.currentTarget.transform.position - transform.position;
        float viewableAngle = Vector3.Angle(targetDirection, transform.forward);
        float  distanceFromTarget = Vector3.Distance(playerManager.currentTarget.transform.position, transform.position);

        if (distanceFromTarget < playerAttackAction.maxDistanceNeededToAttack &&
            distanceFromTarget >= playerAttackAction.minDistanceNeededToAttack)
        {
            playerManager.playerAnimatitorManager.anim.SetFloat("Vertical", 0f, 0f, 0f);
            playerManager.playerAnimatitorManager.anim.SetFloat("Horizontal", 0, 0, 0);

            currentAttack = playerAttackAction;

            //if (viewableAngle >= playerAttackAction.minAttackAngle && viewableAngle <= playerAttackAction.maxAttackAngle)
            //{
            //    if (currentAttack != null)
            //    {
            //        return;
            //    }

            //    playerManager.playerAnimatitorManager.anim.SetFloat("Vertical", 0f, 0f, 0f);
            //    currentAttack = playerAttackAction;

            //}
        }

    }

}
