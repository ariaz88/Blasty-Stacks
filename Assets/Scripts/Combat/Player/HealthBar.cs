using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a unit's world-space HP bar: the fill Image plus the "hit" bar that
/// lags behind it after damage.
///
/// The hit bar is an AUTHORED ART OBJECT (PlayerHP_Hit), not a tinted clone of
/// the fill. Nothing here writes an RGB colour - the sprite decides how the
/// damage sliver looks, and the only channel this script ever touches is alpha,
/// and only to fade the sliver out once it has caught up.
///
/// Expected player hierarchy (see the doc file for the full spec):
///   PlayerProgressBarUI            Canvas + this component
///     PlayerHP_frame               Image (frame art)
///       PlayerHP_Mask              Image + Mask
///         PlayerHP_Hit             Image, Filled/Horizontal  <- hitBar
///         PlayerHP_Fill            Image, Filled/Horizontal  <- healthBar
/// Hit is the earlier sibling on purpose: Fill draws over it, so only the
/// sliver between the two fill amounts is visible.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Header("Bar")]
    [Tooltip("The Filled Image that shows current HP. Empty = resolved from a child " +
             "named 'PlayerHP_Fill', then from an Image on this object.")]
    public Image healthBar;

    [Header("Render Order")]

    [SerializeField] private bool forceOnTop = true;


    [SerializeField] private int onTopSortingOrder = 500;


    [SerializeField] private string onTopSortingLayer = "";

    [Header("Hit Bar (damage trail)")]
    [Tooltip("The Filled Image that trails the fill after a hit - the PlayerHP_Hit art. " +
             "Empty = resolved from a child named 'PlayerHP_Hit'.")]
    [SerializeField] private Image hitBar;

    [Tooltip("Name of the child to auto-resolve the hit bar from when it is left empty.")]
    [SerializeField] private string hitBarChildName = "PlayerHP_Hit";

    [Tooltip("Name of the child to auto-resolve the fill from when Health Bar is left empty.")]
    [SerializeField] private string fillChildName = "PlayerHP_Fill";

    [Tooltip("ON = if no hit bar can be resolved, build one by cloning the fill Image and " +
             "swapping in Hit Sprite. Does nothing while Hit Sprite is empty - a bar with " +
             "no hit art simply has no damage trail.")]
    [SerializeField] private bool autoCreateHitBar = true;

    [Tooltip("Sprite used for the auto-built hit bar. Only read when Hit Bar is empty AND " +
             "Auto Create Hit Bar is on - the player prefabs author PlayerHP_Hit directly " +
             "and never reach this path.")]
    [SerializeField] private Sprite hitSprite;


    [SerializeField, Min(0f)] private float trailHoldSeconds = 0.18f;

    [SerializeField, Min(0.01f)] private float trailDrainPerSecond = 0.9f;

    [Tooltip("Alpha-only fade once the hit bar has caught up with the fill. The sprite's " +
             "RGB is never touched. 0 = snap off with no fade.")]
    [SerializeField, Min(0f)] private float trailFadeSeconds = 0.12f;

    [Header("Battle Gate")]

    [SerializeField] private bool hideUntilBattleStarts = false;

    private Canvas ownerCanvas;

    private bool hiddenOnGate;

    private Canvas sortingCanvas;
    private int authoredSortingOrder;
    private string authoredSortingLayer;
    private bool capturedAuthoredSorting;

    float _baseLocalScaleX;

    // The colour the ARTIST put on the hit Image. Cached so the fade can restore it
    // exactly instead of the script inventing one.
    private Color authoredHitColor = Color.white;

    private float trailFill = 1f;
    private float trailHoldTimer;
    private float trailFadeTimer;
    private bool trailSeeded;

    void Awake()
    {
        ResolveBars();

        CaptureAuthoredSorting();
        ApplyRenderOrder();
        ApplyBattleGate();


        LevelGameManager.OnGameStateChanged += HandleGameStateChanged;

        // A bar spawned INTO an already-finished battle (a unit that outlives the
        // win panel) never sees the event, so resolve the current state now.
        if (!LevelGameManager.IsBattleRunning)
            RestoreAuthoredSorting();

        ConfigureAsHorizontalFill(healthBar);

        if (!hitBar && autoCreateHitBar && hitSprite)
            BuildHitBar();

        // The hit bar starts INVISIBLE - an undamaged unit must not show a sliver
        // under a full bar.
        if (hitBar)
        {
            authoredHitColor = hitBar.color;
            ConfigureAsHorizontalFill(hitBar);
            hitBar.raycastTarget = false;
            hitBar.fillAmount = 1f;
            hitBar.enabled = false;
        }

        _baseLocalScaleX = Mathf.Abs(transform.localScale.x);
    }

    /// <summary>
    /// Fills in whichever of the two Images was left empty in the Inspector, by
    /// child name first and then by falling back to an Image on this object. Named
    /// lookup is what lets one prefab-wide edit rewire every character at once.
    /// </summary>
    void ResolveBars()
    {
        if (!healthBar && !string.IsNullOrEmpty(fillChildName))
            healthBar = FindImageInChildren(fillChildName);

        if (!healthBar)
            healthBar = GetComponent<Image>();

        if (!hitBar && !string.IsNullOrEmpty(hitBarChildName))
            hitBar = FindImageInChildren(hitBarChildName);

        if (!healthBar)
            Debug.LogError("[HealthBar] No fill Image on '" + name + "'. Assign Health Bar.", this);
    }

    Image FindImageInChildren(string childName)
    {
        foreach (var img in GetComponentsInChildren<Image>(true))
            if (img.name == childName) return img;
        return null;
    }

    // A bar that is not Filled/Horizontal/Left never drains, so this is forced
    // rather than trusted to the prefab.
    static void ConfigureAsHorizontalFill(Image img)
    {
        if (!img) return;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left; // anchored left, empties from the right
    }

    void LateUpdate()
    {
        TickDamageTrail();
        KeepUnmirrored();
    }


    void TickDamageTrail()
    {
        if (!hitBar || !hitBar.enabled) return;

        float target = healthBar ? healthBar.fillAmount : 0f;
        float dt = Time.deltaTime;

        if (trailHoldTimer > 0f)
        {
            trailHoldTimer -= dt;
            return;
        }

        if (trailFill > target + 0.0001f)
        {
            trailFill = Mathf.MoveTowards(trailFill, target, trailDrainPerSecond * dt);
            hitBar.fillAmount = trailFill;

            // Caught up: hand over to the fade. Zero fade time still needs one
            // frame here, which HideHitBar below resolves immediately.
            if (trailFill <= target + 0.0001f)
                trailFadeTimer = trailFadeSeconds;
            return;
        }

        if (trailFadeTimer > 0f)
        {
            trailFadeTimer -= dt;
            float a = (trailFadeSeconds > 0f) ? Mathf.Clamp01(trailFadeTimer / trailFadeSeconds) : 0f;

            // Alpha only - RGB stays exactly as the sprite/artist set it.
            var c = authoredHitColor;
            c.a *= a;
            hitBar.color = c;
            if (trailFadeTimer > 0f) return;
        }

        HideHitBar();
    }


    /// <summary>
    /// Fallback for bars that have no authored PlayerHP_Hit child: clone the fill
    /// Image and swap in <see cref="hitSprite"/>. Sprite swap, not a tint - a bar
    /// with no hit art gets no trail rather than a recoloured one.
    /// </summary>
    void BuildHitBar()
    {
        if (!healthBar || healthBar.transform.parent == null) return;

        var src = healthBar.gameObject;


        if (src.GetComponentInChildren<HealthBar>(true) != null)
        {
            Debug.LogWarning("[HealthBar] Auto hit bar skipped on '" + name +
                             "': the fill Image shares its object with a HealthBar. " +
                             "Assign Hit Bar by hand, or move the Image to its own child.", this);
            return;
        }

        var go = Instantiate(src, src.transform.parent, false);
        go.name = src.name + "_Hit";


        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Destroy(go.transform.GetChild(i).gameObject);
        foreach (var c in go.GetComponents<Component>())
            if (!(c is Transform) && !(c is CanvasRenderer) && !(c is Image))
                Destroy(c);

        go.SetActive(true);

        // Behind the fill, so only the sliver past the fill's edge shows.
        go.transform.SetSiblingIndex(src.transform.GetSiblingIndex());

        hitBar = go.GetComponent<Image>();
        if (hitBar) hitBar.sprite = hitSprite;
    }

    void HideHitBar()
    {
        if (!hitBar) return;
        hitBar.enabled = false;
        hitBar.color = authoredHitColor;
        trailFadeTimer = 0f;
        trailHoldTimer = 0f;
    }


    void KeepUnmirrored()
    {

        var probe = healthBar ? healthBar.transform : transform;

        float worldRightX = probe.localToWorldMatrix.MultiplyVector(Vector3.right).x;


        if (worldRightX >= -0.0001f) return;


        if (!probe.IsChildOf(transform)) return;

        Vector3 ls = transform.localScale;
        ls.x = (ls.x >= 0f) ? -_baseLocalScaleX : _baseLocalScaleX;
        transform.localScale = ls;
    }


    void ApplyBattleGate()
    {
        if (!hideUntilBattleStarts) return;

        if (!ownerCanvas) ownerCanvas = GetComponentInParent<Canvas>();
        if (!ownerCanvas) return;


        if (EnemySpawner.EnemiesHaveAppeared)
            return;   // enemies already out (or a stage that never gates)

        ownerCanvas.enabled = false;
        EnemySpawner.OnAnyFirstEnemySpawned += ShowForBattle;
    }

    void ShowForBattle()
    {
        EnemySpawner.OnAnyFirstEnemySpawned -= ShowForBattle;

        // Still posing on the castle gate: the spawn code owns the bar until it
        // lands. SetHiddenOnGate(false) is what reveals it.
        if (hiddenOnGate) return;

        if (ownerCanvas) ownerCanvas.enabled = true;
    }


    public void SetHiddenOnGate(bool hidden)
    {
        hiddenOnGate = hidden;

        // Resolved here as well as in ApplyBattleGate: that method returns early
        // when hideUntilBattleStarts is off, leaving ownerCanvas null.
        if (!ownerCanvas) ownerCanvas = GetComponentInParent<Canvas>();
        if (!ownerCanvas) return;

        if (hidden)
        {
            ownerCanvas.enabled = false;
            return;
        }

        if (hideUntilBattleStarts && !EnemySpawner.EnemiesHaveAppeared) return;

        ownerCanvas.enabled = true;
    }

    void OnDestroy()
    {
        EnemySpawner.OnAnyFirstEnemySpawned -= ShowForBattle;
        LevelGameManager.OnGameStateChanged -= HandleGameStateChanged;
    }


    void HandleGameStateChanged(LevelGameManager.GameState state)
    {
        if (state == LevelGameManager.GameState.Playing)
            ApplyRenderOrder();
        else
            RestoreAuthoredSorting();
    }


    void CaptureAuthoredSorting()
    {
        if (capturedAuthoredSorting) return;

        sortingCanvas = GetComponentInParent<Canvas>();
        if (!sortingCanvas) return;

        authoredSortingOrder = sortingCanvas.sortingOrder;
        authoredSortingLayer = sortingCanvas.sortingLayerName;
        capturedAuthoredSorting = true;
    }

    void RestoreAuthoredSorting()
    {
        if (!capturedAuthoredSorting || !sortingCanvas) return;

        sortingCanvas.sortingOrder = authoredSortingOrder;

        // Only undo the layer move if we were the ones who made it.
        if (!string.IsNullOrEmpty(onTopSortingLayer))
            sortingCanvas.sortingLayerName = authoredSortingLayer;
    }


    void ApplyRenderOrder()
    {
        if (!forceOnTop) return;

        CaptureAuthoredSorting();
        var canvas = sortingCanvas;
        if (!canvas) return;

        if (!string.IsNullOrEmpty(onTopSortingLayer))
            canvas.sortingLayerName = onTopSortingLayer;


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

        float target = currentHealth / maxHealth;
        if (healthBar) healthBar.fillAmount = target;

        if (!hitBar) return;


        if (!trailSeeded)
        {
            trailSeeded = true;
            trailFill = target;
            HideHitBar();
            return;
        }


        if (target >= trailFill - 0.0001f)
        {
            trailFill = target;
            HideHitBar();
            return;
        }


        hitBar.fillAmount = trailFill;
        hitBar.color = authoredHitColor;
        hitBar.enabled = true;
        trailHoldTimer = trailHoldSeconds;
        trailFadeTimer = 0f;
    }
}
