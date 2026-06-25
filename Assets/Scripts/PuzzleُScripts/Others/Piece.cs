using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
[ExecuteAlways]
public class Piece : MonoBehaviour
{
    // -------------------- CONFIG / IDENTITY --------------------

    [Header("Identity (for matching)")]
    [Tooltip("Unique runtime ID assigned by spawner/level builder.")]
    public int pieceId = 0;

    [Tooltip("Shape ID used for matching (e.g., I3_H, L3_NE, T4, etc.).")]
    public string shapeId = "L3";

    [Tooltip("Color key used for matching (e.g., 0=Red,1=Blue...). You can swap to an enum later.")]
    public int colorId = 0;

    [Header("Grid / Units")]
    [Tooltip("Must match BoardGridXZ.CellSize. 1 by default.")]
    [Min(0.01f)]
    public float cellSize = 1f;

    [Tooltip("If enabled, computed offsets are normalized so min X/Z becomes (0,0). Does not move the visuals.")]
    public bool normalizeOffsets = true;

    // -------------------- LAYERING (OPTIONAL) --------------------

    [Header("Layered piece (optional)")]
    [Tooltip("If true, the piece has an OUTER layer that clears first, then an INNER layer.")]
    public bool layered = false;

    [Tooltip("Parent transform holding UnitCells for the OUTER layer. If null, OUTER = UnitCells directly under this root.")]
    public Transform layerOuter;

    [Tooltip("Parent transform holding UnitCells for the INNER layer. If null and 'layered' is true, inner layer is absent.")]
    public Transform layerInner;

    [Tooltip("Active layer at start (Outer clears first).")]
    public ActiveLayer startActiveLayer = ActiveLayer.Outer;

    [System.Serializable]
    public enum ActiveLayer { Outer, Inner }

    [Header("Layer IDs (for matching while that layer is active)")]
    public string outerShapeId = "";
    public int outerColorId = 0;
    public string innerShapeId = "";
    public int innerColorId = 0;

    // -------------------- RUNTIME (computed) --------------------

    // Raw offsets read from children (may be negative if authoring is off-grid)
    private readonly List<Vector2Int> _outerRaw = new List<Vector2Int>();
    private readonly List<Vector2Int> _innerRaw = new List<Vector2Int>();

    // Normalized offsets (shifted so min becomes 0,0 if normalizeOffsets = true)
    private readonly List<Vector2Int> _outer = new List<Vector2Int>();
    private readonly List<Vector2Int> _inner = new List<Vector2Int>();
    private readonly List<Vector2Int> _union = new List<Vector2Int>();

    // Active layer at runtime
    [SerializeField] private ActiveLayer _activeLayer;

    // Scratch buffers (to avoid GC)
    private readonly List<Vector2Int> _scratch = new List<Vector2Int>();

    // -------------------- LIFECYCLE --------------------

    private void Awake()
    {
        RebuildFootprints();
        _activeLayer = layered ? startActiveLayer : ActiveLayer.Outer;
        ApplyLayerVisibility(); // editor-time feedback if you toggle start layer
    }

    private void OnValidate()
    {
        // Keep layer IDs sensible by default
        if (string.IsNullOrEmpty(outerShapeId)) outerShapeId = shapeId;
        if (layered && string.IsNullOrEmpty(innerShapeId)) innerShapeId = shapeId;

        RebuildFootprints();
        if (!Application.isPlaying)
        {
            _activeLayer = layered ? startActiveLayer : ActiveLayer.Outer;
            ApplyLayerVisibility();
        }
    }

    // -------------------- PUBLIC: CORE QUERIES --------------------

    /// <summary>
    /// Returns the currently active matching key (shapeId, colorId) according to the active layer.
    /// If not layered, returns (shapeId, colorId).
    /// </summary>
    public (string shape, int color) GetActiveMatchKey()
    {
        if (!layered)
            return (shapeId, colorId);

        return _activeLayer == ActiveLayer.Outer
            ? (string.IsNullOrEmpty(outerShapeId) ? shapeId : outerShapeId, outerColorId)
            : (string.IsNullOrEmpty(innerShapeId) ? shapeId : innerShapeId, innerColorId);
    }

    /// <summary>
    /// Get the footprint offsets for the ACTIVE layer (normalized if enabled).
    /// </summary>
    public IReadOnlyList<Vector2Int> GetActiveOffsets()
    {
        if (!layered) return _outer; // single-layer uses _outer
        return _activeLayer == ActiveLayer.Outer ? _outer : _inner;
    }

    /// <summary>
    /// Get the union of all layer offsets (normalized if enabled).
    /// Useful for occupancy (cells covered by any layer).
    /// </summary>
    public IReadOnlyList<Vector2Int> GetAllOffsets()
    {
        return _union;
    }

    /// <summary>
    /// Convert an anchor cell (board coords) + active offsets into ABSOLUTE board cells.
    /// Writes into 'buffer' and returns it (for convenience).
    /// </summary>
    public List<Vector2Int> GetActiveCellsAtAnchor(Vector2Int anchor, List<Vector2Int> buffer)
    {
        var src = GetActiveOffsets();
        return OffsetsToCells(anchor, src, buffer);
    }

    /// <summary>
    /// Convert an anchor + ALL-layer offsets into ABSOLUTE cells (union).
    /// Use for occupancy reservation (a layered piece occupies union of its layers).
    /// </summary>
    public List<Vector2Int> GetAllCellsAtAnchor(Vector2Int anchor, List<Vector2Int> buffer)
    {
        return OffsetsToCells(anchor, _union, buffer);
    }

    /// <summary>
    /// True if the piece still has any offsets on the active layer.
    /// (If false after a clear, the piece is effectively finished and can be destroyed.)
    /// </summary>
    public bool HasAnyActiveCells()
    {
        var src = GetActiveOffsets();
        return src != null && src.Count > 0;
    }

    // -------------------- PUBLIC: LAYER STATE --------------------

    /// <summary>
    /// Called when a match blasts the active layer.
    /// - If Outer was active and an Inner exists, switch to Inner.
    /// - If no more layers remain, the caller should destroy the Piece.
    /// </summary>
    public void ConsumeActiveLayer()
    {
        if (!layered)
        {
            // Single-layer: nothing left after consuming
            _outer.Clear();
            _union.Clear();
            ApplyLayerVisibility();
            return;
        }

        if (_activeLayer == ActiveLayer.Outer)
        {
            // Remove outer from union; switch to Inner (if any)
            if (_outer.Count > 0)
            {
                RemoveFromUnion(_outer);
                _outer.Clear();
            }
            _activeLayer = (_inner.Count > 0) ? ActiveLayer.Inner : ActiveLayer.Inner; // will show empty if none
        }
        else // Inner
        {
            if (_inner.Count > 0)
            {
                RemoveFromUnion(_inner);
                _inner.Clear();
            }
        }

        ApplyLayerVisibility();
    }

    // -------------------- INTERNAL: FOOTPRINT BUILD --------------------

    /// <summary>
    /// Re-scan children and recompute raw + normalized offsets for outer/inner/union.
    /// </summary>
    public void RebuildFootprints()
    {
        _outerRaw.Clear();
        _innerRaw.Clear();

        if (layered)
        {
            // OUTER from layerOuter (or direct children if null)
            if (layerOuter != null)
                ReadOffsetsFromChildren(layerOuter, _outerRaw);
            else
                ReadOffsetsFromChildren(this.transform, _outerRaw);

            // INNER from layerInner (if any)
            if (layerInner != null)
                ReadOffsetsFromChildren(layerInner, _innerRaw);
        }
        else
        {
            // Single-layer: everything under root is "outer"
            ReadOffsetsFromChildren(this.transform, _outerRaw);
        }

        // Normalize if requested (shift so min x/z = 0)
        NormalizeInto(_outerRaw, _outer);
        NormalizeInto(_innerRaw, _inner);

        // Build union (unique cells) from both
        _union.Clear();
        AppendUnique(_union, _outer);
        AppendUnique(_union, _inner);
    }

    private void ReadOffsetsFromChildren(Transform parent, List<Vector2Int> outList)
    {
        outList.Clear();
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            // Only count active visual UnitCells (ignore containers or disabled visuals)
            if (!child.gameObject.activeInHierarchy) continue;

            // Local position in units -> translate to grid steps
            Vector3 p = child.localPosition;
            int gx = Mathf.RoundToInt(p.x / Mathf.Max(cellSize, 0.0001f));
            int gz = Mathf.RoundToInt(p.z / Mathf.Max(cellSize, 0.0001f));

            var cell = new Vector2Int(gx, gz);
            if (!outList.Contains(cell))
                outList.Add(cell);
        }
    }

    private void NormalizeInto(List<Vector2Int> src, List<Vector2Int> dst)
    {
        dst.Clear();
        if (src.Count == 0)
            return;

        if (!normalizeOffsets)
        {
            for (int i = 0; i < src.Count; i++)
                dst.Add(src[i]);
            return;
        }

        // Find minimums
        int minX = src[0].x, minZ = src[0].y;
        for (int i = 1; i < src.Count; i++)
        {
            var c = src[i];
            if (c.x < minX) minX = c.x;
            if (c.y < minZ) minZ = c.y;
        }

        // Shift so minX/minZ becomes 0,0 (does NOT move visuals)
        for (int i = 0; i < src.Count; i++)
        {
            var c = src[i];
            dst.Add(new Vector2Int(c.x - minX, c.y - minZ));
        }
    }

    private void AppendUnique(List<Vector2Int> dst, List<Vector2Int> src)
    {
        for (int i = 0; i < src.Count; i++)
        {
            var c = src[i];
            bool exists = false;
            for (int j = 0; j < dst.Count; j++)
            {
                if (dst[j].x == c.x && dst[j].y == c.y)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
                dst.Add(c);
        }
    }

    private List<Vector2Int> OffsetsToCells(Vector2Int anchor, IReadOnlyList<Vector2Int> offsets, List<Vector2Int> buffer)
    {
        buffer ??= new List<Vector2Int>();
        buffer.Clear();
        for (int i = 0; i < offsets.Count; i++)
        {
            buffer.Add(anchor + offsets[i]);
        }
        return buffer;
    }

    private void RemoveFromUnion(List<Vector2Int> layer)
    {
        // Remove any cells in 'layer' from the union
        for (int i = _union.Count - 1; i >= 0; i--)
        {
            var u = _union[i];
            bool found = false;
            for (int j = 0; j < layer.Count; j++)
            {
                if (u.x == layer[j].x && u.y == layer[j].y)
                {
                    found = true;
                    break;
                }
            }
            if (found)
                _union.RemoveAt(i);
        }
    }

    // -------------------- VISUAL FEEDBACK (EDITOR) --------------------

    private void ApplyLayerVisibility()
    {
        // Editor-only nicety: if you provided dedicated layer parents, toggle them to show active state
        if (layered)
        {
            if (layerOuter != null)
                layerOuter.gameObject.SetActive(_outer.Count > 0 && _activeLayer == ActiveLayer.Outer);

            if (layerInner != null)
                layerInner.gameObject.SetActive(_inner.Count > 0 && _activeLayer == ActiveLayer.Inner);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw normalized footprint squares around the root for quick sanity check
        var prev = Gizmos.color;
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.35f);
        DrawCellsGizmo(_outer);
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
        DrawCellsGizmo(_inner);
        Gizmos.color = prev;
    }

    private void DrawCellsGizmo(List<Vector2Int> cells)
    {
        float s = cellSize * 0.5f;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            // world center of this normalized offset relative to the root:
            Vector3 center = transform.TransformPoint(new Vector3(c.x * cellSize, 0f, c.y * cellSize));
            Vector3 a = center + new Vector3(-s, 0f, -s);
            Vector3 b = center + new Vector3( s, 0f, -s);
            Vector3 d = center + new Vector3(-s, 0f,  s);
            Vector3 e = center + new Vector3( s, 0f,  s);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, e);
            Gizmos.DrawLine(e, d);
            Gizmos.DrawLine(d, a);
        }
    }
#endif
}
