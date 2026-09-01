// FeatureSlotUI.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// One coin-gated slot in the Feature Panel - Posion, Battery or Hammer.
///
/// Each slot is TWO buttons that do the SAME thing (the icon on top and the bar
/// underneath) plus TWO mutually exclusive bottom states:
///
///   ENOUGH COINS : "Potion Button" is on - a live button the player can press.
///   NOT ENOUGH   : "Deactive" is on instead - just the price text and coin icon,
///                  no Button component, so it cannot be pressed at all.
///
/// Exactly one of those two is active at any moment. Both buttons are also driven
/// to interactable = CanAfford, so a press cannot slip through in the one frame
/// before the roots are swapped.
///
/// NOTE: the features themselves do not exist yet. This component ONLY handles the
/// affordability state and routes the press into onUse - it deliberately does NOT
/// spend the coins. Whoever builds the actual Potion/Battery/Hammer effect calls
/// CurrencyManager.TrySpendCoins there. See NOTES in the doc.
/// </summary>
public class FeatureSlotUI : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Only used for log messages - Potion, Battery, Hammer.")]
    [SerializeField] private string featureName = "Feature";

    [Tooltip("Coins the player needs before this slot unlocks. Also written into costText.")]
    [SerializeField, Min(0)] private int coinCost = 600;

    [Header("Buttons - BOTH trigger the same action")]
    [Tooltip("The picture button on top. In the prefab this is 'Battery Btn'.")]
    [SerializeField] private Button iconButton;

    [Tooltip("The bar button underneath. In the prefab this is 'Potion Button'.")]
    [SerializeField] private Button actionButton;

    [Header("The two bottom states - exactly one is ever on")]
    [Tooltip("Shown when the player CAN afford it - the prefab's 'Potion Button'. " +
             "Usually the same object as actionButton.")]
    [SerializeField] private GameObject affordableRoot;

    [Tooltip("Shown when the player CANNOT afford it - the prefab's 'Deactive', " +
             "which holds the price Text (TMP) and the Coin icon.")]
    [SerializeField] private GameObject lockedRoot;

    [Header("Price label")]
    [Tooltip("The number inside 'Deactive'. Rewritten from coinCost so the design " +
             "value and the shown value cannot drift apart. Safe to leave empty.")]
    [SerializeField] private TMP_Text costText;

    [Header("Action")]
    [Tooltip("Fired when either button is pressed AND the player can afford it. " +
             "Wire the real feature here once it exists - this component does not " +
             "spend the coins itself.")]
    [SerializeField] private UnityEvent onUse;

    /// <summary>True while the player has at least coinCost coins.</summary>
    public bool CanAfford { get; private set; }

    /// <summary>Raised when either button is pressed and the slot was affordable.</summary>
    public event Action<FeatureSlotUI> OnUsed;

    private void OnEnable()
    {
        if (iconButton) iconButton.onClick.AddListener(OnPressed);
        if (actionButton) actionButton.onClick.AddListener(OnPressed);

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (iconButton) iconButton.onClick.RemoveListener(OnPressed);
        if (actionButton) actionButton.onClick.RemoveListener(OnPressed);

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
    }

    /// <summary>
    /// CurrencyManager is a DontDestroyOnLoad singleton that may not exist yet when
    /// this slot's OnEnable runs on the first frame of a stage, so the subscription
    /// is retried here once script order has settled.
    /// </summary>
    private void Start()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged -= OnCurrencyChanged;
            CurrencyManager.Instance.OnCurrencyChanged += OnCurrencyChanged;
        }

        Refresh();
    }

    private void OnCurrencyChanged(string id, int oldValue, int newValue) => Refresh();

    /// <summary>
    /// Pushes the current coin balance onto the two roots and both buttons.
    /// Public so a future feature can force a redraw after spending.
    /// </summary>
    public void Refresh()
    {
        int coins = CurrencyManager.Instance != null ? CurrencyManager.Instance.Coins : 0;
        CanAfford = coins >= coinCost;

        // Exactly one of the two, never both - they sit at the same position.
        if (affordableRoot) affordableRoot.SetActive(CanAfford);
        if (lockedRoot) lockedRoot.SetActive(!CanAfford);

        // The icon button is NOT inside either root, so it needs disabling on its
        // own or it would stay pressable while the slot reads as locked.
        if (iconButton) iconButton.interactable = CanAfford;
        if (actionButton) actionButton.interactable = CanAfford;

        if (costText) costText.text = coinCost.ToString();
    }

    private void OnPressed()
    {
        if (!CanAfford)
        {
            // Reachable only if something re-enabled a button behind our back.
            Debug.Log($"[FeatureSlotUI] {featureName} pressed while locked - needs {coinCost} coins.", this);
            return;
        }

        onUse?.Invoke();
        OnUsed?.Invoke(this);
    }
}
