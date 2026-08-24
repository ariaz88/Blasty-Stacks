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

    [Header("Damage Trail")]
    [Tooltip("Second fill Image sitting BEHIND the main bar. It holds the health " +
             "the unit had BEFORE the last hit, then drains down to the real value " +
             "and fades out - so a 20% hit reads as a 20% yellow chunk peeling " +
             "away instead of the bar teleporting.\n" +
             "Leave EMPTY and it is BUILT AT RUNTIME from the main bar (see Auto " +
             "Create Trail). Assign one by hand only to override the look.")]
    [SerializeField] private Image delayedBar;

    [Tooltip("ON = clone the main bar at Awake to make the trail, so no prefab or " +
             "scene needs a hand-authored object and every bar in the game gets " +
             "one (units AND castle gates).\n" +
             "OFF with an empty Delayed Bar = no trail, exactly the old behaviour.")]
    [SerializeField] private bool autoCreateTrail = true;

    [Tooltip("Colour forced onto the trail Image at Awake, so every prefab reads " +
             "the same without per-prefab tinting.")]
    [SerializeField] private Color delayedColor = new Color(1f, 0.82f, 0.15f, 1f);

    [Tooltip("Seconds the trail SITS STILL at the old value after a hit, before it " +
             "starts draining. This pause is what makes the chunk readable.")]
    [SerializeField, Min(0f)] private float trailHoldSeconds = 0.18f;

    [Tooltip("Drain speed as a FRACTION OF THE FULL BAR per second. 0.9 = a full " +
             "bar would empty in a bit over a second, so a 20% chip takes ~0.22s.")]
    [SerializeField, Min(0.01f)] private float trailDrainPerSecond = 0.9f;

    [Tooltip("Alpha fade-out once the trail has caught up with the real health.")]
    [SerializeField, Min(0f)] private float trailFadeSeconds = 0.12f;

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

    // True while the spawn code is holding this hero on a castle gate. Kept as
    // state rather than a bare canvas toggle so ShowForBattle cannot reveal the
    // bar out from under the pose.
    private bool hiddenOnGate;

    // The Canvas that carries the sorting order, and the order/layer it was
    // AUTHORED with. onTopSortingOrder is only ever a combat-time override; the
    // authored values are what we fall back to once the battle is over.
    private Canvas sortingCanvas;
    private int authoredSortingOrder;
    private string authoredSortingLayer;
    private bool capturedAuthoredSorting;

    float _baseLocalScaleX;

    // Damage-trail state. trailFill is the fill the trail is CURRENTLY showing -
    // it is the authority, not delayedBar.fillAmount, so a fade can run without
    // the drain maths reading a value someone else changed.
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

        // The 500 override must not outlive the battle. Win, lose and revive
        // panels all sit below it, so a bar left on top punches straight through
        // them - and any future panel (the roguelite skill pick) would hit the
        // same wall. Track the level state and drop back to the authored order
        // the moment a gate dies.
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

    /// <summary>
    /// Hold -> drain -> fade. Runs on scaled time on purpose: when the game is
    /// paused or a panel has frozen the level, the chunk should freeze with it
    /// rather than quietly finishing behind the pause menu.
    /// </summary>
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

    /// <summary>
    /// Clones the main bar Image into a sibling that renders one slot BEHIND it.
    ///
    /// Cloning rather than building from scratch is deliberate: it inherits the
    /// sprite, the RectTransform, and - the part that actually matters here - the
    /// enemy prefabs' hand-authored mirroring (180 degree Y rotation plus
    /// scale.x = -1 on the graphics). A from-scratch Image would have to
    /// re-derive all of that and would drift the first time someone re-authors a
    /// prefab.
    /// </summary>
    void BuildTrail()
    {
        if (!healthBar || healthBar.transform.parent == null) return;

        var src = healthBar.gameObject;

        // HARD STOP. If the fill Image sits on the SAME object as this script
        // (the GetComponent<Image>() fallback above), cloning it clones this
        // script too - and the clone's Awake would clone again, forever. Same
        // for a HealthBar anywhere inside. Bail loudly instead of hanging.
        if (src.GetComponentInChildren<HealthBar>(true) != null)
        {
            Debug.LogWarning("[HealthBar] Auto trail skipped on '" + name +
                             "': the fill Image shares its object with a HealthBar. " +
                             "Assign Delayed Bar by hand, or move the Image to its own child.", this);
            return;
        }

        var go = Instantiate(src, src.transform.parent, false);
        go.name = src.name + "_DamageTrail";

        // A bar graphic is a lone Image in every prefab here, but strip anything
        // that came along anyway - a cloned script would run a second copy of
        // whatever it does, silently.
        for (int i = go.transform.childCount - 1; i >= 0; i--)
            Destroy(go.transform.GetChild(i).gameObject);
        foreach (var c in go.GetComponents<Component>())
            if (!(c is Transform) && !(c is CanvasRenderer) && !(c is Image))
                Destroy(c);

        go.SetActive(true);

        // Taking the main bar's slot pushes the main bar one later in the child
        // list, and later siblings draw ON TOP in uGUI. That is what keeps the
        // trail peeking out only where health USED to be.
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

    /// <summary>
    /// Keep the bar unmirrored in WORLD space, whatever the unit does.
    /// </summary>
    void KeepUnmirrored()
    {
        // This used to mirror itself whenever the PARENT's lossyScale.x was
        // negative. That is a double correction on the enemy prefabs: their root
        // is authored at scale.x = -1 and the bar hierarchy already cancels it by
        // hand (the Canvas, `Bar (1)` and `BG (1)` each carry a 180 degree Y
        // rotation, plus scale.x = -1 on the two graphics). Those flips cancel out
        // to "upright" in the Editor, then this method added one more at runtime -
        // so the enemy bar rendered mirrored in Play mode only, draining left->right
        // while the player's drained right->left.
        //
        // Measuring the FILL IMAGE's own world axis instead fixes both families
        // without touching a prefab: lossyScale cannot see a 180 degree rotation,
        // but the world matrix can. Player bars (clean, identity all the way down)
        // now measure positive and are never written to at all.
        var probe = healthBar ? healthBar.transform : transform;

        float worldRightX = probe.localToWorldMatrix.MultiplyVector(Vector3.right).x;

        // ~0 means the bar is edge-on (rotated 90 degrees); there is no meaningful
        // side to correct, so leave it alone rather than flapping.
        if (worldRightX >= -0.0001f) return;

        // Our own scale only moves the probe if the probe hangs off us. A bar
        // wired to an Image somewhere else entirely would otherwise flip-flop
        // every frame, never able to fix what it is measuring.
        if (!probe.IsChildOf(transform)) return;

        Vector3 ls = transform.localScale;
        ls.x = (ls.x >= 0f) ? -_baseLocalScaleX : _baseLocalScaleX;
        transform.localScale = ls;
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

        // Still posing on the castle gate: the spawn code owns the bar until it
        // lands. SetHiddenOnGate(false) is what reveals it.
        if (hiddenOnGate) return;

        if (ownerCanvas) ownerCanvas.enabled = true;
    }

    /// <summary>
    /// Hides the bar while its hero is still STANDING ON THE CASTLE GATE, and
    /// gives it back once the hero has jumped into the field.
    ///
    /// Separate from <see cref="hideUntilBattleStarts"/> on purpose: that one is
    /// keyed to the FIRST ENEMY appearing, which covers heroes gathering during
    /// the puzzle phase but does nothing for mid-battle reinforcements - those
    /// are bought long AFTER the enemies are out, so ApplyBattleGate has already
    /// bowed out and the bar would pop up on the gate.
    /// </summary>
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

        // Do not punch through the battle gate's own hide - it is still waiting
        // for the first enemy and owns the bar until then.
        if (hideUntilBattleStarts && !EnemySpawner.EnemiesHaveAppeared) return;

        ownerCanvas.enabled = true;
    }

    void OnDestroy()
    {
        // Static events: without this, a hero destroyed before the first enemy
        // appears would keep the subscription alive and throw later.
        EnemySpawner.OnAnyFirstEnemySpawned -= ShowForBattle;
        LevelGameManager.OnGameStateChanged -= HandleGameStateChanged;
    }

    /// <summary>
    /// Combat over -> give the sorting order back so end-of-battle UI can cover
    /// the bar. A revive puts the level back into Playing, so this raises the bar
    /// again rather than being a one-way trip.
    /// </summary>
    void HandleGameStateChanged(LevelGameManager.GameState state)
    {
        if (state == LevelGameManager.GameState.Playing)
            ApplyRenderOrder();
        else
            RestoreAuthoredSorting();
    }

    /// <summary>
    /// Remembers what the prefab actually shipped with, BEFORE ApplyRenderOrder
    /// overwrites it. Restoring these exact values is what guarantees the bar
    /// lands back under the UI - rather than guessing at some "low" number that
    /// might still outrank a panel.
    /// </summary>
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

    /// <summary>
    /// Pushes the owning Canvas above the character sprites so the bar is never
    /// hidden behind a unit standing in front of its owner.
    /// </summary>
    void ApplyRenderOrder()
    {
        if (!forceOnTop) return;

        CaptureAuthoredSorting();
        var canvas = sortingCanvas;
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

        float target = currentHealth / maxHealth;
        healthBar.fillAmount = target;

        if (!delayedBar) return;

        // The FIRST call is the spawn value, not a hit - seed and stay quiet.
        // Without this every unit whose max HP is applied after Awake would
        // flash a trail on frame one.
        if (!trailSeeded)
        {
            trailSeeded = true;
            trailFill = target;
            HideTrail();
            return;
        }

        // Healed (or unchanged): the trail has nothing to show, so snap it up
        // and get out of the way. Anything else would leave a yellow chunk
        // stranded ABOVE the real health.
        if (target >= trailFill - 0.0001f)
        {
            trailFill = target;
            HideTrail();
            return;
        }

        // Took damage. trailFill still holds the PRE-HIT value, which is exactly
        // the chunk we want to show; re-arming the hold on every hit means a
        // combo reads as one continuous chunk rather than a stutter.
        delayedBar.fillAmount = trailFill;
        delayedBar.color = delayedColor;
        delayedBar.enabled = true;
        trailHoldTimer = trailHoldSeconds;
        trailFadeTimer = 0f;
    }
}
