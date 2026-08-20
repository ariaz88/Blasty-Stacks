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
        healthBar.SetCurrentHealth(currentHP, maxHealth);

    }


    public void ApplyDamageToPlayer(float damageAmount)
    {
        currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount));
        healthBar.SetCurrentHealth(currentHP , PlayerManager.statsBase.maxHP);
        if (currentHP <= 0)
        {
            currentHP = 0;
            playerIsdead = true;
           
        }
    }

}
