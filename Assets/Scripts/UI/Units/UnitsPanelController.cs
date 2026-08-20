using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class UnitsPanelController : MonoBehaviour
{
    #region FIELD VARIABLES
    [Header("Cards Screen Section Roots (Header + Container)")]
    [SerializeField] private RectTransform deployedGroupRoot;
    [SerializeField] private RectTransform undeployedGroupRoot;
    [SerializeField] private RectTransform unachievedGroupRoot;


    // ----- Bucket header refs -----
    [Header("Bucket Headers")]
    [SerializeField] private BucketHeader deployedHeader;
    [SerializeField] private BucketHeader undeployedHeader;
    [SerializeField] private BucketHeader unachievedHeader;

    // ----- Stats Panel (overlay) -----
    [Header("Stats Panel")]
    [SerializeField] private BucketStatsPanel statsPanel;   // assign the shared overlay prefab in scene
    [SerializeField] private BucketStatRow rowPrefab;       // list item prefab


    // -------- Design-time data ----------
    [Header("Design-Time Data")]
    [SerializeField] private UnitsDatabaseSO unitsDatabase;          // list of UnitDefinitionSO
    [SerializeField] private UpgradeCostSO upgradeCost;               // for cost L->L+1
    [SerializeField] private ProgressionConfigSO progressionConfig;   // for +Δ preview
    [SerializeField] private CPWeightsConfigSO cpWeights;             // for CP readout

    // -------- Panels / screens ----------
    [Header("Screens")]
    [SerializeField] private GameObject cardsScreen;                  // the list view root
    [SerializeField] private GameObject detailScreen;                 // the detail root

    [Header("Cards Screen Buckets")]
    [SerializeField] private Transform deployedContainer;
    [SerializeField] private Transform undeployedContainer;
    [SerializeField] private Transform unachievedContainer;

    // -------- Detail contents ----------
    [Header("Detail View")]
    [SerializeField] private GameObject UnDeployedSelection;
    [SerializeField] private UnitDetailView undeployedDetailView; // NEW  ← assign: UnitDetailPanel_UnDeploy
    [SerializeField] private UnitDetailView detailView;               // UnitDetailPanel (stats UI)
    [SerializeField] private UnitDetailView unAchivedDetailView;   // <- NEW


    [Header("Detail Buttons")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button upgradeButton;                    // deployed only
    [SerializeField] private Button upgradeDisabledButton;
    [SerializeField] private Button deployButton;                     // undeployed only
    [SerializeField] private Button undeployButton;                   // deployed only



    [Header("Bucket Slot Holders (positions, not content)")]
    [SerializeField] private RectTransform undeployedParentHolder;  // where Undeployed group normally sits
    [SerializeField] private RectTransform unachievedParentHolder;  // where Unachieved group normally sits


    // Remember preferred positions per bucket (sibling index)
    private readonly Dictionary<int, int> _deployedSlotByUnit = new();
    private readonly Dictionary<int, int> _undeployedSlotByUnit = new();


    private  List<int> _deployedOrder = new();   // unitIds in deployed row order
    private  List<int> _undeployedOrder = new();   // unitIds in undeployed row order


    // -------- Prefabs ----------
    [Header("Prefabs")]
    [SerializeField] private UnitCardView cardPrefab;

    // -------- internals ----------
    private enum Bucket { Deployed, Undeployed, Unachieved }

    private readonly List<UnitCardView> _spawnedCards = new();
    private readonly Dictionary<int, UnitCardView> _cardsByUnitId = new();

    // references to systems
    private GameStartManager _gsm;                    // owns PlayerUnitsModel + PlayerProgressionService
    private PlayerUnitsModel PlayerUnits => _gsm != null ? _gsm.PlayerUnits : null;

    private CurrencyManager _currency => CurrencyManager.Instance;

    // selection
    private int _selectedUnitId = -1;

    [SerializeField] private DeployOverlayController deployOverlay; // assign in Inspector

    #endregion

    [Header(" UnAchived Panel Offset")]
    [SerializeField] private float unachievedOffsetY = 606f;   // how much to move up when empty

    private Vector2 _unachievedBasePos;
    private bool _unachievedBasePosCached = false;

    public UnitsDatabaseSO GetUnitsDatabase() => unitsDatabase;

    private void Awake()
    {
        _gsm = FindObjectOfType<GameStartManager>(includeInactive: true);
        if (!_gsm)
            Debug.LogWarning("[UnitsPanelController] GameStartManager not found in scene.");



        deployedHeader.Wire(OpenBucketPanel);
        undeployedHeader.Wire(OpenBucketPanel);
        unachievedHeader.Wire(OpenBucketPanel);

        EnsureOrderListsSync();

    }

    private void OnEnable()
    {
        BuildCardsIntoBuckets();
        ShowCardsScreen();

        if (_currency != null)
            _currency.OnCurrencyChanged += HandleCurrencyChanged; // (string, int, int)

        if (_gsm != null && _gsm.ProgressionService != null)
            _gsm.ProgressionService.OnUnitUpgraded += HandleUnitUpgraded;

    }

    private void OnDisable()
    {
        if (_currency != null)
            _currency.OnCurrencyChanged -= HandleCurrencyChanged;

        if (_gsm != null && _gsm.ProgressionService != null)
            _gsm.ProgressionService.OnUnitUpgraded -= HandleUnitUpgraded;

    }

    private void AdjustUnachievedPanelPosition()
    {
        // Move the visible Unachieved group (header + list)
        if (!unachievedGroupRoot) return;

        // Cache original anchored position only once
        if (!_unachievedBasePosCached)
        {
            _unachievedBasePos = unachievedGroupRoot.anchoredPosition;
            _unachievedBasePosCached = true;
        }

        // Use actual card counts in the containers
        int nDep = deployedContainer ? deployedContainer.childCount : 0;
        int nUnd = undeployedContainer ? undeployedContainer.childCount : 0;

        Vector2 pos = _unachievedBasePos;

        // NEW LOGIC:
        // If we do NOT have any Undeployed cards -> move Unachieved panel up by +606 in Y.
        // If there is at least 1 Undeployed card -> keep base position.
        if (nUnd == 0)
            pos.y = _unachievedBasePos.y + unachievedOffsetY;
        else
            pos.y = _unachievedBasePos.y;

        unachievedGroupRoot.anchoredPosition = pos;
    }



    // Hide empty sections and reflow visible ones (no gaps)


    // ----------------------- Build / buckets -----------------------

    private void SwapPositions(RectTransform a, RectTransform b)
    {
        if (!a || !b) return;
        var pa = a.anchoredPosition;
        a.anchoredPosition = b.anchoredPosition;
        b.anchoredPosition = pa;

        // Optional: also swap sibling index to keep hierarchy tidy
        int ia = a.GetSiblingIndex();
        a.SetSiblingIndex(b.GetSiblingIndex());
        b.SetSiblingIndex(ia);
    }

    private void UpdateBucketSectionsLayout()
    {
        int nDep = deployedContainer ? deployedContainer.childCount : 0;
        int nUnd = undeployedContainer ? undeployedContainer.childCount : 0;
        int nUnach = unachievedContainer ? unachievedContainer.childCount : 0;

        deployedGroupRoot.gameObject.SetActive(nDep > 0);
        undeployedGroupRoot.gameObject.SetActive(nUnd > 0);
        unachievedGroupRoot.gameObject.SetActive(nUnach > 0);

        // If no Undeployed but Unachieved exists, visually move Unachieved into Undeployed’s slot
        if (nDep > 0 && nUnd == 0 && nUnach > 0)
        {
            SwapPositions(unachievedGroupRoot, undeployedGroupRoot);
        }
    }


    private void BuildCardsIntoBuckets()
    {
        ClearContainer(deployedContainer);
        ClearContainer(undeployedContainer);
        ClearContainer(unachievedContainer);

        foreach (var c in _spawnedCards) if (c) Destroy(c.gameObject);
        _spawnedCards.Clear();
        _cardsByUnitId.Clear();

        if (!unitsDatabase) return;

        foreach (var def in unitsDatabase.Units)
        {
            if (!def) continue;
            int id = def.unitId;

            Bucket b = GetBucketForUnit(def);
            Transform parent =
                b == Bucket.Deployed ? deployedContainer :
                b == Bucket.Undeployed ? undeployedContainer :
                                         unachievedContainer;

            var card = Instantiate(cardPrefab, parent);
            bool unlocked = (b != Bucket.Unachieved);
            int level = GetLevelSafe(id);
            card.Bind(def, unlocked, level, () => OnCardClicked(id));

            card.SetLockAwareHeader(def, unlocked, level);//ADDED

            card.OnUnlockStateChanged += HandleCardUnlockStateChanged;



            // after you instantiate or move a card:
            //ApplyUpgradeCue(card, deployedContainer);


            // Show requirement label only for unachieved
            card.SetRequirementText(def.GetRequirementText(), b == Bucket.Unachieved);

            _spawnedCards.Add(card);
            _cardsByUnitId[id] = card;
        }
        ApplySavedOrder(deployedContainer as RectTransform, _deployedSlotByUnit);
        ApplySavedOrder(undeployedContainer as RectTransform, _undeployedSlotByUnit);

        UpdateBucketSectionsLayout();

        AdjustUnachievedPanelPosition();

    }

    private static void ClearContainer(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    private Bucket GetBucketForUnit(UnitDefinitionSO def)
    {
        int id = def.unitId;
        var model = _gsm?.PlayerUnits;
        if (model == null) return Bucket.Unachieved;

        bool unlocked = model.IsUnlocked(id);
        if (!unlocked) return Bucket.Unachieved;

        return model.IsDeployed(id) ? Bucket.Deployed : Bucket.Undeployed;
    }

    // ----------------------- Click routing -----------------------

    private void OnCardClicked(int unitId)
    {
        _selectedUnitId = unitId;

        // selection highlight
        foreach (var c in _spawnedCards)
            if (c) c.SetSelected(c.UnitId == unitId);

        var def = unitsDatabase.GetById(unitId);
        if (!def) return;

        Bucket b = GetBucketForUnit(def);

        switch (b)
        {
            case Bucket.Deployed: ShowDetail_Deployed(unitId); break;
            case Bucket.Undeployed: ShowDetail_Undeployed(unitId); break;
            default: ShowDetail_Unachieved(unitId); break;
        }
    }

    // ----------------------- Detail builders -----------------------

    private void ShowCardsScreen()
    {
        if (cardsScreen) cardsScreen.SetActive(true);
        if (detailScreen) detailScreen.SetActive(false);

        // hard-clear any lingering visuals
        foreach (var c in _spawnedCards) if (c) { c.SetSelected(false); c.ClearOverlayMarks(); }

        RefreshDeployedUpgradeCues();

    }
    // UnitsPanelController.cs
    public void ShowCardsScreenPublic() => ShowCardsScreen();


    // Deployed: stats + Upgrade + Undeploy
    private void ShowDetail_Deployed(int unitId)
    {
        if (cardsScreen) cardsScreen.SetActive(false);
        if (detailScreen) detailScreen.SetActive(true);

        if (detailView) detailView.gameObject.SetActive(true);
        if (undeployedDetailView) undeployedDetailView.gameObject.SetActive(false);
        if (unAchivedDetailView) unAchivedDetailView.gameObject.SetActive(false);


        BuildUnlockedStats(unitId, detailView);


        WireBackButton();
        WireUpgradeButton(unitId, visible: true);
        WireDeployButtons(unitId, showDeploy: false, showUndeploy: true);
        RefreshDeployedUpgradeCues();

    }

    // Undeployed: stats + Deploy
    private void ShowDetail_Undeployed(int unitId)
    {
        if (cardsScreen) cardsScreen.SetActive(false);
        if (detailScreen) detailScreen.SetActive(true);

        if (detailView) detailView.gameObject.SetActive(false);
        if (undeployedDetailView) undeployedDetailView.gameObject.SetActive(true);
        if (unAchivedDetailView) unAchivedDetailView.gameObject.SetActive(false);


        BuildUnlockedStats(unitId, undeployedDetailView);


        WireBackButton();
        WireUpgradeButton(unitId, visible: false);
        WireDeployButtons(unitId, showDeploy: true, showUndeploy: false);
    }
    // Opens the new “Undeployed” detail panel (no extra logic yet)


    // Unachieved: locked message only
    private void ShowDetail_Unachieved2(int unitId)
    {




    }

    // Unachieved: use a normal UnitDetailView layout
    private void ShowDetail_Unachieved(int unitId)
    {
        if (cardsScreen) cardsScreen.SetActive(false);
        if (detailScreen) detailScreen.SetActive(true);

        // only show the UnAchieved detail panel
        if (detailView) detailView.gameObject.SetActive(false);
        if (undeployedDetailView) undeployedDetailView.gameObject.SetActive(false);
        if (unAchivedDetailView) unAchivedDetailView.gameObject.SetActive(true);

  

        BuildLockedStats(unitId);

        WireBackButton();
        HideButton(upgradeButton);
        HideButton(deployButton);
        HideButton(undeployButton);
    }


    // ----------------------- Button wiring -----------------------

    public void WireBackButton()
    {
        if (!backButton) return;
        backButton.gameObject.SetActive(true);
        backButton.interactable = true;
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(() =>
        {
            ShowCardsScreen();
            foreach (var c in _spawnedCards) if (c) c.SetSelected(false);
        });

        if (UnDeployedSelection)

        {
            UnDeployedSelection.SetActive(false);

        }

        if (detailScreen) detailScreen.transform.SetAsLastSibling();
        RefreshDeployedUpgradeCues();

    }

    public void WireUpgradeButton(int unitId, bool visible)
    {
        // Guard: if neither button exists, bail
        if (!upgradeButton && !upgradeDisabledButton) return;

        // Compute affordability
        int level = GetLevelSafe(unitId);
        int cost = upgradeCost ? upgradeCost.GetCostForLevel(level) : 0;
        int coins = _currency ? _currency.Coins : 0;
        bool affordable = coins >= cost;

        // Toggle which button is visible
        if (upgradeButton)
        {
            upgradeButton.gameObject.SetActive(visible && affordable);
            upgradeButton.onClick.RemoveAllListeners();

            if (visible && affordable)
            {
                upgradeButton.onClick.AddListener(() =>
                {

                    if (_gsm?.ProgressionService == null) return;
                    bool ok = _gsm.ProgressionService.TryUpgrade(unitId);
                    if (!ok) return;
                    ShowDetail_Deployed(unitId); // refresh
                });
            }
        }

        if (upgradeDisabledButton)
        {
            upgradeDisabledButton.gameObject.SetActive(visible && !affordable);
            upgradeDisabledButton.onClick.RemoveAllListeners();

            // In UnitsPanelController.cs, inside WireUpgradeButton:
            if (visible && !affordable)
            {
                upgradeDisabledButton.onClick.AddListener(() =>
                {
                    if (!detailView) return;
                    RectTransform anchor = detailView.ToastAnchor
                                          ? detailView.ToastAnchor
                                          : (RectTransform)upgradeDisabledButton.transform;

                    // costNeeded vs coinsOwned
                    int coinsOwned = _currency ? _currency.Coins : 0;
                    detailView.PlayNotEnoughResourcesToastStacked(anchor, cost, coinsOwned);
                });
            }

        }
    }

    private void WireDeployButtons(int unitId, bool showDeploy, bool showUndeploy)
    {
        if (deployButton)
        {
            deployButton.gameObject.SetActive(showDeploy);
            deployButton.onClick.RemoveAllListeners();
            if (showDeploy)
                deployButton.onClick.AddListener(OnClickDeploy); // <-- use overlay opener
        }

       
    }


    private static void HideButton(Button b)
    {
        if (!b) return;
        b.onClick.RemoveAllListeners();
        b.gameObject.SetActive(false);
    }

    // ----------------------- Stats builder -----------------------


    // NEW: generic binder that writes into whichever UnitDetailView you pass in
    private void BuildUnlockedStats(int unitId, UnitDetailView targetView)
    {
        var def = unitsDatabase.GetById(unitId);
        if (!def || targetView == null) return;

        int level = GetLevelSafe(unitId);
        targetView.SetHeader(def, level);

        // CURRENT @ level
        var cur = new UnitStatsRuntime();
        cur.FromSO(def.baseStats);
        var gCur = ProgressionMath.GetGrowthMultipliers(level, progressionConfig);
        cur.attack *= gCur.gA;
        cur.maxHP *= gCur.gH;
        cur.moveSpeed *= gCur.gMv;
        cur.attackSpeed *= gCur.gAS;

        var nxt = new UnitStatsRuntime();
        nxt.FromSO(def.baseStats);
        var gNxt = ProgressionMath.GetGrowthMultipliers(level + 1, progressionConfig);
        nxt.attack *= gNxt.gA;
        nxt.maxHP *= gNxt.gH;
        nxt.moveSpeed *= gNxt.gMv;
        nxt.attackSpeed *= gNxt.gAS;

        float dHP = nxt.maxHP - cur.maxHP;
        float dATK = nxt.attack - cur.attack;
        float dDEF = nxt.defense - cur.defense;      // 0 if no growth
        float dAS = nxt.attackSpeed - cur.attackSpeed;
        float dMV = nxt.moveSpeed - cur.moveSpeed;
        float dRNG = nxt.attackRange - cur.attackRange;  // 0 if no growth

        targetView.SetStatRows(
            cur.maxHP, dHP,
            cur.attack, dATK,
            cur.defense, dDEF,
            cur.attackSpeed, dAS,
            cur.moveSpeed, dMV,
            cur.attackRange, dRNG
        );

        // meta
        int cp = CPCalculator.UnitCP(cur, level, cpWeights);
        int xp = 70;
        targetView.SetMeta(cp, 0, xp); // don't let SetMeta overwrite coins line

        // Show “needed / owned” for coins
        int coinsOwned = _currency ? _currency.Coins : 0;
        int costNeeded = upgradeCost ? upgradeCost.GetCostForLevel(level) : 0;
        targetView.SetCoinsCost(costNeeded, coinsOwned);


        // >>> ADD THESE LINES (feeds the popup buffer) <<<
        int cpNow = CPCalculator.UnitCP(cur, level, cpWeights);
        int cpNext = CPCalculator.UnitCP(nxt, level + 1, cpWeights);
        int cpDelta = cpNext - cpNow;
        targetView.RefreshStatsPopupData(def.displayName, cpNow, cpDelta, cur, nxt);
        // >>> END ADD <<<
    }
    // For Unachieved heroes: show base stats at Lv1 + requirement text
    private void BuildLockedStats(int unitId)
    {
        var def = unitsDatabase.GetById(unitId);
        if (!def || unAchivedDetailView == null) return;

        int level = 1; // always show as level 1 preview

        unAchivedDetailView.SetHeader(def, level);


        // CURRENT @ level 1
        var cur = new UnitStatsRuntime();
        cur.FromSO(def.baseStats);
        var gCur = ProgressionMath.GetGrowthMultipliers(level, progressionConfig);
        cur.attack *= gCur.gA;
        cur.maxHP *= gCur.gH;
        cur.moveSpeed *= gCur.gMv;
        cur.attackSpeed *= gCur.gAS;

        // No “next level” preview for locked units → deltas = 0
        float dHP = 0f;
        float dATK = 0f;
        float dDEF = 0f;
        float dAS = 0f;
        float dMV = 0f;
        float dRNG = 0f;

        unAchivedDetailView.SetStatRows(
            cur.maxHP, dHP,
            cur.attack, dATK,
            cur.defense, dDEF,
            cur.attackSpeed, dAS,
            cur.moveSpeed, dMV,
            cur.attackRange, dRNG
        );

        // Meta + coins (you can tune these if you want)
        int cp = CPCalculator.UnitCP(cur, level, cpWeights);
        unAchivedDetailView.SetMeta(cp, 0, 0);

        int coinsOwned = _currency ? _currency.Coins : 0;
        unAchivedDetailView.SetCoinsCost(0, coinsOwned);

        // Requirement text
        unAchivedDetailView.SetRequirementDetail(def.GetRequirementText());

        // Optional: feed popup data (if you use it)
        int cpNow = cp;
        int cpNext = cp; // same, since we don’t show growth
        int cpDelta = 0;
        unAchivedDetailView.RefreshStatsPopupData(def.displayName, cpNow, cpDelta, cur, cur);
    }


    private int GetLevelSafe(int unitId)
    {
        var model = _gsm?.PlayerUnits;
        return model != null ? Mathf.Max(1, model.GetLevel(unitId)) : 1;
    }

    // ----------------------- Move / migrate -----------------------

    private void MoveCardToBucket(int unitId, Bucket dest)
    {
        if (!_cardsByUnitId.TryGetValue(unitId, out var card) || !card) return;

        Transform parent =
            dest == Bucket.Deployed ? deployedContainer :
            dest == Bucket.Undeployed ? undeployedContainer :
                                        unachievedContainer;

        if (card.transform.parent != parent)
        {
            card.transform.SetParent(parent, worldPositionStays: false);
            card.transform.SetAsLastSibling(); // append to end
        }

        bool unlocked = dest != Bucket.Unachieved;
        int level = GetLevelSafe(unitId);
        card.RefreshBadge(unlocked, level);

        var def = unitsDatabase.GetById(unitId);
        if (def) card.SetRequirementText(def.GetRequirementText(), dest == Bucket.Unachieved);


        // after you instantiate or move a card:

        ApplyUpgradeCue(card, deployedContainer);
        UpdateBucketSectionsLayout();


    }

    // ----------------------- Button handlers -----------------------



    // ----------------------- External events -----------------------

    // FIXED signature to match CurrencyManager.OnCurrencyChanged
    private void HandleCurrencyChanged(string currency, int newValue, int delta)
    {
        // refresh upgrade interactability if we are on a deployed detail page
        if (!detailScreen || !detailScreen.activeInHierarchy) return;
        if (_selectedUnitId < 0) return;

        var def = unitsDatabase.GetById(_selectedUnitId);
        if (!def) return;

        Bucket b = GetBucketForUnit(def);
        if (b == Bucket.Deployed) WireUpgradeButton(_selectedUnitId, visible: true);

        RefreshDeployedUpgradeCues();

    }

    private void HandleUnitUpgraded(int unitId, int oldLevel, int newLevel, int spentGems)
    {
        // refresh the badge level on the card
        if (_cardsByUnitId.TryGetValue(unitId, out var card) && card)
            card.RefreshBadge(true, newLevel);

        // refresh detail if it's showing this unit
        if (detailScreen && detailScreen.activeInHierarchy && _selectedUnitId == unitId)
            ShowDetail_Deployed(unitId);
    }

    private void HandleLevelStageChanged(int levelIndex, int stageIndex, int globalStage)
    {
        Debug.Log($"[UnitsPanel] Stage changed → {levelIndex}-{stageIndex} (global {globalStage})");

        // When reaching requirement, auto-unlock → Undeployed
        ProcessRequirementUnlocks();
    }

    private void ProcessRequirementUnlocks1()
    {
        if (_gsm?.PlayerUnits == null) return;

        foreach (var def in unitsDatabase.Units)
        {
            if (!def) continue;
            int id = def.unitId;

            if (_gsm.PlayerUnits.IsUnlocked(id)) continue;

            bool reached = LevelManager.Instance &&
                           LevelManager.Instance.HasReached(def.requiredLevelIndex,
                                                            def.requiredStageIndexWithinLevel);
            if (!reached) continue;

            _gsm.PlayerUnits.Unlock(id);
            _gsm.PlayerUnits.SetDeployed(id, false);

            //*********************************************
            //SaveSystem.SetUnitDeployed(id, true);  // persist
            SaveSystem.SetUnitDeployed(id, false);

            //*********************************************


            if (_cardsByUnitId.TryGetValue(id, out var card) && card)
                MoveCardToBucket(id, Bucket.Undeployed);
            else
            {
                // spawn new under Undeployed
                var newCard = Instantiate(cardPrefab, undeployedContainer);
                int level = GetLevelSafe(id);
                newCard.Bind(def, true, level, () => OnCardClicked(id));
                newCard.SetRequirementText(def.GetRequirementText(), false);
                newCard.OnUnlockStateChanged += HandleCardUnlockStateChanged;

                _spawnedCards.Add(newCard);
                _cardsByUnitId[id] = newCard;
                newCard.transform.SetAsLastSibling();
            }
        }
        UpdateBucketSectionsLayout();

    }
    private void ProcessRequirementUnlocks()
    {
        if (_gsm?.PlayerUnits == null || LevelManager.Instance == null) return;

        bool anyUnlocked = false;

        foreach (var def in unitsDatabase.Units)
        {
            if (!def) continue;
            int id = def.unitId;

            if (_gsm.PlayerUnits.IsUnlocked(id)) continue;

            bool reached = LevelManager.Instance.HasReached(
                def.requiredLevelIndex,
                def.requiredStageIndexWithinLevel
            );

            if (!reached) continue;

            // Unlock → Undeployed
            _gsm.PlayerUnits.Unlock(id);
            _gsm.PlayerUnits.SetDeployed(id, false);

            SaveSystem.SetUnitUnlocked(id, true);
            SaveSystem.SetUnitDeployed(id, false);

            anyUnlocked = true;
        }

        if (anyUnlocked)
        {
            EnsureOrderListsSync();
            RebuildAllBuckets();
        }
    }


    private void HandleCardUnlockStateChanged(int unitId, bool unlocked)
    {
        if (unlocked) _gsm?.PlayerUnits?.Unlock(unitId);
        else _gsm?.PlayerUnits?.Lock(unitId); // replaced DebugForceLock with Lock

        BuildCardsIntoBuckets();

        if (detailScreen && detailScreen.activeInHierarchy && _selectedUnitId == unitId)
        {
            var def = unitsDatabase.GetById(unitId);
            var b = GetBucketForUnit(def);
            if (b == Bucket.Deployed) ShowDetail_Deployed(unitId);
            else if (b == Bucket.Undeployed) ShowDetail_Undeployed(unitId);
            else ShowDetail_Unachieved(unitId);
        }
    }


    // -----------------------  Upgrade Arrow -----------------------

    // ADD inside UnitsPanelController
    private bool IsUpgradeable(int unitId)
    {
        int level = _gsm.PlayerUnits.GetLevel(unitId);
        int cost = upgradeCost.GetCostForLevel(level);
        int coins = CurrencyManager.Instance ? CurrencyManager.Instance.Coins : 0;
        return coins >= cost;
    }
    // ADD inside UnitsPanelController
    private void RefreshDeployedUpgradeCues()
    {
        if (!deployedContainer) return;

        for (int i = 0; i < deployedContainer.childCount; i++)
        {
            var card = deployedContainer.GetChild(i).GetComponent<UnitCardView>();
            if (!card) continue;

            int unitId = card.UnitId; // assumes UnitCardView exposes UnitId (it already does in your version)
            bool upg = IsUpgradeable(unitId);
            card.SetUpgradeCue(upg);
        }
    }

    // Put near your other privates
    private bool IsDeployedBucket(Transform parent) => parent == deployedContainer;

    private void ApplyUpgradeCue(UnitCardView card, Transform parent)
    {
        if (!card) return;
        bool show = IsDeployedBucket(parent) && IsUpgradeable(card.UnitId);
        card.SetUpgradeCue(show);
    }

    // -----------------------  Bucket STats Panel  -----------------------

    private void OpenBucketPanel1(BucketHeader.BucketType type)
    {
        if (!statsPanel || !rowPrefab) return;

        // 1) Title
        string title = type switch
        {
            BucketHeader.BucketType.Deployed => deployedHeader.Title,
            BucketHeader.BucketType.Undeployed => undeployedHeader.Title,
            _ => unachievedHeader.Title
        };
        ClearContainer(statsPanel.Content);  // <-- ADD THIS: Force destroy-based clear before building rows
        statsPanel.Show(title);

        // 2) Source list (which units to show)
        Transform bucketParent = type switch
        {
            BucketHeader.BucketType.Deployed => deployedContainer,
            BucketHeader.BucketType.Undeployed => undeployedContainer,
            _ => unachievedContainer
        };

        // 3) Build rows
        for (int i = 0; i < bucketParent.childCount; i++)
        {
            var card = bucketParent.GetChild(i).GetComponent<UnitCardView>();
            if (!card) continue;

            int unitId = card.UnitId;

            // 1) Definition
            var def = unitsDatabase.GetById(unitId);
            if (!def) continue;

            // 2) Level
            int lvl = GetLevelSafe(unitId); // you already have this helper in UnitsPanelController

            // 3) CURRENT stats @ lvl  (same pattern you use for the detail panel)
            var cur = new UnitStatsRuntime();
            cur.FromSO(def.baseStats); // pulls base stats from the unit definition

            var g = ProgressionMath.GetGrowthMultipliers(lvl, progressionConfig);
            cur.attack *= g.gA;
            cur.maxHP *= g.gH;
            cur.moveSpeed *= g.gMv;
            cur.attackSpeed *= g.gAS;

            int cp = CPCalculator.UnitCP(cur, lvl, cpWeights);
            int cost = upgradeCost.GetCostForLevel(lvl);

            // 5) Instantiate a row and bind
            var row = Instantiate(rowPrefab, statsPanel.Content);
            row.Bind(
                icon: def.portrait,
                displayName: def.displayName,
                level: lvl,
                hp: Mathf.RoundToInt(cur.maxHP),
                atk: Mathf.RoundToInt(cur.attack),
                def: Mathf.RoundToInt(cur.defense),
                cp: cp,
                coins: cost // or rename in your row to "Cost"
            );
            // NEW: give the row its behavior
            row.Initialize(unitId, type, OnBucketRowClicked);
        }
        // after you've created all rows under statsPanel.Content
        int n = statsPanel.Content.childCount;
        if (n > 0)
        {
            // turn OFF divider on last row
            var last = statsPanel.Content.GetChild(n - 1).GetComponent<BucketStatRow>();
            if (last)
                last.SetDividerVisible(false);


        }




    }
    private void OpenBucketPanel(BucketHeader.BucketType type)
    {
        if (!statsPanel || !rowPrefab) return;

        // 1) Title
        string title = type switch
        {
            BucketHeader.BucketType.Deployed => deployedHeader.Title,
            BucketHeader.BucketType.Undeployed => undeployedHeader.Title,
            _ => unachievedHeader.Title
        };
        statsPanel.Clear();
        statsPanel.Show(title);

        // 2) Source list (which units to show)
        Transform bucketParent = type switch
        {
            BucketHeader.BucketType.Deployed => deployedContainer,
            BucketHeader.BucketType.Undeployed => undeployedContainer,
            _ => unachievedContainer
        };

        // 3) Build rows
        for (int i = 0; i < bucketParent.childCount; i++)
        {
            var card = bucketParent.GetChild(i).GetComponent<UnitCardView>();
            if (!card) continue;

            int unitId = card.UnitId;

            // 1) Definition
            var def = unitsDatabase.GetById(unitId);
            if (!def) continue;

            // 2) Level
            int lvl = GetLevelSafe(unitId);

            // 3) CURRENT stats @ lvl
            var cur = new UnitStatsRuntime();
            cur.FromSO(def.baseStats);

            var g = ProgressionMath.GetGrowthMultipliers(lvl, progressionConfig);
            cur.attack *= g.gA;
            cur.maxHP *= g.gH;
            cur.moveSpeed *= g.gMv;
            cur.attackSpeed *= g.gAS;

            // 4) CP and upgrade cost
            int cp = CPCalculator.UnitCP(cur, lvl, cpWeights);
            int cost = upgradeCost.GetCostForLevel(lvl);

            // 5) Instantiate a row and bind
            var row = Instantiate(rowPrefab, statsPanel.Content);
            row.Bind(
                icon: def.portrait,
                displayName: def.displayName,
                level: lvl,
                hp: Mathf.RoundToInt(cur.maxHP),
                atk: Mathf.RoundToInt(cur.attack),
                def: Mathf.RoundToInt(cur.defense),
                cp: cp,
                coins: cost
            );

            // NEW: give the row its behavior
            row.Initialize(unitId, type, OnBucketRowClicked);
        }

        // after you've created all rows under statsPanel.Content
        int n = statsPanel.Content.childCount;
        if (n > 0)
        {
            // turn OFF divider on last row
            var last = statsPanel.Content.GetChild(n - 1).GetComponent<BucketStatRow>();
            if (last)
                last.SetDividerVisible(false);
        }
    }

    // Called when a row's icon button is clicked
    private void OnBucketRowClicked(int unitId, BucketHeader.BucketType bucketType)
    {
        // 1) Close the stats panel overlay
        if (statsPanel)
            statsPanel.gameObject.SetActive(false);

        // 2) Open the correct card / detail for this unit
        // Option A: jump directly to detail view of this unit:
        //ShowCardsScreen();

        OnCardClicked(unitId);

        // If instead you want to just go back to card list without opening detail,
        // comment the line above and use:
        // ShowCardsScreenPublic();
    }
    // Called when a stats-row icon is clicked



    // -----------------------  Deploy an UnDeployed Card -----------------------

    private void OnClickDeploy()
    {
        if (deployOverlay == null || PlayerUnits == null) return;

        // Make sure orders are synced
        EnsureOrderListsSync();

        // Open overlay, pass the candidate (the _selectedUnitId shown in detail),
        // and pass the authoritative orders so overlay renders exactly like CardScreen.
        deployOverlay.Show(
            candidateToDeploy: _selectedUnitId,
            db: unitsDatabase,
            player: PlayerUnits,
            deployedOrder: _deployedOrder,
            undeployedOrder: _undeployedOrder,
            onSaveReplace: (cand, repl, k, j) => HandleDeploySave(cand, repl, k, j),
            onClose: CloseDeployOverlay
        );

        // Hide Card/Detail screens if you keep single-screen invariant

        if (UnDeployedSelection)
        {
            UnDeployedSelection.SetActive(true);
            UnDeployedSelection.transform.SetAsLastSibling(); // <-- be on top of canvas
        }

        if (cardsScreen) cardsScreen.SetActive(false);
        if (detailScreen) detailScreen.SetActive(false);
    }

    public void OpenUndeployedStatsFromSwapPanel()
    {
        // Optional safety: make sure lists are synced with the current model
        EnsureOrderListsSync();

        // RebuildAllBuckets is already called inside HandleDeploySave after each swap,
        // so normally you don’t need to call it again here.
        // If you ever feel things are out of sync, you *can* uncomment this:
        // RebuildAllBuckets();

        // Reuse the same stats logic you already use for the Undeployed bucket
        OpenBucketPanel(BucketHeader.BucketType.Undeployed);
    }


    private void CloseDeployOverlay()
    {
        if (deployOverlay) deployOverlay.gameObject.SetActive(false);

        // Return to Cards screen (single-screen invariant)
        ShowCardsScreen();
        // Optional: clear selection highlight on cards
        foreach (var c in _spawnedCards) if (c) c.SetSelected(false);
    }
    

    // candidateId moves into Deployed at index k
    // replacedId moves into Undeployed at index j

    private void HandleDeploySave1(int candidateId, int replacedId, int k, int j)
    {
        if (PlayerUnits == null) return;

        // --- Levels BEFORE changing flags
        int replacedLevel = Mathf.Max(1, PlayerUnits.GetLevel(replacedId));
        int candidateLevel = Mathf.Max(1, PlayerUnits.GetLevel(candidateId));

        // --- Update the order lists first (source of truth)
        _deployedOrder.Remove(candidateId);
        _undeployedOrder.Remove(replacedId);

        k = Mathf.Clamp(k, 0, _deployedOrder.Count);
        j = Mathf.Clamp(j, 0, _undeployedOrder.Count);

        _deployedOrder.Insert(k, candidateId);  // put candidate exactly at k
        _undeployedOrder.Insert(j, replacedId); // put replaced exactly at j

        // --- Flip deployed flags
        PlayerUnits.SetDeployed(replacedId, false);
        PlayerUnits.SetDeployed(candidateId, true);

        // --- Transfer levels
        PlayerUnits.SetLevel(candidateId, replacedLevel); // candidate inherits level
        PlayerUnits.SetLevel(replacedId, 1);             // replaced drops to level 1

        // --- Refresh overlay in-place with the SAME lists (no ID ordering inside overlay)
        if (deployOverlay && deployOverlay.gameObject.activeSelf)
        {
            deployOverlay.RefreshWithOrders(
                _deployedOrder,
                _undeployedOrder,
                unitsDatabase,
                PlayerUnits,
                candidateId,   // keep highlight on undeployed grid
                replacedId     // keep highlight on deployed grid
            );
        }

        // --- Refresh the CardScreen behind the overlay so it's ready

        var gsm = GameStartManager.Instance;
        if (gsm?.PlayerUnits != null)
        {
            // Your toggles (e.g.)
            gsm.PlayerUnits.SetDeployed(candidateId, true);
            gsm.PlayerUnits.SetDeployed(replacedId, false);

            // NEW: Swap list entries at slots k (deployed) and j (undeployed)
            if (k >= 0 && k < _deployedOrder.Count && j >= 0 && j < _undeployedOrder.Count)
            {
                int temp = _deployedOrder[k];
                _deployedOrder[k] = candidateId;     // Candidate to deployed slot k
                _undeployedOrder[j] = replacedId;    // Replaced to undeployed slot j
            }

            // Persist states
            SaveSystem.SetUnitDeployed(candidateId, true);
            SaveSystem.SetUnitDeployed(replacedId, false);

            // Sanitize/save orders
            _deployedOrder = _deployedOrder.Where(id => id >= 0 && gsm.PlayerUnits.Exists(id)).Distinct().ToList();
            _undeployedOrder = _undeployedOrder.Where(id => id >= 0 && gsm.PlayerUnits.Exists(id)).Distinct().ToList();
            SaveSystem.SetUnitOrders(_deployedOrder, _undeployedOrder);

            // NEW: Sync model post-save
            EnsureOrderListsSync();

            Debug.Log($"Saved swap: Candidate={candidateId} at k={k}, Replaced={replacedId} at j={j}");
        }
        RebuildAllBuckets();

    }
    private void HandleDeploySave(int candidateId, int replacedId, int k, int j)
    {
        if (PlayerUnits == null) return;

        if (candidateId < 0 || replacedId < 0) return;
        if (!PlayerUnits.Exists(candidateId) || !PlayerUnits.Exists(replacedId)) return;

        // --- Levels BEFORE changing flags
        int replacedLevel = Mathf.Max(1, PlayerUnits.GetLevel(replacedId));
        int candidateLevel = Mathf.Max(1, PlayerUnits.GetLevel(candidateId));

        _undeployedOrder.Remove(candidateId);   // [FIX]
        _deployedOrder.Remove(replacedId);     // [FIX]

        // Clamp target indices to current list sizes (after removes)
        k = Mathf.Clamp(k, 0, _deployedOrder.Count);
        j = Mathf.Clamp(j, 0, _undeployedOrder.Count);

        // Insert into target lists at the requested positions
        _deployedOrder.Insert(k, candidateId);
        _undeployedOrder.Insert(j, replacedId);

        // --- Flip deployed flags in the runtime model
        PlayerUnits.SetDeployed(replacedId, false);
        PlayerUnits.SetDeployed(candidateId, true);

        // --- Transfer levels in the runtime model
        PlayerUnits.SetLevel(candidateId, replacedLevel);
        PlayerUnits.SetLevel(replacedId, 1);

        SaveSystem.SetUnitUnlocked(candidateId, true);                 // [ADDED] ensure unlocked
        SaveSystem.SetUnitDeployed(candidateId, true);                 // [ADDED]
        SaveSystem.SetUnitLevel(candidateId, replacedLevel);           // [ADDED]

        SaveSystem.SetUnitDeployed(replacedId, false);                 // [ADDED]
        SaveSystem.SetUnitLevel(replacedId, 1);                        // [ADDED]

        var gsm = GameStartManager.Instance;
        if (gsm?.PlayerUnits != null)
        {
            _deployedOrder = _deployedOrder
                .Where(id => id >= 0 && gsm.PlayerUnits.Exists(id)).Distinct().ToList();
            _undeployedOrder = _undeployedOrder
                .Where(id => id >= 0 && gsm.PlayerUnits.Exists(id)).Distinct().ToList();

            SaveSystem.SetUnitOrders(_deployedOrder, _undeployedOrder); // [ADDED]
        }


        if (deployOverlay && deployOverlay.gameObject.activeSelf)
        {
            // Note: candidate now lives in Deployed grid; replaced lives in Undeployed.
            deployOverlay.RefreshWithOrders(
                _deployedOrder,
                _undeployedOrder,
                unitsDatabase,
                PlayerUnits,
                /* highlightDeployed  */ candidateId,  // [CHANGED] keep highlight where they are now
                /* highlightUndeployed*/ replacedId    // [CHANGED]
            );
        }

        // --- Rebuild the main screen so it’s correct behind the overlay
        EnsureOrderListsSync();
        RebuildAllBuckets();
    }


    private void ClearChildren(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    private void RebuildAllBuckets()
    {
        if (unitsDatabase == null || PlayerUnits == null) return;

        EnsureOrderListsSync();

        //// Clear
  

        DestroyAllChildren(deployedContainer);
        DestroyAllChildren(undeployedContainer);
        DestroyAllChildren(unachievedContainer);
        _spawnedCards.Clear();
        _cardsByUnitId.Clear();

        // Clear your three containers & caches here…

        // Deployed — in _deployedOrder
        foreach (var id in _deployedOrder)
        {
            if (!PlayerUnits.IsUnlocked(id) || !PlayerUnits.IsDeployed(id)) continue;
            var def = unitsDatabase.GetById(id);
            if (!def) continue;

            int lvl = Mathf.Max(1, PlayerUnits.GetLevel(id));
            var card = Instantiate(cardPrefab, deployedContainer);
            card.Bind(def, true, lvl, () => OnCardClicked(id));

            card.SetLockAwareHeader(def, true, lvl); // ✅ unlocked = true


            // hard reset any overlay/selection visuals so CardScreen is always clean
            card.SetSelected(false);
            card.ClearOverlayMarks();

            // … any cues/labels you show for deployed
            _spawnedCards.Add(card);
            _cardsByUnitId[id] = card;
        }

        // Undeployed — in _undeployedOrder
        foreach (var id in _undeployedOrder)
        {
            if (!PlayerUnits.IsUnlocked(id) || PlayerUnits.IsDeployed(id)) continue;
            var def = unitsDatabase.GetById(id);
            if (!def) continue;

            int lvl = Mathf.Max(1, PlayerUnits.GetLevel(id));
            var card = Instantiate(cardPrefab, undeployedContainer);
            card.Bind(def, true, lvl, () => OnCardClicked(id));

            card.SetLockAwareHeader(def, true, lvl); // ✅ unlocked = true


            // hard reset any overlay/selection visuals so CardScreen is always clean
            card.SetSelected(false);
            card.ClearOverlayMarks();

            _spawnedCards.Add(card);
            _cardsByUnitId[id] = card;
        }

        // Unachieved — (you can keep ID order)
        foreach (var def in unitsDatabase.Units)
        {
            if (!def) continue;
            int id = def.unitId;
            if (PlayerUnits.IsUnlocked(id)) continue;

            var card = Instantiate(cardPrefab, unachievedContainer);
            card.Bind(def, false, 1, () => OnCardClicked(id));

            card.SetLockAwareHeader(def, false, 1); // ✅ correct

            // hard reset any overlay/selection visuals so CardScreen is always clean
            card.SetSelected(false);
            card.ClearOverlayMarks();

            _spawnedCards.Add(card);
            _cardsByUnitId[id] = card;
        }

        RefreshDeployedUpgradeCues();
        UpdateBucketSectionsLayout();
        AdjustUnachievedPanelPosition();

    }
    // --- add once in UnitsPanelController
    private static void DestroyAllChildren(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
           UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
    }

    private void ApplySavedOrder(RectTransform parent, Dictionary<int, int> slotMap)
    {
        if (!parent) return;
        int n = parent.childCount;
        if (n <= 1) return;

        // Snapshot current order
        var children = new List<Transform>(n);
        var unitIds = new List<int>(n);
        for (int i = 0; i < n; i++)
        {
            var t = parent.GetChild(i);
            children.Add(t);
            var cv = t.GetComponent<UnitCardView>();
            unitIds.Add(cv ? cv.UnitId : -1);
        }

        // Prepare slots and assignment tracking
        var slots = new Transform[n];        // final positions
        var assigned = new HashSet<Transform>(); // which children are already placed

        // PASS 1: place all items that have a saved index (absolute index placement)
        if (slotMap != null && slotMap.Count > 0)
        {
            for (int i = 0; i < n; i++)
            {
                int uid = unitIds[i];
                if (uid < 0) continue;

                if (slotMap.TryGetValue(uid, out int desired))
                {
                    if (desired < 0 || desired >= n) continue;     // <-- NEW: ignore invalid indices

                    int idx = desired;                              // no clamping to 0
                    while (idx < n && slots[idx] != null) idx++;

                    if (idx < n)
                    {
                        slots[idx] = children[i];
                        assigned.Add(children[i]);
                    }
                }
            }
        }

        // PASS 2: place remaining items in their original order into free slots
        int write = 0;
        for (int i = 0; i < n; i++)
        {
            var t = children[i];
            if (assigned.Contains(t)) continue;

            // advance to next empty slot
            while (write < n && slots[write] != null) write++;
            if (write >= n) break;

            slots[write] = t;
            write++;
        }

        // Apply the final exact sibling indices
        for (int i = 0; i < n; i++)
        {
            if (slots[i] != null)
                slots[i].SetSiblingIndex(i);
        }
    }

    private void EnsureOrderListsSync()
    {
        if (unitsDatabase == null || PlayerUnits == null) return;

        var deployedIds = new List<int>();
        var undeployedIds = new List<int>();

        foreach (var def in unitsDatabase.Units)
        {
            if (!def) continue;
            int id = def.unitId;
            if (!PlayerUnits.IsUnlocked(id)) continue;

            if (PlayerUnits.IsDeployed(id)) deployedIds.Add(id);
            else undeployedIds.Add(id);
        }

        // First-time seed: ID order
        if (_deployedOrder.Count == 0 && deployedIds.Count > 0)
            _deployedOrder.AddRange(deployedIds.OrderBy(x => x));
        if (_undeployedOrder.Count == 0 && undeployedIds.Count > 0)
            _undeployedOrder.AddRange(undeployedIds.OrderBy(x => x));

        // Remove anything no longer in the set
        _deployedOrder.RemoveAll(id => !deployedIds.Contains(id));
        _undeployedOrder.RemoveAll(id => !undeployedIds.Contains(id));

        // Append any newly present
        foreach (var id in deployedIds)
            if (!_deployedOrder.Contains(id)) _deployedOrder.Add(id);
        foreach (var id in undeployedIds)
            if (!_undeployedOrder.Contains(id)) _undeployedOrder.Add(id);
    }
  


}
