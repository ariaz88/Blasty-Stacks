using System;
using System.Collections;
using UnityEngine;
#if GOOGLE_MOBILE_ADS
using GoogleMobileAds.Api;
#endif

/// <summary>
/// Single entry point for all AdMob traffic (banner / interstitial / rewarded).
/// Gameplay code never touches GoogleMobileAds types directly — it calls this facade.
/// </summary>
// The GoogleMobileAds SDK is not in Packages/manifest.json yet. Everything that touches it
// sits behind GOOGLE_MOBILE_ADS; without the define this class still compiles and simply
// reports "no ad available" so callers keep working.
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    [Serializable]
    public class AdUnitIds
    {
        public string banner;
        public string interstitial;
        public string rewarded;
    }

    [Header("Ad Units")]
    [Tooltip("Ignore the serialized IDs below and use Google's official test units.")]
    [SerializeField] private bool useTestIds = true;
    [SerializeField] private AdUnitIds androidIds = new AdUnitIds();
    [SerializeField] private AdUnitIds iosIds = new AdUnitIds();

    [Header("Lifecycle")]
    [SerializeField] private bool initializeOnAwake = true;
    [SerializeField] private bool preloadAfterInitialize = true;

    [Header("Retry")]
    [Min(0f)] [SerializeField] private float retryDelaySeconds = 5f;
    [Min(0)] [SerializeField] private int maxLoadRetries = 3;

    /// <summary>Where the native banner overlay is pinned on screen.</summary>
    public enum BannerAnchor { Bottom, Top }

    /// <summary>
    /// Standard = fixed 320x50. Adaptive = anchored adaptive banner, sized to the
    /// device width - this is what Google recommends and what makes the banner
    /// fill the reserved strip properly on tall phones.
    /// </summary>
    public enum BannerSizeMode { AnchoredAdaptive, Standard }

    [Header("Banner")]
    [SerializeField] private bool showBannerAfterInitialize = false;
    [SerializeField] private BannerSizeMode bannerSizeMode = BannerSizeMode.AnchoredAdaptive;
    [SerializeField] private BannerAnchor bannerAnchor = BannerAnchor.Bottom;

    // Google's official test units — safe in development, never ship with these.
    private static readonly AdUnitIds TestAndroid = new AdUnitIds
    {
        banner = "ca-app-pub-3940256099942544/6300978111",
        interstitial = "ca-app-pub-3940256099942544/1033173712",
        rewarded = "ca-app-pub-3940256099942544/5224354917",
    };

    private static readonly AdUnitIds TestIos = new AdUnitIds
    {
        banner = "ca-app-pub-3940256099942544/2934735716",
        interstitial = "ca-app-pub-3940256099942544/4411468910",
        rewarded = "ca-app-pub-3940256099942544/1712485313",
    };

    public event Action OnInitialized;
    public event Action<bool> OnRewardedAvailabilityChanged;
    public event Action OnAdOpened;
    public event Action OnAdClosed;

    public bool IsInitialized { get; private set; }
    public bool IsRewardedAdReady => rewardedAd != null;
    public bool IsInterstitialReady => interstitialAd != null;

#if GOOGLE_MOBILE_ADS
    private RewardedAd rewardedAd;
    private InterstitialAd interstitialAd;
    private BannerView bannerView;
#else
    private object rewardedAd;
    private object interstitialAd;
#endif

    private int rewardedRetries;
    private int interstitialRetries;

    private AdUnitIds ActiveIds
    {
        get
        {
#if UNITY_IOS
            return useTestIds ? TestIos : iosIds;
#else
            return useTestIds ? TestAndroid : androidIds;
#endif
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (initializeOnAwake) Initialize();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        DestroyBanner();
    }

    #region Initialization

    public void Initialize()
    {
        if (IsInitialized) return;

#if GOOGLE_MOBILE_ADS
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(_ => HandleInitialized());
#else
        Debug.LogWarning("[AdManager] GoogleMobileAds SDK not installed — running in no-ad mode.", this);
        HandleInitialized();
#endif
    }

    private void HandleInitialized()
    {
        IsInitialized = true;
        OnInitialized?.Invoke();

        if (preloadAfterInitialize)
        {
            LoadRewardedAd();
            LoadInterstitial();
        }
        if (showBannerAfterInitialize) ShowBanner();
    }

    #endregion

    #region Rewarded

    public void LoadRewardedAd()
    {
        if (!IsInitialized || IsRewardedAdReady) return;

#if GOOGLE_MOBILE_ADS
        RewardedAd.Load(ActiveIds.rewarded, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdManager] Rewarded load failed: {error}", this);
                ScheduleRetry(ref rewardedRetries, LoadRewardedAd);
                return;
            }

            rewardedRetries = 0;
            rewardedAd = ad;
            HookRewardedEvents(ad);
            OnRewardedAvailabilityChanged?.Invoke(true);
        });
#endif
    }

    /// <summary>
    /// Shows a rewarded ad. <paramref name="onRewardEarned"/> fires only when the user
    /// actually earns the reward; grant nothing before that callback.
    /// </summary>
    public void ShowRewardedAd(Action onRewardEarned, Action onAdUnavailable = null, Action onAdClosed = null)
    {
        if (!IsRewardedAdReady)
        {
            LoadRewardedAd();
            onAdUnavailable?.Invoke();
            return;
        }

#if GOOGLE_MOBILE_ADS
        pendingReward = onRewardEarned;
        pendingRewardedClosed = onAdClosed;
        rewardedAd.Show(_ =>
        {
            var reward = pendingReward;
            pendingReward = null;
            reward?.Invoke();
        });
#else
        onAdUnavailable?.Invoke();
#endif
    }

#if GOOGLE_MOBILE_ADS
    private Action pendingReward;
    private Action pendingRewardedClosed;

    private void HookRewardedEvents(RewardedAd ad)
    {
        ad.OnAdFullScreenContentOpened += () => OnAdOpened?.Invoke();
        ad.OnAdFullScreenContentClosed += () =>
        {
            ClearRewarded();
            var closed = pendingRewardedClosed;
            pendingRewardedClosed = null;
            closed?.Invoke();
            OnAdClosed?.Invoke();
            LoadRewardedAd();
        };
        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"[AdManager] Rewarded show failed: {error}", this);
            ClearRewarded();
            pendingReward = null;
            pendingRewardedClosed = null;
            LoadRewardedAd();
        };
    }

    private void ClearRewarded()
    {
        rewardedAd?.Destroy();
        rewardedAd = null;
        OnRewardedAvailabilityChanged?.Invoke(false);
    }
#endif

    #endregion

    #region Interstitial

    public void LoadInterstitial()
    {
        if (!IsInitialized || IsInterstitialReady) return;

#if GOOGLE_MOBILE_ADS
        InterstitialAd.Load(ActiveIds.interstitial, new AdRequest(), (ad, error) =>
        {
            if (error != null || ad == null)
            {
                Debug.LogWarning($"[AdManager] Interstitial load failed: {error}", this);
                ScheduleRetry(ref interstitialRetries, LoadInterstitial);
                return;
            }

            interstitialRetries = 0;
            interstitialAd = ad;
            HookInterstitialEvents(ad);
        });
#endif
    }

    public void ShowInterstitial(Action onClosed = null)
    {
        if (!IsInterstitialReady)
        {
            LoadInterstitial();
            onClosed?.Invoke();
            return;
        }

#if GOOGLE_MOBILE_ADS
        pendingInterstitialClosed = onClosed;
        interstitialAd.Show();
#else
        onClosed?.Invoke();
#endif
    }

#if GOOGLE_MOBILE_ADS
    private Action pendingInterstitialClosed;

    private void HookInterstitialEvents(InterstitialAd ad)
    {
        ad.OnAdFullScreenContentOpened += () => OnAdOpened?.Invoke();
        ad.OnAdFullScreenContentClosed += () =>
        {
            ClearInterstitial();
            var closed = pendingInterstitialClosed;
            pendingInterstitialClosed = null;
            closed?.Invoke();
            OnAdClosed?.Invoke();
            LoadInterstitial();
        };
        ad.OnAdFullScreenContentFailed += error =>
        {
            Debug.LogWarning($"[AdManager] Interstitial show failed: {error}", this);
            ClearInterstitial();
            var closed = pendingInterstitialClosed;
            pendingInterstitialClosed = null;
            closed?.Invoke();
            LoadInterstitial();
        };
    }

    private void ClearInterstitial()
    {
        interstitialAd?.Destroy();
        interstitialAd = null;
    }
#endif

    #endregion

    #region Banner

    /// <summary>True once a banner has actually loaded and is on screen.</summary>
    public bool IsBannerVisible { get; private set; }

    /// <summary>
    /// Height of the loaded banner in SCREEN PIXELS, or 0 when none is loaded.
    /// Use this to size the reserved UI strip so the native overlay never covers
    /// gameplay UI. Only meaningful after OnBannerLoaded.
    /// </summary>
    public float BannerHeightPixels { get; private set; }

    /// <summary>Raised when a banner loads. Arg = its height in screen pixels.</summary>
    public event Action<float> OnBannerLoaded;

    /// <summary>Raised when a banner fails to load, after retries are exhausted.</summary>
    public event Action OnBannerFailed;

    private int bannerRetries;

    /// <summary>
    /// Creates (if needed) and shows the bottom banner. The banner is a NATIVE
    /// OVERLAY drawn by the SDK on top of the Unity view - it is not a Unity UI
    /// element, which is why nothing appears in the Game view in the editor
    /// unless the SDK's placeholder is active.
    /// </summary>
    public void ShowBanner()
    {
#if GOOGLE_MOBILE_ADS
        if (bannerView == null)
        {
            bannerView = new BannerView(ActiveIds.banner, ResolveAdSize(), ResolveAdPosition());
            HookBannerEvents(bannerView);
            bannerView.LoadAd(new AdRequest());
        }

        bannerView.Show();
#else
        Debug.LogWarning("[AdManager] ShowBanner ignored - GOOGLE_MOBILE_ADS is not defined.", this);
#endif
    }

    public void HideBanner()
    {
#if GOOGLE_MOBILE_ADS
        bannerView?.Hide();
#endif
        IsBannerVisible = false;
    }

    public void DestroyBanner()
    {
#if GOOGLE_MOBILE_ADS
        bannerView?.Destroy();
        bannerView = null;
#endif
        IsBannerVisible = false;
        BannerHeightPixels = 0f;
    }

#if GOOGLE_MOBILE_ADS
    private AdSize ResolveAdSize()
    {
        if (bannerSizeMode == BannerSizeMode.Standard) return AdSize.Banner;

        // Anchored adaptive: the SDK picks the right height for this device
        // width and orientation.
        return AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
            AdSize.FullWidth);
    }

    private AdPosition ResolveAdPosition()
    {
        return bannerAnchor == BannerAnchor.Top ? AdPosition.Top : AdPosition.Bottom;
    }

    private void HookBannerEvents(BannerView view)
    {
        view.OnBannerAdLoaded += () =>
        {
            bannerRetries = 0;
            IsBannerVisible = true;
            BannerHeightPixels = view.GetHeightInPixels();

            Debug.Log($"[AdManager] Banner loaded ({view.GetWidthInPixels()}x{BannerHeightPixels} px).", this);
            OnBannerLoaded?.Invoke(BannerHeightPixels);
        };

        view.OnBannerAdLoadFailed += error =>
        {
            Debug.LogWarning($"[AdManager] Banner load failed: {error}", this);
            IsBannerVisible = false;

            if (bannerRetries >= maxLoadRetries)
            {
                OnBannerFailed?.Invoke();
                return;
            }

            bannerRetries++;
            StartCoroutine(RetryAfterDelay(ReloadBanner));
        };
    }

    /// <summary>Rebuilds the banner from scratch - the SDK cannot re-load a failed view.</summary>
    private void ReloadBanner()
    {
        DestroyBanner();
        ShowBanner();
    }
#endif

    #endregion

    private void ScheduleRetry(ref int retryCount, Action loader)
    {
        if (retryCount >= maxLoadRetries) return;
        retryCount++;
        StartCoroutine(RetryAfterDelay(loader));
    }

    private IEnumerator RetryAfterDelay(Action loader)
    {
        yield return new WaitForSecondsRealtime(retryDelaySeconds);
        loader?.Invoke();
    }
}
