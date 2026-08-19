using UnityEngine;

/// <summary>
/// Converts screen positions (mouse/touch) to:
///  - world position on the board's horizontal plane (XZ at board Y)
///  - grid cell under the cursor (via BoardGridXZ)
///
/// Assumptions:
///  - The board is horizontal (normal = Vector3.up), i.e., BoardRoot has no rotation.
///  - Camera can be orthographic or perspective.
/// </summary>
[DisallowMultipleComponent]
public class RaycastToBoard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private BoardGridXY board;

    [Header("Behavior")]
    [Tooltip("Clamp returned cells to be inside the board bounds.")]
    [SerializeField] private bool clampInsideGrid = true;

    [Tooltip("Draw a small gizmo where the last hit occurred (Scene view only).")]
    [SerializeField] private bool drawHitGizmo = true;

    // last hit cache (debug only)
    private Vector3 _lastWorldHit;
    private bool _hasLastHit;

    private void Awake()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (board == null)
        {
            board = FindObjectOfType<BoardGridXY>();
            if (board == null)
                Debug.LogWarning("[RaycastToBoard] No BoardGridXZ found. Assign it in the Inspector.");
        }
    }

    /// <summary>
    /// Raycasts from the given screen position to the board's horizontal plane (world Y = board.BoardWorldY).
    /// Returns true if we hit the plane in front of the camera.
    /// </summary>
    public bool TryScreenToBoardWorld(Vector2 screenPos, out Vector3 worldPos)
    {
        worldPos = default;

        if (targetCamera == null || board == null)
            return false;

        Ray ray = targetCamera.ScreenPointToRay(screenPos);

        // Plane at board's Y, horizontal (normal = up)
        float y = board.BoardWorldZ;
        Plane plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));

        if (!plane.Raycast(ray, out float dist) || dist < 0f)
            return false;

        worldPos = ray.GetPoint(dist);
        _lastWorldHit = worldPos;
        _hasLastHit = true;
        return true;
    }

    /// <summary>
    /// Gets the grid cell under the given screen position.
    /// Returns true if the world hit was inside the board bounds.
    /// If clampInsideGrid is true, cells that fall outside will be clamped to the nearest valid cell.
    /// </summary>
    public bool TryScreenToCell(Vector2 screenPos, out Vector2Int cell)
    {
        cell = default;

        if (!TryScreenToBoardWorld(screenPos, out Vector3 world))
            return false;

        // Direct conversion to cell
        bool inside = board.TryWorldToCell(world, out cell);

        if (!inside && clampInsideGrid)
        {
            // Clamp to nearest valid cell
            int x = Mathf.Clamp(cell.x, 0, board.Width - 1);
            int y = Mathf.Clamp(cell.y, 0, board.Height - 1);
            cell = new Vector2Int(x, y);
            return true;
        }

        return inside;
    }

    /// <summary>
    /// Convenience: returns the world-space center of a cell.
    /// </summary>
    public Vector3 CellCenterWorld(Vector2Int cell)
    {
        return board != null ? board.CellCenterWorld(cell) : Vector3.zero;
    }

    /// <summary>
    /// Given a screen position, returns the best anchor cell for a piece
    /// (i.e., where the piece's normalized (0,0) offset should land).
    /// For now this is simply the cell under the cursor.
    /// </summary>
    public bool TryGetAnchorForPieceFromScreen(Piece piece, Vector2 screenPos, out Vector2Int anchorCell)
    {
        // In our normalized-offsets setup, placing the piece so its (0,0) sits under
        // the cursor is a simple, predictable behavior. We can get fancier later.
        return TryScreenToCell(screenPos, out anchorCell);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawHitGizmo || !_hasLastHit) return;

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(_lastWorldHit, 0.05f);
    }
#endif
}
