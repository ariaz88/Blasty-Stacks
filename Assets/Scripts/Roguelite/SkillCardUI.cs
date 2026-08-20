using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles display and interaction for a single skill card in the selection UI.
/// </summary>
public class SkillCardUI : MonoBehaviour
{
    public GameObject firstUI;
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Transform starsContainer;
    [SerializeField] private Image[] starImages;
    [SerializeField] private GameObject newRibbon;

    private SkillData skillData;
    private System.Action<SkillData> onSelected;

    /// <summary>
    /// Initializes the card visuals and stores its callback.
    /// </summary>
    /// <param name="data">The skill data to display.</param>
    /// <param name="timesSelected">How many times this skill has been picked in the current stage.</param>
    /// <param name="callback">Action to invoke when the card is clicked.</param>
    /// 

    void Awake()
    {
        var btn = GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(OnClick);
    }
    public void Init(SkillData data, int timesSelected, System.Action<SkillData> callback)
    {
        skillData = data;
        onSelected = callback;

        // Use evolved icon if picked 5+ times, otherwise normal icon
        iconImage.sprite = timesSelected >= 5 ? data.evolvedIcon : data.normalIcon;
        nameText.text = data.skillName;
        descriptionText.text = data.description;

        if (timesSelected == 0)
        {
            // first time: show FirstUI and NEW ribbon; hide the stars bar
            firstUI?.SetActive(true);
            starsContainer.gameObject.SetActive(false);
            newRibbon.SetActive(true);
        }
        else if (timesSelected < 6)
        {
            // subsequent picks: hide FirstUI, show stars, update star images
            firstUI?.SetActive(false);
            starsContainer.gameObject.SetActive(true);
            newRibbon.SetActive(false);
            for (int i = 0; i < starImages.Length; i++)
            {
                bool showOn = i < timesSelected;
                starImages[i].gameObject.SetActive(showOn);
            }
        }
        else
        {
            // evolved state: hide everything except the evolved icon
            firstUI?.SetActive(false);
            starsContainer.gameObject.SetActive(false);
            newRibbon.SetActive(false);
        }

    }

    /// <summary>
    /// Called by the button component when the card is clicked.
    /// Invokes the callback with this card�s SkillData.
    /// </summary>
    public void OnClick()
    {
        onSelected?.Invoke(skillData);
    }
}
