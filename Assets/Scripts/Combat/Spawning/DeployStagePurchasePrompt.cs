using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The "Unlock this deploy stage for N gems?" confirmation, shared by both side
/// slots - one prompt in the scene, whichever slot was tapped fills it in.
///
/// Deliberately dumb: it holds no price and no unlock state of its own. Open()
/// takes the slot, reads the price off it, and Confirm hands the decision
/// straight back to that slot. Nothing here can unlock a stage or move gems.
/// </summary>
public class DeployStagePurchasePrompt : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("The panel switched on and off. Left empty = this GameObject.")]
    [SerializeField] private GameObject panelRoot;

    [Header("UI")]
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Tooltip("Optional - shown instead of the confirm button when the player " +
             "cannot afford the slot.")]
    [SerializeField] private GameObject notEnoughGemsHint;

    // Which slot opened us. Cleared on close so a stale confirm cannot fire.
    private DeployStageSlot pending;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(Close);

        panelRoot.SetActive(false);
    }

    public void Open(DeployStageSlot slot)
    {
        if (slot == null) return;

        pending = slot;

        if (costText != null)
            costText.text = slot.GemCost.ToString();

        // Affordability is only a HINT here. The real check is TrySpendGems
        // inside the service, which is the only thing that can actually refuse -
        // this just avoids offering a button that is guaranteed to fail.
        bool affordable = CurrencyManager.Instance == null ||
                          CurrencyManager.Instance.Gems >= slot.GemCost;

        if (confirmButton != null) confirmButton.gameObject.SetActive(affordable);
        if (notEnoughGemsHint != null) notEnoughGemsHint.SetActive(!affordable);

        panelRoot.SetActive(true);
    }

    public void Close()
    {
        pending = null;
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnConfirm()
    {
        // Captured before Close(), which clears it.
        var slot = pending;
        Close();

        if (slot != null)
            slot.Confirm();
    }
}
