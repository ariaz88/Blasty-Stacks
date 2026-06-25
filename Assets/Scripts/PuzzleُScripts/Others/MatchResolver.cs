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
    [SerializeField] private float killAnimTime = 0.15f;
    [SerializeField] private bool enableDebug = false;

    private readonly List<Vector2Int> _footprint = new();
    private readonly HashSet<Vector2Int> _neighborCells = new();

    private void Awake()
    {
        if (!board) board = FindObjectOfType<BoardGridXY>();
    }

    // ---------------- Available Blocks COunt ----------------

    //public bool HasAnyStacksLeft()
    //{
    //    return board != null && board.HasAnyOccupiedCells();
    //}

    // ---------------- Available Blocks Count ----------------

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
        outNeighbors.Clear();
        _footprint.Clear();
        board.ShapeToCells(anchor, shape, _footprint);

        for (int i = 0; i < _footprint.Count; i++)
        {
            var c = _footprint[i];
            outNeighbors.Add(new Vector2Int(c.x + 1, c.y));
            outNeighbors.Add(new Vector2Int(c.x - 1, c.y));
            outNeighbors.Add(new Vector2Int(c.x, c.y + 1));
            outNeighbors.Add(new Vector2Int(c.x, c.y - 1));
        }

        // Remove the footprint’s own cells so we don't re-detect the same piece
        for (int i = 0; i < _footprint.Count; i++)
            outNeighbors.Remove(_footprint[i]);
    }

    private PieceSimple FindNearestWithWarriors(PieceSimple origin, List<PieceSimple> group)
    {
        PieceSimple best = null;
        float bestD2 = float.PositiveInfinity;

        foreach (var p in group)
        {
            if (p == origin) continue;
            // If you have a HasWarriors property, uncomment the filter:
            // if (!p.HasWarriors) continue;

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
        // No DOTween? Do a simple fade+scale in a coroutine.
        //foreach (var p in group)
        //    StartCoroutine(FadeAndScaleDownThenDestroy(p, killAnimTime));
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
        if (!p) yield break;

        Transform tr = p.transform;
        Vector3 startScale = tr.localScale;
        Vector3 targetScale = startScale * 0.05f;   // 1/3 scale

        float t = 0f;
        float duration = killAnimTime;   // use same variable
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            tr.localScale = Vector3.Lerp(startScale, targetScale, k);
            yield return null;
        }

        // ----------------------------------------------------
        // CALL MANAGER-BASED EXPLOSION (correct version)
        // ----------------------------------------------------
        FractureObject manager = FindObjectOfType<FractureObject>();   // manager in scene
        if (manager != null)
        {
            manager.Explode(p.transform, p.ColorId);
        }

        // ----------------------------------------------------
        // Destroy original piece AFTER triggering explosion
        // ----------------------------------------------------
        Destroy(p.gameObject , 0.05f);
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

    private bool AreShapesMatchCompatible(string a, string b)
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
    private IEnumerator FadeAndScaleDownThenDestroy(PieceSimple p, float t)
    {
        if (!p) yield break;

        var srs = p.GetComponentsInChildren<SpriteRenderer>(true);
        var startCols = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++) startCols[i] = srs[i].color;

        var tr = p.transform;
        Vector3 startScale = tr.localScale;

        float e = 0f;
        while (e < t)
        {
            e += Time.deltaTime;
            float k = Mathf.Clamp01(e / t);

            // fade
            for (int i = 0; i < srs.Length; i++)
            {
                if (!srs[i]) continue;
                var c = startCols[i];
                c.a = Mathf.Lerp(c.a, 0f, k);
                srs[i].color = c;
            }

            // scale
            tr.localScale = Vector3.Lerp(startScale, startScale * 0.85f, k);

            yield return null;
        }

        if (p) Destroy(p.gameObject);
    }
#endif


}

//public class MatchResolver1 : MonoBehaviour
//{
//    // ===== [ADDED] global blast event (emits once per resolved group) =====
//    public static System.Action<int> OnBlast;   // param = number of groups (here: 1)


//    [SerializeField] private BoardGridXY board;

//    [Header("Matching")]
//    [Tooltip("If ON, pieces must have same shapeId AND colorId to match. If OFF, shapeId alone is enough.")]
//    [SerializeField] private bool matchByColor = true;
//    [SerializeField, Min(2)] private int minGroupSize = 2;

//    [Header("Timing")]
//    [SerializeField] private float resolveDelay = 0.12f;
//    [SerializeField] private float killAnimTime = 0.22f;

//    [Header("Debug")]
//    [SerializeField] private bool enableDebug = true;

//    private void Awake()
//    {
//        if (!board) board = FindObjectOfType<BoardGridXY>();
//    }

//    /// Call this right after a piece is dropped/placed.
//    public void ResolveFrom1(PieceSimple origin)
//    {
//        if (!origin || origin.IsClearing) return;

//        var group = FindConnectedIdenticalGroup(origin);
//        if (enableDebug)
//            Debug.Log($"[MatchResolver] ResolveFrom {origin.ShapeId}/{origin.ColorId} → group {group.Count}", origin);

//        if (group.Count >= minGroupSize)
//            StartCoroutine(ClearGroup(group));
//    }

//    public void ResolveFrom(PieceSimple origin)
//    {
//        if (!origin || origin.IsClearing) return;

//        var group = FindConnectedIdenticalGroup(origin);
//        if (enableDebug)
//            Debug.Log($"[MatchResolver] ResolveFrom {origin.ShapeId}/{origin.ColorId} → group {group.Count}", origin);

//        if (group.Count < minGroupSize) return;

//        // Prefer merge if there is an immovable (warrior) piece in the group
//        PieceSimple target = null;
//        float best = float.PositiveInfinity;

//        foreach (var p in group)
//        {
//            if (p == origin) continue;
//            if (!p.HasWarriors) continue;
//            float d2 = (p.transform.position - origin.transform.position).sqrMagnitude;
//            if (d2 < best) { best = d2; target = p; }
//        }

//        if (target != null)
//        {
//            StartCoroutine(MergePieceInto(origin, target));
//            return; // do NOT blast
//        }

//        // Fallback: your normal clear
//        StartCoroutine(ClearGroup(group));
//    }
//    private IEnumerator MergePieceInto(PieceSimple mover, PieceSimple target)
//    {
//        if (enableDebug) Debug.Log($"[MatchResolver] Merge {mover.PieceId} → {target.PieceId}");

//        //// Lock both from any concurrent interaction
//        mover.MarkClearing(true);
//        target.MarkClearing(true);

//        // Free the mover’s old cells immediately (so the path it came from opens up)
//        mover.ReleaseFromBoard();   // frees occupancy, also clears frozen mask if needed

//        // Disable mover’s colliders while it slides
//        foreach (var c in mover.GetComponentsInChildren<Collider>()) c.enabled = false;

//        // Slide mover to the target’s world position (target keeps its occupancy until the end)
//        Vector3 a = mover.transform.position;
//        Vector3 b = target.transform.position;
//        const float slideTime = 0.4f;
//        float t = 0f;
//        while (t < slideTime)
//        {
//            t += Time.deltaTime;
//            float k = Mathf.Clamp01(t / slideTime);
//            mover.transform.position = Vector3.Lerp(a, b, k);
//            yield return null;
//        }

//        // Now free the target’s cells too, so the board is fully open after the combine
//        target.ReleaseFromBoard();  // frees occupancy, clears any frozen mask


//        // after you finish the slide and before/after destroying the pieces:
//        MatchResolver.OnBlast?.Invoke(1);   // one resolved group => one blast tick

//        //DetachWarriors(mover);   // <<< in case the mover had warriors (future-proof)
//        //DetachWarriors(target);

//        if (target) target.DetachAllWarriors(); // target had the “immovable/warrior” preference


//        // Disable target colliders before kill
//        foreach (var c in target.GetComponentsInChildren<Collider>()) c.enabled = false;

//        // Optional: tiny disappear animation using your existing helper
//#if DOTWEEN_ENABLED
//    mover.transform.DOScale(0f, killAnimTime).SetEase(Ease.InBack).OnComplete(() => Destroy(mover.gameObject));
//    target.transform.DOScale(0f, killAnimTime).SetEase(Ease.InBack).OnComplete(() => Destroy(target.gameObject));
//#else
//        StartCoroutine(ScaleDownAndDestroy(mover.gameObject, killAnimTime));
//        StartCoroutine(ScaleDownAndDestroy(target.gameObject, killAnimTime));
//#endif
//    }





//    /// Optional: sweep the whole board (useful while debugging)
//    public void ResolveAll()
//    {
//        var pieces = FindObjectsOfType<PieceSimple>(false);
//        var visited = new HashSet<PieceSimple>();
//        var anyCleared = false;

//        foreach (var p in pieces)
//        {
//            if (!p || p.IsClearing || visited.Contains(p)) continue;

//            var group = FindConnectedIdenticalGroup(p);
//            foreach (var g in group) visited.Add(g);

//            if (enableDebug)
//                Debug.Log($"[MatchResolver] Sweep group {p.ShapeId}/{p.ColorId} size {group.Count}", p);

//            if (group.Count >= minGroupSize)
//            {
//                StartCoroutine(ClearGroup(group));
//                anyCleared = true;
//            }
//        }

//        if (enableDebug && !anyCleared)
//            Debug.Log("[MatchResolver] Sweep found no clearable groups.");
//    }

//    // -------- internals --------

//    private bool IsSameType(PieceSimple a, PieceSimple b)
//    {
//        if (a.ShapeId != b.ShapeId) return false;
//        return !matchByColor || a.ColorId == b.ColorId;
//    }



//    // BFS over edge-adjacent identical pieces
//    private HashSet<PieceSimple> FindConnectedIdenticalGroup(PieceSimple start)
//    {
//        var result = new HashSet<PieceSimple> { start };
//        var q = new Queue<PieceSimple>();
//        q.Enqueue(start);

//        while (q.Count > 0)
//        {
//            var p = q.Dequeue();
//            foreach (var n in GetEdgeAdjacentPieces(p))
//            {
//                if (!n || n.IsClearing) continue;
//                if (!IsSameType(p, n)) continue;
//                if (result.Add(n))
//                    q.Enqueue(n);
//            }
//        }
//        return result;
//    }

  
//    // MatchResolver.cs (inside the class)
//    private readonly List<int> _adjOccIds = new();

//    private IEnumerable<PieceSimple> GetEdgeAdjacentPieces(PieceSimple p)
//    {
//        var result = new List<PieceSimple>();
//        board.GetDistinctAdjacentOccupants(p.CurrentFootprint, p.PieceId, _adjOccIds);

//        for (int i = 0; i < _adjOccIds.Count; i++)
//        {
//            var other = PieceSimple.GetById(_adjOccIds[i]);
//            if (other != null) result.Add(other);
//        }
//        return result;
//    }


//    // *********************************************************
//    // *****  MAIN CODE THAT  MAKE THE PIECES MATCH ******

//    private IEnumerator ClearGroup(HashSet<PieceSimple> group)
//    {
//        foreach (var piece in group) piece.MarkClearing(true);
//        if (enableDebug) Debug.Log($"[MatchResolver] Clearing group of {group.Count}");

//        // ===== [ADDED] notify a blast happened (once per group) =====
//        OnBlast?.Invoke(1);
//        // ===== [END ADDED] =====

//        yield return new WaitForSeconds(resolveDelay);

//        // release occupancy first
//        foreach (var piece in group)
//            piece.ReleaseFromBoard();

//        // animate & destroy
//        foreach (var piece in group)
//        {
//            var go = piece.gameObject;
//            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;

//#if DOTWEEN_ENABLED
//            go.transform.DOScale(0f, killAnimTime).SetEase(Ease.InBack)
//                .OnComplete(() => Destroy(go));
//#else
//            StartCoroutine(ScaleDownAndDestroy(go, killAnimTime));

//#endif
//        }
//    }

//#if !DOTWEEN_ENABLED
//    private IEnumerator ScaleDownAndDestroy(GameObject go, float t)
//    {
//        var tr = go.transform;
//        var start = tr.localScale;
//        float e = 0f;
//        while (e < t)
//        {
//            e += Time.deltaTime;
//            float k = 1f - Mathf.Clamp01(e / t);
//            tr.localScale = start * k;
//            yield return null;
//        }
//        Destroy(go);
//    }
//#endif
//}



