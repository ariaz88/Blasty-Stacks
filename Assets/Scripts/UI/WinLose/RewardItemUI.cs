using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardItemUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI amountText;

    public void Setup(Sprite icon, int amount)
    {
        if (iconImage)
            iconImage.sprite = icon;

        if (amountText)
            amountText.text = amount.ToString(); // or $"x{amount}" if you like
    }
}
