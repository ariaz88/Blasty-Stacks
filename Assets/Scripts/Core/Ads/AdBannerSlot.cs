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

        if (matchRealBannerHeight && heightPixels > 0f)
            ApplyHeight(PixelsToCanvasUnits(heightPixels));
    }

    private void HandleBannerFailed()
    {
        // No fill / no network: keep the placeholder and the authored height so
        // the layout does not jump around.
        if (placeholderVisual) placeholderVisual.SetActive(true);
        ApplyHeight(FallbackHeight);
    }

    private void ApplyHeight(float canvasUnits)
    {
        if (!rect || canvasUnits <= 0f) return;

        // SetSizeWithCurrentAnchors, NOT sizeDelta. This panel is stretched
        // (anchorMin 0,0 -> anchorMax 1,1), and on a stretched rect sizeDelta.y
        // is an OFFSET FROM THE PARENT'S HEIGHT, not an absolute height -
        // assigning a height straight into it would blow the layout apart.
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, canvasUnits);
    }

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
