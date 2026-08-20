using UnityEngine;

public class EnemyAnimatorManager : AnimatorManager
{
    EnemyLocomotionManager locomotion;
    EnemyDamageCollider enemyDamageCollider;

    void Awake()
    {
        locomotion = GetComponentInParent<EnemyLocomotionManager>();
        enemyDamageCollider = GetComponentInChildren<EnemyDamageCollider>();
    }

    public void EnableEnemyDamageCollier()
    {
        enemyDamageCollider.enemyDmgCollider.enabled = true;

    }

    public void DisableEnemyDamageCollider()
    {
        enemyDamageCollider.enemyDmgCollider.enabled = false;

    }


















}
