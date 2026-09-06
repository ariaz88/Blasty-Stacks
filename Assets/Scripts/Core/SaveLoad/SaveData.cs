// SaveData.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
// SaveData.cs



public class SaveData
{
    public int version = 1;

    // --- Economy
    public int gems = 0;
    public int coins = 0;
    public int heroXp = 0;   // NEW


    // --- Units
    public List<UnitStateEntry> units = new List<UnitStateEntry>();

    // NEW: Persist unit orders for deployed/undeployed buckets (unitIds in display order)
    public List<int> deployedUnitOrder = new List<int>();
    public List<int> undeployedUnitOrder = new List<int>();

    // --- Stages
    public StageSection stages = new StageSection();

    public List<LevelProgress> levels = new();   // ALL levels live here

    // --- Battle allowance (BATTLE button daily cap + energy)
    public BattleEnergyState battleEnergy = new BattleEnergyState();

    // --- Tutorials the player has already been shown (TutorialSequenceSO.TutorialId).
    // Adding this needed no version bump: saves written before it simply have no
    // such key, and JsonUtility leaves the field initializer in place.
    public List<string> completedTutorials = new List<string>();

    // --- The two gem-bought side deploy stages on the player castle.
    // Same reasoning as completedTutorials above: adding this needed no version
    // bump, because saves written before it simply have no such key and
    // JsonUtility leaves the field initializer (both false) in place.
    public DeployStageUnlocks deployStages = new DeployStageUnlocks();

    // --- Optional
    public string savedAtUtc;

    [Serializable]
    public class DeployStageUnlocks
    {
        // Bought once with gems, then open on every level for the rest of the
        // game. See DeployStageUnlockService.
        public bool left;
        public bool right;
    }

    [Serializable]
    public class UnitStateEntry
    {
        public int unitId;
        public bool unlocked;
        public int level;
        // NEW: Track deployed status
        public bool isDeployed;

        public UnitStateEntry() { }
        public UnitStateEntry(int unitId, bool unlocked, int level, bool isDeployed = false)
        {
            this.unitId = unitId;
            this.unlocked = unlocked;
            this.level = level < 1 ? 1 : level;
            this.isDeployed = isDeployed;
        }
    }

    [Serializable]
    public class LevelProgress
    {
        public int levelId;          // e.g., 1, 2, 3...
        public int highestUnlocked;  // 0-based
        public int[] stars;          // length = stagesInThisLevel
    }

    [Serializable]
    public class BattleEnergyState
    {
        // UTC instant the current 24h battle window opened, in round-trip ("o")
        // format. Empty string = no window open yet; the next battle opens one.
        public string windowStartUtc = "";

        // Battles started inside the current window (may exceed the daily limit
        // once the player starts paying with energy).
        public int battlesUsed = 0;

        // PLACEHOLDER: spare energy, spent once the daily allowance is used up.
        // Nothing grants energy yet - see BattleEnergyService.
        public int energy = 0;
    }

    [Serializable]
    public class StageSection
    {
        // 0-based index of the highest unlocked stage (default 0 = Stage 1).
        public int highestUnlocked = 0;

        // Per-stage stars (0..3). Size must match your stage count for this chapter.
        public int[] stars;

    }
}
