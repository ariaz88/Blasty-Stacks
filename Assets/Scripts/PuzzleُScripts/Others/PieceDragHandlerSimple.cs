using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PieceSimple))]
public class PieceDragHandlerSimple : MonoBehaviour
{
    public BoardGridXY board;
    public Camera cam;
    public float liftWhileDragging = 0.12f;

    private PieceSimple piece;
    private Vector2Int startAnchor;
    private Vector2Int lastValidAnchor;
    private bool dragging;

    private readonly List<Vector2Int> tmpFootprint = new();

    private void Awake()
    {
        piece = GetComponent<PieceSimple>();
        if (!board) board = GetComponentInParent<BoardGridXY>();
        if (!cam) cam = Camera.main;
    }

    private void OnMouseDown()
    {
        if (!board || !cam) return;

        dragging = true;
        startAnchor = piece.Anchor;
        lastValidAnchor = startAnchor;

        // lift a bit for feedback
        var p = transform.position;
        transform.position = new Vector3(p.x, board.BoardWorldZ + liftWhileDragging, p.z);
    }

    private void OnMouseDrag()
    {
        if (!dragging) return;

        // project mouse to plane at board height
        var plane = new Plane(board.transform.up, new Vector3(0f, board.BoardWorldZ, 0f));
        var ray = cam.ScreenPointToRay(Input.mousePosition);

        if (!plane.Raycast(ray, out float enter))
        {
            // keep visual on last valid
            transform.position = board.CellCenterWorld(lastValidAnchor) + new Vector3(0, liftWhileDragging, 0);
            return;
        }

        Vector3 world = ray.GetPoint(enter);

        // Convert to a cell under the cursor; if off-grid, we'll clamp the anchor next
        if (!board.TryWorldToCell(world, out var pointerCell))
        {
            // round to nearest index in local grid space as a fallback
            var local = board.transform.InverseTransformPoint(world);
            pointerCell = new Vector2Int(
                Mathf.FloorToInt(local.x / board.CellSize),
                Mathf.FloorToInt(local.z / board.CellSize)
            );
        }

        // Ensure the entire shape stays inside the board
        Vector2Int candidate = board.ClampAnchorToFitShape(pointerCell, piece.ShapeOffsets);

        // Build footprint & validate vs blocked/occupied (allow our own current cells)
        board.ShapeToCells(candidate, piece.ShapeOffsets, tmpFootprint);
        bool canPlaceHere = board.AreCellsPlaceableForMover(tmpFootprint, piece.PieceId);

        if (canPlaceHere)
        {
            lastValidAnchor = candidate;
            transform.position = board.CellCenterWorld(candidate) + new Vector3(0, liftWhileDragging, 0);
        }
        else
        {
            // Prevent sliding into illegal tiles: stick to last valid anchor
            transform.position = board.CellCenterWorld(lastValidAnchor) + new Vector3(0, liftWhileDragging, 0);
        }
    }

    private void OnMouseUp()
    {
        if (!dragging) return;
        dragging = false;

        // Commit to the last valid anchor (PieceSimple.TryPlace releases+re-occupies properly)
        piece.TryPlace(lastValidAnchor);
    }
    // Add these public proxies inside PieceDragHandlerSimple
    public void OnMouseDownProxy() { OnMouseDown(); }
    public void OnMouseDragProxy() { OnMouseDrag(); }
    public void OnMouseUpProxy() { OnMouseUp(); }

}
