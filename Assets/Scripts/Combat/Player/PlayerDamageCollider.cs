using UnityEngine;
public class PlayerDamageCollider : MonoBehaviour
{
    private PlayerManager playerManager;
    private float damageToEnemy;

    private void Awake()
    {
        playerManager = GetComponentInParent<PlayerManager>();
    }

    private void OnTriggerEnter2D1(Collider2D other)
    {


        EnemyGateStats enemyGateStats = other.GetComponentInParent<EnemyGateStats>();
        EnemyStats enemyStats = other.GetComponent<EnemyStats>();

        if (enemyStats != null)
        {
            damageToEnemy = playerManager.DamageApplying(enemyStats.enemyManager);
            enemyStats.ApplyDamageToEnemy(damageToEnemy);

        }

        if (enemyGateStats != null)
        {
            damageToEnemy = playerManager.DamageApplying(enemyGateStats.enemyManager);
            enemyGateStats.ApplyDamageToEnemy(damageToEnemy);
        }

    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // We expect to hit ENEMY BODY, not enemy hitbox
        if (other.gameObject.layer != LayerMask.NameToLayer("EnemyLayer")
            && other.gameObject.layer != LayerMask.NameToLayer("EnemyCastle"))
            return;

        EnemyStats enemyStats = other.GetComponent<EnemyStats>();
        if (enemyStats != null /*&& playerManager.currentState == playerManager.AttackState*/ )
        {
            float dmg = playerManager.DamageApplying(enemyStats.enemyManager);
            enemyStats.ApplyDamageToEnemy(dmg);
            return;
        }

        EnemyGateStats gateStats = other.GetComponentInParent<EnemyGateStats>();
        if (gateStats != null && playerManager.currentState == playerManager.AttackState )
        {
            float dmg = playerManager.DamageApplying(gateStats.enemyManager);
            gateStats.ApplyDamageToEnemy(dmg);
        }
    }


}

public class PlayerDamageCollider1 : MonoBehaviour
{
    PlayerManager PlayerManager;

    private float damageToEnemy ;

    private void Awake()
    {
        PlayerManager = GetComponent<PlayerManager>();
    }


    public void OnTriggerEnter2D(Collider2D other)
    {
        
        EnemyStats enemyStats = other.GetComponent<EnemyStats>();
        //EnemyGateStats enemyGateStats = other.GetComponentInChildren<EnemyGateStats>();
        EnemyGateStats enemyGateStats = other.GetComponent<EnemyGateStats>();

        if (enemyStats != null)
        {
            damageToEnemy = PlayerManager.DamageApplying(enemyStats.enemyManager);
            enemyStats.ApplyDamageToEnemy(damageToEnemy);

        }
        if (enemyGateStats != null)
        {
            damageToEnemy = PlayerManager.DamageApplying(enemyGateStats.enemyManager);
            enemyGateStats.ApplyDamageToEnemy(damageToEnemy);
        }
    }

}
