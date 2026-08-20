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
