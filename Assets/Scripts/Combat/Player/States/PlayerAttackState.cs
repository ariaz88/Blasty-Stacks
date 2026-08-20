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
