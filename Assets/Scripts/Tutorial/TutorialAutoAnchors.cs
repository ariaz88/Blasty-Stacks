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

    [Tooltip("Anchors for deployed cards 2, 3, 4... in deck order. Element 0 is the " +
             "SECOND card - the first keeps its own id above.")]
    [SerializeField] private string[] moreDeployedCardIds =
        { "units_deployed_card_2", "units_deployed_card_3", "units_deployed_card_4" };
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
        BindMoreDeployedCards(units.DeployedContainer);
    }

    /// <summary>
    /// Cards 2..N. A GameObject carries only ONE TutorialAnchor, and the container
    /// already holds card 1's, so each extra card gets an empty helper object.
    ///
    /// The helpers sit BESIDE the container (under its parent), never inside it:
    /// UnitsPanelController.ClearContainer destroys EVERY child of the container on
    /// each rebuild, and a helper inside would also be counted as a card. The parent
    /// ("Deployed") is a plain RectTransform with no layout group, and the helpers
    /// get ignoreLayout anyway in case that ever changes.
    /// </summary>
    private void BindMoreDeployedCards(Transform container)
    {
        if (!container || !container.parent || moreDeployedCardIds == null) return;

        for (int i = 0; i < moreDeployedCardIds.Length; i++)
        {
            string id = moreDeployedCardIds[i];
            if (string.IsNullOrEmpty(id)) continue;

            string helperName = "TutorialAnchor_" + id;
            if (container.parent.Find(helperName)) continue;   // bound on an earlier pass

            var go = new GameObject(helperName, typeof(RectTransform), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            var rt = (RectTransform)go.transform;
            rt.SetParent(container.parent, false);
            rt.sizeDelta = Vector2.zero;

            go.AddComponent<TutorialAnchor>().SetAnchorId(id);
            go.AddComponent<TutorialChildAnchor>().Configure(container, i + 1);
        }
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
