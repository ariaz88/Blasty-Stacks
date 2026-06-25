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

        // If you also use a global UI manager for states, you can do:
        // GameUIManager.Instance?.ShowLevelComplete();
    }


}



public class EnemyGateStats1 : CharacterStats
{
    public static event Action OnGateDestroyed;   // <- broadcast to everyone

    public EnemyManager enemyManager;
    public HealthBar healthBar;
    public bool isDestroyed;
    [SerializeField] private WinPanel winPanel;             // drag from scene
     private PlayerGateStats playerHealth;     // or PlayerManager / PlayerStats



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
        playerHealth = FindObjectOfType<PlayerGateStats>();

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
        if (isDestroyed)
        {
            return;
        }
        currentHP = Mathf.Max(0f, currentHP - Mathf.Max(0f, damageAmount));
        healthBar.SetCurrentHealth(currentHP, maxHealth);

        if (currentHP <= 0)
        {
            //isDestroyed = true;
            HandleCastleDestroyed();
            //DestroyGate();
            currentHP = 0;
            //Destroy(gameObject);
        }
    }
    private void HandleCastleDestroyed1()
    {
        if (isDestroyed) return;
        isDestroyed = true;

   

        // 2) Compute player's HP percent for reward logic
        float hpPercent = 1f;

        if (playerHealth != null && playerHealth.maxHealth > 0)
        {
            hpPercent = Mathf.Clamp01(playerHealth.currentHP / playerHealth.maxHealth);
        }


        // NEW: record progress + queue next stage
        HomeManager.NotifyStageWon(hpPercent);


        // 3) Show WinPanel with the HP%
        if (winPanel != null)
        {
            winPanel.gameObject.SetActive(true);
            winPanel.Show(hpPercent);
        }
        else
        {
            Debug.LogWarning("[EnemyCastleHealth] WinPanel reference is missing.");
        }
        OnGateDestroyed?.Invoke();


        Destroy(gameObject, 0.1f);

        // If you also use a global UI manager for states, you can do:
        // GameUIManager.Instance?.ShowLevelComplete();
    }
    private void HandleCastleDestroyed()
    {
        if (isDestroyed) return;
        isDestroyed = true;


        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = false;

        // Optional: play VFX, SFX, disable collider, etc.

        //// 1) Freeze everything (player + enemies)
        //if (LevelCompletionManager.Instance != null)
        //{
        //    LevelCompletionManager.Instance.MarkLevelCompleted();
        //}

        // 2) Compute player's HP percent for reward logic
        //float hpPercent = 1f;

        //if (playerHealth != null && playerHealth.maxHealth > 0)
        //{
        //    hpPercent = Mathf.Clamp01(playerHealth.currentHP / playerHealth.maxHealth);
        //}


        // NEW: record progress + queue next stage
        //HomeManager.NotifyStageWon(hpPercent);


        // 3) Show WinPanel with the HP%
        //if (winPanel != null)
        //{
        //    winPanel.gameObject.SetActive(true);
        //    winPanel.Show(hpPercent);
        //}
        //else
        //{
        //    Debug.LogWarning("[EnemyCastleHealth] WinPanel reference is missing.");
        //}

        OnGateDestroyed?.Invoke();


        Destroy(gameObject, 0.1f);

        // If you also use a global UI manager for states, you can do:
        // GameUIManager.Instance?.ShowLevelComplete();
    }

    private void DestroyGate()
    {
        isDestroyed = true;
        OnGateDestroyed?.Invoke();


        // optional: turn off collider to stop further triggers
        var col = GetComponent<Collider2D>();
        if (col) col.enabled = false;

        //if (destroyVFX) Instantiate(destroyVFX, transform.position, Quaternion.identity);

        // optional: hide mesh/sprite
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.enabled = false;
        Destroy(gameObject,0.1f);

    }

 

}
