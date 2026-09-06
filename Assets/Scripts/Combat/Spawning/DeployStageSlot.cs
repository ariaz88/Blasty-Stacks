using UnityEngine;

/// <summary>
/// One of the two PURCHASABLE side deploy stages on the player castle
/// (DestroyStageLeft / DestroyStageRight under PlayerCastle/Stage Holder/BoardStages).
///
/// Two jobs:
///   1. Show whether this side is locked or open.
///   2. Take the tap that buys it.
///
/// It does NOT decide anything about spawning. PlayerWaveManager and
/// FormationGapFiller both hold this stage in their arrays permanently and ask
/// IsUnlocked every time they build their list of usable columns - so a purchase
/// takes effect on the very next wave with nothing to re-wire.
///
/// WHY THE STAGE OBJECT STAYS ACTIVE WHILE LOCKED: the spawner looks the slot up
/// with GetComponent on the gate Transform, and a deactivated GameObject would
/// also take its collider and its visuals out of the scene. Locked state is a
/// LOOK (lockedVisual / unlockedVisual), never SetActive on this object.
/// </summary>
[DisallowMultipleComponent]
public class DeployStageSlot : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Which side this is. The two slots in a scene must not both be the same side.")]
    [SerializeField] private DeployStageSide side = DeployStageSide.Left;

    [Header("Price")]
    [Tooltip("Gems charged once, the first time this side is bought.")]
    [SerializeField, Min(0)] private int gemCost = 400;

    [Header("Visuals")]
    [Tooltip("Shown while the slot is LOCKED - padlock, dimmed platform, price tag. " +
             "Optional: leave empty if the locked look is handled elsewhere.")]
    [SerializeField] private GameObject lockedVisual;

    [Tooltip("Shown once the slot is OPEN - the normal platform and its flag. " +
             "Optional.")]
    [SerializeField] private GameObject unlockedVisual;

    [Header("Purchase")]
    [Tooltip("Confirmation popup. Leave EMPTY to buy immediately on tap with no " +
             "confirmation (useful while testing).")]
    [SerializeField] private DeployStagePurchasePrompt prompt;

    [Tooltip("OFF = tapping the slot does nothing, for scenes where the purchase " +
             "is driven from somewhere else entirely.")]
    [SerializeField] private bool tapToBuy = true;

    public DeployStageSide Side => side;
    public int GemCost => gemCost;

    /// <summary>
    /// Read LIVE by the spawner and the formation grid on every query - never
    /// cached by them, so a mid-battle purchase opens the column immediately.
    /// </summary>
    public bool IsUnlocked => DeployStageUnlockService.IsUnlocked(side);

    private void OnEnable()
    {
        DeployStageUnlockService.OnStageUnlocked += HandleStageUnlocked;
        ApplyVisualState();
    }

    private void OnDisable()
    {
        DeployStageUnlockService.OnStageUnlocked -= HandleStageUnlocked;
    }

    private void HandleStageUnlocked(DeployStageSide unlockedSide)
    {
        if (unlockedSide == side)
            ApplyVisualState();
    }

    private void ApplyVisualState()
    {
        bool open = IsUnlocked;

        if (lockedVisual != null) lockedVisual.SetActive(!open);
        if (unlockedVisual != null) unlockedVisual.SetActive(open);
    }

    /// <summary>
    /// World-space tap. Works on a Collider2D without an EventSystem raycaster,
    /// and keeps working while the game is "paused", because this project pauses
    /// with a FLAG rather than timeScale.
    /// </summary>
    private void OnMouseUpAsButton()
    {
        if (!tapToBuy) return;
        RequestPurchase();
    }

    /// <summary>
    /// Entry point for the tap and for any other UI that wants to sell this slot.
    /// Opens the prompt if there is one, otherwise buys on the spot.
    /// </summary>
    public void RequestPurchase()
    {
        if (IsUnlocked) return;

        if (prompt != null) prompt.Open(this);
        else Confirm();
    }

    /// <summary>
    /// Actually charges and unlocks. Called by the prompt's confirm button.
    /// Returns false when the player could not afford it - the slot stays locked
    /// and no gems move.
    /// </summary>
    public bool Confirm()
    {
        bool bought = DeployStageUnlockService.TryPurchase(side, gemCost);

        if (!bought)
            Debug.Log($"[DeployStageSlot] Not enough gems for the {side} stage ({gemCost}).");

        // The service raises OnStageUnlocked on success, which repaints us. This
        // call covers the already-unlocked early-out inside TryPurchase, which
        // deliberately raises nothing.
        ApplyVisualState();
        return bought;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // A collider is what makes OnMouseUpAsButton fire at all; without one the
        // slot is simply unclickable and the failure is completely silent.
        if (tapToBuy && GetComponent<Collider2D>() == null)
            Debug.LogWarning($"[DeployStageSlot] '{name}' has tapToBuy ON but no Collider2D - " +
                             "it cannot be tapped.", this);
    }
#endif
}
