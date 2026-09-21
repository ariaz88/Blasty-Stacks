// SavePersistence.cs
using UnityEngine;

/// <summary>
/// TEMPORARY DEBUG GATE over all disk persistence.
///
/// While <see cref="Enabled"/> is false:
///   - <see cref="SaveSystem.Save"/> keeps updating the in-memory cache but never
///     writes PlayerPrefs, so progress made during a Play session is live but volatile.
///   - <see cref="SaveSystem"/> ignores any existing "GAME_SAVE_V1" blob on load.
///   - <see cref="LevelManager"/> ignores "LM.CurrentStage" and boots at startingStage.
///   - <see cref="GameStartManager"/> seeds coins/gems/heroXP from
///     <see cref="FreshStartCoins"/> etc. instead of the CurrencyManager inspector values.
/// Net effect: every Play run starts at stage 1 with zero resources.
///
/// The toggle itself lives in its OWN PlayerPrefs key, so SaveSystem.ResetAll()
/// (which only deletes GAME_SAVE_V1) never clears it.
///
/// SAFETY: in a non-development player this gate is hard-wired ON, so shipping a
/// build while the editor toggle is off cannot disable saving for real players.
/// </summary>
public static class SavePersistence
{
    private const string ToggleKey = "DEBUG_SAVE_PERSISTENCE_ENABLED";

    /// <summary>Resource values a fresh (non-persisted) session starts with.</summary>
    public const int FreshStartCoins = 0;
    public const int FreshStartGems = 0;
    public const int FreshStartHeroXp = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Cached so the hot paths (Save() runs on almost every state change) don't hit
    // PlayerPrefs. -1 = not read yet.
    private static int _cached = -1;

    /// <summary>
    /// False = nothing is read from or written to disk (default while debugging).
    /// Setting it true flushes the current in-memory state straight to disk.
    /// </summary>
    public static bool Enabled
    {
        get
        {
            if (_cached < 0) _cached = PlayerPrefs.GetInt(ToggleKey, 0); // default OFF
            return _cached != 0;
        }
        set
        {
            if (_cached >= 0 && (_cached != 0) == value) return;         // no change

            _cached = value ? 1 : 0;
            PlayerPrefs.SetInt(ToggleKey, _cached);
            PlayerPrefs.Save();

            if (value)
            {
                // Turning saving back on: commit whatever this session has built up,
                // otherwise the first write would only land on the next state change.
                SaveSystem.Save();
                LevelManager.PersistCurrentStage();
            }

            Debug.Log($"[SavePersistence] Save/Load is now {(value ? "ENABLED" : "DISABLED")}.");
        }
    }

    public static void Toggle() => Enabled = !Enabled;
#else
    /// <summary>Release builds always persist; the debug gate is editor/dev-only.</summary>
    public static bool Enabled { get => true; set { } }

    public static void Toggle() { }
#endif
}
