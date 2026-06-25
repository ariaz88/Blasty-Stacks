using UnityEngine;

public class EnemyStats : CharacterStats
{
    public EnemyManager enemyManager;
    public HealthBar healthBar;
    public bool enemyIsdead;

    public Transform EnemyOffsetLeft;   // enemy's left side (negative local X)
    public Transform EnemyOffsetRight;  // enemy's right side (positive local X)

    private void Awake()
    {
        enemyManager = GetComponent<EnemyManager>();
    }
    void Start()
    {
        //maxHealth = enemyManager.statsBase.maxHP;
        //currentHP = maxHealth;
        if (healthBar != null)
        {
        healthBar.SetCurrentHealth(currentHP, maxHealth);

        }

    }

    public Transform GetOffsetFacingPlayer(Vector2 playerPos)
    {
        // If both side offsets exist, pick the NEARER one to the player.
        if (EnemyOffsetLeft != null && EnemyOffsetRight != null)
        {
            float dL = ((Vector2)EnemyOffsetLeft.position - playerPos).sqrMagnitude;
            float dR = ((Vector2)EnemyOffsetRight.position - playerPos).sqrMagnitude;
            return (dL <= dR) ? EnemyOffsetLeft : EnemyOffsetRight;
        }

        // If only one side exists, use it
        if (EnemyOffsetLeft != null) return EnemyOffsetLeft;
        if (EnemyOffsetRight != null) return EnemyOffsetRight;

       
        return this.transform;
    }
    public void ApplyDamageToEnemy(float damageAmount)
    {
        currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount));
        healthBar.SetCurrentHealth(currentHP, maxHealth);

        if (currentHP <= 0)
        {
            enemyIsdead = true;
            currentHP = 0;
        }
    }

}
