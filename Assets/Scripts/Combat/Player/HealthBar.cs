using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image healthBar;

    [Header("Render Order")]
    [Tooltip("Force the owning world-space Canvas to draw ABOVE every character " +
             "sprite. Without this the bar sits at sortingOrder 1, effectively tied " +
             "with the units, so whichever unit happens to draw later covers the " +
             "bars of the units behind it.")]
    [SerializeField] private bool forceOnTop = true;

    [Tooltip("Sorting order applied when Force On Top is on. Must be higher than " +
             "any character sprite's order on the same sorting layer.")]
    [SerializeField] private int onTopSortingOrder = 500;

    [Tooltip("Optional sorting layer to move the bar onto. Leave EMPTY to stay on " +
             "the Canvas's current layer and rely on the order alone.")]
    [SerializeField] private string onTopSortingLayer = "";

    float _baseLocalScaleX;

    void Awake()
    {
        if (!healthBar)
            healthBar = GetComponent<Image>();

        ApplyRenderOrder();

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
            float localSign = (parentSign >= 0f) ? 1f : -1f;

            Vector3 ls = transform.localScale;
            ls.x = _baseLocalScaleX * localSign;
            transform.localScale = ls;
        }
    }

    /// <summary>
    /// Pushes the owning Canvas above the character sprites so the bar is never
    /// hidden behind a unit standing in front of its owner.
    /// </summary>
    void ApplyRenderOrder()
    {
        if (!forceOnTop) return;

        var canvas = GetComponentInParent<Canvas>();
        if (!canvas) return;

        if (!string.IsNullOrEmpty(onTopSortingLayer))
            canvas.sortingLayerName = onTopSortingLayer;

        // A NESTED canvas ignores its own sorting unless overrideSorting is set;
        // for a root canvas the flag is simply irrelevant.
        if (canvas.transform.parent != null &&
            canvas.transform.parent.GetComponentInParent<Canvas>() != null)
        {
            canvas.overrideSorting = true;
        }

        canvas.sortingOrder = onTopSortingOrder;
    }

    public void SetCurrentHealth(float currentHealth, float maxHealth)
    {
        currentHealth = Mathf.Max(0f, currentHealth);
        maxHealth = Mathf.Max(0.0001f, maxHealth); // avoid divide-by-zero

        healthBar.fillAmount = currentHealth / maxHealth;
    }
}
