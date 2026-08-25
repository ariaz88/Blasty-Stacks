using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// #define DOTWEEN_ENABLED
#if DOTWEEN_ENABLED
using DG.Tweening;
#endif

public class MatchResolver : MonoBehaviour
{
    public static System.Action<int> OnBlast;   // param = number of groups (here: 1)

    [SerializeField] private BoardGridXY board;

    [Header("Rules")]
    [SerializeField, Min(2)] private int minGroupSize = 3; // e.g., 3+ connected to resolve
    [SerializeField] private bool preferMergeIfImmovablePresent = true;

    [Header("FX")]
    [SerializeField] private float killAnimTime = 0.10f;   // anticipation beat, measured at ~100 ms
    [SerializeField] private bool enableDebug = false;

    private readonly List<Vector2Int> _footprint = new();
    private readonly HashSet<Vector2Int> _neighborCells = new();

    // Scratch buffers for the collapse beat, reused so a big clear allocates nothing.
    private readonly List<Transform> _collapseParts = new();
    private readonly List<Vector3> _collapseOrigins = new();

    // Cached once instead of a FindObjectOfType per piece per clear.
    private ShardBurst _burst;
    private FractureObject _legacyFracture;

    private void Awake()
    {
        if (!board) board = FindObjectOfType<BoardGridXY>();
    }




    public bool HasAnyStacksLeft()
    {
        // Make sure we have a board reference
        if (!board)
            board = FindObjectOfType<BoardGridXY>();

        if (!board)
            return false;

        return HasAnyActiveBlocksOnBoard();
    }

    /// <summary>
    /// Dynamically searches the scene for active PieceSimple that are currently inside the board area.
    /// </summary>
    private bool HasAnyActiveBlocksOnBoard()
    {
        // Find all PieceSimple in the scene (including inactive=false)
        PieceSimple[] allPieces = FindObjectsOfType<PieceSimple>(false);

        foreach (var piece in allPieces)
        {
            if (!piece)
                continue;

            // Only consider active in hierarchy
            if (!piece.gameObject.activeInHierarchy)
                continue;

            // Check if this piece is currently over a valid board cell
            if (board.TryWorldToCell(piece.transform.position, out Vector2Int cell))
            {
                // Optional: if you want to ignore blocked cells, you can add:
                // if (board.IsBlocked(cell)) continue;

                // If we reached here, this piece is inside the board grid
                return true;
            }
        }

        // No active pieces found inside the board
        return false;
    }



    // ---------------- Public entry points ----------------

    public void ResolveFrom(PieceSimple origin)
    {
        if (!origin) return;


        var group = FindConnectedIdenticalGroup(origin);
        if (enableDebug)
            Debug.Log($"[MatchResolver] ResolveFrom {origin.ShapeId}/{origin.ColorId} → group {group.Count}", origin);

        if (group.Count < minGroupSize) return;

        // Prefer merge if there is an immovable piece in the group (e.g., warrior)
        if (preferMergeIfImmovablePresent)
        {
            PieceSimple target = FindNearestWithWarriors(origin, group);
            if (target != null)
            {
                StartCoroutine(MergePieceInto(origin, target));
                return;
            }
        }

        // Blast the whole group
        StartCoroutine(ClearGroup(group));
    }

    public void ResolveAll()
    {
        var pieces = FindObjectsOfType<PieceSimple>(false);
        var visited = new HashSet<PieceSimple>();
        var groupsToClear = new List<List<PieceSimple>>();

        foreach (var p in pieces)
        {
            if (!p || visited.Contains(p)) continue;
            var g = FindConnectedIdenticalGroup(p);
            foreach (var m in g) visited.Add(m);
            if (g.Count >= minGroupSize) groupsToClear.Add(g);
        }

        if (groupsToClear.Count == 0) return;

        foreach (var g in groupsToClear)
            StartCoroutine(ClearGroup(g));
    }

    // ---------------- Core search ----------------

    private List<PieceSimple> FindConnectedIdenticalGroup(PieceSimple seed)
    {
        var result = new List<PieceSimple>();
        var q = new Queue<PieceSimple>();
        var seen = new HashSet<PieceSimple>();

        q.Enqueue(seed); seen.Add(seed);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            result.Add(cur);

            // Build the 4-neighbor cell set around cur's footprint
            // Use the cells the piece ACTUALLY holds, not anchor + offsets. A piece
            // resting between cells reserves every cell it overlaps, and looking at
            // the nominal footprint there would probe the wrong cells and miss matches.
            if (cur.OccupiedCells != null && cur.OccupiedCells.Count > 0)
                BuildCellNeighbors4(cur.OccupiedCells, _neighborCells);
            else
                BuildFootprintNeighbors4(cur.Anchor, cur.ShapeOffsets, _neighborCells);

            foreach (var nCell in _neighborCells)
            {
                if (!board.IsInside(nCell)) continue;

                int occ = board.GetOccupant(nCell);
                if (occ <= 0) continue;

                var piece = PieceSimple.GetById(occ);
                if (piece == null) continue;
                if (seen.Contains(piece)) continue;

                if (/*piece.ShapeId == seed.ShapeId &&*/ AreShapesMatchCompatible(piece.ShapeId, seed.ShapeId) && piece.ColorId == seed.ColorId)
                {
                    seen.Add(piece);
                    q.Enqueue(piece);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Fills 'outNeighbors' with all unique 4-adjacent cells that touch any cell of the given footprint.
    /// Includes occupied cells (so we can detect neighboring pieces).
    /// </summary>
    private void BuildFootprintNeighbors4(Vector2Int anchor, IReadOnlyList<Vector2Int> shape, HashSet<Vector2Int> outNeighbors)
    {
        _footprint.Clear();
        board.ShapeToCells(anchor, shape, _footprint);
        BuildCellNeighbors4(_footprint, outNeighbors);
    }

    /// <summary>
    /// Same as <see cref="BuildFootprintNeighbors4"/> but over an explicit set of
    /// cells, so it works for a piece resting between cells that holds more cells
    /// than its nominal shape.
    /// </summary>
    private void BuildCellNeighbors4(IReadOnlyList<Vector2Int> cells, HashSet<Vector2Int> outNeighbors)
    {
        outNeighbors.Clear();

        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            outNeighbors.Add(new Vector2Int(c.x + 1, c.y));
            outNeighbors.Add(new Vector2Int(c.x - 1, c.y));
            outNeighbors.Add(new Vector2Int(c.x, c.y + 1));
            outNeighbors.Add(new Vector2Int(c.x, c.y - 1));
        }

        // Remove the piece's own cells so we don't re-detect the same piece.
        // Must strip THESE cells, not _footprint - when called with a piece's real
        // occupied set, _footprint holds someone else's leftovers.
        for (int i = 0; i < cells.Count; i++)
            outNeighbors.Remove(cells[i]);
    }

    private PieceSimple FindNearestWithWarriors(PieceSimple origin, List<PieceSimple> group)
    {
        PieceSimple best = null;
        float bestD2 = float.PositiveInfinity;

        foreach (var p in group)
        {
            if (p == origin) continue;

            float d2 = (p.transform.position - origin.transform.position).sqrMagnitude;
            if (d2 < bestD2) { bestD2 = d2; best = p; }
        }
        return best;
    }

    // ---------------- Actions ----------------

    private IEnumerator ClearGroup(List<PieceSimple> group)
    {
        if (group == null || group.Count == 0) yield break;

        // Release cells first so new moves can happen while animating out
        foreach (var p in group)
        {
            if (!p) continue;
            p.ReleaseFromBoard();
        }

        OnBlast?.Invoke(1);

#if DOTWEEN_ENABLED
        foreach (var p in group)
        {
            if (!p) continue;
            var renderers = p.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                // fallback: scale only
                p.transform.DOScale(0f, killAnimTime).SetEase(Ease.InBack).OnComplete(() =>
                {
                    if (p) Destroy(p.gameObject);
                });
                continue;
            }

            foreach (var r in renderers)
                r.DOFade(0f, killAnimTime);

            p.transform.DOScale(0.85f, killAnimTime).SetEase(Ease.InBack).OnComplete(() =>
            {
                if (p) Destroy(p.gameObject);
            });
        }
        yield return new WaitForSeconds(killAnimTime);
#else
        foreach (var p in group)
        {
            if (!p) continue;
            StartCoroutine(ScaleDownAndExplode(p));
        }

        yield return new WaitForSeconds(killAnimTime);
#endif
    }
    private IEnumerator ScaleDownAndExplode(PieceSimple p)
    {
        yield return CollapseThenBurst(p);
    }

    /// <summary>
    /// The 100 ms anticipation beat, then the shard burst.
    ///
    /// Measured off Assets/Arts/Reference videos/Stack movement.mp4: the block does not
    /// shrink as one object. It splits into one cube per board cell and each collapses in
    /// place with a slight pull toward the group centre, over three frames at 30 fps.
    /// That is what reads as "it broke" instead of "it disappeared".
    /// </summary>
    private IEnumerator CollapseThenBurst(PieceSimple p)
    {
        if (!p) yield break;

        Transform tr = p.transform;
        int colorId = p.ColorId;

        Vector3 centre = FootprintCentreWorld(p);
        Vector2 footprint = FootprintCells(p);

        // Sample the block's own sprite NOW, while the piece still exists, so the shards
        // match the stack exactly rather than following a parallel colour table.
        Color? tint = PieceTintSampler.TryGetTint(p.gameObject, out Color sampled)
            ? sampled
            : (Color?)null;

        // Per-cell children if the piece has them, otherwise the root as a single cube.
        _collapseParts.Clear();
        _collapseOrigins.Clear();
        for (int i = 0; i < tr.childCount; i++)
        {
            var child = tr.GetChild(i);
            if (child && child.GetComponentInChildren<Renderer>(true) != null)
            {
                _collapseParts.Add(child);
                _collapseOrigins.Add(child.position);
            }
        }
        if (_collapseParts.Count == 0)
        {
            _collapseParts.Add(tr);
            _collapseOrigins.Add(tr.position);
        }

        var startScales = new Vector3[_collapseParts.Count];
        for (int i = 0; i < _collapseParts.Count; i++)
            startScales[i] = _collapseParts[i].localScale;

        float duration = Mathf.Max(0.01f, killAnimTime);
        float t = 0f;

        while (t < duration)
        {
            if (!p) break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = (1f - k) * (1f - k);   // ease-in collapse: 1 -> 0.6 -> 0.3 -> 0

            for (int i = 0; i < _collapseParts.Count; i++)
            {
                var part = _collapseParts[i];
                if (!part) continue;

                part.localScale = startScales[i] * s;
                // slight inward pull, 8% of the distance to the group centre
                part.position = Vector3.Lerp(_collapseOrigins[i], centre, k * 0.08f);
            }

            yield return null;
        }

        FireBurst(centre, footprint, colorId, tint);

        if (p) Destroy(p.gameObject);
    }

    /// <summary>Fires the shard burst, falling back to the legacy FractureObject if present.</summary>
    private void FireBurst(Vector3 centre, Vector2 footprintCells, int colorId, Color? tint = null)
    {
        if (!_burst) _burst = ShardBurst.Instance ? ShardBurst.Instance : FindObjectOfType<ShardBurst>();

        if (_burst)
        {
            _burst.Play(centre, footprintCells, colorId, tint);
            return;
        }

        if (!_legacyFracture) _legacyFracture = FindObjectOfType<FractureObject>();
        if (_legacyFracture) _legacyFracture.ExplodeAtPosition(centre, colorId);
    }

    private Vector3 FootprintCentreWorld(PieceSimple p)
    {
        var cells = (p.OccupiedCells != null && p.OccupiedCells.Count > 0)
            ? p.OccupiedCells
            : null;

        if (board != null && cells != null && cells.Count > 0)
        {
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < cells.Count; i++) sum += board.CellCenterWorld(cells[i]);
            var c = sum / cells.Count;
            c.z = 0f;
            return c;
        }

        var fallback = p.transform.position;
        fallback.z = 0f;
        return fallback;
    }

    private Vector2 FootprintCells(PieceSimple p)
    {
        var cells = (p.OccupiedCells != null && p.OccupiedCells.Count > 0)
            ? p.OccupiedCells
            : null;

        if (cells == null || cells.Count == 0) return Vector2.one;

        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;
        for (int i = 0; i < cells.Count; i++)
        {
            var c = cells[i];
            if (c.x < minX) minX = c.x;
            if (c.x > maxX) maxX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.y > maxY) maxY = c.y;
        }
        return new Vector2(maxX - minX + 1, maxY - minY + 1);
    }


    private IEnumerator MergePieceInto(PieceSimple mover, PieceSimple target)
    {
        if (!mover || !target) yield break;
        if (enableDebug) Debug.Log($"[MatchResolver] Merge {mover.name} → {target.name}");

        var path = new List<Vector2Int>();
        if (!TryComputeStraightPath(mover.Anchor, target.Anchor, mover.ShapeOffsets, path))
        {
            // If no simple straight path, just clear both as a fallback
            yield return ClearGroup(new List<PieceSimple> { mover, target });
            yield break;
        }

        foreach (var step in path)
        {
            mover.TryPlace(step);
            mover.transform.position = board.CellCenterWorld(step);
            yield return null; // one frame per step
        }

        target.ReleaseFromBoard();
        OnBlast?.Invoke(1);

#if DOTWEEN_ENABLED
        var renderersM = mover.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in renderersM) r.DOFade(0f, killAnimTime);
        mover.transform.DOScale(0.85f, killAnimTime).SetEase(Ease.InBack).OnComplete(() => { if (mover) Destroy(mover.gameObject); });

        var renderersT = target.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in renderersT) r.DOFade(0f, killAnimTime);
        target.transform.DOScale(0.85f, killAnimTime).SetEase(Ease.InBack).OnComplete(() => { if (target) Destroy(target.gameObject); });

        yield return new WaitForSeconds(killAnimTime);
#else
        StartCoroutine(FadeAndScaleDownThenDestroy(mover, killAnimTime));
        StartCoroutine(FadeAndScaleDownThenDestroy(target, killAnimTime));
        yield return new WaitForSeconds(killAnimTime);
#endif
    }

    private bool TryComputeStraightPath(Vector2Int from, Vector2Int to, IReadOnlyList<Vector2Int> shape, List<Vector2Int> outPath)
    {
        outPath.Clear();
        if (from == to) return true;

        Vector2Int cur = from;

        // Move along X
        int dx = Mathf.Abs(to.x - cur.x);
        int sx = (to.x > cur.x) ? 1 : -1;
        for (int i = 0; i < dx; i++)
        {
            var next = new Vector2Int(cur.x + sx, cur.y);
            if (!IsFootprintFreeAt(next, shape)) return false;
            outPath.Add(next);
            cur = next;
        }

        // Move along Y
        int dy = Mathf.Abs(to.y - cur.y);
        int sy = (to.y > cur.y) ? 1 : -1;
        for (int i = 0; i < dy; i++)
        {
            var next = new Vector2Int(cur.x, cur.y + sy);
            if (!IsFootprintFreeAt(next, shape)) return false;
            outPath.Add(next);
            cur = next;
        }

        return true;
    }

    private bool IsFootprintFreeAt(Vector2Int anchor, IReadOnlyList<Vector2Int> shape)
    {
        board.ShapeToCells(anchor, shape, _footprint);
        return board.AreCellsPlaceableForMover(_footprint, 0); // treat as empty mover
    }

    /// <summary>
    /// The match rule for shapes. Public and static so the tutorial layer can ask
    /// "would these two blast?" without duplicating (and drifting from) the rule.
    /// </summary>
    public static bool AreShapesMatchCompatible(string a, string b)
    {
        if (a == b) return true;

        // B6 <-> B7 are compatible
        if ((a == "B6" && b == "B7") || (a == "B7" && b == "B6"))
            return true;

        // B4 <-> B5 are compatible
        if ((a == "B4" && b == "B5") || (a == "B5" && b == "B4"))
            return true;

        return false;
    }


#if !DOTWEEN_ENABLED
    // The merge path is what runs for most matches (see MergePieceInto). It used to just
    // fade the two pieces to 0.85 scale and destroy them, so a merged match produced no
    // effect at all no matter how the fracture VFX was tuned. It now runs the same
    // collapse-then-burst beat as a plain group clear.
    private IEnumerator FadeAndScaleDownThenDestroy(PieceSimple p, float t)
    {
        yield return CollapseThenBurst(p);
    }
#endif


}
