using UnityEngine;
public class EnemyDamageCollider : MonoBehaviour
{
    private EnemyManager enemyManager;
    private float damageToPlayer;

    private int playerBodyLayer;
    private int playerCastleLayer;
    public CapsuleCollider2D enemyDmgCollider;

    private void Awake()
    {
        enemyManager = GetComponentInParent<EnemyManager>();

        playerBodyLayer = LayerMask.NameToLayer("PlayerLayer");
        playerCastleLayer = LayerMask.NameToLayer("PlayerCastle");
    }

    private void Start()
    {
        if (enemyDmgCollider != null)
        {
            enemyDmgCollider.enabled = false;
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        //// Safety: only triggers can deal damage
        //if (!other.isTrigger)
        //    return;

        int otherLayer = other.gameObject.layer;

        // =========================
        // Player BODY hit
        // =========================
        if (otherLayer == playerBodyLayer)
        {
            PlayerStats playerStats = other.GetComponent<PlayerStats>();
            if (playerStats == null)
                return;

            PlayerManager pm = playerStats.PlayerManager;
            if (pm == null)
                return;

            // Optional state guard (you already had this)
            if (pm.currentState == pm.PlayerLockState)
                return;

            damageToPlayer = enemyManager.DamageApplying(pm);
            playerStats.ApplyDamageToPlayer(damageToPlayer);
            return;
        }

        // =========================
        // Player CASTLE hit
        // =========================
        if (otherLayer == playerCastleLayer)
        {
            PlayerGateStats gateStats = other.GetComponentInParent<PlayerGateStats>();
            if (gateStats == null)
                return;

            damageToPlayer = enemyManager.DamageApplying(gateStats.playerManager);
            gateStats.ApplyDamageToPlayerGate(damageToPlayer);
        }
    }
}


public class EnemyDamageCollider2 : MonoBehaviour
{
    private EnemyManager enemyManager;
    private float damageToPlayer;
    public CapsuleCollider2D enemyDamageCollider;

    private void Awake()
    {
        // look up the EnemyManager on the parent Enemy object
        enemyManager = GetComponentInParent<EnemyManager>();
    }
    private void Start()
    {
        if (enemyDamageCollider != null)
        {
            enemyDamageCollider.enabled = false;
        }
    }
    public void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats playerStats = other.GetComponent<PlayerStats>();
        PlayerGateStats playerGateStats = other.GetComponent<PlayerGateStats>();

        if (playerStats != null)
        {
            PlayerManager playerManeger = playerStats.PlayerManager;
            if (playerManeger.currentState != playerManeger.PlayerLockState)
            {
            damageToPlayer = enemyManager.DamageApplying(playerManeger);
            playerStats.ApplyDamageToPlayer(damageToPlayer);
                 
            }
        }

        if (playerGateStats != null)
        {
            damageToPlayer = enemyManager.DamageApplying(playerGateStats.playerManager);
            playerGateStats.ApplyDamageToPlayerGate(damageToPlayer);
        }
    }
}

public class EnemyDamageCollider1 : MonoBehaviour
{
    EnemyManager enemyManager;

    private float damageToPlayer;

    private void Awake()
    {
        enemyManager = GetComponent<EnemyManager>();
    }


    public void OnTriggerEnter2D(Collider2D other)
    {
        PlayerStats playerStats = other.GetComponent<PlayerStats>();
        PlayerGateStats playerGateStats = other.GetComponent<PlayerGateStats>();

        if (playerStats != null)
        {
            damageToPlayer = enemyManager.DamageApplying(playerStats.PlayerManager);

            playerStats.ApplyDamageToPlayer(damageToPlayer);
        }
        if (playerGateStats != null)
        {
            damageToPlayer = enemyManager.DamageApplying(playerGateStats.playerManager);
            playerGateStats.ApplyDamageToPlayerGate(damageToPlayer);
        }

    }
}
