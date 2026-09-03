using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The in-battle roguelite layer.
///
/// PHASES. This only exists during the BATTLE phase. While the player is solving
/// the puzzle there is no XP, no level bar and no cards - the army is being
/// assembled and nothing is fighting yet. It starts on
/// <see cref="BattleStartController.OnAnyBattleStarted"/>, which means the roster
/// is already final before the first card is ever drawn.
///
/// LOOP. Enemies killed grant XP -> the bar fills -> the game pauses and offers
/// three cards -> the pick multiplies the matching players' UnitStatsRuntime ->
/// the game resumes.
///
/// TWO STAT LAYERS, kept strictly apart:
///   PERMANENT  PlayerStatsApplier computes baseStats x growth(saved upgrade level)
///              into CurrentStats. Owned by the Units UI, saved to disk.
///   PER-STAGE  this class multiplies CurrentStats on top. Never saved, wiped by
///              ResetForNewStage().
///
/// Which hero each card targets is decided by <see cref="BuffDraw"/>; this class
/// only applies the result.
/// </summary>
public class RogueliteManager : MonoBehaviour
{
    [Header("XP Progression")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TextMeshProUGUI levelCounterText;

    [Tooltip("LEGACY fallback, used only when no Config asset is assigned: the bar needs " +
             "initialThreshold kills at level 1, then one more per level.")]
    [SerializeField] private int initialThreshold = 1;

    [Tooltip("Seconds for the bar to travel its full width. Purely cosmetic - the logical " +
             "XP is never delayed by it.")]
    [SerializeField] private float xpIncreaseDuration = 0.5f;

    [Tooltip("Extra seconds the bar is held VISIBLY FULL before the level-up fires " +
             "and the card screen opens. Without a small pause the bar hits 100% and " +
             "is wiped in the same breath, which reads as a flicker rather than as " +
             "'you filled it'. 0 = fire the moment the fill arrives.")]
    [SerializeField, Min(0f)] private float levelUpBarHoldSeconds = 0.2f;

    [Header("Config")]
    [Tooltip("XP curve and card-draw tuning. Strongly recommended; without it the legacy " +
             "linear threshold above is used and the draw runs unweighted.")]
    [SerializeField] private RogueliteConfigSO config;

    [Tooltip("Used only to look up a hero PORTRAIT for the cards, by matching classType. " +
             "Safe to leave empty - cards then fall back to the buff icon.")]
    [SerializeField] private UnitsDatabaseSO unitsDatabase;

    [Header("Skill Pool")]
    [Tooltip("One asset PER STAT, not per hero. The hero on each card is chosen at draw " +
             "time, so 5 assets x a 4-hero roster already gives 20 distinct cards.")]
    [SerializeField] private List<SkillData> skillPool;

    [Header("UI References")]
    [SerializeField] private GameObject levelUpOverlay;
    [SerializeField] private GameObject skillSelectPanel;
    [SerializeField] private SkillCardUI[] cardSlots;

    [Tooltip("The XP bar's root. Switched ON when the battle phase begins - in most " +
             "stage scenes it is authored inactive. Left empty = found from xpSlider.")]
    [SerializeField] private GameObject xpBarRoot;

    [Tooltip("Distance from the TOP of the screen to the top edge of the XP bar, in " +
             "canvas units. The HUD resource row ends around 185, so ~200 puts the bar " +
             "directly under it. Measured at runtime, so it does not depend on however " +
             "the bar happened to be authored in each of the 21 scenes.")]
    [SerializeField] private float xpBarTopMargin = 200f;

    [Tooltip("Horizontal nudge, if the bar needs shifting left or right.")]
    [SerializeField] private float xpBarHorizontalOffset = 0f;

    [Tooltip("Vertical nudge applied AFTER the measured placement. Positive = up. " +
             "This is the field to drag when the bar just needs to sit a bit higher " +
             "or lower - xpBarTopMargin stays at the value that parks it under the HUD, " +
             "and this offsets from there. 83.7 lines the row up centre-to-centre with " +
             "the gem counter.")]
    [SerializeField] private float xpBarVerticalOffset = 83.7f;

    [Tooltip("Seconds the whole card screen takes to fade away after a pick. " +
             "0 = disappear instantly.")]
    [SerializeField, Min(0f)] private float panelFadeDuration = 0.22f;

    [Tooltip("Legacy leftovers hidden at startup: the Offensive / Defensive icon rows " +
             "and the empty panel above them. The buff list is per-hero now, so these " +
             "rows have no meaning and must not be on screen.")]
    [SerializeField] private GameObject[] legacyObjectsToHide;

    [Tooltip("Sorting order forced onto the card panel. Unit health bars are WorldSpace " +
             "canvases at 500, so anything below that would render UNDER them.")]
    [SerializeField] private int panelSortingOrder = 1000;

    [Tooltip("The dimmer painted over the battlefield while the cards are up.")]
    [SerializeField] private Color scrimColor = new Color(0.05f, 0.05f, 0.08f, 0.82f);

    /// <summary>Raised on every XP gain with its share of the current bar (20f = "+20%").</summary>
    public event Action<float> OnXpGained;

    /// <summary>Raised after each level-up with the new level.</summary>
    public event Action<int> OnLevelUp;

    // ------------------------------------------------------------------ state

    private BuffDraw draw;

    private int level = 1;
    private float xp;
    private float xpDisplay;
    private bool isPaused;
    private bool running;

    /// <summary>
    /// Logical XP has crossed the threshold but the BAR has not finished showing
    /// it. Update() retries every frame until the fill arrives, then the level-up
    /// is released. See TryLevelUp for why this exists.
    /// </summary>
    private bool pendingLevelUp;

    /// <summary>Seconds the fill has been sitting at full, for levelUpBarHoldSeconds.</summary>
    private float barFullTimer;

    /// <summary>
    /// How close to 1 counts as "the bar is full". MoveTowards lands exactly on
    /// its target, but a float compare against 1f is still a coin toss, and being
    /// one frame early here is invisible anyway.
    /// </summary>
    private const float BarFullEpsilon = 0.001f;

    // The ScreenSpaceOverlay canvas the card panel is moved onto, so no world
    // sprite or health bar can ever draw over it.
    private GameObject overlayRoot;

    private readonly List<PlayerStatsApplier> activePlayers = new List<PlayerStatsApplier>();
    private readonly List<EnemyManager> activeEnemies = new List<EnemyManager>();

    // Combat animators halted while the card screen is up, with their original speeds.
    private readonly List<Animator> frozenAnimators = new List<Animator>();
    private readonly List<float> frozenSpeeds = new List<float>();

    // Keyed by UNIT ID, not FighterType: every UnitDefinitionSO in this project is
    // classType = Warrior, so FighterType cannot tell two heroes apart.

    // The multiplier ONE buff currently contributes to one hero. Applying only the
    // ratio new/old is what stops repeated picks of the same card compounding.
    private readonly Dictionary<(int, SkillData), float> skillMultiplier =
        new Dictionary<(int, SkillData), float>();

    // The product of every buff on one (hero, stat). Used to catch up a player
    // that joins the fight after a card has already been taken.
    private readonly Dictionary<(int, SkillEffectType), float> statMultiplier =
        new Dictionary<(int, SkillEffectType), float>();


    private float Threshold => config != null
        ? config.ThresholdFor(level)
        : Mathf.Max(1, initialThreshold + level - 1);

    private bool AtMaxLevel => config != null && config.maxLevel > 0 && level >= config.maxLevel;

    private float Fraction => AtMaxLevel ? 1f : Mathf.Clamp01(xp / Mathf.Max(0.0001f, Threshold));

    // -------------------------------------------------------------- lifecycle

    private void Awake()
    {
        draw = new BuffDraw(config);
    }

    private void OnEnable()
    {
        BattleStartController.OnAnyBattleStarted += BeginBattlePhase;
    }

    private void OnDisable()
    {
        BattleStartController.OnAnyBattleStarted -= BeginBattlePhase;
    }

    private void OnDestroy()
    {
        // The overlay canvas is created at runtime and is not parented to this
        // object, so it has to be cleaned up explicitly on scene change.
        if (overlayRoot != null) Destroy(overlayRoot);
    }

    private void Start()
    {
        HideAllPanels();
        HideLegacyUI();
        EnsurePanelRendersOnTop();
        ShowXpBar();
        ResetProgressionState();

        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = 1f;
            xpSlider.value = 0f;
        }

        // BattleIsRunning defaults to TRUE and is only cleared by a stage that
        // actually has a BattleStartController. So a scene without one behaves
        // exactly as before: the roguelite is live from the start.
        if (BattleStartController.BattleIsRunning)
            BeginBattlePhase();
    }

    private void Update()
    {
        if (xpSlider == null) return;

        float target = Fraction;

        // Unscaled, so the bar still settles while the card screen has the game paused.
        xpDisplay = xpIncreaseDuration <= 0f
            ? target
            : Mathf.MoveTowards(xpDisplay, target, Time.unscaledDeltaTime / xpIncreaseDuration);

        xpSlider.value = xpDisplay;

        if (!pendingLevelUp) return;

        // The hold only starts counting once the fill has actually ARRIVED, so a
        // slow bar cannot serve out its hold while still halfway up.
        if (xpDisplay >= 1f - BarFullEpsilon) barFullTimer += Time.unscaledDeltaTime;
        else barFullTimer = 0f;

        // Assigning the slider BEFORE this call matters: TryLevelUp may zero
        // xpDisplay, and the player has to see the full bar for this frame first.
        TryLevelUp();
    }

    /// <summary>
    /// The battle has begun: snapshot the roster and build the card pool from the
    /// hero types actually on the field, so a card can never target a hero that
    /// is not in this fight.
    /// </summary>
    public void BeginBattlePhase()
    {
        if (running) return;
        running = true;

        RefreshActivePlayers();
        RefreshActiveEnemies();
        ReconfigureDraw();
        UpdateXpUI();
    }

    private void ReconfigureDraw()
    {
        var roster = new List<int>();

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var p = activePlayers[i];
            if (p == null) continue;
            if (!IsBuffableHero(p)) continue;

            if (!roster.Contains(p.unitId)) roster.Add(p.unitId);
        }

        // Safety net. If the DEPLOYED check wiped out everything while real heroes are
        // clearly on the field, trust the field over the save data - an empty roster
        // means no cards at all, which is far worse than the castle bug it guards
        // against. The gate and database rules still apply.
        if (roster.Count == 0)
        {
            for (int i = 0; i < activePlayers.Count; i++)
            {
                var p = activePlayers[i];
                if (p == null) continue;
                if (p.GetComponentInParent<PlayerGateStats>() != null) continue;
                if (unitsDatabase != null && unitsDatabase.GetById(p.unitId) == null) continue;

                if (!roster.Contains(p.unitId)) roster.Add(p.unitId);
            }

            if (roster.Count > 0)
                Debug.LogWarning("[RogueliteManager] No DEPLOYED unit was found on the field, " +
                                 "so the roster fell back to whoever is actually fighting: " +
                                 string.Join(", ", roster) + ". Check the Units-UI deck state.", this);
        }

        if (roster.Count == 0)
            Debug.LogWarning("[RogueliteManager] Battle started with no players on the field - " +
                             "no hero cards can be drawn.", this);

        draw.Configure(roster, skillPool);
    }

    /// <summary>
    /// Moves the XP bar so its TOP EDGE sits xpBarTopMargin below the top of the
    /// screen, just under the HUD resource row.
    ///
    /// The position is MEASURED, not nudged. The bar root is centre-anchored while its
    /// children carry large authored offsets (one sits +928 up), so any fixed offset is
    /// a guess that breaks the moment a scene is authored differently. Reading the real
    /// rendered bounds and correcting the difference always lands in the same place.
    /// </summary>
    private void PlaceXpBarUnderHud()
    {
        if (xpBarRoot == null || xpSlider == null) return;

        var barRT = xpBarRoot.transform as RectTransform;
        if (barRT == null) return;

        var canvas = barRT.GetComponentInParent<Canvas>();
        var canvasRT = canvas != null && canvas.rootCanvas != null
            ? canvas.rootCanvas.transform as RectTransform : null;
        if (canvasRT == null) return;

        // Where the bar actually is right now, in canvas space (origin = centre).
        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
            canvasRT, xpSlider.transform as RectTransform);

        float currentTop = bounds.max.y;
        float desiredTop = canvasRT.rect.height * 0.5f - xpBarTopMargin;

        barRT.anchoredPosition += new Vector2(xpBarHorizontalOffset,
                                              desiredTop - currentTop + xpBarVerticalOffset);
    }

    /// <summary>
    /// The unit ids that may appear on a card RIGHT NOW: deployed, in this battle,
    /// and still ALIVE.
    ///
    /// Recomputed before every draw. A hero type whose units have all died stops
    /// being offered immediately - buffing a corpse is meaningless - and reappears by
    /// itself if the player revives it from the gem panel, because that puts a living
    /// unit of that type back on the field.
    /// </summary>
    private List<int> BuildLivingRoster()
    {
        var roster = new List<int>();

        for (int i = activePlayers.Count - 1; i >= 0; i--)
        {
            var p = activePlayers[i];

            // Destroyed units leave null holes behind.
            if (p == null) { activePlayers.RemoveAt(i); continue; }

            if (!IsBuffableHero(p)) continue;
            if (!IsAlive(p)) continue;

            if (!roster.Contains(p.unitId)) roster.Add(p.unitId);
        }

        return roster;
    }

    private static bool IsAlive(PlayerStatsApplier applier)
    {
        var stats = applier.GetComponentInChildren<PlayerStats>();
        if (stats == null) return true;              // no health component: assume alive

        return !stats.playerIsdead && stats.currentHP > 0f;
    }

    /// <summary>
    /// THE ROSTER RULE. A unit may be buffed only when ALL of these hold:
    ///
    ///   1. it is not the castle / gate - those are structures, not heroes;
    ///   2. its unitId exists in UnitsDatabaseSO;
    ///   3. it is DEPLOYED in the Units-UI deck;
    ///   4. it is actually present in this battle (it is in activePlayers).
    ///
    /// So a deck of 4 that spawned only 3 gives a roster of exactly those 3.
    ///
    /// This exists because PlayerCastle also carries a PlayerStatsApplier, with the
    /// default unitId 0. Without the filter the castle joined the roster as a fifth
    /// "hero": it produced a blank card (id 0 resolves to no definition), it competed
    /// for the three slots, and any buff picked on that card was applied to the castle.
    /// </summary>
    private bool IsBuffableHero(PlayerStatsApplier applier)
    {
        if (applier == null) return false;

        // 1. Never the castle or a gate, whatever id it happens to carry.
        if (applier.GetComponent<PlayerGateStats>() != null ||
            applier.GetComponentInParent<PlayerGateStats>() != null)
            return false;

        // 2. Must be a real unit in the database.
        if (unitsDatabase != null && unitsDatabase.GetById(applier.unitId) == null)
            return false;

        // 3. Must be in the active deck. Skipped only when progression is unavailable
        //    (a bare test scene), so those scenes keep working.
        var gsm = GameStartManager.Instance;
        if (gsm != null && gsm.PlayerUnits != null && !gsm.PlayerUnits.IsDeployed(applier.unitId))
            return false;

        return true;
    }

    // ---------------------------------------------------------------- roster

    private void RefreshActivePlayers()
    {
        var found = FindObjectsOfType<PlayerStatsApplier>();
        for (int i = 0; i < found.Length; i++) RegisterPlayer(found[i]);
    }

    private void RefreshActiveEnemies()
    {
        activeEnemies.Clear();
        activeEnemies.AddRange(FindObjectsOfType<EnemyManager>());
    }

    /// <summary>
    /// Registers a player. Any buff already taken for its hero type is applied
    /// immediately, so a unit that reaches the field late is never under-buffed.
    /// </summary>
    public void RegisterPlayer(PlayerStatsApplier player)
    {
        if (player == null || activePlayers.Contains(player)) return;

        activePlayers.Add(player);
        ApplyAccumulatedTo(player);
    }

    public void RegisterEnemy(EnemyManager enemy)
    {
        if (enemy == null || activeEnemies.Contains(enemy)) return;
        activeEnemies.Add(enemy);
    }

    /// <summary>Called by an EnemyManager as it dies: grants that enemy's XP and drops it.</summary>
    public void NotifyEnemyKilled(EnemyManager enemy)
    {
        AddXP(enemy != null ? enemy.XpValue : 1f);
        if (enemy != null) activeEnemies.Remove(enemy);
    }

    // -------------------------------------------------------------------- XP

    public void AddXP(int amount) => AddXP((float)amount);

    /// <summary>
    /// Adds XP and levels up if the threshold is crossed. The remainder ALWAYS
    /// carries into the next level - the bar is never reset to zero.
    /// </summary>
    public void AddXP(float amount)
    {
        if (!running || AtMaxLevel || amount <= 0f) return;

        // Logical XP is credited immediately and in full. Update() catches the
        // bar up separately, so kills landing faster than the fill animation can
        // never be dropped or double-counted.
        xp += amount;

        OnXpGained?.Invoke(amount / Mathf.Max(0.0001f, Threshold) * 100f);

        TryLevelUp();
    }

    private void TryLevelUp()
    {
        // While the card screen is open the level-up is deferred; OnOfferChosen
        // re-checks, so a single huge kill can still cascade several levels.
        if (isPaused || !running || AtMaxLevel) return;

        float t = Threshold;
        if (xp < t) { pendingLevelUp = false; barFullTimer = 0f; return; }

        // THE BAR HAS TO GET THERE FIRST.
        //
        // Logical XP crossed the threshold this frame, but the fill is a separate,
        // slower animation. Levelling immediately blanked xpDisplay while it was
        // still travelling, so at 2 kills per level the bar was wiped at ~50% and
        // the ONLY thing the player ever saw was the overflow crawling up from
        // zero a few seconds later. It never once rendered between 50% and 100%.
        //
        // So the level-up is now gated on the DISPLAY, not the number: hold here,
        // let Update keep filling, and release once the bar has visibly reached
        // full and sat there for levelUpBarHoldSeconds. The XP itself was already
        // credited in AddXP, so nothing can be lost or double-counted by waiting.
        if (xpIncreaseDuration > 0f &&
            (xpDisplay < 1f - BarFullEpsilon || barFullTimer < levelUpBarHoldSeconds))
        {
            pendingLevelUp = true;
            return;
        }

        pendingLevelUp = false;
        barFullTimer = 0f;

        xp -= t;                    // <- the overflow carries
        level++;
        xpDisplay = 0f;

        UpdateXpUI();
        OnLevelUp?.Invoke(level);

        OpenSkillSelection();
    }

    private void UpdateXpUI()
    {
        if (levelCounterText != null) levelCounterText.text = level.ToString();
    }

    // ------------------------------------------------------------ card screen

    private void OpenSkillSelection()
    {
        int slots = cardSlots != null ? cardSlots.Length : 0;
        if (slots == 0)
        {
            Debug.LogWarning("[RogueliteManager] Level-up with no card slots assigned.", this);
            return;
        }

        // Who is alive RIGHT NOW, not who was alive when the battle started. Stars
        // survive this: UpdateRoster deliberately does not reset progress.
        var living = BuildLivingRoster();
        if (living.Count == 0)
        {
            Debug.LogWarning("[RogueliteManager] Level-up with no living hero to buff - " +
                             "card screen skipped.", this);
            return;
        }

        draw.UpdateRoster(living);

        var offers = draw.Draw(slots);
        if (offers.Count == 0) return;   // everything maxed - skip the screen entirely

        isPaused = true;
        GameplayPause.SetPaused(true);
        FreezeBattlefield(true);

        if (skillSelectPanel != null) skillSelectPanel.SetActive(true);
        if (levelUpOverlay != null) levelUpOverlay.SetActive(true);

        // A previous fade-out may have left the group transparent.
        var group = EnsureCanvasGroup(skillSelectPanel);
        if (group != null)
        {
            group.DOKill();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        // Harmless in the battle phase (the board is already gone), but keeps the
        // behaviour correct for any stage that still shows the board.
        var input = FindObjectOfType<BoardInputController>();
        if (input) input.enabled = false;

        for (int i = 0; i < slots; i++)
        {
            var slot = cardSlots[i];
            if (slot == null) continue;

            bool has = i < offers.Count;
            slot.gameObject.SetActive(has);
            if (has) slot.Init(offers[i], UnitFor(offers[i]), OnOfferChosen);
        }
    }

    private void OnOfferChosen(BuffOffer offer)
    {
        if (!offer.IsValid) return;

        draw.Commit(offer);
        ApplyOffer(offer);

        // The whole screen fades out after the card's pop, then play resumes.
        // TryLevelUp runs only once that is finished, so a cascade of level-ups can
        // never open the next screen on top of one that is still fading out.
        CloseSkillSelection(TryLevelUp);
    }

    private void CloseSkillSelection(Action onClosed = null)
    {
        var group = EnsureCanvasGroup(skillSelectPanel);

        if (group == null || panelFadeDuration <= 0f)
        {
            FinishClosing(null, onClosed);
            return;
        }

        group.DOKill();
        group.blocksRaycasts = false;
        group.interactable = false;

        group.DOFade(0f, panelFadeDuration)
             .SetUpdate(true)                    // unscaled: the game is still paused
             .SetEase(Ease.InQuad)
             .OnComplete(() => FinishClosing(group, onClosed));
    }

    /// <summary>
    /// Hard-stops every combat Animator while the card screen is up, and restores the
    /// speeds afterwards.
    ///
    /// Time.timeScale = 0 alone is not enough: an Animator still evaluates the state
    /// change it was asked for, so a unit that died on the very frame the screen opened
    /// would play its death animation UNDER the cards. Nothing may move once the cards
    /// are showing, so the animators are frozen outright.
    ///
    /// Only PlayerManager and EnemyManager rigs are touched. The card previews are
    /// deliberately left alone - those are UI and must keep idling while paused.
    /// </summary>
    private void FreezeBattlefield(bool freeze)
    {
        if (!freeze)
        {
            for (int i = 0; i < frozenAnimators.Count; i++)
                if (frozenAnimators[i] != null) frozenAnimators[i].speed = frozenSpeeds[i];

            frozenAnimators.Clear();
            frozenSpeeds.Clear();
            return;
        }

        frozenAnimators.Clear();
        frozenSpeeds.Clear();

        foreach (var pm in FindObjectsByType<PlayerManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            CollectAnimators(pm.gameObject);

        foreach (var em in FindObjectsByType<EnemyManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            CollectAnimators(em.gameObject);
    }

    private void CollectAnimators(GameObject root)
    {
        foreach (var a in root.GetComponentsInChildren<Animator>(true))
        {
            if (a == null || frozenAnimators.Contains(a)) continue;

            frozenAnimators.Add(a);
            frozenSpeeds.Add(a.speed);
            a.speed = 0f;
        }
    }

    private void FinishClosing(CanvasGroup group, Action onClosed)
    {
        HideAllPanels();

        // Restored, because the next level-up reuses this same panel.
        if (group != null)
        {
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
        }

        // Play resumes only now, once the screen has finished fading away.
        FreezeBattlefield(false);

        isPaused = false;
        GameplayPause.SetPaused(false);

        var input = FindObjectOfType<BoardInputController>();
        if (input) input.enabled = true;

        onClosed?.Invoke();
    }

    private static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (go == null) return null;

        var group = go.GetComponent<CanvasGroup>();
        if (group == null) group = go.AddComponent<CanvasGroup>();
        return group;
    }

    private void HideAllPanels()
    {
        if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
        if (levelUpOverlay != null) levelUpOverlay.SetActive(false);
    }

    /// <summary>
    /// The Offensive / Defensive icon rows are dead: buffs are tracked per hero now,
    /// not per category, so those rows can only mislead. Hidden rather than deleted
    /// so the change is reversible from the inspector.
    /// </summary>
    private void HideLegacyUI()
    {
        if (legacyObjectsToHide != null)
            for (int i = 0; i < legacyObjectsToHide.Length; i++)
                if (legacyObjectsToHide[i] != null) legacyObjectsToHide[i].SetActive(false);

        // Fallback so all 21 stage scenes are cleaned without wiring each one by
        // hand. The names come from the shared Canvas prefab, so they are stable.
        if (skillSelectPanel == null) return;

        var panel = skillSelectPanel.transform;
        for (int i = 0; i < panel.childCount; i++)
        {
            var child = panel.GetChild(i);
            string n = child.name.Trim();

            bool isCategoryRow = n == "Offencive List Holder" || n == "Defensive List Holder";

            // The stray empty white panel: a bare Image child with nothing inside it.
            bool isEmptyPlate = n == "Image" && child.childCount == 0
                                && child.GetComponent<Image>() != null;

            if (isCategoryRow || isEmptyPlate) child.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Unit and gate health bars are WORLD-SPACE canvases at sortingOrder 500, while
    /// the UI canvas sits at 2 - so without this the card screen renders UNDERNEATH
    /// every health bar on the battlefield. Gameplay must never draw over the UI.
    /// </summary>
    private void EnsurePanelRendersOnTop()
    {
        if (skillSelectPanel == null || overlayRoot != null) return;

        // A NESTED canvas with overrideSorting is not enough here. The scene's UI
        // canvas is ScreenSpaceCamera, which competes with world sprites and
        // WorldSpace health-bar canvases for the same sorting layers - that is why
        // gate bars and unit bars punched through the card screen.
        //
        // ScreenSpaceOverlay is the only mode that renders unconditionally last, and
        // render mode can only be set on a ROOT canvas. So the panel is moved onto
        // one of its own.
        var sourceCanvas = skillSelectPanel.GetComponentInParent<Canvas>();
        var sourceScaler = sourceCanvas != null && sourceCanvas.rootCanvas != null
            ? sourceCanvas.rootCanvas.GetComponent<CanvasScaler>() : null;

        overlayRoot = new GameObject("Roguelite Overlay Canvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = panelSortingOrder;

        // Copy the project's scaling so the cards keep the size they were authored at.
        var scaler = overlayRoot.GetComponent<CanvasScaler>();
        if (sourceScaler != null)
        {
            scaler.uiScaleMode = sourceScaler.uiScaleMode;
            scaler.referenceResolution = sourceScaler.referenceResolution;
            scaler.screenMatchMode = sourceScaler.screenMatchMode;
            scaler.matchWidthOrHeight = sourceScaler.matchWidthOrHeight;
            scaler.referencePixelsPerUnit = sourceScaler.referencePixelsPerUnit;
        }

        // The panel's own backdrop is an opaque grey plate in the scenes. It should
        // read as a dimmer over the battlefield, not a solid box.
        var backdrop = skillSelectPanel.GetComponent<Image>();
        if (backdrop != null) backdrop.color = scrimColor;

        var rt = skillSelectPanel.transform as RectTransform;
        if (rt == null) return;

        rt.SetParent(overlayRoot.transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
    }

    /// <summary>The XP bar root is authored inactive in the stage scenes; switch it on.</summary>
    private void ShowXpBar()
    {
        if (xpBarRoot == null && xpSlider != null)
        {
            // Fall back to the slider's parent chain so no scene wiring is required.
            var t = xpSlider.transform.parent;
            xpBarRoot = t != null ? t.gameObject : xpSlider.gameObject;
        }

        if (xpBarRoot == null) return;

        xpBarRoot.SetActive(true);

        // The bar root holds a SECOND, undriven slider left over from an earlier
        // pass. Switching the root on revealed it sitting at full, which is why the
        // bar read as solid green from the first frame. Only the wired one survives.
        foreach (var s in xpBarRoot.GetComponentsInChildren<Slider>(true))
        {
            if (xpSlider != null && s == xpSlider) continue;
            s.gameObject.SetActive(false);
        }

        PlaceXpBarUnderHud();

        // Start empty, whatever the scene authored.
        if (xpSlider != null)
        {
            xpSlider.minValue = 0f;
            xpSlider.maxValue = 1f;
            xpSlider.value = 0f;
        }

        xpDisplay = 0f;
        pendingLevelUp = false;
        barFullTimer = 0f;
    }

    /// <summary>
    /// The definition behind a card: supplies the portrait AND the animated rig, so
    /// the character idles on the card. Null on a global card.
    /// </summary>
    private UnitDefinitionSO UnitFor(BuffOffer offer)
    {
        if (offer.isGlobal || unitsDatabase == null) return null;
        return unitsDatabase.GetById(offer.unitId);
    }

    // ------------------------------------------------------------- applying

    private void ApplyOffer(BuffOffer offer)
    {
        float newMultiplier = 1f + offer.increment;

        if (offer.isGlobal)
        {
            var roster = draw.Roster;
            for (int i = 0; i < roster.Count; i++)
                ApplyToHero(roster[i], offer.skill, newMultiplier);
        }
        else
        {
            ApplyToHero(offer.unitId, offer.skill, newMultiplier);
        }
    }

    private void ApplyToHero(int unitId, SkillData skill, float newMultiplier)
    {
        var skillKey = (unitId, skill);
        float oldMultiplier = skillMultiplier.TryGetValue(skillKey, out float m) ? m : 1f;

        // Only the DELTA is applied, so taking the same buff again moves the stat
        // from its old total to the new one instead of stacking multiplicatively.
        float factor = oldMultiplier > 0f ? newMultiplier / oldMultiplier : newMultiplier;
        skillMultiplier[skillKey] = newMultiplier;

        var statKey = (unitId, skill.effectType);
        statMultiplier[statKey] =
            (statMultiplier.TryGetValue(statKey, out float s) ? s : 1f) * factor;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var p = activePlayers[i];
            if (p == null || p.CurrentStats == null) continue;
            if (p.unitId != unitId) continue;

            ApplyFactor(p, skill.effectType, factor);
        }
    }

    private void ApplyAccumulatedTo(PlayerStatsApplier player)
    {
        if (player == null || player.CurrentStats == null || statMultiplier.Count == 0) return;

        foreach (SkillEffectType stat in Enum.GetValues(typeof(SkillEffectType)))
        {
            if (!statMultiplier.TryGetValue((player.unitId, stat), out float f)) continue;
            if (Mathf.Approximately(f, 1f)) continue;

            ApplyFactor(player, stat, f);
        }
    }

    private static void ApplyFactor(PlayerStatsApplier player, SkillEffectType stat, float factor)
    {
        var s = player.CurrentStats;
        if (s == null || Mathf.Approximately(factor, 1f)) return;

        switch (stat)
        {
            case SkillEffectType.AttackSpeed:
                s.ApplyMultipliers(atkSpdMult: factor);
                break;

            case SkillEffectType.AttackDamage:
                s.ApplyMultipliers(atkMult: factor);
                break;

            case SkillEffectType.Health:
                s.ApplyMultipliers(hpMult: factor);
                ScaleLiveHealth(player, factor);
                break;

            case SkillEffectType.Defense:
                s.ApplyMultipliers(defMult: factor);
                break;

            case SkillEffectType.MoveSpeed:
                s.ApplyMultipliers(moveMult: factor);
                break;
        }
    }

    /// <summary>
    /// Raising maxHP on the runtime stats alone would do nothing visible - the
    /// live health component holds its own copy, so it is scaled by the same
    /// factor. Current HP rises too, which is the point of an in-battle HP buff.
    /// </summary>
    private static void ScaleLiveHealth(PlayerStatsApplier player, float factor)
    {
        var health = player.GetComponentInChildren<PlayerStats>();
        if (health == null) return;

        health.maxHealth *= factor;
        health.currentHP = Mathf.Min(health.currentHP * factor, health.maxHealth);
    }

    // ----------------------------------------------------------------- reset

    /// <summary>
    /// Wipes every per-stage buff. The Units-UI upgrade level is untouched - that
    /// lives in PlayerStatsApplier / the save file, not here.
    /// </summary>
    public void ResetForNewStage()
    {
        ResetProgressionState();

        if (running) ReconfigureDraw();
        else draw.Reset();

        UpdateXpUI();
    }

    private void ResetProgressionState()
    {
        level = 1;
        xp = 0f;
        xpDisplay = 0f;
        pendingLevelUp = false;
        barFullTimer = 0f;

        skillMultiplier.Clear();
        statMultiplier.Clear();

        if (xpSlider != null) xpSlider.value = 0f;
        UpdateXpUI();
    }
}
