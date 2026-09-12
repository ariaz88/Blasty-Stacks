using UnityEngine;

public class EnemyStats : CharacterStats
{
    public EnemyManager enemyManager;
    public HealthBar healthBar;
    public bool enemyIsdead;

    public Transform EnemyOffsetLeft;   // enemy's left side (negative local X)
    public Transform EnemyOffsetRight;  // enemy's right side (positive local X)

    /// <summary>Live defence, so the toughness band follows stage growth.</summary>
    protected override float Defense =>
        enemyManager != null && enemyManager.unitStats != null ? enemyManager.unitStats.defense : 0f;

    private void Awake()
    {
        enemyManager = GetComponent<EnemyManager>();
    }
    void Start()
    {
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
    /// <summary>
    /// <paramref name="attacker"/> is the hero that swung. Needed because the
    /// stage 1-5 model lets a lone, outnumbered protected hero kill in two hits,
    /// and that boost must apply to THAT hero's blows only - never to its allies',
    /// which stay completely unassisted. Optional, so any older call still compiles.
    /// </summary>
    public void ApplyDamageToEnemy(float damageAmount, PlayerManager attacker = null)
    {
        damageAmount = CPBattleController.AdjustIncomingDamage(this, damageAmount, attacker);
        // LAST, and outside the CP battle on purpose: the four-hit rule must hold
        // even when no battle is prepared or this enemy never reached the
        // controller's registered set. See CharacterStats.ClampIncomingBlow.
        damageAmount = ClampIncomingBlow(damageAmount, attacker);
        SetResolvedHealth(Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount)));
    }

    public void SetResolvedHealth(float hp)
    {
        currentHP = Mathf.Clamp(hp, 0, maxHealth);
        if (healthBar) healthBar.SetCurrentHealth(currentHP, maxHealth);

        if (currentHP <= 0)
        {
            if (!enemyIsdead) ReportDeath();
            enemyIsdead = true;
            currentHP = 0;
        }
    }

}
