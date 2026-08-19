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



//public static class SaveSystem2
//{
//    private const string KEY = "GAME_SAVE_V2";
//    private const string KEY_BACKUP = "GAME_SAVE_V2_BACKUP";

//    private static SaveData _cache;

//    public static SaveData Data => _cache ??= LoadInternal();

//    // ------------- Core Persistence -------------

//    public static void Save()
//    {
//        if (_cache == null) _cache = new SaveData();
//        _cache.savedAtUtc = DateTime.UtcNow.ToString("o");

//        // Backup previous save (optional safety)
//        if (PlayerPrefs.HasKey(KEY))
//            PlayerPrefs.SetString(KEY_BACKUP, PlayerPrefs.GetString(KEY));

//        string json = JsonUtility.ToJson(_cache);
//        PlayerPrefs.SetString(KEY, json);
//        PlayerPrefs.Save();
//    }

//    public static void ResetAll()
//    {
//        _cache = new SaveData();
//        Save();
//    }

//    private static SaveData LoadInternal()
//    {
//        try
//        {
//            if (!PlayerPrefs.HasKey(KEY))
//                return new SaveData();

//            string json = PlayerPrefs.GetString(KEY);
//            var data = JsonUtility.FromJson<SaveData>(json);
//            if (data == null) data = new SaveData();

//            // Migration example(s)
//            if (data.version < 2)
//            {
//                data.version = 2;
//                if (data.deck == null) data.deck = new SaveData.DeckSection();
//                if (data.deck.slots == null || data.deck.slots.Length == 0)
//                    data.deck.slots = new int[data.deck.deckSize];
//                for (int i = 0; i < data.deck.slots.Length; i++)
//                    if (data.deck.slots[i] == 0) data.deck.slots[i] = -1;
//            }

//            return data;
//        }
//        catch
//        {
//            // Try backup if main is corrupt
//            if (PlayerPrefs.HasKey(KEY_BACKUP))
//            {
//                string json = PlayerPrefs.GetString(KEY_BACKUP);
//                var data = JsonUtility.FromJson<SaveData>(json);
//                return data ?? new SaveData();
//            }
//            return new SaveData();
//        }
//    }

//    // ------------- Currency -------------

//    public static int GetCoins() => Data.coins;
//    public static int GetGems() => Data.gems;

//    public static void SetCoins(int value)
//    {
//        Data.coins = Mathf.Max(0, value);
//        Save();
//    }

//    // ------------- Deck / Deployment -------------

//    public static void EnsureDeckSize(int size)
//    {
//        if (size < 1) size = 1;
//        if (Data.deck == null) Data.deck = new SaveData.DeckSection();

//        bool changed = false;

//        if (Data.deck.deckSize != size) { Data.deck.deckSize = size; changed = true; }

//        if (Data.deck.slots == null || Data.deck.slots.Length != size)
//        {
//            var newArr = new int[size];
//            for (int i = 0; i < size; i++) newArr[i] = -1;

//            if (Data.deck.slots != null)
//            {
//                for (int i = 0; i < Mathf.Min(size, Data.deck.slots.Length); i++)
//                    newArr[i] = Data.deck.slots[i];
//            }

//            Data.deck.slots = newArr;
//            changed = true;
//        }

//        if (changed) Save();
//    }

//    public static int GetDeckSize() => Data.deck?.deckSize ?? 0;

//    // SaveSystem.cs  (inside the class)
//    public static bool HasSave()
//    {
//        return PlayerPrefs.HasKey(KEY);   // KEY = "GAME_SAVE_V2" in your file
//    }

//    // ---------------- Convenience for currencies ----------------
//    public static void LoadCurrenciesInto(CurrencyManager cm)
//    {
//        if (cm == null) return;
//        cm.SetCoins(GetCoins(), silent: true);
//        cm.SetGems(GetGems());          // [CHANGED] also restore gems

//    }
//    public static void ApplyUnitsOverlay1(PlayerUnitsModel model)
//    {
//        if (model == null || Data.units == null) return;
//        foreach (var e in Data.units)
//        {
//            //if (!model.Exists(e.unitId)) continue;
//            if (e.unlocked) model.Unlock(e.unitId); else model.Lock(e.unitId);
//            model.SetLevel(e.unitId, Mathf.Max(1, e.level));
//            model.SetDeployed(e.unitId, e.isDeployed);
//        }
//    }
//    public static void ApplyUnitsOverlay(PlayerUnitsModel model)
//    {
//        if (model == null) return;

//        // [CHANGED] 1) Apply from saved unit entries (as before)
//        if (Data.units != null)
//        {
//            foreach (var e in Data.units)
//            {
//                if (e == null) continue;                              // [ADDED]
//                if (e.unlocked) model.Unlock(e.unitId); else model.Lock(e.unitId);
//                model.SetLevel(e.unitId, Mathf.Max(1, e.level));
//                model.SetDeployed(e.unitId, e.isDeployed);
//            }
//        }

//        // [ADDED] 2) Also apply from deck (source of truth for deployment)
//        if (Data.deck?.slots != null)
//        {
//            for (int i = 0; i < Data.deck.slots.Length; i++)
//            {
//                int id = Data.deck.slots[i];
//                if (id < 0) continue;

//                // ensure it exists and is unlocked if it's in the deck
//                var u = EnsureUnit(id);                 // [ADDED]
//                u.unlocked = true;                    // [ADDED]
//                if (u.level < 1) u.level = 1;           // [ADDED]
//                u.isDeployed = true;                    // [ADDED]

//                // mirror into runtime model
//                model.Unlock(id);                       // [ADDED]
//                model.SetLevel(id, u.level);            // [ADDED]
//                model.SetDeployed(id, true);            // [ADDED]
//            }
//        }
//    }

//    // -------- Units (Setters for UI) --------
//    public static SaveData.UnitStateEntry EnsureUnit(int unitId)                // [ADDED]
//    {
//        var u = Data.units.Find(x => x.unitId == unitId);
//        if (u == null)
//        {
//            u = new SaveData.UnitStateEntry { unitId = unitId, unlocked = false, level = 1, isDeployed = false, deckSlot = -1 };
//            Data.units.Add(u);
//        }
//        return u;
//    }

//    public static void SetUnitUnlocked(int unitId, bool unlocked)              // [ADDED]
//    {
//        var u = EnsureUnit(unitId);
//        u.unlocked = unlocked;
//        if (unlocked && u.level < 1) u.level = 1;
//        Save();
//    }

//    public static void SetUnitDeployed(int unitId, bool deployed)
//    {
//        var u = EnsureUnit(unitId);
//        u.isDeployed = deployed;                // [SYNC]
//        if (deployed)
//        {
//            u.unlocked = true;                  // [FIX] deployment implies ownership
//            if (u.level < 1) u.level = 1;       // [FIX] keep level sane
//        }
//        else
//        {
//            u.deckSlot = -1;                    // [SYNC] detach from deck
//        }
//        Save();
//    }

//    public static void SetUnitLevel(int unitId, int level)                     // [ADDED]
//    {
//        var u = EnsureUnit(unitId);
//        u.level = Mathf.Max(1, level);
//        Save();
//    }

//    public static void DeployToSlot(int unitId, int slotIndex)                 // [ADDED]
//    {
//        EnsureDeckSize(Mathf.Max(slotIndex + 1, GetDeckSize()));
//        for (int i = 0; i < Data.deck.slots.Length; i++)
//            if (Data.deck.slots[i] == unitId) Data.deck.slots[i] = -1;

//        Data.deck.slots[slotIndex] = unitId;

//        var u = EnsureUnit(unitId);
//        u.deckSlot = slotIndex;
//        u.isDeployed = true; // [SYNC]  
//        u.unlocked = true;   // sanity
//        Save();
//    }

//    public static void Undeploy(int unitId)                                    // [ADDED]
//    {
//        if (Data.deck?.slots != null)
//            for (int i = 0; i < Data.deck.slots.Length; i++)
//                if (Data.deck.slots[i] == unitId) Data.deck.slots[i] = -1;

//        var u = EnsureUnit(unitId);
//        u.deckSlot = -1;
//        u.isDeployed = false; // [SYNC]     
//        Save();
//    }

//    // [ADDED] Write the entire PlayerUnitsModel into SaveData (first run seeding)
//    public static void ImportFromModel(PlayerUnitsModel model)
//    {
//        if (model == null) return;

//        Data.units.Clear();                       // [ADDED]
//        foreach (var s in model.AllStates)        // [ADDED]
//        {
//            if (s == null) continue;              // [ADDED]
//            Data.units.Add(new SaveData.UnitStateEntry
//            {
//                unitId = s.unitId,            // [ADDED]
//                unlocked = s.unlocked,          // [ADDED]
//                level = Mathf.Max(1, s.level), // [ADDED]
//                isDeployed = s.isDeployed,        // [ADDED]
//                deckSlot = -1                   // [ADDED]
//            });
//        }
//        Save();                                   // [ADDED]
//    }




//    // ------------- Levels / Stages (keep simple & flexible) -------------

//    public static SaveData.LevelProgress EnsureLevelProgress(int levelId)
//    {
//        var lp = Data.levels.Find(x => x.levelId == levelId);
//        if (lp == null)
//        {
//            lp = new SaveData.LevelProgress { levelId = levelId, highestUnlocked = 0, stars = Array.Empty<int>() };
//            Data.levels.Add(lp);
//            Save();
//        }
//        return lp;
//    }

//    public static void SetLevelUnlockedIndex(int levelId, int highestUnlocked)
//    {
//        var lp = EnsureLevelProgress(levelId);
//        lp.highestUnlocked = Mathf.Max(lp.highestUnlocked, highestUnlocked);
//        Save();
//    }

//    public static void SetLevelStars(int levelId, int[] stars)
//    {
//        var lp = EnsureLevelProgress(levelId);
//        lp.stars = stars ?? Array.Empty<int>();
//        Save();
//    }
//    // ======== HomeUI compatibility helpers (wrappers) ========

//    // HomeManager used to call this; map to our v2 structure.
//    public static SaveData.LevelProgress EnsureLevel(int levelId)
//    {
//        return EnsureLevelProgress(levelId);
//    }

//    // Highest unlocked stage index (0-based).
//    public static int GetHighestUnlocked(int levelId)
//    {
//        return EnsureLevelProgress(levelId).highestUnlocked;
//    }

//    // All stars array for a level (length may be < total stages if not played yet).
//    public static int[] GetStars(int levelId)
//    {
//        var lp = EnsureLevelProgress(levelId);
//        return lp.stars ?? Array.Empty<int>();
//    }

//    // Stars for a specific stage; returns 0 if not recorded.
//    public static int GetStars(int levelId, int stageIndex)
//    {
//        var lp = EnsureLevelProgress(levelId);
//        if (lp.stars == null || stageIndex < 0 || stageIndex >= lp.stars.Length) return 0;
//        return lp.stars[stageIndex];
//    }

//    // Record a result for a single stage (keeps the best star count).
//    // Also advances highestUnlocked if this stage index is farther.
//    public static void RecordStageResult(int levelId, int stageIndex, int starCount)
//    {
//        var lp = EnsureLevelProgress(levelId);
//        if (stageIndex < 0) return;

//        // ensure stars array can hold this index
//        int needLen = stageIndex + 1;
//        if (lp.stars == null || lp.stars.Length < needLen)
//        {
//            var newArr = new int[needLen];
//            if (lp.stars != null) Array.Copy(lp.stars, newArr, lp.stars.Length);
//            lp.stars = newArr;
//        }

//        // clamp stars to [0..3] and keep BEST so far
//        int clamped = Mathf.Clamp(starCount, 0, 3);
//        lp.stars[stageIndex] = Mathf.Max(lp.stars[stageIndex], clamped);

//        // advance highest unlocked if needed
//        if (stageIndex > lp.highestUnlocked)
//            lp.highestUnlocked = stageIndex;

//        Save();
//    }

//    // ---------- Extra overloads to match HomeManager's older calls ----------

//    // HomeManager calls EnsureLevel(levelId, expectedStages)
//    // We'll ensure the LevelProgress exists AND make the stars array at least that long.
//    public static SaveData.LevelProgress EnsureLevel(int levelId, int expectedStages)
//    {
//        var lp = EnsureLevelProgress(levelId);
//        if (expectedStages < 0) expectedStages = 0;

//        if (lp.stars == null || lp.stars.Length < expectedStages)
//        {
//            var newArr = new int[expectedStages];
//            if (lp.stars != null) Array.Copy(lp.stars, newArr, lp.stars.Length);
//            lp.stars = newArr;
//            Save();
//        }
//        return lp;
//    }

//    // HomeManager passes a float for stars (e.g., computed score). Accept it and convert.
//    public static void RecordStageResult(int levelId, int stageIndex, float starCount)
//    {
//        // Round to nearest int; clamp happens in the int overload.
//        RecordStageResult(levelId, stageIndex, Mathf.RoundToInt(starCount));
//    }

//}


//public static class SaveSystem1
//{
//    private static SaveData _cache;
//    private const string KEY = "GAME_SAVE_V1";

//    public static SaveData Data => _cache ??= LoadInternal();

//    public static SaveData.LevelProgress EnsureLevel(int levelId, int stagesPerLevel)
//    {
//        var lvl = Data.levels.Find(l => l.levelId == levelId);
//        if (lvl == null)
//        {
//            lvl = new SaveData.LevelProgress
//            {
//                levelId = levelId,
//                highestUnlocked = 0,
//                stars = new int[stagesPerLevel]
//            };
//            Data.levels.Add(lvl);
//        }
//        else if (lvl.stars == null || lvl.stars.Length != stagesPerLevel)
//        {
//            // Resize while preserving existing stars
//            var newArr = new int[stagesPerLevel];
//            if (lvl.stars != null)
//                System.Array.Copy(lvl.stars, newArr, Mathf.Min(lvl.stars.Length, stagesPerLevel));
//            lvl.stars = newArr;
//            lvl.highestUnlocked = Mathf.Clamp(lvl.highestUnlocked, 0, stagesPerLevel - 1);
//        }
//        return lvl;
//    }

//    public static int GetHighestUnlocked(int levelId) =>
//        Data.levels.Find(l => l.levelId == levelId)?.highestUnlocked ?? 0;

//    public static int GetStars(int levelId, int stageIndex0)
//    {
//        var lvl = Data.levels.Find(l => l.levelId == levelId);
//        if (lvl == null || lvl.stars == null || stageIndex0 < 0 || stageIndex0 >= lvl.stars.Length) return 0;
//        return lvl.stars[stageIndex0];
//    }

//    public static void RecordStageResult(int levelId, int stageIndex0, float hpPercent)
//    {
//        // Ensure array exists (e.g., stagesPerLevel = 20 for this chapter)
//        var lvl = EnsureLevel(levelId, stagesPerLevel: 20);

//        stageIndex0 = Mathf.Clamp(stageIndex0, 0, lvl.stars.Length - 1);

//        int stars = 0;
//        if (hpPercent > 0f)
//        {
//            if (hpPercent >= 100f) stars = 3;
//            else if (hpPercent >= 50f) stars = 2;
//            else stars = 1;
//        }

//        if (stars > lvl.stars[stageIndex0])
//            lvl.stars[stageIndex0] = stars;

//        if (stars >= 1 && stageIndex0 >= lvl.highestUnlocked && stageIndex0 + 1 < lvl.stars.Length)
//            lvl.highestUnlocked = stageIndex0 + 1;

//        Save();
//    }

//    public static void Save()
//    {
//        Data.savedAtUtc = System.DateTime.UtcNow.ToString("o");
//        PlayerPrefs.SetString(KEY, JsonUtility.ToJson(Data));
//        PlayerPrefs.Save();
//    }

//    public static void ResetAll()
//    {
//        PlayerPrefs.DeleteKey(KEY);
//        _cache = null;
//    }

//    private static SaveData LoadInternal()
//    {
//        if (!PlayerPrefs.HasKey(KEY)) return new SaveData();
//        var json = PlayerPrefs.GetString(KEY);
//        return JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
//    }
//}


//// *************** DESIGNED FOR SINGLE LEVEL WITH SEVERAL STAGES ***********************

//public static class SaveSystem1
//{
//    private const string KEY = "GAME_SAVE_V1";   // bump when structure changes
//    public static int StagesPerLevel = 20;       // set from your Home scene on boot

//    private static SaveData _cache;

//    public static SaveData Data
//    {
//        get
//        {
//            if (_cache == null) _cache = LoadInternal();
//            return _cache;
//        }
//    }

//    public static void Save()
//    {
//        Data.savedAtUtc = DateTime.UtcNow.ToString("o");
//        string json = JsonUtility.ToJson(Data);
//        PlayerPrefs.SetString(KEY, json);
//        PlayerPrefs.Save();
//    }

//    public static void ResetAll()
//    {
//        PlayerPrefs.DeleteKey(KEY);
//        _cache = null;
//    }

//    private static SaveData LoadInternal()
//    {
//        if (!PlayerPrefs.HasKey(KEY))
//            return Fresh();

//        var json = PlayerPrefs.GetString(KEY, "{}");
//        var data = JsonUtility.FromJson<SaveData>(json) ?? Fresh();

//        // Ensure arrays are sized (migration-safe)
//        EnsureStageArrays(data);
//        return data;
//    }

//    private static SaveData Fresh()
//    {
//        var d = new SaveData();
//        EnsureStageArrays(d);
//        return d;
//    }

//    private static void EnsureStageArrays(SaveData d)
//    {
//        if (d.stages == null) d.stages = new SaveData.StageSection();
//        if (d.stages.stars == null || d.stages.stars.Length != StagesPerLevel)
//            d.stages.stars = new int[StagesPerLevel];
//        d.stages.highestUnlocked = Mathf.Clamp(d.stages.highestUnlocked, 0, StagesPerLevel - 1);
//    }

//    // ---------- Convenience API for stages ----------

//    /// <summary>
//    /// hpPercent: 0..100 (0 = failed). Upgrades stars and unlocks the next stage on any clear.
//    /// </summary>
//    public static void RecordStageResult(int stageIndex0, float hpPercent)
//    {
//        stageIndex0 = Mathf.Clamp(stageIndex0, 0, StagesPerLevel - 1);
//        int stars = 0;
//        if (hpPercent > 0f)
//        {
//            if (hpPercent >= 100f) stars = 3;
//            else if (hpPercent >= 50f) stars = 2;
//            else stars = 1;
//        }

//        // upgrade if better
//        if (stars > Data.stages.stars[stageIndex0])
//            Data.stages.stars[stageIndex0] = stars;

//        // unlock next
//        if (stars >= 1 && stageIndex0 >= Data.stages.highestUnlocked && stageIndex0 + 1 < StagesPerLevel)
//            Data.stages.highestUnlocked = stageIndex0 + 1;

//        Save();
//    }

//    public static int GetHighestUnlocked() => Data.stages.highestUnlocked;
//    public static int GetStars(int stageIndex0) =>
//        (stageIndex0 >= 0 && stageIndex0 < Data.stages.stars.Length) ? Data.stages.stars[stageIndex0] : 0;
//}
