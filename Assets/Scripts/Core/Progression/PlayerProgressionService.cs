using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
///  view for player-side upgrades:
/// - Computes cost & checks caps
/// - Spends gems via CurrencyManager
/// - Levels up via PlayerUnitsModel
/// - (Optional helper) Computes current/next stats for UI using ProgressionMath
/// 
/// Not a MonoBehaviour. Construct once (e.g., in a bootstrapper).
/// </summary>
public class PlayerProgressionService
{
    public event Action<int, int, int, int> OnUnitUpgraded;
    // Args: (unitId, oldLevel, newLevel, costSpent)

    private readonly PlayerUnitsModel _units;
    private readonly CurrencyManager _currency;            // lives in scene (singleton)
    private readonly UpgradeCostSO _costConfig;            // SO with geometric/piecewise curve
    private readonly UnitsDatabaseSO _unitsDb;             // design-time unit list
    private readonly ProgressionConfigSO _progressionCfg;  // stat growth curves

    public PlayerProgressionService(PlayerUnitsModel units,
                                    CurrencyManager currency,
                                    UpgradeCostSO costConfig,
                                    UnitsDatabaseSO unitsDb,
                                    ProgressionConfigSO progressionCfg)
    {
        _units = units ?? throw new ArgumentNullException(nameof(units));
        _currency = currency ?? throw new ArgumentNullException(nameof(currency));
        _costConfig = costConfig ?? throw new ArgumentNullException(nameof(costConfig));
        _unitsDb = unitsDb ?? throw new ArgumentNullException(nameof(unitsDb));
        _progressionCfg = progressionCfg ?? throw new ArgumentNullException(nameof(progressionCfg));
    }
    // PlayerProgressionService.cs
    public void ProcessStageUnlocks()
    {
        if (_unitsDb == null || LevelManager.Instance == null)
            return;

        foreach (var def in _unitsDb.Units)
        {
            if (def == null)
                continue;

            int id = def.unitId;

            // Already unlocked → skip
            if (_units.IsUnlocked(id))
                continue;

            bool reached = LevelManager.Instance.HasReached(
                def.requiredLevelIndex,
                def.requiredStageIndexWithinLevel
            );

            if (!reached)
                continue;

            // 🔓 Unlock at runtime → Undeployed
            _units.Unlock(id);
            _units.SetDeployed(id, false);

            Debug.Log($"[Progression] Runtime unlocked unit: {def.displayName}");
        }
    }
    public List<int> GetReachableButLockedUnits()
    {
        var result = new List<int>();

        if (_unitsDb == null || LevelManager.Instance == null)
            return result;

        foreach (var def in _unitsDb.Units)
        {
            if (def == null)
                continue;

            int id = def.unitId;

            // Skip already unlocked
            if (_units.IsUnlocked(id))
                continue;

            bool reached = LevelManager.Instance.HasReached(
                def.requiredLevelIndex,
                def.requiredStageIndexWithinLevel
            );

            if (reached)
                result.Add(id);
        }

        return result;
    }



    #region Queries

    public int GetLevel(int unitId) => _units.GetLevel(unitId);

    public bool IsUnlocked(int unitId) => _units.IsUnlocked(unitId);

    public bool IsAtCap(int unitId)
    {
        int lvl = _units.GetLevel(unitId);
        return _costConfig != null && _costConfig.IsAtCap(lvl);
    }

    /// <summary>
    /// Cost to upgrade from current level → next. Returns 0 if at cap.
    /// </summary>
    public int GetUpgradeCost(int unitId)
    {
        int lvl = _units.GetLevel(unitId);
        return _costConfig != null ? _costConfig.GetCostForLevel(lvl) : 0;
    }

    /// <summary>
    /// Returns true if the unit can upgrade right now, and a short reason if not.
    /// Reasons: "Locked", "AtCap", "NotEnoughGems".
    /// </summary>
    // PlayerProgressionService.cs
    public bool CanUpgrade(int unitId, out string reason)
    {
        reason = null;

        if (!IsUnlocked(unitId)) { reason = "Locked"; return false; }
        if (IsAtCap(unitId)) { reason = "AtCap"; return false; }

        int cost = GetUpgradeCost(unitId);
        if (_currency == null || _currency.Coins < cost)
        {
            reason = "NotEnoughCoins";
            return false;
        }
        return true;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Performs the upgrade if possible:
    /// - spends gems
    /// - increments level
    /// - fires OnUnitUpgraded
    /// Returns true on success.
    /// </summary>
    public bool TryUpgrade(int unitId)
    {
        if (!IsUnlocked(unitId)) return false;
        if (IsAtCap(unitId)) return false;

        int oldLevel = _units.GetLevel(unitId);
        int cost = GetUpgradeCost(unitId);


        if (!_currency.TrySpendCoins(cost)) return false;


        int newLevel = _units.LevelUp(unitId);

        // Persist the new level immediately
        SaveSystem.SetUnitLevel(unitId, newLevel);


        // Fire event for UI to refresh, SFX/VFX, etc.
        try { OnUnitUpgraded?.Invoke(unitId, oldLevel, newLevel, cost); }
        catch (Exception e) { Debug.LogException(e); }

        // (Save hook goes here later, when SaveSystem is wired)

        return true;
    }

    /// <summary>
    /// Unlock a unit (no cost here; call from your unlock flow).
    /// </summary>
    public void UnlockUnit1(int unitId)
    {
        if (!IsUnlocked(unitId))
        {
            _units.Unlock(unitId);
            // (Save hook goes here later)
            SaveSystem.SetUnitUnlocked(unitId, true);

        }
    }

    public void UnlockUnit(int unitId)
    {
        if (!IsUnlocked(unitId))
        {
            _units.Unlock(unitId);
            SaveSystem.SetUnitUnlocked(unitId, true); // NEW: Persist immediately
        }
    }

    #endregion

    #region UI Helpers: Stat snapshots (current, next, delta)

    public struct StatSnapshot
    {
        public float current;
        public float next;
        public float delta => next - current;
    }

    public struct UnitStatsSnapshot
    {
        public StatSnapshot attack, defense, hp, attackSpeed, moveSpeed, attackRange;
    }

    /// <summary>
    /// Computes current and next-level stats for UI (green +Δ) using:
    /// base (UnitStatsSO) × growth multipliers from ProgressionMath.
    /// </summary>
    public bool TryGetUnitStatsSnapshot(int unitId, out UnitStatsSnapshot snap)
    {
        snap = default;

        var def = _unitsDb?.GetById(unitId);
        if (def == null || def.baseStats == null || _progressionCfg == null) return false;

        int L = Mathf.Max(1, _units.GetLevel(unitId));

        var gL = ProgressionMath.GetGrowthMultipliers(L, _progressionCfg);
        var gL1 = ProgressionMath.GetGrowthMultipliers(L + 1, _progressionCfg);

        // current
        float atkC = def.baseStats.attack * gL.gA;
        float defC = def.baseStats.defense * gL.gD;
        float hpC = def.baseStats.maxHP * gL.gH;
        float asC = def.baseStats.attackSpeed * gL.gAS;
        float mvC = def.baseStats.moveSpeed * gL.gMv;
        float rgC = def.baseStats.attackRange * gL.gR;

        // next
        float atkN = def.baseStats.attack * gL1.gA;
        float defN = def.baseStats.defense * gL1.gD;
        float hpN = def.baseStats.maxHP * gL1.gH;
        float asN = def.baseStats.attackSpeed * gL1.gAS;
        float mvN = def.baseStats.moveSpeed * gL1.gMv;
        float rgN = def.baseStats.attackRange * gL1.gR;

        snap.attack = new StatSnapshot { current = atkC, next = atkN };
        snap.defense = new StatSnapshot { current = defC, next = defN };
        snap.hp = new StatSnapshot { current = hpC, next = hpN };
        snap.attackSpeed = new StatSnapshot { current = asC, next = asN };
        snap.moveSpeed = new StatSnapshot { current = mvC, next = mvN };
        snap.attackRange = new StatSnapshot { current = rgC, next = rgN };

        return true;
    }

    #endregion
}
