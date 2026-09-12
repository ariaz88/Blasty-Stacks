using UnityEngine;

public class PlayerStats : CharacterStats
{
    public PlayerManager PlayerManager;
    public HealthBar healthBar;
    public bool playerIsdead;

    /// <summary>Live defence, so upgrades move this hero up the toughness band.</summary>
    protected override float Defense =>
        PlayerManager != null && PlayerManager.unitStats != null ? PlayerManager.unitStats.defense : 0f;
    private void Awake()
    {
        PlayerManager = GetComponent<PlayerManager>();
    }
    void Start()
    {
        if (healthBar) healthBar.SetCurrentHealth(currentHP, maxHealth);

    }


    /// <summary>
    /// <paramref name="attacker"/> is the enemy that swung. It matters: the stage
    /// 1-5 model gives each individual enemy its own lifetime damage allowance
    /// against the protected hero, so identical blows from two different enemies
    /// have to be accounted separately. Optional, so any older call still compiles.
    /// </summary>
    public void ApplyDamageToPlayer(float damageAmount, EnemyManager attacker = null)
    {
        damageAmount = CPBattleController.AdjustIncomingDamage(this, damageAmount, attacker);
        // LAST, and outside the CP battle on purpose: the four-hit rule must hold
        // even when no battle is prepared or this hero never reached the
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
            if (!playerIsdead) ReportDeath();
            currentHP = 0;
            playerIsdead = true;
           
        }
    }

}
