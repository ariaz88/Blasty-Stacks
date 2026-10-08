using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Passes swipe drags that start on the Home card strip's Viewport (the gaps
/// between and beside the cards) to HomeCardsPager. The Viewport is a SIBLING of
/// the pager's Content, so those drags would never bubble up to the pager.
/// Added at runtime by HomeCardsPager.BuildIfNeeded - nothing to wire.
/// </summary>
public class HomeCardsSwipeForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public HomeCardsPager target;

    public void OnBeginDrag(PointerEventData e) { if (target) target.OnBeginDrag(e); }
    public void OnDrag(PointerEventData e)      { if (target) target.OnDrag(e); }
    public void OnEndDrag(PointerEventData e)   { if (target) target.OnEndDrag(e); }
}
