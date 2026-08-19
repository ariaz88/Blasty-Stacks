using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image healthBar;

    float _baseLocalScaleX;

    void Awake()
    {
        if (!healthBar)
            healthBar = GetComponent<Image>();

        // Make sure the image is a filled horizontal bar
        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Horizontal;
        healthBar.fillOrigin = (int)Image.OriginHorizontal.Left; // fill from right → left

        _baseLocalScaleX = Mathf.Abs(transform.localScale.x);
    }

    void LateUpdate()
    {
        // If parent flips (scale.x negative), flip this child back
        // so in world space it always stays upright.
        if (transform.parent != null)
        {
            float parentSign = Mathf.Sign(transform.parent.lossyScale.x);
            // localSign = parentSign >= 0 ? +1 : -1
            float localSign = (parentSign >= 0f) ? 1f : -1f;

            Vector3 ls = transform.localScale;
            ls.x = _baseLocalScaleX * localSign;
            transform.localScale = ls;
        }
    }

    public void SetCurrentHealth(float currentHealth, float maxHealth)
    {
        currentHealth = Mathf.Max(0f, currentHealth);
        maxHealth = Mathf.Max(0.0001f, maxHealth); // avoid divide-by-zero

        healthBar.fillAmount = currentHealth / maxHealth;
    }
}
