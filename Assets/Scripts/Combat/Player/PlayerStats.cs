using UnityEngine;

public class PlayerStats : CharacterStats
{
    public PlayerManager PlayerManager;
    public HealthBar healthBar;
    public bool playerIsdead;
    private void Awake()
    {
        PlayerManager = GetComponent<PlayerManager>();
    }
    void Start()
    {
        if (healthBar) healthBar.SetCurrentHealth(currentHP, maxHealth);

    }


    public void ApplyDamageToPlayer(float damageAmount)
    {
        damageAmount = CPBattleController.AdjustIncomingDamage(this, damageAmount);
        SetResolvedHealth(Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount)));
    }

    public void SetResolvedHealth(float hp)
    {
        currentHP = Mathf.Clamp(hp, 0, maxHealth);
        if (healthBar) healthBar.SetCurrentHealth(currentHP, maxHealth);
        if (currentHP <= 0)
        {
            currentHP = 0;
            playerIsdead = true;
           
        }
    }

}
