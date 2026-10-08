// AdBannerSlot.cs
using UnityEngine;

/// <summary>
/// Reserves the strip of screen the AdMob banner sits in, and asks AdManager to
/// show the banner while this scene is active.
///
/// WHY THIS EXISTS: the AdMob banner is a NATIVE OVERLAY. The SDK draws it on
/// top of the Unity view - it is not a Unity UI element and it does not push
/// anything out of the way. Without a reserved strip underneath it, the banner
/// simply covers whatever UI happens to be at the bottom of the screen.
///
/// Put this on the "Ads Banner panel" RectTransform. Nothing shows in the Editor
/// Game view (the native overlay does not exist there) - the placeholder image
/// stands in for it. On device the real banner lands exactly over this strip.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class AdBannerSlot : MonoBehaviour
{
    [Header("Behaviour")]
    [Tooltip("Ask AdManager to show the banner when this object is enabled.")]
    [SerializeField] private bool showBannerOnEnable = true;

    [Tooltip("Hide the banner again when this object is disabled / the scene unloads.")]
    [SerializeField] private bool hideBannerOnDisable = true;

    [Header("Reserved Height")]
    [Tooltip("Resize this RectTransform's height to match the REAL banner height " +
             "once it loads. Off = keep the height you authored by hand.")]
    [SerializeField] private bool matchRealBannerHeight = true;

    [Tooltip("Height used before a banner loads, in canvas units. Leave at 0 to " +
             "keep whatever height you authored in the Inspector.")]
    [SerializeField, Min(0f)] private float fallbackHeight = 0f;

    [Header("Placeholder")]
    [Tooltip("Optional editor/no-fill placeholder graphic. Hidden once a real " +
             "banner is confirmed on screen.")]
    [SerializeField] private GameObject placeholderVisual;

#if UNITY_EDITOR
    [Header("Editor")]
    [Tooltip("EDITOR ONLY. The Google Ads plugin fakes the banner in the Editor with a " +
             "test prefab whose first appearance froze the game ~7-11 s. Off = no banner " +
             "is requested in the Editor; builds always request it.")]
    [SerializeField] private bool showTestBannerInEditor = false;
#endif

    private RectTransform rect;
    private Canvas canvas;
    private float authoredHeight;

    /// <summary>The height to fall back to: the serialized value, else whatever was authored.</summary>
    private float FallbackHeight => fallbackHeight > 0f ? fallbackHeight : authoredHeight;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        // Read the real, resolved height - rect.height is correct whatever the
        // anchors are, unlike sizeDelta.y.
        authoredHeight = rect.rect.height;
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!showTestBannerInEditor) return;   // the strip stays reserved, no banner requested
#endif
        var ads = AdManager.Instance;
        if (ads == null)
        {
            // AdManager is a DontDestroyOnLoad singleton that boots from the
            // menu scene. Playing a gameplay scene directly means there is none.
            Debug.LogWarning("[AdBannerSlot] No AdManager in the scene - the strip is " +
                             "reserved but no banner will be requested.", this);
            return;
        }

        ads.OnBannerLoaded += HandleBannerLoaded;
        ads.OnBannerFailed += HandleBannerFailed;

        if (showBannerOnEnable) ads.ShowBanner();

        // A banner may already be up from a previous scene.
        if (ads.IsBannerVisible) HandleBannerLoaded(ads.BannerHeightPixels);
    }

    private void OnDisable()
    {
        var ads = AdManager.Instance;
        if (ads == null) return;

        ads.OnBannerLoaded -= HandleBannerLoaded;
        ads.OnBannerFailed -= HandleBannerFailed;

        if (hideBannerOnDisable) ads.HideBanner();
    }

    private void HandleBannerLoaded(float heightPixels)
    {
        if (placeholderVisual) placeholderVisual.SetActive(false);

        // !! The strip's own grey Image is drawn ON TOP of the HUD (it sits late in
        // the canvas order). Once the real native banner is up it has nothing left
        // to show - and after a resize it painted over the boosters, BATTLE and the
        // hero cards (stage 10 on device). Hide it while a real banner is showing.
        SetStripVisible(false);

        if (matchRealBannerHeight && heightPixels > 0f)
            ApplyHeight(PixelsToCanvasUnits(heightPixels));
    }

    private void HandleBannerFailed()
    {
        // No fill / no network: keep the placeholder and the authored height so
        // the layout does not jump around.
        if (placeholderVisual) placeholderVisual.SetActive(true);
        SetStripVisible(true);
        ApplyHeight(FallbackHeight);
    }

    private void SetStripVisible(bool visible)
    {
        foreach (var g in GetComponents<UnityEngine.UI.Graphic>())
            g.enabled = visible;
    }

    private void ApplyHeight(float canvasUnits)
    {
        if (!rect || canvasUnits <= 0f) return;

        // Keep the BOTTOM edge where it was authored. The rect's pivot is centred,
        // so a plain resize grew it half upward - straight into the HUD above it.
        float bottomBefore = BottomEdge();

        // SetSizeWithCurrentAnchors, NOT sizeDelta. This panel is stretched
        // (anchorMin 0,0 -> anchorMax 1,1), and on a stretched rect sizeDelta.y
        // is an OFFSET FROM THE PARENT'S HEIGHT, not an absolute height -
        // assigning a height straight into it would blow the layout apart.
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, canvasUnits);

        var p = rect.anchoredPosition;
        p.y += bottomBefore - BottomEdge();
        rect.anchoredPosition = p;
    }

    /// <summary>The rect's bottom edge in its parent's space.</summary>
    private float BottomEdge() => rect.localPosition.y + rect.rect.yMin;

    /// <summary>
    /// Screen pixels -> canvas units. With a CanvasScaler the canvas is scaled,
    /// so a raw pixel height would be the wrong size in UI space.
    /// </summary>
    private float PixelsToCanvasUnits(float pixels)
    {
        float scale = (canvas != null && canvas.scaleFactor > 0f) ? canvas.scaleFactor : 1f;
        return pixels / scale;
    }
}
