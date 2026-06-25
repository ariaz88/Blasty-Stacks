using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;


public class UnitCardView : MonoBehaviour
{
    [Header("Upgrade Cue")]
    [SerializeField] private GameObject upgradeArrow;   // a small green arrow image
    private Tween upgradeTween;
    private Vector3 upgradeArrowBasePos;

    [Header("UI Refs (assign in Prefab)")]
    [SerializeField] private TMP_Text levelText;            // "Lvl Num"
    [SerializeField] private TMP_Text stageValueText;   // new: shows "1_3"

    [SerializeField] private Image portraitImage;           // "ProfileImage"
    [SerializeField] private GameObject selectedHighlight;     // full green overlay (DeployOverlay: chosen deployed)
    [SerializeField] private Image bgHighlighted;           // "BGImage-Highlighted"
    [SerializeField] private Image bgGrayOut;               // "BGImage-GrayOut"
    [SerializeField] private Image BGNormal;               // "BGImage-GrayOut"
    [SerializeField] private Button clickButton;            // "Button"
    [SerializeField] private TMP_Text requirementText;   // optional badge under card (e.g., "Stage 1-16")

    [Header("Behavior")]
    [Tooltip("If true, card still sends clicks when locked (so detail screen can show 'Locked').")]
    [SerializeField] private bool allowClickWhenLocked = true;

    // Public identity
    public int UnitId { get; private set; }

    // Fired when the card changes its own lock state at runtime (debug)
    public event Action<int, bool> OnUnlockStateChanged;

    // Internal state
    private bool _unlocked;
    private bool _selected;
    private Action _onClick;


    private void Awake()
    {
        if (!clickButton) clickButton = GetComponentInChildren<Button>(true);
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(false);
        if (selectedHighlight) selectedHighlight.SetActive(false);
        if (upgradeArrow)
        {
            upgradeArrowBasePos = upgradeArrow.transform.localPosition;
            upgradeArrow.SetActive(false);   // start hidden regardless of prefab
        }
    }
    private void OnDisable()
    {
        // safety: stop tween if card goes inactive
        if (upgradeTween != null) upgradeTween.Pause();
    }
    /// <summary>Bind visuals and wire click.</summary>
    public void Bind(UnitDefinitionSO def, bool unlocked, int level, Action onClick)
    {
        if (!def)
        {
            Debug.LogWarning("[UnitCardView] Bind called with null UnitDefinitionSO.");
            return;
        }

        UnitId = def.unitId;
        _unlocked = unlocked;
        _onClick = onClick;

        if (portraitImage) portraitImage.sprite = def.portrait;
        SetLevelText(level);

        SetLockAwareHeader(def, unlocked, level); // ADDED


        _selected = false;
        ApplyVisuals();

        if (selectedHighlight) selectedHighlight.SetActive(false);

        if (clickButton)
        {
            clickButton.onClick.RemoveAllListeners();
            clickButton.onClick.AddListener(HandleClick);
            clickButton.interactable = true; // stay clickable when locked; controller decides behavior
        }
    }

    /// <summary>Update lock badge + level after an upgrade or external change.</summary>
    public void RefreshBadge(bool unlocked, int level)
    {
        _unlocked = unlocked;
        SetLevelText(level);
        ApplyVisuals();
    }

    /// <summary>Selection highlight (and highlighted BG).</summary>
    public void SetSelected1(bool selected)
    {
        _selected = selected;
        if (selectedHighlight) selectedHighlight.SetActive(selected);
        //ApplyVisuals();
        if (bgHighlighted) bgHighlighted.enabled = selected;
        if (BGNormal) BGNormal.enabled = !selected;
    }
    public void SetSelected2(bool selected)
    {
        if (selectedHighlight) selectedHighlight.SetActive(false); // no full overlay on Cards screen
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(selected);
        if (BGNormal) BGNormal.gameObject.SetActive(!selected);
    }
    public void SetSelected(bool selected)
    {
        if (!_unlocked) return;

        if (selectedHighlight) selectedHighlight.SetActive(false);
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(selected);
    }



    private void HandleClick()
    {
        if (!_unlocked && !allowClickWhenLocked) return;
        _onClick?.Invoke();
    }

    private void SetLevelText(int level)
    {
        //if (levelText) levelText.text = $"{Mathf.Max(1, level)}";

        if (levelText)
        {
            //string valueColorHex = ColorUtility.ToHtmlStringRGBA(stageValueColor);

            levelText.text =
                $"<color=#FFFFFF>LVL.</color>\u2009" +
                $"{Mathf.Max(1, level)}";
        }
    }

    private void ApplyVisuals()
    {
        // Backgrounds
        if (BGNormal) BGNormal.gameObject.SetActive(_unlocked);
        if (bgGrayOut) bgGrayOut.gameObject.SetActive(!_unlocked);
        //if (bgHighlighted) bgHighlighted.gameObject.SetActive(_unlocked/* && _selected*/);

        // IMPORTANT: let overlay methods control the border; keep it off by default here
        //if (bgHighlighted) bgHighlighted.enabled = false;






        // Portrait tint (dim when locked)  this code gray outs the BG by code!!!!!!!
        //if (portraitImage)
        //    portraitImage.color = _unlocked ? Color.white : new Color(1f, 1f, 1f, 0.5f);
    }

    /// <summary>
    /// TEST/DEBUG ONLY: set lock/unlock state from the card itself and notify panel.
    /// </summary>
    public void SetUnlocked(bool unlocked, bool notify = true)
    {
        if (_unlocked == unlocked) return;
        _unlocked = unlocked;
        ApplyVisuals();
        if (notify) OnUnlockStateChanged?.Invoke(UnitId, _unlocked);
    }

    // ---------------------------
    // Context Menu (Play Mode OK)
    // ---------------------------

    [ContextMenu("Debug ▸ Unlock")]
    private void DebugUnlock()
    {
        SetUnlocked(true, notify: true);
    }

    [ContextMenu("Debug ▸ Lock")]
    private void DebugLock()
    {
        SetUnlocked(false, notify: true);
    }

    [ContextMenu("Debug ▸ Toggle Unlock")]
    private void DebugToggle()
    {
        SetUnlocked(!_unlocked, notify: true);
    }
    public void SetRequirementText(string text, bool visible)
    {
        if (!requirementText) return;
        requirementText.gameObject.SetActive(visible);
        requirementText.text = text ?? string.Empty;
    }
    public void SetStageUI(int requiredLevelIndex, int requiredStageIndexWithinLevel, bool visible)
    {
        if (stageValueText) stageValueText.gameObject.SetActive(visible);


        if (!visible) return;

       
        //if (stageValueText) stageValueText.text = $"STAGE {requiredLevelIndex}-{requiredStageIndexWithinLevel}";
        if (stageValueText)
        {
            //string valueColorHex = ColorUtility.ToHtmlStringRGBA(stageValueColor);

            stageValueText.text =
                $"<color=#FFFFFF>STAGE</color>\u2009" +
                $"{requiredLevelIndex}-{requiredStageIndexWithinLevel}";
        }

    }

    /// <summary>
    /// Shows level number when unlocked, shows STAGE requirement when locked.
    /// </summary>
    public void SetLockAwareHeader(UnitDefinitionSO def, bool unlocked, int level)
    {
        // Existing level text behavior
        if (levelText) levelText.gameObject.SetActive(unlocked);
        if (unlocked) SetLevelText(level);


        // New stage UI behavior
        if (def != null)
        {
            SetStageUI(def.requiredLevelIndex, def.requiredStageIndexWithinLevel, visible: !unlocked);            
        }
        else
            SetStageUI(1, 1, visible: false);
    }

    public void SetUpgradeCue(bool on)
    {
        if (!upgradeArrow) return;

        if (on)
        {
            if (!upgradeArrow.activeSelf)
            {
                upgradeArrow.SetActive(true);

                // lazy-init base pos once
                if (upgradeTween == null)
                {
                    upgradeArrowBasePos = upgradeArrow.transform.localPosition;

                    // gentle vertical float (no color/scale changes)
                    upgradeTween = upgradeArrow.transform
                        .DOLocalMoveY(upgradeArrowBasePos.y + 6f, 0.6f) // ~6 px up in 0.6s
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .Pause();
                }
            }

            if (upgradeTween != null && !upgradeTween.IsPlaying())
                upgradeTween.Play();
        }
        else
        {
            if (upgradeTween != null && upgradeTween.IsPlaying())
                upgradeTween.Pause();

            if (upgradeArrow.activeSelf)
                upgradeArrow.SetActive(false);

            // reset to base Y (keeps it neat if re-enabled)
            if (upgradeArrow) upgradeArrow.transform.localPosition = upgradeArrowBasePos;
        }
    }


    // === Overlay helpers ===

    // Called for UNDEPLOYED candidate: turn on green border; make sure full overlay is off
    //public void SetOverlayCandidate(bool on)
    //{
    //    //if (selectedHighlight) selectedHighlight.SetActive(false);
    //    if (bgHighlighted) bgHighlighted.enabled = on;
    //    if (BGNormal) BGNormal.enabled = false;
    //}

    //// Called for DEPLOYED targets: turn on full green overlay on ALL deployed cards
    //public void SetDeployedTargetStyle(bool on)
    //{
    //    if (selectedHighlight) selectedHighlight.SetActive(on);
    //    if (bgHighlighted) bgHighlighted.enabled = false; // no border on deployed
    //}

    //// Clear both decorations (used when leaving overlay)
    //public void ClearOverlayMarks()
    //{
    //    if (selectedHighlight) selectedHighlight.SetActive(false);
    //    if (bgHighlighted) bgHighlighted.enabled = false;
    //    if (BGNormal) BGNormal.enabled = true;
    //    if (selectedHighlight) selectedHighlight.SetActive(false);


    //}

    public void SetOverlayCandidate1(bool on)
    {
        if (selectedHighlight) selectedHighlight.SetActive(false); // candidate is NOT full overlay
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(on);
        if (BGNormal) BGNormal.gameObject.SetActive(!on);
    }

    public void SetDeployedTargetStyle1(bool on)
    {
        if (selectedHighlight) selectedHighlight.SetActive(on);    // full green overlay
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(false);
        // keep BGNormal on behind the overlay (looks correct either way)
        if (BGNormal && !BGNormal.gameObject.activeSelf) BGNormal.gameObject.SetActive(true);
    }

    public void ClearOverlayMarks1()
    {
        if (selectedHighlight) selectedHighlight.SetActive(false);
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(false);
        if (BGNormal) BGNormal.gameObject.SetActive(true);
    }

    public void SetOverlayCandidate(bool on)
    {
        if (!_unlocked) return;

        if (selectedHighlight) selectedHighlight.SetActive(false);
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(on);
    }

    public void SetDeployedTargetStyle(bool on)
    {
        // Locked cards must never change background
        if (!_unlocked) return;

        // Full green overlay for deployed units
        if (selectedHighlight) selectedHighlight.SetActive(on);

        // Deployed style never uses border highlight
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(false);
    }

    public void ClearOverlayMarks()
    {
        // Clear all overlay decorations
        if (selectedHighlight) selectedHighlight.SetActive(false);
        if (bgHighlighted) bgHighlighted.gameObject.SetActive(false);

        // IMPORTANT:
        // Do NOT touch BGNormal or bgGrayOut here.
        // ApplyVisuals() is the sole authority for backgrounds.
    }


    //public void DeactiveNormalBG()
    //{
    //    if (BGNormal) BGNormal.gameObject.SetActive(false);

    //}


}

//public class UnitCardView1 : MonoBehaviour
//{
//    [Header("UI Refs (assign in Prefab)")]
//    [SerializeField] private Image portraitImage;
//    [SerializeField] private TMP_Text levelText;
//    [SerializeField] private GameObject lockOverlay;
//    [SerializeField] private Button clickButton;
//    [SerializeField] private GameObject selectedHighlight;

//    /// <summary> The unit this card represents. </summary>
//    public int UnitId { get; private set; }

//    // Internal
//    private bool _unlocked;
//    private Action _onClick;

//    /// <summary>
//    /// Binds this card's visuals to a unit definition and initial state.
//    /// Also wires the click callback (only fires when unlocked).
//    /// </summary>
//    public void Bind(UnitDefinitionSO def, bool unlocked, int level, Action onClick)
//    {
//        if (def == null)
//        {
//            Debug.LogWarning("[UnitCardView] Bind called with null UnitDefinitionSO.");
//            return;
//        }

//        UnitId = def.unitId;
//        _unlocked = unlocked;
//        _onClick = onClick;

//        if (portraitImage) portraitImage.sprite = def.portrait;
//        SetLevelText(level);
//        ApplyLockVisual(unlocked);

//        // Ensure selected highlight starts off
//        if (selectedHighlight) selectedHighlight.SetActive(false);

//        // Wire click
//        if (clickButton)
//        {
//            clickButton.onClick.RemoveAllListeners();
//            clickButton.onClick.AddListener(HandleClick);
//        }
//    }

//    /// <summary>
//    /// Updates only the lock badge and level label (used after upgrades/unlocks).
//    /// </summary>
//    public void RefreshBadge(bool unlocked, int level)
//    {
//        _unlocked = unlocked;
//        ApplyLockVisual(unlocked);
//        SetLevelText(level);
//    }

//    /// <summary>
//    /// Toggles the visual state for "selected" (e.g., a glow frame).
//    /// </summary>
//    public void SetSelected(bool selected)
//    {
//        if (selectedHighlight) selectedHighlight.SetActive(selected);
//    }

//    /// <summary>
//    /// Click handler: only forwards the click if this unit is unlocked.
//    /// (Locked cards are visible but uninteractable.)
//    /// </summary>
//    private void HandleClick()
//    {
//        if (!_unlocked) return;
//        _onClick?.Invoke();
//    }

//    private void SetLevelText(int level)
//    {
//        if (levelText) levelText.text = $"Lv. {Mathf.Max(1, level)}";
//    }

//    private void ApplyLockVisual(bool unlocked)
//    {
//        if (lockOverlay) lockOverlay.SetActive(!unlocked);

//        // Disable the button if locked so it's not interactable
//        if (clickButton) clickButton.interactable = unlocked;

//        // Optional: dim portrait when locked
//        if (portraitImage) portraitImage.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.5f);
//    }


//}
