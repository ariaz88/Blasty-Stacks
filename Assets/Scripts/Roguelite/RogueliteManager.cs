using System;
using System.Collections.Generic;
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
    [SerializeField] private Image[] offensiveSlots;
    [SerializeField] private Image[] defensiveSlots;

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

    private readonly List<PlayerStatsApplier> activePlayers = new List<PlayerStatsApplier>();
    private readonly List<EnemyManager> activeEnemies = new List<EnemyManager>();

    // The multiplier ONE buff currently contributes to one hero. Applying only the
    // ratio new/old is what stops repeated picks of the same card compounding.
    private readonly Dictionary<(FighterType, SkillData), float> skillMultiplier =
        new Dictionary<(FighterType, SkillData), float>();

    // The product of every buff on one (hero, stat). Used to catch up a player
    // that joins the fight after a card has already been taken.
    private readonly Dictionary<(FighterType, SkillEffectType), float> statMultiplier =
        new Dictionary<(FighterType, SkillEffectType), float>();

    private readonly Dictionary<SkillData, Image> skillToSlot = new Dictionary<SkillData, Image>();

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

    private void Start()
    {
        HideAllPanels();
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
        var roster = new List<FighterType>();

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var p = activePlayers[i];
            if (p == null || p.CurrentStats == null) continue;

            var t = p.CurrentStats.type;
            if (!roster.Contains(t)) roster.Add(t);
        }

        if (roster.Count == 0)
            Debug.LogWarning("[RogueliteManager] Battle started with no players on the field - " +
                             "no hero cards can be drawn.", this);

        draw.Configure(roster, skillPool);
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
        if (xp < t) return;

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

        var offers = draw.Draw(slots);
        if (offers.Count == 0) return;   // everything maxed - skip the screen entirely

        isPaused = true;
        GameplayPause.SetPaused(true);

        if (skillSelectPanel != null) skillSelectPanel.SetActive(true);
        if (levelUpOverlay != null) levelUpOverlay.SetActive(true);

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
            if (has) slot.Init(offers[i], PortraitFor(offers[i]), OnOfferChosen);
        }
    }

    private void OnOfferChosen(BuffOffer offer)
    {
        if (!offer.IsValid) return;

        draw.Commit(offer);
        ApplyOffer(offer);
        AddSkillToList(offer.skill);

        CloseSkillSelection();

        // A single elite kill can be worth more than one level.
        TryLevelUp();
    }

    private void CloseSkillSelection()
    {
        HideAllPanels();

        isPaused = false;
        GameplayPause.SetPaused(false);

        var input = FindObjectOfType<BoardInputController>();
        if (input) input.enabled = true;
    }

    private void HideAllPanels()
    {
        if (skillSelectPanel != null) skillSelectPanel.SetActive(false);
        if (levelUpOverlay != null) levelUpOverlay.SetActive(false);
    }

    private Sprite PortraitFor(BuffOffer offer)
    {
        if (offer.isGlobal || unitsDatabase == null) return null;

        var units = unitsDatabase.Units;
        for (int i = 0; i < units.Count; i++)
        {
            var u = units[i];
            if (u != null && u.classType == offer.hero) return u.portrait;
        }

        return null;
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
            ApplyToHero(offer.hero, offer.skill, newMultiplier);
        }
    }

    private void ApplyToHero(FighterType hero, SkillData skill, float newMultiplier)
    {
        var skillKey = (hero, skill);
        float oldMultiplier = skillMultiplier.TryGetValue(skillKey, out float m) ? m : 1f;

        // Only the DELTA is applied, so taking the same buff again moves the stat
        // from its old total to the new one instead of stacking multiplicatively.
        float factor = oldMultiplier > 0f ? newMultiplier / oldMultiplier : newMultiplier;
        skillMultiplier[skillKey] = newMultiplier;

        var statKey = (hero, skill.effectType);
        statMultiplier[statKey] =
            (statMultiplier.TryGetValue(statKey, out float s) ? s : 1f) * factor;

        for (int i = 0; i < activePlayers.Count; i++)
        {
            var p = activePlayers[i];
            if (p == null || p.CurrentStats == null) continue;
            if (p.CurrentStats.type != hero) continue;

            ApplyFactor(p, skill.effectType, factor);
        }
    }

    private void ApplyAccumulatedTo(PlayerStatsApplier player)
    {
        if (player == null || player.CurrentStats == null || statMultiplier.Count == 0) return;

        var type = player.CurrentStats.type;

        foreach (SkillEffectType stat in Enum.GetValues(typeof(SkillEffectType)))
        {
            if (!statMultiplier.TryGetValue((type, stat), out float f)) continue;
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

    // ------------------------------------------------------------- HUD icons

    /// <summary>
    /// Drops the picked buff's icon into its category row, or swaps it to the
    /// evolved art once any hero has maxed it.
    /// </summary>
    private void AddSkillToList(SkillData skill)
    {
        if (skill == null) return;

        bool maxed = draw.TotalPicks(skill) >= skill.MaxStars;
        var art = maxed && skill.evolvedIcon != null ? skill.evolvedIcon : skill.normalIcon;

        if (skillToSlot.TryGetValue(skill, out var existing) && existing != null)
        {
            existing.sprite = art;
            return;
        }

        var row = skill.category == SkillCategory.Offensive ? offensiveSlots : defensiveSlots;
        if (row == null) return;

        for (int i = 0; i < row.Length; i++)
        {
            var slot = row[i];
            if (slot == null || slot.gameObject.activeSelf) continue;

            slot.sprite = art;
            slot.gameObject.SetActive(true);
            skillToSlot[skill] = slot;
            return;
        }
    }

    private void ClearIconRows()
    {
        skillToSlot.Clear();
        SetRowInactive(offensiveSlots);
        SetRowInactive(defensiveSlots);
    }

    private static void SetRowInactive(Image[] row)
    {
        if (row == null) return;

        for (int i = 0; i < row.Length; i++)
            if (row[i] != null) row[i].gameObject.SetActive(false);
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

        skillMultiplier.Clear();
        statMultiplier.Clear();
        ClearIconRows();

        if (xpSlider != null) xpSlider.value = 0f;
        UpdateXpUI();
    }
}
