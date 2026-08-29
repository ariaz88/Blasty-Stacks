using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// One card on the level-up screen.
///
/// A card is a (hero, stat) pair: the portrait says WHO gets the buff, the value
/// says how much, and the star row previews what that hero's stack will look
/// like once the card is taken.
/// </summary>
public class SkillCardUI : MonoBehaviour
{
    [Tooltip("Shown only on a hero's FIRST star of this buff, alongside the NEW ribbon.")]
    public GameObject firstUI;

    [SerializeField] private Image iconImage;

    [Tooltip("The hero this card buffs. Left empty, or on a global card, the card just " +
             "shows the buff icon instead.")]
    [SerializeField] private Image portraitImage;

    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;

    [Tooltip("The big green number, e.g. '+50%'. Optional - without it the value is " +
             "appended to the description instead.")]
    [SerializeField] private TextMeshProUGUI valueText;

    [SerializeField] private Transform starsContainer;
    [SerializeField] private Image[] starImages;
    [SerializeField] private GameObject newRibbon;

    private BuffOffer offer;
    private System.Action<BuffOffer> onSelected;

    private void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClick);
    }

    /// <summary>
    /// Fills the card in. <paramref name="portrait"/> may be null (global card, or
    /// no UnitsDatabase assigned on the manager).
    /// </summary>
    public void Init(BuffOffer data, Sprite portrait, System.Action<BuffOffer> callback)
    {
        offer = data;
        onSelected = callback;

        if (!data.IsValid) return;

        var skill = data.skill;
        int resultingStars = data.currentStars + 1;
        bool isMaxed = resultingStars >= skill.MaxStars;

        if (portraitImage != null)
        {
            bool hasPortrait = portrait != null;
            portraitImage.gameObject.SetActive(hasPortrait);
            if (hasPortrait) portraitImage.sprite = portrait;
        }

        if (iconImage != null)
            iconImage.sprite = isMaxed && skill.evolvedIcon != null ? skill.evolvedIcon : skill.normalIcon;

        if (nameText != null) nameText.text = skill.skillName;

        string value = FormatValue(data.increment);

        if (valueText != null)
        {
            valueText.text = value;
            if (descriptionText != null) descriptionText.text = skill.description;
        }
        else if (descriptionText != null)
        {
            // No dedicated value label on this prefab - fold it into the description
            // so the number is never invisible.
            descriptionText.text = string.IsNullOrEmpty(skill.description)
                ? value
                : $"{skill.description}  {value}";
        }

        bool isFirstStar = data.currentStars == 0;

        if (firstUI != null) firstUI.SetActive(isFirstStar);
        if (newRibbon != null) newRibbon.SetActive(isFirstStar);

        // The star row previews the hero's stack AFTER this pick, so the player can
        // see what taking it buys. Hidden on the first star (the NEW ribbon says it)
        // and once the buff is maxed (the evolved art says it).
        bool showStars = !isFirstStar && !isMaxed;

        if (starsContainer != null) starsContainer.gameObject.SetActive(showStars);

        if (starImages != null)
        {
            for (int i = 0; i < starImages.Length; i++)
            {
                if (starImages[i] == null) continue;
                starImages[i].gameObject.SetActive(showStars && i < resultingStars);
            }
        }
    }

    /// <summary>
    /// Increments are stored as fractions - 0.5 means +50%. Everything in the pool
    /// is a positive buff, so the sign is always '+'.
    /// </summary>
    private static string FormatValue(float increment)
    {
        return $"+{Mathf.RoundToInt(increment * 100f)}%";
    }

    /// <summary>Wired from the Button in Awake. Also safe to call from the prefab's OnClick list.</summary>
    public void OnClick()
    {
        onSelected?.Invoke(offer);
    }
}
