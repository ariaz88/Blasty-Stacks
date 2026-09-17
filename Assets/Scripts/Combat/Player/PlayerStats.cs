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
    /// <paramref name="attacker"/> is the enemy that swung. It is kept so the
    /// per-attacker hit breakdown in CharacterStats.ReportDeath stays meaningful -
    /// a unit that looks like it died in four blows has usually taken its full
    /// eight, half of them from a neighbour. Optional, so older calls still compile.
    ///
    /// NOTHING adjusts this damage any more. The blow is taken as the attacker's
    /// stats produced it, clamped only by the pacing band in ClampIncomingBlow.
    /// </summary>
    public void ApplyDamageToPlayer(float damageAmount, EnemyManager attacker = null)
    {
        damageAmount = ClampIncomingBlow(damageAmount, attacker);
        CPBattleController.ReportBlow(this, damageAmount, attacker);   // observation only
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
