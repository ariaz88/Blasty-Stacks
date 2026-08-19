using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls XP progression, level-ups, skill selection, and applies skill effects.
/// Supports multiple player characters and dynamic enemy tracking.
/// </summary>
public class RogueliteManager : MonoBehaviour
{
    [Header("XP Progression")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TextMeshProUGUI levelCounterText;
    [SerializeField] private int initialThreshold = 1;

    // Keep a reference to a running animation so we can stop it
    private Coroutine xpAnimationCoroutine;
    [SerializeField] private float xpIncreaseDuration = 0.5f; // seconds


    private int currentThreshold;
    private int currentXP;
    private int levelCounter;

    [Header("Skill Pool")]
    [SerializeField] private List<SkillData> skillPool;
    private readonly int evolveThreshold = 6; // sixth pick triggers the final evolution


    private Dictionary<SkillData, int> skillPickCounts = new Dictionary<SkillData, int>();
    private Dictionary<SkillData, float> skillCurrentMultipliers = new Dictionary<SkillData, float>();

    [Header("UI References")]
    [SerializeField] private GameObject levelUpOverlay;
    [SerializeField] private GameObject skillSelectPanel;
    [SerializeField] private SkillCardUI[] cardSlots;
    [SerializeField] private Image[] offensiveSlots;
    [SerializeField] private Image[] defensiveSlots;

    private bool isPaused;

    // Dynamic player and enemy lists
    // These collections track all players and enemies currently active in the scene.  They are
    // populated automatically at game start via FindObjectsOfType and updated when new
    // characters spawn or enemies die.  Keeping these lists up to date allows the manager
    // to apply roguelite effects only to present characters and award XP accurately.
    private List<PlayerStatsApplier> activePlayers = new List<PlayerStatsApplier>();
    private List<EnemyManager> activeEnemies = new List<EnemyManager>();
    // In RogueliteManager, add a new field:
    private Dictionary<SkillData, Image> skillToSlot = new Dictionary<SkillData, Image>();

    private void Start()
    {
        // Initialize XP values and the level counter.  The XP bar will fill to
        // 'initialThreshold' before the first level‑up.  After each level‑up the
        // threshold increases by one.
        currentThreshold = initialThreshold;
        currentXP = 0;
        levelCounter = 0;
        xpSlider.maxValue = currentThreshold;
        xpSlider.value = currentXP;
        levelCounterText.text = levelCounter.ToString();

        // Initialize the dictionaries that track how many times each skill has
        // been selected and what multiplier is currently applied.  Each skill
        // starts with zero picks and a multiplier of 1.0 (no bonus).
        foreach (var skill in skillPool)
        {
            skillPickCounts[skill] = 0;
            skillCurrentMultipliers[skill] = 1f;
        }

        // Hide the level‑up and skill selection overlays at the beginning of the run.
        if (levelUpOverlay != null) levelUpOverlay.SetActive(false);
        if (skillSelectPanel != null) skillSelectPanel.SetActive(false);


        foreach (var slot in offensiveSlots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }
        foreach (var slot in defensiveSlots)
        {
            if (slot != null) slot.gameObject.SetActive(false);
        }


        // Populate the active player and enemy lists.  These calls find all
        // PlayerStatsApplier and EnemyManager components that currently exist in the scene.
        // If additional characters or enemies spawn later, they should register
        // themselves with this manager via RegisterPlayer and RegisterEnemy.
        UpdateActivePlayers();
        UpdateActiveEnemies();
    }

    /// <summary>
    /// Refreshes the list of active player characters in the scene.  This method
    /// uses FindObjectsOfType to locate every PlayerStatsApplier currently
    /// instantiated and replaces the existing list.  Call this at game start
    /// and whenever new characters spawn if the spawn system does not call
    /// RegisterPlayer.
    /// </summary>
    private void UpdateActivePlayers()
    {
        activePlayers.Clear();
        activePlayers.AddRange(FindObjectsOfType<PlayerStatsApplier>());
    }

    /// <summary>
    /// Refreshes the list of active enemies.  This method finds all
    /// EnemyManager components currently present and rebuilds the active list.
    /// It can be called at the beginning of a stage or whenever a major
    /// wave of enemies has spawned.  Individual spawners should call
    /// RegisterEnemy and NotifyEnemyKilled when appropriate.
    /// </summary>
    private void UpdateActiveEnemies()
    {
        activeEnemies.Clear();
        activeEnemies.AddRange(FindObjectsOfType<EnemyManager>());
    }

    /// <summary>
    /// Registers a newly spawned player character.
    /// Should be called by the character spawn system.
    /// </summary>
    public void RegisterPlayer(PlayerStatsApplier player)
    {
        if (!activePlayers.Contains(player))
        {
            activePlayers.Add(player);
        }
    }

    /// <summary>
    /// Registers a newly spawned enemy.
    /// Should be called by the enemy spawner.
    /// </summary>
    public void RegisterEnemy(EnemyManager enemy)
    {
        if (!activeEnemies.Contains(enemy))
        {
            activeEnemies.Add(enemy);
        }
    }

    /// <summary>
    /// Called by an EnemyManager when it dies to award XP and remove it from the list.
    /// </summary>
    public void NotifyEnemyKilled(EnemyManager enemy)
    {
        AddXP(1);
        activeEnemies.Remove(enemy);
    }

    /// <summary>
    /// Adds XP to the bar. Call this whenever an enemy dies (via NotifyEnemyKilled).
    /// </summary>
    public void AddXP1(int amount)
    {
        if (isPaused) return;
        currentXP += amount;
        if (currentXP >= currentThreshold)
        {
            LevelUp();
        }
        else
        {
            xpSlider.value = currentXP;
        }
    }

    public void AddXP(int amount)
    {
        if (isPaused) return;

        // Calculate the target XP after adding
        int startXP = currentXP;
        currentXP += amount;

        // If we're about to hit or exceed the threshold, cap the XP at the threshold for animation
        int targetXP = Mathf.Min(currentXP, currentThreshold);

        // Stop any ongoing animation before starting a new one
        if (xpAnimationCoroutine != null)
        {
            StopCoroutine(xpAnimationCoroutine);
        }
        // Start animating the slider from its current value to the target
        xpAnimationCoroutine = StartCoroutine(AnimateXpBar(startXP, targetXP));

        // If we actually reached the threshold, schedule the LevelUp call to happen after the bar finishes animating
        if (currentXP >= currentThreshold)
        {
            StartCoroutine(DelayedLevelUp());
        }
    }

    // Coroutine that interpolates xpSlider.value from 'fromXP' to 'toXP' over xpIncreaseDuration
    private IEnumerator AnimateXpBar(int fromXP, int toXP)
    {
        float elapsed = 0f;
        while (elapsed < xpIncreaseDuration)
        {
            elapsed += Time.unscaledDeltaTime; // use unscaled time so pausing doesn't freeze animation
            float t = elapsed / xpIncreaseDuration;
            xpSlider.value = Mathf.Lerp(fromXP, toXP, t);
            yield return null;
        }
        xpSlider.value = toXP; // ensure exact value at the end
    }

    // Coroutine that waits for the animation to finish before calling LevelUp
    private IEnumerator DelayedLevelUp()
    {
        yield return new WaitForSecondsRealtime(xpIncreaseDuration);
        LevelUp();
    }


    /// <summary>
    /// Handles leveling up: resets XP, increments threshold,
    /// pauses gameplay, and triggers the skill selection UI.
    /// Also refreshes the player list in case new characters have spawned.
    /// </summary>
    private void LevelUp()
    {
        currentXP = 0;
        levelCounter++;
        currentThreshold++;
        xpSlider.maxValue = currentThreshold;
        xpSlider.value = currentXP;
        levelCounterText.text = levelCounter.ToString();

        // Pause gameplay
        isPaused = true;
        Time.timeScale = 0f;

        //if (levelUpOverlay != null) levelUpOverlay.SetActive(true);
        if (skillSelectPanel != null) skillSelectPanel.SetActive(true);


        // Update players list in case new characters spawned
        UpdateActivePlayers();

        var input = FindObjectOfType<BoardInputController>();
        if (input) input.enabled = false;

        ShowSkillSelection();
    }

    /// <summary>
    /// Randomly picks three skills from the pool and populates the card slots.
    /// </summary>
    private void ShowSkillSelection()
    {
        List<SkillData> available = new List<SkillData>(skillPool);
        for (int i = 0; i < cardSlots.Length; i++)
        {
            if (available.Count == 0) break;
            int index = Random.Range(0, available.Count);
            SkillData skill = available[index];
            available.RemoveAt(index);

            int timesSelected = skillPickCounts[skill];
            cardSlots[i].Init(skill, timesSelected, OnSkillChosen);
        }

        //if (levelUpOverlay != null) levelUpOverlay.SetActive(false);
        //if (skillSelectPanel != null) skillSelectPanel.SetActive(true);
    }

    /// <summary>
    /// Called when a card is chosen. Applies the effect, updates UI, and resumes gameplay.
    /// </summary>
    private void OnSkillChosen(SkillData skill)
    {
        if (skillSelectPanel != null) skillSelectPanel.SetActive(false);

        // Update selection count and apply effect
        skillPickCounts[skill]++;
        ApplySkillEffect(skill, skillPickCounts[skill]);

        // Show the skill’s icon in the appropriate list
        AddSkillToList(skill);

        // If we've just evolved this skill, remove it from the pool so it won't appear again
        if (skillPickCounts[skill] >= evolveThreshold)
        {
            skillPool.Remove(skill);
        }


        // Resume gameplay
        isPaused = false;
        Time.timeScale = 1f;

        var input = FindObjectOfType<BoardInputController>();
        if (input) input.enabled = true;
    }

    /// <summary>
    /// Adds the selected skill’s icon to the next empty slot in its category list.
    /// Shows the evolved icon if picked 5+ times.
    /// </summary>
    private void AddSkillToList1(SkillData skill)
    {
        Image[] list = (skill.category == SkillCategory.Offensive) ? offensiveSlots : defensiveSlots;
        for (int i = 0; i < list.Length; i++)
        {
            if (!list[i].gameObject.activeSelf)
            {
                list[i].sprite = skillPickCounts[skill] >= 5 ? skill.evolvedIcon : skill.normalIcon;
                list[i].gameObject.SetActive(true);
                break;
            }
        }
    }
    private void AddSkillToList(SkillData skill)
    {
        Image[] list = (skill.category == SkillCategory.Offensive) ? offensiveSlots : defensiveSlots;

        // If the skill is already in a list, just update its sprite for the new level (normal → evolved)
        if (skillToSlot.TryGetValue(skill, out var existingSlot) && existingSlot != null)
        {
            existingSlot.sprite = (skillPickCounts[skill] > 5 ? skill.evolvedIcon : skill.normalIcon);
            return;
        }

        // Otherwise find the first empty slot and assign the skill
        foreach (var slot in list)
        {
            if (!slot.gameObject.activeSelf)
            {
                slot.sprite = (skillPickCounts[skill] > 5 ? skill.evolvedIcon : skill.normalIcon);
                slot.gameObject.SetActive(true);
                skillToSlot[skill] = slot;
                break;
            }
        }
    }


    /// <summary>
    /// Applies the selected skill’s effect across all active players, respecting allowed fighter types.
    /// Tracks multipliers to avoid compounding errors on repeated picks.
    /// </summary>
    private void ApplySkillEffect(SkillData skill, int timesSelected)
    {
        // Determine the new total increment (e.g. +20% after 4 picks, +50% when evolved)
        float totalIncrement = (timesSelected < 6)
            ? Mathf.Min(timesSelected * skill.baseIncrement, skill.maxIncrement)
            : skill.evolvedIncrement;
        float newMultiplier = 1f + totalIncrement;

        // Look up the previously applied multiplier (default 1.0)
        float currentMultiplier = skillCurrentMultipliers[skill];
        float factor = newMultiplier / currentMultiplier;

        // Apply to all currently active players that are of allowed type
        foreach (var applier in activePlayers)
        {
            UnitStatsRuntime stats = applier.CurrentStats;
            if (stats == null) continue;

            // Check allowed fighter types
            if (skill.allowedTypes != null && skill.allowedTypes.Length > 0)
            {
                bool allowed = false;
                foreach (var t in skill.allowedTypes)
                {
                    if (stats.type == t) { allowed = true; break; }
                }
                if (!allowed) continue;
            }

            // Apply the computed factor to the appropriate stat
            switch (skill.effectType)
            {
                case SkillEffectType.AttackSpeed:
                    stats.ApplyMultipliers(atkSpdMult: factor);
                    break;
                case SkillEffectType.AttackDamage:
                    stats.ApplyMultipliers(atkMult: factor);
                    break;
                case SkillEffectType.Health:
                    stats.ApplyMultipliers(hpMult: factor);
                    break;
            }
        }

        // Store the new multiplier for next time this skill is selected
        skillCurrentMultipliers[skill] = newMultiplier;
    }

    /// <summary>
    /// Resets XP, thresholds, and clears skill selections at the start of a new stage.
    /// Call this when advancing to the next level.
    /// </summary>
    public void ResetForNewStage()
    {
        currentThreshold = initialThreshold;
        currentXP = 0;
        levelCounter = 0;
        xpSlider.maxValue = currentThreshold;
        xpSlider.value = currentXP;
        levelCounterText.text = levelCounter.ToString();

        foreach (var key in new List<SkillData>(skillPickCounts.Keys))
        {
            skillPickCounts[key] = 0;
            skillCurrentMultipliers[key] = 1f;
        }

        foreach (var slot in offensiveSlots)
        {
            slot.gameObject.SetActive(false);
        }
        foreach (var slot in defensiveSlots)
        {
            slot.gameObject.SetActive(false);
        }

        // Optionally refresh player and enemy lists on new stage
        UpdateActivePlayers();
        UpdateActiveEnemies();
    }
}


