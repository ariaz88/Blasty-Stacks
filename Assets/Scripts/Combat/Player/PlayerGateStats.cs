using System;
using System.Collections;
using UnityEngine;
using TMPro;

public class PlayerGateStats : CharacterStats
{
    public static event Action OnGateDestroyed;   // <- broadcast to everyone
    public PlayerManager playerManager;
    public HealthBar healthBar;
    public bool isPlayerGateDestroyed;
    [SerializeField] TextMeshProUGUI baseTxt;

   

    private void Awake()
    {
        playerManager = GetComponent<PlayerManager>();
    }
    void Start()
    {
        if (healthBar != null)
        {
            healthBar.SetCurrentHealth(currentHP, maxHealth);

        }
        baseTxt.text = currentHP.ToString();


    }

    public void ApplyDamageToPlayerGate(float damageAmount)
    {
        if (isPlayerGateDestroyed)
        {
            return;
        }
        currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount));
        baseTxt.text = Mathf.FloorToInt(currentHP).ToString();
        healthBar.SetCurrentHealth(currentHP, maxHealth);
        if (currentHP <= 0)
        {
            DestroyGate();
            currentHP = 0;
        }
    }
    private void DestroyGate()
    {
        if (isPlayerGateDestroyed)
        {
            return;
        }
        isPlayerGateDestroyed = true;
        OnGateDestroyed?.Invoke();


        //// optional: turn off collider to stop further triggers

        ////if (destroyVFX) Instantiate(destroyVFX, transform.position, Quaternion.identity);

        //// optional: hide mesh/sprite




    }

    IEnumerator DeactivateGate()
    {
        yield return new WaitForSeconds(0.5f);

        gameObject.SetActive(false);
    }
}
