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
    private bool _isClosing = false;   // prevents refresh/rebuild while we�re closing

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

            gameObject.SetActive(false);
            _onClose?.Invoke();
            return;
        }

        // still guard against missing candidate
        if (_candidateUndeployedId < 0)
            return;

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

            // rotate around Z between -15� and +15� endlessly, no visible pause
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



//// The first edition in which selecting the deploy and undeploy happens when we  click on savbe












































//// --- NEW: always compute the current indices so they are not -1 ---



//// Initial visual selection (scale) for both lists
