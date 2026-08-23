using DG.Tweening;
using UnityEngine;

/// <summary>
/// The "your army is collapsing - want one more hero?" prompt.
///
/// Watches the WHOLE roster rather than one type: once <see cref="deadFraction"/>
/// of everyone who started the battle is dead, it shows a single buy button for
/// a designer-chosen hero. Paying spawns that hero through the EXACT same path a
/// bought-back squad uses - PlayerWaveManager.SpawnReinforcements - so it lands
/// on a castle gate, poses there with no HP bar, and jumps into the rear rank.
/// None of that arrival logic is duplicated here.
///
/// Distinct from HeroStatsPanel, which offers a per-type buy-back and only when
/// that ONE type is wiped out. This fires on the army as a whole and offers a
/// hero the designer picked for the stage, wiped or not.
///
/// SHOWS ONCE PER BATTLE. Buying it, or simply having shown it, retires it for
/// the rest of the stage.
/// </summary>
public class LastStandOffer : MonoBehaviour
{
    [Header("The Offer")]
    [Tooltip("WHICH hero this stage offers. Set per level - this is the one field " +
             "you have to fill in for the feature to do anything. Its runtimePrefab " +
             "is what actually gets spawned.")]
    [SerializeField] private UnitDefinitionSO offeredUnit;

    [Tooltip("Gems charged for the hero. Left at 0 = fall back to the unit's own " +
             "UnitDefinitionSO.respawnGemCost.")]
    [SerializeField, Min(0)] private int gemCost = 200;

    [Tooltip("How many heroes arrive for that price.")]
    [SerializeField, Min(1)] private int heroesPerPurchase = 1;

    [Header("Trigger")]
    [Tooltip("Fraction of the STARTING army that must be dead before the offer " +
             "appears. 0.8 = show once 80% have fallen (5 of 6, 4 of 5, ...).")]
    [SerializeField, Range(0.1f, 1f)] private float deadFraction = 0.8f;

    [Header("Presentation")]
    [Tooltip("Where the offer cell is parented. Left empty = this GameObject.")]
    [SerializeField] private RectTransform offerContainer;

    [Tooltip("The HeroStatCell to clone for the offer's look. Point this at the " +
             "same authored 'Hero 1' template the Heroes Stats panel uses - the " +
             "clone is shown in OFFER mode (portrait in colour, no count).")]
    [SerializeField] private HeroStatCell cellTemplate;

    [Header("Attention Pulse")]
    [Tooltip("Breathe the whole offer between full size and this fraction of it, " +
             "forever, to pull the eye. 1 = no pulse.")]
    [SerializeField, Range(0.5f, 1f)] private float pulseScale = 0.8f;

    [Tooltip("Seconds for ONE direction of the breath, so a full cycle is twice " +
             "this. 0.75 reads as a calm heartbeat; below ~0.4 starts to nag.")]
    [SerializeField, Min(0.05f)] private float pulseDuration = 0.75f;

    [Header("Refs (left empty = found in the scene)")]
    [SerializeField] private PlayerWaveManager waveManager;

    private HeroStatCell offerCell;
    private Tween pulse;

    // One shot per battle. Set the moment the offer is SHOWN, not when it is
    // bought - a player who ignores it does not get asked again.
    private bool spent;

    // Nothing is evaluated until THIS scene's battle actually starts.
    //
    // Not merely defensive: HeroRoster is static and survives a scene reload, so
    // replaying a stage used to leave last battle's StartingCount in place while
    // TotalAlive was still 0 - the ratio read as "army wiped out" and the offer
    // appeared during the puzzle phase. HeroRoster now clears itself on scene
    // load, and this flag is the second lock: an event that FIRED IN THIS SCENE
    // cannot be inherited from the last one, however the statics behave.
    private bool armed;

    private void Awake()
    {
        if (!offerContainer) offerContainer = transform as RectTransform;
        if (!waveManager) waveManager = FindObjectOfType<PlayerWaveManager>(true);

        if (!cellTemplate)
            Debug.LogError("[LastStandOffer] No cell template - assign the authored " +
                           "'Hero 1' object (it needs a HeroStatCell component).", this);

        if (!offeredUnit)
            Debug.LogError("[LastStandOffer] No offeredUnit - this stage will never " +
                           "show the last-stand offer. Assign a UnitDefinitionSO.", this);
    }

    private void OnEnable()
    {
        BattleStartController.OnAnyBattleStarted += HandleBattleStarted;
        HeroRoster.OnRosterChanged += Evaluate;
        LevelGameManager.OnGameStateChanged += HandleGameStateChanged;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;

        Evaluate();
    }

    private void OnDisable()
    {
        BattleStartController.OnAnyBattleStarted -= HandleBattleStarted;
        HeroRoster.OnRosterChanged -= Evaluate;
        LevelGameManager.OnGameStateChanged -= HandleGameStateChanged;

        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    /// <summary>
    /// Deliberately NOT read from BattleStartController.BattleIsRunning in
    /// OnEnable. That flag is a static too, defaults to true, and is only set
    /// false in the new stage's Awake - so a component enabling before it would
    /// see the PREVIOUS stage's "yes, running" and arm during the puzzle phase.
    /// The event cannot lie about which scene it came from.
    /// </summary>
    private void HandleBattleStarted()
    {
        armed = true;
        Evaluate();
    }

    /// <summary>
    /// The whole trigger. Called on every roster change, which covers both halves
    /// of the ratio: heroes dying drives it down, a purchase landing drives it
    /// back up.
    /// </summary>
    private void Evaluate()
    {
        if (!armed) return;
        if (spent || offerCell) return;
        if (!offeredUnit || !cellTemplate) return;

        // Zero until SnapshotStartingCounts runs at battle start, which is what
        // keeps this inert through the whole puzzle phase - no explicit
        // "has the battle begun" check needed.
        int starting = HeroRoster.TotalStarting();
        if (starting <= 0) return;

        // Compared as "few enough left" rather than "enough dead" so the maths
        // stays honest when a reinforcement pushes TotalAlive above the
        // snapshot: 80% dead of 6 means 1 or fewer still standing.
        if (HeroRoster.TotalAlive() > starting * (1f - deadFraction)) return;

        Show();
    }

    private void Show()
    {
        if (!waveManager || !waveManager.CanSpawnReinforcements(offeredUnit.unitId))
        {
            // Checked BEFORE the offer is ever shown, so a stage with a broken
            // UnitDefinitionSO simply stays quiet instead of taking gems and
            // delivering nothing.
            Debug.LogError($"[LastStandOffer] '{offeredUnit.displayName}' cannot be " +
                           "spawned (no PlayerWaveManager, or no runtimePrefab) - " +
                           "the offer stays hidden.", this);
            spent = true;
            return;
        }

        // Retired here, not in HandleBuy: ignoring the offer must not leave it
        // re-triggering on every subsequent death.
        spent = true;

        offerCell = Instantiate(cellTemplate, offerContainer);
        offerCell.gameObject.SetActive(true);
        offerCell.name = $"LastStandOffer_{offeredUnit.displayName}";

        // squadSize doubles as "how many this buys" - Bind stores it and
        // SpawnReinforcements reads it back as the count.
        offerCell.Bind(offeredUnit.unitId, offeredUnit, heroesPerPurchase, ResolveGemCost(), HandleBuy);
        offerCell.ShowAsOffer();

        StartPulse();
        RefreshAffordable();
    }

    /// <summary>
    /// The looping "look at me" breath on the whole offer.
    ///
    /// Yoyo from the cell's AUTHORED scale rather than from Vector3.one, so a
    /// template the designer sized to anything other than 1 still breathes around
    /// its own size instead of snapping to full.
    /// </summary>
    private void StartPulse()
    {
        if (!offerCell || pulseScale >= 1f) return;

        var t = offerCell.transform;
        Vector3 full = t.localScale;

        // Unscaled: a pause menu sets timeScale to 0, and an offer frozen
        // mid-breath reads as a hung UI rather than a paused one.
        pulse = t.DOScale(full * pulseScale, pulseDuration)
                 .SetEase(Ease.InOutSine)      // ease BOTH ends - a linear yoyo ticks
                 .SetLoops(-1, LoopType.Yoyo)
                 .SetUpdate(true);
    }

    private void StopPulse()
    {
        if (pulse == null) return;

        pulse.Kill();
        pulse = null;
    }

    private int ResolveGemCost()
    {
        if (gemCost > 0) return gemCost;

        return offeredUnit.respawnGemCost > 0 ? offeredUnit.respawnGemCost : 0;
    }

    private void HandleBuy(HeroStatCell cell)
    {
        if (!cell || !waveManager) return;

        // Re-checked at the moment of purchase, not just at Show: the stage may
        // have ended while the offer sat on screen.
        if (!waveManager.CanSpawnReinforcements(cell.UnitId)) return;

        var currency = CurrencyManager.Instance;
        if (currency == null)
        {
            Debug.LogWarning("[LastStandOffer] No CurrencyManager - the hero is free.", this);
        }
        else if (!currency.TrySpendGems(cell.GemCost))
        {
            Debug.Log($"[LastStandOffer] Not enough gems: need {cell.GemCost}, have {currency.Gems}.");
            RefreshAffordable();
            return;
        }

        // The one line this whole feature exists to reach. Gate pose, HP bar
        // hidden, 0.75s hold, jump into the rear rank - all of it already lives
        // in PlayerWaveManager and is shared with the per-type buy-back.
        waveManager.SpawnReinforcements(cell.UnitId, cell.SquadSize);

        Hide();
    }

    private void Hide()
    {
        // Killed BEFORE the Destroy: a live tween holding a destroyed Transform
        // throws on its next step.
        StopPulse();

        if (!offerCell) return;

        Destroy(offerCell.gameObject);
        offerCell = null;
    }

    // Covers the paths Hide does not: the scene unloading, or this object being
    // switched off, while the offer is still on screen.
    private void OnDestroy() => StopPulse();

    /// <summary>Greys the price out while the player cannot afford it.</summary>
    private void RefreshAffordable()
    {
        if (!offerCell) return;

        int gems = CurrencyManager.Instance != null ? CurrencyManager.Instance.Gems : int.MaxValue;
        offerCell.SetAffordable(gems >= offerCell.GemCost);
    }

    private void HandleCurrencyChanged(string currency, int newValue, int delta)
    {
        if (currency == "Gems") RefreshAffordable();
    }

    /// <summary>
    /// Win, lose or revive-pending: pull the offer off screen so it cannot sit on
    /// top of the end-of-battle panels.
    /// </summary>
    private void HandleGameStateChanged(LevelGameManager.GameState state)
    {
        if (state != LevelGameManager.GameState.Playing) Hide();
    }
}
