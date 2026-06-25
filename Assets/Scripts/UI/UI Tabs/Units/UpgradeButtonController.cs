using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the Upgrade button:
/// - Shows "Upgrade (COST)" or "Max"
/// - Enables only when the selected unit is unlocked, not at cap, and affordable
/// - On click, attempts the upgrade via PlayerProgressionService
/// 
/// Attach to your Upgrade button GameObject. Provide references in Inspector.
/// The UnitsPanelController should call SetSelectedUnit(unitId) whenever selection changes.
/// </summary>
public class UpgradeButtonController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text label;

    private GameStartManager _gsm;
    private PlayerProgressionService _svc;

    private int _selectedUnitId = -1;
    private string _lastReason = null;

    private void Awake()
    {
        if (!button) button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _gsm = GameStartManager.Instance;
        _svc = _gsm != null ? _gsm.ProgressionService : null;

        if (_svc == null)
        {
            Debug.LogError("[UpgradeButtonController] ProgressionService is null. Check Game Start Manager setup.");
            if (button) button.interactable = false;
            return;
        }

        // Subscribe to external changes that affect interactability/label
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;

        _svc.OnUnitUpgraded += HandleUnitUpgraded;

        Refresh();
        if (button)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }
    }

    private void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;

        if (_svc != null)
            _svc.OnUnitUpgraded -= HandleUnitUpgraded;

        if (button) button.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Sets which unit this button is controlling.
    /// Call this when the user selects a different card.
    /// </summary>
    public void SetSelectedUnit(int unitId)
    {
        _selectedUnitId = unitId;
        Refresh();
    }

    /// <summary>
    /// Recomputes the label ("Upgrade (COST)" / "Max") and interactable state
    /// based on lock state, cap, and current gems. Stores a short "reason" if disabled.
    /// </summary>
    private void Refresh()
    {
        if (_svc == null || _selectedUnitId < 0)
        {
            SetUI(text: "Upgrade", interactable: false);
            _lastReason = "NoSelection";
            return;
        }

        // Query cost & feasibility
        int cost = _svc.GetUpgradeCost(_selectedUnitId);
        bool can = _svc.CanUpgrade(_selectedUnitId, out string reason);
        _lastReason = can ? null : reason;

        // Label text
        string txt = cost > 0 ? $"Upgrade ({cost})" : "Max";
        SetUI(txt, can);
    }

    /// <summary>
    /// Handles click:
    /// - If disabled, show a brief reason in Console (or hook your toast).
    /// - If enabled, attempts the upgrade. Success will trigger service events that refresh the UI.
    /// </summary>
    private void HandleClick()
    {
        if (button != null && !button.interactable)
        {
            // Optional: route to a toast/snackbar
            if (!string.IsNullOrEmpty(_lastReason))
            {
                switch (_lastReason)
                {
                    case "Locked": Debug.Log("Unit is locked."); break;
                    case "AtCap": Debug.Log("Reached max level."); break;
                    case "NotEnoughGems": Debug.Log("Not enough gems."); break;
                    default: Debug.Log("Cannot upgrade."); break;
                }
            }
            return;
        }

        if (_svc == null || _selectedUnitId < 0) return;
        _svc.TryUpgrade(_selectedUnitId);
        // On success, OnUnitUpgraded will be raised → Refresh() in the handler
    }

    /// <summary>
    /// Currency changed → if it's Gems, refresh cost/affordability.
    /// </summary>
    private void HandleCurrencyChanged(string currency, int newValue, int delta)
    {
        if (currency == "Gems")
            Refresh();
    }

    /// <summary>
    /// A unit upgraded somewhere → if it's our selected unit, refresh.
    /// </summary>
    private void HandleUnitUpgraded(int unitId, int oldLevel, int newLevel, int cost)
    {
        if (unitId == _selectedUnitId)
            Refresh();
    }

    private void SetUI(string text, bool interactable)
    {
        if (label) label.text = text;
        if (button) button.interactable = interactable;
    }
}
