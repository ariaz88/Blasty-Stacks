// BattleStartController.cs
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the BATTLE button in the Feature Panel.
///
/// New level flow: the stage opens in a puzzle-only phase - the EnemySpawner
/// sits idle (its waitForBattleStart flag is ON) and nothing attacks. Pressing
/// BATTLE charges the player's daily allowance through BattleEnergyService and,
/// if that succeeds, releases the spawner for the rest of the stage.
///
/// The allowance is global (25 battles per rolling 24h by default), so it is
/// NOT reset by reloading the scene or restarting the app.
/// </summary>
public class BattleStartController : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("The BATTLE button. Left empty = a Button on this GameObject is used.")]
    [SerializeField] private Button battleButton;

    [Tooltip("The stage's EnemySpawner. Left empty = found in the scene at Awake.")]
    [SerializeField] private EnemySpawner enemySpawner;

    [Tooltip("Camera pan + board hide. Left empty = found in the scene at Awake. " +
             "If there is none, the battle starts immediately with no transition.")]
    [SerializeField] private BattlePhaseTransition transition;

    [Header("Optional UI")]
    [Tooltip("The cost label under the BATTLE icon (shows '25').")]
    [SerializeField] private TMP_Text energyCostText;

    [Tooltip("Optional 'battles left today' label. Safe to leave empty.")]
    [SerializeField] private TMP_Text remainingBattlesText;

    [Header("Daily Allowance")]
    [Tooltip("Free battles inside a rolling 24h window, shared across every stage.")]
    [SerializeField, Min(1)] private int dailyBattleLimit = BattleEnergyService.DefaultDailyBattleLimit;

    [Tooltip("PLACEHOLDER: energy charged per battle once the free allowance is spent.")]
    [SerializeField, Min(0)] private int energyCostPerBattle = BattleEnergyService.DefaultEnergyCostPerBattle;

    [Header("Behaviour")]
    [Tooltip("Hide the BATTLE button once the battle has started.")]
    [SerializeField] private bool hideButtonAfterBattleStarts = true;

    [Header("Match Gate")]
    [Tooltip("ON = the BATTLE button starts NON-INTERACTABLE and only unlocks once " +
             "the player has actually cleared a match on the puzzle board. Stops a " +
             "stage being started with zero heroes recruited.")]
    [SerializeField] private bool requireMatchBeforeBattle = true;

    [Tooltip("How many matches must be cleared before BATTLE unlocks.")]
    [SerializeField, Min(1)] private int matchesRequiredToUnlock = 1;

    [Tooltip("Optional UI shown only while the button is still locked - a 'make a " +
             "match first' hint. Safe to leave empty.")]
    [SerializeField] private GameObject lockedHint;

    [Header("Testing")]
    [Tooltip("TEST MODE (ON while developing): the daily allowance is kept in memory " +
             "only and never written to the save file, so every Play session starts " +
             "with a full allowance. TURN THIS OFF FOR RELEASE, otherwise the 24h cap " +
             "resets every time the app restarts.")]
    [SerializeField] private bool doNotPersistAllowance = true;

    /// <summary>True once this stage's battle phase has been released.</summary>
    public bool BattleStarted { get; private set; }

    /// <summary>
    /// True once enough matches have been cleared for BATTLE to be pressable.
    /// Always true when requireMatchBeforeBattle is off, so an old stage that
    /// never had a match gate behaves exactly as it always did.
    /// </summary>
    public bool MatchGateOpen { get; private set; }

    /// <summary>Raised when the match gate opens - for a button-glow / tutorial cue.</summary>
    public event Action OnMatchGateOpened;

    private int matchesCleared;

    /// <summary>
    /// Global "the fighting has begun" flag, for things spawned at runtime that
    /// cannot hold a reference to this component - unit health bars, mainly.
    ///
    /// DEFAULTS TO TRUE on purpose. A scene with no BattleStartController (every
    /// stage authored before the puzzle-first flow) therefore behaves exactly as
    /// it always did. Awake below flips it to false, so ONLY a stage that has
    /// this component gates anything.
    /// </summary>
    public static bool BattleIsRunning { get; private set; } = true;

    /// <summary>Raised when any stage's battle phase starts. Static, so runtime-spawned units can listen.</summary>
    public static event Action OnAnyBattleStarted;

    /// <summary>Raised the moment the battle phase begins.</summary>
    public event Action OnBattleStarted;

    /// <summary>Raised when the press was refused (allowance spent, no energy).</summary>
    public event Action OnBattleBlocked;

    private void Awake()
    {
        // This stage gates the battle, so nothing is "running" until BATTLE is
        // pressed. Set in Awake so units spawned later read the correct value.
        BattleIsRunning = false;

        // Set before anything reads the allowance, so the very first query
        // already goes to the in-memory store rather than the save file.
        BattleEnergyService.SessionOnly = doNotPersistAllowance;

        if (!battleButton) battleButton = GetComponent<Button>();
        if (!enemySpawner) enemySpawner = FindObjectOfType<EnemySpawner>(true);
        if (!transition) transition = FindObjectOfType<BattlePhaseTransition>(true);

        if (!battleButton)
            Debug.LogError("[BattleStartController] No BATTLE button assigned or found.", this);

        if (!enemySpawner)
            Debug.LogError("[BattleStartController] No EnemySpawner found in the scene.", this);

        // Locked in Awake, before the first frame is drawn, so the button is never
        // pressable for even one frame at stage open.
        MatchGateOpen = !requireMatchBeforeBattle;
        ApplyGateToButton();
    }

    private void OnEnable()
    {
        if (battleButton) battleButton.onClick.AddListener(OnBattleButtonPressed);
        BattleEnergyService.OnAllowanceChanged += RefreshUI;

        if (requireMatchBeforeBattle && !MatchGateOpen)
            MatchResolver.OnBlast += OnBoardBlast;

        RefreshUI();
    }

    private void OnDisable()
    {
        if (battleButton) battleButton.onClick.RemoveListener(OnBattleButtonPressed);
        BattleEnergyService.OnAllowanceChanged -= RefreshUI;

        // OnBlast is a plain static delegate, never cleared by MatchResolver, so an
        // unsubscribe here is what stops a destroyed controller being called.
        MatchResolver.OnBlast -= OnBoardBlast;
    }

    /// <summary>
    /// Every cleared match on the puzzle board. The gate opens on the Nth one and
    /// then unsubscribes - later matches are none of this component's business.
    /// </summary>
    private void OnBoardBlast(int groups)
    {
        if (MatchGateOpen) return;

        matchesCleared++;
        if (matchesCleared < matchesRequiredToUnlock) return;

        MatchGateOpen = true;
        MatchResolver.OnBlast -= OnBoardBlast;

        ApplyGateToButton();
        OnMatchGateOpened?.Invoke();
    }

    /// <summary>
    /// Pushes MatchGateOpen onto the button and the hint. Kept separate so Awake
    /// and the unlock share one code path and cannot drift apart.
    /// </summary>
    private void ApplyGateToButton()
    {
        // Never re-enable a button the battle has already consumed.
        if (BattleStarted) return;

        if (battleButton) battleButton.interactable = MatchGateOpen;
        if (lockedHint) lockedHint.SetActive(!MatchGateOpen);
    }

    /// <summary>
    /// The BATTLE press. Public so it can also be wired from the button's
    /// OnClick list - but do NOT do both, the listener is already added in code.
    /// </summary>
    public void OnBattleButtonPressed()
    {
        if (BattleStarted) return;

        // interactable=false already blocks the click, but this method is public and
        // may also be wired from the button's OnClick list or a debug key.
        if (!MatchGateOpen)
        {
            Debug.Log("[BattleStartController] Battle refused: no match cleared yet " +
                      $"({matchesCleared}/{matchesRequiredToUnlock}).");
            OnBattleBlocked?.Invoke();
            return;
        }

        var result = BattleEnergyService.Consume(dailyBattleLimit, energyCostPerBattle);

        if (result == BattleEnergyService.StartCheck.BlockedNoEnergy)
        {
            var wait = BattleEnergyService.TimeUntilReset;
            Debug.Log($"[BattleStartController] Battle refused: daily allowance " +
                      $"({dailyBattleLimit}) spent and energy < {energyCostPerBattle}. " +
                      $"Resets in {wait.Hours}h {wait.Minutes}m.");

            // TODO: show the "not enough energy" popup / rewarded-ad offer here
            //       once the energy economy exists.
            OnBattleBlocked?.Invoke();
            RefreshUI();
            return;
        }

        StartBattle();
    }

    /// <summary>Releases the spawner. Bypasses the allowance - use for debug/revive flows.</summary>
    public void StartBattle()
    {
        if (BattleStarted) return;
        BattleStarted = true;

        if (hideButtonAfterBattleStarts && battleButton)
            battleButton.gameObject.SetActive(false);

        if (lockedHint) lockedHint.SetActive(false);

        // StartBattle is also the debug/revive entry point and bypasses the gate;
        // keep the flag honest so nothing later re-locks the button.
        MatchGateOpen = true;
        MatchResolver.OnBlast -= OnBoardBlast;

        // Announce it globally BEFORE the transition, so unit health bars appear
        // as the fighting begins rather than after the camera settles.
        BattleIsRunning = true;
        OnAnyBattleStarted?.Invoke();

        OnBattleStarted?.Invoke();
        RefreshUI();

        // The camera pans up to the battlefield first; waves are only released
        // once it has settled, so the player is never fighting off screen.
        if (transition != null) transition.Play(ReleaseSpawner);
        else ReleaseSpawner();
    }

    private void ReleaseSpawner()
    {
        if (enemySpawner) enemySpawner.StartBattle();
        else Debug.LogError("[BattleStartController] Cannot start battle: no EnemySpawner.", this);
    }

    private void RefreshUI()
    {
        if (energyCostText)
            energyCostText.text = energyCostPerBattle.ToString();

        if (remainingBattlesText)
            remainingBattlesText.text = $"{BattleEnergyService.RemainingFree(dailyBattleLimit)}/{dailyBattleLimit}";
    }
}
