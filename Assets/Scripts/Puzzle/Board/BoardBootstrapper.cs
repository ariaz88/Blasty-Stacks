using UnityEngine;
public class BoardBootstrapper : MonoBehaviour
{
    [Tooltip("If true and a piece's offsets weren't authored, we�ll auto-build them from child colliders once at startup.")]
    [SerializeField] private bool autoBuildOffsets = true;

    private void Start()
    {
        var board = FindObjectOfType<BoardGridXY>();
        if (!board) { Debug.LogError("No BoardGridXY found in scene."); return; }

        var pieces = FindObjectsOfType<PieceSimple>(includeInactive: false);
        foreach (var p in pieces)
        {
            if (!p) continue;

            p.SetBoard(board); // ensure the reference BEFORE any TryPlace

            if (autoBuildOffsets)
            {
                // If offsets were not authored, build once from child colliders.
                // (PieceSimple will also do this in Start if needed; calling here is harmless.)
                p.AutoBuildOffsetsFromChildren();
            }

            // Prefer deriving the anchor from the sub-blocks (authoritative).
            Vector2Int anchor;
            if (!p.TrySolveAnchorFromChildren(out anchor))
            {
                // Fallback: derive from the current root position on the XY board.
                if (!board.TryWorldToCell(p.transform.position, out anchor))
                    anchor = p.InitialAnchor;
            }

            // Keep the footprint inside the board and place.
            anchor = board.ClampAnchorToFitShape(anchor, p.ShapeOffsets);
            p.TryPlace(anchor); // placement sets board occupancy and snaps to CellCenterWorld(anchor)
        }
    }
}
