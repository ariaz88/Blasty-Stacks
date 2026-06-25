using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


using TMPro;


public class DeployOverlayController : MonoBehaviour
{
    [Header("Grids")]
    [SerializeField] private Transform deployedGrid;      // grid for currently deployed
    [SerializeField] private Transform undeployedGrid;    // grid for undeployed list

    [Header("UI")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button closeButton;

    [Header("Prefabs")]
    [SerializeField] private UnitCardView cardPrefab;

    // runtime
    private UnitsDatabaseSO _db;
    private PlayerUnitsModel _player;

    // authoritative UI orders (kept exactly as passed from UnitsPanelController)
    private readonly List<int> _deployedOrder = new();
    private readonly List<int> _undeployedOrder = new();

    // selection state inside overlay
    private int _candidateUndeployedId = -1;  // the card you want to bring IN (undeployed tab)
    private int _replaceDeployedId = -1;  // the deployed card you will replace

    // callbacks to UnitsPanelController
    private Action<int, int, int, int> _onSaveReplace; // (candidateId, replacedId, k, j)
    private Action _onClose;

    private readonly List<Tween> _deployedShakeTweens = new List<Tween>();

    // add with your other fields
    private bool _isClosing = false;   // prevents refresh/rebuild while we’re closing

    [SerializeField] private Button undeployedStatsBurgerButton;   // burger in SwapPanel header
    [SerializeField] private UnitsPanelController unitsPanelController; // drag from scene



    // ---------------- API expected by UnitsPanelController ----------------

    /// <summary>
    /// Opens overlay. Keeps the exact visual order that Cards screen had by using
    /// the passed-in deployedOrder / undeployedOrder lists (no resort).
    /// </summary>
    /// 
    private void Awake()
    {
       

        if (saveButton)
            saveButton.onClick.AddListener(HandleSave);

        // NEW: burger button to open Undeployed stats panel
        if (undeployedStatsBurgerButton && unitsPanelController)
            undeployedStatsBurgerButton.onClick.AddListener(HandleUndeployedStatsBurgerClicked);
    }

    private void HandleUndeployedStatsBurgerClicked()
    {
        // Calls the public method we added in UnitsPanelController
        unitsPanelController.OpenUndeployedStatsFromSwapPanel();
    }



    private void OnDisable()
    {
        StopDeployedShake();
    }

    public void Show(
        int candidateToDeploy,
        UnitsDatabaseSO db,
        PlayerUnitsModel player,
        List<int> deployedOrder,         // named param used by UnitsPanelController
        List<int> undeployedOrder,       // named param used by UnitsPanelController
        Action<int, int, int, int> onSaveReplace,
        Action onClose
    )
    {
        _isClosing = false;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        _db = db;
        _player = player;

        _onSaveReplace = onSaveReplace;
        _onClose = onClose;

        _deployedOrder.Clear();
        _undeployedOrder.Clear();
        if (deployedOrder != null) _deployedOrder.AddRange(deployedOrder);
        if (undeployedOrder != null) _undeployedOrder.AddRange(undeployedOrder);

        _candidateUndeployedId = candidateToDeploy;
        _replaceDeployedId = -1; // user picks this

        BuildUI();

        if (titleText) titleText.text = "Select a Hero to Replace";

        WireButtons();
    }

    /// <summary>
    /// Re-renders both grids with the provided orders and keeps the correct highlights.
    /// Used immediately after a save to let the user continue swapping without closing.
    /// </summary>
    public void RefreshWithOrders(
        List<int> deployedOrder,
        List<int> undeployedOrder,
        UnitsDatabaseSO db,
        PlayerUnitsModel player,
        int highlightUndeployedId,   // candidate (border highlight)
        int highlightDeployedId      // replace target (full green overlay)
    )
    {
        if (_isClosing) return;  // ignore late refresh while closing

        _db = db;
        _player = player;

        _deployedOrder.Clear();
        _undeployedOrder.Clear();
        if (deployedOrder != null) _deployedOrder.AddRange(deployedOrder);
        if (undeployedOrder != null) _undeployedOrder.AddRange(undeployedOrder);

        _candidateUndeployedId = highlightUndeployedId;
        _replaceDeployedId = highlightDeployedId;

        BuildUI();
    }

    // ---------------- internal UI build ----------------

    private void WireButtons()
    {
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() =>
            {
                StopDeployedShake();
                _isClosing = true;
                gameObject.SetActive(false);
                _onClose?.Invoke();
            });
        }


        if (saveButton)
        {
            saveButton.onClick.RemoveAllListeners();
            saveButton.onClick.AddListener(HandleSave);
            //saveButton.interactable = (_candidateUndeployedId >= 0 && _replaceDeployedId >= 0);
        }
    }

    private void BuildUI()
    {
        Clear(deployedGrid);
        Clear(undeployedGrid);

        // Deployed grid (exact order)
        for (int i = 0; i < _deployedOrder.Count; i++)
        {
            int id = _deployedOrder[i];
            if (!_player.Exists(id) || !_player.IsUnlocked(id) || !_player.IsDeployed(id)) continue;

            var def = _db.GetById(id);
            if (!def) continue;

            int lvl = Mathf.Max(1, _player.GetLevel(id));
            var card = Instantiate(cardPrefab, deployedGrid);
            card.Bind(def, true, lvl, () => OnClickDeployedCard(id));

            // Overlay styling: deployed targets should use FULL green overlay when selected.
            bool isTarget = (id == _replaceDeployedId);
            card.SetDeployedTargetStyle(isTarget);
        }

        // Undeployed grid (exact order)
        for (int i = 0; i < _undeployedOrder.Count; i++)
        {
            int id = _undeployedOrder[i];
            if (!_player.Exists(id) || !_player.IsUnlocked(id) || _player.IsDeployed(id)) continue;

            var def = _db.GetById(id);
            if (!def) continue;

            int lvl = Mathf.Max(1, _player.GetLevel(id));
            var card = Instantiate(cardPrefab, undeployedGrid);
            card.Bind(def, true, lvl, () => OnClickUndeployedCard(id));

            // highlight ONLY the selected candidate
            card.SetOverlayCandidate(id == _candidateUndeployedId);
        }



        // ensure save btn state reflects current selection
        //if (saveButton) saveButton.interactable = (_candidateUndeployedId >= 0 && _replaceDeployedId >= 0);

        // NEW: start shake for all deployed cards every time we rebuild the overlay
        StartDeployedShake();

    }

    private void Clear(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    // ---------------- clicks ----------------

    private void OnClickUndeployedCard(int id)
    {
        _candidateUndeployedId = id;

        // Update border highlight so only the selected candidate is on
        for (int i = 0; i < undeployedGrid.childCount; i++)
        {
            var cv = undeployedGrid.GetChild(i).GetComponent<UnitCardView>();
            if (!cv) continue;
            cv.SetOverlayCandidate(cv.UnitId == _candidateUndeployedId);
        }

        //if (saveButton) saveButton.interactable = (_candidateUndeployedId >= 0 && _replaceDeployedId >= 0);
    }


    private void OnClickDeployedCard(int id)
    {
        _replaceDeployedId = id;

        // Update full green overlay on deployed grid
        for (int i = 0; i < deployedGrid.childCount; i++)
        {
            var cv = deployedGrid.GetChild(i).GetComponent<UnitCardView>();
            if (!cv) continue;
            cv.SetDeployedTargetStyle(cv.UnitId == _replaceDeployedId);
        }
        if (saveButton) saveButton.interactable = (_candidateUndeployedId >= 0 && _replaceDeployedId >= 0);
    }

    // ---------------- save ----------------

    private void HandleSave1()
    {
        if (_candidateUndeployedId < 0 || _replaceDeployedId < 0) return;

        // k = index in Deployed, j = index in Undeployed (keep exact slots)
        int k = IndexOf(_deployedOrder, _replaceDeployedId);
        int j = IndexOf(_undeployedOrder, _candidateUndeployedId);
        if (k < 0) k = Mathf.Clamp(k, 0, _deployedOrder.Count);
        if (j < 0) j = Mathf.Clamp(j, 0, _undeployedOrder.Count);

        // 1) stop shake
        StopDeployedShake();

        // 2) mark closing so any external RefreshWithOrders calls get ignored
        _isClosing = true;

        // 3) commit swap to controller (it updates model and CardsScreen)
        _onSaveReplace?.Invoke(_candidateUndeployedId, _replaceDeployedId, k, j);

        // 4) clear selection, close overlay, tell controller to show CardsScreen
        _candidateUndeployedId = -1;
        _replaceDeployedId = -1;

        gameObject.SetActive(false);   // triggers OnDisable -> StopDeployedShake() as a safety
        _onClose?.Invoke();            // your UnitsPanelController should reactivate CardsScreen here
    }

    private void HandleSave()
    {
        // NEW: if no deployed hero was selected, behave like Back
        if (_replaceDeployedId < 0)
        {
            // cancel selection + stop shake
            StopDeployedShake();
            _candidateUndeployedId = -1;
            _replaceDeployedId = -1;

            // same behaviour as Back button
            //Hide();
            gameObject.SetActive(false);
            _onClose?.Invoke();
            return;
        }

        // still guard against missing candidate
        if (_candidateUndeployedId < 0)
            return;

        // k = index in Deployed, j = index in Undeployed (keep exact slots)
        int k = IndexOf(_deployedOrder, _replaceDeployedId);
        int j = IndexOf(_undeployedOrder, _candidateUndeployedId);
        if (k < 0) k = Mathf.Clamp(k, 0, _deployedOrder.Count);
        if (j < 0) j = Mathf.Clamp(j, 0, _undeployedOrder.Count);

        // 1) stop shake
        StopDeployedShake();

        // 2) mark closing so any external RefreshWithOrders calls get ignored
        _isClosing = true;

        // 3) commit swap to controller (it updates model and CardsScreen)
        _onSaveReplace?.Invoke(_candidateUndeployedId, _replaceDeployedId, k, j);

        // 4) clear selection, close overlay, tell controller to show CardsScreen
        _candidateUndeployedId = -1;
        _replaceDeployedId = -1;

        //Hide();          // instead of gameObject.SetActive(false)
        gameObject.SetActive(false);
        _onClose?.Invoke();
    }


    private static int IndexOf(List<int> list, int id)
    {
        if (list == null) return -1;
        for (int i = 0; i < list.Count; i++)
            if (list[i] == id) return i;
        return -1;
    }

    private void StartDeployedShake()
    {
        StopDeployedShake(); // safety

        if (!deployedGrid) return;
        for (int i = 0; i < deployedGrid.childCount; i++)
        {
            var t = deployedGrid.GetChild(i) as RectTransform;
            if (!t) continue;

            // rotate around Z between -15° and +15° endlessly, no visible pause
            var tw = t.DOLocalRotate(new Vector3(0f, 0f, 5f), 0.8f)
                      .SetEase(Ease.InOutSine)
                      .SetLoops(-1, LoopType.Yoyo)
                      .SetRelative(true); // oscillate around current rotation

            _deployedShakeTweens.Add(tw);
        }
    }

    private void StopDeployedShake()
    {
        // stop and reset rotation cleanly
        foreach (var tw in _deployedShakeTweens)
        {
            if (tw == null) continue;
            if (tw.IsActive()) tw.Kill();
        }
        _deployedShakeTweens.Clear();

        if (!deployedGrid) return;
        for (int i = 0; i < deployedGrid.childCount; i++)
        {
            var t = deployedGrid.GetChild(i) as RectTransform;
            if (!t) continue;
            t.localRotation = Quaternion.identity;
        }
    }

}

public class DeployOverlayController1 : MonoBehaviour
{    // add at top of class
    [SerializeField] private bool stayOpenAfterSave = false; // optional toggle

    [Header("Root & Buttons")]
    [SerializeField] private GameObject root;   // whole overlay GO
    [SerializeField] private Button backButton; // top-left/back button
    [SerializeField] private Button saveButton; // optional “SAVE”

    [Header("Lists")]
    [SerializeField] private RectTransform deployedGrid;   // grid under “Deployed Heroes”
    [SerializeField] private RectTransform undeployedGrid; // grid under “Undeployed Heroes”
    [SerializeField] private UnitCardView cardPrefab;

    // runtime
    private int _candidateToDeploy = -1;                // the unit we want to deploy (from Detail screen)
    private int _selectedDeployedToReplace = -1;        // what the user picked to replace
    private Action _onClose;
    //private Action<int, int> _onSaveReplace;             // (candidate, replaceThisDeployed)
    private Action<int, int, int, int> _onSaveReplace; // (candidateId, replaceId, deployedSlot, undeployedSlot)


    // data
    private UnitsDatabaseSO _db;
    private PlayerUnitsModel _player;


    // Scale
    [SerializeField, Min(1f)] private float selectedScale = 1.1f;
    [SerializeField, Min(0.01f)] private float normalScale = 1.0f;
    // If you use DOTween, set >0 (e.g., 0.12f). If not, leave 0 for instant.
    [SerializeField, Min(0f)] private float scaleTween = 0.0f;

    // Positioning

    private int _selectedDeployedIndex = -1;  // where the deployed-to-replace currently sits
    private int _selectedUndeployedIndex = -1;  // where the candidate currently sits

    //lists
    private List<int> _deployedOrderRef;
    private List<int> _undeployedOrderRef;

    // Track deployed card RectTransforms to animate
    private readonly List<RectTransform> _deployedRects = new();

    // Track active shake tweens (one per deployed card)
    private readonly List<Tween> _deployedShakeTweens = new();

    // Gate: deployed selection is allowed only after an undeployed card is selected
    // (We already have _candidateToDeploy; we’ll just use that check in the click handler)





    private void Awake()
    {
        Hide();

        if (backButton)
            backButton.onClick.AddListener(() => { Hide(); _onClose?.Invoke(); });

        if (saveButton)
            saveButton.onClick.AddListener(HandleSave);
    }

    public void Show(
        int candidateToDeploy,
        UnitsDatabaseSO db,
        PlayerUnitsModel player,
        List<int> deployedOrder,      // from controller
        List<int> undeployedOrder,    // from controller
        System.Action<int, int, int, int> onSaveReplace,
        System.Action onClose)
    {
        _db = db;
        _player = player;
        _onSaveReplace = onSaveReplace;
        _onClose = onClose;

        _deployedOrderRef = deployedOrder;
        _undeployedOrderRef = undeployedOrder;

        // preselect candidate from DetailView (first time)
        _candidateToDeploy = candidateToDeploy;
        _selectedUndeployedIndex = -1;   // will compute after build
        _selectedDeployedToReplace = -1;
        _selectedDeployedIndex = -1;

        gameObject.SetActive(true);

        // Build strictly from lists; no local ordering
        RefreshWithOrders(
            _deployedOrderRef,
            _undeployedOrderRef,
            _db,
            _player,
            _candidateToDeploy,
            _selectedDeployedToReplace
        );
    }



    public void Hide()
    {
        if (root) root.SetActive(false);
    }

    private void HandleSave()
    {

        StopDeployedShake();

        // Save = behave like Back; overlay closes, CardsScreen already refreshed by controller
        _onClose?.Invoke();
    }




    private static void Clear(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
    }



    // ... existing fields/methods ...



    public void RefreshFromModel(int keepCandidate = -1)
    {
        if (_db == null || _player == null) return;

        Clear(deployedGrid);
        Clear(undeployedGrid);

        var deployedIds = GatherIds(deployed: true, unlockedOnly: true);
        var undeployedIds = GatherIds(deployed: false, unlockedOnly: true);

        // Rebuild deployed list (click = choose which to replace)
        foreach (var id in deployedIds)
        {
            SpawnCard(deployedGrid, id, onClick: () =>
            {
                _selectedDeployedToReplace = id;

                // capture the slot where this deployed card currently sits
                var t = FindCardByUnitId(deployedGrid, id);
                _selectedDeployedIndex = t ? t.GetSiblingIndex() : -1;

                // apply 1.1× scale to the selected, 1.0× to others
                UpdateGridSelectionScales(deployedGrid, id);
            });
        }

        // Rebuild undeployed list (click = choose candidate to deploy)
        foreach (var id in undeployedIds)
        {
            SpawnCard(undeployedGrid, id, onClick: () =>
            {
                _candidateToDeploy = id;

                // capture the slot where this undeployed card currently sits
                var t = FindCardByUnitId(undeployedGrid, id);
                _selectedUndeployedIndex = t ? t.GetSiblingIndex() : -1;

                // apply 1.1× scale to the selected, 1.0× to others
                UpdateGridSelectionScales(undeployedGrid, id);
            });
        }

        // Keep previously selected candidate if requested
        if (keepCandidate >= 0)
            _candidateToDeploy = keepCandidate;

        // --- NEW: always compute the current indices so they are not -1 ---
        _selectedUndeployedIndex = -1;
        _selectedDeployedIndex = -1;

        if (_candidateToDeploy >= 0)
        {
            var candT = FindCardByUnitId(undeployedGrid, _candidateToDeploy);
            _selectedUndeployedIndex = candT ? candT.GetSiblingIndex() : -1;
        }

        if (_selectedDeployedToReplace >= 0)
        {
            var repT = FindCardByUnitId(deployedGrid, _selectedDeployedToReplace);
            _selectedDeployedIndex = repT ? repT.GetSiblingIndex() : -1;
        }

        // Initial visual selection (scale) for both lists
        UpdateGridSelectionScales(undeployedGrid, _candidateToDeploy);
        UpdateGridSelectionScales(deployedGrid, _selectedDeployedToReplace);

    }


    private List<int> GatherIds(bool deployed, bool unlockedOnly)
    {
        var list = new List<int>();
        foreach (var def in _db.Units)
        {
            if (!def) continue;
            if (unlockedOnly && !_player.IsUnlocked(def.unitId)) continue;
            if (_player.IsDeployed(def.unitId) == deployed) list.Add(def.unitId);
        }
        return list;
    }

    private void SpawnCard(RectTransform parent, int unitId, System.Action onClick)
    {
        var def = _db.GetById(unitId);
        if (!def) return;
        var card = Instantiate(cardPrefab, parent);
        card.Bind(def, _player.IsUnlocked(unitId), Mathf.Max(1, _player.GetLevel(unitId)), onClick);
    }


    private void UpdateGridSelectionScales(RectTransform grid, int selectedUnitId)
    {
        if (!grid) return;

        for (int i = 0; i < grid.childCount; i++)
        {
            var t = grid.GetChild(i);
            var c = t.GetComponent<UnitCardView>();
            if (!c) continue;

            bool isSelected = (c.UnitId == selectedUnitId);

            // scale visual
            ScaleCard(t, isSelected ? selectedScale : normalScale);

            // call the method directly (if your UnitCardView exposes it)
            // remove this line if you don't want/need a selected state on the card
            c.SetSelected(isSelected);
        }
    }

    private void ScaleCard(Transform card, float targetScale)
    {
#if DOTWEEN
        if (scaleTween > 0f)
        {
            card.DOScale(targetScale, scaleTween).SetEase(Ease.OutSine);
            return;
        }
#endif
        card.localScale = Vector3.one * targetScale;
    }

    private Transform FindCardByUnitId(RectTransform grid, int unitId)
    {
        if (!grid) return null;
        for (int i = 0; i < grid.childCount; i++)
        {
            var t = grid.GetChild(i);
            var c = t.GetComponent<UnitCardView>();
            if (c && c.UnitId == unitId)
                return t;
        }
        return null;
    }

    // DeployOverlayController
    private static void ClearGrid(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }


    public void RefreshWithOrders(
        List<int> deployedOrder,
        List<int> undeployedOrder,
        UnitsDatabaseSO db,
        PlayerUnitsModel player,
        int candidateToDeploy,
        int deployedToReplace)
    {
        // Stop any running shakes before rebuilding
        StopDeployedShake();

        _deployedRects.Clear();
        // ... then continue with Clear(deployedGrid), Clear(undeployedGrid) and rebuilding

        // IMPORTANT: clear the two lists before rebuilding
        ClearGrid(deployedGrid);
        ClearGrid(undeployedGrid);

        _db = db;
        _player = player;

        Clear(deployedGrid);
        Clear(undeployedGrid);

        // Deployed grid (exact order)
        foreach (var id in deployedOrder)
        {
            if (!_player.IsUnlocked(id) || !_player.IsDeployed(id)) continue;
            var def = _db.GetById(id);
            if (!def) continue;

            int lvl = Mathf.Max(1, _player.GetLevel(id));
            var card = Instantiate(cardPrefab, deployedGrid);
            card.Bind(def, true, lvl, onClick: () =>
            {
                // #3: block selecting Deployed until an Undeployed candidate is chosen
                if (_candidateToDeploy < 0) return;

                // pick target to replace
                _selectedDeployedToReplace = id;
                _selectedDeployedIndex = card.transform.GetSiblingIndex();
                UpdateGridSelectionScales(deployedGrid, id);
                TrySwapIfReady(); // if we already had a candidate, swap now
            });
            // collect for shaking
            _deployedRects.Add(card.transform as RectTransform);
        }

        // Undeployed grid (exact order)
        foreach (var id in undeployedOrder)
        {
            if (!_player.IsUnlocked(id) || _player.IsDeployed(id)) continue;
            var def = _db.GetById(id);
            if (!def) continue;

            int lvl = Mathf.Max(1, _player.GetLevel(id));
            var card = Instantiate(cardPrefab, undeployedGrid);
            card.Bind(def, true, lvl, onClick: () =>
            {
                // choose candidate
                _candidateToDeploy = id;
                _selectedUndeployedIndex = card.transform.GetSiblingIndex();
                UpdateGridSelectionScales(undeployedGrid, id);

                // #4: once we have a candidate, shake all deployed cards
                StartDeployedShake();

                TrySwapIfReady(); // if a deployed target already selected, swap now
            });
        }

        // compute indices for preselected candidate (first open)
        _candidateToDeploy = candidateToDeploy;
        if (_candidateToDeploy >= 0)
        {
            var candT = FindCardByUnitId(undeployedGrid, _candidateToDeploy);
            _selectedUndeployedIndex = candT ? candT.GetSiblingIndex() : -1;
        }

        _selectedDeployedToReplace = deployedToReplace;
        if (_selectedDeployedToReplace >= 0)
        {
            var repT = FindCardByUnitId(deployedGrid, _selectedDeployedToReplace);
            _selectedDeployedIndex = repT ? repT.GetSiblingIndex() : -1;
        }

        UpdateGridSelectionScales(undeployedGrid, _candidateToDeploy);
        UpdateGridSelectionScales(deployedGrid, _selectedDeployedToReplace);


        // NEW: if a candidate is already selected (first open from DetailView), start shaking now
        if (_candidateToDeploy >= 0)
            StartDeployedShake();
    }

    private void TrySwapIfReady()
    {
        if (_candidateToDeploy < 0 || _selectedDeployedToReplace < 0) return;
        if (_selectedUndeployedIndex < 0 || _selectedDeployedIndex < 0) return;

        // fire the swap callback (UnitsPanelController will update lists, levels, flags)
        _onSaveReplace?.Invoke(
            _candidateToDeploy,
            _selectedDeployedToReplace,
            _selectedDeployedIndex,   // k
            _selectedUndeployedIndex  // j
        );

        // Stop shake after a swap
        StopDeployedShake();

        // After controller updates, it should call RefreshWithOrders(...) back on us.
        // Clear selections so next swap requires a fresh pair (as you requested).
        _candidateToDeploy = -1;
        _selectedUndeployedIndex = -1;
        _selectedDeployedToReplace = -1;
        _selectedDeployedIndex = -1;
    }

    private void StartDeployedShake(float angle = 3f, float halfPeriod = 0.6f)
    {
        if (_deployedRects.Count == 0) return;

        // kill any leftovers first
        StopDeployedShake();

        foreach (var rt in _deployedRects)
        {
            if (!rt) continue;

            // IMPORTANT: do NOT forcibly set localRotation here – let DOTween handle start
            // This tween goes from -angle -> +angle and back, forever, with no restart jump.
            var tween = rt.DOLocalRotate(
                            new Vector3(0f, 0f, angle),  // target (+angle)
                            halfPeriod
                        )
                        .From(new Vector3(0f, 0f, -angle)) // start at -angle
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetUpdate(false); // use normal update; set true if you want it during pause

            _deployedShakeTweens.Add(tween);
        }
    }



    private void StopDeployedShake()
    {
        foreach (var t in _deployedShakeTweens)
            t?.Kill();
        _deployedShakeTweens.Clear();

        // reset rotations
        foreach (var rt in _deployedRects)
            if (rt) rt.localRotation = Quaternion.identity;
    }


}

//// The first edition in which selecting the deploy and undeploy happens when we  click on savbe
//public class DeployOverlayController1 : MonoBehaviour
//{    // add at top of class
//    [SerializeField] private bool stayOpenAfterSave = false; // optional toggle

//    [Header("Root & Buttons")]
//    [SerializeField] private GameObject root;   // whole overlay GO
//    [SerializeField] private Button backButton; // top-left/back button
//    [SerializeField] private Button saveButton; // optional “SAVE”

//    [Header("Lists")]
//    [SerializeField] private RectTransform deployedGrid;   // grid under “Deployed Heroes”
//    [SerializeField] private RectTransform undeployedGrid; // grid under “Undeployed Heroes”
//    [SerializeField] private UnitCardView cardPrefab;

//    // runtime
//    private int _candidateToDeploy = -1;                // the unit we want to deploy (from Detail screen)
//    private int _selectedDeployedToReplace = -1;        // what the user picked to replace
//    private Action _onClose;
//    //private Action<int, int> _onSaveReplace;             // (candidate, replaceThisDeployed)
//    private Action<int, int, int, int> _onSaveReplace; // (candidateId, replaceId, deployedSlot, undeployedSlot)


//    // data
//    private UnitsDatabaseSO _db;
//    private PlayerUnitsModel _player;


//    // Scale
//    [SerializeField, Min(1f)] private float selectedScale = 1.1f;
//    [SerializeField, Min(0.01f)] private float normalScale = 1.0f;
//    // If you use DOTween, set >0 (e.g., 0.12f). If not, leave 0 for instant.
//    [SerializeField, Min(0f)] private float scaleTween = 0.0f;

//    // Positioning

//    private int _selectedDeployedIndex = -1;  // where the deployed-to-replace currently sits
//    private int _selectedUndeployedIndex = -1;  // where the candidate currently sits

//    //lists
//    private List<int> _deployedOrderRef;
//    private List<int> _undeployedOrderRef;


//    private void Awake()
//    {
//        Hide();

//        if (backButton)
//            backButton.onClick.AddListener(() => { Hide(); _onClose?.Invoke(); });

//        if (saveButton)
//            saveButton.onClick.AddListener(HandleSave);
//    }


//    public void Show(
//    int candidateToDeploy,
//    UnitsDatabaseSO db,
//    PlayerUnitsModel player,
//    List<int> deployedOrder,          // NEW: order from controller
//    List<int> undeployedOrder,        // NEW: order from controller
//    Action<int, int, int, int> onSaveReplace,
//    Action onClose)
//    {
//        _candidateToDeploy = candidateToDeploy;
//        _db = db;
//        _player = player;
//        _onSaveReplace = onSaveReplace;
//        _onClose = onClose;

//        // store references; we never compute order locally
//        _deployedOrderRef = deployedOrder;
//        _undeployedOrderRef = undeployedOrder;

//        gameObject.SetActive(true);

//        // build strictly from provided lists
//        RefreshWithOrders(
//            _deployedOrderRef,
//            _undeployedOrderRef,
//            _db,
//            _player,
//            candidateToDeploy,
//            _selectedDeployedToReplace   // keep previous deployed pick if any
//        );
//    }


//    public void Hide()
//    {
//        if (root) root.SetActive(false);
//    }

//    private void HandleSave()
//    {
//        if (_selectedDeployedToReplace < 0)
//        {
//            Debug.Log("Pick a deployed hero to replace.");
//            return;
//        }

//        _onSaveReplace?.Invoke(
//            _candidateToDeploy,
//            _selectedDeployedToReplace,
//            _selectedDeployedIndex,
//            _selectedUndeployedIndex
//        );

//        // Controller updates the lists; re-render from those same lists
//        if (_deployedOrderRef != null && _undeployedOrderRef != null)
//        {
//            RefreshWithOrders(
//                _deployedOrderRef,
//                _undeployedOrderRef,
//                _db,
//                _player,
//                _candidateToDeploy,
//                _selectedDeployedToReplace
//            );
//        }
//    }



//    private void UpdateSelectionHighlights(RectTransform grid, int pickedId)
//    {
//        for (int i = 0; i < grid.childCount; i++)
//        {
//            var card = grid.GetChild(i).GetComponent<UnitCardView>();
//            if (!card) continue;
//            card.SetSelected(card.UnitId == pickedId);
//        }
//    }

//    private static void Clear(RectTransform parent)
//    {
//        for (int i = parent.childCount - 1; i >= 0; i--)
//            UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
//    }



//    // ... existing fields/methods ...



//    public void RefreshFromModel(int keepCandidate = -1)
//    {
//        if (_db == null || _player == null) return;

//        Clear(deployedGrid);
//        Clear(undeployedGrid);

//        var deployedIds = GatherIds(deployed: true, unlockedOnly: true);
//        var undeployedIds = GatherIds(deployed: false, unlockedOnly: true);

//        // Rebuild deployed list (click = choose which to replace)
//        foreach (var id in deployedIds)
//        {
//            SpawnCard(deployedGrid, id, onClick: () =>
//            {
//                _selectedDeployedToReplace = id;

//                // capture the slot where this deployed card currently sits
//                var t = FindCardByUnitId(deployedGrid, id);
//                _selectedDeployedIndex = t ? t.GetSiblingIndex() : -1;

//                // apply 1.1× scale to the selected, 1.0× to others
//                UpdateGridSelectionScales(deployedGrid, id);
//            });
//        }

//        // Rebuild undeployed list (click = choose candidate to deploy)
//        foreach (var id in undeployedIds)
//        {
//            SpawnCard(undeployedGrid, id, onClick: () =>
//            {
//                _candidateToDeploy = id;

//                // capture the slot where this undeployed card currently sits
//                var t = FindCardByUnitId(undeployedGrid, id);
//                _selectedUndeployedIndex = t ? t.GetSiblingIndex() : -1;

//                // apply 1.1× scale to the selected, 1.0× to others
//                UpdateGridSelectionScales(undeployedGrid, id);
//            });
//        }

//      // Keep previously selected candidate if requested
//if (keepCandidate >= 0)
//    _candidateToDeploy = keepCandidate;

//// --- NEW: always compute the current indices so they are not -1 ---
//_selectedUndeployedIndex = -1;
//_selectedDeployedIndex   = -1;

//if (_candidateToDeploy >= 0)
//{
//    var candT = FindCardByUnitId(undeployedGrid, _candidateToDeploy);
//    _selectedUndeployedIndex = candT ? candT.GetSiblingIndex() : -1;
//}

//if (_selectedDeployedToReplace >= 0)
//{
//    var repT = FindCardByUnitId(deployedGrid, _selectedDeployedToReplace);
//    _selectedDeployedIndex = repT ? repT.GetSiblingIndex() : -1;
//}

//// Initial visual selection (scale) for both lists
//UpdateGridSelectionScales(undeployedGrid, _candidateToDeploy);
//UpdateGridSelectionScales(deployedGrid, _selectedDeployedToReplace);

//    }


//    private List<int> GatherIds(bool deployed, bool unlockedOnly)
//    {
//        var list = new List<int>();
//        foreach (var def in _db.Units)
//        {
//            if (!def) continue;
//            if (unlockedOnly && !_player.IsUnlocked(def.unitId)) continue;
//            if (_player.IsDeployed(def.unitId) == deployed) list.Add(def.unitId);
//        }
//        return list;
//    }

//    private void SpawnCard(RectTransform parent, int unitId, System.Action onClick)
//    {
//        var def = _db.GetById(unitId);
//        if (!def) return;
//        var card = Instantiate(cardPrefab, parent);
//        card.Bind(def, _player.IsUnlocked(unitId), Mathf.Max(1, _player.GetLevel(unitId)), onClick);
//    }


//private void UpdateGridSelectionScales(RectTransform grid, int selectedUnitId)
//{
//    if (!grid) return;

//    for (int i = 0; i < grid.childCount; i++)
//    {
//        var t = grid.GetChild(i);
//        var c = t.GetComponent<UnitCardView>();
//        if (!c) continue;

//        bool isSelected = (c.UnitId == selectedUnitId);

//        // scale visual
//        ScaleCard(t, isSelected ? selectedScale : normalScale);

//        // call the method directly (if your UnitCardView exposes it)
//        // remove this line if you don't want/need a selected state on the card
//        c.SetSelected(isSelected);
//    }
//}

//    private void ScaleCard(Transform card, float targetScale)
//    {
//#if DOTWEEN
//        if (scaleTween > 0f)
//        {
//            card.DOScale(targetScale, scaleTween).SetEase(Ease.OutSine);
//            return;
//        }
//#endif
//        card.localScale = Vector3.one * targetScale;
//    }

//    private Transform FindCardByUnitId(RectTransform grid, int unitId)
//    {
//        if (!grid) return null;
//        for (int i = 0; i < grid.childCount; i++)
//        {
//            var t = grid.GetChild(i);
//            var c = t.GetComponent<UnitCardView>();
//            if (c && c.UnitId == unitId)
//                return t;
//        }
//        return null;
//    }


//    public void RefreshWithOrders(
//    List<int> deployedOrder,
//    List<int> undeployedOrder,
//    UnitsDatabaseSO db,
//    PlayerUnitsModel player,
//    int candidateToDeploy,
//    int deployedToReplace)
//    {
//        _db = db;
//        _player = player;

//        Clear(deployedGrid);
//        Clear(undeployedGrid);

//        // Build Deployed exactly in deployedOrder
//        foreach (var id in deployedOrder)
//        {
//            if (!_player.IsUnlocked(id)) continue;
//            if (!_player.IsDeployed(id)) continue;

//            var def = _db.GetById(id);
//            if (!def) continue;

//            int lvl = Mathf.Max(1, _player.GetLevel(id));
//            var card = Instantiate(cardPrefab, deployedGrid);
//            card.Bind(def, true, lvl, onClick: () =>
//            {
//                _selectedDeployedToReplace = id;
//                _selectedDeployedIndex = card.transform.GetSiblingIndex();
//                UpdateGridSelectionScales(deployedGrid, id);
//            });
//        }

//        // Build Undeployed exactly in undeployedOrder
//        foreach (var id in undeployedOrder)
//        {
//            if (!_player.IsUnlocked(id)) continue;
//            if (_player.IsDeployed(id)) continue;

//            var def = _db.GetById(id);
//            if (!def) continue;

//            int lvl = Mathf.Max(1, _player.GetLevel(id));
//            var card = Instantiate(cardPrefab, undeployedGrid);
//            card.Bind(def, true, lvl, onClick: () =>
//            {
//                _candidateToDeploy = id;
//                _selectedUndeployedIndex = card.transform.GetSiblingIndex();
//                UpdateGridSelectionScales(undeployedGrid, id);
//            });
//        }

//        // selections + scales
//        _candidateToDeploy = candidateToDeploy;
//        _selectedDeployedToReplace = deployedToReplace;

//        var candT = FindCardByUnitId(undeployedGrid, _candidateToDeploy);
//        _selectedUndeployedIndex = candT ? candT.GetSiblingIndex() : -1;

//        var repT = FindCardByUnitId(deployedGrid, _selectedDeployedToReplace);
//        _selectedDeployedIndex = repT ? repT.GetSiblingIndex() : -1;

//        UpdateGridSelectionScales(undeployedGrid, _candidateToDeploy);
//        UpdateGridSelectionScales(deployedGrid, _selectedDeployedToReplace);
//    }

//}
