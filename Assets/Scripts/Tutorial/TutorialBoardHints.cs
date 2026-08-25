using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Works out, from the CURRENT state of the board, which stack the player should
/// drag and where it should land to make a match.
///
/// This is what lets the tutorial keep pointing at the right thing:
///   - if the player drags the hinted stack somewhere else, the next hint starts
///     from where the stack now is, not from where it began;
///   - once a pair blasts, the next call finds the NEXT matchable pair, so one
///     step can walk the player through a whole board.
///
/// It answers with a LEGAL, REACHABLE move: the landing anchor is always a spot
/// the piece can actually slide to in one drag, using the same cell-by-cell rule
/// BoardInputController uses, so the hand never demonstrates an impossible move.
/// </summary>
public static class TutorialBoardHints
{
    private static readonly Vector2Int[] Dirs4 =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0),
        new Vector2Int(0, 1), new Vector2Int(0, -1),
    };

    // reused scratch - Unity is single-threaded, so this is safe and keeps the
    // per-cycle hint free of allocations
    private static readonly List<Vector2Int> _cells = new List<Vector2Int>();
    private static readonly List<Vector2Int> _cellsB = new List<Vector2Int>();
    private static readonly HashSet<Vector2Int> _targetCells = new HashSet<Vector2Int>();
    private static readonly HashSet<Vector2Int> _candidates = new HashSet<Vector2Int>();
    private static readonly List<PieceSimple> _placed = new List<PieceSimple>();

    /// <summary>
    /// World-space centre of a piece's whole footprint if it sat at `anchor`.
    /// Averaging the cells means a 2-tall stack is pointed at in the middle, not
    /// at its bottom cell - and it works for any shape without hand-tuning.
    /// </summary>
    public static Vector3 PieceCenterWorld(BoardGridXY board, PieceSimple piece, Vector2Int anchor)
    {
        var offsets = piece.ShapeOffsets;
        if (offsets == null || offsets.Count == 0)
            return board.CellCenterWorld(anchor);

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < offsets.Count; i++)
            sum += board.CellCenterWorld(anchor + offsets[i]);

        return sum / offsets.Count;
    }

    /// <summary>
    /// Finds the cheapest match the player could make right now.
    /// Returns false when the board holds no reachable matchable pair.
    /// </summary>
    public static bool TryFindMatchHint(BoardGridXY board, out PieceSimple mover, out PieceSimple target, out Vector2Int landing)
        => TryFindMatchHint(board, null, out mover, out target, out landing);

    /// <summary>
    /// Same, but `preferred` (if it is still on the board and still has a match
    /// available) wins over any shorter drag. That is how a tutorial says "teach
    /// THIS pair first" without hard-coding the whole move - once that pair is
    /// blasted, the search falls back to the cheapest remaining match.
    /// </summary>
    public static bool TryFindMatchHint(BoardGridXY board, PieceSimple preferred,
                                        out PieceSimple mover, out PieceSimple target, out Vector2Int landing)
        => TryFindMatchHint(board, preferred, null, out mover, out target, out landing);

    /// <summary>
    /// Full form. `sticky` is the stack the hand is ALREADY demonstrating: while
    /// it is still on the board and still has a move, it keeps being the one that
    /// moves.
    ///
    /// Without that the search would re-pick the cheapest direction every cycle,
    /// so moving either stack could make the hand suddenly demonstrate dragging
    /// the OTHER one - the gesture would appear to reverse under the player.
    /// </summary>
    public static bool TryFindMatchHint(BoardGridXY board, PieceSimple preferred, PieceSimple sticky,
                                        out PieceSimple mover, out PieceSimple target, out Vector2Int landing)
    {
        mover = null;
        target = null;
        landing = default;
        if (!board) return false;

        CollectPlaced(board);

        // 1) keep demonstrating the same stack for as long as that still works
        if (sticky && _placed.Contains(sticky)
            && TryBestForMover(board, sticky, out target, out landing))
        {
            mover = sticky;
            return true;
        }

        // 2) the pair the tutorial asked to teach first
        if (preferred && _placed.Contains(preferred)
            && TryBestFor(board, preferred, out mover, out target, out landing))
            return true;

        int bestSteps = int.MaxValue;

        for (int i = 0; i < _placed.Count; i++)
        {
            for (int j = i + 1; j < _placed.Count; j++)
            {
                var a = _placed[i];
                var b = _placed[j];

                // same rule MatchResolver blasts by
                if (a.ColorId != b.ColorId) continue;
                if (!MatchResolver.AreShapesMatchCompatible(a.ShapeId, b.ShapeId)) continue;

                // either one may be the one that moves - try both and keep the shorter drag
                TryPair(board, a, b, ref mover, ref target, ref landing, ref bestSteps);
                TryPair(board, b, a, ref mover, ref target, ref landing, ref bestSteps);
            }
        }

        return mover != null;
    }

    /// <summary>
    /// Best match where `mover` is specifically the piece that MOVES. This is what
    /// keeps the demonstrated direction stable: the hand always drags this stack,
    /// wherever the player has since dragged it, onto whichever partner is cheapest.
    /// </summary>
    private static bool TryBestForMover(BoardGridXY board, PieceSimple mover,
                                        out PieceSimple target, out Vector2Int landing)
    {
        PieceSimple chosenMover = null;
        target = null;
        landing = default;
        int bestSteps = int.MaxValue;

        for (int i = 0; i < _placed.Count; i++)
        {
            var other = _placed[i];
            if (other == mover) continue;
            if (other.ColorId != mover.ColorId) continue;
            if (!MatchResolver.AreShapesMatchCompatible(other.ShapeId, mover.ShapeId)) continue;

            TryPair(board, mover, other, ref chosenMover, ref target, ref landing, ref bestSteps);
        }

        return chosenMover != null;
    }

    /// <summary>Best match involving one specific piece, moving either side.</summary>
    private static bool TryBestFor(BoardGridXY board, PieceSimple piece,
                                   out PieceSimple mover, out PieceSimple target, out Vector2Int landing)
    {
        mover = null;
        target = null;
        landing = default;
        int bestSteps = int.MaxValue;

        for (int i = 0; i < _placed.Count; i++)
        {
            var other = _placed[i];
            if (other == piece) continue;
            if (other.ColorId != piece.ColorId) continue;
            if (!MatchResolver.AreShapesMatchCompatible(other.ShapeId, piece.ShapeId)) continue;

            TryPair(board, piece, other, ref mover, ref target, ref landing, ref bestSteps);
            TryPair(board, other, piece, ref mover, ref target, ref landing, ref bestSteps);
        }

        return mover != null;
    }

    /// <summary>The placed piece whose footprint covers `cell`, or null.</summary>
    public static PieceSimple PieceAtCell(BoardGridXY board, Vector2Int cell)
    {
        if (!board || !board.IsInside(cell)) return null;

        int occupant = board.GetOccupant(cell);
        return occupant > 0 ? PieceSimple.GetById(occupant) : null;
    }

    private static void CollectPlaced(BoardGridXY board)
    {
        _placed.Clear();

        foreach (var p in Object.FindObjectsOfType<PieceSimple>(false))
        {
            if (!p || !p.gameObject.activeInHierarchy) continue;
            if (p.Board != board) continue;

            // ShapeOffsets always contains (0,0), so the anchor cell is part of the
            // footprint - if the board does not say this piece owns it, the piece
            // is not really placed (mid-blast, or never bootstrapped).
            if (board.GetOccupant(p.Anchor) != p.PieceId) continue;

            _placed.Add(p);
        }
    }

    private static void TryPair(BoardGridXY board, PieceSimple mover, PieceSimple target,
                                ref PieceSimple bestMover, ref PieceSimple bestTarget,
                                ref Vector2Int bestLanding, ref int bestSteps)
    {
        // Footprint of the piece that stays put. Prefer the cells it ACTUALLY
        // holds over anchor + offsets: a piece resting between cells reserves
        // every cell it overlaps, and MatchResolver reads it the same way.
        _targetCells.Clear();
        var held = target.OccupiedCells;
        if (held != null && held.Count > 0)
        {
            for (int i = 0; i < held.Count; i++) _targetCells.Add(held[i]);
        }
        else
        {
            board.ShapeToCells(target.Anchor, target.ShapeOffsets, _cellsB);
            for (int i = 0; i < _cellsB.Count; i++) _targetCells.Add(_cellsB[i]);
        }

        // every anchor that would put ANY of the mover's cells against the target
        _candidates.Clear();
        foreach (var tc in _targetCells)
        {
            for (int d = 0; d < Dirs4.Length; d++)
            {
                var touching = tc + Dirs4[d];
                if (_targetCells.Contains(touching)) continue;

                var offsets = mover.ShapeOffsets;
                for (int o = 0; o < offsets.Count; o++)
                    _candidates.Add(touching - offsets[o]);
            }
        }

        foreach (var anchor in _candidates)
        {
            if (anchor == mover.Anchor) continue;          // already there
            if (!board.IsInside(anchor)) continue;

            board.ShapeToCells(anchor, mover.ShapeOffsets, _cells);
            if (!board.AreCellsPlaceableForMover(_cells, mover.PieceId)) continue;
            if (!TouchesTarget(_cells, _targetCells)) continue;
            if (!CanSlide(board, mover, mover.Anchor, anchor, out int steps)) continue;

            if (steps < bestSteps)
            {
                bestSteps = steps;
                bestMover = mover;
                bestTarget = target;
                bestLanding = anchor;
            }
        }
    }

    private static bool TouchesTarget(List<Vector2Int> moverCells, HashSet<Vector2Int> targetCells)
    {
        for (int i = 0; i < moverCells.Count; i++)
            for (int d = 0; d < Dirs4.Length; d++)
                if (targetCells.Contains(moverCells[i] + Dirs4[d]))
                    return true;

        return false;
    }

    /// <summary>
    /// Can the piece get from `from` to `to` in one drag? Mirrors what
    /// BoardInputController actually does: step one cell at a time and stop at the
    /// first illegal anchor, X first then Y (and the other order too, since the
    /// player's pointer path decides which happens).
    /// </summary>
    private static bool CanSlide(BoardGridXY board, PieceSimple piece, Vector2Int from, Vector2Int to, out int steps)
    {
        steps = Mathf.Abs(to.x - from.x) + Mathf.Abs(to.y - from.y);
        if (steps == 0) return false;

        if (to.x != from.x && !piece.AllowsX) return false;
        if (to.y != from.y && !piece.AllowsY) return false;

        return Walk(board, piece, from, to, xFirst: true)
            || Walk(board, piece, from, to, xFirst: false);
    }

    private static bool Walk(BoardGridXY board, PieceSimple piece, Vector2Int from, Vector2Int to, bool xFirst)
    {
        var cur = from;

        for (int pass = 0; pass < 2; pass++)
        {
            bool doX = xFirst ? pass == 0 : pass == 1;

            if (doX)
            {
                int step = to.x > cur.x ? 1 : -1;
                while (cur.x != to.x)
                {
                    var next = new Vector2Int(cur.x + step, cur.y);
                    if (!IsLegal(board, piece, next)) return false;
                    cur = next;
                }
            }
            else
            {
                int step = to.y > cur.y ? 1 : -1;
                while (cur.y != to.y)
                {
                    var next = new Vector2Int(cur.x, cur.y + step);
                    if (!IsLegal(board, piece, next)) return false;
                    cur = next;
                }
            }
        }

        return cur == to;
    }

    private static bool IsLegal(BoardGridXY board, PieceSimple piece, Vector2Int anchor)
    {
        board.ShapeToCells(anchor, piece.ShapeOffsets, _cells);
        return board.AreCellsPlaceableForMover(_cells, piece.PieceId);
    }
}
