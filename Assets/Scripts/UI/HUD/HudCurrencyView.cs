using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class HudCurrencyView : MonoBehaviour
{
    /// <summary>When the coin/gem/XP chips are allowed to be on screen.</summary>
    public enum ResourceVisibility
    {
        /// <summary>Hidden during a stage, revealed on the win - decided by whether
        /// this scene has a LevelGameManager. Correct for both the menu and all
        /// 20 stage scenes without anything being authored per scene.</summary>
        Auto = 0,
        /// <summary>Never hidden (the menu's behaviour).</summary>
        AlwaysVisible = 1,
        /// <summary>Always hidden until a win, whatever scene this is.</summary>
        HideUntilWin = 2,
    }

    [Header("Currency UI")]
    [SerializeField] TMP_Text coinsText;
    [SerializeField] TMP_Text gemsText;
    [SerializeField] TMP_Text heroXpText;
    [SerializeField] Button pauseButton;

    [Header("Hide resources during a stage")]
    [Tooltip("Auto = hide the coin/gem/XP chips while a stage is being played and " +
             "reveal them when it is won. The PAUSE BUTTON IS NEVER HIDDEN - it is " +
             "a child of this HUD but the player needs it mid-stage.")]
    [SerializeField] private ResourceVisibility resourceVisibility = ResourceVisibility.Auto;

    [Tooltip("Seconds the chips take to fade in on a win. 0 = pop in.")]
    [SerializeField] private float revealFadeDuration = 0.35f;

    [Tooltip("Optional. The chips to hide. Left empty they are found from the three " +
             "currency labels above - each chip is the child of this HUD that " +
             "contains one of them.")]
    [SerializeField] private GameObject[] resourceChips;

    /// <summary>True while a stage is being played and the chips are hidden.</summary>
    private bool chipsHidden;
    private GameObject[] _resolvedChips;

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

    // ------------------------------------------------------------------
    //  Resource chips: hidden while a stage is played, revealed on the win
    // ------------------------------------------------------------------

    /// <summary>
    /// Start() rather than Awake(): the Auto test looks for a LevelGameManager
    /// anywhere in the scene, and every scene object is guaranteed to exist by
    /// the time any Start runs.
    /// </summary>
    void Start()
    {
        if (ShouldHideChipsDuringPlay()) HideResourceChips();
    }

    private bool ShouldHideChipsDuringPlay()
    {
        switch (resourceVisibility)
        {
            case ResourceVisibility.AlwaysVisible: return false;
            case ResourceVisibility.HideUntilWin: return true;

            // A LevelGameManager means "this scene is a stage being played".
            // It is the project's authority on battle state already, so the menu
            // (which has none) keeps its chips and all 20 stages hide theirs
            // with nothing authored per scene.
            default: return FindObjectOfType<LevelGameManager>(true) != null;
        }
    }

    /// <summary>
    /// Reveals the chips after a stage is won. Static so WinPanel can call it
    /// without holding a reference; harmless when there is no HUD or the chips
    /// were never hidden.
    /// </summary>
    public static void RevealResourcesForWin()
    {
        if (Instance) Instance.RevealResourceChips();
    }

    public void HideResourceChips()
    {
        chipsHidden = true;

        foreach (var chip in ResolveResourceChips())
        {
            if (!chip) continue;
            chip.SetActive(false);
        }
    }

    /// <summary>
    /// Brings the chips back, fading them in. They are ACTIVATED as well as
    /// faded because two of the three (coins and hero XP) are authored inactive
    /// in the stage scenes - without this the claim animation would fly coins
    /// towards a chip that never appears.
    /// </summary>
    public void RevealResourceChips()
    {
        if (!chipsHidden) return;
        chipsHidden = false;

        foreach (var chip in ResolveResourceChips())
        {
            if (!chip) continue;

            chip.SetActive(true);

            if (revealFadeDuration <= 0f) continue;

            // Added at runtime rather than authored on each chip: this is the
            // only thing that needs one, and cloning/adding beats editing the
            // same prefab in 20 scenes.
            var cg = chip.GetComponent<CanvasGroup>();
            if (!cg) cg = chip.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            cg.DOKill();
            // SetUpdate(true): the win pauses gameplay, and a timeScale-driven
            // tween would never finish. WinPanel waits in real time for the
            // same reason.
            cg.DOFade(1f, revealFadeDuration).SetUpdate(true);
        }
    }

    /// <summary>
    /// The chips, from the explicit list or - when it is empty - by walking up
    /// from each currency label to the child of THIS HUD that contains it.
    /// That walk is what makes the feature need no scene edit: the labels were
    /// already wired in both the menu and the stage scenes.
    /// </summary>
    private GameObject[] ResolveResourceChips()
    {
        if (resourceChips != null && resourceChips.Length > 0) return resourceChips;
        if (_resolvedChips != null) return _resolvedChips;

        var found = new List<GameObject>(3);
        AddChipOf(coinsText, found);
        AddChipOf(gemsText, found);
        AddChipOf(heroXpText, found);

        _resolvedChips = found.ToArray();

        if (_resolvedChips.Length == 0)
            Debug.LogWarning("[HudCurrencyView] No resource chips found - assign " +
                             "resourceChips, or the currency labels above.", this);

        return _resolvedChips;
    }

    private void AddChipOf(TMP_Text label, List<GameObject> into)
    {
        if (!label) return;

        var t = label.transform;
        while (t.parent != null && t.parent != transform) t = t.parent;
        if (t.parent != transform) return;          // not under this HUD at all

        if (!into.Contains(t.gameObject)) into.Add(t.gameObject);
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
