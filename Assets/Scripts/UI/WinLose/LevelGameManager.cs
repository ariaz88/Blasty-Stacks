using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;


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

    [Header("Result presentation")]
    [SerializeField, Range(0.5f, 2.5f)] private float delayAfterBaseHides = 1.6f;
    [SerializeField, Range(1f, 1.08f)] private float resultZoomOut = 1.06f;
    [SerializeField, Range(0f, 3f)] private float resultCameraPullback = 1.5f;

    // =====================================================================
    // REVIVE DISABLED - 2026-09-06
    //
    // The defeat flow used to fork: offer the gem revive first, fall through to
    // Lose only once the stage's single revive had been spent. Revive was cut
    // from the game, so EnterDefeatFlow now always goes straight to Lose.
    //
    // Nothing is deleted - the ReviveOffer state, both Notify* callbacks and the
    // whole of RevivePanel's revive machinery are intact and unreachable. Set
    // this (and ReviveEnabled in RevivePanel) to true to restore the old flow.
    //
    // static readonly rather than [SerializeField]: the scenes already carry a
    // serialized allowSingleRevivePerStage = true, and a second Inspector toggle
    // is exactly the kind of thing that gets flipped back on by accident in one
    // stage out of twenty.
    // =====================================================================
    private static readonly bool OfferRevive = false;

    [Header("Revive rules")]
    [Tooltip("If true, only one successful revive is allowed per stage. " +
             "Dormant while revive is disabled (see OfferRevive in this script).")]
    [SerializeField] private bool allowSingleRevivePerStage = true;

    [Header("Stalemate (mutual wipe)")]
    [Tooltip("Ends the level as a DEFEAT when both armies wipe each other out and no " +
             "gate ever falls. Without this the stage hangs in Playing forever: there " +
             "is nothing left on the field to destroy either gate.")]
    [SerializeField] private bool detectMutualWipe = true;

    [Tooltip("The wipe must hold uninterrupted for this long before the level ends. " +
             "This is the window a gem buy-back (LastStandOffer / HeroStatsPanel) has " +
             "to land in - a hero arriving resets the timer.")]
    [SerializeField, Min(0f)] private float stalemateGraceSeconds = 2.5f;

    [Tooltip("Ignore the wipe check for this long after a revive. RevivePanel destroys " +
             "every locked hero and restarts the wave loop, so the field is legitimately " +
             "empty for a moment and would otherwise re-trigger the defeat instantly.")]
    [SerializeField, Min(0f)] private float postReviveSettleSeconds = 5f;

    /// <summary>
    /// Fires whenever the level leaves or re-enters GameState.Playing.
    ///
    /// This is the authoritative "is the battle still running?" signal: the battle
    /// ends the moment a GATE hits 0 HP (enemy gate = won, player gate = lost or
    /// revive offer), and resumes only when a revive is accepted. Anything that
    /// behaves differently during combat than it does under an end-of-battle panel
    /// should listen here rather than inventing its own notion of "over".
    ///
    /// STATIC, so it survives the listener's own lifetime - every subscriber must
    /// unsubscribe in OnDestroy or it will leak across scene loads.
    /// </summary>
    public static event Action<GameState> OnGameStateChanged;

    /// <summary>True while the level is still being played. Safe before any
    /// LevelGameManager exists (a stage opened directly in the editor).</summary>
    public static bool IsBattleRunning =>
        Instance == null || Instance.CurrentState == GameState.Playing;

    private GameState _currentState = GameState.Playing;

    public GameState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState == value) return;
            _currentState = value;
            OnGameStateChanged?.Invoke(value);
        }
    }

    private bool hasRevivedThisStage = false;

    private PlayerGateStats playerGate;
    private EnemyGateStats enemyGate;
    private EnemySpawner enemySpawner;
    private HeroDeploymentSequencer deploymentSequencer;

    // How long the mutual-wipe condition has held without a break.
    private float stalemateTimer;

    // Unscaled timestamp before which the wipe check is skipped entirely.
    private float suppressStalemateUntil;
    private bool leavingStage;
    public bool ResultPanelVisible { get; private set; }

    /// <summary>Abandon the current stage without recording a win or loss.</summary>
    public void ReturnToHomeMenu()
    {
        if (leavingStage) return;
        leavingStage = true;

        if (HudCurrencyView.Instance && HudCurrencyView.Instance.IsGameplayPaused)
            HudCurrencyView.Instance.ResumeGameplay();
        GameplayPause.SetPaused(false);
        SceneManager.LoadScene("MenuScene", LoadSceneMode.Single);
    }

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
        enemySpawner = FindObjectOfType<EnemySpawner>(true);
        deploymentSequencer = FindObjectOfType<HeroDeploymentSequencer>(true);
    }

    // ---------------------------------------------------------
    // ENEMY gate dies  -> LEVEL WON
    // ---------------------------------------------------------
    private void OnEnemyGateDestroyed()
    {
        if (CurrentState != GameState.Playing)
            return;

        CurrentState = GameState.Won;
        FreezeCharactersForResult(false);

        // 1) Compute player's HP percent for stage rewards
        float hpPercent = 1f;
        if (playerGate != null && playerGate.maxHealth > 0f)
        {
            hpPercent = Mathf.Clamp01(playerGate.currentHP / playerGate.maxHealth);
        }

        // 2) Notify progression system
        HomeManager.NotifyStageWon(hpPercent);

        StartCoroutine(ShowResultAfterBaseHides(enemyGate ? enemyGate.gameObject : null,
                                               enemyGate ? enemyGate.healthBar : null,
                                               true, hpPercent));
    }

    // ---------------------------------------------------------
    // PLAYER gate dies  -> REVIVE or LOSE
    // ---------------------------------------------------------
    private void OnPlayerGateDestroyed()
    {
        if (CurrentState != GameState.Playing)
            return;

        EnterDefeatFlow();
    }

    /// <summary>
    /// The one defeat path, shared by the gate death above and the mutual-wipe check
    /// below: pause, then offer the revive if the stage still has one, otherwise go
    /// straight to Lose. Callers are responsible for the "are we still Playing?" guard.
    ///
    /// [2026-09-06] With OfferRevive off, the first branch is the ONLY one taken -
    /// every defeat lands on the Lose panel after the base transition. The else-branch is kept for
    /// the day revive comes back.
    /// </summary>
    private void EnterDefeatFlow()
    {
        if (!OfferRevive || (allowSingleRevivePerStage && hasRevivedThisStage))
        {
            CurrentState = GameState.Lost;
        }
        else
        {
            CurrentState = GameState.ReviveOffer;
        }

        FreezeCharactersForResult(true);
        StartCoroutine(ShowResultAfterBaseHides(playerGate ? playerGate.gameObject : null,
                                               playerGate ? playerGate.healthBar : null,
                                               false, 0f));
    }

    private void FreezeCharactersForResult(bool pauseClock)
    {
        // Reuse the stage's existing pause path: it stops movement, physics and
        // animators on both teams as soon as the base bar reaches zero.
        if (HudCurrencyView.Instance)
            HudCurrencyView.Instance.PauseGameplay();
        else if (!pauseClock)
            GameplayPause.SetPaused(true);

        // Win keeps scaled time for the claim's resource flight. Lose already
        // uses unscaled UI fades, so it can pause the game clock as before.
        if (pauseClock) GameplayPause.SetPaused(true);
    }

    private IEnumerator ShowResultAfterBaseHides(GameObject baseRoot, HealthBar baseBar,
                                                  bool won, float hpPercent)
    {
        // The base fill reaches zero on the hit frame. Wait for any authored
        // bar transition before hiding the complete base hierarchy.
        while (baseBar && !baseBar.DamageTrailFinished)
            yield return null;

        if (baseRoot) baseRoot.SetActive(false);

        yield return ZoomOutBeforeResult();

        if (won)
        {
            if (winPanel)
            {
                winPanel.gameObject.SetActive(true);
                winPanel.Show(hpPercent);
                ResultPanelVisible = true;
            }
            else
                Debug.LogWarning("[LevelGameManager] WinPanel reference is missing.");
        }
        else if (revivePanel)
        {
            if (CurrentState == GameState.ReviveOffer)
                revivePanel.ShowRevivePanel();
            else
                revivePanel.ShowLosePanel();
            ResultPanelVisible = true;
        }
    }

    private IEnumerator ZoomOutBeforeResult()
    {
        var camera = Camera.main;
        float duration = Mathf.Clamp(delayAfterBaseHides, 0.5f, 2.5f);
        float elapsed = 0f;
        float start = camera ? (camera.orthographic ? camera.orthographicSize : camera.fieldOfView) : 0f;
        float target = start * Mathf.Clamp(resultZoomOut, 1f, 1.08f);
        Vector3 startPosition = camera ? camera.transform.position : Vector3.zero;
        Vector3 targetPosition = startPosition + Vector3.back * Mathf.Clamp(resultCameraPullback, 0f, 3f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (camera)
            {
                if (camera.orthographic) camera.orthographicSize = Mathf.Lerp(start, target, k);
                else camera.fieldOfView = Mathf.Lerp(start, target, k);
                camera.transform.position = Vector3.Lerp(startPosition, targetPosition, k);
            }
            yield return null;
        }
    }

    // ---------------------------------------------------------
    // MUTUAL WIPE  -> same defeat flow, no gate involved
    // ---------------------------------------------------------

    /// <summary>
    /// The third way a level can end.
    ///
    /// Normally one surviving side always resolves the battle - leftover enemies march
    /// on the player gate, leftover heroes march on the enemy gate. But if the LAST hero
    /// and the LAST enemy kill each other, and the spawner has no waves left, both gates
    /// are still standing and NOTHING can ever fire OnGateDestroyed again. The stage used
    /// to hang in Playing forever. That state is a defeat.
    ///
    /// Deliberately gated behind a grace timer rather than reacting on the frame it
    /// becomes true, so a hero mid-arrival (or one bought through LastStandOffer) gets a
    /// chance to land and clear the condition.
    /// </summary>
    private void Update()
    {
        // NOTE: this used to bail out whenever a CP battle was prepared, because the
        // scripted stages required a physically destroyed gate rather than a timeout.
        // Nothing is scripted any more, and CPBattleController is now prepared for
        // EVERY battle as a reporter - so keeping that check would have disabled
        // mutual-wipe detection permanently and hung the stage in Playing forever.
        if (!detectMutualWipe || CurrentState != GameState.Playing)
        {
            stalemateTimer = 0f;
            return;
        }

        if (Time.unscaledTime < suppressStalemateUntil || !IsMutualWipe())
        {
            stalemateTimer = 0f;
            return;
        }

        stalemateTimer += Time.unscaledDeltaTime;
        if (stalemateTimer < stalemateGraceSeconds)
            return;

        stalemateTimer = 0f;

        Debug.Log("[LevelGameManager] Mutual wipe - no heroes, no enemies and no waves " +
                  "left, with both gates standing. Treating the stage as a defeat.");

        if (playerGate)
            playerGate.CollapseAfterMutualWipe();
        else
            EnterDefeatFlow();
    }

    private bool IsMutualWipe()
    {
        // No spawner, or the puzzle-only phase: nothing has fought yet, so nothing can
        // be wiped out. This single check is what keeps the whole thing inert before
        // BATTLE is pressed.
        if (enemySpawner == null) return false;
        if (!enemySpawner.BattleStarted || !enemySpawner.HasSpawnedFirstEnemy) return false;

        // More waves are still coming - the fight is not over, it is just between waves.
        if (!enemySpawner.AllWavesDispatched) return false;

        // A fallen gate is the gate events' business, not ours.
        if (playerGate == null || playerGate.isPlayerGateDestroyed) return false;
        if (enemyGate == null || enemyGate.isDestroyed) return false;

        // Heroes actually released into the field. Reinforcements are flagged unlocked
        // the moment they are bought, before their gate hold, so one on its way in keeps
        // this above zero and the timer resets.
        if (HeroRoster.TotalAlive() > 0) return false;

        // Heroes still LOADING on their cards are still in the fight. Deployment
        // is one hero per type every few seconds, so the field can be empty while
        // a slow type (9s) has several left to send - that is not a defeat.
        if (deploymentSequencer && deploymentSequencer.HeroesStillComing) return false;

        // Cheap counter first; the scene sweep only runs on the rare frames where both
        // sides already read empty, and catches enemies hand-placed in the scene that
        // the spawner never counted.
        if (enemySpawner.AliveEnemyCount > 0) return false;

        return !AnyLiveEnemyInScene();
    }

    private static bool AnyLiveEnemyInScene()
    {
        foreach (var e in FindObjectsOfType<EnemyStats>())
            if (e != null && !e.enemyIsdead) return true;

        return false;
    }

    // ---------------------------------------------------------
    // Called from RevivePanel when user actually revives
    // ---------------------------------------------------------
    public void NotifyReviveAccepted()
    {
        hasRevivedThisStage = true;
        CurrentState = GameState.Playing;

        // RevivePanel.ReviveTheStage() wipes every locked hero and restarts the wave
        // loop, so the field is genuinely empty for a moment. Without this the wipe
        // check would fire again the instant the player paid to come back.
        stalemateTimer = 0f;
        suppressStalemateUntil = Time.unscaledTime + postReviveSettleSeconds;

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
        ResultPanelVisible = false;
        hasRevivedThisStage = false;
        stalemateTimer = 0f;
        suppressStalemateUntil = 0f;
    }
}
