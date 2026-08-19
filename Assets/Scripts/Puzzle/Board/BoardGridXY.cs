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

        // In-gap or outside the square => false
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


public class BoardGridXY2 : MonoBehaviour
{
    [Header("Grid Dimensions")]
    [SerializeField, Min(1)] private int width = 8;
    [SerializeField, Min(1)] private int height = 8;

    [Header("Cell Metrics")]
    [SerializeField, Min(0.01f)] private float cellSize = 1f;
    [Tooltip("Board plane depth in LOCAL Z.")]
    [SerializeField] private float localZ = 0f;

    [Header("Debug & Gizmos")]
    [SerializeField] private bool drawGridGizmos = true;
    [SerializeField] private Color gridLineColor = new Color(1, 1, 1, 0.15f);
    [SerializeField] private Color blockedFillColor = new Color(1f, 0.2f, 0.2f, 0.35f);
    [SerializeField] private Color occupiedFillColor = new Color(0.2f, 0.6f, 1f, 0.25f);
    [SerializeField] private Color ghostFillColor = new Color(0.7f, 0.7f, 0.7f, 0.2f);

    private bool isBoardEmpty;


    // data
    private bool[,] blocked;
    private bool[,] ghost;
    private int[,] occupancy;  // 0 = empty, >0 = pieceId

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    [Header("Cell Padding (logic-level)")]
    [Tooltip("Gap between neighboring cells (world units). " +
         "A point in this gap belongs to NO cell. Must be < cellSize.")]
    [SerializeField, Min(0f)] private float cellPadding = 0f;

    /// <summary>Inner usable size inside each cell (where interactions count as being “in the cell”).</summary>
    public float InnerCellSize => Mathf.Max(0f, cellSize - cellPadding);

    /// <summary>Half of the excluded margin from each side inside the cell.</summary>
    private float HalfPadPerSide => Mathf.Max(0f, cellPadding) * 0.5f;

    /// <summary>Local-space center of a cell (x,y), z fixed to localZ.</summary>
    private Vector3 CellCenterLocal(Vector2Int cell)
    {
        float s = cellSize;
        return new Vector3((cell.x + 0.5f) * s, (cell.y + 0.5f) * s, localZ);
    }

    /// <summary>Return true if a local-space point is inside the *inner* box of the given cell.</summary>
    private bool IsInsideInnerLocal(Vector2 localXY, Vector2Int cell)
    {
        float s = cellSize;
        float halfInner = InnerCellSize * 0.5f;

        // Cell center in local space (x,y)
        Vector2 c = new Vector2((cell.x + 0.5f) * s, (cell.y + 0.5f) * s);

        // Point must fall within the inner rectangle
        return Mathf.Abs(localXY.x - c.x) <= halfInner && Mathf.Abs(localXY.y - c.y) <= halfInner;
    }


    /// <summary>World-space Z where the board lives.</summary>
    public float BoardWorldZ
    {
        get
        {
            var w = transform.TransformPoint(new Vector3(0f, 0f, localZ));
            return w.z;
        }
    }

    // ****** Checking wether any stacks inside the BOARD or not ********
    public bool HasAnyOccupiedCells()
    {
        EnsureReady();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (occupancy[x, y] != 0)
                    return true;   // at least one stack/piece on the board
            }
        }
        isBoardEmpty = true;
        return false;               // board is completely empty
    }

    // Optional, if you want the exact count for future logic:
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
    // ****** Checking wether any stacks inside the BOARD or not  - END OF THE METHODS********


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
    // Clears only the occupancy map. Blocked cells remain blocked.
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

    public void OccupyCells(List<Vector2Int> cells, int ownerId) { EnsureReady(); foreach (var c in cells) if (IsInside(c)) occupancy[c.x, c.y] = ownerId; }
    public void ReleaseCellsOwnedBy(List<Vector2Int> cells, int ownerId) { EnsureReady(); foreach (var c in cells) if (IsInside(c) && occupancy[c.x, c.y] == ownerId) occupancy[c.x, c.y] = 0; }

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
        for (int i = 0; i < offsets.Count; i++) outCells.Add(anchor + offsets[i]);
    }

    /// <summary>Clamp anchor so all offsets stay inside board.</summary>
    public Vector2Int ClampAnchorToFitShape(Vector2Int anchor, IReadOnlyList<Vector2Int> offsets)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int i = 0; i < offsets.Count; i++)
        {
            var o = offsets[i];
            if (o.x < minX) minX = o.x; if (o.y < minY) minY = o.y;
            if (o.x > maxX) maxX = o.x; if (o.y > maxY) maxY = o.y;
        }
        int axMin = -minX;
        int ayMin = -minY;
        int axMax = (width - 1) - maxX;
        int ayMax = (height - 1) - maxY;
        return new Vector2Int(Mathf.Clamp(anchor.x, axMin, axMax), Mathf.Clamp(anchor.y, ayMin, ayMax));
    }

    // -------------------- Grid <-> World --------------------

    public bool TryWorldToCell1(Vector3 worldPos, out Vector2Int cell)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        int cx = Mathf.FloorToInt(local.x / cellSize);
        int cy = Mathf.FloorToInt(local.y / cellSize);
        cell = new Vector2Int(cx, cy);
        return IsInside(cell);
    }
    public bool TryWorldToCell(Vector3 worldPos, out Vector2Int cell)
    {
        // Project to board plane first (defensive if caller passes an off-plane point)
        Vector3 onPlane = SnapToPlane(worldPos);
        Vector3 local = transform.InverseTransformPoint(onPlane);

        // First pick the *geometric* cell by pitch (outer cell index)
        int cx = Mathf.FloorToInt(local.x / cellSize);
        int cy = Mathf.FloorToInt(local.y / cellSize);
        cell = new Vector2Int(cx, cy);

        // Outside board? quick out.
        if (!IsInside(cell)) return false;

        // Now enforce padding: point must fall within the cell's *inner* box
        if (!IsInsideInnerLocal(new Vector2(local.x, local.y), cell))
            return false; // it's in the gap → no cell

        return true;
    }


    public Vector3 CellCenterWorld(Vector2Int cell)
    {
        float s = cellSize;
        Vector3 local = new Vector3((cell.x + 0.5f) * s, (cell.y + 0.5f) * s, localZ);
        return transform.TransformPoint(local);
    }

    /// <summary>Exact board plane normal (world +Z).</summary>
    public Vector3 BoardPlaneNormal() => transform.forward;

    /// <summary>Project a world position orthogonally onto the board plane.</summary>
    public Vector3 ProjectToBoardPlane(Vector3 world)
    {
        Vector3 n = BoardPlaneNormal();
        Vector3 p0 = CellCenterWorld(new Vector2Int(0, 0));
        float d = Vector3.Dot(world - p0, n);
        return world - n * d;
    }

    /// <summary>Snap a world point to the board plane (alias for ProjectToBoardPlane).</summary>
    public Vector3 SnapToPlane(Vector3 world) => ProjectToBoardPlane(world);

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!drawGridGizmos) return;
        EnsureReady();
        Gizmos.color = gridLineColor;

        // lines
        for (int y = 0; y <= height; y++)
        {
            var a = transform.TransformPoint(new Vector3(0f, y * cellSize, localZ));
            var b = transform.TransformPoint(new Vector3(width * cellSize, y * cellSize, localZ));
            Gizmos.DrawLine(a, b);
        }
        for (int x = 0; x <= width; x++)
        {
            var a = transform.TransformPoint(new Vector3(x * cellSize, 0f, localZ));
            var b = transform.TransformPoint(new Vector3(x * cellSize, height * cellSize, localZ));
            Gizmos.DrawLine(a, b);
        }

        // overlays
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                var c = new Vector2Int(x, y);
                if (ghost[x, y]) DrawCellBox(c, ghostFillColor);
                else if (blocked[x, y]) DrawCellBox(c, blockedFillColor);
                else if (occupancy[x, y] != 0) DrawCellBox(c, occupiedFillColor);
            }
    }

    private void DrawCellBox1(Vector2Int cell, Color color)
    {
        var center = CellCenterWorld(cell);
        float h = (cellSize * 0.5f) - 0.01f;
        var a = new Vector3(center.x - h, center.y - h, center.z);
        var b = new Vector3(center.x + h, center.y - h, center.z);
        var c = new Vector3(center.x + h, center.y + h, center.z);
        var d = new Vector3(center.x - h, center.y + h, center.z);
        Color prev = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        Gizmos.DrawLine(a, c); Gizmos.DrawLine(b, d);
        Gizmos.color = prev;
    }
    private void DrawCellBox(Vector2Int cell, Color color)
    {
        var center = CellCenterWorld(cell);
        float half = InnerCellSize * 0.5f;   // use inner size so gizmos reflect padding
        float z = center.z + 0.0001f;

        var a = new Vector3(center.x - half, center.y - half, z);
        var b = new Vector3(center.x + half, center.y - half, z);
        var c = new Vector3(center.x + half, center.y + half, z);
        var d = new Vector3(center.x - half, center.y + half, z);

        Color prev = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);
        Gizmos.DrawLine(a, c); Gizmos.DrawLine(b, d);
        Gizmos.color = prev;
    }

#endif
}

public class BoardGridXY1 : MonoBehaviour
{
    [Header("Grid Dimensions")]
    [SerializeField, Min(1)] private int width = 8;
    [SerializeField, Min(1)] private int height = 8;

    [Header("Cell Metrics")]
    [SerializeField, Min(0.01f)] private float cellSize = 1f;

    [Tooltip("Local Y of the grid plane relative to this Transform. Usually 0. World Y = transform.TransformPoint(0,y,0).y")]
    [SerializeField] private float localZ = 0f;

    [Header("Debug & Gizmos")]
    [SerializeField] private bool drawGridGizmos = true;
    [SerializeField] private Color gridLineColor = new Color(1f, 1f, 1f, 0.15f);
    [SerializeField] private Color blockedFillColor = new Color(1f, 0.2f, 0.2f, 0.35f);
    [SerializeField] private Color occupiedFillColor = new Color(0.2f, 0.6f, 1f, 0.25f);

    // True = blocked (static obstacle); false = free
    private bool[,] blocked;
    // 0 = empty; >0 = pieceId occupying the cell
    private int[,] occupancy;


    [Header("Ghost Cells")]
    // In BoardGridXY.cs (fields)
    [SerializeField] private bool useGhostCells = true;
    [SerializeField] private List<Vector2Int> ghostPreset = new();   // optional: pre-mark in Inspector

    private bool[,] ghost; // true = ghosted (invisible + impassable)


        [Header(" Local Z ")]

    public float LocalZ => localZ;
    public Vector3 PlanePointWorld => transform.TransformPoint(new Vector3(0f, 0f, localZ));

    [Header("Cell Padding (gutters around each cell)")]
    [Tooltip("Padding on each side of a tile as a fraction of cellSize (keeps tile size, adds gaps).")]
    [SerializeField, Min(0f)] private float cellPadFracX = 0.55f;   // ✓
    [SerializeField, Min(0f)] private float cellPadFracY = 0.55f;   // ✓
                                                                    // Board offset in *cell units* (set both to 0 if you don’t want a global shift)
    [SerializeField] private Vector2 boardOffsetCells = Vector2.zero;   // ✓

    private float PadX => cellPadFracX * cellSize;                               // ✓
    private float PadY => cellPadFracY * cellSize;                               // ✓
    public float StepX => cellSize + 2f * PadX;                                 // ✓
    public float StepY => cellSize + 2f * PadY;                                 // ✓

    public float OriginXLocal => (boardOffsetCells.x * cellSize) + PadX;        // ✓ first cell’s left edge (local space)
    public float OriginYLocal => (boardOffsetCells.y * cellSize) + PadY;        // ✓ first cell’s bottom edge (local space)


    [Header("Gizmo Toggles")]
    [SerializeField] private bool showGridLines = false;          // <- turn OFF old white lines
    [SerializeField] private bool showInsetCellOutlines = true;   // <- keep the new ones
    [SerializeField] private bool showOverlays = false;           // blocked/occupied fills



    // ------------ LIFECYCLE ------------

    private void OnEnable()
    {
        EnsureBuffers();
    }

    private void OnValidate()
    {
        EnsureBuffers();
    }

    private void Start()
    {
        //blocked[2, 2] = true;
        //blocked[3,3] = true;
        //blocked[4, 4] = true;

        //SetBlocked(new Vector2Int(2, 2), true);
        //SetBlocked(new Vector2Int(4, 3), true);
    }

    // ------------ INITIALIZATION ------------

    private void EnsureBuffers()
    {
        if (blocked == null || blocked.GetLength(0) != width || blocked.GetLength(1) != height)
            blocked = new bool[width, height];

        if (occupancy == null || occupancy.GetLength(0) != width || occupancy.GetLength(1) != height)
            occupancy = new int[width, height];
    }
    public void EnsureReady() => EnsureBuffers();   // expose a safe initializer


   

    // ------------ PROPERTIES ------------

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    /// <summary>
    /// World-space Y at which cell centers lie.
    /// </summary>
 public float BoardWorldZ
{
    get
    {
        Vector3 w = transform.TransformPoint(new Vector3(0f, 0f, localZ));   // ✓
        return w.z;                                                          // ✓
    }
}


    // ------------ COORDINATE CONVERSION ------------

    /// <summary>
    /// Grid (cell) -> world center position for that cell.
    /// Cell (0,0) corresponds to local (cellSize*0.5, localZ, cellSize*0.5) from this transform.
    /// </summary>
    /// 
    



    // After Padding 
    public Vector3 CellCenterWorld(Vector2Int cell)
    {
        float cx = OriginXLocal + cell.x * StepX + cellSize * 0.5f;  // ✓ keep tile size; add padding in spacing
        float cy = OriginYLocal + cell.y * StepY + cellSize * 0.5f;  // ✓
        Vector3 local = new Vector3(cx, cy, localZ);                 // ✓
        return transform.TransformPoint(local);
    }




    public bool TryWorldToCell(Vector3 worldPos, out Vector2Int cell)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        // indices by step (works even if pointer is in the gutter; floors to the cell on the left/bottom)  ✓
        int cx = Mathf.FloorToInt((local.x - OriginXLocal) / StepX);                                      // ✓
        int cy = Mathf.FloorToInt((local.y - OriginYLocal) / StepY);                                      // ✓
        cell = new Vector2Int(cx, cy);
        return IsInside(cell);
    }



    /// <summary>
    /// Converts an anchor cell + shape offsets (relative) to absolute cells.
    /// Offsets are grid steps in X/Z (e.g., (0,0), (1,0), (0,1), ...).
    /// </summary>
    public void ShapeToCells(Vector2Int anchor, IReadOnlyList<Vector2Int> shapeOffsets, List<Vector2Int> outCells)
    {
        outCells.Clear();
        for (int i = 0; i < shapeOffsets.Count; i++)
        {
            var c = anchor + shapeOffsets[i];
            outCells.Add(c); 
        }
    }

    // ------------ BOUNDS & STATE QUERIES ------------

    public bool IsInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height;
    }

    public bool IsBlocked(Vector2Int cell)
    {
        if (!IsInside(cell)) return true;
        return blocked[cell.x, cell.y];
    }

    public void SetBlocked(Vector2Int cell, bool value)
    {
        if (!IsInside(cell)) return;
        blocked[cell.x, cell.y] = value;
    }

    public bool IsOccupied(Vector2Int cell)
    {
        if (!IsInside(cell)) return false;
        return occupancy[cell.x, cell.y] != 0;
    }

    public int GetOccupant(Vector2Int cell)
    {
        if (!IsInside(cell)) return -1;
        return occupancy[cell.x, cell.y];
    }

    public bool IsEmptyAndFree(Vector2Int cell)
    {
        return IsInside(cell) && !IsBlocked(cell) && !IsOccupied(cell);
    }



    /// <summary>
    /// 4-neighbors (up, down, left, right) inside the grid.
    /// </summary>
    public void GetNeighbors4(Vector2Int cell, List<Vector2Int> outNeighbors)
    {
        outNeighbors.Clear();
        var c = cell + Vector2Int.right;
        if (IsInside(c)) outNeighbors.Add(c);
        c = cell + Vector2Int.left;
        if (IsInside(c)) outNeighbors.Add(c);
        c = new Vector2Int(cell.x, cell.y + 1);
        if (IsInside(c)) outNeighbors.Add(c);
        c = new Vector2Int(cell.x, cell.y - 1);
        if (IsInside(c)) outNeighbors.Add(c);
    }

    // BoardGridXY.cs (inside the class)
    public void GetDistinctAdjacentOccupants(IReadOnlyList<Vector2Int> footprint, int excludePieceId, List<int> outOccIds)
    {
        outOccIds.Clear();
        var seen = new HashSet<int>();
        var neigh = new List<Vector2Int>();

        for (int i = 0; i < footprint.Count; i++)
        {
            GetNeighbors4(footprint[i], neigh);
            for (int j = 0; j < neigh.Count; j++)
            {
                var n = neigh[j];
                int occ = occupancy[n.x, n.y];  // safe: we're inside BoardGridXY
                if (occ == 0 || occ == excludePieceId) continue; // empty or same piece
                if (seen.Add(occ)) outOccIds.Add(occ);
            }
        }
    }


    // ------------ OCCUPANCY MUTATION ------------

    /// <summary>
    /// Marks these cells as occupied by a given pieceId.
    /// Assumes you've already validated they are placeable.
    /// </summary>
    public void OccupyCells(IReadOnlyList<Vector2Int> cells, int pieceId)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (IsInside(c))
                occupancy[c.x, c.y] = pieceId;
        }
    }

    /// <summary>
    /// Clears occupancy for these cells, but only if currently owned by the same pieceId (safety).
    /// </summary>
    public void ReleaseCellsOwnedBy(IReadOnlyList<Vector2Int> cells, int pieceId)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (IsInside(c) && occupancy[c.x, c.y] == pieceId)
                occupancy[c.x, c.y] = 0;
        }
    }

    // Allow placement while moving a piece: its own currently-occupied cells are OK.
    public bool AreCellsPlaceableForMover(IReadOnlyList<Vector2Int> cells, int moverPieceId)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (!IsInside(c)) return false;
            if (blocked[c.x, c.y]) return false;

            int occ = occupancy[c.x, c.y];
            if (occ != 0 && occ != moverPieceId) return false; // if other piece occupies it then result is false
        }
        return true;
    }

    /// <summary>
    /// Clamp an anchor so that ALL shape cells (anchor + offsets) stay inside the grid.
    /// </summary>
    public Vector2Int ClampAnchorToFitShape(Vector2Int anchor, IReadOnlyList<Vector2Int> shapeOffsets)
    {
        int minAllowedX = int.MinValue, maxAllowedX = int.MaxValue;
        int minAllowedY = int.MinValue, maxAllowedY = int.MaxValue;

        // For each offset (dx,dy): 0 <= anchor.x + dx <= width-1  =>  -dx <= anchor.x <= (width-1) - dx
        // Intersect all ranges.
        for (int i = 0; i < shapeOffsets.Count; i++)
        {
            int dx = shapeOffsets[i].x;
            int dy = shapeOffsets[i].y;

            minAllowedX = Mathf.Max(minAllowedX, -dx);
            maxAllowedX = Mathf.Min(maxAllowedX, (width - 1) - dx);

            minAllowedY = Mathf.Max(minAllowedY, -dy);
            maxAllowedY = Mathf.Min(maxAllowedY, (height - 1) - dy);
        }

        // (Paranoia) If the shape is bigger than the board, collapse to a safe cell
        if (minAllowedX > maxAllowedX) { int mid = Mathf.Clamp(width / 2, 0, width - 1); minAllowedX = maxAllowedX = mid; }
        if (minAllowedY > maxAllowedY) { int mid = Mathf.Clamp(height / 2, 0, height - 1); minAllowedY = maxAllowedY = mid; }

        int ax = Mathf.Clamp(anchor.x, minAllowedX, maxAllowedX);
        int ay = Mathf.Clamp(anchor.y, minAllowedY, maxAllowedY);
        return new Vector2Int(ax, ay);
    }


    // ------------ GIZMOS ------------
    private void OnDrawGizmos()
    {
        if (!drawGridGizmos) return;

        // --- (optional) OLD grid lines ---
        if (showGridLines)
        {
            Gizmos.color = gridLineColor;

            for (int x = 0; x <= width; x++)
            {
                float xLocal = OriginXLocal + x * StepX;
                Vector3 aLocal = new Vector3(OriginXLocal + x * StepX, OriginYLocal, localZ);
                Vector3 bLocal = new Vector3(OriginXLocal + x * StepX, OriginYLocal + height * StepY, localZ);
                Gizmos.DrawLine(transform.TransformPoint(aLocal), transform.TransformPoint(bLocal));
            }

            for (int y = 0; y <= height; y++)
            {
                float yLocal = OriginYLocal + y * StepY;
                Vector3 aLocal = new Vector3(OriginXLocal, yLocal, localZ);
                Vector3 bLocal = new Vector3(OriginXLocal + width * StepX, yLocal, localZ);
                Gizmos.DrawLine(transform.TransformPoint(aLocal), transform.TransformPoint(bLocal));
            }
        }

        // --- NEW per-cell outlines (keep this) ---
        if (showInsetCellOutlines)
        {
            Gizmos.color = Color.yellow;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Vector3 c = CellCenterWorld(cell);
                    float hx = cellSize * 0.5f, hy = cellSize * 0.5f;
                    Vector3 a = new Vector3(c.x - hx, c.y - hy, c.z);
                    Vector3 b = new Vector3(c.x + hx, c.y - hy, c.z);
                    Vector3 d = new Vector3(c.x - hx, c.y + hy, c.z);
                    Vector3 e = new Vector3(c.x + hx, c.y + hy, c.z);
                    Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, e);
                    Gizmos.DrawLine(e, d); Gizmos.DrawLine(d, a);
                }
        }

        // --- (optional) blocked/occupied overlays ---
        if (showOverlays && (blocked != null || occupancy != null))
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    if (blocked != null && blocked[x, y])
                        DrawCellFill(new Vector2Int(x, y), blockedFillColor);
                    else if (occupancy != null && occupancy[x, y] != 0)
                        DrawCellFill(new Vector2Int(x, y), occupiedFillColor);
                }
        }
    }


    private void DrawCellFill(Vector2Int cell, Color color)
    {
        // full tile size at the padded center  ✓
        Vector3 c = CellCenterWorld(cell);                                     // ✓
        float halfX = cellSize * 0.5f;                                         // ✓
        float halfY = cellSize * 0.5f;                                         // ✓

        Vector3 a = new Vector3(c.x - halfX, c.y - halfY, c.z);                // ✓
        Vector3 b = new Vector3(c.x + halfX, c.y - halfY, c.z);                // ✓
        Vector3 d = new Vector3(c.x - halfX, c.y + halfY, c.z);                // ✓
        Vector3 e = new Vector3(c.x + halfX, c.y + halfY, c.z);                // ✓

        Color prev = Gizmos.color;
        Gizmos.color = color;

        Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, e);                          // ✓
        Gizmos.DrawLine(e, d); Gizmos.DrawLine(d, a);                          // ✓
        Gizmos.DrawLine(a, e); Gizmos.DrawLine(b, d);                          // ✓

        Gizmos.color = prev;
    }




}
