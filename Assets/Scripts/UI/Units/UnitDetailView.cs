using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;


/// <summary>
/// Detail panel for the selected unit:
/// - Header: portrait, name, Level
/// - Stat rows: current value and green +Δ (next - current)
/// 
/// This is a pure View. It does NO calculations—values are provided by the controller.
/// Attach to the Detail panel GameObject.
/// </summary>
public class UnitDetailView : MonoBehaviour
{
    // === Stats Popup (overlay) ===
    [SerializeField] private GameObject statsPopupRoot;     // whole popup panel (inactive by default)
    [SerializeField] private TMPro.TMP_Text popupHeader;    // e.g., "WARRIOR STATS"

    // Row values (current)
    [SerializeField] private TMPro.TMP_Text popupCP;
    [SerializeField] private TMPro.TMP_Text popupHP;
    [SerializeField] private TMPro.TMP_Text popupATK;
    [SerializeField] private TMPro.TMP_Text popupDEF;
    [SerializeField] private TMPro.TMP_Text popupMove;
    [SerializeField] private TMPro.TMP_Text popupAtkSpd;
    [SerializeField] private TMPro.TMP_Text popupRange;

    // Row deltas (green +X when upgrading). Optional—leave null if you don’t want deltas.
    [SerializeField] private TMPro.TMP_Text popupCPDelta;
    [SerializeField] private TMPro.TMP_Text popupHPDelta;
    [SerializeField] private TMPro.TMP_Text popupATKDelta;
    [SerializeField] private TMPro.TMP_Text popupDEFDelta;
    [SerializeField] private TMPro.TMP_Text popupMoveDelta;
    [SerializeField] private TMPro.TMP_Text popupAtkSpdDelta;
    [SerializeField] private TMPro.TMP_Text popupRangeDelta;

    // Buttons
    [SerializeField] private UnityEngine.UI.Button burgerButton;   // the small “menu” button
    [SerializeField] private UnityEngine.UI.Button popupCloseBtn;  // close at bottom of popup
    [SerializeField] private UnityEngine.UI.Button gearBtn; 
    [SerializeField] private UnityEngine.UI.Button STartUpBtn; 
    [SerializeField] private UnityEngine.UI.Button skinBtn;
    [SerializeField] private RectTransform gearToastAnchor;
    [SerializeField] private RectTransform starUpToastAnchor;
    [SerializeField] private RectTransform skinToastAnchor;



    [Header("Header UI")]
    [SerializeField] private RectTransform visualRoot;   // empty UI container
    private GameObject currentVisualInstance;

    // --- Upgrade FX hooks -----------------------------------------------
    // Read-only views of what UnitUpgradeFx anchors to: the hero (for the light
    // pillar) and the four stat texts (for the stats-row flash). Getters only.
    public RectTransform VisualRoot => visualRoot;
    public GameObject CurrentVisual => currentVisualInstance;
    public TMP_Text LevelText => levelText;
    public TMP_Text HpText => hpText;
    public TMP_Text AtkText => atkText;
    public TMP_Text DefText => defText;

    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;

    [SerializeField] private TMP_Text cpText;

    // ---- The two "cost / owned" rows under the stats block -------------------
    // Each row is a "Resources-frame" holding an icon + TWO texts: the green one
    // on the left is the COST, the white one on the right is "/ owned". The
    // SLASH IS WRITTEN BY CODE, never authored in the scene - a scene-authored
    // slash is what produced the "1/6/200" bug (see NOTES in the doc).
    [SerializeField] private TMP_Text xpText;            // hero XP row, green = cost
    [SerializeField] private TMP_Text xpTextOwned;       // hero XP row, white = "/ owned"
    [SerializeField] private TMP_Text coinsText;         // coin row,    white = "/ owned"
    [SerializeField] private TMP_Text coinsTextDelta;    // coin row,    green = cost

    [Tooltip("Optional. The whole coin row, hidden by SetCostRowsVisible(false). " +
             "Left empty, the row is taken to be coinsText's parent.")]
    [SerializeField] private GameObject coinsRow;

    [Tooltip("Optional. The whole hero-XP row, hidden by SetCostRowsVisible(false). " +
             "Left empty, the row is taken to be xpText's parent.")]
    [SerializeField] private GameObject heroXpRow;


    [Header("Stats UI (Current / +Delta)")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text hpDeltaText;

    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text atkDeltaText;

    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text defDeltaText;

    [SerializeField] private TMP_Text atkSpdText;
    [SerializeField] private TMP_Text atkSpdDeltaText;

    [SerializeField] private TMP_Text moveSpdText;
    [SerializeField] private TMP_Text moveSpdDeltaText;

    [SerializeField] private TMP_Text rangeText;
    [SerializeField] private TMP_Text rangeDeltaText;


    [SerializeField] private TMP_Text requirementDetailText;  // lives under your Locked Message panel


    [Header("Formatting")]
    [Tooltip("Number of decimals for continuous stats (damage, speeds, range).")]
    [Range(0, 3)] public int decimals = 2;

    [Tooltip("Color used for +Δ labels.")]
    public Color deltaColor = new Color(0.2f, 0.85f, 0.2f); // green
 


    [Header("Tooltip")]
    [SerializeField] private GameObject tooltipRoot;  // The tooltip panel (inactive by default)
    [SerializeField] private UnityEngine.UI.Button tooltipButton;  // The button that triggers show
    [SerializeField] private UnityEngine.UI.Button hideOnClickButton;  // Full-screen transparent button for "click anywhere" hide (parented to tooltipRoot)
    [SerializeField] private float tooltipFadeDuration = 0.3f;  // Fade in/out time
    [SerializeField] private Ease tooltipFadeEase = Ease.OutQuad;  // Smooth easing for fade

    [Header("Locked Stage Toast Settings && Upgrade Button Disable")]
    [SerializeField] private RectTransform toastCanvasParent; // usually the Detail screen root
    [SerializeField] private RectTransform toastAnchor;       // place this above the button    
    [SerializeField] private string notEnoughText = "NOT ENOUGH RESOURCES";
    [SerializeField] private string commingSoonText = "Comming Soon..";
    [SerializeField] private GameObject lockedToastPrefab;
    [SerializeField] private Color toastStartColor = Color.white;
    [SerializeField] private Color toastGrayColor = Color.gray;
    [SerializeField] private float toastStartYOffset = 0f;
    [SerializeField] private float toastRisePixels = 40f;
    [SerializeField] private float toastRiseDuration = 2f;
    [SerializeField] private Ease toastRiseEase = Ease.OutQuad;
    [SerializeField] private float toastFadeInDuration = 0.13f;
    [SerializeField, Range(0f, 1f)] private float toastGrayAt = 0.75f;
    [SerializeField, Range(0f, 1f)] private float toastFadeOutAt = 0.8f;
    private float toastTextSize = 25f;  // Font size for toast text
    [SerializeField] private float DesigeredTextSize = 40f;  // Font size for toast text
    public RectTransform ToastAnchor => toastAnchor;

    void Awake()
    {
        // Wire once
        if (burgerButton)
        {
            burgerButton.onClick.RemoveAllListeners();
            burgerButton.onClick.AddListener(ShowStatsPopup);
        }
        if (popupCloseBtn)
        {
            popupCloseBtn.onClick.RemoveAllListeners();
            popupCloseBtn.onClick.AddListener(HideStatsPopup);
        }

        if (statsPopupRoot) statsPopupRoot.SetActive(false);

        // wire Tooltip
        // Wire tooltip button to show
        if (tooltipButton)
        {
            tooltipButton.onClick.RemoveAllListeners();
            tooltipButton.onClick.AddListener(ShowTooltip);
        }

        // Wire full-screen hide button (assumes it's a child of tooltipRoot, covering the screen)
        if (hideOnClickButton)
        {
            hideOnClickButton.onClick.RemoveAllListeners();
            hideOnClickButton.onClick.AddListener(HideTooltip);
        }

        if (gearBtn)
        {
            gearBtn.onClick.RemoveAllListeners();
            gearBtn.onClick.AddListener(ShowGearToast);
        }
        if (STartUpBtn)
        {
            STartUpBtn.onClick.RemoveAllListeners();
            STartUpBtn.onClick.AddListener(ShowStarUpToast);
        }
        if (skinBtn)
        {
            skinBtn.onClick.RemoveAllListeners();
            skinBtn.onClick.AddListener(ShowSkinToast);
        }


        // Ensure tooltip starts inactive
        if (tooltipRoot) tooltipRoot.SetActive(false);
    }

    public void ShowStatsPopup()
    {
        if (statsPopupRoot && !statsPopupRoot.activeSelf)
        {
            if (currentVisualInstance !=null)
            {
                currentVisualInstance.SetActive(false);
            }
            statsPopupRoot.SetActive(true);

        }
        // The Detail screen stays active underneath — this is just an overlay.
    }

    public void HideStatsPopup()
    {
        if (statsPopupRoot && statsPopupRoot.activeSelf)
        {
            if (currentVisualInstance != null)
            {
                currentVisualInstance.SetActive(true);
                statsPopupRoot.SetActive(false);
            }
        }
    }
    public void ShowTooltip()
    {
        if (!tooltipRoot) return;

        // Activate and ensure CanvasGroup for fading
        tooltipRoot.SetActive(true);
        var cg = tooltipRoot.GetComponent<CanvasGroup>();
        if (!cg) cg = tooltipRoot.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;  // Start invisible
        cg.interactable = true;  // Enable interactions (e.g., hide button)
        cg.blocksRaycasts = true;

        // Smooth fade in
        cg.DOFade(1f, tooltipFadeDuration).SetEase(tooltipFadeEase);
    }

    public void HideTooltip()
    {
        if (!tooltipRoot || !tooltipRoot.activeSelf) return;

        var cg = tooltipRoot.GetComponent<CanvasGroup>();
        if (!cg) return;

        // Smooth fade out, then deactivate
        cg.DOFade(0f, tooltipFadeDuration).SetEase(tooltipFadeEase).OnComplete(() =>
        {
            tooltipRoot.SetActive(false);
            cg.alpha = 1f;  // Reset for next show
            cg.interactable = false;
            cg.blocksRaycasts = false;
        });
    }


    /// <summary>
    /// Sets the header area (portrait, display name, and Level).
    /// </summary>
    public void SetHeader1(string displayName, Sprite portrait, int level)
    {
        if (portraitImage) portraitImage.sprite = portrait;
        if (nameText) nameText.text = displayName ?? "-";
        if (levelText) levelText.text = $"LVL. {Mathf.Max(1, level)}";
    }

    public void SetHeader(UnitDefinitionSO unitDef, int level)
    {
        // Texts
        if (nameText) nameText.text = unitDef.displayName ?? "-";
        if (levelText) levelText.text = $"LVL. {Mathf.Max(1, level)}";

        // Remove old visual
        if (currentVisualInstance)
            Destroy(currentVisualInstance);

        if (!unitDef.visualPrefab || !visualRoot)
            return;

        // Instantiate visual
        currentVisualInstance = Instantiate(unitDef.visualPrefab, visualRoot);

        var t = currentVisualInstance.transform;
        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = unitDef.uiVisualScale;

        // Force idle animation
        var animator = currentVisualInstance.GetComponentInChildren<Animator>();
        if (animator)
        {
            animator.Update(0f);
            animator.Play("Idle", 0, 0f);
        }
    }

    // this is for Main Screen Stats( HP , Attack,Deffence)
    public void SetStatRows(
        float hpCurrent, float hpDelta,
        float atkCurrent, float atkDelta,
        float defCurrent, float defDelta,
        float asCurrent, float asDelta,
        float mvCurrent, float mvDelta,
        float rgCurrent, float rgDelta
        )
    {
        // Current values
        SetNumber(hpText, hpCurrent, 0);            // HP looks cleaner as integer
        SetNumber(atkText, atkCurrent, 0);
        SetNumber(defText, defCurrent, 0);

    }

    // ---------- Helpers ----------

    private void SetNumber(TMP_Text label, float value, int dec)
    {
        if (!label) return;
        if (dec <= 0)
            label.text = Mathf.RoundToInt(value).ToString();
        else
            label.text = value.ToString($"F{dec}");
    }

    private void SetDelta(TMP_Text label, float delta, int dec)
    {
        if (!label) return;

        // Treat tiny deltas as zero to avoid visual noise
        const float EPS = 0.0005f;
        bool show = Mathf.Abs(delta) > EPS;

        if (!show)
        {
            label.text = "";
            return;
        }

        string s;
        if (dec <= 0) s = $"+{Mathf.RoundToInt(delta)}";
        else s = $"+{delta.ToString($"F{dec}")}";

        label.text = s;
        label.color = deltaColor;
    }

    /// <summary>
    /// The `xp` argument is IGNORED and kept only so the existing call sites
    /// compile. It used to write xpText, which SetHeroXpCost then overwrote a
    /// line later - two writers on one label, and the locked-unit path (which
    /// calls SetMeta but not SetHeroXpCost) was left showing SetMeta's value
    /// next to a scene-authored "/200". SetHeroXpCost is now the only writer.
    /// </summary>
    public void SetMeta(int cp, int coins, int xp)
    {
        if (cpText) cpText.text = cp.ToString();
    }

    // Compact 12_300 -> "12.3K", 1_250_000 -> "1.25M". Anything up to 999 is
    // printed in full, which is the rule Arash asked for (2026-09-22).
    private static string Compact(int v)
    {
        if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.##") + "M";
        if (v >= 1_000) return (v / 1_000f).ToString("0.##") + "K";
        return v.ToString();
    }

    public void SetCoinsCost(int costNeeded, int coinsOwned)
    {
        if (coinsTextDelta) coinsTextDelta.text = Compact(costNeeded);
        if (coinsText) coinsText.text = $"/ {Compact(coinsOwned)}";
    }

    public void SetHeroXpCost(int costNeeded, int xpOwned)
    {
        if (xpText) xpText.text = Compact(costNeeded);

        var owned = ResolveXpOwnedText();
        if (owned) owned.text = $"/ {Compact(xpOwned)}";
    }

    /// <summary>
    /// Shows or hides BOTH cost rows (icon included). Used by the Unachieved
    /// panel, where a locked hero cannot be upgraded so the numbers are
    /// meaningless - it was previously printing a cost of 0.
    /// </summary>
    public void SetCostRowsVisible(bool visible)
    {
        var coins = coinsRow ? coinsRow : RowOf(coinsText);
        var xp = heroXpRow ? heroXpRow : RowOf(xpText);

        if (coins) coins.SetActive(visible);
        if (xp) xp.SetActive(visible);
    }

    private static GameObject RowOf(TMP_Text label)
        => label && label.transform.parent ? label.transform.parent.gameObject : null;

    /// <summary>
    /// The white "/ owned" half of the hero-XP row. The coin row has always had
    /// both halves wired; the XP row only ever had the green half, so its white
    /// label kept whatever placeholder the scene was authored with ("/200") and
    /// printed it after the code's own text. Falling back to the sibling means
    /// the fix needs no scene edit, and an explicit xpTextOwned still wins.
    /// </summary>
    private TMP_Text ResolveXpOwnedText()
    {
        if (xpTextOwned) return xpTextOwned;
        if (!xpText || !xpText.transform.parent) return null;

        foreach (Transform sibling in xpText.transform.parent)
        {
            if (sibling == xpText.transform) continue;

            var label = sibling.GetComponent<TMP_Text>();
            if (label) { xpTextOwned = label; return label; }
        }
        return null;
    }



    public void SetRequirementDetail(string text)
    {
        if (requirementDetailText) requirementDetailText.text = text ?? "";
    }

   
    // Call this from UnitsPanelController right after it refreshes the detail panel.
    // THIS SHOWS THE STATS FOR HP. ATTACK , DEFFENCE...  INSIDE THE STATS PANEL AFTER CLICKING THE BURGER BUTTON !!!!!!
    public void RefreshStatsPopupData(string unitName,float cpNow, float cpDelta,UnitStatsRuntime current,   UnitStatsRuntime next  )
    { 
        if (popupHeader) popupHeader.text = $"{unitName} ";

        if (popupCP) popupCP.text = Mathf.RoundToInt(cpNow).ToString();
        if (popupCPDelta) popupCPDelta.text = cpDelta > 0 ? $"+{Mathf.RoundToInt(cpDelta)}" : "0";

        if (popupHP) popupHP.text = current.maxHP.ToString("0.##");
        if (popupHPDelta) popupHPDelta.text = DeltaText(next.maxHP - current.maxHP);

        if (popupATK) popupATK.text = current.attack.ToString("0.##");
        if (popupATKDelta) popupATKDelta.text = DeltaText(next.attack - current.attack);

        if (popupDEF) popupDEF.text = current.defense.ToString("0.##");
        if (popupDEFDelta) popupDEFDelta.text = DeltaText(next.defense - current.defense);

        if (popupMove) popupMove.text = current.moveSpeed.ToString("0.##");
        if (popupMoveDelta) popupMoveDelta.text = DeltaText(next.moveSpeed - current.moveSpeed);

        if (popupAtkSpd) popupAtkSpd.text = current.attackSpeed.ToString("0.##");
        if (popupAtkSpdDelta) popupAtkSpdDelta.text = DeltaText(next.attackSpeed - current.attackSpeed);

        if (popupRange) popupRange.text = current.attackRange.ToString("0.##");
        if (popupRangeDelta) popupRangeDelta.text = DeltaText(next.attackRange - current.attackRange);
    }

    private static string DeltaText(float d)
    {
        if (d <= 0.0001f) return "0";
        if (d < 1f) return $"+{d:0.##}";
        return $"+{Mathf.RoundToInt(d)}";
    }


    public void ShowLockedStageToast(RectTransform anchorParent , float textSize , string textToDisplay)
    {
        if (!lockedToastPrefab) return;

        // Parent & anchor (top of the button area)
        RectTransform parent = toastCanvasParent ? toastCanvasParent : (RectTransform)transform;
        RectTransform baseAnchor = anchorParent ? anchorParent : parent;

        // Spawn
        var go = Instantiate(lockedToastPrefab, baseAnchor);
        var rt = go.GetComponent<RectTransform>();
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) cg = go.AddComponent<CanvasGroup>();

        // Text reference(s); set starting color
        var tmps = go.GetComponentsInChildren<TMPro.TMP_Text>(true);
        foreach (var t in tmps)
        {
            t.color = toastStartColor;
            t.fontSize = textSize;
        }
        if (tmps.Length > 0) tmps[0].text = textToDisplay; // keep your existing text

        // Place at top-center of anchor, pivot at bottom so it rises “up”
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, toastStartYOffset);

        // Motion parameters (kept from your previous version)
        float rise = Mathf.Max(0f, toastRisePixels);
        float duration = Mathf.Max(0.01f, toastRiseDuration);

        // Clamp the visual events
        float grayPct = Mathf.Clamp01(toastGrayAt);
        float fadePct = Mathf.Clamp01(toastFadeOutAt);
        if (grayPct > fadePct) grayPct = fadePct; // ensure gray happens before/at fade start

        float grayTime = duration * grayPct;
        float fadeTime = duration * (1f - fadePct);
        if (fadeTime < 0.06f) fadeTime = 0.06f;   // tiny guard so fade is visible

        // Build sequence:
        cg.alpha = 0f;

        var seq = DOTween.Sequence();

        // 1) quick pop-in
        seq.Append(cg.DOFade(1f, toastFadeInDuration));

        // 2) rise upward over full duration (ease unchanged from your last logic)
        seq.Join(rt.DOAnchorPosY(rt.anchoredPosition.y + rise, duration)
                 .SetEase(toastRiseEase));

        // 3) at 75% of travel, flip text color to gray
        seq.InsertCallback(grayTime, () =>
        {
            foreach (var t in tmps) t.color = toastGrayColor;
        });

        // 4) from 90% of travel to the end, fade out to 0
        seq.Insert(duration * fadePct, cg.DOFade(0f, fadeTime));

        // 5) cleanup
        seq.OnComplete(() => Destroy(go));
    }

    public void PlayNotEnoughResourcesToastStacked(RectTransform anchorParent, int costRequired, int gemsOwned)
    {
        if (!lockedToastPrefab) return;

        // 3 layers: main + 2 echoes
        const int LAYERS = 3;
        float[] alphas = { 1.0f, 0.6f, 0.35f };
        Vector2[] layerOffsets =   // small offsets so echoes are visible
        {
        Vector2.zero,
        new Vector2(1.5f, -1.5f),
        new Vector2(-1.5f, 1.5f)
    };

        for (int i = 0; i < LAYERS; i++)
        {
            // Call ShowLockedStageToast for each layer with custom params (no shake, uses rise/fade logic)

            ShowLockedStageToast(anchorParent , DesigeredTextSize , notEnoughText);
        }
    }

    private void ShowGearToast()
    {
        ShowLockedStageToast(gearToastAnchor, DesigeredTextSize, commingSoonText);
    }
    private void ShowStarUpToast()
    {
        ShowLockedStageToast(starUpToastAnchor, DesigeredTextSize, commingSoonText);
    }
    private void ShowSkinToast()
    {
        ShowLockedStageToast(skinToastAnchor, DesigeredTextSize, commingSoonText);
    }


}
