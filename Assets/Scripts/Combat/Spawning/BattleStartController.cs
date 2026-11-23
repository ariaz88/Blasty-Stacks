// BattleStartController.cs
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the BATTLE button in the Feature Panel.
///
/// New level flow: the stage opens in a puzzle-only phase - the EnemySpawner
/// sits idle (its waitForBattleStart flag is ON) and nothing attacks. Pressing
/// BATTLE charges the player's session allowance through BattleEnergyService and,
/// if that succeeds, releases the spawner for the rest of the stage.
///
/// The allowance is shared by all stages. A rewarded ad grants five more
/// starts after the counter reaches zero.
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

    private BoardGridXY puzzleBoard;
    private bool autoStartPending;

    [Header("Optional UI")]
    [Tooltip("Optional cost label under BATTLE (one unit per battle).")]
    [SerializeField] private TMP_Text energyCostText;

    [Tooltip("'Battles left this session' label - the number next to the energy icon " +
             "(20, 19 ... 0). Safe to leave empty.")]
    [SerializeField] private TMP_Text remainingBattlesText;

    private Button energyRefillButton;
    private bool rewardedAdPending;
    private float adUnavailableUntil;

    [Header("Session Allowance")]
    [Tooltip("Battles available at the start of a play session, shared across every stage.")]
    [SerializeField, Min(1)] private int dailyBattleLimit = BattleEnergyService.DefaultDailyBattleLimit;

    [Tooltip("Energy units charged per battle after the initial allowance is spent.")]
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

    [Tooltip("Shown while the gate is SHUT - the prefab's 'Battle_Diactive'. " +
             "Switched off the moment the first match lands.")]
    [SerializeField] private GameObject lockedRoot;

    [Tooltip("Shown once the gate OPENS - the prefab's 'Battle_Active'. Leave it " +
             "switched OFF in the scene; this script turns it on. battleButton " +
             "should be the Button that lives on THIS object.")]
    [SerializeField] private GameObject unlockedRoot;

    [Header("Testing")]
    [Tooltip("Keep battle energy for this app session across stage changes. " +
             "A new app session starts with the full allowance.")]
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
        if (remainingBattlesText)
            energyRefillButton = remainingBattlesText.GetComponentInParent<Button>(true);
        if (!enemySpawner) enemySpawner = FindObjectOfType<EnemySpawner>(true);
        if (!transition) transition = FindObjectOfType<BattlePhaseTransition>(true);
        puzzleBoard = FindObjectOfType<BoardGridXY>(true);

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
        if (energyRefillButton) energyRefillButton.onClick.AddListener(OnEnergyRefillPressed);
        BattleEnergyService.OnAllowanceChanged += RefreshUI;

        MatchResolver.OnBlast += OnBoardBlast;

        RefreshUI();
    }

    private void OnDisable()
    {
        if (battleButton) battleButton.onClick.RemoveListener(OnBattleButtonPressed);
        if (energyRefillButton) energyRefillButton.onClick.RemoveListener(OnEnergyRefillPressed);
        BattleEnergyService.OnAllowanceChanged -= RefreshUI;

        // OnBlast is a plain static delegate, never cleared by MatchResolver, so an
        // unsubscribe here is what stops a destroyed controller being called.
        MatchResolver.OnBlast -= OnBoardBlast;
        autoStartPending = false;
    }

    /// <summary>
    /// Open the button gate after enough matches, and start the battle automatically
    /// when the last match leaves the puzzle board empty.
    /// </summary>
    private void OnBoardBlast(int groups)
    {
        if (BattleStarted || groups <= 0) return;

        matchesCleared += groups;
        if (!MatchGateOpen && matchesCleared >= matchesRequiredToUnlock)
        {
            MatchGateOpen = true;
            ApplyGateToButton();
            OnMatchGateOpened?.Invoke();
        }

        if (!autoStartPending && puzzleBoard && !puzzleBoard.HasAnyOccupiedCells())
        {
            autoStartPending = true;
            StartCoroutine(AutoStartAfterLastMatch());
        }
    }

    private IEnumerator AutoStartAfterLastMatch()
    {
        // Let every OnBlast listener record the last match before SealForBattle.
        yield return null;
        autoStartPending = false;

        if (BattleStarted || !puzzleBoard || puzzleBoard.HasAnyOccupiedCells()) yield break;

        // An empty board is the completion condition even if a stage configured
        // its button to require more matches than the board contained.
        if (!MatchGateOpen)
        {
            MatchGateOpen = true;
            ApplyGateToButton();
            OnMatchGateOpened?.Invoke();
        }

        OnBattleButtonPressed();
    }

    /// <summary>
    /// Pushes MatchGateOpen onto the button and the hint. Kept separate so Awake
    /// and the unlock share one code path and cannot drift apart.
    /// </summary>
    private void ApplyGateToButton()
    {
        // Never re-enable a button the battle has already consumed.
        if (BattleStarted) return;

        // Swap the two authored states. Exactly one is ever on - they occupy the
        // same slot in the Feature Panel.
        if (lockedRoot) lockedRoot.SetActive(!MatchGateOpen);
        if (unlockedRoot) unlockedRoot.SetActive(MatchGateOpen);

        // Belt and braces: the live button is inside unlockedRoot, so this is
        // redundant while the roots are wired - but it is what keeps a scene that
        // uses ONLY a single always-visible button behaving correctly.
        // With no battles left, the energy strip offers a rewarded ad.
        if (battleButton) battleButton.interactable = MatchGateOpen && HasBattleLeft();
    }

    private bool HasBattleLeft() =>
        BattleEnergyService.Peek(dailyBattleLimit, energyCostPerBattle)
        != BattleEnergyService.StartCheck.BlockedNoEnergy;

    private void OnEnergyRefillPressed()
    {
        if (rewardedAdPending ||
            BattleEnergyService.RemainingBattles(dailyBattleLimit, energyCostPerBattle) > 0)
            return;

        var ads = AdManager.Instance;
        if (!ads)
        {
            ShowAdUnavailable();
            return;
        }

        rewardedAdPending = true;
        RefreshUI();
        ads.ShowRewardedAd(
            () =>
            {
                BattleEnergyService.AddEnergy(BattleEnergyService.RewardedAdBattles * energyCostPerBattle);
                rewardedAdPending = false;
                if (this) RefreshUI();
            },
            () =>
            {
                rewardedAdPending = false;
                if (this) ShowAdUnavailable();
            },
            () =>
            {
                rewardedAdPending = false;
                if (this) RefreshUI();
            });
    }

    private void ShowAdUnavailable()
    {
        adUnavailableUntil = Time.unscaledTime + 2f;
        Debug.LogWarning("[BattleStartController] Rewarded ad is not available yet.", this);
        RefreshUI();
    }

    private float nextRefillCheck;

    // Refresh the button and temporary ad status at a low frequency.
    private void Update()
    {
        if (BattleStarted || Time.unscaledTime < nextRefillCheck) return;
        nextRefillCheck = Time.unscaledTime + 1f;

        if ((battleButton && !battleButton.interactable && MatchGateOpen && HasBattleLeft()) ||
            (adUnavailableUntil > 0f && Time.unscaledTime >= adUnavailableUntil))
        {
            adUnavailableUntil = 0f;
            RefreshUI();
        }
    }

    /// <summary>
    /// The BATTLE press. Public so it can also be wired from the button's
    /// OnClick list - but do NOT do both, the listener is already added in code.
    /// </summary>
    public void OnBattleButtonPressed()
    {
        if (!GameStartManager.IsStageBootReady) return;
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
            Debug.Log($"[BattleStartController] Battle refused: session allowance " +
                      $"({dailyBattleLimit}) spent and energy < {energyCostPerBattle}. " +
                      "Tap the energy counter for a rewarded ad.");

            OnBattleBlocked?.Invoke();
            RefreshUI();
            return;
        }

        StartBattle();
    }

    /// <summary>Releases the spawner. Bypasses the allowance - use for debug/revive flows.</summary>
    public void StartBattle()
    {
        if (!GameStartManager.IsStageBootReady) return;
        if (BattleStarted) return;
        var playerWaves = FindObjectOfType<PlayerWaveManager>();
        if (playerWaves) playerWaves.SealForBattle();
        BattleStarted = true;

        if (hideButtonAfterBattleStarts && battleButton)
            battleButton.gameObject.SetActive(false);

        // The whole Battel slot is about to fade out with the Feature Panel, but
        // leave the states consistent in case a stage keeps the panel on screen.
        if (lockedRoot) lockedRoot.SetActive(false);

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
        {
            int remaining = BattleEnergyService.RemainingBattles(dailyBattleLimit, energyCostPerBattle);
            remainingBattlesText.text = remaining > 0 ? remaining.ToString()
                : rewardedAdPending ? "AD..."
                : Time.unscaledTime < adUnavailableUntil ? "NO AD"
                : "0 AD+5";
        }

        ApplyGateToButton();
    }
}
