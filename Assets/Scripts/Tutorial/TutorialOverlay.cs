using UnityEngine;

/// <summary>
/// The tutorial's screen furniture: one canvas holding the pointing hand, the
/// fixed caption, the anchored tooltip, a dimmer and the focus gate.
///
/// Drop the TutorialOverlay prefab into any scene that needs a tutorial - or let
/// TutorialAutoAnchors spawn it. It is the ONLY scene-side dependency of the whole
/// system; TutorialManager finds it with FindInScene when a trigger does not name
/// one.
///
/// The focus gate and the tooltip are BUILT AT RUNTIME when the prefab does not
/// carry them, so the guided-chain feature works on the existing prefab without
/// anyone having to re-author it. Anything wired in the Inspector wins.
/// </summary>
[DisallowMultipleComponent]
public class TutorialOverlay : MonoBehaviour
{
    [Header("Parts")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private TutorialHand hand;
    [SerializeField] private TutorialCaption caption;

    [Tooltip("Bubble that follows a target. Built at runtime when empty.")]
    [SerializeField] private TutorialTooltip tooltip;

    [Tooltip("One-target input gate. Built at runtime when empty.")]
    [SerializeField] private TutorialFocusGate focusGate;

    [Tooltip("Target glow + tap burst. Built at runtime when empty.")]
    [SerializeField] private TutorialFocusFx focusFx;

    [Tooltip("Full-screen dark image. Off by default.")]
    [SerializeField] private CanvasGroup dimmer;

    [Tooltip("Legacy full-screen blocker. Superseded by focusGate - see SetBlockInput.")]
    [SerializeField] private GameObject blocker;

    [Header("Sorting")]
    [Tooltip("Sorting order forced on the canvas at runtime. It has to clear every " +
             "other canvas in the game: MenuScene's TopCanvas is 99, combat health " +
             "bars are 500 and the roguelite card panel is 1000. Negative = leave " +
             "whatever the prefab says.")]
    [SerializeField] private int forceSortingOrder = 1100;

    public Canvas Canvas => canvas;
    public TutorialHand Hand => hand;
    public TutorialCaption Caption => caption;
    public TutorialTooltip Tooltip => tooltip;
    public TutorialFocusGate Focus => focusGate;
    public TutorialFocusFx Fx => focusFx;

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

        if (canvas && forceSortingOrder >= 0) canvas.sortingOrder = forceSortingOrder;

        EnsureParts();

        if (hand) hand.Configure(canvas);
        if (tooltip) tooltip.Configure(canvas);
        if (focusGate) focusGate.Configure(canvas);

        SetDim(0f);
        SetBlockInput(false);
    }

    /// <summary>
    /// Builds the parts the shipped prefab predates. Order matters: the gate is
    /// created FIRST so it ends up as an earlier sibling and therefore draws
    /// underneath the caption, the tooltip and the hand - a scrim over the hand
    /// would hide the very thing it is pointing with.
    /// </summary>
    private void EnsureParts()
    {
        if (!focusGate)
        {
            focusGate = GetComponentInChildren<TutorialFocusGate>(true);
            if (!focusGate)
            {
                var go = new GameObject("FocusGate", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(canvas ? canvas.transform : transform, false);
                Stretch(rt);
                rt.SetAsFirstSibling();
                focusGate = go.AddComponent<TutorialFocusGate>();
            }
        }

        if (!focusFx)
        {
            focusFx = GetComponentInChildren<TutorialFocusFx>(true);
            if (!focusFx)
            {
                var go = new GameObject("FocusFx", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(canvas ? canvas.transform : transform, false);
                Stretch(rt);

                // Directly ABOVE the dim plates: the halo spills onto the darkened
                // area and must not be darkened itself, but it stays under the
                // caption, the tooltip and the hand.
                if (focusGate) rt.SetSiblingIndex(focusGate.transform.GetSiblingIndex() + 1);

                focusFx = go.AddComponent<TutorialFocusFx>();
            }
        }
        if (focusFx) focusFx.Bind(focusGate);

        if (!tooltip)
        {
            tooltip = GetComponentInChildren<TutorialTooltip>(true);
            if (!tooltip)
            {
                var go = new GameObject("Tooltip", typeof(RectTransform));
                go.layer = gameObject.layer;
                var rt = (RectTransform)go.transform;
                rt.SetParent(canvas ? canvas.transform : transform, false);
                Stretch(rt);
                tooltip = go.AddComponent<TutorialTooltip>();
            }
        }

        // The hand is drawn last so nothing can cover it.
        if (hand) hand.transform.SetAsLastSibling();
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Fade level of the full-screen dim, 0 = fully lit board.</summary>
    public void SetDim(float alpha)
    {
        if (!dimmer) return;

        dimmer.alpha = Mathf.Clamp01(alpha);
        dimmer.gameObject.SetActive(dimmer.alpha > 0.001f);
    }

    /// <summary>
    /// Turns the full-screen input block on or off.
    ///
    /// This now goes through the focus gate. The prefab's original `blocker` Image
    /// was authored with alpha 0 AND cullTransparentMesh on, so its mesh was culled
    /// and the GraphicRaycaster never saw it - calling this used to do nothing at
    /// all. The gate handles the transparency trap properly.
    /// </summary>
    public void SetBlockInput(bool block)
    {
        if (focusGate)
        {
            if (block) focusGate.BlockAll(0f);
            else focusGate.Release();
        }

        if (blocker) blocker.SetActive(false);   // never rely on the broken one
    }

    /// <summary>Puts the overlay back to "nothing showing", and gives input back.</summary>
    public void ClearAll()
    {
        if (hand) hand.StopAndHide();
        if (caption) caption.Hide();
        if (tooltip) tooltip.Hide();
        if (focusGate) focusGate.Release();
        SetDim(0f);
        if (blocker) blocker.SetActive(false);
    }

    /// <summary>The overlay in the currently loaded scenes, or null.</summary>
    public static TutorialOverlay FindInScene()
    {
        return FindObjectOfType<TutorialOverlay>(true);
    }
}
