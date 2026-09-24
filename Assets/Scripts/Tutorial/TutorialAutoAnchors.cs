using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attaches every TutorialAnchor the onboarding needs, at runtime, by reading the
/// references the game's own controllers already hold.
///
/// WHY NOT AUTHOR THE ANCHORS IN THE SCENE: the targets are awkward to address by
/// hand. Two different GameObjects are called "DEPLOYEDGridLayout (1)", there are
/// five objects called "BackButton", and several names carry a trailing space
/// ("UnitsButton_Selected "). Hand-placing components in a 74k-line scene file to
/// hit the right one of each is exactly the kind of edit that silently targets the
/// wrong object. MainMenuPanelController and UnitsPanelController already have the
/// correct references wired in the Inspector, so this reads those instead and is
/// right by construction.
///
/// It also spawns the TutorialOverlay when the scene has none, so adding the whole
/// onboarding to a scene is "drop in one GameObject with this component".
///
/// Anchors are added to objects that may be INACTIVE at the time (the Units panel
/// and the Lose panel both start switched off). That is fine and intended:
/// TutorialAnchor.Find skips anchors that are not active in the hierarchy, so each
/// one simply becomes findable at the moment its screen appears.
/// </summary>
[DisallowMultipleComponent]
public class TutorialAutoAnchors : MonoBehaviour
{
    [Header("Overlay")]
    [Tooltip("Spawned when the scene has no TutorialOverlay. " +
             "Assets/PREFABS/Tutorial/TutorialOverlay.prefab")]
    [SerializeField] private TutorialOverlay overlayPrefab;

    [Header("Anchor ids (must match the sequence assets)")]
    [SerializeField] private string navUnitsId = "menu_nav_units";
    [SerializeField] private string firstDeployedCardId = "units_first_deployed_card";
    [SerializeField] private string upgradeButtonId = "units_upgrade_button";
    [SerializeField] private string detailBackId = "units_detail_back";
    [SerializeField] private string loseLeaveStageId = "lose_leave_stage";

    [Header("Timing")]
    [Tooltip("Extra passes after the first, so a controller that wires its " +
             "references in Start is still picked up. Cheap - it stops as soon as " +
             "everything it can find is bound.")]
    [SerializeField] private int extraPasses = 3;

    private void Awake()
    {
        EnsureOverlay();
        Bind();
    }

    private IEnumerator Start()
    {
        for (int i = 0; i < extraPasses; i++)
        {
            yield return null;
            Bind();
        }
    }

    private void EnsureOverlay()
    {
        if (TutorialOverlay.FindInScene()) return;

        if (!overlayPrefab)
        {
            Debug.LogWarning("[Tutorial] No TutorialOverlay in the scene and no prefab assigned " +
                             "on TutorialAutoAnchors - the onboarding cannot be shown.", this);
            return;
        }

        var overlay = Instantiate(overlayPrefab);
        overlay.name = overlayPrefab.name;   // drop the "(Clone)" so logs stay readable
    }

    // ------------------------------------------------------------------
    //  Binding
    // ------------------------------------------------------------------

    private void Bind()
    {
        BindMenu();
        BindUnits();
        BindLosePanel();
    }

    private void BindMenu()
    {
        var nav = FindFirst<MainMenuPanelController>();
        if (!nav) return;

        // Both halves of the pair get the SAME id on purpose - exactly one of them
        // is ever active, and TutorialAnchor.Find returns the active one.
        Attach(nav.UnitsButton, navUnitsId);
        Attach(nav.UnitsSelectedButton, navUnitsId);
    }

    private void BindUnits()
    {
        var units = FindFirst<UnitsPanelController>();
        if (!units) return;

        // Same-id pair again: WireUpgradeButton swaps these two by affordability.
        Attach(units.UpgradeButton, upgradeButtonId);
        Attach(units.UpgradeDisabledButton, upgradeButtonId);
        Attach(units.DetailBackButton, detailBackId);

        BindFirstDeployedCard(units.DeployedContainer);
    }

    private void BindFirstDeployedCard(Transform container)
    {
        if (!container) return;

        var anchor = container.GetComponent<TutorialAnchor>();
        if (!anchor)
        {
            anchor = container.gameObject.AddComponent<TutorialAnchor>();
            anchor.SetAnchorId(firstDeployedCardId);
        }

        // The cards themselves are destroyed and rebuilt on every panel open, so the
        // anchor lives on the container and this resolves child 0 on demand.
        if (!container.GetComponent<TutorialChildAnchor>())
            container.gameObject.AddComponent<TutorialChildAnchor>().Configure(container, 0);
    }

    private void BindLosePanel()
    {
        var lose = FindFirst<LoseGame>();
        if (!lose) return;

        Attach(lose.MainMenuButton, loseLeaveStageId);
    }

    // ------------------------------------------------------------------
    //  Helpers
    // ------------------------------------------------------------------

    private static void Attach(Button button, string anchorId)
    {
        if (!button || string.IsNullOrEmpty(anchorId)) return;

        var existing = button.GetComponent<TutorialAnchor>();
        if (existing)
        {
            // Never stomp an id somebody authored by hand - say so instead.
            if (existing.AnchorId != anchorId)
                Debug.LogWarning($"[Tutorial] '{button.name}' already carries anchor id " +
                                 $"'{existing.AnchorId}'; leaving it alone instead of " +
                                 $"setting '{anchorId}'.", button);
            return;
        }

        button.gameObject.AddComponent<TutorialAnchor>().SetAnchorId(anchorId);
    }

    /// <summary>Includes inactive: the Units panel and the Lose panel both start off.</summary>
    private static T FindFirst<T>() where T : Object
        => FindObjectOfType<T>(true);
}
