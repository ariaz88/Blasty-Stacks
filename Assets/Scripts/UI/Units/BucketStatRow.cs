using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;   // <- add this

public class BucketStatRow : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text atkText;
    [SerializeField] private TMP_Text defText;
    [SerializeField] private TMP_Text cpText;
    [SerializeField] private TMP_Text coinsText; // or gemsText
    public GameObject lineBetweenRows; // assign your Image here

    // NEW: the button in the row (UI_Player-Icon_Cell-btn)
    [SerializeField] private Button iconButton;

    // NEW: data this row represents
    private int _unitId;
    private BucketHeader.BucketType _bucketType;
    private Action<int, BucketHeader.BucketType> _onClicked;

    public void SetDividerVisible(bool visible)
    {
        if (lineBetweenRows) lineBetweenRows.SetActive(visible);
    }

    public void Bind(Sprite icon, string displayName, int level,
                     int hp, int atk, int def, int cp, int coins)
    {
        if (portrait) portrait.sprite = icon;
        if (nameText) nameText.text = displayName;
        if (levelText) levelText.text = $"{level}";
        if (hpText) hpText.text = hp.ToString("N0");
        if (atkText) atkText.text = atk.ToString("N0");
        if (defText) defText.text = def.ToString("N0");
        if (cpText) cpText.text = cp.ToString("N0");
        if (coinsText) coinsText.text = coins.ToString("N0");
    }

    // NEW: called right after Bind from UnitsPanelController

    // Now each row knows:
    // Which unitId it is showing.
    // Which bucket type it came from. 
    // Which callback it should call when clicked.
    public void Initialize(
        int unitId,
        BucketHeader.BucketType bucketType,
        Action<int, BucketHeader.BucketType> onClicked)
    {
        _unitId = unitId;
        _bucketType = bucketType;
        _onClicked = onClicked;

        if (iconButton != null)
        {
            iconButton.onClick.RemoveAllListeners();
            iconButton.onClick.AddListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        Debug.Log($"[BucketStatRow {gameObject.name}] Clicked unitId {_unitId} in {_bucketType}");  // Log per row
        _onClicked?.Invoke(_unitId, _bucketType);
        if (_onClicked == null) Debug.LogError($"[BucketStatRow {gameObject.name}] _onClicked is null!");
    }
}
