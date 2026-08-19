using System;
using UnityEngine;
using System.Collections;

/// <summary>
/// Applies the player's upgrade level to this character's runtime stats.
/// - On start (and when upgrades happen), computes: base stats × growth(level)
/// - Exposes the computed stats via CurrentStats (UnitStatsRuntime)
/// - Optionally pushes HP into a found health component (EnemyStats or any component with maxHealth/currentHP fields)
/// 
/// Attach to your player character root (or a dedicated "PlayerSystems" object in the gameplay scene).
/// </summary>


//[DefaultExecutionOrder(-50)]
public class PlayerStatsApplier : MonoBehaviour
{
    [Header("Design-Time Data (assign in Inspector)")]
    [SerializeField] private UnitsDatabaseSO unitsDatabase;
    [SerializeField] private ProgressionConfigSO progressionConfig;

    [Header("Which Unit to Apply")]
    [Tooltip("Unit ID whose saved level should drive this character's stats. Example: Warrior = 0.")]
    public int unitId = 0;

    /// <summary>Latest computed runtime stats after growth is applied.</summary>
    public UnitStatsRuntime CurrentStats { get; private set; }

    private GameStartManager _gsm;
    private PlayerManager playerManager;
    private void Awake1()
    {
        _gsm = GameStartManager.Instance;
        if (_gsm == null)
        {
            Debug.LogError("[PlayerStatsApplier] GameStartManager.Instance not found in scene. " +
                           "Add 'Game Start Manager' to your Managers and assign data.");
        }
        playerManager = GetComponent<PlayerManager>();
        ApplyNow(); // ensure stats are correct when the scene starts

    }

    private void Awake()
    {
        // Existing: playerManager = GetComponent<PlayerManager>();  

        _gsm = GameStartManager.Instance;
        if (_gsm == null)
        {
            _gsm = FindObjectOfType<GameStartManager>();
            if (_gsm == null)
            {
                Debug.LogError("[PlayerStatsApplier] GameStartManager missing.");
                enabled = false;
                return;
            }
        }

        // NEW: Defer ApplyNow if services not initialized  
        if (_gsm.PlayerUnits == null || _gsm.unitsDatabase == null || progressionConfig == null)
        {
            StartCoroutine(WaitForServicesAndApply());
        }
        //else
        //{
        //    ApplyNow();
        //}
    }

    // NEW: Coroutine to wait for readiness  
    private IEnumerator WaitForServicesAndApply()
    {
        yield return new WaitUntil(() => _gsm != null && _gsm.PlayerUnits != null && _gsm.unitsDatabase != null && progressionConfig != null);
        ApplyNow();
    }

    private void OnEnable()
    {
        // Subscribe to upgrade events so this character updates instantly on level-ups
        if (_gsm != null && _gsm.ProgressionService != null)
            _gsm.ProgressionService.OnUnitUpgraded += HandleUnitUpgraded;
    }

    private void OnDisable()
    {
        if (_gsm != null && _gsm.ProgressionService != null)
            _gsm.ProgressionService.OnUnitUpgraded -= HandleUnitUpgraded;
    }

    private void Start()
    {
        //ApplyNow(); // ensure stats are correct when the scene starts
    }

    /// <summary>
    /// Event handler: when any unit upgrades, if it's our unitId, recompute and apply stats.
    /// </summary>
    private void HandleUnitUpgraded(int upgradedUnitId, int oldLevel, int newLevel, int cost)
    {
        if (upgradedUnitId == unitId)
            ApplyNow();
    }

    /// <summary>
    /// Computes this character's runtime stats based on:
    ///   base stats (UnitDefinitionSO → UnitStatsSO) × growth multipliers for saved Level L,
    /// then stores them in CurrentStats and attempts to push HP into a health component.
    /// 
    /// Steps:
    /// 1) Resolve UnitDefinitionSO by unitId from UnitsDatabaseSO.
    /// 2) Read current player level L from GameStartManager.PlayerUnits.
    /// 3) Get growth multipliers via ProgressionMath.GetGrowthMultipliers(L, progressionConfig).
    /// 4) Build a fresh UnitStatsRuntime from base SO and multiply each stat by its growth.
    /// 5) Optionally push maxHP/currentHP into a health component if one exists.
    /// </summary>
    public void ApplyNow()
    {
        //if (_gsm == null || _gsm.PlayerUnits == null || unitsDatabase == null || progressionConfig == null)
        //{
        //    Debug.LogWarning("[PlayerStatsApplier] Missing references (GSM/UnitsDB/ProgressionConfig). Cannot apply stats.");
        //    return;
        //}

        if (_gsm == null || _gsm.PlayerUnits == null || unitsDatabase == null || progressionConfig == null)
        {
            Debug.LogWarning("[PlayerStatsApplier] Dependencies missing—deferring ApplyNow.");
            StartCoroutine(WaitForServicesAndApply());
            return;
        }

        var def = unitsDatabase.GetById(unitId);
        if (def == null || def.baseStats == null)
        {
            Debug.LogWarning($"[PlayerStatsApplier] UnitDefinition or baseStats missing for unitId={unitId}.");
            return;
        }

        int L = Mathf.Max(1, _gsm.PlayerUnits.GetLevel(unitId));
        var g = ProgressionMath.GetGrowthMultipliers(L, progressionConfig);

        // Build fresh runtime stats from base SO
        var rt = new UnitStatsRuntime();
        rt.FromSO(def.baseStats);

        // Apply growth to ALL six stats (matches your extended ProgressionMath)
        rt.attack *= g.gA;
        rt.defense *= g.gD;
        rt.maxHP *= g.gH;
        rt.attackSpeed *= g.gAS;
        rt.moveSpeed *= g.gMv;
        rt.attackRange *= g.gR;

        CurrentStats = rt;
        //playerManager.unitStats = CurrentStats;
        // NEW: Null check for playerManager  
        //if (playerManager != null)
        //{
        //    playerManager.unitStats = CurrentStats;
        //}
        //else
        //{
        //    Debug.LogWarning("[PlayerStatsApplier] PlayerManager missing—attach to same GameObject.");
        //}

        // Try to push HP into a health component if present
        TryApplyHealth(rt.maxHP);
    }

    /// <summary>
    /// Attempts to push HP into a known health component on this GameObject:
    /// - If component 'EnemyStats' exists: sets maxHealth/currentHP
    /// - Otherwise, tries to find any component with float fields 'maxHealth' and 'currentHP'
    /// (best-effort; safe no-op if nothing matches)
    /// </summary>
    private void TryApplyHealth(float newMaxHp)
    {
        // Case 1: Your existing EnemyStats component (as seen in your code snippet)
        var playerStats = GetComponentInChildren<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.maxHealth = newMaxHp;
            playerStats.currentHP = Mathf.Min(playerStats.currentHP, newMaxHp);
            if (playerStats.currentHP <= 0f) playerStats.currentHP = newMaxHp; // reset if dead/zero
            return;
        }

        // Case 2: Generic fallback via reflection on a component named "*Health*"
        // (kept minimal; you can replace with your actual player health component later)
        //var comps = GetComponentsInChildren<MonoBehaviour>();
        //foreach (var c in comps)
        //{
        //    if (c == null) continue;
        //    var t = c.GetType();
        //    if (!t.Name.ToLower().Contains("health")) continue;

        //    var maxField = t.GetField("maxHealth");
        //    var curField = t.GetField("currentHP");
        //    if (maxField != null && curField != null &&
        //        maxField.FieldType == typeof(float) &&
        //        curField.FieldType == typeof(float))
        //    {
        //        maxField.SetValue(c, newMaxHp);
        //        float cur = (float)curField.GetValue(c);
        //        cur = Mathf.Min(cur, newMaxHp);
        //        if (cur <= 0f) cur = newMaxHp;
        //        curField.SetValue(c, cur);
        //        return;
        //    }
        //}
        // If nothing found, it's fine—your combat scripts can read CurrentStats instead.
    }
    public void SetUnitId(int id)
    {
        unitId = id;
    }

}
