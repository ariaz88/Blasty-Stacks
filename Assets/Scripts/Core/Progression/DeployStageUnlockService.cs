using System;
using UnityEngine;

/// <summary>Which of the two purchasable side stages on the player castle.</summary>
public enum DeployStageSide
{
    Left,
    Right
}

/// <summary>
/// The authority on whether the LEFT / RIGHT deploy stages have been bought.
///
/// The castle ships with four always-available deploy stages plus these two side
/// slots, which start locked. Each is bought once with gems and then stays open
/// for the rest of the game, on every level - so the flag lives in SaveData, not
/// in the scene.
///
/// Static, like SaveSystem, so nothing has to be wired in the Inspector and a
/// stage scene loaded straight from the editor still reads the right state.
/// </summary>
public static class DeployStageUnlockService
{
    /// <summary>
    /// Raised right after a side is unlocked. The castle slot repaints itself,
    /// and the spawner / formation grid pick the new column up on their next
    /// query - both read IsUnlocked live rather than caching it.
    /// </summary>
    public static event Action<DeployStageSide> OnStageUnlocked;

    public static bool IsUnlocked(DeployStageSide side) =>
        SaveSystem.IsDeployStageUnlocked(side);

    /// <summary>
    /// Charges the player and opens the stage permanently.
    ///
    /// Returns false and changes NOTHING when the player cannot afford it, so the
    /// caller can play a "not enough gems" beat. Buying a side that is already
    /// unlocked returns true WITHOUT charging again - a double tap on the slot,
    /// or a stale prompt left open, must never take a second payment.
    /// </summary>
    public static bool TryPurchase(DeployStageSide side, int gemCost)
    {
        if (IsUnlocked(side))
            return true;

        var wallet = CurrencyManager.Instance;
        if (wallet == null)
        {
            Debug.LogWarning("[DeployStageUnlockService] No CurrencyManager - refusing to " +
                             "unlock for free rather than handing out a paid slot.");
            return false;
        }

        if (!wallet.TrySpendGems(Mathf.Max(0, gemCost)))
            return false;

        Unlock(side);
        return true;
    }

    /// <summary>Opens a side without charging. Debug / rewards / testing.</summary>
    public static void Unlock(DeployStageSide side)
    {
        if (IsUnlocked(side)) return;

        SaveSystem.SetDeployStageUnlocked(side, true);
        OnStageUnlocked?.Invoke(side);

        Debug.Log($"[DeployStageUnlockService] {side} deploy stage unlocked.");
    }

    /// <summary>Re-locks both sides. Debug / testing only.</summary>
    public static void ResetAll()
    {
        SaveSystem.SetDeployStageUnlocked(DeployStageSide.Left, false);
        SaveSystem.SetDeployStageUnlocked(DeployStageSide.Right, false);
        Debug.Log("[DeployStageUnlockService] Both side deploy stages re-locked.");
    }
}
