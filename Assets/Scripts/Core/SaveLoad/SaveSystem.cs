// SaveSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class SaveSystem
{
    private static int saveDepth = 0;

    private static SaveData _cache;
    private const string KEY = "GAME_SAVE_V1";

    public static SaveData Data => _cache ??= LoadInternal();

    // NEW: Unit persistence helpers
    private static SaveData.UnitStateEntry GetUnitEntry(int unitId)
    {
        return Data.units.FirstOrDefault(u => u.unitId == unitId);
    }

    private static void EnsureUnitEntry(int unitId)
    {
        if (GetUnitEntry(unitId) == null)
        {
            Data.units.Add(new SaveData.UnitStateEntry(unitId, false, 1, false));
        }
    }

    // NEW: Setters for units (update cache + auto-save)
    public static void SetUnitLevel(int unitId, int level)
    {
        EnsureUnitEntry(unitId);
        var entry = GetUnitEntry(unitId);
        if (entry.level != level)
        {
            entry.level = Mathf.Max(1, level);
            Save();
            Debug.Log($"[SaveSystem] Saved unit {unitId} level {level}");
        }
    }

    public static void SetUnitUnlocked(int unitId, bool unlocked)
    {
        EnsureUnitEntry(unitId);
        var entry = GetUnitEntry(unitId);
        if (entry.unlocked != unlocked)
        {
            entry.unlocked = unlocked;
            entry.isDeployed = unlocked ? entry.isDeployed : false; // Lock → undeploy
            Save();
            Debug.Log($"[SaveSystem] Saved unit {unitId} unlocked: {unlocked}");
        }
    }

    // NEW: Deployed status setter
    public static void SetUnitDeployed1(int unitId, bool deployed)
    {
        EnsureUnitEntry(unitId);
        var entry = GetUnitEntry(unitId);
        if (entry.isDeployed != deployed && entry.unlocked)
        {
            entry.isDeployed = deployed;
            Save();
            Debug.Log($"[SaveSystem] Saved unit {unitId} deployed: {deployed}");
        }
    }
    public static void SetUnitDeployed(int unitId, bool deployed)
    {
        EnsureUnitEntry(unitId);
        var entry = GetUnitEntry(unitId);

        if (deployed && !entry.unlocked)
            entry.unlocked = true;                 // [FIX] deploying implies unlocked

        if (entry.isDeployed != deployed)
        {
            entry.isDeployed = deployed;
            Save();
            Debug.Log($"[SaveSystem] Saved unit {unitId} deployed: {deployed}");
        }
    }


    // NEW: Bulk setter for bucket orders (sanitize: remove invalids/duplicates)
    public static void SetUnitOrders(List<int> deployedOrder, List<int> undeployedOrder)
    {
        // Sanitize: unique, valid IDs only (assume unitsDb exists elsewhere; skip validation for now)
        Data.deployedUnitOrder = deployedOrder?.Where(id => id >= 0).Distinct().ToList() ?? new List<int>();
        Data.undeployedUnitOrder = undeployedOrder?.Where(id => id >= 0).Distinct().ToList() ?? new List<int>();
        Save();
        Debug.Log($"[SaveSystem] Saved orders: Deployed={Data.deployedUnitOrder.Count}, Undeployed={Data.undeployedUnitOrder.Count}");
    }




    // NEW: Coins setter (for CurrencyManager)
    public static void SetCoins(int coins)
    {
        Data.coins = Mathf.Max(0, coins);
        Save();
    }
    // NEW: Gems setter (for CurrencyManager)
    public static void SetGems(int gems)
    {
        Data.gems = Mathf.Max(0, gems);
        Save();
    }

    // NEW: Hero XP setter
    public static void SetHeroXP(int heroXp)
    {
        Data.heroXp = Mathf.Max(0, heroXp);
        Save();
    }

    // (Optional) simple getters if you ever need them when bootstrapping:
    public static int GetCoins() => Data.coins;
    public static int GetGems() => Data.gems;
    public static int GetHeroXP() => Data.heroXp;

    // NEW: Battle allowance (BATTLE button daily cap + energy placeholder).
    // Read-only accessor - all writes go through SetBattleEnergy so they persist.
    public static SaveData.BattleEnergyState GetBattleEnergy()
    {
        // Old saves predate this section, so JsonUtility may leave it null.
        Data.battleEnergy ??= new SaveData.BattleEnergyState();
        return Data.battleEnergy;
    }

    // NEW: Tutorials already shown. Keyed by TutorialSequenceSO.TutorialId, so a
    // renamed id makes every player see that tutorial again - see TutorialManager.
    public static bool IsTutorialCompleted(string tutorialId)
    {
        if (string.IsNullOrEmpty(tutorialId)) return false;

        // Old saves predate this list, so JsonUtility may leave it null.
        Data.completedTutorials ??= new List<string>();
        return Data.completedTutorials.Contains(tutorialId);
    }

    public static void SetTutorialCompleted(string tutorialId, bool completed = true)
    {
        if (string.IsNullOrEmpty(tutorialId)) return;

        Data.completedTutorials ??= new List<string>();

        bool has = Data.completedTutorials.Contains(tutorialId);
        if (has == completed) return;                 // nothing to write

        if (completed) Data.completedTutorials.Add(tutorialId);
        else Data.completedTutorials.Remove(tutorialId);

        Save();
        Debug.Log($"[SaveSystem] Tutorial '{tutorialId}' completed: {completed}");
    }

    /// <summary>Forgets every tutorial, so they all play again. Debug / testing.</summary>
    public static void ResetTutorials()
    {
        Data.completedTutorials = new List<string>();
        Save();
        Debug.Log("[SaveSystem] Tutorial progress cleared.");
    }

    public static void SetBattleEnergy(string windowStartUtc, int battlesUsed, int energy)
    {
        var state = GetBattleEnergy();
        state.windowStartUtc = windowStartUtc ?? "";
        state.battlesUsed = Mathf.Max(0, battlesUsed);
        state.energy = Mathf.Max(0, energy);
        Save();
    }





    // Existing stage/level methods unchanged...
    public static SaveData.LevelProgress EnsureLevel(int levelId, int stagesPerLevel)
    {
        var lvl = Data.levels.Find(l => l.levelId == levelId);
        if (lvl == null)
        {
            lvl = new SaveData.LevelProgress
            {
                levelId = levelId,
                highestUnlocked = 0,
                stars = new int[stagesPerLevel]
            };
            Data.levels.Add(lvl);
        }
        else if (lvl.stars == null || lvl.stars.Length != stagesPerLevel)
        {
            // Resize while preserving existing stars
            var newArr = new int[stagesPerLevel];
            if (lvl.stars != null)
                System.Array.Copy(lvl.stars, newArr, Mathf.Min(lvl.stars.Length, stagesPerLevel));
            lvl.stars = newArr;
            lvl.highestUnlocked = Mathf.Clamp(lvl.highestUnlocked, 0, stagesPerLevel - 1);
        }
        return lvl;
    }

    public static int GetHighestUnlocked(int levelId) =>
        Data.levels.Find(l => l.levelId == levelId)?.highestUnlocked ?? 0;

    public static int GetStars(int levelId, int stageIndex0)
    {
        var lvl = Data.levels.Find(l => l.levelId == levelId);
        if (lvl == null || lvl.stars == null || stageIndex0 < 0 || stageIndex0 >= lvl.stars.Length) return 0;
        return lvl.stars[stageIndex0];
    }

    public static void RecordStageResul1(int levelId, int stageIndex0, float hpPercent)
    {
        // Ensure array exists (e.g., stagesPerLevel = 20 for this chapter)
        var lvl = EnsureLevel(levelId, stagesPerLevel: 20);

        stageIndex0 = Mathf.Clamp(stageIndex0, 0, lvl.stars.Length - 1);

        int stars = 0;
        if (hpPercent > 0f)
        {
            if (hpPercent >= 100f) stars = 3;
            else if (hpPercent >= 50f) stars = 2;
            else stars = 1;
        }

        if (stars > lvl.stars[stageIndex0])
            lvl.stars[stageIndex0] = stars;

        if (stars >= 1 && stageIndex0 >= lvl.highestUnlocked && stageIndex0 + 1 < lvl.stars.Length)
            lvl.highestUnlocked = stageIndex0 + 1;

        Save();
    }

    public static void RecordStageResult(int levelId, int stageIndex0, int stars)
    {
        var lvl = EnsureLevel(levelId, stagesPerLevel: 20);

        stageIndex0 = Mathf.Clamp(stageIndex0, 0, lvl.stars.Length - 1);

        stars = Mathf.Clamp(stars, 0, 3);

        // keep best result only
        if (stars > lvl.stars[stageIndex0])
            lvl.stars[stageIndex0] = stars;

        // unlock next stage
        if (stars >= 1 && stageIndex0 >= lvl.highestUnlocked && stageIndex0 + 1 < lvl.stars.Length)
            lvl.highestUnlocked = stageIndex0 + 1;

        Save();
    }


    public static void Save1()
    {
        Data.savedAtUtc = System.DateTime.UtcNow.ToString("o");
        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(Data));
        PlayerPrefs.Save();
    }
    public static void Save()
    {
        if (saveDepth > 10) { Debug.LogError("[SaveSystem] Recursion guard: Skipping save"); return; }
        saveDepth++;
        try
        {
            Data.savedAtUtc = System.DateTime.UtcNow.ToString("o");
            PlayerPrefs.SetString(KEY, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }
        finally { saveDepth--; }
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(KEY);
        _cache = null;
    }

    private static SaveData LoadInternal()
    {
        if (!PlayerPrefs.HasKey(KEY)) return new SaveData();
        var json = PlayerPrefs.GetString(KEY);
        var loaded = JsonUtility.FromJson<SaveData>(json);
        if (loaded == null) return new SaveData();
        // NEW: Migrate old saves (pre-isDeployed)
        if (loaded.version < 2)
        {
            foreach (var u in loaded.units)
            {
                if (!u.unlocked) u.isDeployed = false; // Default for old data
            }
            loaded.version = 2;

            // Publish the cache BEFORE saving. Save() reads Data, and Data is
            // "_cache ??= LoadInternal()" - without this line it re-enters
            // LoadInternal, recurses until the depth guard trips, and the
            // migration is never actually written (so it ran again every launch).
            _cache = loaded;
            Save(); // Persist migration
        }
        return loaded;
    }

    //******************************** RESET ALL DEBUGER *****************************************

    /// <summary>
    /// Optional reset method: If resetNow is true, fully resets all saves (units, currency, stages) 
    /// to initial state, like a new game. Clears PlayerPrefs and cache.
    /// Call this from UI (e.g., checkbox) or debug tools.
    /// </summary>
    public static void ResetAllIfRequested1(bool resetNow)
    {
        if (!resetNow) return; // No-op if unchecked

        ResetAll(); // Existing method: Deletes key, nulls cache
        Debug.Log("[SaveSystem] Full reset requested: Game state cleared to new player defaults.");
    }

    public static void ResetAllIfRequested2(bool resetNow)
    {
        if (!resetNow) return;

        ResetAll(); // Clears PlayerPrefs and cache

        // NEW: Force runtime resets for managers (call after bootstrap or in scene reload)
        var currencyMgr = CurrencyManager.Instance;
        if (currencyMgr != null)
        {
            currencyMgr.SetGems(currencyMgr.StartingGems);
            currencyMgr.SetCoins(currencyMgr.StartingCoins, silent: true);
            Debug.Log("[SaveSystem] Reset runtime currency to starting values.");
        }

        var gsm = GameStartManager.Instance;
        if (gsm?.PlayerUnits != null)
        {
            // Reload units from database defaults (no saved override)
            gsm.PlayerUnits.InitializeFromDatabase(gsm.unitsDatabase, gsm.initiallyUnlockedUnitIds); // Assume fields exposed
            Debug.Log("[SaveSystem] Reset units to initial state.");
        }

        Debug.Log("[SaveSystem] Full reset complete: All progress cleared to new game defaults.");
    }
    public static void ResetAllIfRequested(bool resetNow)
    {
        if (!resetNow) return;

        ResetAll(); // Clears SaveData PlayerPrefs and cache

        // --- NEW: reset LevelManager stage progress ---
        LevelManager.ResetProgressToStart();

        // Reset runtime currency
        var currencyMgr = CurrencyManager.Instance;
        if (currencyMgr != null)
        {
            currencyMgr.SetGems(currencyMgr.StartingGems);
            currencyMgr.SetCoins(currencyMgr.StartingCoins, silent: true);
            Debug.Log("[SaveSystem] Reset runtime currency to starting values.");
        }

        // Reset player units to initial state
        var gsm = GameStartManager.Instance;
        if (gsm?.PlayerUnits != null)
        {
            gsm.PlayerUnits.InitializeFromDatabase(
                gsm.unitsDatabase,
                gsm.initiallyUnlockedUnitIds
            );
            Debug.Log("[SaveSystem] Reset units to initial state.");
        }

        Debug.Log("[SaveSystem] Full reset complete: All progress cleared to new game defaults.");
    }

}


















































































//// *************** DESIGNED FOR SINGLE LEVEL WITH SEVERAL STAGES ***********************
