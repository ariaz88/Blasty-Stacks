﻿﻿using System;
using System.Collections.Generic;
using UnityEngine;



[DisallowMultipleComponent]
public class PieceSimple : MonoBehaviour
{
    [Header("Padding (collider shrink only)")]
    [SerializeField, Min(0f)]
    private float paddingPerCell = 0f; // set how big the gap between neighbors should be

    [Header("Root Snapping")]
    [SerializeField]
    private bool snapRootXYToCellCenterOnPlace = false;   // default OFF

    // ===== Identity / registry =====
    private static int NextId = 1;
    private static readonly Dictionary<int, PieceSimple> Registry = new();
    [SerializeField/*, HideInInspector*/] private int pieceId;
    public int PieceId => pieceId;

    // ===== Movement constraints =====
    public enum MovementAxis { Both, HorizontalOnly, VerticalOnly }

    [Header("Movement")]
    [SerializeField] private MovementAxis movementAxis = MovementAxis.Both;
    [SerializeField] private bool isFrozen = false;
    public bool AllowsX => !isFrozen && movementAxis != MovementAxis.VerticalOnly;
    public bool AllowsY => !isFrozen && movementAxis != MovementAxis.HorizontalOnly;

    // ===== Appearance / type =====
    [Header("Identity")]
    [SerializeField] private PieceShapeLayout shapeLayout;
    [SerializeField] private string shapeId = "P";
    [SerializeField] private int colorId = 0;

    // ===== Shape footprint =====
    [Header("Shape (grid offsets)")]
    [Tooltip("Offsets (relative to anchor cell) that define occupied cells. Must include (0,0).")]
    [SerializeField] private List<Vector2Int> shapeOffsets = new() { Vector2Int.zero };
    [Tooltip("If true AND offsets look empty, build offsets from child colliders.")]
    [SerializeField] private bool autoBuildOffsetsFromChildren = true;

    // ===== Board refs =====
    [Header("Board & Placement")]
    [SerializeField] private BoardGridXY board;
    public Vector2Int InitialAnchor = Vector2Int.zero;

    // ===== Sub-block snapping =====
    [Header("Sub-Block Enforcement")]
    [Tooltip("If true, sub-block children will be set to (offset * cellSize) in local space on Start and after every placement.")]
    [SerializeField] private bool enforceSubBlocksToOffsets = true;

    // runtime
    private readonly List<Vector2Int> _footprint = new();
    private readonly List<Vector2Int> _lastOccupied = new();
    private bool _isPlaced;
    private Vector2Int _anchor;

    public string ShapeId => shapeId;
    public int ColorId => colorId;
    public Vector2Int Anchor => _anchor;
    public IReadOnlyList<Vector2Int> ShapeOffsets => shapeOffsets;

    // --- Add to PieceSimple.cs ---

    // Store a reference to the board this piece belongs to.
    // (If you already have a 'board' field, keep that one and just add the methods.)
    public BoardGridXY Board => board;

    /// <summary>Assign the XY board this piece should use.</summary>
    /// 

    public void SetBoard(BoardGridXY b)
    {
        board = b;
    }


    public static PieceSimple GetById(int id)
    {
        return Registry.TryGetValue(id, out var p) ? p : null;
    }
    private void Awake()
    {
        if (!board) board = FindObjectOfType<BoardGridXY>();
        // keep parent depth on board plane
        if (board) transform.position = board.SnapToPlane(transform.position);
    }

    private void Start()
    {
        // ===== FIX: ensure unique PieceId and registry =====
        if (pieceId == 0)
        {
            pieceId = NextId++;
            Registry[pieceId] = this;
        }

        if (!board) board = FindObjectOfType<BoardGridXY>();
        if (!board) return;

        if (!shapeLayout)
            shapeLayout = GetComponent<PieceShapeLayout>();

        if (shapeLayout)
            shapeLayout.ApplyLayout(this);

        // Ensure we are on the board plane before any math
        transform.position = board.SnapToPlane(transform.position);

        // If offsets were not authored, build from children once
        if (autoBuildOffsetsFromChildren && LooksEmpty(shapeOffsets))
            AutoBuildOffsetsFromChildren();

        // Solve anchor from children (authoritative). If fail, fallback to parent pos.
        if (!TrySolveAnchorFromChildren(out var anchor))
        {
            if (!board.TryWorldToCell(transform.position, out anchor))
                anchor = InitialAnchor;
        }

        // Clamp and place
        anchor = board.ClampAnchorToFitShape(anchor, shapeOffsets);
        TryPlace(anchor);
    }
    private void OnDestroy()
    {
        if (pieceId != 0 && Registry.TryGetValue(pieceId, out var self) && self == this)
            Registry.Remove(pieceId);
    }

    private float TargetColliderSize()
    {
        // If the board exposes InnerCellSize, use it; else fall back to full cell size.
        // (BoardGridXY in your project already has InnerCellSize.)
        float inner = board ? board.InnerCellSize : 0f;
        inner *= 0.97f; // 2% cushion

        float cell = board ? board.CellSize : 1f;

        // If you still want per-piece extra gap via paddingPerCell, keep the smaller:
        if (paddingPerCell > 0f)
            inner = Mathf.Min(inner, Mathf.Clamp(cell - paddingPerCell, 0.01f, cell));

        // Safety floor
        return Mathf.Max(0.01f, inner);
    }

    private static bool LooksEmpty(List<Vector2Int> list)
        => list == null || list.Count == 0 || (list.Count == 1 && list[0] == Vector2Int.zero);

    // ------------------------------------------------------------
    //  SUB-BLOCK CELL GATHERING / OFFSETS BUILDING / ANCHOR SOLVE
    // ------------------------------------------------------------

    /// <summary>Collects world-space cell positions for each child collider.</summary>
    private void CollectChildCells(HashSet<Vector2Int> outCells)
    {
        outCells.Clear();
        if (!board) return;

        float s = board.CellSize;
        // both 2D and 3D supported (though this board is XY)
        var cols2D = GetComponentsInChildren<Collider2D>(false);
        var cols3D = GetComponentsInChildren<Collider>(false);

        void Add(Vector3 world)
        {
            var onPlane = board.SnapToPlane(world);
            if (board.TryWorldToCell(onPlane, out var cell)) { outCells.Add(cell); return; }

            // rare fallback
            var local = board.transform.InverseTransformPoint(onPlane);
            var c = new Vector2Int(Mathf.FloorToInt(local.x / s), Mathf.FloorToInt(local.y / s));
            outCells.Add(c);
        }

        foreach (var c in cols2D) if (c && c.enabled) Add(c.bounds.center);
        foreach (var c in cols3D) if (c && c.enabled) Add(c.bounds.center);
    }

    /// <summary>Build shapeOffsets from children; chooses an anchor on the set so that (0,0) is included.</summary>
    public bool AutoBuildOffsetsFromChildren()
    {
        if (!board) return false;

        var cells = new HashSet<Vector2Int>();
        CollectChildCells(cells);
        if (cells.Count == 0) return false;

        // pick lowest-left cell as anchor
        Vector2Int anchor = new(int.MaxValue, int.MaxValue);
        foreach (var c in cells)
            if (c.y < anchor.y || (c.y == anchor.y && c.x < anchor.x)) anchor = c;

        var list = new List<Vector2Int>(cells.Count);
        foreach (var c in cells) list.Add(c - anchor);
        if (!list.Contains(Vector2Int.zero)) list.Add(Vector2Int.zero);
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

        shapeOffsets = list;
        _anchor = anchor;

        if (enforceSubBlocksToOffsets) SnapSubBlocksToOffsets();
        return true;
    }

    /// <summary>
    /// Tries to find the anchor A such that {A + offset} == set(childCells).
    /// If there is a match, returns true and outputs that A.
    /// </summary>
    public bool TrySolveAnchorFromChildren(out Vector2Int anchor)
    {
        anchor = default;
        if (!board || LooksEmpty(shapeOffsets)) return false;

        var cells = new HashSet<Vector2Int>();
        CollectChildCells(cells);
        if (cells.Count == 0) return false;

        foreach (var cc in cells)
        {
            for (int i = 0; i < shapeOffsets.Count; i++)
            {
                var candidate = cc - shapeOffsets[i];
                bool ok = true;
                for (int j = 0; j < shapeOffsets.Count; j++)
                {
                    if (!cells.Contains(candidate + shapeOffsets[j])) { ok = false; break; }
                }
                if (ok) { anchor = candidate; return true; }
            }
        }
        return false;
    }

    /// <summary>
    /// Force each sub-block child (with a Collider/Collider2D OR name starting with "Cell")
    /// to the exact local position that corresponds to its offset, and zero collider offsets.
    /// </summary>

    private static bool IsCellLike(Transform t)
    {
        if (t.name.StartsWith("Cell")) return true;
        if (t.GetComponent<Collider2D>()) return true;
        if (t.GetComponent<Collider>()) return true;
        return false;
    }
    private void SnapSubBlocksToOffsets()
    {
        if (!board) return;

        float s = board.CellSize;

        // 1) Build target local positions for each shape offset
        var targets = new List<(Vector2Int off, Vector3 localPos)>(shapeOffsets.Count);
        for (int i = 0; i < shapeOffsets.Count; i++)
        {
            var off = shapeOffsets[i];
            targets.Add((off, new Vector3(off.x * s, off.y * s, 0f)));
        }

        // 2) Collect candidate children (cell-like)
        var children = new List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t == transform) continue;
            if (IsCellLike(t)) children.Add(t);
        }
        if (children.Count == 0) return;

        // 3) If counts mismatch, don’t try to move things—just normalize colliders
        if (children.Count != targets.Count)
        {
            NormalizeCollidersOnly(children, s);
            return;
        }

        // 4) Try to bind children to offsets using names first; fall back to nearest target
        var assigned = new HashSet<int>(); // target indices taken
        var childToTarget = new int[children.Count];
        for (int i = 0; i < childToTarget.Length; i++) childToTarget[i] = -1;

        // 4a) Name-based binding
        for (int i = 0; i < children.Count; i++)
        {
            var t = children[i];
            if (TryGetOffsetFromName(t.name, out var want))
            {
                int k = targets.FindIndex(tg => tg.off == want && !assigned.Contains(tg.GetHashCode()));
                if (k >= 0)
                {
                    childToTarget[i] = k;
                    assigned.Add(targets[k].GetHashCode());
                }
            }
        }

        // 4b) Nearest-target binding for any unbound child (greedy)
        for (int i = 0; i < children.Count; i++)
        {
            if (childToTarget[i] != -1) continue;

            var t = children[i];
            var lp = t.localPosition;
            float best = float.PositiveInfinity;
            int bestIdx = -1;
            for (int k = 0; k < targets.Count; k++)
            {
                if (assigned.Contains(targets[k].GetHashCode())) continue;
                float d = (lp - targets[k].localPos).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    bestIdx = k;
                }
            }
            if (bestIdx != -1)
            {
                childToTarget[i] = bestIdx;
                assigned.Add(targets[bestIdx].GetHashCode());
            }
        }

        // 5) Move each child to its matched target and normalize colliders
        for (int i = 0; i < children.Count; i++)
        {
            var t = children[i];
            int k = childToTarget[i];
            if (k < 0) continue; // should not happen


            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            t.localPosition = new Vector3(targets[k].localPos.x, targets[k].localPos.y, t.localPosition.z);

            // ---- CHANGE: size to the board's inner cell size
            float inner = TargetColliderSize();

            if (t.TryGetComponent<BoxCollider2D>(out var b2d))
            {
                b2d.offset = Vector2.zero;
                b2d.size = new Vector2(inner, inner);
            }
            else if (t.TryGetComponent<BoxCollider>(out var b3d))
            {
                b3d.center = Vector3.zero;
                // thickness for 3D colliders; if you keep any 3D, use a small Z so it doesn't stick out
                b3d.size = new Vector3(inner, inner, inner);
            }

        }
    }
    /// <summary>
    /// Do NOT move sub-blocks. Keep their centers on the cell centers.
    /// Only resize colliders so that (collider size + padding) == cell size.
    /// This creates a gap = padding between adjacent blocks without any offset.
    /// </summary>
    private void SnapSubBlocksToOffsets2()
    {
        if (!board) return;

        float cell = board.CellSize;
        // Size that each collider should have so: colliderSize + padding == cellSize
        float inner = Mathf.Clamp(cell - paddingPerCell, 0.01f, cell);

        // Touch only colliders; do NOT change child positions at all.
        var children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            var t = children[i];
            if (t == transform) continue;

            // 2D colliders
            if (t.TryGetComponent<BoxCollider2D>(out var box2D))
            {
                box2D.offset = Vector2.zero;               // keep centered on the child's origin (cell center)
                box2D.size = new Vector2(inner, inner);  // shrink to create the gap
            }
            else if (t.TryGetComponent<CircleCollider2D>(out var circ2D))
            {
                circ2D.offset = Vector2.zero;
                circ2D.radius = inner * 0.5f;
            }
            else if (t.TryGetComponent<CapsuleCollider2D>(out var cap2D))
            {
                cap2D.offset = Vector2.zero;
                cap2D.size = new Vector2(inner, inner);
            }
            // (Polygon/Edge 2D left unchanged — they don’t have a simple “size”.)

            // 3D colliders (in case some sub-blocks still have them)
            if (t.TryGetComponent<BoxCollider>(out var box3D))
            {
                box3D.center = Vector3.zero;
                box3D.size = new Vector3(inner, inner, inner);
            }
            // (Sphere/Capsule 3D not handled here; add if you use them.)
        }
    }


    private void NormalizeCollidersOnly(List<Transform> children, float s)
    {
        foreach (var t in children)
        {
            if (t.TryGetComponent<BoxCollider2D>(out var b2d))
            {
                b2d.offset = Vector2.zero;
                b2d.size = Vector2.one * s;
            }
            else if (t.TryGetComponent<BoxCollider>(out var b3d))
            {
                b3d.center = Vector3.zero;
                b3d.size = new Vector3(s, s, s);
            }
        }
    }

    // Accepts: "Cell_1_0", "Cell_1.0", "Cell(1,0)", and also "cell1_0" variants.
    private static bool TryGetOffsetFromName(string name, out Vector2Int off)
    {
        off = default;
        if (string.IsNullOrEmpty(name)) return false;

        string n = name.Trim().ToLowerInvariant();
        if (!n.StartsWith("cell")) return false;

        // Normalize separators to underscore
        n = n.Replace('(', '_').Replace(')', '_').Replace(',', '_').Replace('.', '_');

        // Now look for two trailing ints split by underscores
        // eg: "cell__-1__2" → we take the last two int chunks
        var parts = n.Split('_');
        var ints = new System.Collections.Generic.List<int>(2);
        foreach (var p in parts)
            if (int.TryParse(p, out int v)) ints.Add(v);

        if (ints.Count >= 2)
        {
            off = new Vector2Int(ints[ints.Count - 2], ints[ints.Count - 1]);
            return true;
        }
        return false;
    }


    private static Vector2Int GuessOffsetFromLocal(Vector3 localPos, float s)
    {
        int ox = Mathf.RoundToInt(localPos.x / Mathf.Max(0.0001f, s));
        int oy = Mathf.RoundToInt(localPos.y / Mathf.Max(0.0001f, s));
        return new Vector2Int(ox, oy);
    }

    // ------------------------------------------------------------
    //                 PLACEMENT / BOARD OCCUPANCY
    // ------------------------------------------------------------

    private void BuildFootprint(Vector2Int anchor, List<Vector2Int> outCells)
    {
        board.ShapeToCells(anchor, shapeOffsets, outCells);
    }

    /// <summary>
    /// Place (or slide) the piece onto the board.
    /// - Validates collision using board occupancy.
    /// - Sets the root to the exact cell center (world XY + board Z).
    /// - Optionally enforces sub-block local positions to match offsets.
    /// </summary>
    public bool TryPlace(Vector2Int anchor)
    {
        if (!board) return false;

        // axis lock
        if (_isPlaced)
        {
            var dx = anchor.x - _anchor.x;
            var dy = anchor.y - _anchor.y;
            if (dx != 0 && !AllowsX) return false;
            if (dy != 0 && !AllowsY) return false;
        }

        BuildFootprint(anchor, _footprint);
        if (!board.AreCellsPlaceableForMover(_footprint, pieceId)) return false;

        if (_isPlaced) board.ReleaseCellsOwnedBy(_lastOccupied, pieceId);
        board.OccupyCells(_footprint, pieceId);

        _lastOccupied.Clear(); _lastOccupied.AddRange(_footprint);
        _anchor = anchor;
        _isPlaced = true;

        // root to center (on plane)
        transform.position = board.CellCenterWorld(_anchor);

        // make children sit exactly on their offsets if requested
        if (enforceSubBlocksToOffsets) SnapSubBlocksToOffsets();

        return true;
    }

    public void ReleaseFromBoard()
    {
        if (!board || !_isPlaced) return;
        board.ReleaseCellsOwnedBy(_lastOccupied, pieceId);
        _lastOccupied.Clear();
        _isPlaced = false;
    }
}
