﻿﻿using System.Collections.Generic;
using UnityEngine;

public class BoardInputController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardGridXY board;
    [SerializeField] private Camera cam;

    [Header("Picking Layers")]
    [SerializeField] private LayerMask pieceLayer3D = ~0; // in case any 3D colliders remain
    [SerializeField] private LayerMask pieceLayer2D = ~0; // default: Everything (set to your Pieces layer)

    [Header("Visuals")]
    [SerializeField] private float liftWhileDragging = 0.10f;  // along +Z (toward camera in 2D)

    [Header("Smoothing")]
    [SerializeField, Range(0.01f, 0.35f)] private float smoothTime = 0.12f;  // seconds
    [SerializeField] private float maxSpeed = 100f;

    // runtime state
    private PieceSimple activePiece;
    private Vector2Int lastValidAnchor;
    private Vector2Int grabbedOffset;     // which subcell of the piece the pointer grabbed (grid space)
    private float _velX, _velY;           // per-axis smooth damp temps

    private readonly List<Vector2Int> tmpFootprint = new();

    private void Reset()
    {
        if (!cam) cam = Camera.main;
        //if (!board) board = FindObjectOfType<BoardGridXY>();
    }

    private void Awake()
    {
        if (!cam) cam = Camera.main;
        //if (!board) board = FindObjectOfType<BoardGridXY>();
    }

    private void Update()
    {
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

    // ------------------------ Picking ------------------------

    private bool TryPickPiece(Vector2 screenPos, out PieceSimple piece, out Collider2D col2D, out Collider col3D)
    {
        piece = null; col2D = null; col3D = null;
        if (!cam) return false;

        // Prefer 2D pick in XY mode
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z - board.BoardWorldZ)));
        var hit2D = Physics2D.OverlapPoint(wp, pieceLayer2D);
        if (hit2D)
        {
            col2D = hit2D;
            piece = hit2D.GetComponentInParent<PieceSimple>();
            if (piece) return true;
        }

        // Fallback to 3D (if some prefabs still have 3D colliders)
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 1000f, pieceLayer3D, QueryTriggerInteraction.Ignore))
        {
            col3D = hit.collider;
            piece = hit.collider.GetComponentInParent<PieceSimple>();
            if (piece) return true;
        }

        return false;
    }

    private void TryBeginDrag(Vector2 screenPos)
    {
        if (!cam || !board) return;

        if (!TryPickPiece(screenPos, out var piece, out var col2D, out var col3D))
            return;

        if (!piece /*|| piece.IsFrozen*/) return;

        activePiece = piece;

        // Determine the current anchor on the board from the piece root
        if (!board.TryWorldToCell(activePiece.transform.position, out lastValidAnchor))
            lastValidAnchor = board.ClampAnchorToFitShape(Vector2Int.zero, activePiece.ShapeOffsets);

        // Figure out which sub-cell we actually grabbed (grid space) from collider center
        Vector3 hitCenterWorld = activePiece.transform.position;
        if (col2D) hitCenterWorld = col2D.bounds.center;
        else if (col3D) hitCenterWorld = col3D.bounds.center;

        hitCenterWorld = board.SnapToPlane(hitCenterWorld);

        Vector2Int hitCell;
        if (!board.TryWorldToCell(hitCenterWorld, out hitCell))
            hitCell = lastValidAnchor;

        grabbedOffset = hitCell - lastValidAnchor;
        _velX = _velY = 0f;

        // Put the visual on the exact center (Z lifted a bit for clarity)
        Vector3 c = board.CellCenterWorld(lastValidAnchor);
        activePiece.transform.position = c + board.BoardPlaneNormal() * liftWhileDragging;
    }

    // ------------------------ Dragging ------------------------

    private bool IsAnchorLegal(Vector2Int anchor, PieceSimple piece)
    {
        board.ShapeToCells(anchor, piece.ShapeOffsets, tmpFootprint);
        return board.AreCellsPlaceableForMover(tmpFootprint, piece.PieceId);
    }

    private void TryDrag(Vector2 screenPos)
    {
        if (!activePiece) return;

        // Convert screen to world → local board → grid cell under pointer
        Vector3 wp = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(cam.transform.position.z - board.BoardWorldZ)));
        Vector3 local = board.transform.InverseTransformPoint(wp);

        float s = board.CellSize;
        Vector2Int pointerCell = new Vector2Int(
            Mathf.FloorToInt(local.x / s),
            Mathf.FloorToInt(local.y / s)
        );

        // Where we want the anchor if we keep grabbing the same sub-cell
        Vector2Int desiredAnchor = pointerCell - grabbedOffset;

        if (!activePiece.AllowsX) desiredAnchor.x = lastValidAnchor.x;
        if (!activePiece.AllowsY) desiredAnchor.y = lastValidAnchor.y;

        // Step cell-by-cell toward desiredAnchor, validating each step
        Vector2Int stepAnchor = lastValidAnchor;

        int dx = desiredAnchor.x - stepAnchor.x;
        int sx = dx == 0 ? 0 : (dx > 0 ? 1 : -1);
        if (!activePiece.AllowsX) { dx = 0; sx = 0; }
        while (dx != 0)
        {
            var cand = new Vector2Int(stepAnchor.x + sx, stepAnchor.y);
            if (IsAnchorLegal(cand, activePiece)) { stepAnchor.x += sx; dx -= sx; } else break;
        }

        int dy = desiredAnchor.y - stepAnchor.y;
        int sy = dy == 0 ? 0 : (dy > 0 ? 1 : -1);
        if (!activePiece.AllowsY) { dy = 0; sy = 0; }
        while (dy != 0)
        {
            var cand = new Vector2Int(stepAnchor.x, stepAnchor.y + sy);
            if (IsAnchorLegal(cand, activePiece)) { stepAnchor.y += sy; dy -= sy; } else break;
        }

        lastValidAnchor = stepAnchor;

        // Smooth the visual toward the exact cell center (keep Z lift)
        Vector3 target = board.CellCenterWorld(lastValidAnchor);
        Transform tr = activePiece.transform;

        float newX = Mathf.SmoothDamp(tr.position.x, target.x, ref _velX, smoothTime, maxSpeed, Time.deltaTime);
        float newY = Mathf.SmoothDamp(tr.position.y, target.y, ref _velY, smoothTime, maxSpeed, Time.deltaTime);

        // clamp to plane & add lift along +Z
        Vector3 basePos = new Vector3(newX, newY, board.BoardWorldZ);
        tr.position = basePos + board.BoardPlaneNormal() * liftWhileDragging;
    }

    private void EndDrag()
    {
        if (!activePiece) return;

        var p = activePiece;
        activePiece = null;

        p.TryPlace(lastValidAnchor); // updates occupancy

        // final: exact center on plane (no lift)
        var center = board.CellCenterWorld(lastValidAnchor);
        p.transform.position = center;

        // trigger resolving if you need it
        var resolver = GetComponent<MatchResolver>() ?? FindObjectOfType<MatchResolver>();
        if (resolver) resolver.ResolveFrom(p);
    }
}

