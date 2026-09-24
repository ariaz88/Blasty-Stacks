using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tags any object in a scene with a string id so a tutorial step can point at
/// it without the TutorialSequenceSO holding a scene reference (an asset cannot
/// reference a scene object anyway).
///
/// Works for BOTH kinds of object:
///   - a world object (a Transform)     -> resolved through the world camera
///   - a UI element (a RectTransform)   -> resolved through its own Canvas
///
/// Drop one on a button, a HUD icon, a castle gate, anything a tutorial needs to
/// highlight, then reference it by id from TutorialTarget.SceneAnchor.
///
/// DUPLICATE IDS ARE ALLOWED, ON PURPOSE. Find() returns the first ENABLED anchor
/// with that id, and the menu is full of pairs where exactly one of the two is
/// ever active - UnitsButton_InActive / UnitsButton_Selected, UpgradeButton /
/// UpgradeButton_DISABLED. Giving both halves of such a pair the same id is the
/// cleanest way to say "whichever of these is currently on screen".
/// </summary>
[DisallowMultipleComponent]
public class TutorialAnchor : MonoBehaviour
{
    [Tooltip("Unique id a tutorial step points at, e.g. \"battle_button\". " +
             "Two objects MAY share an id when only one is ever enabled.")]
    [SerializeField] private string anchorId = "";

    [Tooltip("Optional. Resolve to THIS transform instead of my own - lets a fixed " +
             "component stand in for a child that is created at runtime.")]
    [SerializeField] private Transform pointAtOverride;

    [Tooltip("Stop walking up for blocking CanvasGroups at this transform. Leave " +
             "empty to walk to the root.")]
    [SerializeField] private Transform interactableCheckRoot;

    public string AnchorId => anchorId;

    // Set by TutorialChildAnchor so "the first card in this list" can be resolved
    // fresh on every call, without this component knowing what a card is.
    private Func<Transform> _provider;

    // Live anchors, refreshed by OnEnable/OnDisable. A plain list (not a
    // dictionary) because there are only ever a handful and duplicate ids
    // are meaningful here - see the class doc.
    private static readonly List<TutorialAnchor> Live = new List<TutorialAnchor>();

    private void OnEnable()
    {
        if (!Live.Contains(this)) Live.Add(this);
    }

    private void OnDisable()
    {
        Live.Remove(this);
    }

    /// <summary>Lets a companion component decide what this anchor points at.</summary>
    public void SetTargetProvider(Func<Transform> provider) => _provider = provider;

    /// <summary>Sets the id from code, for anchors added at runtime.</summary>
    public void SetAnchorId(string id) => anchorId = id;

    /// <summary>Finds an enabled anchor by id. Returns null when there is none.</summary>
    public static TutorialAnchor Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < Live.Count; i++)
        {
            var a = Live[i];
            if (!a || a.anchorId != id) continue;

            // An anchor on a disabled branch is not "on screen", and pointing at it
            // would aim the hand at a stale position.
            if (!a.gameObject.activeInHierarchy) continue;

            return a;
        }
        return null;
    }

    // ------------------------------------------------------------------
    //  Resolving
    // ------------------------------------------------------------------

    /// <summary>
    /// What this anchor actually stands for: the runtime provider if one is set,
    /// else the explicit override, else this transform. Can be null when a provider
    /// has nothing to offer yet (an empty list), which callers treat as "wait".
    /// </summary>
    public Transform ResolveTransform()
    {
        if (_provider != null)
        {
            Transform t = null;
            try { t = _provider(); }
            catch (Exception e) { Debug.LogException(e, this); }
            return t;
        }

        return pointAtOverride ? pointAtOverride : transform;
    }

    /// <summary>
    /// The Button this anchor stands for. GetComponentInChildren, because a hero
    /// card carries its Button on a CHILD literally named "Button" - looking only
    /// at the root is why an AnchorClicked condition on a card would never fire.
    /// </summary>
    public Button ResolveButton()
    {
        var t = ResolveTransform();
        if (!t) return null;

        var own = t.GetComponent<Button>();
        return own ? own : t.GetComponentInChildren<Button>(false);
    }

    /// <summary>
    /// Present AND able to receive a click right now.
    ///
    /// This is what lets a step wait out MainMenuPanelController's 0.35s panel
    /// slide instead of putting a hand on a button that cannot be pressed: for the
    /// whole slide it sets blocksRaycasts=false on the panel's CanvasGroup. Writing
    /// to those CanvasGroups is pointless - the controller rewrites them on every
    /// tab change - so the tutorial waits rather than fights.
    /// </summary>
    public bool IsInteractableNow()
    {
        var t = ResolveTransform();
        if (!t || !t.gameObject.activeInHierarchy) return false;

        var selectable = t.GetComponent<Selectable>();
        if (!selectable) selectable = t.GetComponentInChildren<Selectable>(false);
        if (selectable && !selectable.IsInteractable()) return false;

        Transform walk = t;
        while (walk)
        {
            var cg = walk.GetComponent<CanvasGroup>();
            if (cg)
            {
                if (!cg.blocksRaycasts || !cg.interactable) return false;
                if (cg.ignoreParentGroups) break;
            }

            if (interactableCheckRoot && walk == interactableCheckRoot) break;
            walk = walk.parent;
        }

        return true;
    }

    /// <summary>
    /// Screen position of this anchor. UI anchors go through their own canvas
    /// (which may be Overlay or Camera mode); world anchors use the camera the
    /// caller supplies.
    /// </summary>
    public bool TryGetScreenPosition(Camera worldCamera, out Vector2 screenPos)
    {
        screenPos = default;

        var t = ResolveTransform();
        if (!t) return false;

        if (t is RectTransform rt)
        {
            // rect.center, not rt.position: rt.position is the PIVOT, so a button
            // authored with a corner pivot would take the hand to its corner.
            screenPos = RectTransformUtility.WorldToScreenPoint(UiCameraFor(rt),
                                                                rt.TransformPoint(rt.rect.center));
            return true;
        }

        if (!worldCamera) return false;

        screenPos = worldCamera.WorldToScreenPoint(t.position);
        return true;
    }

    /// <summary>
    /// The anchor's full rectangle in screen space - what the focus gate punches a
    /// hole in and what the tooltip hangs off. Falls back to a square around the
    /// point for non-UI anchors, which have no rect of their own.
    /// </summary>
    public bool TryGetScreenRect(Camera worldCamera, Vector2 fallbackSize, out Rect screenRect)
    {
        screenRect = default;

        var t = ResolveTransform();
        if (!t) return false;

        if (t is RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            Camera cam = UiCameraFor(rt);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
            Vector2 max = min;

            for (int i = 1; i < 4; i++)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            screenRect = new Rect(min, max - min);
            return screenRect.width > 0f && screenRect.height > 0f;
        }

        if (!TryGetScreenPosition(worldCamera, out var center)) return false;

        screenRect = new Rect(center - fallbackSize * 0.5f, fallbackSize);
        return true;
    }

    /// <summary>The camera a UI element's own canvas renders through, or null for Overlay.</summary>
    private static Camera UiCameraFor(RectTransform rt)
    {
        var canvas = rt.GetComponentInParent<Canvas>();
        if (!canvas) return null;

        canvas = canvas.rootCanvas ? canvas.rootCanvas : canvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }
}
