// BattleEnergyService.cs
using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Owns the rule for "may the player start another battle?".
///
/// The player gets a free allowance of battles (default 25) inside a rolling
/// 24-hour window. The window opens on the FIRST battle after a reset, not at
/// midnight, so it does not depend on the device timezone. The allowance is
/// global - it is shared by every scene and survives app restarts, because it
/// lives in SaveData via SaveSystem rather than in scene state.
///
/// Once the allowance is used up, each further battle costs `energyCost`
/// energy instead.
///
/// ENERGY IS A PLACEHOLDER: nothing grants energy yet, so today the player is
/// simply blocked until the window resets. When the real energy economy is
/// written, feed it through AddEnergy / Energy below and everything else in
/// this class keeps working unchanged.
/// </summary>
public static class BattleEnergyService
{
    /// <summary>Free battles per 24h window. Overridable per-scene from the Inspector.</summary>
    public const int DefaultDailyBattleLimit = 25;

    /// <summary>PLACEHOLDER cost shown under the BATTLE icon, charged past the free allowance.</summary>
    public const int DefaultEnergyCostPerBattle = 25;

    private const double WindowHours = 24.0;

    public enum StartCheck
    {
        AllowedFree,             // still inside the free daily allowance
        AllowedPaidWithEnergy,   // allowance spent, energy covered this battle
        BlockedNoEnergy          // allowance spent and not enough energy
    }

    /// <summary>Raised whenever the used count or energy changes, so UI can refresh.</summary>
    public static event Action OnAllowanceChanged;

    /// <summary>
    /// TEST MODE. While true the allowance is held in memory only and is never
    /// written to the save file, so every Play session starts with a full
    /// allowance. Set from BattleStartController's inspector flag.
    ///
    /// This exists because GameStartManager - which wipes the save on every run
    /// - is NOT in the gameplay scenes; it arrives via DontDestroyOnLoad from
    /// the menu scene. Pressing Play directly on a stage would therefore
    /// persist the count. TURN THIS OFF FOR RELEASE.
    /// </summary>
    public static bool SessionOnly { get; set; }

    private static SaveData.BattleEnergyState _sessionState;

    private static SaveData.BattleEnergyState State =>
        SessionOnly
            ? (_sessionState ??= new SaveData.BattleEnergyState())
            : SaveSystem.GetBattleEnergy();

    /// <summary>
    /// Single write path. Goes to memory in SessionOnly mode, otherwise through
    /// SaveSystem so it lands in the save file.
    /// </summary>
    private static void Persist(string windowStartUtc, int battlesUsed, int energy)
    {
        if (!SessionOnly)
        {
            SaveSystem.SetBattleEnergy(windowStartUtc, battlesUsed, energy);
            return;
        }

        var state = State;
        state.windowStartUtc = windowStartUtc ?? "";
        state.battlesUsed = Mathf.Max(0, battlesUsed);
        state.energy = Mathf.Max(0, energy);
    }

    // ---------------- Queries ----------------

    /// <summary>Battles started inside the current window (0 once the window expires).</summary>
    public static int BattlesUsed
    {
        get { RefreshWindow(); return State.battlesUsed; }
    }

    /// <summary>Free battles left in the current window.</summary>
    public static int RemainingFree(int dailyLimit)
    {
        RefreshWindow();
        return Mathf.Max(0, dailyLimit - State.battlesUsed);
    }

    /// <summary>PLACEHOLDER balance - no economy feeds this yet.</summary>
    public static int Energy => State.energy;

    /// <summary>UTC instant the current window expires, or null when no window is open.</summary>
    public static DateTime? WindowResetUtc
    {
        get
        {
            RefreshWindow();
            return TryGetWindowStart(out var start) ? start.AddHours(WindowHours) : (DateTime?)null;
        }
    }

    /// <summary>How long until the free allowance refills. Zero when no window is open.</summary>
    public static TimeSpan TimeUntilReset
    {
        get
        {
            var reset = WindowResetUtc;
            if (reset == null) return TimeSpan.Zero;

            var left = reset.Value - DateTime.UtcNow;
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }
    }

    /// <summary>What Consume() would return, without spending anything.</summary>
    public static StartCheck Peek(int dailyLimit, int energyCost)
    {
        RefreshWindow();

        if (State.battlesUsed < dailyLimit)
            return StartCheck.AllowedFree;

        if (energyCost <= 0 || State.energy >= energyCost)
            return StartCheck.AllowedPaidWithEnergy;

        return StartCheck.BlockedNoEnergy;
    }

    // ---------------- Mutations ----------------

    /// <summary>
    /// Charges one battle: uses the free allowance first, then energy.
    /// Nothing is spent when the result is BlockedNoEnergy.
    /// </summary>
    public static StartCheck Consume(int dailyLimit, int energyCost)
    {
        var result = Peek(dailyLimit, energyCost);
        if (result == StartCheck.BlockedNoEnergy)
            return result;

        // The first battle of a fresh window is what opens the 24h clock.
        string windowStart = TryGetWindowStart(out _)
            ? State.windowStartUtc
            : DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        int energy = State.energy;
        if (result == StartCheck.AllowedPaidWithEnergy)
            energy = Mathf.Max(0, energy - Mathf.Max(0, energyCost));

        // battlesUsed keeps counting past dailyLimit so UI can show the overrun.
        Persist(windowStart, State.battlesUsed + 1, energy);
        RaiseChanged();

        return result;
    }

    /// <summary>PLACEHOLDER granter - call this from the real energy economy later.</summary>
    public static void AddEnergy(int amount)
    {
        if (amount == 0) return;

        Persist(State.windowStartUtc, State.battlesUsed, Mathf.Max(0, State.energy + amount));
        RaiseChanged();
    }

    /// <summary>Debug/testing helper: clears the window so the full allowance is available again.</summary>
    public static void ResetAllowance()
    {
        Persist("", 0, State.energy);
        RaiseChanged();
    }

    // ---------------- Internals ----------------

    /// <summary>Clears the window once 24h have elapsed (or the saved timestamp is unusable).</summary>
    private static void RefreshWindow()
    {
        var state = State;
        if (state.battlesUsed <= 0 && string.IsNullOrEmpty(state.windowStartUtc))
            return; // nothing to expire

        if (!TryGetWindowStart(out var start))
        {
            // Corrupt/missing timestamp but a non-zero count: treat as expired
            // rather than locking the player out forever.
            ClearWindow();
            return;
        }

        var now = DateTime.UtcNow;

        // now < start means the device clock moved backwards. There is no
        // trustworthy way to reason about the elapsed time, so restart the
        // window. See NOTES in the doc: this is only clock-honest, not
        // clock-proof - real enforcement needs server time.
        if (now < start || (now - start).TotalHours >= WindowHours)
            ClearWindow();
    }

    private static void ClearWindow()
    {
        Persist("", 0, State.energy);
    }

    private static bool TryGetWindowStart(out DateTime startUtc)
    {
        startUtc = default;

        var raw = State.windowStartUtc;
        if (string.IsNullOrEmpty(raw)) return false;

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                                 DateTimeStyles.RoundtripKind, out startUtc);
    }

    private static void RaiseChanged()
    {
        try { OnAllowanceChanged?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
