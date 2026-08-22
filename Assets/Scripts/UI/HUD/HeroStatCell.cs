using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ONE hero type in the battle HUD's "Heroes Stats panel" - the "Hero 1" object.
///
/// It has exactly two looks:
///   ALIVE : portrait in colour, "alive/total" under it, buy button hidden.
///   WIPED : portrait greyed out, count hidden, buy button shown with its gem price.
///
/// It owns no game logic. HeroStatsPanel clones it once per hero type present in
/// the battle, feeds it numbers, and handles the purchase itself.
/// </summary>
public class HeroStatCell : MonoBehaviour
{
    [Header("Avatar")]
    [Tooltip("The 'Hero avatar' Image. Its sprite comes from UnitDefinitionSO.portrait.")]
    [SerializeField] private Image avatar;

    [Tooltip("Grey overlay drawn on top of the avatar while every hero of this type is dead - " +
             "the 'Gray out Avatar' object. When this is assigned the avatar's own colour is " +
             "NEVER touched. Left empty = the two tints below are used instead.")]
    [SerializeField] private GameObject deadOverlay;

    [Tooltip("Fallback ONLY, used when deadOverlay is empty: the avatar's colour while at " +
             "least one hero of this type is alive. White = the portrait's own colours.")]
    [SerializeField] private Color aliveTint = Color.white;

    [Tooltip("Fallback ONLY, used when deadOverlay is empty: the avatar's colour once every " +
             "hero of this type is dead.")]
    [SerializeField] private Color deadTint = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("Count")]
    [Tooltip("The 'Hero Count' label - shows '2/3'.")]
    [SerializeField] private TMP_Text countText;

    [Header("Buy Back")]
    [Tooltip("The 'Parent' Button holding the gem icon + price. Starts switched OFF.")]
    [SerializeField] private GameObject buyRoot;

    [Tooltip("The Button component on 'Parent'. Left empty = taken from buyRoot.")]
    [SerializeField] private Button buyButton;

    [Tooltip("The 'gem Amount' label on the buy button.")]
    [SerializeField] private TMP_Text gemCostText;

    /// <summary>Which unit type this cell stands for. Matches UnitDefinitionSO.unitId.</summary>
    public int UnitId { get; private set; } = -1;

    /// <summary>Squad size when the battle started - the "/total" half of the label.</summary>
    public int SquadSize { get; private set; }

    /// <summary>Gems charged to bring the whole squad back.</summary>
    public int GemCost { get; private set; }

    private Action<HeroStatCell> onBuyPressed;

    private void Awake()
    {
        if (!buyButton && buyRoot) buyButton = buyRoot.GetComponent<Button>();
        if (!buyRoot && buyButton) buyRoot = buyButton.gameObject;
    }

    /// <summary>
    /// Wires this cell to one hero type. Called once, right after cloning.
    /// unitId is passed separately from def on purpose: a hero can be on the
    /// field with no matching entry in UnitsDatabaseSO, and the cell must still
    /// track the right bucket in HeroRoster even when it has no portrait.
    /// </summary>
    public void Bind(int unitId, UnitDefinitionSO def, int squadSize, int gemCost, Action<HeroStatCell> onBuy)
    {
        UnitId = unitId;
        SquadSize = Mathf.Max(0, squadSize);
        GemCost = Mathf.Max(0, gemCost);
        onBuyPressed = onBuy;

        if (avatar && def && def.portrait) avatar.sprite = def.portrait;
        if (gemCostText) gemCostText.text = GemCost.ToString();

        if (buyButton)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(HandleBuyPressed);
        }

        SetAlive(SquadSize);
    }

    /// <summary>Updates the numbers and flips between the two looks.</summary>
    public void SetAlive(int alive)
    {
        // A purchase can briefly put more heroes on the field than the snapshot
        // recorded; the label must never read "4/3".
        alive = Mathf.Clamp(alive, 0, SquadSize);
        bool wiped = alive <= 0;

        if (countText)
        {
            // enabled rather than SetActive: the buy button may be parented under
            // this label, and switching the GameObject off would hide it too.
            countText.enabled = !wiped;
            countText.text = $"{alive}/{SquadSize}";
        }

        if (deadOverlay) deadOverlay.SetActive(wiped);
        else if (avatar) avatar.color = wiped ? deadTint : aliveTint;

        if (buyRoot) buyRoot.SetActive(wiped);
    }

    /// <summary>Greys out the price while the player cannot afford it. Purely cosmetic.</summary>
    public void SetAffordable(bool affordable)
    {
        if (buyButton) buyButton.interactable = affordable;
    }

    private void HandleBuyPressed()
    {
        onBuyPressed?.Invoke(this);
    }
}
