﻿using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pointer-driven dragging for board pieces.
///
/// The piece follows the pointer 1:1 in CONTINUOUS board space; the grid is only
/// used to test legality and to snap once on release. Collision is resolved per
/// axis in small sub-steps, so a piece slides flush along a wall instead of
/// snagging, and diagonal drags move diagonally instead of tracing a staircase.
/// </summary>
public class BoardInputController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardGridXY board;
    [SerializeField] private Camera cam;

    [Tooltip("Per-level move cap. Left empty, it is found in the scene at Awake. " +
             "No PuzzleMoveBudget in the scene = unlimited moves (old stages).")]
    [SerializeField] private PuzzleMoveBudget moveBudget;

    [Header("Picking Layers")]
    [SerializeField] private LayerMask pieceLayer3D = ~0; // in case any 3D colliders remain
    [SerializeField] private LayerMask pieceLayer2D = ~0; // default: Everything (set to your Pieces layer)

    [Header("Visuals")]
    [SerializeField] private float liftWhileDragging = 0.10f;  // along the board normal (toward camera in 2D)

    [Header("Drag Feel")]
    [Tooltip("Largest movement resolved in one collision sub-step, in cells. " +
             "Must stay <= 0.5 so a fast flick cannot tunnel through an occupied cell.")]
    [SerializeField, Range(0.05f, 0.5f)] private float maxSubStepCells = 0.25f;

    [Tooltip("Safety cap on sub-steps per frame, so a huge pointer jump cannot stall the frame.")]
    [SerializeField, Min(1)] private int maxSubStepsPerFrame = 64;

    [Tooltip("NO-SNAP MODE. ON: the piece does not move at all when released - it stays exactly " +
             "where the finger left it, and reserves every cell its body overlaps so nothing " +
             "visually overlaps and matching still works. Cost: a piece dropped between cells " +
             "takes up 2 cells instead of 1, so the board fills faster. " +
             "OFF: the piece eases onto the nearest cell over settleDuration (up to half a cell).")]
    [SerializeField] private bool restExactlyWhereReleased = false;

    [Tooltip("Seconds spent easing onto the exact cell center after the pointer is released. " +
             "Unused when Rest Exactly Where Released is on.")]
    [SerializeField, Range(0f, 0.3f)] private float settleDuration = 0.10f;

    [Tooltip("When a piece is walled on the axis it is being pushed along, it may be pulled onto " +
             "the nearest cell line on the OTHER axis by up to this many cells to slip into a gap. " +
             "Keep this SMALL: it displaces the piece without moving the finger, so a large value " +
             "is felt directly as 'I released it here and it landed there'. 0 disables the assist.")]
    [SerializeField, Range(0f, 0.49f)] private float gapAssistCells = 0.15f;

    // ---- drag state ----
    private PieceSimple activePiece;
    private Vector2Int lastValidAnchor;
    private Vector2Int dragStartAnchor;   // anchor the piece sat on when it was picked up
    private Vector2 freeAnchor;           // CONTINUOUS anchor, in cell units
    private Vector2 grabOffsetLocal;      // pieceLocal - pointerLocal at pickup (board-local units)
    private float boardLocalZ;            // board plane depth in board-local space
    private int laneLockedAxis = -1;      // axis the gap assist pinned for the rest of this frame

    // ---- release settle state ----
    private PieceSimple settlePiece;
    private Vector3 settleFrom, settleTo;
    private float settleT;

    private readonly List<Vector2Int> tmpFootprint = new();

    private void Reset()
    {
        if (!cam) cam = Camera.main;
    }

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        if (!moveBudget) moveBudget = FindObjectOfType<PuzzleMoveBudget>();
    }

    private void Update()
    {
        TickSettle();

#if UNITY_EDITOR || UNITY_STANDALONE
        MouseUpdate();
#else
        TouchUpdate();
#endif
    }

    private void MouseUpdate()
    {
        if (Input.GetMouseButtonDown(0))
            TryBeginDrag(Input.mousePosition);

        if (Input.GetMouseButton(0))
            TryDrag(Input.mousePosition);

        if (Input.GetMouseButtonUp(0))
            EndDrag();
    }

    private void TouchUpdate()
    {
        if (Input.touchCount == 0) return;
        var t = Input.GetTouch(0);
        if (t.phase == TouchPhase.Began) TryBeginDrag(t.position);
        else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) TryDrag(t.position);
        else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) EndDrag();
    }

    // ------------------------ Board space helpers ------------------------

    /// <summary>Board plane depth expressed in the board's own local space.</summary>
    private float BoardLocalZ()
        => board.transform.InverseTransformPoint(board.CellCenterWorld(Vector2Int.zero)).z;

    /// <summary>Pointer ray intersected with the board plane, in world space.</summary>
    private Vector3 PointerToBoardWorld(Vector2 screenPos)
    {
        var plane = new Plane(board.BoardPlaneNormal(), board.CellCenterWorld(Vector2Int.zero));
        Ray ray = cam.ScreenPointToRay(screenPos);

        if (plane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        // Ray parallel to the plane (degenerate camera setup): fall back to a
        // straight screen->world projection at the board's depth.
        return cam.ScreenToWorldPoint(new Vector3(
            screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z - board.BoardWorldZ)));
    }

    private Vector2 PointerToBoardLocal(Vector2 screenPos)
    {
        Vector3 local = board.transform.InverseTransformPoint(PointerToBoardWorld(screenPos));
        return new Vector2(local.x, local.y);
    }

    // Continuous anchor <-> board-local, using CellPitch (cellSize + cellPadding),
    // which is the real cell-to-cell distance. CellSize alone drifts by the
    // padding on every cell crossed.
    private Vector2 LocalToAnchor(Vector2 local)
    {
        float pitch = board.CellPitch;
        float half = board.CellSize * 0.5f;
        return new Vector2((local.x - half) / pitch, (local.y - half) / pitch);
    }

    private Vector3 AnchorToWorld(Vector2 anchor)
    {
        float pitch = board.CellPitch;
        float half = board.CellSize * 0.5f;
        return board.transform.TransformPoint(new Vector3(
            anchor.x * pitch + half,
            anchor.y * pitch + half,
            boardLocalZ));
    }

    // ------------------------ Legality ------------------------

    private const float AnchorEpsilon = 1e-4f;

    /// <summary>Round-half-UP. Mathf.RoundToInt is round-half-to-EVEN, so a piece
    /// sitting on exactly x.5 would snap left or right depending on the parity of
    /// the cell — which reads as "it snapped somewhere random".</summary>
    private static float RoundWhole(float v) => Mathf.Floor(v + 0.5f);

    private static Vector2Int RoundAnchor(Vector2 v)
        => new Vector2Int(Mathf.FloorToInt(v.x + 0.5f), Mathf.FloorToInt(v.y + 0.5f));

    private static float SnapNearWhole(float v)
    {
        float r = RoundWhole(v);
        return Mathf.Abs(v - r) < AnchorEpsilon ? r : v;
    }

    private static Vector2 SnapNearWhole(Vector2 v)
        => new Vector2(SnapNearWhole(v.x), SnapNearWhole(v.y));

    private static float Axis(Vector2 v, int axis) => axis == 0 ? v.x : v.y;

    private static Vector2 WithAxis(Vector2 v, int axis, float value)
    {
        if (axis == 0) v.x = value; else v.y = value;
        return v;
    }

    private bool IsAnchorLegal(Vector2Int anchor, PieceSimple piece)
    {
        board.ShapeToCells(anchor, piece.ShapeOffsets, tmpFootprint);
        return board.AreCellsPlaceableForMover(tmpFootprint, piece.PieceId);
    }

    /// <summary>
    /// Legality of a CONTINUOUS anchor. A piece sitting between cells overlaps the
    /// union of its footprints at the surrounding integer anchors, so testing the
    /// (up to) four floor/ceil corners is exact.
    /// </summary>
    private bool IsFreeAnchorLegal(Vector2 anchor, PieceSimple piece)
    {
        // A value a hair off a whole cell must count as being ON that cell, or float
        // noise flips the corner set between one column and two and the piece
        // shivers against every wall it touches.
        anchor = SnapNearWhole(anchor);

        int x0 = Mathf.FloorToInt(anchor.x), x1 = Mathf.CeilToInt(anchor.x);
        int y0 = Mathf.FloorToInt(anchor.y), y1 = Mathf.CeilToInt(anchor.y);

        if (!IsAnchorLegal(new Vector2Int(x0, y0), piece)) return false;
        if (x1 != x0 && !IsAnchorLegal(new Vector2Int(x1, y0), piece)) return false;
        if (y1 != y0 && !IsAnchorLegal(new Vector2Int(x0, y1), piece)) return false;
        if (x1 != x0 && y1 != y0 && !IsAnchorLegal(new Vector2Int(x1, y1), piece)) return false;

        return true;
    }

    // ------------------------ Picking ------------------------

    private bool TryPickPiece(Vector2 screenPos, out PieceSimple piece)
    {
        piece = null;
        if (!cam) return false;

        // Prefer 2D pick in XY mode
        Vector3 wp = PointerToBoardWorld(screenPos);
        var hit2D = Physics2D.OverlapPoint(wp, pieceLayer2D);
        if (hit2D)
        {
            piece = hit2D.GetComponentInParent<PieceSimple>();
            if (piece) return true;
        }

        // Fallback to 3D (if some prefabs still have 3D colliders)
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, pieceLayer3D, QueryTriggerInteraction.Ignore))
        {
            piece = hit.collider.GetComponentInParent<PieceSimple>();
            if (piece) return true;
        }

        return false;
    }

    private void TryBeginDrag(Vector2 screenPos)
    {
        if (!cam || !board) return;

        // Land any piece still easing onto its cell before touching the board again,
        // so occupancy and match resolution are settled before a new pickup.
        CompleteSettle();

        // Move budget spent -> the board is no longer interactable. An already
        // running drag is unaffected; only NEW pickups are refused.
        if (moveBudget && !moveBudget.HasMovesLeft) return;

        if (!TryPickPiece(screenPos, out var piece))
            return;

        if (!piece /*|| piece.IsFrozen*/) return;

        activePiece = piece;
        boardLocalZ = BoardLocalZ();

        // PieceSimple.TryPlace keeps _anchor authoritative, so trust it first and
        // only fall back to reading the transform back off the grid.
        Vector2Int anchor = piece.Anchor;
        if (!IsAnchorLegal(anchor, piece))
        {
            if (!board.TryWorldToCell(piece.transform.position, out anchor))
                anchor = board.ClampAnchorToFitShape(
                    WorldToAnchorRounded(piece.transform.position), piece.ShapeOffsets);
        }

        lastValidAnchor = anchor;
        dragStartAnchor = anchor;

        // Seed the continuous position from where the piece ACTUALLY is, not from the
        // whole-cell anchor. In no-snap mode a piece can legitimately be resting
        // between cells, and seeding from the anchor would teleport it onto its cell
        // the instant it is touched — undoing the very thing that mode exists for.
        Vector3 restLocal = board.transform.InverseTransformPoint(piece.transform.position);
        freeAnchor = LocalToAnchor(new Vector2(restLocal.x, restLocal.y));
        if (!IsFreeAnchorLegal(freeAnchor, piece)) freeAnchor = anchor;

        // Keep the sub-cell grab point: the piece must not jump under the finger.
        // Measure the offset against where the piece is ABOUT to be drawn, not
        // against its old transform — if the two disagree the piece lurches on the
        // very first drag frame.
        Vector3 pieceLocal = board.transform.InverseTransformPoint(AnchorToWorld(freeAnchor));
        grabOffsetLocal = new Vector2(pieceLocal.x, pieceLocal.y) - PointerToBoardLocal(screenPos);

        // Only the lift changes on pickup — no snap to the cell center.
        piece.transform.position = AnchorToWorld(freeAnchor) + board.BoardPlaneNormal() * liftWhileDragging;
    }

    private Vector2Int WorldToAnchorRounded(Vector3 world)
    {
        Vector3 local = board.transform.InverseTransformPoint(world);
        return Vector2Int.RoundToInt(LocalToAnchor(new Vector2(local.x, local.y)));
    }

    // ------------------------ Dragging ------------------------

    private void TryDrag(Vector2 screenPos)
    {
        if (!activePiece) return;

        Vector2 desired = LocalToAnchor(PointerToBoardLocal(screenPos) + grabOffsetLocal);

        // Axis locks pin the blocked component to where the drag started.
        if (!activePiece.AllowsX) desired.x = dragStartAnchor.x;
        if (!activePiece.AllowsY) desired.y = dragStartAnchor.y;

        MoveFreeAnchorTowards(desired);

        // Rounding a legal continuous anchor always lands on one of its corner
        // anchors, which IsFreeAnchorLegal already proved legal.
        lastValidAnchor = RoundAnchor(freeAnchor);

        activePiece.transform.position =
            AnchorToWorld(freeAnchor) + board.BoardPlaneNormal() * liftWhileDragging;
    }

    /// <summary>
    /// Walks <see cref="freeAnchor"/> toward <paramref name="desired"/> in sub-cell
    /// steps. Each step takes the diagonal when it is free and otherwise resolves X
    /// and Y independently, which is what lets a piece keep gliding along a wall.
    /// </summary>
    private void MoveFreeAnchorTowards(Vector2 desired)
    {
        Vector2 delta = desired - freeAnchor;
        float span = Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
        if (span <= AnchorEpsilon) return;

        int steps = Mathf.Clamp(Mathf.CeilToInt(span / maxSubStepCells), 1, maxSubStepsPerFrame);
        Vector2 step = delta / steps;

        laneLockedAxis = -1;

        for (int i = 0; i < steps; i++)
        {
            Vector2 want = freeAnchor + step;

            // Fast path: a clean diagonal (or straight) move with nothing in the way.
            if (IsFreeAnchorLegal(want, activePiece))
            {
                freeAnchor = want;
                continue;
            }

            Vector2 before = freeAnchor;

            // Resolve the axis the player is pushing hardest FIRST, so the gap assist
            // aligns the piece with the lane it is trying to enter rather than the
            // one it is leaving.
            if (Mathf.Abs(step.x) >= Mathf.Abs(step.y))
            {
                ResolveAxisAssisted(0, want.x);
                ResolveAxisAssisted(1, want.y);
            }
            else
            {
                ResolveAxisAssisted(1, want.y);
                ResolveAxisAssisted(0, want.x);
            }

            // Wedged on both axes — further sub-steps cannot help this frame.
            if (freeAnchor == before) break;
        }

        freeAnchor = SnapNearWhole(freeAnchor);
    }

    /// <summary>
    /// Moves one axis of <see cref="freeAnchor"/> toward <paramref name="target"/>.
    /// When that axis is completely walled, it retries after pulling the OTHER axis
    /// onto its nearest cell line. That assist is what makes a gap exactly as tall as
    /// the piece passable at all: without it the player would have to hold the
    /// perpendicular axis at a float-exact whole cell by hand, so the piece jams at
    /// the mouth of the corridor while the pointer sails on.
    /// </summary>
    private void ResolveAxisAssisted(int axis, float target)
    {
        if (axis == laneLockedAxis) return;

        float from = Axis(freeAnchor, axis);
        float moved = ResolveAxis(freeAnchor, axis, target);
        if (!Mathf.Approximately(moved, from))
        {
            freeAnchor = WithAxis(freeAnchor, axis, moved);
            return;
        }

        if (gapAssistCells <= 0f) return;

        int other = 1 - axis;

        // A locked axis must never be "helped" onto a different lane.
        if (other == 0 && !activePiece.AllowsX) return;
        if (other == 1 && !activePiece.AllowsY) return;

        float perp = Axis(freeAnchor, other);
        float lane = RoundWhole(perp);
        float drift = Mathf.Abs(perp - lane);
        if (drift <= AnchorEpsilon || drift > gapAssistCells) return;

        Vector2 aligned = WithAxis(freeAnchor, other, lane);
        if (!IsFreeAnchorLegal(aligned, activePiece)) return;

        float retry = ResolveAxis(aligned, axis, target);
        if (Mathf.Approximately(retry, Axis(aligned, axis))) return;  // still walled - don't nudge for nothing

        freeAnchor = WithAxis(aligned, axis, retry);

        // Hold the lane for the rest of the frame. Otherwise the perpendicular
        // resolve later in this same frame drags the piece straight back off the
        // line it was just helped onto, and the two fight sub-step by sub-step.
        laneLockedAxis = other;
    }

    /// <summary>
    /// Moves one axis of <paramref name="anchor"/> toward <paramref name="target"/>,
    /// stopping flush against the obstruction. Blocking boundaries always fall on
    /// whole cells, so the flush position is the integer boundary ahead.
    /// </summary>
    private float ResolveAxis(Vector2 anchor, int axis, float target)
    {
        float current = axis == 0 ? anchor.x : anchor.y;
        if (Mathf.Approximately(current, target)) return current;

        Vector2 test = anchor;
        if (axis == 0) test.x = target; else test.y = target;
        if (IsFreeAnchorLegal(test, activePiece)) return target;

        bool forward = target > current;
        float boundary = forward ? Mathf.Ceil(current) : Mathf.Floor(current);

        // Ceil/Floor of a legal position is itself legal (it overlaps fewer cells),
        // but guard anyway rather than trusting the invariant blindly.
        if (forward ? boundary <= target : boundary >= target)
        {
            if (axis == 0) test.x = boundary; else test.y = boundary;
            if (IsFreeAnchorLegal(test, activePiece)) return boundary;
        }

        return current;
    }

    // ------------------------ Release ------------------------

    private void EndDrag()
    {
        if (!activePiece) return;

        var p = activePiece;
        activePiece = null;

        if (restExactlyWhereReleased && TryRestInPlace(p)) return;

        // TryPlace updates occupancy and jumps the root to the cell center.
        // On failure fall back to where this drag STARTED, never to p.Anchor:
        // PieceSimple._anchor can hold a value that was never validated or occupied,
        // because AutoBuildOffsetsFromChildren assigns it directly and
        // BoardBootstrapper calls that unconditionally. Trusting it can fling the
        // piece to the far side of the board.
        if (!p.TryPlace(lastValidAnchor))
            lastValidAnchor = dragStartAnchor;

        // A move only counts if the piece actually ended up on a different
        // cell. Tapping a piece and letting go, or a drag the board refused
        // (the anchor never advanced), both land back on dragStartAnchor.
        if (moveBudget && lastValidAnchor != dragStartAnchor)
            moveBudget.RegisterMove();

        // Ease from where the finger left it (lift included) onto the exact
        // center, instead of popping up to half a cell on release.
        settlePiece = p;
        settleFrom = AnchorToWorld(freeAnchor) + board.BoardPlaneNormal() * liftWhileDragging;
        settleTo = board.CellCenterWorld(lastValidAnchor);
        settleT = 0f;

        if (settleDuration <= 0f) CompleteSettle();
        else p.transform.position = settleFrom;
    }

    /// <summary>
    /// NO-SNAP RELEASE. Leaves the piece exactly where the finger left it and books
    /// every cell its body overlaps, so the occupancy grid still tells the truth and
    /// matching, blocked cells and the move budget all keep working unchanged.
    /// Returns false if the board refuses the reservation, in which case the caller
    /// falls back to the ordinary snap-to-cell path.
    /// </summary>
    private bool TryRestInPlace(PieceSimple p)
    {
        BuildOverlappedCells(freeAnchor, p, tmpFootprint);

        if (!p.TryPlaceExact(lastValidAnchor, tmpFootprint, snapRootToAnchor: false))
            return false;

        // Drop the drag lift, but do NOT touch X/Y - that is the whole point.
        p.transform.position = AnchorToWorld(freeAnchor);

        if (moveBudget && lastValidAnchor != dragStartAnchor)
            moveBudget.RegisterMove();

        var resolver = GetComponent<MatchResolver>() ?? FindObjectOfType<MatchResolver>();
        if (resolver) resolver.ResolveFrom(p);

        return true;
    }

    /// <summary>
    /// Every cell a piece at a CONTINUOUS anchor overlaps: the union of its footprint
    /// at the surrounding whole anchors. A piece straddling a boundary covers both
    /// sides, so it must reserve both.
    /// </summary>
    private void BuildOverlappedCells(Vector2 anchor, PieceSimple piece, List<Vector2Int> outCells)
    {
        anchor = SnapNearWhole(anchor);

        int x0 = Mathf.FloorToInt(anchor.x), x1 = Mathf.CeilToInt(anchor.x);
        int y0 = Mathf.FloorToInt(anchor.y), y1 = Mathf.CeilToInt(anchor.y);

        outCells.Clear();
        AddCorner(x0, y0, piece, outCells);
        if (x1 != x0) AddCorner(x1, y0, piece, outCells);
        if (y1 != y0) AddCorner(x0, y1, piece, outCells);
        if (x1 != x0 && y1 != y0) AddCorner(x1, y1, piece, outCells);
    }

    private static void AddCorner(int ax, int ay, PieceSimple piece, List<Vector2Int> outCells)
    {
        var offsets = piece.ShapeOffsets;
        for (int i = 0; i < offsets.Count; i++)
        {
            var c = new Vector2Int(ax + offsets[i].x, ay + offsets[i].y);
            if (!outCells.Contains(c)) outCells.Add(c);
        }
    }

    private void TickSettle()
    {
        if (!settlePiece) { settlePiece = null; return; }

        settleT += Time.deltaTime / Mathf.Max(0.0001f, settleDuration);
        if (settleT >= 1f) { CompleteSettle(); return; }

        settlePiece.transform.position =
            Vector3.Lerp(settleFrom, settleTo, Mathf.SmoothStep(0f, 1f, settleT));
    }

    /// <summary>Finishes a pending settle right now: exact center, then match resolution.</summary>
    private void CompleteSettle()
    {
        if (!settlePiece) { settlePiece = null; return; }

        var p = settlePiece;
        settlePiece = null;

        p.transform.position = settleTo;

        var resolver = GetComponent<MatchResolver>() ?? FindObjectOfType<MatchResolver>();
        if (resolver) resolver.ResolveFrom(p);
    }
}
