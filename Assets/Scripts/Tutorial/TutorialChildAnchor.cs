using UnityEngine;

/// <summary>
/// Lets a tutorial point at "the Nth item in this list" when the items are
/// Instantiated and Destroyed under our feet.
///
/// The hero roster is the case this exists for: UnitsPanelController destroys and
/// re-creates every card on every OnEnable (BuildCardsIntoBuckets), and keeps the
/// containers, the card list and the card lookup all private. A TutorialAnchor
/// authored on a card would die with the card; an accessor on the controller would
/// mean editing a 1300-line file we otherwise never touch.
///
/// So the anchor id lives on the CONTAINER, which is a scene object and never goes
/// away, and this component re-points that anchor at child N on demand. The lookup
/// is pull-based - resolved the moment a step asks, never cached - so the rebuild
/// is simply invisible to the tutorial.
///
/// Reusable for any future "point at the first item in this list" step.
/// </summary>
[RequireComponent(typeof(TutorialAnchor))]
[DisallowMultipleComponent]
public class TutorialChildAnchor : MonoBehaviour
{
    [Tooltip("The list to read. Empty = this transform.")]
    [SerializeField] private Transform container;

    [Tooltip("Which item to point at. 0 = the first one.")]
    [SerializeField] private int childIndex = 0;

    [Tooltip("Skip inactive children, so a hidden layout spacer can never become " +
             "'the first card'.")]
    [SerializeField] private bool skipInactive = true;

    private void Awake()
    {
        GetComponent<TutorialAnchor>().SetTargetProvider(ResolveChild);
    }

    /// <summary>Null when the list is empty - the waiting step then simply keeps waiting.</summary>
    private Transform ResolveChild()
    {
        var parent = container ? container : transform;

        int seen = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (skipInactive && !child.gameObject.activeSelf) continue;

            if (seen == childIndex) return child;
            seen++;
        }

        return null;
    }

    /// <summary>Wiring helper for anchors built at runtime (see TutorialAutoAnchors).</summary>
    public void Configure(Transform listContainer, int index)
    {
        container = listContainer;
        childIndex = index;
    }
}
