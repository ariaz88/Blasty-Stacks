using System;
using UnityEngine;
using TMPro;


public class EnemyGateStats : CharacterStats
{
    public static event Action OnGateDestroyed;

    public EnemyManager enemyManager;
    public HealthBar healthBar;
    public bool isDestroyed;

    public Transform EnemyOffsetLeft;
    public Transform EnemyOffsetRight;
    [SerializeField] TextMeshProUGUI baseTxt;


    private void Awake()
    {
        enemyManager = GetComponent<EnemyManager>();
    }

    private void Start()
    {
        if (healthBar != null)
        {
            healthBar.SetCurrentHealth(currentHP, maxHealth);
        }
        baseTxt.text = currentHP.ToString();

    }

    public Transform GetOffsetFacingPlayer(Vector2 playerPos)
    {
        if (EnemyOffsetLeft != null && EnemyOffsetRight != null)
        {
            float dL = ((Vector2)EnemyOffsetLeft.position - playerPos).sqrMagnitude;
            float dR = ((Vector2)EnemyOffsetRight.position - playerPos).sqrMagnitude;
            return (dL <= dR) ? EnemyOffsetLeft : EnemyOffsetRight;
        }

        if (EnemyOffsetLeft != null) return EnemyOffsetLeft;
        if (EnemyOffsetRight != null) return EnemyOffsetRight;

        return this.transform;
    }

    public void ApplyDamageToEnemy(float damageAmount)
    {
        if (isDestroyed)
            return;

        currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount));
        baseTxt.text = Mathf.FloorToInt(currentHP).ToString();

        if (healthBar != null)
            healthBar.SetCurrentHealth(currentHP, maxHealth);

        if (currentHP <= 0f)
        {
            currentHP = 0f;
            HandleCastleDestroyed();
        }
    }

    private void HandleCastleDestroyed1()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Optional VFX/SFX here

        // Tell listeners (LevelGameManager)
        OnGateDestroyed?.Invoke();

        // Disable collider / sprite if you want here

        Destroy(gameObject, 0.1f);
    }
    private void HandleCastleDestroyed()
    {
        if (isDestroyed) return;
        isDestroyed = true;


        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = false;


        OnGateDestroyed?.Invoke();


        Destroy(gameObject, 0.1f);

    }


}
