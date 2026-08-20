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

    [Header("Testing")]
    [Tooltip("TEST MODE (ON while developing): the daily allowance is kept in memory " +
             "only and never written to the save file, so every Play session starts " +
             "with a full allowance. TURN THIS OFF FOR RELEASE, otherwise the 24h cap " +
             "resets every time the app restarts.")]
    [SerializeField] private bool doNotPersistAllowance = true;

    /// <summary>True once this stage's battle phase has been released.</summary>
    public bool BattleStarted { get; private set; }

    /// <summary>Raised the moment the battle phase begins.</summary>
    public event Action OnBattleStarted;

    /// <summary>Raised when the press was refused (allowance spent, no energy).</summary>
    public event Action OnBattleBlocked;

    private void Awake()
    {
        // Set before anything reads the allowance, so the very first query
        // already goes to the in-memory store rather than the save file.
        BattleEnergyService.SessionOnly = doNotPersistAllowance;

        if (!battleButton) battleButton = GetComponent<Button>();
        if (!enemySpawner) enemySpawner = FindObjectOfType<EnemySpawner>(true);

        if (!battleButton)
            Debug.LogError("[BattleStartController] No BATTLE button assigned or found.", this);

        if (!enemySpawner)
            Debug.LogError("[BattleStartController] No EnemySpawner found in the scene.", this);
    }

    private void OnEnable()
    {
        if (battleButton) battleButton.onClick.AddListener(OnBattleButtonPressed);
        BattleEnergyService.OnAllowanceChanged += RefreshUI;
        RefreshUI();
    }

    private void OnDisable()
    {
        if (battleButton) battleButton.onClick.RemoveListener(OnBattleButtonPressed);
        BattleEnergyService.OnAllowanceChanged -= RefreshUI;
    }

    /// <summary>
    /// The BATTLE press. Public so it can also be wired from the button's
    /// OnClick list - but do NOT do both, the listener is already added in code.
    /// </summary>
    public void OnBattleButtonPressed()
    {
        if (BattleStarted) return;

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

        if (enemySpawner) enemySpawner.StartBattle();
        else Debug.LogError("[BattleStartController] Cannot start battle: no EnemySpawner.", this);

        if (hideButtonAfterBattleStarts && battleButton)
            battleButton.gameObject.SetActive(false);

        OnBattleStarted?.Invoke();
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (energyCostText)
            energyCostText.text = energyCostPerBattle.ToString();

        if (remainingBattlesText)
            remainingBattlesText.text = $"{BattleEnergyService.RemainingFree(dailyBattleLimit)}/{dailyBattleLimit}";
    }
}
