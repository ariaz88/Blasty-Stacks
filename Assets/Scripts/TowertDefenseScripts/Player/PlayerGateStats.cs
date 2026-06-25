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
        //maxHealth = enemyManager.statsBase.maxHP;
        //currentHP = maxHealth;
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
            //isDestroyed = true;
            DestroyGate();
            currentHP = 0;
            //Destroy(gameObject);
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
        //var col = GetComponent<Collider2D>();
        //if (col) col.enabled = false;

        ////if (destroyVFX) Instantiate(destroyVFX, transform.position, Quaternion.identity);

        //// optional: hide mesh/sprite
        //var sr = GetComponent<SpriteRenderer>();
        //if (sr) sr.enabled = false;

        //StartCoroutine(DeactivateGate());

        //Destroy(gameObject, 0.1f);


    }

    IEnumerator DeactivateGate()
    {
        yield return new WaitForSeconds(0.5f);

        gameObject.SetActive(false);
    }
}
