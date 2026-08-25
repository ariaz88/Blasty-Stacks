using UnityEngine;

/// <summary>
/// The tutorial's screen furniture: one Screen-Space-Overlay canvas holding the
/// pointing hand, the caption, an (unused for now) dimmer and an (unused for
/// now) full-screen input blocker.
///
/// Drop the TutorialOverlay prefab into any scene that needs a tutorial. It is
/// the ONLY scene-side dependency of the whole system - TutorialManager finds it
/// with FindInScene when a trigger does not name one.
///
/// The dimmer and blocker exist but are switched off: the first tutorial matches
/// the reference video, which keeps the board fully lit and fully playable. They
/// are here so a later "spotlight this button" tutorial needs no new prefab.
/// </summary>
[DisallowMultipleComponent]
public class TutorialOverlay : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TutorialHand hand;
    [SerializeField] private TutorialCaption caption;

    [Tooltip("Full-screen dark image. Off by default.")]
    [SerializeField] private CanvasGroup dimmer;

    [Tooltip("Full-screen transparent raycast target that swallows input. Off by default.")]
    [SerializeField] private GameObject blocker;

    public Canvas Canvas => canvas;
    public TutorialHand Hand => hand;
    public TutorialCaption Caption => caption;

    private void Awake()
    {
        if (!canvas) canvas = GetComponentInChildren<Canvas>(true);

        // A prefab cannot hold a reference to a scene camera, so a Screen-Space-
        // Camera canvas arrives with an empty worldCamera and would render
        // nothing. Claim Camera.main here - BEFORE Configure, which caches it.
        if (canvas && canvas.renderMode == RenderMode.ScreenSpaceCamera && !canvas.worldCamera)
        {
            canvas.worldCamera = Camera.main;
            if (!canvas.worldCamera)
                Debug.LogWarning("[Tutorial] No Camera.main for the overlay canvas - the tutorial will not be visible.", this);
        }

        if (hand) hand.Configure(canvas);

        SetDim(0f);
        SetBlockInput(false);
    }

    /// <summary>Fade level of the full-screen dim, 0 = fully lit board.</summary>
    public void SetDim(float alpha)
    {
        if (!dimmer) return;

        dimmer.alpha = Mathf.Clamp01(alpha);
        dimmer.gameObject.SetActive(dimmer.alpha > 0.001f);
    }

    /// <summary>Turns the full-screen input blocker on or off.</summary>
    public void SetBlockInput(bool block)
    {
        if (blocker) blocker.SetActive(block);
    }

    /// <summary>Puts the overlay back to "nothing showing".</summary>
    public void ClearAll()
    {
        if (hand) hand.StopAndHide();
        if (caption) caption.Hide();
        SetDim(0f);
        SetBlockInput(false);
    }

    /// <summary>The overlay in the currently loaded scenes, or null.</summary>
    public static TutorialOverlay FindInScene()
    {
        return FindObjectOfType<TutorialOverlay>(true);
    }
}
