using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;
using System.Linq;  // NEW: Add this for Select extension

public class GameStartManager : MonoBehaviour
{
    public static GameStartManager Instance { get; private set; }


    private bool resetBool;


    [Header("Design-Time Data (Assign in Inspector)")]
    public UnitsDatabaseSO unitsDatabase;
   /* [SerializeField]*/ public UpgradeCostSO upgradeCostConfig;
   /* [SerializeField]*/ public ProgressionConfigSO progressionConfig;

    [Header("Initial Unlocks (First Run Defaults)")]
    [Tooltip("Unit IDs to unlock at start (e.g., Warrior = 0).")]
    public int[] initiallyUnlockedUnitIds = new[] { 0 };
    [Tooltip("Units that start unlocked BUT NOT deployed (appear in Undeployed).")]
    [SerializeField] private int[] initiallyUndeployedIds = new int[0];



    // Runtime singletons/services
    public PlayerUnitsModel PlayerUnits { get; private set; }
    public PlayerProgressionService ProgressionService { get; private set; }


    /// <summary>
    /// Ensures a single instance survives scene loads and initializes services.
    /// </summary>
    /// 
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); 



        resetBool = true;
        OnResetButtonClicked();
        InitializeServices();

        // Subscribe ONCE to level progression
    }
    private void OnEnable()
    {
        StartCoroutine(WaitForLevelManager());
    }

    private IEnumerator WaitForLevelManager()
    {
        // Wait until LevelManager exists
        while (LevelManager.Instance == null)
            yield return null;

        LevelManager.Instance.OnLevelStageChanged += OnLevelStageChanged;

        Debug.Log("[GameStartManager] Subscribed to LevelManager.OnLevelStageChanged");
    }

    private void OnLevelStageChanged(int levelIndex, int stageIndex, int globalStage)
    {
    }

    public void OnResetButtonClicked()
    {
        SaveSystem.ResetAllIfRequested(resetBool);
        // Optional: Reload scene or notify user
        Debug.Log("Reset complete—starting fresh!");
    }


    /// <summary>
    /// Build the runtime PlayerUnitsModel from the Units database,
    /// ensure CurrencyManager exists, and construct PlayerProgressionService.
    /// 
    /// Steps:
    /// 1) Create PlayerUnitsModel and seed it from UnitsDatabaseSO (mark only Warrior unlocked by default).
    /// 2) Grab CurrencyManager.Instance (must be present in scene).
    /// 3) Construct PlayerProgressionService with all required references.
    /// </summary>
    /// 
    private void InitializeServices()
    {
        // 1) Build runtime PlayerUnits from DB
        PlayerUnits = new PlayerUnitsModel();
        if (unitsDatabase != null)
            PlayerUnits.InitializeFromDatabase(unitsDatabase, initiallyUnlockedUnitIds);

        ProgressionService = new PlayerProgressionService(
            PlayerUnits,
            CurrencyManager.Instance,
            upgradeCostConfig,
            unitsDatabase,
            progressionConfig
        );


        // 2) Decide: first run or normal boot based on saved units
        var savedUnits = SaveSystem.Data.units;
        bool hasUnitsSave = savedUnits != null && savedUnits.Count > 0;

        if (hasUnitsSave)
        {
            var modelStates = savedUnits
                .Select(u => new PlayerUnitsModel.UnitState(u.unitId, u.unlocked, u.level, u.isDeployed))
                .ToList();

            PlayerUnits.LoadFromSavedStates(modelStates);
            Debug.Log($"[GameStartManager] Loaded {savedUnits.Count} saved unit states");

        }
        else
        {
            foreach (var def in unitsDatabase.Units)
            {
                if (!def) continue;
                PlayerUnits.Lock(def.unitId);
            }

            // Unlock + DEPLOY initial roster (from inspector)
            foreach (var id in initiallyUnlockedUnitIds)
            {
                PlayerUnits.Unlock(id);
                PlayerUnits.SetDeployed(id, true);
                if (PlayerUnits.GetLevel(id) < 1) PlayerUnits.SetLevel(id, 1);
            }

            // Unlock + keep UNDEPLOYED (optional)
            if (initiallyUndeployedIds != null)
            {
                foreach (var id in initiallyUndeployedIds)
                {
                    PlayerUnits.Unlock(id);
                    PlayerUnits.SetDeployed(id, false);
                    if (PlayerUnits.GetLevel(id) < 1) PlayerUnits.SetLevel(id, 1);
                }
            }

            // Persist first run roster
            SaveSystem.Data.units = PlayerUnits.ToSavedEntries(); // writes unlocked/level/isDeployed per unit
            SaveSystem.Save();
            Debug.Log("[GameStartManager] First run: seeded units and saved.");
        }

        // 3) Currency
        var currencyMgr = CurrencyManager.Instance;
        if (currencyMgr == null)
        {
            Debug.LogError("[GameStartManager] CurrencyManager.Instance not found in scene.");
            return;
        }

        var data = SaveSystem.Data;

        // Decide if this is a true first run.
        // We already calculated hasUnitsSave above. If there are no saved units AND
        // all economy values are zero, treat it as first run.
        bool isFirstRunEconomy =
            !hasUnitsSave &&
            data.coins == 0 &&
            data.gems == 0 &&
            data.heroXp == 0;   // heroXp is the new field we added to SaveData

        int loadedCoins;
        int loadedGems;
        int loadedHeroXp;

        if (isFirstRunEconomy)
        {
            // Use starting values from CurrencyManager
            loadedCoins = currencyMgr.StartingCoins;
            loadedGems = currencyMgr.StartingGems;
            loadedHeroXp = currencyMgr.StartingHeroXP;
        }
        else
        {
            // Use whatever is in the save (even if some are 0, because that might be legit)
            loadedCoins = data.coins;
            loadedGems = data.gems;
            loadedHeroXp = data.heroXp;
        }

        // Push values into the runtime CurrencyManager
        currencyMgr.SetCoins(loadedCoins, silent: true);
        currencyMgr.SetGems(loadedGems);
        currencyMgr.SetHeroXP(loadedHeroXp);

        // If it's truly the first run, seed the SaveData once
        if (isFirstRunEconomy)
        {
            data.coins = loadedCoins;
            data.gems = loadedGems;
            data.heroXp = loadedHeroXp;
            SaveSystem.Save();

            Debug.Log("[GameStartManager] Seeded SaveData with starting currency + hero XP.");
        }

        Debug.Log($"[GameStartManager] Loaded currency: Coins={loadedCoins}, Gems={loadedGems}, HeroXP={loadedHeroXp}");
        ProgressionService = new PlayerProgressionService(
            units: PlayerUnits,
            currency: currencyMgr,
            costConfig: upgradeCostConfig,
            unitsDb: unitsDatabase,
            progressionCfg: progressionConfig
        );

    }


    private void InitializeServices2()
    {
        // 1) Build runtime PlayerUnits from DB
        PlayerUnits = new PlayerUnitsModel();
        if (unitsDatabase != null)
            PlayerUnits.InitializeFromDatabase(unitsDatabase, initiallyUnlockedUnitIds);

        // 2) Decide: first run or normal boot based on saved units
        var savedUnits = SaveSystem.Data.units;
        bool hasUnitsSave = savedUnits != null && savedUnits.Count > 0;

        if (hasUnitsSave)
        {
            var modelStates = savedUnits
                .Select(u => new PlayerUnitsModel.UnitState(u.unitId, u.unlocked, u.level, u.isDeployed))
                .ToList();

            PlayerUnits.LoadFromSavedStates(modelStates);
            Debug.Log($"[GameStartManager] Loaded {savedUnits.Count} saved unit states");

        }
        else
        {
            foreach (var def in unitsDatabase.Units)
            {
                if (!def) continue;
                PlayerUnits.Lock(def.unitId);
            }

            // Unlock + DEPLOY initial roster (from inspector)
            foreach (var id in initiallyUnlockedUnitIds)
            {
                PlayerUnits.Unlock(id);
                PlayerUnits.SetDeployed(id, true);
                if (PlayerUnits.GetLevel(id) < 1) PlayerUnits.SetLevel(id, 1);
            }

            // Unlock + keep UNDEPLOYED (optional)
            if (initiallyUndeployedIds != null)
            {
                foreach (var id in initiallyUndeployedIds)
                {
                    PlayerUnits.Unlock(id);
                    PlayerUnits.SetDeployed(id, false);
                    if (PlayerUnits.GetLevel(id) < 1) PlayerUnits.SetLevel(id, 1);
                }
            }

            // Persist first run roster
            SaveSystem.Data.units = PlayerUnits.ToSavedEntries(); // writes unlocked/level/isDeployed per unit
            SaveSystem.Save();
            Debug.Log("[GameStartManager] First run: seeded units and saved.");
        }

        // 3) Currency
        var currencyMgr = CurrencyManager.Instance;
        if (currencyMgr == null)
        {
            Debug.LogError("[GameStartManager] CurrencyManager.Instance not found in scene.");
            return; // [FIX] don’t proceed without currency; upgrades need it
        }

        // Use saved values if present; else starting values (don’t overwrite a non-zero save)
        int loadedGems = SaveSystem.Data.gems > 0 ? SaveSystem.Data.gems : currencyMgr.StartingGems;
        int loadedCoins = SaveSystem.Data.coins > 0 ? SaveSystem.Data.coins : currencyMgr.StartingCoins;

        currencyMgr.SetGems(loadedGems);
        currencyMgr.SetCoins(loadedCoins, silent: true);

        // Seed currency to save only if it wasn’t there
        if (SaveSystem.Data.gems == 0 && SaveSystem.Data.coins == 0)
        {
            SaveSystem.Data.gems = loadedGems;
            SaveSystem.Data.coins = loadedCoins;
            SaveSystem.Save();
            Debug.Log("[GameStartManager] Seeded SaveData with starting currency.");
        }

        ProgressionService = new PlayerProgressionService(
            units: PlayerUnits,
            currency: currencyMgr,
            costConfig: upgradeCostConfig,
            unitsDb: unitsDatabase,
            progressionCfg: progressionConfig
        );
    }

    private void InitializeServices1()
    {
        // 1) PlayerUnitsModel (runtime state for unlocked/levels)
        PlayerUnits = new PlayerUnitsModel();
        if (unitsDatabase != null)
            PlayerUnits.InitializeFromDatabase(unitsDatabase, initiallyUnlockedUnitIds);


        // NEW: Load saved unit states (overrides defaults)
        // NEW: Load saved unit states (overrides defaults)
        var savedUnits = SaveSystem.Data.units;
        if (savedUnits != null && savedUnits.Count > 0)
        {
            // Convert to model states (requires System.Linq)
            var modelStates = savedUnits.Select(u => new PlayerUnitsModel.UnitState(u.unitId, u.unlocked, u.level, u.isDeployed)).ToList();
            PlayerUnits.LoadFromSavedStates(modelStates);
            Debug.Log($"[GameStartManager] Loaded {savedUnits.Count} saved unit states");
        }
        else
        {
            // First run: Save initial states for persistence
            SaveSystem.Data.units = PlayerUnits.ToSavedEntries();
            SaveSystem.Save();
        }

        var currencyMgr = CurrencyManager.Instance;
        if (currencyMgr != null)
        {
            // NEW: Fallback to starting if no real save (coins/gem=0 indicates first run)
            int loadedGems = SaveSystem.Data.gems > 0 ? SaveSystem.Data.gems : currencyMgr.StartingGems;
            int loadedCoins = SaveSystem.Data.coins > 0 ? SaveSystem.Data.coins : currencyMgr.StartingCoins;

            currencyMgr.SetGems(loadedGems);
            currencyMgr.SetCoins(loadedCoins, silent: true);

            // NEW: Seed SaveData with defaults on first run for future persistence
            if (SaveSystem.Data.coins == 0)
            {
                SaveSystem.Data.gems = currencyMgr.StartingGems;
                SaveSystem.Data.coins = currencyMgr.StartingCoins;
                SaveSystem.Save(); // Persist seeded defaults
                Debug.Log("[GameStartManager] Seeded fresh SaveData with starting currency.");
            }

            Debug.Log($"[GameStartManager] Loaded currency: Gems={loadedGems}, Coins={loadedCoins}");
        }

        // Rest of method...

        // Make everything LOCKED by default
        foreach (var def in unitsDatabase.Units)
        {
            if (!def) continue;
            PlayerUnits.Lock(def.unitId);
        }

        // Unlock + DEPLOY the initial roster (your Inspector list)
        foreach (var id in initiallyUnlockedUnitIds)
        {
            PlayerUnits.Unlock(id);
            PlayerUnits.SetDeployed(id, true);
            if (PlayerUnits.GetLevel(id) < 1) PlayerUnits.SetLevel(id, 1); // if you have SetLevel
        }

        // Build a fast lookup of valid IDs from the database
        var validIds = new HashSet<int>();
        foreach (var def in unitsDatabase.Units)
        {
            if (!def) continue;
            validIds.Add(def.unitId);
        }

        // Helper: avoid double-processing if an ID appears in both arrays
        bool IsDuplicate(int id)
        {
            if (initiallyUnlockedUnitIds != null)
            {
                for (int i = 0; i < initiallyUnlockedUnitIds.Length; i++)
                    if (initiallyUnlockedUnitIds[i] == id) return true;
            }
            return false;
        }

        // 4) Unlock but leave UNDEPLOYED (→ shows in Undeployed bucket)
        if (initiallyUndeployedIds != null)
        {
            foreach (var id in initiallyUndeployedIds)
            {
                if (!validIds.Contains(id))
                {
                    Debug.LogWarning($"[GameStartManager] initiallyUndeployedIds contains unknown unitId {id}. Skipping.", this);
                    continue;
                }

                // Skip if it's also listed as deployed
                if (IsDuplicate(id)) continue;

                PlayerUnits.Unlock(id);
                PlayerUnits.SetDeployed(id, false);
                if (PlayerUnits.GetLevel(id) < 1) PlayerUnits.SetLevel(id, 1);
            }



            // 2) CurrencyManager (must be in scene on its own GameObject)
            if (CurrencyManager.Instance == null)
            {
                Debug.LogError("[ServicesBootstrap] CurrencyManager.Instance not found in scene. " +
                               "Please add a Managers/CurrencyManager GameObject with CurrencyManager component.");
                return;
            }

            // 3) ProgressionService (central upgrade API for UI)
            ProgressionService = new PlayerProgressionService(
                units: PlayerUnits,
                currency: CurrencyManager.Instance,
                costConfig: upgradeCostConfig,
                unitsDb: unitsDatabase,
                progressionCfg: progressionConfig
            );
        }



    }


}
