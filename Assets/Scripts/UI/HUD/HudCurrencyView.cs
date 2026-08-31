using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HudCurrencyView : MonoBehaviour
{
    [Header("Currency UI")]
    [SerializeField] TMP_Text coinsText;
    [SerializeField] TMP_Text gemsText;
    [SerializeField] TMP_Text heroXpText;
    [SerializeField] Button pauseButton;

    [Header("Main HUD root / canvas")]  

    [Header("Main HUD root / canvas")]
    [SerializeField] private Canvas mainCanvas;       // root Canvas on the scene
    [SerializeField] private CanvasGroup hudCanvasGroup;

    // The swapOpen value HandlePanelsVisibility last actually wrote to
    // hudCanvasGroup. Null until the first pass, so the correct state is
    // established once on frame one and then only on a real change.
    private bool? swapOpenApplied;

    [Header("Panels that affect HUD")]
    [SerializeField] private GameObject bucketStatsPanelRoot;   // BucketStatsPanel root
    [SerializeField] private Canvas bucketStatsCanvas;          // Canvas sitting ON BucketStatsPanel
    [SerializeField] private GameObject swapPanelRoot;          // SwapPanel-Deployed&Undeployed root

    [SerializeField] private GameObject mainMenuPanelRoot;   // <- drag MainMenuPanel here

    [SerializeField] private GameObject unitsDetailPanelRoot;   // NEW: Units DetailView panel root

    // Singleton
    public static HudCurrencyView Instance { get; private set; }

    bool isGameplayPaused;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!mainCanvas)
            mainCanvas = FindObjectOfType<Canvas>();

        if (!bucketStatsCanvas && bucketStatsPanelRoot)
            bucketStatsCanvas = bucketStatsPanelRoot.GetComponent<Canvas>();
    }

    void OnEnable()
    {
        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;

            HandleCurrencyChanged("Coins", CurrencyManager.Instance.Coins, 0);
            HandleCurrencyChanged("Gems", CurrencyManager.Instance.Gems, 0);
            HandleCurrencyChanged("HeroXP", CurrencyManager.Instance.HeroXP, 0);
        }
    }

    void OnDisable()
    {
        if (CurrencyManager.Instance != null)
            CurrencyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    void HandleCurrencyChanged(string currency, int newValue, int delta)
    {
        switch (currency)
        {
            case "Coins":
                if (coinsText) coinsText.text = newValue.ToString();
                break;
            case "Gems":
                if (gemsText) gemsText.text = newValue.ToString();
                break;
            case "HeroXP":
                if (heroXpText) heroXpText.text = newValue.ToString();
                break;
        }
    }

    public bool IsGameplayPaused => isGameplayPaused;

    // 1) Single toggle method for your button
    public void ToggleGameplayPause()
    {
        if (isGameplayPaused)
            ResumeGameplay();
        else
            PauseGameplay();
    }

    // 2) Explicit pause / resume (useful from Win/Lose/PlayerManager)
    public void PauseGameplay()
    {
        SetGameplayPaused(true);
    }

    public void ResumeGameplay()
    {
        SetGameplayPaused(false);
    }

    void SetGameplayPaused(bool paused)
    {
        if (isGameplayPaused == paused)
            return;

        isGameplayPaused = paused;

        //// roguelite Manager 



        var enemySpawer = FindObjectOfType<EnemySpawner>();
        if (enemySpawer)
            enemySpawer.enabled = !paused;

        // Player Spawner
        var playerSpawer = FindObjectOfType<PlayerWaveManager>();
        if (playerSpawer)
            playerSpawer.enabled = !paused;


        // Board input
        var input = FindObjectOfType<BoardInputController>();
        if (input)
            input.enabled = !paused;

        // Players
        var players = FindObjectsOfType<PlayerManager>();
        foreach (var player in players)
        {
            if (!player) continue;

            player.enabled = !paused;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb)
            {
                if (paused)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    rb.simulated = false;
                }
                else
                {
                    rb.simulated = true;
                }
            }

            var anim = player.GetComponentInChildren<Animator>();
            if (anim)
                anim.speed = paused ? 0f : 1f;
        }

        // Enemies
        var enemies = FindObjectsOfType<EnemyManager>();
        foreach (var enemy in enemies)
        {
            if (!enemy) continue;

            enemy.enabled = !paused;

            var rb = enemy.GetComponent<Rigidbody2D>();
            if (rb)
            {
                if (paused)
                {
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                    rb.simulated = false;
                }
                else
                {
                    rb.simulated = true;
                }
            }

            var anim = enemy.GetComponentInChildren<Animator>();
            if (anim)
                anim.speed = paused ? 0f : 1f;
        }
    }

    void LateUpdate()
    {
        HandleBucketStatsOrder();
        HandlePanelsVisibility();
    }

    // 1) Make BucketStatsPanel render above HUD
    void HandleBucketStatsOrder()
    {
        if (!bucketStatsPanelRoot || !bucketStatsCanvas || !mainCanvas)
            return;

        bool statsOpen = bucketStatsPanelRoot.activeInHierarchy;

        if (statsOpen)
        {
            bucketStatsCanvas.overrideSorting = true;
            bucketStatsCanvas.sortingOrder = mainCanvas.sortingOrder + 5;
        }
        else
        {
            // optional: turn off override when closed
            bucketStatsCanvas.overrideSorting = false;
        }
    }

    // 2) Hide HUD when SwapPanel is open
    void HandleSwapPanelVisibility1()
    {
        if (!hudCanvasGroup || !swapPanelRoot)
            return;

        bool swapOpen = swapPanelRoot.activeInHierarchy;

        hudCanvasGroup.alpha = swapOpen ? 0f : 1f;
        hudCanvasGroup.interactable = !swapOpen;
        hudCanvasGroup.blocksRaycasts = !swapOpen;
    }

    void HandleSwapPanelVisibility2()
    {
        if (swapPanelRoot == null)
            return;

        bool swapOpen = swapPanelRoot.activeInHierarchy;

        // Hide / show HUD
        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = swapOpen ? 0f : 1f;
            hudCanvasGroup.interactable = !swapOpen;
            hudCanvasGroup.blocksRaycasts = !swapOpen;
        }

        // Hide / show MainMenuPanel as well
        if (mainMenuPanelRoot != null)
        {
            mainMenuPanelRoot.SetActive(!swapOpen);
        }
    }
    void HandlePanelsVisibility()
    {
        bool swapOpen = swapPanelRoot != null && swapPanelRoot.activeInHierarchy;
        bool detailOpen = unitsDetailPanelRoot != null && unitsDetailPanelRoot.activeInHierarchy;

        // HUD: only hide when SwapPanel is open.
        //
        // Written ONLY ON A CHANGE of swapOpen, never every frame. This runs in
        // LateUpdate, so an unconditional write here stomps any external fade of
        // the same CanvasGroup back to 1 after DOTween has already set it -
        // which is exactly why BattlePhaseTransition could not fade the HUD out
        // while it faded every other panel fine.
        if (hudCanvasGroup != null && swapOpenApplied != swapOpen)
        {
            hudCanvasGroup.alpha = swapOpen ? 0f : 1f;
            hudCanvasGroup.interactable = !swapOpen;
            hudCanvasGroup.blocksRaycasts = !swapOpen;
        }

        swapOpenApplied = swapOpen;

        // MainMenu: hide when SwapPanel OR Units DetailView is open
        if (mainMenuPanelRoot != null)
        {
            bool shouldHideMainMenu = swapOpen || detailOpen;
            mainMenuPanelRoot.SetActive(!shouldHideMainMenu);
        }
    }

}
