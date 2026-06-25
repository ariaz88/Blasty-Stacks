using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;




public class WinPanel : MonoBehaviour
{
    [System.Serializable]
    public class RewardValues
    {
        public int coins;
        public int gems;
        public int heroXP;

        public void Add(RewardValues other)
        {
            if (other == null) return;
            coins += other.coins;
            gems += other.gems;
            heroXP += other.heroXP;
        }

        public static RewardValues FromScaled(RewardValues source, float factor)
        {
            if (source == null) return new RewardValues();
            return new RewardValues
            {
                coins = Mathf.RoundToInt(source.coins * factor),
                gems = Mathf.RoundToInt(source.gems * factor),
                heroXP = Mathf.RoundToInt(source.heroXP * factor)
            };
        }
    }

    [Header("Reward config (usually comes from HomeManager.SharedRewardConfig)")]
    [Tooltip("Optional local override. If null, WinPanel will use HomeManager.SharedRewardConfig.")]
    [SerializeField] private StageRewardConfig rewardConfigOverride;

    [Header("UI - Reward texts")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text gemsText;
    [SerializeField] private TMP_Text heroXPText;

    [Header("UI - Level header (top of panel)")]
    [SerializeField] private TMP_Text chapterStageText;
    [SerializeField] private string chapterLabelFormat = "{1}";

    [Header("Claim button & scene")]
    [SerializeField] private Button claimButton;
    [SerializeField] private string targetSceneName = "MenuScene";

    [Header("Optional fade out")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Resource animation anchors (from this panel)")]
    [SerializeField] private RectTransform coinSpawnPoint;
    [SerializeField] private RectTransform gemSpawnPoint;
    [SerializeField] private RectTransform xpSpawnPoint;

    [Header("Resource animation timing")]
    [SerializeField] private float resourceAnimDuration = 0.8f;

    // internal state
    private int hpCase; // 1, 2, or 3
    private RewardValues totalRewardRow = new RewardValues();
    private RewardValues totalGiven = new RewardValues();
    private bool rewardsClaimed;

    /* [SerializeField]*/
    public ProgressionConfigSO progressionConfig;


    private void Awake()
    {
        if (claimButton != null)
        {
            claimButton.onClick.RemoveAllListeners();
            claimButton.onClick.AddListener(OnClaimPressed);
        }

        // start hidden
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);
    }

    // Show the panel and compute rewards based on HP% (0..1).
    public void Show(float hpPercent)
    {
        if (hpPercent < 0f) hpPercent = 0f;
        if (hpPercent > 1f) hpPercent = 1f;

        rewardsClaimed = false;

        if (claimButton)
        {
            claimButton.interactable = true;
            claimButton.gameObject.SetActive(true); // visible at start
        }

        hpCase = CalculateHpCase(hpPercent);

        CalculateAllRewards();
        UpdateRewardTexts();
        UpdateLevelHeaderText();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        gameObject.SetActive(true);

    }

    private int CalculateHpCase(float hpPercent)
    {
        if (hpPercent >= 1f - 0.0001f)
            return 3;          // full HP

        if (hpPercent >= 0.5f)
            return 2;          // 50%–<100%

        return 1;              // <50%
    }

    private void CalculateAllRewards()
    {
        // Determine current stage (1-based) from HomeManager
        int stage1Based = HomeManager.CurrentStage1Based;
        if (stage1Based < 1) stage1Based = 1;

        // Get shared config from HomeManager, or fall back to local override
        StageRewardConfig cfg = HomeManager.SharedRewardConfig;
        if (cfg == null)
        {
            cfg = rewardConfigOverride;
        }

        if (cfg == null)
        {
            Debug.LogWarning("[WinPanel] No StageRewardConfig found. Rewards will be zero.");
            totalRewardRow = new RewardValues();
            totalGiven = new RewardValues();
            return;
        }

        totalRewardRow = StageRewardCalculator.GetRewardForStageAndHpCase(stage1Based, hpCase, cfg);

        // What we actually give to the player (can be modified later if needed)
        totalGiven = new RewardValues
        {
            coins = totalRewardRow.coins,
            gems = totalRewardRow.gems,
            heroXP = totalRewardRow.heroXP
        };
    }

    private void UpdateRewardTexts()
    {
        if (coinsText != null)
            coinsText.text = totalRewardRow.coins.ToString();

        if (gemsText != null)
            gemsText.text = totalRewardRow.gems.ToString();

        if (heroXPText != null)
            heroXPText.text = totalRewardRow.heroXP.ToString();
    }

    private void UpdateLevelHeaderText1()
    {
        if (chapterStageText == null)
            return;

        int chapter = HomeManager.CurrentLevelId;
        if (chapter < 1) chapter = 1;

        int stage = HomeManager.CurrentStage1Based;
        if (stage < 1) stage = 1;

        if (string.IsNullOrEmpty(chapterLabelFormat))
        {
            chapterStageText.text = $"{stage}";
        }
        else
        {
            //chapterStageText.text = string.Format(chapterLabelFormat, chapter, stage);
            chapterStageText.text = $"{stage}";

        }
    }
    private void UpdateLevelHeaderText()
    {
        if (chapterStageText == null)
            return;

        int chapter = 1;
        int stage = 1;

        // Prefer LevelManager as the single source of truth
        if (LevelManager.Instance != null)
        {
            // CurrentStage is global additive index: 1, 2, 3, ... 21, 22, ...
            LevelManager.Instance.GlobalToLevelStage(
                LevelManager.CurrentStage,
                out chapter,
                out stage);
        }
        else
        {
            // Fallback to HomeManager if LevelManager is missing (editor tests etc.)
            chapter = Mathf.Max(1, HomeManager.CurrentLevelId);
            stage = Mathf.Max(1, HomeManager.CurrentStage1Based);
        }
        if (string.IsNullOrEmpty(chapterLabelFormat))
        {
            chapterStageText.text = $"{stage}";
        }
        else
        {
            //chapterStageText.text = string.Format(chapterLabelFormat, chapter, stage);
            chapterStageText.text = $"{stage}";

        }

    }


    private void OnClaimPressed()
    {
        if (rewardsClaimed) return;
        rewardsClaimed = true;

        // Hide the button instead of showing disabled style
        if (claimButton)
        {
            claimButton.interactable = false;
            claimButton.gameObject.SetActive(false);
        }

        StartCoroutine(ClaimRoutine());
    }

    private IEnumerator ClaimRoutine1()
    {
        // 1) play resource fly animations (COIN + GEM + XP) when we press Claim
        yield return StartCoroutine(PlayResourceFlyAnimations());

        // 2) actually give rewards to the player
        ApplyRewardsToPlayer();

        // 3) fade out panel and then close
        if (canvasGroup != null && fadeDuration > 0f)
        {
            float t = 0f;
            float startAlpha = canvasGroup.alpha;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime; // unscaled, in case timescale is 0
                float k = Mathf.Clamp01(t / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);
        LevelManager.Instance.MarkLevelWon();

        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }
    private IEnumerator ClaimRoutine()
    {
        // 1) play resource fly animations (COIN + GEM + XP) when we press Claim
        yield return StartCoroutine(PlayResourceFlyAnimations());

        // 2) actually give rewards to the player
        ApplyRewardsToPlayer();

        // 3) fade out panel and then close
        if (canvasGroup != null && fadeDuration > 0f)
        {
            float t = 0f;
            float startAlpha = canvasGroup.alpha;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime; // unscaled, in case timescale is 0
                float k = Mathf.Clamp01(t / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);



        // --- IMPORTANT PART: advance LevelManager’s global stage index ---
        if (LevelManager.Instance != null)
        {
            int nextStage = LevelManager.CurrentStage + 1;
            // We only want to update the index & save it; MenuScene will be loaded manually.
            LevelManager.Instance.SetStage(nextStage, loadScene: false);
        }

        // Return to Menu (or whatever you set as targetSceneName)
        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }


    private IEnumerator PlayResourceFlyAnimations()
    {
        var anim = ResourcesAnimationManager.instance;
        if (anim != null)
        {
            // Coins
            if (totalGiven.coins > 0 && coinSpawnPoint != null)
            {
                anim.AddCoinsFromUI(coinSpawnPoint, totalGiven.coins);
            }

            // Gems
            if (totalGiven.gems > 0 && gemSpawnPoint != null)
            {
                anim.AddGemsFromUI(gemSpawnPoint, totalGiven.gems);
            }

            // XP / Hero XP
            if (totalGiven.heroXP > 0 && xpSpawnPoint != null)
            {
                anim.AddHeroXPFromUI(xpSpawnPoint, totalGiven.heroXP);
            }
        }

        // Wait a bit so player sees the animation before we fade and load next scene
        yield return new WaitForSecondsRealtime(resourceAnimDuration);
    }

    private void ApplyRewardsToPlayer()
    {
        if (CurrencyManager.Instance != null)
        {
            if (totalGiven.coins != 0)
                CurrencyManager.Instance.AddCoins(totalGiven.coins);

            if (totalGiven.gems != 0)
                CurrencyManager.Instance.AddGems(totalGiven.gems);

            if (totalGiven.heroXP != 0)
                CurrencyManager.Instance.AddHeroXP(totalGiven.heroXP);
        }
        else
        {
            Debug.LogWarning("[WinPanel] CurrencyManager.Instance is null – rewards not applied!");
        }
    }




}


public class WinPanel3 : MonoBehaviour
{
    [System.Serializable]
    public class RewardValues
    {
        public int coins;
        public int gems;
        public int heroXP;

        public void Add(RewardValues other)
        {
            if (other == null) return;
            coins += other.coins;
            gems += other.gems;
            heroXP += other.heroXP;
        }

        public static RewardValues FromScaled(RewardValues source, float factor)
        {
            if (source == null) return new RewardValues();
            return new RewardValues
            {
                coins = Mathf.RoundToInt(source.coins * factor),
                gems = Mathf.RoundToInt(source.gems * factor),
                heroXP = Mathf.RoundToInt(source.heroXP * factor)
            };
        }
    }

    [Header("Stage base values (Stage 1, before progression)")]
    [Tooltip("Base values used for the main rewards (no bonus row in this version).")]
    [SerializeField] private RewardValues baseRewardStageValues;

    [Header("Per-stage progression")]
    [Tooltip("Extra reward per stage, e.g. 0.05 = +5% per stage (Stage 2 = base * 1.05, Stage 3 = base * 1.10, etc.).")]
    [SerializeField] private float perStageBonusPercent = 0.05f;

    [Header("Tier multipliers (for HP tiers)")]
    [SerializeField] private float tier1Multiplier = 1.0f;
    [SerializeField] private float tier2Multiplier = 1.2f;
    [SerializeField] private float tier3Multiplier = 1.5f;

    [Header("UI – Static texts (we no longer instantiate icons)")]
    [SerializeField] private TMP_Text coinsText;      // under the pre-placed coin icon
    [SerializeField] private TMP_Text gemsText;       // under the pre-placed gem icon
    [SerializeField] private TMP_Text heroXPText;     // under the pre-placed XP/hero icon

    [Header("UI – Level header (top of panel)")]
    [SerializeField] private TMP_Text chapterStageText;
    [Tooltip("Optional: current chapter number if you want to show 'Chapter X - Stage Y'.")]
    [SerializeField] private int currentChapterNumber = 1;

    [Header("Claim button")]
    [SerializeField] private Button claimButton;
    [SerializeField] private string targetSceneName = "MenuScene";

    [Header("Optional fade out")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Resource animation anchors (from this panel)")]
    [SerializeField] private RectTransform coinSpawnPoint;
    [SerializeField] private RectTransform gemSpawnPoint;
    [SerializeField] private RectTransform xpSpawnPoint;

    [Header("Resource animation timing")]
    [SerializeField] private float resourceAnimDuration = 0.8f;

    // internal state
    private int hpCase; // 1, 2, or 3: 1 = <50%, 2 = 50–<100, 3 = 100%

    // Stage-scaled base
    private RewardValues stageScaledRewardBase;

    // HP-tiered rewards (R1/R2/R3)
    private RewardValues rewardTier1;
    private RewardValues rewardTier2;
    private RewardValues rewardTier3;

    // Sum of unlocked tiers (what we show & give)
    private RewardValues totalRewardRow;
    private RewardValues totalGiven;

    private bool rewardsClaimed;

    private void Awake()
    {
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimPressed);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the win panel and compute rewards based on HP% (0..1).
    /// </summary>
    public void Show(float hpPercent)
    {
        if (hpPercent < 0f) hpPercent = 0f;
        if (hpPercent > 1f) hpPercent = 1f;

        rewardsClaimed = false;
        if (claimButton) claimButton.interactable = true;
        if (claimButton) claimButton.gameObject.SetActive(true); // make sure it's visible at start

        hpCase = CalculateHpCase(hpPercent);

        CalculateAllRewards();
        UpdateRewardTexts();
        UpdateLevelHeaderText();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        gameObject.SetActive(true);

        // NOTE: we NO LONGER play coin animation at panel show
    }

    // Decide which HP case we are in: 1, 2, or 3
    private int CalculateHpCase(float hpPercent)
    {
        if (hpPercent >= 1f - 0.0001f)
            return 3;          // full HP

        if (hpPercent >= 0.5f)
            return 2;          // 50% – <100%

        return 1;              // <50%
    }

    private void CalculateAllRewards()
    {
        // 1) Determine current stage (1-based) from HomeManager
        int stage1Based = HomeManager.CurrentStage1Based;
        if (stage1Based < 1) stage1Based = 1;

        // 2) Compute stage multiplier
        float stageMultiplier = 1f + perStageBonusPercent * (stage1Based - 1);

        // 3) Apply stage multiplier to base reward
        stageScaledRewardBase = RewardValues.FromScaled(baseRewardStageValues, stageMultiplier);

        // 4) Compute HP-tiered reward values
        rewardTier1 = RewardValues.FromScaled(stageScaledRewardBase, tier1Multiplier);
        rewardTier2 = RewardValues.FromScaled(stageScaledRewardBase, tier2Multiplier);
        rewardTier3 = RewardValues.FromScaled(stageScaledRewardBase, tier3Multiplier);

        // 5) Sum unlocked tiers into main reward row (no bonus row in this version)
        totalRewardRow = new RewardValues();

        if (hpCase >= 1) totalRewardRow.Add(rewardTier1);
        if (hpCase >= 2) totalRewardRow.Add(rewardTier2);
        if (hpCase >= 3) totalRewardRow.Add(rewardTier3);

        // 6) Total actually given = reward row
        totalGiven = new RewardValues
        {
            coins = totalRewardRow.coins,
            gems = totalRewardRow.gems,
            heroXP = totalRewardRow.heroXP
        };
    }

    private void UpdateRewardTexts()
    {
        if (coinsText != null)
            coinsText.text = totalRewardRow.coins.ToString();

        if (gemsText != null)
            gemsText.text = totalRewardRow.gems.ToString();

        if (heroXPText != null)
            heroXPText.text = totalRewardRow.heroXP.ToString();
    }

    private void UpdateLevelHeaderText()
    {
        if (chapterStageText == null)
            return;

        int stage1Based = HomeManager.CurrentStage1Based;
        if (stage1Based < 1) stage1Based = 1;

        // Example format: "Chapter 1 - Stage 3"
        chapterStageText.text = $"{stage1Based}";
    }

    private void OnClaimPressed()
    {
        if (rewardsClaimed) return;
        rewardsClaimed = true;

        // We don't want to show the disabled style – just hide the button visually
        if (claimButton)
        {
            claimButton.interactable = false;
            claimButton.gameObject.SetActive(false);
        }

        StartCoroutine(ClaimRoutine());
    }

    private IEnumerator ClaimRoutine()
    {
        // 1) play resource fly animations (COIN + GEM + XP) exactly when we press Claim
        yield return StartCoroutine(PlayResourceFlyAnimations());

        // 2) actually give rewards to the player (and save via CurrencyManager)
        ApplyRewardsToPlayer();

        // 3) fade out panel and then close
        if (canvasGroup != null && fadeDuration > 0f)
        {
            float t = 0f;
            float startAlpha = canvasGroup.alpha;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime; // unscaled, in case timescale is 0
                float k = Mathf.Clamp01(t / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);

        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }

    private IEnumerator PlayResourceFlyAnimations()
    {
        var anim = ResourcesAnimationManager.instance;
        if (anim != null)
        {
            // Coins
            if (totalGiven.coins > 0 && coinSpawnPoint != null)
            {
                // Assuming you have a method like this. If your signature is different,
                // just adjust the call.
                anim.AddCoinsFromUI(coinSpawnPoint, totalGiven.coins);
            }

            // Gems
            if (totalGiven.gems > 0 && gemSpawnPoint != null)
            {
                // You may need to implement AddGemsFromUI in ResourcesAnimationManager
                anim.AddGemsFromUI(gemSpawnPoint, totalGiven.gems);
            }

            // XP / Hero XP
            if (totalGiven.heroXP > 0 && xpSpawnPoint != null)
            {
                // You may need to implement AddHeroXPFromUI (or similar) in ResourcesAnimationManager
                anim.AddHeroXPFromUI(xpSpawnPoint, totalGiven.heroXP);
            }
        }

        // Wait a bit so player sees the animation before we fade and load next scene
        yield return new WaitForSecondsRealtime(resourceAnimDuration);
    }

    private void ApplyRewardsToPlayer()
    {
        if (CurrencyManager.Instance != null)
        {
            if (totalGiven.coins != 0)
                CurrencyManager.Instance.AddCoins(totalGiven.coins);

            if (totalGiven.gems != 0)
                CurrencyManager.Instance.AddGems(totalGiven.gems);

            if (totalGiven.heroXP != 0)
                CurrencyManager.Instance.AddHeroXP(totalGiven.heroXP);
        }
        else
        {
            Debug.LogWarning("[WinPanel] CurrencyManager.Instance is null – rewards not applied!");
        }
    }

    // Optional helper if you want to set base values from code (e.g. from a config)
    public void SetStageBaseValues(RewardValues rewardBase)
    {
        baseRewardStageValues = rewardBase;
    }

    // Optional: allow other scripts to set chapter dynamically
    public void SetChapterNumber(int chapter)
    {
        currentChapterNumber = Mathf.Max(1, chapter);
    }
}

public class WinPanel2 : MonoBehaviour
{
    [System.Serializable]
    public class RewardValues
    {
        public int coins;
        public int gems;
        public int heroXP;

        public void Add(RewardValues other)
        {
            if (other == null) return;
            coins += other.coins;
            gems += other.gems;
            heroXP += other.heroXP;
        }

        public static RewardValues FromScaled(RewardValues source, float factor)
        {
            if (source == null) return new RewardValues();
            return new RewardValues
            {
                coins = Mathf.RoundToInt(source.coins * factor),
                gems = Mathf.RoundToInt(source.gems * factor),
                heroXP = Mathf.RoundToInt(source.heroXP * factor)
            };
        }
    }

    [Header("Stage base values (Stage 1, before progression)")]
    [Tooltip("Base values used for the main rewards (no bonus row in this version).")]
    [SerializeField] private RewardValues baseRewardStageValues;

    [Header("Per-stage progression")]
    [Tooltip("Extra reward per stage, e.g. 0.05 = +5% per stage (Stage 2 = base * 1.05, Stage 3 = base * 1.10, etc.).")]
    [SerializeField] private float perStageBonusPercent = 0.05f;

    [Header("Tier multipliers (for HP tiers)")]
    [Tooltip("Tier1 = 1x, Tier2 = 1.2x, Tier3 = 1.5x by default.")]
    [SerializeField] private float tier1Multiplier = 1.0f;
    [SerializeField] private float tier2Multiplier = 1.2f;
    [SerializeField] private float tier3Multiplier = 1.5f;

    [Header("UI – Static texts (we no longer instantiate icons)")]
    [SerializeField] private TMP_Text coinsText;      // under the pre-placed coin icon
    [SerializeField] private TMP_Text gemsText;       // under the pre-placed gem icon
    [SerializeField] private TMP_Text heroXPText;     // under the pre-placed XP/hero icon

    [Header("UI – Level header (top of panel)")]
    [SerializeField] private TMP_Text chapterStageText;
    [Tooltip("Optional: current chapter number if you want to show 'Chapter X - Stage Y'.")]
    [SerializeField] private int currentChapterNumber = 1;

    [Header("Claim button")]
    [SerializeField] private Button claimButton;
    [SerializeField] private string targetSceneName = "MenuScene";

    [Header("Optional fade out")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Optional resource animation")]
    [SerializeField] private RectTransform coinSpawnPoint;  // UI point for coin fly animation

    // internal state
    private int hpCase; // 1, 2, or 3: 1 = <50%, 2 = 50–<100, 3 = 100%

    // Stage-scaled base
    private RewardValues stageScaledRewardBase;

    // HP-tiered rewards (R1/R2/R3)
    private RewardValues rewardTier1;
    private RewardValues rewardTier2;
    private RewardValues rewardTier3;

    // Sum of unlocked tiers (what we show & give)
    private RewardValues totalRewardRow;
    private RewardValues totalGiven;

    private bool rewardsClaimed;

    private void Awake()
    {
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimPressed);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the win panel and compute rewards based on HP% (0..1).
    /// </summary>
    public void Show(float hpPercent)
    {
        if (hpPercent < 0f) hpPercent = 0f;
        if (hpPercent > 1f) hpPercent = 1f;

        rewardsClaimed = false;
        if (claimButton) claimButton.interactable = true;

        hpCase = CalculateHpCase(hpPercent);

        CalculateAllRewards();
        UpdateRewardTexts();
        UpdateLevelHeaderText();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        gameObject.SetActive(true);

        // Optional: small coin animation when panel appears
        if (ResourcesAnimationManager.instance != null && coinSpawnPoint != null)
        {
            ResourcesAnimationManager.instance.AddCoinsFromUI(coinSpawnPoint, 100);
        }
    }

    // Decide which HP case we are in: 1, 2, or 3
    private int CalculateHpCase(float hpPercent)
    {
        if (hpPercent >= 1f - 0.0001f)
            return 3;          // full HP

        if (hpPercent >= 0.5f)
            return 2;          // 50% – <100%

        return 1;              // <50%
    }

    private void CalculateAllRewards()
    {
        // 1) Determine current stage (1-based) from HomeManager
        int stage1Based = HomeManager.CurrentStage1Based;
        if (stage1Based < 1) stage1Based = 1;

        // 2) Compute stage multiplier
        float stageMultiplier = 1f + perStageBonusPercent * (stage1Based - 1);

        // 3) Apply stage multiplier to base reward
        stageScaledRewardBase = RewardValues.FromScaled(baseRewardStageValues, stageMultiplier);

        // 4) Compute HP-tiered reward values
        rewardTier1 = RewardValues.FromScaled(stageScaledRewardBase, tier1Multiplier);
        rewardTier2 = RewardValues.FromScaled(stageScaledRewardBase, tier2Multiplier);
        rewardTier3 = RewardValues.FromScaled(stageScaledRewardBase, tier3Multiplier);

        // 5) Sum unlocked tiers into main reward row (no bonus row in this version)
        totalRewardRow = new RewardValues();

        if (hpCase >= 1) totalRewardRow.Add(rewardTier1);
        if (hpCase >= 2) totalRewardRow.Add(rewardTier2);
        if (hpCase >= 3) totalRewardRow.Add(rewardTier3);

        // 6) Total actually given = reward row
        totalGiven = new RewardValues
        {
            coins = totalRewardRow.coins,
            gems = totalRewardRow.gems,
            heroXP = totalRewardRow.heroXP
        };
    }

    private void UpdateRewardTexts()
    {
        if (coinsText != null)
            coinsText.text = totalRewardRow.coins.ToString();

        if (gemsText != null)
            gemsText.text = totalRewardRow.gems.ToString();

        if (heroXPText != null)
            heroXPText.text = totalRewardRow.heroXP.ToString();
    }

    private void UpdateLevelHeaderText()
    {
        if (chapterStageText == null)
            return;

        int stage1Based = HomeManager.CurrentStage1Based;
        if (stage1Based < 1) stage1Based = 1;

        // Example format: "Chapter 1 - Stage 3"
        chapterStageText.text = $"{stage1Based}";
    }

    private void OnClaimPressed()
    {
        if (rewardsClaimed) return;
        rewardsClaimed = true;

        if (claimButton)
            claimButton.interactable = false;

        StartCoroutine(ClaimRoutine());
    }

    private IEnumerator ClaimRoutine()
    {
        // 1) play resource fly animations (if you want more, expand PlayResourceFlyAnimations)
        yield return StartCoroutine(PlayResourceFlyAnimations());

        // 2) actually give rewards to the player (and save via CurrencyManager)
        ApplyRewardsToPlayer();

        // 3) fade out panel and then close
        if (canvasGroup != null && fadeDuration > 0f)
        {
            float t = 0f;
            float startAlpha = canvasGroup.alpha;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime; // unscaled, in case timescale is 0
                float k = Mathf.Clamp01(t / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);

        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
    }

    private IEnumerator PlayResourceFlyAnimations()
    {
        // For now just wait a bit so there is a feeling of "claim" before scene change.
        // You can hook your ResourcesAnimationManager here later if you want.
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private void ApplyRewardsToPlayer()
    {
        if (CurrencyManager.Instance != null)
        {
            if (totalGiven.coins != 0)
                CurrencyManager.Instance.AddCoins(totalGiven.coins);

            if (totalGiven.gems != 0)
                CurrencyManager.Instance.AddGems(totalGiven.gems);

            if (totalGiven.heroXP != 0)
                CurrencyManager.Instance.AddHeroXP(totalGiven.heroXP);
        }
        else
        {
            Debug.LogWarning("[WinPanel] CurrencyManager.Instance is null – rewards not applied!");
        }
    }

    // Optional helper if you want to set base values from code (e.g. from a config)
    public void SetStageBaseValues(RewardValues rewardBase)
    {
        baseRewardStageValues = rewardBase;
    }

    // Optional: allow other scripts to set chapter dynamically
    public void SetChapterNumber(int chapter)
    {
        currentChapterNumber = Mathf.Max(1, chapter);
    }
}


public class WinPanel1 : MonoBehaviour
{
    [System.Serializable]
    public class RewardValues
    {
        public int coins;
        public int gems;
        public int heroXP;

        // Add another RewardValues into this one
        public void Add(RewardValues other)
        {
            if (other == null) return;

            coins += other.coins;
            gems += other.gems;
            heroXP += other.heroXP;
        }

        // Create a new RewardValues scaled from a base by factor
        public static RewardValues FromScaled(RewardValues source, float factor)
        {
            if (source == null) return new RewardValues();
            return new RewardValues
            {
                coins = Mathf.RoundToInt(source.coins * factor),
                gems = Mathf.RoundToInt(source.gems * factor),
                heroXP = Mathf.RoundToInt(source.heroXP * factor)
            };
        }
    }

    [Header("Stage base values (Stage 1, before progression)")]
    [Tooltip("Base values used for the Rewards row (normal rewards).")]
    [SerializeField] private RewardValues baseRewardStageValues;

    [Tooltip("Base values used for the Bonus row (bonus rewards).")]
    [SerializeField] private RewardValues baseBonusStageValues;

    [Header("Per-stage progression")]
    [Tooltip("Extra reward per stage, e.g. 0.05 = +5% per stage (Stage 2 = base * 1.05, Stage 3 = base * 1.10, etc.).")]
    [SerializeField] private float perStageBonusPercent = 0.05f;

    [Header("Tier multipliers (for BOTH rewards and bonus)")]
    [Tooltip("Tier1 = 1x, Tier2 = 1.2x, Tier3 = 1.5x by default.")]
    [SerializeField] private float tier1Multiplier = 1.0f;
    [SerializeField] private float tier2Multiplier = 1.2f;
    [SerializeField] private float tier3Multiplier = 1.5f;

    [Header("UI – Layout containers")]
    [Tooltip("Parent with HorizontalLayoutGroup for the base Rewards row.")]
    [SerializeField] private Transform rewardRowContainer;
    [Tooltip("Parent with HorizontalLayoutGroup for the Bonus row.")]
    [SerializeField] private Transform bonusRowContainer;

    [Header("UI – Prefab")]
    [Tooltip("Prefab that has icon + text and RewardItemUI component.")]
    [SerializeField] private RewardItemUI rewardItemPrefab;

    [Header("Icons")]
    [SerializeField] private Sprite coinIcon;
    [SerializeField] private Sprite gemIcon;
    [SerializeField] private Sprite heroXpIcon;

    [Header("Claim button")]
    [SerializeField] private Button claimButton;
    [SerializeField] private string targetSceneName = "MenuScene";   // exact name in Build Settings


    [Header("Optional fade out")]
    [SerializeField] private CanvasGroup canvasGroup;   // assign if you want fade
    [SerializeField] private float fadeDuration = 0.35f;

    // internal state
    private int hpCase; // 1, 2, or 3: 1 = <50%, 2 = 50–<100, 3 = 100%

    // Stage-scaled bases (after stage progression, before tiers)
    private RewardValues stageScaledRewardBase;
    private RewardValues stageScaledBonusBase;

    // Normal reward tiers (R1/R2/R3)
    private RewardValues rewardTier1;
    private RewardValues rewardTier2;
    private RewardValues rewardTier3;

    // Bonus tiers (B1/B2/B3)
    private RewardValues bonusTier1;
    private RewardValues bonusTier2;
    private RewardValues bonusTier3;

    // Sums per row (what we display)
    private RewardValues totalRewardRow;
    private RewardValues totalBonusRow;

    // Total actually given to the player
    private RewardValues totalGiven;

    private bool rewardsClaimed;

    [SerializeField] private RectTransform coinSpawnPoint;  // drag a child of WinPanel here


    private void Awake()
    {
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimPressed);

        gameObject.SetActive(false);
    }

    /// <summary>
    /// Show the win panel and compute rewards based on HP% (0..1).
    /// </summary>
    public void Show(float hpPercent)
    {
        if (hpPercent < 0f) hpPercent = 0f;
        if (hpPercent > 1f) hpPercent = 1f;

        rewardsClaimed = false;
        if (claimButton) claimButton.interactable = true;

        hpCase = CalculateHpCase(hpPercent);
        CalculateAllRewards();
        BuildRewardRow();
        BuildBonusRow();

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        gameObject.SetActive(true);

        if (ResourcesAnimationManager.instance != null && coinSpawnPoint != null)
        {
            ResourcesAnimationManager.instance.AddCoinsFromUI(coinSpawnPoint, 100);
        }
    }

    // Decide which case we are in: 1, 2, or 3
    private int CalculateHpCase(float hpPercent)
    {
        // Case 3: full HP
        if (hpPercent >= 1f - 0.0001f)
            return 3;

        // Case 2: 50% – <100%
        if (hpPercent >= 0.5f)
            return 2;

        // Case 1: <50%
        return 1;
    }

    private void CalculateAllRewards()
    {
        // 1) Determine current stage (1-based) from HomeManager
        int stage1Based = HomeManager.CurrentStage1Based;
        if (stage1Based < 1) stage1Based = 1;

        // 2) Compute stage multiplier
        // Stage 1 -> 1 + p * 0
        // Stage 2 -> 1 + p * 1
        // Stage 3 -> 1 + p * 2, etc.
        float stageMultiplier = 1f + perStageBonusPercent * (stage1Based - 1);

        // 3) Apply stage multiplier to each base set (rewards + bonus)
        stageScaledRewardBase = RewardValues.FromScaled(baseRewardStageValues, stageMultiplier);
        stageScaledBonusBase = RewardValues.FromScaled(baseBonusStageValues, stageMultiplier);

        // 4) Compute TIER values for rewards from stageScaledRewardBase
        rewardTier1 = RewardValues.FromScaled(stageScaledRewardBase, tier1Multiplier);
        rewardTier2 = RewardValues.FromScaled(stageScaledRewardBase, tier2Multiplier);
        rewardTier3 = RewardValues.FromScaled(stageScaledRewardBase, tier3Multiplier);

        // 5) Compute TIER values for bonus from stageScaledBonusBase
        bonusTier1 = RewardValues.FromScaled(stageScaledBonusBase, tier1Multiplier);
        bonusTier2 = RewardValues.FromScaled(stageScaledBonusBase, tier2Multiplier);
        bonusTier3 = RewardValues.FromScaled(stageScaledBonusBase, tier3Multiplier);

        // 6) Sum unlocked tiers into each row based on hpCase

        totalRewardRow = new RewardValues();
        totalBonusRow = new RewardValues();

        // Normal rewards row (R1/R2/R3)
        if (hpCase >= 1) totalRewardRow.Add(rewardTier1);
        if (hpCase >= 2) totalRewardRow.Add(rewardTier2);
        if (hpCase >= 3) totalRewardRow.Add(rewardTier3);

        // Bonus row (B1/B2/B3)
        if (hpCase >= 1) totalBonusRow.Add(bonusTier1);
        if (hpCase >= 2) totalBonusRow.Add(bonusTier2);
        if (hpCase >= 3) totalBonusRow.Add(bonusTier3);

        // 7) Total actually given to the player = reward row + bonus row
        totalGiven = new RewardValues
        {
            coins = totalRewardRow.coins + totalBonusRow.coins,
            gems = totalRewardRow.gems + totalBonusRow.gems,
            heroXP = totalRewardRow.heroXP + totalBonusRow.heroXP
        };
    }

    private void BuildRewardRow()
    {
        ClearContainer(rewardRowContainer);

        if (!rewardItemPrefab || !rewardRowContainer) return;

        // Reward row — instantiate one item per non-zero resource (sum of all unlocked reward tiers)
        if (totalRewardRow.coins > 0)
            CreateRewardItem(rewardRowContainer, coinIcon, totalRewardRow.coins);

        if (totalRewardRow.gems > 0)
            CreateRewardItem(rewardRowContainer, gemIcon, totalRewardRow.gems);

        if (totalRewardRow.heroXP > 0)
            CreateRewardItem(rewardRowContainer, heroXpIcon, totalRewardRow.heroXP);
    }

    private void BuildBonusRow()
    {
        ClearContainer(bonusRowContainer);

        if (!rewardItemPrefab || !bonusRowContainer) return;

        // Bonus row — instantiate one item per non-zero resource (sum of all unlocked bonus tiers)
        if (totalBonusRow.coins > 0)
            CreateRewardItem(bonusRowContainer, coinIcon, totalBonusRow.coins);

        if (totalBonusRow.gems > 0)
            CreateRewardItem(bonusRowContainer, gemIcon, totalBonusRow.gems);

        if (totalBonusRow.heroXP > 0)
            CreateRewardItem(bonusRowContainer, heroXpIcon, totalBonusRow.heroXP);
    }

    private void CreateRewardItem(Transform parent, Sprite icon, int amount)
    {
        var instance = Instantiate(rewardItemPrefab, parent);
        instance.Setup(icon, amount);
    }

    private void ClearContainer(Transform container)
    {
        if (!container) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            Destroy(container.GetChild(i).gameObject);
        }
    }

    private void OnClaimPressed()
    {
        if (rewardsClaimed) return;
        rewardsClaimed = true;

        if (claimButton)
            claimButton.interactable = false;

        StartCoroutine(ClaimRoutine());
    }

    private IEnumerator ClaimRoutine()
    {
        // 1) play fly animations (coins/gems/xp from panel icons to HUD)
        yield return StartCoroutine(PlayResourceFlyAnimations());

        // 2) actually give rewards to the player (and save via CurrencyManager)
        ApplyRewardsToPlayer();

        // 3) fade out panel and then close
        if (canvasGroup != null && fadeDuration > 0f)
        {
            float t = 0f;
            float startAlpha = canvasGroup.alpha;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime; // unscaled because timescale may be 0
                float k = Mathf.Clamp01(t / fadeDuration);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, k);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        gameObject.SetActive(false);

        SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);


        // TODO: notify your main menu / home UI:
        // GameUIManager.Instance.GoToMainMenu();
    }

    private IEnumerator PlayResourceFlyAnimations()
    {
        // Implement DOTween animations here if you want:
        //   - spawn icons at each instantiated reward item
        //   - move them towards HUD resource positions
        // For now we just wait a bit so the flow works.
        yield return new WaitForSecondsRealtime(0.8f);
    }

    private void ApplyRewardsToPlayer()
    {
        if (CurrencyManager.Instance != null)
        {
            if (totalGiven.coins != 0)
                CurrencyManager.Instance.AddCoins(totalGiven.coins);
                //ResourcesAnimationManager.instance.AddCoins(rewardRowContainer.position , 100);

            if (totalGiven.gems != 0)
                CurrencyManager.Instance.AddGems(totalGiven.gems);
            //ResourcesAnimationManager.instance.AddGems(rewardRowContainer.position, 100);
            //ResourcesAnimationManager.instance.AddCoins(new Vector2(rewardRowContainer.position.x - 10, rewardRowContainer.position.y), 100);



            if (totalGiven.heroXP != 0)
                CurrencyManager.Instance.AddHeroXP(totalGiven.heroXP);

            // CurrencyManager internally calls:
            // SaveSystem.SetCoins(int)
            // SaveSystem.SetGems(int)
            // SaveSystem.SetHeroXP(int)
        }
        else
        {
            Debug.LogWarning("[WinPanel] CurrencyManager.Instance is null – rewards not applied!");
        }
    }

    // Optional helper if you want to set base values from code
    public void SetStageBaseValues(RewardValues rewardBase, RewardValues bonusBase)
    {
        baseRewardStageValues = rewardBase;
        baseBonusStageValues = bonusBase;
    }
}




