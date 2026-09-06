using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ONE hero type in the battle HUD's "Heroes Stats panel" - the "Hero Card" object.
///
/// It has three looks, and NONE of them tints anything. The artwork carries the
/// state:
///   ALIVE : "Cell Active"   frame on (coloured), "card  Button" showing "alive/total",
///                           "Cost  Gem" off.
///   WIPED : "Cell DeActive" frame on (that sprite is ALREADY grey), the count off,
///                           "Cost  Gem" on with its gem price.
///   SPENT : the buy-back has been used, so the price never comes back - a wiped
///           SPENT card is the grey frame with the count still on it, reading "0/3".
///
/// The avatar sprite is written into BOTH frames at Bind time and its colour is
/// never touched - that is the whole point of the two-frame model, and it replaces
/// the old single-avatar + grey-overlay / deadTint approach of the "Hero 1" card.
///
/// It owns no game logic. HeroStatsPanel clones it once per hero type present in
/// the battle, feeds it numbers, and handles the purchase itself.
///
/// Authored hierarchy this expects (any field left empty is looked up by name
/// inside the card, so the names below are a contract):
///   Hero Card
///   +- BG
///   +- Cell DeActive      starts OFF   -> Mask/Avatar
///   +- Cell Active        starts ON    -> Mask/Avatar
///   +- card  Button       starts ON    -> Text (TMP), the "2/2" count
///   +- Cost  Gem          starts OFF   -> Text (TMP) + Gem, and the Button that buys
/// </summary>
public class HeroStatCell : MonoBehaviour
{
    [Header("Avatar frames")]
    [Tooltip("The 'Cell Active' object - the coloured frame, shown while at least one " +
             "hero of this type is alive. Left empty = found by name.")]
    [SerializeField] private GameObject activeRoot;

    [Tooltip("The 'Cell DeActive' object - the frame whose sprite is ALREADY grey, shown " +
             "once every hero of this type is dead. Left empty = found by name.")]
    [SerializeField] private GameObject deactiveRoot;

    [Tooltip("The Image under 'Cell Active' (Mask/Avatar). Its sprite comes from " +
             "UnitDefinitionSO.portrait. Left empty = found by name inside activeRoot.")]
    [SerializeField] private Image activeAvatar;

    [Tooltip("The Image under 'Cell DeActive' (Mask/Avatar). Gets the SAME portrait - " +
             "its colour is never changed. Left empty = found by name inside deactiveRoot.")]
    [SerializeField] private Image deactiveAvatar;

    [Header("Count")]
    [Tooltip("The 'card  Button' object holding the count label. Switched OFF once the " +
             "type is wiped out, which is when 'Cost  Gem' takes its place.")]
    [SerializeField] private GameObject countRoot;

    [Tooltip("The count label under 'card  Button' - rendered as '2/3'. " +
             "Left empty = the first TMP_Text inside countRoot.")]
    [SerializeField] private TMP_Text countText;

    [Header("Buy Back")]
    [Tooltip("The 'Cost  Gem' object holding the price label + gem icon. MUST start " +
             "switched OFF in the scene. Left empty = found by name.")]
    [SerializeField] private GameObject costRoot;

    [Tooltip("The Button that actually buys the squad back - the one on 'Cost  Gem', " +
             "NOT the disabled Button sitting on 'card  Button'. Left empty = taken from costRoot.")]
    [SerializeField] private Button buyButton;

    [Tooltip("The price label under 'Cost  Gem'. Left empty = the first TMP_Text inside costRoot.")]
    [SerializeField] private TMP_Text gemCostText;

    /// <summary>Which unit type this cell stands for. Matches UnitDefinitionSO.unitId.</summary>
    public int UnitId { get; private set; } = -1;

    /// <summary>Squad size when the battle started - the "/total" half of the label.</summary>
    public int SquadSize { get; private set; }

    /// <summary>Gems charged to bring the whole squad back.</summary>
    public int GemCost { get; private set; }

    /// <summary>
    /// True once this card's ONE buy-back for the level has been used.
    ///
    /// The buy-back is deliberately not repeatable: a squad can be bought back
    /// once per stage, and after that the card is a read-out only. Without this
    /// the card re-armed every time the type was wiped out again, so a player
    /// with gems could keep the same squad on the field indefinitely.
    ///
    /// Also the gate LastStandOffer waits on - it only appears once EVERY card
    /// here is spent.
    /// </summary>
    public bool IsSpent { get; private set; }

    private Action<HeroStatCell> onBuyPressed;

    private void Awake()
    {
        AutoWire();
    }

    /// <summary>
    /// Fills in whatever the Inspector left empty, by name, from the authored card.
    /// An explicit assignment wins - but ONLY if it points inside this card; see below.
    ///
    /// Name matching ignores case AND whitespace on purpose: the authored objects are
    /// "card  Button" and "Cost  Gem" with a DOUBLE space, which is invisible in the
    /// Hierarchy and is exactly the kind of thing that makes a hand-typed lookup fail.
    /// </summary>
    private void AutoWire()
    {
        // FIRST drop anything pointing at a DIFFERENT card. "Copy Component" +
        // "Paste Component Values" from another card carries its references across,
        // and a foreign reference is far worse than an empty one: an empty field gets
        // resolved correctly below, whereas a foreign countText means every clone
        // writes its count into the SAME label and they all fight over one object.
        DropForeign(ref activeRoot, nameof(activeRoot));
        DropForeign(ref deactiveRoot, nameof(deactiveRoot));
        DropForeign(ref activeAvatar, nameof(activeAvatar));
        DropForeign(ref deactiveAvatar, nameof(deactiveAvatar));
        DropForeign(ref countRoot, nameof(countRoot));
        DropForeign(ref countText, nameof(countText));
        DropForeign(ref costRoot, nameof(costRoot));
        DropForeign(ref buyButton, nameof(buyButton));
        DropForeign(ref gemCostText, nameof(gemCostText));

        if (!activeRoot) activeRoot = FindChild(transform, "Cell Active");
        if (!deactiveRoot) deactiveRoot = FindChild(transform, "Cell DeActive");
        if (!countRoot) countRoot = FindChild(transform, "card Button");
        if (!costRoot) costRoot = FindChild(transform, "Cost Gem");

        // Scoped to each frame: there are TWO objects called "Avatar" under a card,
        // one per frame, so an unscoped search would bind both fields to the same one.
        if (!activeAvatar && activeRoot)
        {
            var go = FindChild(activeRoot.transform, "Avatar");
            if (go) activeAvatar = go.GetComponent<Image>();
        }

        if (!deactiveAvatar && deactiveRoot)
        {
            var go = FindChild(deactiveRoot.transform, "Avatar");
            if (go) deactiveAvatar = go.GetComponent<Image>();
        }

        // One TMP_Text under each root, so a typed search beats a name lookup here.
        if (!countText && countRoot)
            countText = countRoot.GetComponentInChildren<TMP_Text>(true);

        if (!gemCostText && costRoot)
            gemCostText = costRoot.GetComponentInChildren<TMP_Text>(true);

        if (!buyButton && costRoot) buyButton = costRoot.GetComponent<Button>();
        if (!costRoot && buyButton) costRoot = buyButton.gameObject;

        if (!activeRoot || !deactiveRoot)
            Debug.LogWarning($"[HeroStatCell] '{name}' is missing a frame " +
                             $"(active={(activeRoot ? "ok" : "NULL")}, " +
                             $"deactive={(deactiveRoot ? "ok" : "NULL")}). The card cannot " +
                             "switch between its alive and wiped looks.", this);
    }

    /// <summary>
    /// Clears a field whose target is not this card or one of its descendants, so the
    /// name lookup below can resolve it against the RIGHT card. Warns loudly, because a
    /// cross-card reference is silent at authoring time and only shows up as two cards
    /// sharing one label at runtime.
    /// </summary>
    private void DropForeign(ref GameObject field, string fieldName)
    {
        if (!field) { field = null; return; }
        if (field.transform.IsChildOf(transform)) return;

        Debug.LogWarning($"[HeroStatCell] '{name}'.{fieldName} pointed at '{field.name}', " +
                         "which belongs to a DIFFERENT card - cleared and re-resolved. " +
                         "Re-assign it in the Inspector to silence this.", this);
        field = null;
    }

    /// <inheritdoc cref="DropForeign(ref GameObject, string)"/>
    private void DropForeign<T>(ref T field, string fieldName) where T : Component
    {
        if (!field) { field = null; return; }
        if (field.transform.IsChildOf(transform)) return;

        Debug.LogWarning($"[HeroStatCell] '{name}'.{fieldName} pointed at '{field.name}', " +
                         "which belongs to a DIFFERENT card - cleared and re-resolved. " +
                         "Re-assign it in the Inspector to silence this.", this);
        field = null;
    }

    /// <summary>
    /// Recursive child lookup that compares names with case and whitespace stripped.
    /// Returns the first match in depth-first order, inactive children included.
    /// </summary>
    private static GameObject FindChild(Transform root, string wanted)
    {
        string target = Normalize(wanted);

        foreach (Transform child in root)
        {
            if (Normalize(child.name) == target) return child.gameObject;

            var deeper = FindChild(child, wanted);
            if (deeper) return deeper;
        }

        return null;
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;

        var buffer = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            if (!char.IsWhiteSpace(c)) buffer.Append(char.ToLowerInvariant(c));

        return buffer.ToString();
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
        IsSpent = false;

        // The SAME sprite goes into both frames. The greyed-out look comes from the
        // "Cell DeActive" frame art, never from tinting the portrait.
        if (def && def.portrait)
        {
            if (activeAvatar) activeAvatar.sprite = def.portrait;
            if (deactiveAvatar) deactiveAvatar.sprite = def.portrait;
        }

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

        ShowFrame(wiped);

        // The price only takes the count's place while the buy-back is still
        // available. A SPENT card that gets wiped out a second time keeps the grey
        // frame but goes back to showing "0/3" - the squad is gone and there is
        // nothing left to sell, which reads very differently from a price the
        // player merely cannot afford.
        bool canBuy = wiped && !IsSpent;

        if (countText) countText.text = $"{alive}/{SquadSize}";
        ShowCount(!canBuy);

        if (costRoot) costRoot.SetActive(canBuy);
    }

    /// <summary>
    /// Burns this card's single buy-back for the rest of the level. Called by
    /// HeroStatsPanel the moment a purchase actually goes through - after the gems
    /// are charged and the reinforcements are queued, never before.
    ///
    /// The button is switched off here as well as hidden, so the frame between this
    /// call and the next SetAlive cannot register a second click.
    /// </summary>
    public void MarkSpent()
    {
        if (IsSpent) return;

        IsSpent = true;

        if (buyButton) buyButton.interactable = false;
        if (costRoot) costRoot.SetActive(false);
    }

    /// <summary>
    /// The THIRD look, used by the last-stand offer: the COLOURED frame, no count,
    /// buy button showing.
    ///
    /// Deliberately not SetAlive(0). That is the "this type is wiped out" state and
    /// it swaps in the grey frame, which is the wrong sell for a prompt asking the
    /// player to buy a fresh hero - the offer is an invitation, not an obituary.
    /// Everything else (the button, its listener, the price label) is exactly what
    /// Bind already wired.
    /// </summary>
    public void ShowAsOffer()
    {
        ShowFrame(false);
        ShowCount(false);

        if (costRoot) costRoot.SetActive(true);
    }

    /// <summary>
    /// Greys out the price while the player cannot afford it. Purely cosmetic -
    /// except that a SPENT card can never be re-enabled by it, however the gem
    /// balance moves afterwards.
    /// </summary>
    public void SetAffordable(bool affordable)
    {
        if (buyButton) buyButton.interactable = affordable && !IsSpent;
    }

    private void ShowFrame(bool wiped)
    {
        if (activeRoot) activeRoot.SetActive(!wiped);
        if (deactiveRoot) deactiveRoot.SetActive(wiped);
    }

    /// <summary>
    /// Hides the count by switching "card  Button" off, which is what the new card
    /// wants: "Cost  Gem" is its SIBLING and sits in the same slot, so the two swap
    /// cleanly. The countText fallback covers a card authored without a dedicated
    /// count root - the old "Hero 1" layout, where the buy button was a CHILD of the
    /// count label and switching that label off would have hidden the button too.
    /// </summary>
    private void ShowCount(bool show)
    {
        if (countRoot) countRoot.SetActive(show);
        else if (countText) countText.enabled = show;
    }

    private void HandleBuyPressed()
    {
        onBuyPressed?.Invoke(this);
    }
}
