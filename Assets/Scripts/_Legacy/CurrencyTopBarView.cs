using UnityEngine;
using TMPro;

/// <summary>
/// Simple top-bar currency display:
/// - Reads current Gems (and optional Coins) from CurrencyManager
/// - Subscribes to OnCurrencyChanged to update instantly
/// 
/// Attach to a UI object in your persistent HUD/top bar.
/// </summary>
public class CurrencyTopBarView : MonoBehaviour
{
    [Header("UI Refs")]
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private TMP_Text coinsText; // optional; can leave null

    private void OnEnable()
    {
        RefreshAll();

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;
    }

    private void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    /// <summary>
    /// Currency changed → update only the affected label.
    /// </summary>
    private void HandleCurrencyChanged(string currency, int newValue, int delta)
    {
        switch (currency)
        {
            case "Gems":
                if (gemsText) gemsText.text = newValue.ToString();
                break;
            case "Coins":
                if (coinsText) coinsText.text = newValue.ToString();
                break;
        }
    }

    /// <summary>
    /// Reads current balances and updates both labels (called on enable).
    /// </summary>
    private void RefreshAll()
    {
        if (CurrencyManager.Instance == null)
        {
            if (gemsText) gemsText.text = "0";
            if (coinsText) coinsText.text = "0";
            return;
        }

        if (gemsText) gemsText.text = CurrencyManager.Instance.Gems.ToString();
        if (coinsText) coinsText.text = CurrencyManager.Instance.Coins.ToString();
    }
}
