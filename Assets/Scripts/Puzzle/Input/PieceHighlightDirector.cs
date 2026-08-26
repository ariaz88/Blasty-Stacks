using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides WHICH stacks light up during a drag, and when they shake.
/// <see cref="PieceHighlight"/> owns the actual visuals; this owns the rules.
///
/// The behaviour, as specified against the reference game:
///   PICK UP   the held piece scales up 3%, shakes once, and glows. EVERY stack on the
///             board that could match it also glows, immediately - that is the hint,
///             so distance is deliberately NOT part of this test.
///   DRAG      a matching stack scales up and shakes ONCE when the held piece closes to
///             within MatchNearCells empty cells of it, measured along a row or a column.
///   RELEASE   everything drops.
///
/// "Could match" is <see cref="MatchResolver.AreShapesMatchCompatible"/> plus an equal
/// ColorId - the SAME test the matcher itself runs, reused rather than restated, so the
/// highlight can never promise a match the resolver would refuse (the B4/B5 and B6/B7
/// compatible-shape pairs are easy to forget).
/// </summary>
[DisallowMultipleComponent]
public class PieceHighlightDirector : MonoBehaviour
{
    public static PieceHighlightDirector Instance { get; private set; }

    [Tooltip("Empty cells that may remain between the held piece and a match before that " +
             "match scales up and shakes. Measured along a row or a column only.")]
    [SerializeField, Min(0f)] private float matchNearCells = 2f;

    [Tooltip("Extra distance a match must travel back out before it can shake again, so a " +
             "piece jiggled on the boundary does not retrigger every other frame.")]
    [SerializeField, Min(0f)] private float rearmHysteresisCells = 0.75f;

    private readonly List<PieceSimple> _matches = new();
    private readonly HashSet<PieceSimple> _shaken = new();

    private PieceSimple _held;
    private BoardGridXY _board;

    /// <summary>
    /// Creates itself on scene load, so there is nothing to place by hand - same pattern
    /// as ShardBurst. Add the component to a scene only to tune the fields per level.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance) return;
        if (FindObjectOfType<PieceHighlightDirector>(true)) return;

        var go = new GameObject("~PieceHighlightDirector");
        go.AddComponent<PieceHighlightDirector>();
    }

    private void Awake()
    {
        if (Instance && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ------------------------------------------------------------------
    // Drag lifecycle - called from BoardInputController
    // ------------------------------------------------------------------

    public static void NotifyPickup(PieceSimple held)
    {
        if (Instance) Instance.OnPickup(held);
    }

    public static void NotifyDrag(PieceSimple held)
    {
        if (Instance) Instance.OnDrag(held);
    }

    public static void NotifyRelease()
    {
        if (Instance) Instance.OnRelease();
    }

    private void OnPickup(PieceSimple held)
    {
        OnRelease();          // never leave a previous drag's highlights lit
        if (!held) return;

        _held = held;
        if (!_board) _board = FindObjectOfType<BoardGridXY>();

        // The held piece: glow, grow, and shake straight away - "as soon as it is touched".
        var self = Get(held);
        if (self)
        {
            self.SetHalo(true);
            self.SetEmphasis(true);
            self.PlayShake();
        }

        // Every possible match on the board glows for the whole drag.
        CollectMatches(held, _matches);
        for (int i = 0; i < _matches.Count; i++)
        {
            var h = Get(_matches[i]);
            if (h) h.SetHalo(true);
        }
    }

    private void OnDrag(PieceSimple held)
    {
        if (!_held || _held != held) return;

        for (int i = 0; i < _matches.Count; i++)
        {
            var other = _matches[i];
            if (!other) continue;

            float gap = LineGapCells(held, other);
            bool near = gap <= matchNearCells;

            if (near && !_shaken.Contains(other))
            {
                _shaken.Add(other);
                var h = Get(other);
                if (h) { h.SetEmphasis(true); h.PlayShake(); }
            }
            else if (!near && _shaken.Contains(other) &&
                     gap > matchNearCells + rearmHysteresisCells)
            {
                _shaken.Remove(other);
                var h = Get(other);
                if (h) h.SetEmphasis(false);
            }
        }
    }

    private void OnRelease()
    {
        var self = _held ? Get(_held) : null;
        if (self) self.ClearAll();

        for (int i = 0; i < _matches.Count; i++)
        {
            if (!_matches[i]) continue;
            var h = Get(_matches[i]);
            if (h) h.ClearAll();
        }

        _matches.Clear();
        _shaken.Clear();
        _held = null;
    }

    // ------------------------------------------------------------------
    // Rules
    // ------------------------------------------------------------------

    private static PieceHighlight Get(PieceSimple p)
    {
        if (!p) return null;
        var h = p.GetComponent<PieceHighlight>();
        if (!h) h = p.gameObject.AddComponent<PieceHighlight>();
        return h;
    }

    /// <summary>
    /// Every other live piece that shares this one's colour and a compatible shape.
    /// </summary>
    private static void CollectMatches(PieceSimple held, List<PieceSimple> into)
    {
        into.Clear();

        var all = FindObjectsByType<PieceSimple>(FindObjectsInactive.Exclude,
                                                 FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            var p = all[i];
            if (!p || p == held) continue;
            if (p.ColorId != held.ColorId) continue;
            if (!MatchResolver.AreShapesMatchCompatible(p.ShapeId, held.ShapeId)) continue;

            into.Add(p);
        }
    }

    /// <summary>
    /// Empty cells between two pieces along a row or a column, using their world bounds.
    /// Returns <see cref="float.MaxValue"/> when they are diagonal - i.e. they do not share
    /// any row or column - because "two cells away" only means anything in line.
    ///
    /// World bounds rather than board cells on purpose: while dragging, the held piece sits
    /// at a CONTINUOUS position between cells, so its occupancy is stale and rounding it to
    /// a cell would make the trigger jump a whole cell at a time.
    /// </summary>
    private float LineGapCells(PieceSimple a, PieceSimple b)
    {
        if (!TryBounds(a, out Bounds ba) || !TryBounds(b, out Bounds bb))
            return float.MaxValue;

        float pitch = _board ? _board.CellPitch : 1.086f;
        if (pitch <= 0.0001f) pitch = 1.086f;

        // Gap on each axis: 0 when the bodies overlap on that axis.
        float gapX = Mathf.Max(0f, Mathf.Max(ba.min.x - bb.max.x, bb.min.x - ba.max.x));
        float gapY = Mathf.Max(0f, Mathf.Max(ba.min.y - bb.max.y, bb.min.y - ba.max.y));

        bool inColumn = gapX <= 0f;   // overlapping horizontally -> stacked vertically
        bool inRow = gapY <= 0f;      // overlapping vertically   -> side by side

        if (inRow && inColumn) return 0f;              // touching / overlapping
        if (inRow) return gapX / pitch;
        if (inColumn) return gapY / pitch;
        return float.MaxValue;                          // diagonal, not "in line"
    }

    private static bool TryBounds(PieceSimple p, out Bounds bounds)
    {
        bounds = default;
        if (!p) return false;

        var renderers = p.GetComponentsInChildren<SpriteRenderer>(true);
        bool any = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (!r || !r.sprite) continue;
            // Skip the highlight's own copies - a rim copy is deliberately larger than the
            // piece and would inflate the bounds by the rim thickness.
            if (r.gameObject.name.StartsWith("~Highlight")) continue;

            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }
}
