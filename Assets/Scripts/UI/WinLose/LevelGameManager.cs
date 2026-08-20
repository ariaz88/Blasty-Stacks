using System;
using UnityEngine;


public class LevelGameManager : MonoBehaviour
{
    public static LevelGameManager Instance { get; private set; }

    public enum GameState
    {
        Playing,
        Won,
        ReviveOffer,
        Lost
    }

    [Header("UI References")]
    [SerializeField] private WinPanel winPanel;
    [SerializeField] private RevivePanel revivePanel;

    [Header("Revive rules")]
    [Tooltip("If true, only one successful revive is allowed per stage.")]
    [SerializeField] private bool allowSingleRevivePerStage = true;

    public GameState CurrentState { get; private set; } = GameState.Playing;

    private bool hasRevivedThisStage = false;

    private PlayerGateStats playerGate;
    private EnemyGateStats enemyGate;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        EnemyGateStats.OnGateDestroyed += OnEnemyGateDestroyed;
        PlayerGateStats.OnGateDestroyed += OnPlayerGateDestroyed;
    }

    private void OnDisable()
    {
        EnemyGateStats.OnGateDestroyed -= OnEnemyGateDestroyed;
        PlayerGateStats.OnGateDestroyed -= OnPlayerGateDestroyed;
    }

    private void Start()
    {
        playerGate = FindObjectOfType<PlayerGateStats>();
        enemyGate = FindObjectOfType<EnemyGateStats>();
    }

    // ---------------------------------------------------------
    // ENEMY gate dies  -> LEVEL WON
    // ---------------------------------------------------------
    private void OnEnemyGateDestroyed()
    {
        if (CurrentState != GameState.Playing)
            return;

        CurrentState = GameState.Won;

        // IMPORTANT:
        //  - DO NOT pause the whole game here.
        //  - Player units already receive EnemyGateStats.OnGateDestroyed
        //    and call PlayerManager.HandleGateDestroyed(), where they
        //    stop moving and switch to GameComplete state.
        //  - We want UI animations (coins/gems/XP) to keep running.

        // 1) Compute player's HP percent for stage rewards
        float hpPercent = 1f;
        if (playerGate != null && playerGate.maxHealth > 0f)
        {
            hpPercent = Mathf.Clamp01(playerGate.currentHP / playerGate.maxHealth);
        }

        // 2) Notify progression system
        HomeManager.NotifyStageWon(hpPercent);

        // 3) Show WinPanel with the HP%
        if (winPanel != null)
        {
            winPanel.gameObject.SetActive(true);
            winPanel.Show(hpPercent);
        }
        else
        {
            Debug.LogWarning("[LevelGameManager] WinPanel reference is missing.");
        }

        // NOTE: NO GameplayPause.SetPaused(true) here.
        // If you ever want to stop enemy AI on win, do that by
        // stopping their movement, not by pausing the whole game.
    }

    // ---------------------------------------------------------
    // PLAYER gate dies  -> REVIVE or LOSE
    // ---------------------------------------------------------
    private void OnPlayerGateDestroyed()
    {
        if (CurrentState != GameState.Playing)
            return;

        // For Lose / Revive we DO pause gameplay.
        GameplayPause.SetPaused(true);

        if (allowSingleRevivePerStage && hasRevivedThisStage)
        {
            // No more revives: go straight to Lose
            CurrentState = GameState.Lost;

            if (revivePanel != null)
            {
                // Make sure this method is public in RevivePanel
                revivePanel.ShowLosePanel();
            }
        }
        else
        {
            // Offer revive
            CurrentState = GameState.ReviveOffer;

            if (revivePanel != null)
            {
                // Make sure this method is public in RevivePanel
                revivePanel.ShowRevivePanel();
            }
        }
    }

    // ---------------------------------------------------------
    // Called from RevivePanel when user actually revives
    // ---------------------------------------------------------
    public void NotifyReviveAccepted()
    {
        hasRevivedThisStage = true;
        CurrentState = GameState.Playing;

        // RevivePanel itself should call GameplayPause.SetPaused(false)
        // after ReviveTheStage() so the level continues normally.
    }

    // ---------------------------------------------------------
    // Called from RevivePanel when user declines revive / timer ends
    // ---------------------------------------------------------
    public void NotifyReviveDeclined()
    {
        CurrentState = GameState.Lost;
        // Gameplay remains paused, LosePanel remains visible.
    }

    // Optional if you ever reload the same stage without reloading scene
    public void ResetForNewStage()
    {
        CurrentState = GameState.Playing;
        hasRevivedThisStage = false;
    }
}
