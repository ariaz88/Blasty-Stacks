using UnityEngine;

public class MouseForwarderToParent : MonoBehaviour
{
    public PieceDragHandlerSimple target; // drag the parent’s handler here in Inspector

    void OnMouseDown() { if (target) target.OnMouseDownProxy(); }
    void OnMouseDrag() { if (target) target.OnMouseDragProxy(); }
    void OnMouseUp() { if (target) target.OnMouseUpProxy(); }
}
