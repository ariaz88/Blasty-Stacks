using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[ExecuteAlways]
public class BoardGridXY : MonoBehaviour
{
    // Backward-compatibility for older scripts (e.g., PieceSimple.cs)
    public float InnerCellSize => cellSize;          // actual square size
    public float OuterCellSize => CellPitch;         // square + gap (start-to-start distance)

    [Header("Grid Dimensions")]
    [SerializeField, Min(1)] private int width = 8;
    [SerializeField, Min(1)] private int height = 8;

    [Header("Cell Metrics")]
    [Tooltip("Actual size of each square cell (world units).")]
    [SerializeField, Min(0.01f)] private float cellSize = 1f;

    [Tooltip("Gap between neighboring cells (world units). This space belongs to NO cell.")]
    [SerializeField, Min(0f)] private float cellPadding = 0f;

    [Tooltip("Board plane depth in LOCAL Z.")]
    [SerializeField] private float localZ = 0f;

    [Header("Debug & Gizmos")]
    [SerializeField] private bool drawCellGizmos = true;
    [SerializeField] private Color cellWireColor = new Color(0.2f, 1f, 0.2f, 0.85f);

    [SerializeField] private bool drawStateOverlays = true;
    [SerializeField] private Color blockedFillColor = new Color(1f, 0.2f, 0.2f, 0.25f);
    [SerializeField] private Color occupiedFillColor = new Color(0.2f, 0.6f, 1f, 0.20f);
    [SerializeField] private Color ghostFillColor = new Color(0.7f, 0.7f, 0.7f, 0.18f);

    // data
    private bool[,] blocked;
    private bool[,] ghost;
    private int[,] occupancy; // 0 = empty, >0 = pieceId

    private bool isBoardEmpty;

    public int Width => width;
    public int Height => height;

    public float CellSize => cellSize;
    public float CellPadding => cellPadding;

    // Distance between the start of one cell and the start of the next cell.
    // This is what creates the visual "gap" when padding > 0.
    public float CellPitch => cellSize + cellPadding;

    // Local-space board width/height (no extra padding after last cell).
    public float BoardSizeX => Mathf.Max(0f, width * CellPitch - cellPadding);
    public float BoardSizeY => Mathf.Max(0f, height * CellPitch - cellPadding);

    public float BoardWorldZ
    {
        get
        {
            var w = transform.TransformPoint(new Vector3(0f, 0f, localZ));
            return w.z;
        }
    }

    public void EnsureReady()
    {
        if (blocked == null || blocked.GetLength(0) != width || blocked.GetLength(1) != height)
            blocked = new bool[width, height];

        if (ghost == null || ghost.GetLength(0) != width || ghost.GetLength(1) != height)
            ghost = new bool[width, height];

        if (occupancy == null || occupancy.GetLength(0) != width || occupancy.GetLength(1) != height)
            occupancy = new int[width, height];
    }

    public void ClearAll()
    {
        EnsureReady();
        System.Array.Clear(blocked, 0, blocked.Length);
        System.Array.Clear(ghost, 0, ghost.Length);
        System.Array.Clear(occupancy, 0, occupancy.Length);
    }

    public void ClearAllOccupancy()
    {
        EnsureReady();
        System.Array.Clear(occupancy, 0, occupancy.Length);
    }

    public bool IsInside(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < width && c.y < height;

    public void SetBlocked(Vector2Int c, bool v) { EnsureReady(); if (IsInside(c)) blocked[c.x, c.y] = v; }
    public bool IsBlocked(Vector2Int c) { EnsureReady(); return IsInside(c) && blocked[c.x, c.y]; }

    public void SetGhost(Vector2Int c, bool v) { EnsureReady(); if (IsInside(c)) ghost[c.x, c.y] = v; }
    public bool IsGhost(Vector2Int c) { EnsureReady(); return IsInside(c) && ghost[c.x, c.y]; }

    public int GetOccupant(Vector2Int c) { EnsureReady(); return IsInside(c) ? occupancy[c.x, c.y] : -1; }

    public void OccupyCells(List<Vector2Int> cells, int ownerId)
    {
        EnsureReady();
        foreach (var c in cells)
            if (IsInside(c))
                occupancy[c.x, c.y] = ownerId;
    }

    public void ReleaseCellsOwnedBy(List<Vector2Int> cells, int ownerId)
    {
        EnsureReady();
        foreach (var c in cells)
            if (IsInside(c) && occupancy[c.x, c.y] == ownerId)
                occupancy[c.x, c.y] = 0;
    }

    public bool AreCellsPlaceableForMover(List<Vector2Int> cells, int moverId)
    {
        EnsureReady();
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (!IsInside(c)) return false;
            if (blocked[c.x, c.y]) return false;
            if (ghost[c.x, c.y]) return false;

            int o = occupancy[c.x, c.y];
            if (o != 0 && o != moverId) return false;
        }
        return true;
    }

    public void ShapeToCells(Vector2Int anchor, IReadOnlyList<Vector2Int> offsets, List<Vector2Int> outCells)
    {
        outCells.Clear();
        for (int i = 0; i < offsets.Count; i++)
            outCells.Add(anchor + offsets[i]);
    }

    public Vector2Int ClampAnchorToFitShape(Vector2Int anchor, IReadOnlyList<Vector2Int> offsets)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < offsets.Count; i++)
        {
            var o = offsets[i];
            if (o.x < minX) minX = o.x;
            if (o.y < minY) minY = o.y;
            if (o.x > maxX) maxX = o.x;
            if (o.y > maxY) maxY = o.y;
        }

        int axMin = -minX;
        int ayMin = -minY;
        int axMax = (width - 1) - maxX;
        int ayMax = (height - 1) - maxY;

        return new Vector2Int(
            Mathf.Clamp(anchor.x, axMin, axMax),
            Mathf.Clamp(anchor.y, ayMin, ayMax)
        );
    }

    // Checking whether any stacks inside the BOARD or not
    public bool HasAnyOccupiedCells()
    {
        EnsureReady();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (occupancy[x, y] != 0)
                    return true;
            }
        }
        isBoardEmpty = true;
        return false;
    }

    public int CountOccupiedCells()
    {
        EnsureReady();
        int count = 0;
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                if (occupancy[x, y] != 0)
                    count++;
        return count;
    }

    // -------------------- Grid <-> World --------------------

    // Local-space min corner of a cell square (not including the gap).
    private Vector3 CellMinLocal(Vector2Int cell)
    {
        float p = CellPitch;
        return new Vector3(cell.x * p, cell.y * p, localZ);
    }

    private Vector3 CellCenterLocal(Vector2Int cell)
    {
        Vector3 min = CellMinLocal(cell);
        float half = cellSize * 0.5f;
        return new Vector3(min.x + half, min.y + half, localZ);
    }

    public Vector3 CellCenterWorld(Vector2Int cell)
    {
        return transform.TransformPoint(CellCenterLocal(cell));
    }

    // True if local point is inside the square area of this cell (not in the gap).
    private bool IsInsideCellSquareLocal(Vector2 localXY, Vector2Int cell)
    {
        float p = CellPitch;

        float cellMinX = cell.x * p;
        float cellMinY = cell.y * p;

        float dx = localXY.x - cellMinX;
        float dy = localXY.y - cellMinY;

        return dx >= 0f && dy >= 0f && dx <= cellSize && dy <= cellSize;
    }

    public bool TryWorldToCell(Vector3 worldPos, out Vector2Int cell)
    {
        Vector3 onPlane = SnapToPlane(worldPos);
        Vector3 local = transform.InverseTransformPoint(onPlane);

        float p = CellPitch;

        int cx = Mathf.FloorToInt(local.x / p);
        int cy = Mathf.FloorToInt(local.y / p);
        cell = new Vector2Int(cx, cy);

        if (!IsInside(cell)) return false;

        // If the pointer is inside the gap region, return false (no cell).
        if (!IsInsideCellSquareLocal(new Vector2(local.x, local.y), cell))
            return false;

        return true;
    }

    public Vector3 BoardPlaneNormal() => transform.forward;

    public Vector3 ProjectToBoardPlane(Vector3 world)
    {
        Vector3 n = BoardPlaneNormal();
        Vector3 p0 = CellCenterWorld(new Vector2Int(0, 0));
        float d = Vector3.Dot(world - p0, n);
        return world - n * d;
    }

    public Vector3 SnapToPlane(Vector3 world) => ProjectToBoardPlane(world);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawCellGizmos) return;

        EnsureReady();

        // Draw each cell as a separate square (wire).
        Color prev = Gizmos.color;
        Gizmos.color = cellWireColor;

        float zEps = 0.0001f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = new Vector2Int(x, y);
                DrawCellWireLocal(cell, zEps);
            }
        }

        // Optional overlays (ghost/blocked/occupied) drawn as filled quads.
        if (drawStateOverlays)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (ghost[x, y]) DrawCellFillWorld(new Vector2Int(x, y), ghostFillColor);
                    else if (blocked[x, y]) DrawCellFillWorld(new Vector2Int(x, y), blockedFillColor);
                    else if (occupancy[x, y] != 0) DrawCellFillWorld(new Vector2Int(x, y), occupiedFillColor);
                }
            }
        }

        Gizmos.color = prev;
    }

    private void DrawCellWireLocal(Vector2Int cell, float zEps)
    {
        Vector3 min = CellMinLocal(cell);
        float z = min.z + zEps;

        Vector3 aL = new Vector3(min.x, min.y, z);
        Vector3 bL = new Vector3(min.x + cellSize, min.y, z);
        Vector3 cL = new Vector3(min.x + cellSize, min.y + cellSize, z);
        Vector3 dL = new Vector3(min.x, min.y + cellSize, z);

        Vector3 a = transform.TransformPoint(aL);
        Vector3 b = transform.TransformPoint(bL);
        Vector3 c = transform.TransformPoint(cL);
        Vector3 d = transform.TransformPoint(dL);

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }

    private void DrawCellFillWorld(Vector2Int cell, Color color)
    {
        Vector3 min = transform.TransformPoint(CellMinLocal(cell));
        Vector3 max = transform.TransformPoint(CellMinLocal(cell) + new Vector3(cellSize, cellSize, 0f));

        float z = min.z + 0.0002f;

        Vector3 a = new Vector3(min.x, min.y, z);
        Vector3 b = new Vector3(max.x, min.y, z);
        Vector3 c = new Vector3(max.x, max.y, z);
        Vector3 d = new Vector3(min.x, max.y, z);

        Color prev = Gizmos.color;
        Gizmos.color = color;

        // Filled look using diagonals + border (simple and readable in Scene view)
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
        Gizmos.DrawLine(a, c);
        Gizmos.DrawLine(b, d);

        Gizmos.color = prev;
    }
#endif
}
