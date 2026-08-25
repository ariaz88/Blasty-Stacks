using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image healthBar;

    [Header("Render Order")]

    [SerializeField] private bool forceOnTop = true;

   
    [SerializeField] private int onTopSortingOrder = 500;

    
    [SerializeField] private string onTopSortingLayer = "";

    [Header("Damage Trail")]
    
    [SerializeField] private Image delayedBar;

   
    [SerializeField] private bool autoCreateTrail = true;

   
    [SerializeField] private Color delayedColor = new Color(1f, 0.82f, 0.15f, 1f);

  
    [SerializeField, Min(0f)] private float trailHoldSeconds = 0.18f;

        [SerializeField, Min(0.01f)] private float trailDrainPerSecond = 0.9f;

   
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

    
    private float trailFill = 1f;
    private float trailHoldTimer;
    private float trailFadeTimer;
    private bool trailSeeded;

    void Awake()
    {
        if (!healthBar)
            healthBar = GetComponent<Image>();

        CaptureAuthoredSorting();
        ApplyRenderOrder();
        ApplyBattleGate();

        
        LevelGameManager.OnGameStateChanged += HandleGameStateChanged;

        // A bar spawned INTO an already-finished battle (a unit that outlives the
        // win panel) never sees the event, so resolve the current state now.
        if (!LevelGameManager.IsBattleRunning)
            RestoreAuthoredSorting();

        // Make sure the image is a filled horizontal bar
        healthBar.type = Image.Type.Filled;
        healthBar.fillMethod = Image.FillMethod.Horizontal;
        healthBar.fillOrigin = (int)Image.OriginHorizontal.Left; // anchored left, empties from the right

        if (!delayedBar && autoCreateTrail)
            BuildTrail();

        // Same treatment for the trail, and it starts INVISIBLE - an undamaged
        // unit must not show a yellow sliver under a full bar.
        if (delayedBar)
        {
            delayedBar.type = Image.Type.Filled;
            delayedBar.fillMethod = Image.FillMethod.Horizontal;
            delayedBar.fillOrigin = (int)Image.OriginHorizontal.Left;
            delayedBar.color = delayedColor;
            delayedBar.raycastTarget = false;
            delayedBar.fillAmount = 1f;
            delayedBar.enabled = false;
        }

        _baseLocalScaleX = Mathf.Abs(transform.localScale.x);
    }

    void LateUpdate()
    {
        TickDamageTrail();
        KeepUnmirrored();
    }

    
    void TickDamageTrail()
    {
        if (!delayedBar || !delayedBar.enabled) return;

        float target = healthBar.fillAmount;
        float dt = Time.deltaTime;

        if (trailHoldTimer > 0f)
        {
            trailHoldTimer -= dt;
            return;
        }

        if (trailFill > target + 0.0001f)
        {
            trailFill = Mathf.MoveTowards(trailFill, target, trailDrainPerSecond * dt);
            delayedBar.fillAmount = trailFill;

            // Caught up: hand over to the fade. Zero fade time still needs one
            // frame here, which HideTrail below resolves immediately.
            if (trailFill <= target + 0.0001f)
                trailFadeTimer = trailFadeSeconds;
            return;
        }

        if (trailFadeTimer > 0f)
        {
            trailFadeTimer -= dt;
            float a = (trailFadeSeconds > 0f) ? Mathf.Clamp01(trailFadeTimer / trailFadeSeconds) : 0f;
            var c = delayedColor;
            c.a *= a;
            delayedBar.color = c;
            if (trailFadeTimer > 0f) return;
        }

        HideTrail();
    }

    
    void BuildTrail()
    {
        if (!healthBar || healthBar.transform.parent == null) return;

        var src = healthBar.gameObject;

        
        if (src.GetComponentInChildren<HealthBar>(true) != null)
        {
            Debug.LogWarning("[HealthBar] Auto trail skipped on '" + name +
                             "': the fill Image shares its object with a HealthBar. " +
                             "Assign Delayed Bar by hand, or move the Image to its own child.", this);
            return;
        }

        var go = Instantiate(src, src.transform.parent, false);
        go.name = src.name + "_DamageTrail";

       
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Destroy(go.transform.GetChild(i).gameObject);
        foreach (var c in go.GetComponents<Component>())
            if (!(c is Transform) && !(c is CanvasRenderer) && !(c is Image))
                Destroy(c);

        go.SetActive(true);
        
        go.transform.SetSiblingIndex(src.transform.GetSiblingIndex());

        delayedBar = go.GetComponent<Image>();
    }

    void HideTrail()
    {
        if (!delayedBar) return;
        delayedBar.enabled = false;
        delayedBar.color = delayedColor;
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
        healthBar.fillAmount = target;

        if (!delayedBar) return;

       
        if (!trailSeeded)
        {
            trailSeeded = true;
            trailFill = target;
            HideTrail();
            return;
        }

      
        if (target >= trailFill - 0.0001f)
        {
            trailFill = target;
            HideTrail();
            return;
        }

        
        delayedBar.fillAmount = trailFill;
        delayedBar.color = delayedColor;
        delayedBar.enabled = true;
        trailHoldTimer = trailHoldSeconds;
        trailFadeTimer = 0f;
    }
}
