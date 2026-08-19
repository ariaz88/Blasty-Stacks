using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;



public class RevivePanel : MonoBehaviour
{
    public static RevivePanel Instance { get; private set; }

    [Header("Fade (WinPanel-style)")]
    [SerializeField] private CanvasGroup reviveCanvasGroup;   // CanvasGroup on the Revive panel root
    [SerializeField] private CanvasGroup loseCanvasGroup;     // CanvasGroup on the Lose panel root
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private float fadeDurationrEVIVE = 0.85f;


    [Header("UI")]
    [SerializeField] private Button reviveButton;
    [SerializeField] private Button noThanksButton;

    [SerializeField] private GameObject revivePanel;   // the orange window
    [SerializeField] private GameObject losePanel;     // pink Lose panel

    [SerializeField] private Image BGImage;            // dark overlay behind panels
    [SerializeField] private Image progressBar;        // circular timer bar
    [SerializeField] private Image whiteBar;           // inner white ring (optional)
    [SerializeField] private TMP_Text barText;         // countdown number in the circle
    [SerializeField] private TMP_Text gemCostText;     // the purple gem cost number on the button

    [Header("Revive Settings")]
    [SerializeField] private float countdownDuration = 9f;

    [SerializeField] private int baseReviveGemCost = 15;       // first revive
    [SerializeField] private int secondReviveFlatIncrement = 5;// second revive = base + 5
    [SerializeField] private float laterReviveMultiplier = 1.3f; // from 3rd revive onward: cost *= 1.3

    [Header("State (debug)")]
    [SerializeField] private bool isReviveActive = false;
    [SerializeField] private bool isReviveButtonPressed = false;
    [SerializeField] public bool gameOver = false;            // exposed so you can watch it in Inspector

    [Header("Per-Stage Revive Rules")]
    [Tooltip("If true, we only offer revive once per stage. After a successful revive, the next gate death goes directly to Lose panel.")]
    //[SerializeField] private bool allowSingleRevivePerStage = true;
    //[SerializeField] private bool hasRevivedThisStage = false;   // NEW

    [Header("Board Roots")]
    [SerializeField] private GameObject blocksRoot;          // BG/Blocks
    [SerializeField] private GameObject redundantBlocksRoot; // BG/RedundantBlocks
    [SerializeField] private BoardGridXY board;              // your main board

    private float timer;
    private int currentReviveCost;
    private int reviveUsedCount;




    private PlayerWaveManager playerWaveManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        playerWaveManager = FindObjectOfType<PlayerWaveManager>();

        // per-run initialise
        currentReviveCost = baseReviveGemCost;
        reviveUsedCount = 0;

        if (revivePanel != null)
            revivePanel.SetActive(false);

        // make sure overlay starts transparent
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
        }
    }

    //private void OnEnable()
    //{
    //    PlayerGateStats.OnGateDestroyed += HandleGateDestroyed;
    //}

    //private void OnDisable()
    //{
    //    PlayerGateStats.OnGateDestroyed -= HandleGateDestroyed;
    //}

    private void Start()
    {
        if (reviveButton != null)
            reviveButton.onClick.AddListener(ReviveLevel);

        if (noThanksButton != null)
            noThanksButton.onClick.AddListener(NoThanksClick);

        RefreshGemCostUI();
    }

    private void Update()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        // unscaled so pause doesn't stop countdown
        timer -= Time.unscaledDeltaTime;

        float t = Mathf.Clamp01(timer / countdownDuration);
        if (progressBar != null)
            progressBar.fillAmount = t;
        if (whiteBar != null)
            whiteBar.fillAmount = t;

        if (barText != null)
            barText.text = Mathf.CeilToInt(Mathf.Max(0f, timer)).ToString();

        if (timer <= 0f)
        {
            isReviveActive = false;
            isReviveButtonPressed = false;
            HideReviveAndShowLose();
        }
    }

    // -------------------------------------------------------------------------
    // Game over → show revive panel (or direct lose if revive already used)
    // -------------------------------------------------------------------------

    //private void HandleGateDestroyed()
    //{
    //    // PlayerGateStats told us the gate is dead
    //    gameOver = true;

    //    // If we already used revive in this stage, go straight to Lose panel
    //    if (allowSingleRevivePerStage && hasRevivedThisStage)
    //    {
    //        ShowLosePanel();
    //    }
    //    else
    //    {
    //        ShowRevivePanel();
    //    }
    //}

    private void ShowRevivePanel1()
    {
        if (revivePanel == null)
            return;

        // Pause gameplay
        GameplayPause.SetPaused(true);

        RefreshGemCostUI();

        revivePanel.SetActive(true);
        isReviveButtonPressed = false;
        isReviveActive = true;

        timer = countdownDuration;

        if (progressBar != null) progressBar.fillAmount = 1f;
        if (whiteBar != null) whiteBar.fillAmount = 1f;
        if (barText != null) barText.text = Mathf.CeilToInt(countdownDuration).ToString();

        // fade in overlay (unscaled)
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
            BGImage.DOFade(0.75f, 0.35f).SetUpdate(true);
        }

        // scale in panel (unscaled)
        revivePanel.transform.localScale = Vector3.zero;
        revivePanel.transform.DOScale(Vector3.one, 0.7f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }
    public void ShowRevivePanel()
    {
        if (revivePanel == null)
            return;

        // Pause gameplay while Revive is visible
        GameplayPause.SetPaused(true);

        RefreshGemCostUI();

        revivePanel.SetActive(true);
        isReviveButtonPressed = false;
        isReviveActive = true;

        timer = countdownDuration;

        if (progressBar != null) progressBar.fillAmount = 1f;
        if (whiteBar != null) whiteBar.fillAmount = 1f;
        if (barText != null) barText.text = Mathf.CeilToInt(countdownDuration).ToString();

        // Fade in dark BG overlay (unscaled)
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
        }

        // Start both fades together: BG + Revive CanvasGroup
        StartCoroutine(FadeInRevivePanelRoutine());
    }
    private IEnumerator FadeInRevivePanelRoutine()
    {
        float t = 0f;

        // Prepare CanvasGroup
        if (reviveCanvasGroup != null)
        {
            reviveCanvasGroup.alpha = 0f;
            reviveCanvasGroup.blocksRaycasts = true;
            reviveCanvasGroup.interactable = true;
        }

        float bgStartAlpha = 0f;
        float bgTargetAlpha = 0.75f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);

            // Panel fade
            if (reviveCanvasGroup != null)
                reviveCanvasGroup.alpha = k;

            // BG fade
            if (BGImage != null)
            {
                Color c = BGImage.color;
                c.a = Mathf.Lerp(bgStartAlpha, bgTargetAlpha, k);
                BGImage.color = c;
            }

            yield return null;
        }

        // Final snap
        if (reviveCanvasGroup != null)
            reviveCanvasGroup.alpha = 1f;

        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = bgTargetAlpha;
            BGImage.color = c;
        }
    }
    private IEnumerator FadeOutRevivePanelRoutine(System.Action onComplete)
    {
        float t = 0f;

        float startPanelAlpha = reviveCanvasGroup != null ? reviveCanvasGroup.alpha : 1f;
        float startBgAlpha = BGImage != null ? BGImage.color.a : 1f;

        while (t < fadeDurationrEVIVE)
        {
            t += Time.unscaledDeltaTime;   // unscaled, same as Lose fade
            float k = Mathf.Clamp01(t / fadeDurationrEVIVE);

            // Fade panel
            if (reviveCanvasGroup != null)
                reviveCanvasGroup.alpha = Mathf.Lerp(startPanelAlpha, 0f, k);

            // Fade BG
            if (BGImage != null)
            {
                Color c = BGImage.color;
                c.a = Mathf.Lerp(startBgAlpha, 0f, k);
                BGImage.color = c;
            }

            yield return null;
        }

        // Final snap
        if (reviveCanvasGroup != null)
        {
            reviveCanvasGroup.alpha = 0f;
            reviveCanvasGroup.blocksRaycasts = false;
            reviveCanvasGroup.interactable = false;
        }

        if (revivePanel != null)
            revivePanel.SetActive(false);

        // callback (e.g. ShowLosePanel) after fade out
        onComplete?.Invoke();
    }



    // -------------------------------------------------------------------------
    // Button: Revive
    // -------------------------------------------------------------------------



    public void ReviveLevel1()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        // Try to pay gems first
        if (!RemoveGem())
        {
            // Not enough gems → treat like "No, thanks"
            isReviveActive = false;
            isReviveButtonPressed = true;
            HideReviveAndShowLose();
            return;
        }

        isReviveButtonPressed = true;
        isReviveActive = false;

        // Mark that we have used revive in THIS stage
        //if (allowSingleRevivePerStage)
        //    hasRevivedThisStage = true;   // NEW

        // We are reviving → resume gameplay logic before we reset enemies/waves
        GameplayPause.SetPaused(false);

        // Do all your revive/reset work
        gameOver = false;
        ReviveTheStage();

        if (revivePanel == null)
            return;

        // Now just hide the panel visually (unscaled tween is fine)
        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                revivePanel.gameObject.SetActive(false);
                if (BGImage != null)
                {
                    Color color = BGImage.color;
                    color.a = 0f;
                    BGImage.color = color;
                }
            });
    }
    public void ReviveLevel()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        // Try to pay gems first
        if (!RemoveGem())
        {
            // Not enough gems → treat like "No, thanks"
            isReviveActive = false;
            isReviveButtonPressed = true;
            HideReviveAndShowLose();
            return;
        }

        isReviveButtonPressed = true;
        isReviveActive = false;

        //// Mark that we have used revive in THIS stage (single revive rule)
        //if (allowSingleRevivePerStage)
        //    hasRevivedThisStage = true;

        // Inform LevelGameManager that revive was accepted
        if (LevelGameManager.Instance != null)
            LevelGameManager.Instance.NotifyReviveAccepted();


        // We are reviving: resume gameplay + reset stage BEFORE the UI fades out
        GameplayPause.SetPaused(false);
        gameOver = false;
        ReviveTheStage();

        // If there is no panel or canvas group, just bail out
        if (revivePanel == null && reviveCanvasGroup == null && BGImage == null)
            return;

        // Fade out revive panel + BG (WinPanel/Lose-style), no extra action after
        StartCoroutine(FadeOutRevivePanelRoutine(null));
    }


    // Pay the revive cost, update future cost, and save via CurrencyManager
    private bool RemoveGem()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogWarning("CurrencyManager not found – revive is free.");
            return true;
        }

        if (!cm.TrySpendGems(currentReviveCost))
        {
            // not enough gems
            Debug.Log("Not enough gems to revive.");
            return false;
        }

        // one successful revive
        reviveUsedCount++;

        // update cost for next time
        if (reviveUsedCount == 1)
        {
            currentReviveCost = baseReviveGemCost + secondReviveFlatIncrement;
        }
        else
        {
            currentReviveCost = Mathf.CeilToInt(currentReviveCost * laterReviveMultiplier);
        }

        RefreshGemCostUI();
        return true;
    }

    private void RefreshGemCostUI()
    {
        if (gemCostText != null)
            gemCostText.text = currentReviveCost.ToString();
    }

    // -------------------------------------------------------------------------
    // Button: No thanks
    // -------------------------------------------------------------------------

    public void NoThanksClick()
    {
        if (!isReviveActive) return;

        isReviveActive = false;
        isReviveButtonPressed = false;

        if (LevelGameManager.Instance != null)
            LevelGameManager.Instance.NotifyReviveDeclined();

        HideReviveAndShowLose();
    }

    private void HideReviveAndShowLose1()
    {
        if (revivePanel != null)
        {
            revivePanel.transform.DOScale(Vector3.zero, 0.7f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    revivePanel.SetActive(false);
                    ShowLosePanel();
                });
        }
        else
        {
            ShowLosePanel();
        }
    }
    private void HideReviveAndShowLose()
    {

        if (LevelGameManager.Instance != null)
            LevelGameManager.Instance.NotifyReviveDeclined();

        // If there is no revive panel, just show Lose directly
        if (revivePanel == null && reviveCanvasGroup == null && BGImage == null)
        {
            ShowLosePanel();
            return;
        }

        // Fade out Revive panel (same style as Lose fadeOut), then show LosePanel (which already has FadeIn)
        StartCoroutine(FadeOutRevivePanelRoutine(ShowLosePanel));
    }


    public void ShowLosePanel1()
    {
        // Make sure game is paused while Lose panel is visible
        GameplayPause.SetPaused(true);

        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
            BGImage.DOFade(0.75f, 0.35f).SetUpdate(true);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(true);
            losePanel.transform.localScale = Vector3.zero;
            losePanel.transform.DOScale(Vector3.one, 0.8f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }
    }
    public void ShowLosePanel()
    {
        // game is lost, keep gameplay paused
        GameplayPause.SetPaused(true);

        if (losePanel == null)
            return;

        losePanel.SetActive(true);

        // Reset canvas group state
        if (loseCanvasGroup != null)
        {
            loseCanvasGroup.alpha = 0f;
            loseCanvasGroup.blocksRaycasts = true;
            loseCanvasGroup.interactable = true;
        }

        // Reset BG alpha to 0 so we can fade it in
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
        }

        StartCoroutine(FadeInLosePanelRoutine());
    }
    private IEnumerator FadeInLosePanelRoutine()
    {
        float t = 0f;

        float bgStartAlpha = 0f;
        float bgTargetAlpha = 0.75f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / fadeDuration);

            if (loseCanvasGroup != null)
                loseCanvasGroup.alpha = k;

            if (BGImage != null)
            {
                Color c = BGImage.color;
                c.a = Mathf.Lerp(bgStartAlpha, bgTargetAlpha, k);
                BGImage.color = c;
            }

            yield return null;
        }

        if (loseCanvasGroup != null)
            loseCanvasGroup.alpha = 1f;

        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = bgTargetAlpha;
            BGImage.color = c;
        }
    }



    // -------------------------------------------------------------------------    
    // Actual revive logic
    // -------------------------------------------------------------------------


    private void ReviveTheStage()
    {
        // we are not game over anymore
        gameOver = false;

        // 1) Clear board occupancy so new blocks can use the cells
        if (board == null)
            board = FindObjectOfType<BoardGridXY>();

        if (board != null)
            board.ClearAllOccupancy();

        // 2) Remove ALL existing blocks from scene (if any)
        var allPieces = FindObjectsOfType<PieceSimple>(false);
        foreach (var p in allPieces)
        {
            if (p != null && p.gameObject.activeInHierarchy)
            {
                Destroy(p.gameObject);
            }
        }

        // 2.5) Remove ALL current players that are on the stage (Lock state)
        var allPlayers = FindObjectsOfType<PlayerManager>(false);
        foreach (var pl in allPlayers)
        {
            if (pl == null || !pl.gameObject.activeInHierarchy)
                continue;

            // Ignore the PlayerCastle (gate) which also has PlayerManager
            if (pl.CompareTag("PlayerGate"))
                continue;

            if (pl.currentState == pl.PlayerLockState)
            {
                Destroy(pl.gameObject);
            }
        }

        // 3) Swap board roots: disable original Blocks, enable RedundantBlocks
        if (blocksRoot != null)
            blocksRoot.SetActive(false);
        else
            Debug.LogWarning("RevivePanel: blocksRoot is NOT assigned in the Inspector!");

        if (redundantBlocksRoot != null)
            redundantBlocksRoot.SetActive(true);
        else
            Debug.LogWarning("RevivePanel: redundantBlocksRoot is NOT assigned in the Inspector!");

        // 4) Refill PlayerGate health
        PlayerGateStats gate2 = FindObjectOfType<PlayerGateStats>();
        if (gate2 != null)
        {
            gate2.isPlayerGateDestroyed = false;
            gate2.currentHP = gate2.maxHealth;
            if (gate2.healthBar != null)
                gate2.healthBar.SetCurrentHealth(gate2.currentHP, gate2.maxHealth);
        }
        else
        {
            Debug.LogWarning("RevivePanel: PlayerGateStats not found – cannot refill gate HP.");
        }

        // 5) Restart waves after a short REAL-TIME delay
        StartCoroutine(ActivateWaveSpawner());

        // 6) Unfreeze all enemies and let them search targets again
        var enemies2 = FindObjectsOfType<EnemyManager>();
        foreach (var enemy in enemies2)
        {
            enemy.ResetAfterRevive();
        }
    }

    IEnumerator ActivateWaveSpawner()
    {
        // real-time delay so it works even when timescale is 0
        yield return new WaitForSecondsRealtime(0.1f);

        if (playerWaveManager == null)
            playerWaveManager = FindObjectOfType<PlayerWaveManager>();

        if (playerWaveManager != null)
        {
            playerWaveManager.RestartAfterRevive();
        }
    }
}


public class RevivePanel3 : MonoBehaviour
{
    public static RevivePanel Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Button reviveButton;
    [SerializeField] private Button noThanksButton;

    [SerializeField] private GameObject revivePanel;   // the orange window
    [SerializeField] private GameObject losePanel;     // pink Lose panel

    [SerializeField] private Image BGImage;            // dark overlay behind panels
    [SerializeField] private Image progressBar;        // circular timer bar
    [SerializeField] private Image whiteBar;           // inner white ring (optional)
    [SerializeField] private TMP_Text barText;         // countdown number in the circle
    [SerializeField] private TMP_Text gemCostText;     // the purple gem cost number on the button

    [Header("Revive Settings")]
    [SerializeField] private float countdownDuration = 9f;

    [SerializeField] private int baseReviveGemCost = 15;       // first revive
    [SerializeField] private int secondReviveFlatIncrement = 5;// second revive = base + 5
    [SerializeField] private float laterReviveMultiplier = 1.3f; // from 3rd revive onward: cost *= 1.3

    [Header("State (debug)")]
    [SerializeField] private bool isReviveActive = false;
    [SerializeField] private bool isReviveButtonPressed = false;
    [SerializeField] public bool gameOver = false;            // exposed so you can watch it in Inspector

    [Header("Board Roots")]
    [SerializeField] private GameObject blocksRoot;          // BG/Blocks
    [SerializeField] private GameObject redundantBlocksRoot; // BG/RedundantBlocks
    [SerializeField] private BoardGridXY board;              // your main board

    private float timer;
    private int currentReviveCost;
    private int reviveUsedCount;

    private PlayerWaveManager playerWaveManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        //Instance = this;

        playerWaveManager = FindObjectOfType<PlayerWaveManager>();

        // per-run initialise
        currentReviveCost = baseReviveGemCost;
        reviveUsedCount = 0;

        if (revivePanel != null)
            revivePanel.SetActive(false);

        // make sure overlay starts transparent
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
        }
    }

    private void OnEnable()
    {
        PlayerGateStats.OnGateDestroyed += HandleGateDestroyed;
    }

    private void OnDisable()
    {
        PlayerGateStats.OnGateDestroyed -= HandleGateDestroyed;
    }

    private void Start()
    {
        if (reviveButton != null)
            reviveButton.onClick.AddListener(ReviveLevel);

        if (noThanksButton != null)
            noThanksButton.onClick.AddListener(NoThanksClick);

        RefreshGemCostUI();
    }

    private void Update()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        // unscaled so pause doesn't stop countdown
        timer -= Time.unscaledDeltaTime;

        float t = Mathf.Clamp01(timer / countdownDuration);
        if (progressBar != null)
            progressBar.fillAmount = t;
        if (whiteBar != null)
            whiteBar.fillAmount = t;

        if (barText != null)
            barText.text = Mathf.CeilToInt(Mathf.Max(0f, timer)).ToString();

        if (timer <= 0f)
        {
            isReviveActive = false;
            isReviveButtonPressed = false;
            HideReviveAndShowLose();
        }
    }

    // -------------------------------------------------------------------------
    // Game over → show revive panel
    // -------------------------------------------------------------------------

    private void HandleGateDestroyed()
    {
        // PlayerGateStats told us the gate is dead
        gameOver = true;
        ShowRevivePanel();
    }

    private void ShowRevivePanel()
    {
        if (revivePanel == null)
            return;

        // Pause gameplay
        GameplayPause.SetPaused(true);

        RefreshGemCostUI();

        revivePanel.SetActive(true);
        isReviveButtonPressed = false;
        isReviveActive = true;

        timer = countdownDuration;

        if (progressBar != null) progressBar.fillAmount = 1f;
        if (whiteBar != null) whiteBar.fillAmount = 1f;
        if (barText != null) barText.text = Mathf.CeilToInt(countdownDuration).ToString();

        // fade in overlay (unscaled)
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
            BGImage.DOFade(0.75f, 0.35f).SetUpdate(true);
        }

        // scale in panel (unscaled)
        revivePanel.transform.localScale = Vector3.zero;
        revivePanel.transform.DOScale(Vector3.one, 0.7f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true);
    }

    // -------------------------------------------------------------------------
    // Button: Revive
    // -------------------------------------------------------------------------

    public void ReviveLevel1()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        // Try to pay gems first
        if (!RemoveGem())
        {
            // Not enough gems → treat like "No, thanks"
            isReviveActive = false;
            isReviveButtonPressed = true;
            HideReviveAndShowLose();
            return;
        }

        isReviveButtonPressed = true;
        isReviveActive = false;

        if (revivePanel == null)
        {
            // fail-safe – still do revive logic
            gameOver = false;
            ReviveTheStage();
            GameplayPause.SetPaused(false);   // resume gameplay
            return;
        }

        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
           .SetEase(Ease.OutBack)
           .SetUpdate(true)
           .OnComplete(() =>
           {
               revivePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f;
                   BGImage.color = color;
               }

               // important: we are no longer in game over
               gameOver = false;

               // actually revive the gameplay (refill board, gate HP, etc.)
               ReviveTheStage();

               // now resume gameplay
               GameplayPause.SetPaused(false);
           });
    }
    public void ReviveLevel()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        // Try to pay gems first
        if (!RemoveGem())
        {
            // Not enough gems → treat like "No, thanks"
            isReviveActive = false;
            isReviveButtonPressed = true;
            HideReviveAndShowLose();
            return;
        }

        isReviveButtonPressed = true;
        isReviveActive = false;

        // We are reviving → resume gameplay logic before we reset enemies/waves
        GameplayPause.SetPaused(false);

        // Do all your revive/reset work (this is your existing method)
        gameOver = false;
        ReviveTheStage();

        if (revivePanel == null)
            return;

        // Now just hide the panel visually (unscaled tween is fine)
        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
            .SetEase(Ease.OutBack)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                revivePanel.gameObject.SetActive(false);
                if (BGImage != null)
                {
                    Color color = BGImage.color;
                    color.a = 0f;
                    BGImage.color = color;
                }
            });
    }


    // Pay the revive cost, update future cost, and save via CurrencyManager
    private bool RemoveGem()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogWarning("CurrencyManager not found – revive is free.");
            return true;
        }

        if (!cm.TrySpendGems(currentReviveCost))
        {
            // not enough gems
            Debug.Log("Not enough gems to revive.");
            return false;
        }

        // one successful revive
        reviveUsedCount++;

        // update cost for next time
        if (reviveUsedCount == 1)
        {
            currentReviveCost = baseReviveGemCost + secondReviveFlatIncrement;
        }
        else
        {
            currentReviveCost = Mathf.CeilToInt(currentReviveCost * laterReviveMultiplier);
        }

        RefreshGemCostUI();
        return true;
    }

    private void RefreshGemCostUI()
    {
        if (gemCostText != null)
            gemCostText.text = currentReviveCost.ToString();
    }

    // -------------------------------------------------------------------------
    // Button: No thanks
    // -------------------------------------------------------------------------

    public void NoThanksClick()
    {
        if (!isReviveActive) return;

        isReviveActive = false;
        isReviveButtonPressed = false;

        HideReviveAndShowLose();
    }

    private void HideReviveAndShowLose()
    {
        if (revivePanel != null)
        {
            revivePanel.transform.DOScale(Vector3.zero, 0.7f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    revivePanel.SetActive(false);
                    ShowLosePanel();
                });
        }
        else
        {
            ShowLosePanel();
        }
    }

    private void ShowLosePanel()
    {
        // Make sure game is paused while Lose panel is visible
        GameplayPause.SetPaused(true);

        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
            BGImage.DOFade(0.75f, 0.35f).SetUpdate(true);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(true);
            losePanel.transform.localScale = Vector3.zero;
            losePanel.transform.DOScale(Vector3.one, 0.8f)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }
    }


    // -------------------------------------------------------------------------
    // Actual revive logic
    // -------------------------------------------------------------------------

    private void ReviveTheStage1()
    {
        // 4) before everything, game is no longer over
        gameOver = false;

        // clear all grid occupancy so those revived blocks can really use the cells
        var board = FindObjectOfType<BoardGridXY>();      // or BoardGridXY1 if that’s what you use
        if (board != null)
            board.ClearAllOccupancy();

        // 2) re-enable redundant blocks to refill puzzle board
        GameObject bgRoot = GameObject.Find("BG");
        if (bgRoot != null)
        {
            Transform blocks = bgRoot.transform.Find("Blocks");
            if (blocks != null)
            {
                blocks.gameObject.SetActive(false);
            }

            Transform redundant = bgRoot.transform.Find("RedundantBlocks");
            if (redundant != null)
            {
                redundant.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("RevivePanel: 'RedundantBlocks' child not found under 'BG'.");
            }
        }
        else
        {
            Debug.LogWarning("RevivePanel: BG GameObject not found in scene.");
        }

        // 3) refill PlayerGate health to max
        PlayerGateStats gate = FindObjectOfType<PlayerGateStats>();
        if (gate != null)
        {
            gate.isPlayerGateDestroyed = false;
            gate.currentHP = gate.maxHealth;          // from CharacterStats
            if (gate.healthBar != null)
                gate.healthBar.SetCurrentHealth(gate.currentHP, gate.maxHealth);
        }
        else
        {
            Debug.LogWarning("RevivePanel: PlayerGateStats not found – cannot refill gate HP.");
        }

        // start waves again after short delay (real time, independent of pause)
        StartCoroutine(ActivateWaveSpawner());

        // unfreeze all enemies and let them search targets again
        var enemies = FindObjectsOfType<EnemyManager>();
        foreach (var enemy in enemies)
        {
            enemy.ResetAfterRevive();
        }
    }

    private void ReviveTheStage()
    {
        // we are not game over anymore
        gameOver = false;

        // 1) Clear board occupancy so new blocks can use the cells
        if (board == null)
            board = FindObjectOfType<BoardGridXY>();

        if (board != null)
            board.ClearAllOccupancy();

        // 2) Remove ALL existing blocks from scene (if any)
        //    This handles the "if exists any, they must be removed" case.
        var allPieces = FindObjectsOfType<PieceSimple>(false);
        foreach (var p in allPieces)
        {
            if (p != null && p.gameObject.activeInHierarchy)
            {
                Destroy(p.gameObject);   // or p.gameObject.SetActive(false);
            }
        }

        // 2.5) Remove ALL current players that are on the stage
        //      (so spawn points / stages are completely free)

        var allPlayers = FindObjectsOfType<PlayerManager>(false);

        foreach (var pl in allPlayers)
        {
            if (pl == null || !pl.gameObject.activeInHierarchy)
                continue;

            // Ignore the PlayerCastle (gate) which also has PlayerManager
            if (pl.CompareTag("PlayerGate"))   // or "PlayerCastle" if that's the tag you set
                continue;

            // Only delete players that are actually on the stage (Lock state)
            if (pl.currentState == pl.PlayerLockState)
            {
                Destroy(pl.gameObject);
            }
        }


        // 3) Swap board roots: disable original Blocks, enable RedundantBlocks
        if (blocksRoot != null)
            blocksRoot.SetActive(false);
        else
            Debug.LogWarning("RevivePanel: blocksRoot is NOT assigned in the Inspector!");

        if (redundantBlocksRoot != null)
            redundantBlocksRoot.SetActive(true);
        else
            Debug.LogWarning("RevivePanel: redundantBlocksRoot is NOT assigned in the Inspector!");

        // 4) Refill PlayerGate health
        PlayerGateStats gate = FindObjectOfType<PlayerGateStats>();
        if (gate != null)
        {
            gate.isPlayerGateDestroyed = false;
            gate.currentHP = gate.maxHealth;
            if (gate.healthBar != null)
                gate.healthBar.SetCurrentHealth(gate.currentHP, gate.maxHealth);
        }
        else
        {
            Debug.LogWarning("RevivePanel: PlayerGateStats not found – cannot refill gate HP.");
        }

        // 5) Restart waves after a short REAL-TIME delay
        StartCoroutine(ActivateWaveSpawner());

        // 6) Unfreeze all enemies and let them search targets again
        var enemies = FindObjectsOfType<EnemyManager>();
        foreach (var enemy in enemies)
        {
            enemy.ResetAfterRevive();
        }
    }

    IEnumerator ActivateWaveSpawner()
    {
        // real-time delay so it works even when timescale is 0
        yield return new WaitForSecondsRealtime(0.1f);

        if (playerWaveManager == null)
            playerWaveManager = FindObjectOfType<PlayerWaveManager>();

        if (playerWaveManager != null)
        {
            playerWaveManager.RestartAfterRevive();
        }
    }
}


public class RevivePanel2 : MonoBehaviour
{
    public static RevivePanel Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private Button reviveButton;
    [SerializeField] private Button noThanksButton;

    [SerializeField] private GameObject revivePanel;   // the orange window
    [SerializeField] private GameObject losePanel;     // pink Lose panel

    [SerializeField] private Image BGImage;            // dark overlay behind panels
    [SerializeField] private Image progressBar;        // circular timer bar
    [SerializeField] private Image whiteBar;           // inner white ring (optional)
    [SerializeField] private TMP_Text barText;         // countdown number in the circle
    [SerializeField] private TMP_Text gemCostText;     // the purple gem cost number on the button

    [Header("Revive Settings")]
    [SerializeField] private float countdownDuration = 9f;

    [SerializeField] private int baseReviveGemCost = 1;       // first revive
    [SerializeField] private int secondReviveFlatIncrement = 5;// second revive = base + 5
    [SerializeField] private float laterReviveMultiplier = 1.3f; // from 3rd revive onward: cost *= 1.3

    [Header("State (debug)")]
    [SerializeField] private bool isReviveActive = false;
    [SerializeField] private bool isReviveButtonPressed = false;
    [SerializeField] public bool gameOver = false;            // exposed so you can watch it in Inspector

    private float timer;
    private int currentReviveCost;
    private int reviveUsedCount;

    private PlayerWaveManager playerWaveManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        //Instance = this;

        playerWaveManager = FindObjectOfType<PlayerWaveManager>();

        // per-run initialise
        currentReviveCost = baseReviveGemCost;
        reviveUsedCount = 0;

        if (revivePanel != null)
            revivePanel.SetActive(false);

        // make sure overlay starts transparent
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
        }
    }

    private void OnEnable()
    {
        PlayerGateStats.OnGateDestroyed += HandleGateDestroyed;
    }

    private void OnDisable()
    {
        PlayerGateStats.OnGateDestroyed -= HandleGateDestroyed;
    }

    private void Start()
    {
        if (reviveButton != null)
            reviveButton.onClick.AddListener(ReviveLevel);

        if (noThanksButton != null)
            noThanksButton.onClick.AddListener(NoThanksClick);

        RefreshGemCostUI();
    }

    private void Update()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        timer -= Time.deltaTime;

        float t = Mathf.Clamp01(timer / countdownDuration); // 1 → 0
        if (progressBar != null)
            progressBar.fillAmount = t;
        if (whiteBar != null)
            whiteBar.fillAmount = t;

        if (barText != null)
            barText.text = Mathf.CeilToInt(Mathf.Max(0f, timer)).ToString();

        if (timer <= 0f)
        {
            isReviveActive = false;
            isReviveButtonPressed = false;
            HideReviveAndShowLose();
        }
    }

    // -------------------------------------------------------------------------
    // Game over → show revive panel
    // -------------------------------------------------------------------------

    private void HandleGateDestroyed()
    {
        // PlayerGateStats told us the gate is dead
        gameOver = true;
        ShowRevivePanel();
    }

    private void ShowRevivePanel()
    {
        if (revivePanel == null)
            return;

        RefreshGemCostUI();

        revivePanel.SetActive(true);
        isReviveButtonPressed = false;
        isReviveActive = true;

        timer = countdownDuration;

        if (progressBar != null) progressBar.fillAmount = 1f;
        if (whiteBar != null) whiteBar.fillAmount = 1f;
        if (barText != null) barText.text = Mathf.CeilToInt(countdownDuration).ToString();

        // fade in overlay
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
            BGImage.DOFade(0.75f, 0.35f);
        }

        // scale in panel
        revivePanel.transform.localScale = Vector3.zero;
        revivePanel.transform.DOScale(Vector3.one, 0.7f)
            .SetEase(Ease.OutBack);
    }

    // -------------------------------------------------------------------------
    // Button: Revive
    // -------------------------------------------------------------------------

    public void ReviveLevel()
    {
        if (!isReviveActive || isReviveButtonPressed)
            return;

        // Try to pay gems first
        if (!RemoveGem())
        {
            // Not enough gems → treat like "No, thanks"
            isReviveActive = false;
            isReviveButtonPressed = true;
            HideReviveAndShowLose();
            return;
        }

        isReviveButtonPressed = true;
        isReviveActive = false;

        if (revivePanel == null)
        {
            // fail-safe – still do revive logic
            gameOver = false;
            ReviveTheStage();
            return;
        }

        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               revivePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f;
                   BGImage.color = color;
               }

               // important: we are no longer in game over
               gameOver = false;

               // actually revive the gameplay
               ReviveTheStage();
           });
    }

    // Pay the revive cost, update future cost, and save via CurrencyManager
    private bool RemoveGem()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null)
        {
            Debug.LogWarning("CurrencyManager not found – revive is free.");
            return true;
        }

        if (!cm.TrySpendGems(currentReviveCost))
        {
            // not enough gems
            Debug.Log("Not enough gems to revive.");
            return false;
        }

        // one successful revive
        reviveUsedCount++;

        // update cost for next time
        if (reviveUsedCount == 1)
        {
            currentReviveCost = baseReviveGemCost + secondReviveFlatIncrement;
        }
        else
        {
            currentReviveCost = Mathf.CeilToInt(currentReviveCost * laterReviveMultiplier);
        }

        RefreshGemCostUI();
        return true;
    }

    private void RefreshGemCostUI()
    {
        if (gemCostText != null)
            gemCostText.text = currentReviveCost.ToString();
    }

    // -------------------------------------------------------------------------
    // Button: No thanks
    // -------------------------------------------------------------------------

    public void NoThanksClick()
    {
        if (!isReviveActive) return;

        isReviveActive = false;
        isReviveButtonPressed = false;

        HideReviveAndShowLose();
    }

    private void HideReviveAndShowLose()
    {
        if (revivePanel != null)
        {
            revivePanel.transform.DOScale(Vector3.zero, 0.7f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    revivePanel.SetActive(false);
                    ShowLosePanel();
                });
        }
        else
        {
            ShowLosePanel();
        }
    }

    private void ShowLosePanel()
    {
        if (BGImage != null)
        {
            Color c = BGImage.color;
            c.a = 0f;
            BGImage.color = c;
            BGImage.DOFade(0.75f, 0.35f);
        }

        if (losePanel != null)
        {
            losePanel.SetActive(true);
            losePanel.transform.localScale = Vector3.zero;
            losePanel.transform.DOScale(Vector3.one, 0.8f)
                .SetEase(Ease.OutBack);
        }
    }

    // -------------------------------------------------------------------------
    // Actual revive logic
    // -------------------------------------------------------------------------

    private void ReviveTheStage()
    {
        // 4) before everything, game is no longer over
        gameOver = false;

        // 2) re-enable redundant blocks to refill puzzle board
        // BG / RedundantBlocks

        // clear all grid occupancy so those revived blocks can really use the cells
        var board = FindObjectOfType<BoardGridXY>();      // or BoardGridXY1 if that’s what you use
        if (board != null)
            board.ClearAllOccupancy();


        GameObject bgRoot = GameObject.Find("BG");
        if (bgRoot != null)
        {
            Transform blocks = bgRoot.transform.Find("Blocks");
            if (blocks != null)
            {
                blocks.gameObject.SetActive(false);
            }

            Transform redundant = bgRoot.transform.Find("RedundantBlocks");
            if (redundant != null)
            {
                redundant.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("RevivePanel: 'RedundantBlocks' child not found under 'BG'.");
            }

          
        }
        else
        {
            Debug.LogWarning("RevivePanel: BG GameObject not found in scene.");
        }

        // 3) refill PlayerGate health to max
        PlayerGateStats gate = FindObjectOfType<PlayerGateStats>();
        if (gate != null)
        {
            gate.isPlayerGateDestroyed = false;
            gate.currentHP = gate.maxHealth;          // from CharacterStats
            if (gate.healthBar != null)
                gate.healthBar.SetCurrentHealth(gate.currentHP, gate.maxHealth);
        }
        else
        {
            Debug.LogWarning("RevivePanel: PlayerGateStats not found – cannot refill gate HP.");
        }

        StartCoroutine(ActivateWaveSpawner());


        // 5) unfreeze all enemies and let them search targets again
        var enemies = FindObjectsOfType<EnemyManager>();
        foreach (var enemy in enemies)
        {
            enemy.ResetAfterRevive();
        }

    }

    IEnumerator ActivateWaveSpawner()
    {
        yield return new WaitForSeconds(0.5f);
        // 1) allow PlayerWaveManager to spawn waves again
        if (playerWaveManager == null)
            playerWaveManager = FindObjectOfType<PlayerWaveManager>();

        if (playerWaveManager != null)
        {
            playerWaveManager.BeginWaves();
        }
    }
}



public class RevivePanel1 : MonoBehaviour
{
    public static RevivePanel instance;

    [SerializeField] Button reviveButton;
    [SerializeField] Button noThanksButton;
    [SerializeField] GameObject revivePanel;
    [SerializeField] GameObject losePanel;
    [SerializeField] Image BGImage;
    [SerializeField] Image progressBar;
    [SerializeField] TextMeshProUGUI barText;
    public bool isReviveButtonPressed;
    public bool isReviveActive;
    public float countdownDuration = 9f;
    private void Awake()
    {
        if (instance == null)
        {
            //instance = this;
        }
    }

 public void ShowCoroutine()
    {
        if (/*GameManager.instance.gameEnded*/  isReviveActive && !isReviveButtonPressed)
        {
            Debug.Log(" Revive is active");

            //revivePanel.gameObject.SetActive(true);
            StartCoroutine(StartCountdown());
        }
    }
    private IEnumerator StartCountdown()
    {

        float elapsedTime = 0f; // Time that has passed
        float remainingTime = countdownDuration; // Remaining time

        while (elapsedTime < countdownDuration)
        {
            // Update the progress bar fillAmount (0 to 1)
            progressBar.fillAmount = elapsedTime / countdownDuration;

            // Update the countdown text
            barText.text = Mathf.CeilToInt(remainingTime).ToString();

            // Wait for the next frame
            yield return null;

            // Update elapsed and remaining time
            elapsedTime += Time.deltaTime;
            remainingTime = countdownDuration - elapsedTime;
        }

        // Ensure progress bar is fully filled and text shows 0 at the end
        progressBar.fillAmount = 1f;
        barText.text = "0";
        //******************************
        if (!isReviveButtonPressed && isReviveActive)
        {
            Debug.Log("Inside The  Start Coroutine In Revive Class");

            yield return  new WaitForSeconds(1f);
            revivePanel.transform.DOScale(Vector3.zero, 0.7f)
              .SetEase(Ease.OutBack).OnComplete(() =>
              {
                  revivePanel.gameObject.SetActive(false);
                  if (BGImage != null)
                  {
                      Color color = BGImage.color;
                      color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
                  }
                  ShowLosePanel();
                  isReviveActive = false;

              }
              );

        }
        
    }

    private void ShowLosePanel()
    {
        if (isReviveButtonPressed )
        {
            return;
        }
        Debug.Log("Inside the Show Lose Panel");
        losePanel.transform.localScale = Vector3.zero;

        // Activate the panel
        losePanel.gameObject.SetActive(true);

        // Animate the scale from 0 to its default scale (1, 1, 1)
        losePanel.transform.DOScale(Vector3.one, 0.5f)
            .SetEase(Ease.OutBack).OnComplete(()=> {
            
                Graphic mainPanelGraphic = gameObject.GetComponent<Graphic>(); // Check if it has Image or similar
                if (mainPanelGraphic != null)
                {
                    mainPanelGraphic.raycastTarget = false;
                    Debug.Log("Raycast Target of main panel is now disabled.");
                }

            });
    }
    private void RemoveGem()
    {

        // ***  In this place we should Remove  Required Gem Number from CurrencyManager ***


    }

    public void ReviveLevel()
    {
        isReviveButtonPressed = true;
        isReviveActive = false;
        RemoveGem();
        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
           .SetEase(Ease.OutBack).OnComplete(() =>
           {
               revivePanel.gameObject.SetActive(false);
               if (BGImage != null)
               {
                   Color color = BGImage.color;
                   color.a = 0f; // Set alpha to 0
                   BGImage.color = color;
               }
               //StartCoroutine(ShowAnimation(3));

               //  *** In this Place we must set the GameIsOver = false ***

               ReviveTheStage();
           }
           );
    }

    private void ReviveTheStage()
    {

    }
    public void NoThanksClick()
    {
      
        Debug.Log("Inside The  No Thanks Button");
        revivePanel.transform.DOScale(Vector3.zero, 0.7f)
             .SetEase(Ease.OutBack).OnComplete(() =>
             {
                 revivePanel.gameObject.SetActive(false);
                 if (BGImage != null)
                 {
                     Color color = BGImage.color;
                     color.a = 0f; // Set alpha to 0
                      BGImage.color = color;
                 }
                 ShowLosePanel();
                 isReviveActive = false;
             }
             );

    }
}
