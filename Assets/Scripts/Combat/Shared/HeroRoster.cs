using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "How many heroes of each unit type are alive right now, and how many did the
/// stage start the battle with."
///
/// Static on purpose: heroes are spawned at runtime by PlayerWaveManager and the
/// UI that reads this (HeroStatsPanel) may itself be switched on only when the
/// battle starts, so neither side can hold an Inspector reference to the other.
///
/// Ownership:
///   * PlayerManager.Start   -> Register    (its unitId comes from PlayerStatsApplier)
///   * PlayerManager.OnDestroy -> Unregister (death destroys the GameObject 0.5s
///     into the dying animation, so the tally drops as the body falls)
///   * BattleStartController's battle-start event -> SnapshotStartingCounts, which
///     freezes the denominator shown as "alive/total".
///
/// Everything is keyed by UnitDefinitionSO.unitId, never by prefab or name.
/// </summary>
public static class HeroRoster
{
    private class TypeEntry
    {
        public readonly HashSet<PlayerManager> Alive = new();
        public int StartingCount;
    }

    private static readonly Dictionary<int, TypeEntry> ByUnitId = new();

    /// <summary>Raised whenever a hero is registered, unregistered, or the snapshot is taken.</summary>
    public static event Action OnRosterChanged;

    /// <summary>
    /// Wipes the statics when Play mode starts. Required because "Enter Play Mode
    /// Options" can disable the domain reload, in which case a static Dictionary
    /// survives from the previous run and the first battle would open with the
    /// last session's counts.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ByUnitId.Clear();
        OnRosterChanged = null;
    }

    public static void Register(int unitId, PlayerManager hero)
    {
        if (hero == null) return;

        if (!ByUnitId.TryGetValue(unitId, out var entry))
        {
            entry = new TypeEntry();
            ByUnitId[unitId] = entry;
        }

        if (!entry.Alive.Add(hero)) return;   // already tracked

        Changed();
    }

    public static void Unregister(int unitId, PlayerManager hero)
    {
        if (hero == null) return;
        if (!ByUnitId.TryGetValue(unitId, out var entry)) return;
        if (!entry.Alive.Remove(hero)) return;

        Changed();
    }

    /// <summary>Living heroes of this type that are actually IN the field.</summary>
    public static int AliveCount(int unitId)
    {
        if (!ByUnitId.TryGetValue(unitId, out var entry)) return 0;

        Prune(entry);

        int n = 0;
        foreach (var pm in entry.Alive)
            if (IsInBattle(pm)) n++;

        return n;
    }

    /// <summary>
    /// A registered hero only counts once it has been RELEASED into the field.
    ///
    /// Heroes spawn onto the castle gates locked, and a puzzle match is what
    /// throws them out. Pressing BATTLE with a wave still sitting on the gates
    /// strands it there - it never fights and never dies. Counting those would
    /// pin the label above zero forever and the buy-back could never appear.
    /// </summary>
    private static bool IsInBattle(PlayerManager pm)
    {
        return pm != null && pm.isUnlocked;
    }

    /// <summary>The denominator: how many of this type stood on the field when BATTLE was pressed.</summary>
    public static int StartingCount(int unitId)
    {
        return ByUnitId.TryGetValue(unitId, out var entry) ? entry.StartingCount : 0;
    }

    /// <summary>
    /// Freezes the "/total" for every type currently on the field. Called once,
    /// when the battle phase begins - the puzzle board is hidden from that point
    /// on, so no further waves can arrive and the roster is stable.
    /// </summary>
    public static void SnapshotStartingCounts()
    {
        foreach (var kv in ByUnitId)
            kv.Value.StartingCount = AliveCount(kv.Key);

        Changed();
    }

    /// <summary>
    /// Unit ids that were present at the snapshot, i.e. the types that deserve a
    /// cell in the panel. Order follows the database, not spawn order - the caller
    /// sorts, this only reports.
    /// </summary>
    public static List<int> UnitIdsInBattle()
    {
        var ids = new List<int>(ByUnitId.Count);
        foreach (var kv in ByUnitId)
            if (kv.Value.StartingCount > 0) ids.Add(kv.Key);

        return ids;
    }

    /// <summary>Drops every type and its snapshot. Used when a stage restarts.</summary>
    public static void ClearAll()
    {
        if (ByUnitId.Count == 0) return;

        ByUnitId.Clear();
        Changed();
    }

    private static void Prune(TypeEntry entry)
    {
        entry.Alive.RemoveWhere(pm => pm == null);
    }

    private static void Changed()
    {
        try { OnRosterChanged?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
