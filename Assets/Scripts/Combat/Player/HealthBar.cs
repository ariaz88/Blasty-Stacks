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

    [Header("Battle Gate")]
    [Tooltip("ON  = the bar stays hidden through the puzzle phase AND the camera " +
             "move, appearing only once the FIRST ENEMY is actually on screen. " +
             "Use this on UNIT prefabs.\n" +
             "OFF = always visible. Keep it off for GATE bars, which are meant to " +
             "be readable during the puzzle phase.")]
    [SerializeField] private bool hideUntilBattleStarts = false;

    // The Canvas we show/hide. Disabling the COMPONENT (not the GameObject) hides
    // every child graphic without re-running anyone's Awake when it comes back.
    private Canvas ownerCanvas;

    float _baseLocalScaleX;

    void Awake()
    {
        if (!healthBar)
            healthBar = GetComponent<Image>();

        ApplyRenderOrder();
        ApplyBattleGate();

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
    /// Hides the bar until the battle actually starts, then reveals it.
    /// Only does anything when hideUntilBattleStarts is ticked, so gate bars are
    /// untouched.
    /// </summary>
    void ApplyBattleGate()
    {
        if (!hideUntilBattleStarts) return;

        if (!ownerCanvas) ownerCanvas = GetComponentInParent<Canvas>();
        if (!ownerCanvas) return;

        // The cue is the FIRST ENEMY APPEARING, not the BATTLE press. Pressing
        // BATTLE only starts the camera move; revealing the bars at that moment
        // made them pop in while the camera was still travelling. Waiting for the
        // spawner means the bars arrive with the enemies.
        if (EnemySpawner.EnemiesHaveAppeared)
            return;   // enemies already out (or a stage that never gates)

        ownerCanvas.enabled = false;
        EnemySpawner.OnAnyFirstEnemySpawned += ShowForBattle;
    }

    void ShowForBattle()
    {
        EnemySpawner.OnAnyFirstEnemySpawned -= ShowForBattle;
        if (ownerCanvas) ownerCanvas.enabled = true;
    }

    void OnDestroy()
    {
        // Static event: without this, a hero destroyed before the first enemy
        // appears would keep the subscription alive and throw later.
        EnemySpawner.OnAnyFirstEnemySpawned -= ShowForBattle;
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
