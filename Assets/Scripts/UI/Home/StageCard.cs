using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StageCard : MonoBehaviour
{
    [Header("Identity")]
    public int stageIndex;     // 0-based

    [Header("Lock UI")]
    public GameObject lockOverlay;   // optional dim/lock group

    [Header("Section Images (one Image per section)")]
    [Tooltip("Section 1: 'STAGE CLEAR' (1 star graphic)")]
    public Image section1Image;

    [Tooltip("Section 2: '50% HP CLEAR' (2 stars graphic)")]
    public Image section2Image;

    [Tooltip("Section 3: '100% HP CLEAR' (3 stars graphic)")]
    public Image section3Image;

    [Header("Section 1 Sprites")]
    public Sprite sec1Empty;   // empty/grey for section 1
    public Sprite sec1Gold;    // gold/filled for section 1

    [Header("Section 2 Sprites")]
    public Sprite sec2Empty;   // empty/grey for section 2
    public Sprite sec2Gold;    // gold/filled for section 2

    [Header("Section 3 Sprites")]
    public Sprite sec3Empty;   // empty/grey for section 3
    public Sprite sec3Gold;    // gold/filled for section 3

    [Header("Optional")]
    public TMP_Text titleText;


    [Header("Reward preview UI")]
    [SerializeField] private TMP_Text rewardCoinsText;
    [SerializeField] private TMP_Text rewardGemsText;
    [SerializeField] private TMP_Text rewardXPText;


    public void SetLocked(bool locked)
    {
        if (lockOverlay) lockOverlay.SetActive(locked);
        // design choice: locked still shows empty sprites
    }

    /// stars meaning (exclusive sections):
    /// 0 → not cleared yet: all sections show their Empty sprite
    /// 1 → section 1 uses Gold, sections 2/3 use Empty
    /// 2 → section 2 uses Gold, sections 1/3 use Empty
    /// 3 → section 3 uses Gold, sections 1/2 use Empty
    public void SetStars1(int stars)
    {
        stars = Mathf.Clamp(stars, 0, 3);

        // default everything to Empty
        SetSectionSprite(section1Image, sec1Empty);
        SetSectionSprite(section2Image, sec2Empty);
        SetSectionSprite(section3Image, sec3Empty);

        // flip only the active section to Gold
        switch (stars)
        {
            case 1:
                SetSectionSprite(section1Image, sec1Gold);
                break;
            case 2:
                SetSectionSprite(section2Image, sec2Gold);
                break;
            case 3:
                SetSectionSprite(section3Image, sec3Gold);
                break;
        }
    }
    public void SetStars(int stars)
    {
        stars = Mathf.Clamp(stars, 0, 3);

        // default: all empty
        SetSectionSprite(section1Image, sec1Empty);
        SetSectionSprite(section2Image, sec2Empty);
        SetSectionSprite(section3Image, sec3Empty);

        // cumulative gold
        if (stars >= 1) SetSectionSprite(section1Image, sec1Gold);
        if (stars >= 2) SetSectionSprite(section2Image, sec2Gold);
        if (stars >= 3) SetSectionSprite(section3Image, sec3Gold);
    }

    public void SetRewardPreview(int coins, int gems, int xp)
    {
        if (rewardCoinsText) rewardCoinsText.text = coins.ToString();
        if (rewardGemsText) rewardGemsText.text = gems.ToString();
        if (rewardXPText) rewardXPText.text = xp.ToString();
    }
    public void SetTitle(string text)
    {
        if (titleText) titleText.text = text;
    }

    // helpers
    private static void SetSectionSprite(Image img, Sprite sprite)
    {
        if (!img) return;
        img.enabled = true;
        img.sprite = sprite;
    }
}
