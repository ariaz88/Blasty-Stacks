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

    // --- Optional
    public string savedAtUtc;

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
    public class StageSection
    {
        // 0-based index of the highest unlocked stage (default 0 = Stage 1).
        public int highestUnlocked = 0;

        // Per-stage stars (0..3). Size must match your stage count for this chapter.
        public int[] stars;

    }
}
