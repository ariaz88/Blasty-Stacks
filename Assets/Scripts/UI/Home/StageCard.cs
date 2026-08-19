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
                // case 0: already all-empty
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


public class StageCard1 : MonoBehaviour
{
    [Header("Identity")]
    public int stageIndex;                        // 0-based

    [Header("Lock UI")]
    public GameObject lockOverlay;                // optional dim/lock group

    [Header("Sprites")]
    public Sprite goldStarSprite;                 // filled/bright
    public Sprite whiteStarSprite;                // empty/grey

    [Header("Sections (exactly like your layout)")]
    [Tooltip("Section 1: 'stage clear' (1 star)")]
    public Image stageClearStar;                  // exactly 1 Image

    [Tooltip("Section 2: '50% HP CLEAR' (2 stars) left→right")]
    public Image[] hp50Stars;                     // length = 2

    [Tooltip("Section 3: '100% HP CLEAR' (3 stars) left→right")]
    public Image[] hp100Stars;                    // length = 3

    [Header("Optional")]
    public TMPro.TMP_Text titleText;

    public void SetLocked(bool locked)
    {
        if (lockOverlay) lockOverlay.SetActive(locked);
        // design choice: locked still shows all sections as white stars
    }

    /// <summary>
    /// stars = best result for this stage:
    ///   0 → not cleared yet: all sections white
    ///   1 → light ONLY section 1's single star gold
    ///   2 → light ONLY section 2's two stars gold
    ///   3 → light ONLY section 3's three stars gold
    /// </summary>
    public void SetStars(int stars)
    {
        // reset: everything white
        if (stageClearStar) stageClearStar.sprite = whiteStarSprite;
        SetGroup(hp50Stars, 0);
        SetGroup(hp100Stars, 0);

        switch (Mathf.Clamp(stars, 0, 3))
        {
            case 1:
                if (stageClearStar) stageClearStar.sprite = goldStarSprite;
                break;

            case 2:
                SetGroup(hp50Stars, 2);   // both stars gold
                break;

            case 3:
                SetGroup(hp100Stars, 3);  // all three gold
                break;

                // case 0: already white everywhere
        }
    }

    public void SetTitle(string text)
    {
        if (titleText) titleText.text = text;
    }

    // ---- helpers ----
    private void SetGroup(Image[] imgs, int goldCount)
    {
        if (imgs == null) return;
        for (int i = 0; i < imgs.Length; i++)
        {
            if (!imgs[i]) continue;
            imgs[i].sprite = (i < goldCount) ? goldStarSprite : whiteStarSprite;
            imgs[i].enabled = true; // always visible; just swap sprite
        }
    }
}

//public class StageCard1 : MonoBehaviour
//{
//    [Header("Wiring")]
//    public int stageIndex;                 // 0-based
//    public GameObject lockOverlay;         // whole dim/lock group (or icon)
//    public Image[] starImages;             // length 3; filled star sprite set via Image type "Filled" or swap sprite

//    [Header("Optional")]
//    public TMPro.TMP_Text titleText;       // e.g., "Stage 1-18"

//    /// <summary> Called by HomeManager after build & whenever visuals update. </summary>
//    public void SetLocked(bool locked)
//    {
//        if (lockOverlay) lockOverlay.SetActive(locked);
//    }

//    /// <summary> 0..3 stars </summary>
//    public void SetStars(int stars)
//    {
//        if (starImages == null) return;
//        for (int i = 0; i < starImages.Length; i++)
//        {
//            if (!starImages[i]) continue;
//            starImages[i].enabled = i < stars;   // simple on/off; or use fill/swap
//        }
//    }

//    public void SetTitle(string text)
//    {
//        if (titleText) titleText.text = text;
//    }
//}
