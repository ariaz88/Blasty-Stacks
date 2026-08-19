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

        // Optional: claimed chests, best time, etc. (add later)
        // public bool[] chestClaimed;
        // public float[] bestTimeSec;
    }
}
public class SaveData2
{
    public int version = 2;

    // -------- Economy --------
    public int coins = 0;
    public int gems = 0;

    // -------- Units --------
    public List<UnitStateEntry> units = new List<UnitStateEntry>();

    // -------- Deck / Main Team --------
    public DeckSection deck = new DeckSection();

    // -------- Stages / Home UI (keep compatible with your current usage) --------
    public List<LevelProgress> levels = new List<LevelProgress>();

    // Meta
    public string savedAtUtc;

    // ================== Nested Types ==================

    [Serializable]
    public class DeckSection
    {
        public int deckSize = 5;
        // slot i holds the unitId, or -1 if empty
        public int[] slots = new int[5] { -1, -1, -1, -1, -1 };
    }

    [Serializable]
    public class UnitBaseStatsSnapshot
    {
        public int hp;
        public int attack;
        public int defense;
        public float moveSpeed;
        public float attackSpeed;
        public float attackRange;   // <— added as requested
    }

    [Serializable]
    public class UnitStateEntry
    {
        public int unitId;
        public bool unlocked;
        public int level;
        public bool isDeployed;   // true => in Deployed bucket


        // -1 means not deployed; otherwise the deck slot index
        public int deckSlot = -1;

        // Optional: snapshot original/base stats at first sight
        public UnitBaseStatsSnapshot baseStats;

        public UnitStateEntry() { }

        public UnitStateEntry(int unitId, bool unlocked, int level , bool isDeployed)
        {
            this.unitId = unitId;
            this.unlocked = unlocked;
            this.level = Mathf.Max(1, level);
            this.isDeployed = isDeployed;
            this.deckSlot = -1;
        }
    }

    [Serializable]
    public class LevelProgress
    {
        public int levelId;
        public int highestUnlocked;  // 0-based, or your own convention
        public int[] stars;            // exactly what your Home UI expects
    }
}


public class SaveData1
{
    public int version = 1;

    // --- Economy
    public int gems = 0;
    public int coins = 0;

    // --- Units
    public List<UnitStateEntry> units = new List<UnitStateEntry>();

    // --- Stages (NEW)
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
        public UnitStateEntry() { }
        public UnitStateEntry(int unitId, bool unlocked, int level)
        {
            this.unitId = unitId;
            this.unlocked = unlocked;
            this.level = level < 1 ? 1 : level;
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

        // Optional: claimed chests, best time, etc. (add later)
        // public bool[] chestClaimed;
        // public float[] bestTimeSec;
    }
}
