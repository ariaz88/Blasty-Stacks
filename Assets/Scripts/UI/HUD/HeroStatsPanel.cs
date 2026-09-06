using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The battle HUD's "Heroes Stats panel": one cell per hero TYPE that is on the
/// field when BATTLE is pressed, each showing "alive/total" and, once that type
/// is wiped out, a gem price to buy the whole squad back.
///
/// A card's buy-back is good for ONE purchase per level. Once used it never comes
/// back, even if that type is wiped out again - see HeroStatCell.IsSpent. When
/// every card has been spent, <see cref="BuyBacksExhausted"/> opens the gate that
/// lets LastStandOffer appear.
///
/// The cells are BUILT AT RUNTIME. The scene holds a single authored cell
/// ("Hero Card") which is used as a template and switched off - a stage that fields
/// two hero types gets two cells, one that fields four gets four. Nothing is
/// hard-coded to a hero.
///
/// Layout is a HorizontalLayoutGroup on the container with MiddleCenter
/// alignment, so a short row stays centred instead of hugging the left edge.
/// Awake enforces that if the component is present.
///
/// Where the numbers come from: <see cref="HeroRoster"/>, which PlayerManager
/// registers into on spawn and out of on death.
/// </summary>
public class HeroStatsPanel : MonoBehaviour
{
    [Header("Cells")]
    [Tooltip("The object the cells are parented to. Put the HorizontalLayoutGroup here. " +
             "Left empty = this GameObject.")]
    [SerializeField] private RectTransform cellContainer;

    [Tooltip("The authored 'Hero Card' cell. Used as a template only - it is switched " +
             "off at Awake and cloned once per hero type in the battle.")]
    [SerializeField] private HeroStatCell cellTemplate;

    [Tooltip("Force the container's layout group to centre its children. Turn OFF " +
             "if you want to control the alignment by hand in the Inspector.")]
    [SerializeField] private bool forceCenterAlignment = true;

    [Header("Buy Back Cost")]
    [Tooltip("Fallback price: this many gems for EACH hero in the squad. Used only " +
             "when the unit's own UnitDefinitionSO.respawnGemCost is left at 0.")]
    [SerializeField, Min(0)] private int gemsPerHero = 50;

    [Header("Refs (left empty = found in the scene)")]
    [SerializeField] private PlayerWaveManager waveManager;
    [SerializeField] private GameStartManager gameStartManager;

    private readonly List<HeroStatCell> cells = new();
    private UnitsDatabaseSO unitsDatabase;
    private bool built;

    /// <summary>
    /// Raised the moment a card's one-per-level buy-back is used up. LastStandOffer
    /// listens so it can re-test its gate immediately, rather than waiting for the
    /// next roster change to happen to poke it.
    /// </summary>
    public event Action OnBuyBackSpent;

    /// <summary>
    /// True once EVERY card in this panel has had its buy-back used. This is the
    /// gate LastStandOffer waits on: the last-stand hero is the final resort, so it
    /// must not be offered while the player still holds an unused squad buy-back.
    ///
    /// Requires <see cref="built"/>, so a panel that has not laid its cells out yet
    /// reads as "not exhausted" instead of vacuously true. A battle that opened with
    /// no heroes at all builds zero cells and does report true - there is genuinely
    /// nothing left to spend.
    /// </summary>
    public bool BuyBacksExhausted
    {
        get
        {
            if (!built) return false;

            foreach (var cell in cells)
                if (cell && !cell.IsSpent) return false;

            return true;
        }
    }

    private void Awake()
    {
        if (!cellContainer) cellContainer = transform as RectTransform;

        if (!cellTemplate && cellContainer)
            cellTemplate = cellContainer.GetComponentInChildren<HeroStatCell>(true);

        if (!cellTemplate)
        {
            Debug.LogError("[HeroStatsPanel] No cell template - assign the authored " +
                           "'Hero Card' object (it needs a HeroStatCell component).", this);
            enabled = false;
            return;
        }

        cellTemplate.gameObject.SetActive(false);

        if (forceCenterAlignment && cellContainer)
        {
            var group = cellContainer.GetComponent<HorizontalOrVerticalLayoutGroup>();
            if (group) group.childAlignment = TextAnchor.MiddleCenter;
        }

        if (!waveManager) waveManager = FindObjectOfType<PlayerWaveManager>(true);

        // Written out rather than with ??: GameStartManager.Instance can be a
        // destroyed-but-not-null Unity object, which ?? happily hands back.
        if (!gameStartManager) gameStartManager = GameStartManager.Instance;
        if (!gameStartManager) gameStartManager = FindObjectOfType<GameStartManager>(true);

        unitsDatabase = gameStartManager ? gameStartManager.unitsDatabase : null;
    }

    private void OnEnable()
    {
        BattleStartController.OnAnyBattleStarted += HandleBattleStarted;
        HeroRoster.OnRosterChanged += Refresh;

        // Subscribed here rather than in HandleBattleStarted so a panel that is
        // switched off and on again mid-battle (built is already true) still
        // greys the price out when the balance changes.
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;

        // The panel may be switched on BY the battle transition, i.e. after the
        // event above has already fired. Build straight away in that case -
        // but at the END of the frame, so every hero that is starting up this
        // same frame has run its Start() and registered first.
        if (!built && BattleStartController.BattleIsRunning)
            StartCoroutine(BuildAtEndOfFrame());
        else
            Refresh();
    }

    private IEnumerator BuildAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        HandleBattleStarted();
    }

    private void OnDisable()
    {
        BattleStartController.OnAnyBattleStarted -= HandleBattleStarted;
        HeroRoster.OnRosterChanged -= Refresh;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    private void HandleBattleStarted()
    {
        if (built) return;
        built = true;

        // Freeze the "/total" for every type standing on the field right now.
        HeroRoster.SnapshotStartingCounts();
        BuildCells();
        Refresh();
    }

    private void BuildCells()
    {
        foreach (var cell in cells)
            if (cell) Destroy(cell.gameObject);

        cells.Clear();

        var ids = HeroRoster.UnitIdsInBattle();
        if (ids.Count == 0)
        {
            Debug.LogWarning("[HeroStatsPanel] Battle started with no heroes on the " +
                             "field - the panel stays empty.", this);
            return;
        }

        // Database order, so the row reads the same way every run instead of
        // following whichever type happened to spawn first.
        ids.Sort(CompareByDatabaseOrder);

        foreach (int unitId in ids)
        {
            var def = unitsDatabase ? unitsDatabase.GetById(unitId) : null;
            if (!def)
                Debug.LogWarning($"[HeroStatsPanel] unitId={unitId} is on the field but " +
                                 "not in UnitsDatabaseSO - the cell gets no portrait and " +
                                 "cannot be bought back.", this);

            int squadSize = HeroRoster.StartingCount(unitId);

            var cell = Instantiate(cellTemplate, cellContainer);
            cell.gameObject.SetActive(true);
            cell.name = def ? $"Hero_{def.displayName}" : $"Hero_{unitId}";
            cell.Bind(unitId, def, squadSize, ResolveGemCost(def, squadSize), HandleBuyBack);

            cells.Add(cell);
        }
    }

    private int CompareByDatabaseOrder(int a, int b)
    {
        if (unitsDatabase == null) return a.CompareTo(b);

        int ia = unitsDatabase.IndexOf(a);
        int ib = unitsDatabase.IndexOf(b);

        if (ia < 0 || ib < 0) return a.CompareTo(b);
        return ia.CompareTo(ib);
    }

    private int ResolveGemCost(UnitDefinitionSO def, int squadSize)
    {
        if (def != null && def.respawnGemCost > 0)
            return def.respawnGemCost;

        return gemsPerHero * Mathf.Max(1, squadSize);
    }

    private void Refresh()
    {
        int gems = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gems : int.MaxValue;

        foreach (var cell in cells)
        {
            if (!cell) continue;
            cell.SetAlive(HeroRoster.AliveCount(cell.UnitId));
            cell.SetAffordable(gems >= cell.GemCost);
        }
    }

    private void HandleCurrencyChanged(string currency, int newValue, int delta)
    {
        if (currency == "Gems") Refresh();
    }

    private void HandleBuyBack(HeroStatCell cell)
    {
        if (!cell) return;

        // ONE buy-back per card per level. The button is hidden and disabled the
        // instant a purchase lands, so this is belt-and-braces against a second
        // click queued in the same frame - but it is also the authoritative rule,
        // and it must sit ahead of the gem charge.
        if (cell.IsSpent) return;

        // Nothing to buy back if the type is not actually wiped out.
        if (HeroRoster.AliveCount(cell.UnitId) > 0) return;

        if (!waveManager)
        {
            Debug.LogError("[HeroStatsPanel] No PlayerWaveManager - cannot spawn heroes.", this);
            return;
        }

        // Checked BEFORE the charge, so a missing prefab can never eat the gems.
        if (!waveManager.CanSpawnReinforcements(cell.UnitId))
        {
            Debug.LogError($"[HeroStatsPanel] unitId={cell.UnitId} cannot be spawned - " +
                           "no runtimePrefab on its UnitDefinitionSO.", this);
            return;
        }

        var currency = CurrencyManager.Instance;
        if (currency == null)
        {
            Debug.LogWarning("[HeroStatsPanel] No CurrencyManager - buying back for free.", this);
        }
        else if (!currency.TrySpendGems(cell.GemCost))
        {
            Debug.Log($"[HeroStatsPanel] Not enough gems: need {cell.GemCost}, have {currency.Gems}.");
            Refresh();
            return;
        }

        waveManager.SpawnReinforcements(cell.UnitId, cell.SquadSize);

        // Burned BEFORE the Refresh below, so SetAlive sees the spent flag and puts
        // the count back where the price was instead of re-offering it.
        cell.MarkSpent();

        // The heroes register themselves in Start (next frame), which fires
        // OnRosterChanged - Refresh here only reacts to the gem spend.
        Refresh();

        // Announced last, with the panel already in its final state, so a listener
        // reading BuyBacksExhausted from inside the handler gets the truth.
        try { OnBuyBackSpent?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }
}
