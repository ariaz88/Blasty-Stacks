using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime model of the player's unit progression (unlocked + level).
/// Design-time data (stats/portraits) live in UnitDefinitionSO / UnitsDatabaseSO.
/// This class holds ONLY the mutable player state and exposes simple APIs.
/// </summary>
[Serializable]
public class PlayerUnitsModel
{
    [Serializable]
    public class UnitState
    {
        public int unitId;
        public bool unlocked;
        public int level;      // upgrade level (>= 1)

        // NEW:
        public bool isDeployed;

        public UnitState() { }
        public UnitState(int id, bool unlocked, int level, bool isDeployed=false)
        {
            this.unitId = id;
            this.unlocked = unlocked;
            this.level = Mathf.Max(1, level);
            this.isDeployed = isDeployed;
        }

    }

    // Internal map for quick access; serialize a list for persistence if needed.
    private readonly Dictionary<int, UnitState> _byId = new Dictionary<int, UnitState>();

    /// <summary> Enumerate all current states (e.g., for saving). </summary>
    public IEnumerable<UnitState> AllStates
    {
        get { return _byId.Values; }
    }


    // CHANGED
    public bool IsDeployed(int unitId)
    {
        if (!_byId.TryGetValue(unitId, out var u)) return false;
        return u.isDeployed;
    }

    // CHANGED
    public void SetDeployed(int unitId, bool deployed)
    {
        if (!_byId.TryGetValue(unitId, out var u)) return;
        if (u.isDeployed == deployed) return;
        u.isDeployed = deployed;
    }

    // CHANGED
    public void SeedInitialDeployed(IEnumerable<int> deployedUnitIds)
    {
        if (deployedUnitIds == null) return;
        foreach (var id in deployedUnitIds)
        {
            if (_byId.TryGetValue(id, out var u))
            {
                u.unlocked = true;         // ensure owned
                u.isDeployed = true;       // put in Deployed bucket
                if (u.level < 1) u.level = 1;
            }
        }
    }




    /// <summary>
    /// Initialize from the design-time database.
    /// By default, mark all units locked at level 1, except any you pass in as unlocked.
    /// </summary>
    public void InitializeFromDatabase1(UnitsDatabaseSO db, params int[] initiallyUnlockedUnitIds)
    {
        _byId.Clear();
        if (db == null || db.Units == null) return;

        var unlockedSet = new HashSet<int>(initiallyUnlockedUnitIds ?? Array.Empty<int>());
        foreach (var def in db.Units)
        {
            if (def == null) continue;
            int id = def.unitId;
            bool isUnlocked = unlockedSet.Contains(id);
            _byId[id] = new UnitState(id, isUnlocked, 1);
        }
    }

    public void InitializeFromDatabase(UnitsDatabaseSO db, params int[] initiallyUnlockedUnitIds)
    {
        _byId.Clear();
        if (db == null || db.Units == null) return;

        var unlockedSet = new HashSet<int>(initiallyUnlockedUnitIds ?? Array.Empty<int>());
        foreach (var def in db.Units)
        {
            if (def == null) continue;
            int id = def.unitId;
            bool isUnlocked = unlockedSet.Contains(id);
            bool isDeployed = def.startsDeployed && isUnlocked; // Respect SO default
            _byId[id] = new UnitState(id, isUnlocked, 1, isDeployed);
        }
        // NEW: If saved data exists, it will override in GameStartManager
    }

    /// <summary> Returns true if we track a state for this unit id. </summary>
    public bool HasUnit(int unitId) => _byId.ContainsKey(unitId);

    /// <summary> Get a readonly snapshot; returns null if unknown. </summary>
    public UnitState GetState(int unitId)
    {
        _byId.TryGetValue(unitId, out var s);
        return s;
    }

    /// <summary> Current level (>=1). If unit not found, returns 1. </summary>
    public int GetLevel(int unitId)
    {
        return _byId.TryGetValue(unitId, out var s) ? s.level : 1;
    }

    /// <summary> Is this unit unlocked? </summary>
    public bool IsUnlocked(int unitId)
    {
        return _byId.TryGetValue(unitId, out var s) && s.unlocked;
    }

    /// <summary> Unlock a unit (no-op if already unlocked). </summary>
    public void Unlock(int unitId)
    {
        if (_byId.TryGetValue(unitId, out var s))
        {
            s.unlocked = true;
        }
    }

    /// <summary>
    /// Set an explicit level (min 1). Does not enforce caps (service should).
    /// Returns the final level after clamping.
    /// </summary>
    public int SetLevel(int unitId, int level)
    {
        if (!_byId.TryGetValue(unitId, out var s))
        {
            s = new UnitState(unitId, false, 1);
            _byId[unitId] = s;
        }
        s.level = Mathf.Max(1, level);
        return s.level;
    }

    /// <summary>
    /// Increment level by 1 and return the new level.
    /// Does not handle currency or caps (handled by PlayerProgressionService).
    /// </summary>
    public int LevelUp(int unitId)
    {
        if (!_byId.TryGetValue(unitId, out var s))
        {
            s = new UnitState(unitId, false, 1);
            _byId[unitId] = s;
        }
        s.level = Mathf.Max(1, s.level + 1);
        return s.level;
    }

    /// <summary>
    /// Replace internal data from a saved list (used by SaveSystem.Load()).
    /// Unknown units will be added; known units overwritten.
    /// </summary>
    // CHANGED
    public void LoadFromSavedStates1(IEnumerable<UnitState> saved)
    {
        _byId.Clear();
        if (saved == null) return;
        foreach (var s in saved)
        {
            if (s == null) continue;
            // copy all fields, including isDeployed
            var copy = new UnitState(s.unitId, s.unlocked, s.level)
            {
                isDeployed = s.isDeployed
            };
            _byId[copy.unitId] = copy;
        }
    }
    // CHANGED: LoadFromSavedStates (now correctly typed for PlayerUnitsModel.UnitState)
    public void LoadFromSavedStates(IEnumerable<PlayerUnitsModel.UnitState> saved)
    {
        _byId.Clear();
        if (saved == null) return;
        foreach (var s in saved)
        {
            if (s == null || s.unitId < 0) continue;
            // Copy all fields, including isDeployed
            var copy = new UnitState(s.unitId, s.unlocked, s.level, s.isDeployed);
            _byId[copy.unitId] = copy;
        }
    }
    // NEW: Export to serializable SaveData entries (public for access from GameStartManager)
    public List<SaveData.UnitStateEntry> ToSavedEntries()
    {
        var list = new List<SaveData.UnitStateEntry>();
        foreach (var kv in _byId)
        {
            var state = kv.Value;
            list.Add(new SaveData.UnitStateEntry(state.unitId, state.unlocked, state.level, state.isDeployed));
        }
        return list;
    }
    public List<UnitState> ToSavedStates()
    {
        // returns live instances; callers serialize fields including isDeployed
        return new List<UnitState>(_byId.Values);
    }

    public void Lock(int unitId)
    {
        if (!_byId.TryGetValue(unitId, out var u)) return;
        u.unlocked = false;
        u.isDeployed = false;
        if (u.level < 1) u.level = 1;
        _byId[unitId] = u;
    }




    // .............................................................

    // Returns all unitIds filtered by deployed/unlocked flags.
    public List<int> GetAll(bool deployed, bool unlockedOnly = true)
    {
        var list = new List<int>(_byId.Count);
        foreach (var kv in _byId)
        {
            var s = kv.Value;
            if (unlockedOnly && !s.unlocked) continue;
            if (s.isDeployed == deployed) list.Add(s.unitId);
        }
        return list;
    }

    // Convenience: all unlocked ids (both deployed + undeployed)
    public List<int> GetAllUnlocked()
    {
        var list = new List<int>(_byId.Count);
        foreach (var kv in _byId)
        {
            var s = kv.Value;
            if (s.unlocked) list.Add(s.unitId);
        }
        return list;
    }

    // (Optional) all ids tracked by the model (locked + unlocked)
    public List<int> GetAllIds()
    {
        var list = new List<int>(_byId.Count);
        foreach (var kv in _byId) list.Add(kv.Key);
        return list;
    }

    public bool Exists(int unitId)
    {
        return _byId != null && _byId.ContainsKey(unitId);
    }



}
