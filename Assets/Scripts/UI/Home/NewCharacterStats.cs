using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class NewCharacterStats : MonoBehaviour
{

    private int currentUnitId;
    [Header("Header UI")]
    private GameObject currentVisualInstance;


    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;



    [Header("Stats UI (Current / +Delta)")]
    [SerializeField] private TMP_Text hpText;

    [SerializeField] private TMP_Text atkText;

    [SerializeField] private TMP_Text defText;



    [Header("Stats Popup")]

    // Buttons
    [SerializeField] private UnityEngine.UI.Button burgerButton;   // the small “menu” button
    [SerializeField] private UnityEngine.UI.Button claimButton;   // the small “menu” button
    [SerializeField] private UnityEngine.UI.Button popupCloseBtn;  // close at bottom of popup

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


   

    void Awake()
    {        
            // Wire once
        if (claimButton)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimClicked);
        }
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


    }

    public void ShowStatsPopup()
    {
        if (statsPopupRoot && !statsPopupRoot.activeSelf)
            statsPopupRoot.SetActive(true);
        // The Detail screen stays active underneath — this is just an overlay.
    }

    public void HideStatsPopup()
    {
        if (statsPopupRoot && statsPopupRoot.activeSelf)
            statsPopupRoot.SetActive(false);
    }


    /// <summary>
    /// Sets the header area (portrait, display name, and Level).
    /// </summary>

    public void SetHeader(string displayName, Sprite portrait, int level)
    {
        if (portraitImage) portraitImage.sprite = portrait;
        if (nameText) nameText.text = displayName ?? "-";
        if (levelText) levelText.text = $"LVL. {Mathf.Max(1, level)}";

    }

        // this is for Main Screen Stats( HP , Attack,Deffence)
        public void SetStatRows(float hpCurrent,float atkCurrent,  float defCurrent )
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




    // Compact 12_300 -> "12.3K", 1_250_000 -> "1.25M"
    private static string Compact(int v)
    {
        if (v >= 1_000_000) return (v / 1_000_000f).ToString("0.##") + "M";
        if (v >= 1_000) return (v / 1_000f).ToString("0.##") + "K";
        return v.ToString();
    }

    // Call this from UnitsPanelController right after it refreshes the detail panel.
    // THIS SHOWS THE STATS FOR HP. ATTACK , DEFFENCE...  INSIDE THE STATS PANEL AFTER CLICKING THE BURGER BUTTON !!!!!!
    public void RefreshStatsPopupData(string unitName, float cpNow, float cpDelta, UnitStatsRuntime current, UnitStatsRuntime next)
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


    public void Show1(int unitId)
    {
        currentUnitId = unitId;


        gameObject.SetActive(true);
    }
    public void Show(int unitId)
    {
        currentUnitId = unitId;

        var db = GameStartManager.Instance.unitsDatabase;
        if (db == null)
        {
            Debug.LogWarning("NewCharacterStats.Show: unitsDatabase is null.");
            gameObject.SetActive(true);
            return;
        }

        // You must have a way to get definition by id (recommended). If you don't,
        // we will scan db.Units.
        UnitDefinitionSO def = null;
        foreach (var u in db.Units)
        {
            if (u != null && u.unitId == unitId)
            {
                def = u;
                break;
            }
        }

        if (def == null)
        {
            Debug.LogWarning($"NewCharacterStats.Show: UnitDefinition not found for id={unitId}");
            gameObject.SetActive(true);
            return;
        }

        // Level shown in header:
        // If you have per-unit level in PlayerUnits, use it. Otherwise show 1.
        int unitLevel = 1;
        var playerUnits = GameStartManager.Instance.PlayerUnits;
        if (playerUnits != null)
            unitLevel = Mathf.Max(1, playerUnits.GetLevel(unitId));

        // Build runtime stats for display (HP/ATK/DEF)
        // We use the same math you use elsewhere: base SO + progression curves.
        // If you already have a helper/service that builds UnitStatsRuntime, use that instead.
        var runtime = new UnitStatsRuntime();
        runtime.FromSO(def.baseStats); // adjust name if your SO field differs

        var prog = GameStartManager.Instance.progressionConfig;
        if (prog != null)
        {
            var g = ProgressionMath.GetGrowthMultipliers(unitLevel, prog);
            runtime.attack *= g.gA;
            runtime.maxHP *= g.gH;
            runtime.moveSpeed *= g.gMv;
            runtime.attackSpeed *= g.gAS;
        }

        // Header + main stat rows
        SetHeader(def.displayName, def.portrait, unitLevel);
        SetStatRows(runtime.maxHP, runtime.attack, runtime.defense);

        gameObject.SetActive(true);
    }


    public void OnClaimClicked()
    {

        ClaimCharacter(currentUnitId);


        gameObject.SetActive(false);
    }
    public void ClaimCharacter(int unitId)
    {
        var service = GameStartManager.Instance.ProgressionService;
        if (service == null)
            return;

        service.UnlockUnit(unitId);
    }


}
