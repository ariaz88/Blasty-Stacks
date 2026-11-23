// BattleEnergyService.cs
using System;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Owns the rule for "may the player start another battle?".
///
/// A play session starts with 20 battles shared by every stage. The session
/// balance survives scene loads and resets when the app starts a new session.
///
/// Once the allowance is used up, rewarded ads can add five more starts.
/// Each subsequent battle costs one energy unit.
/// </summary>
public static class BattleEnergyService
{
    /// <summary>Initial battles per play session.</summary>
    public const int DefaultDailyBattleLimit = 20;

    /// <summary>One energy unit buys one battle after the initial allowance.</summary>
    public const int DefaultEnergyCostPerBattle = 1;

    public const int RewardedAdBattles = 5;

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
    /// While true the allowance belongs to the current app session and is
    /// held in memory across stage changes. Set by BattleStartController.
    /// </summary>
    public static bool SessionOnly { get; set; } = true;

    private static SaveData.BattleEnergyState _sessionState;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void BeginNewSession()
    {
        _sessionState = null;
        SessionOnly = true;
    }

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

    /// <summary>Battles started in the current session.</summary>
    public static int BattlesUsed
    {
        get { RefreshWindow(); return State.battlesUsed; }
    }

    /// <summary>Initial session battles left before ad credits are used.</summary>
    public static int RemainingFree(int dailyLimit)
    {
        RefreshWindow();
        return Mathf.Max(0, dailyLimit - State.battlesUsed);
    }

    /// <summary>All starts currently available, including those earned from ads.</summary>
    public static int RemainingBattles(int dailyLimit, int energyCost)
    {
        int cost = Mathf.Max(1, energyCost);
        return RemainingFree(dailyLimit) + State.energy / cost;
    }

    /// <summary>Extra starts earned from rewarded ads.</summary>
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

    /// <summary>Credits energy after a rewarded ad grants its reward.</summary>
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
        if (SessionOnly) return;
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
