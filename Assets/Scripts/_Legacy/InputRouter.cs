using UnityEngine;

/// <summary>
/// Central input controller:
/// - Raycasts to pick a Piece (by hitting a UnitCell collider).
/// - Applies slight Y-lift + scale highlight while dragging.
/// - Uses RaycastToBoard to map screen -> grid cell and moves the piece preview,
///   keeping the grabbed cell under the pointer (no jump).
/// - On release: accepts the move (for now) or reverts, until PlacementSystem is wired.
/// </summary>
[DisallowMultipleComponent]
public class InputRouter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private BoardGridXY board;
    [SerializeField] private RaycastToBoard rayToBoard;

    [Header("Picking")]
    [Tooltip("Which layers can be picked (put UnitCells on this).")]
    [SerializeField] private LayerMask pickLayerMask = ~0;

    [Header("Drag Feedback")]
    [Tooltip("Meters to lift the piece while dragging (world Y).")]
    [SerializeField] private float dragLiftY = 0.05f;
    [Tooltip("Uniform scale multiplier while dragging (visual highlight).")]
    [SerializeField] private float dragScale = 1.03f;

    [Header("Behavior (MVP)")]
    [Tooltip("While PlacementSystem is not implemented, accept the new pose on release. If false, revert to start pose on release.")]
    [SerializeField] private bool acceptWithoutValidationForNow = true;

    // ----- runtime state -----
    private Piece _activePiece;
    private Transform _activeRoot;
    private Vector3 _startWorldPos;
    private float _baseWorldY;
    private Vector3 _originalLocalScale;

    // which cell inside the piece we grabbed (piece-local grid offset)
    private Vector2Int _grabbedCellOffset; // e.g., (1,0) if you clicked the second cell of an I3_H

    // target anchor we’re currently previewing
    private Vector2Int _previewAnchor;
    private bool _dragging;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (board == null) board = FindObjectOfType<BoardGridXY>();
        if (rayToBoard == null) rayToBoard = FindObjectOfType<RaycastToBoard>();

        if (targetCamera == null) Debug.LogWarning("[InputRouter] Missing Camera.");
        if (board == null) Debug.LogWarning("[InputRouter] Missing BoardGridXZ.");
        if (rayToBoard == null) Debug.LogWarning("[InputRouter] Missing RaycastToBoard.");
    }

    private void Update()
    {
        // support mouse (Editor) and single touch (device)
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouse();
#else
        HandleTouch();
#endif
    }

    // ---------------- MOUSE PATH ----------------
    private void HandleMouse()
    {
        // press
        if (Input.GetMouseButtonDown(0))
            TryBeginDrag(Input.mousePosition);

        // move
        if (_dragging && Input.GetMouseButton(0))
            ContinueDrag(Input.mousePosition);

        // release
        if (_dragging && Input.GetMouseButtonUp(0))
            EndDrag();
    }

    // ---------------- TOUCH PATH ----------------
    private void HandleTouch()
    {
        if (Input.touchCount == 0) return;
        var t = Input.GetTouch(0);

        if (t.phase == TouchPhase.Began)
            TryBeginDrag(t.position);
        else if (_dragging && (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary))
            ContinueDrag(t.position);
        else if (_dragging && (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled))
            EndDrag();
    }

    // ---------------- CORE FLOW ----------------

    private void TryBeginDrag(Vector2 screenPos)
    {
        // 1) pick a UnitCell collider
        if (!TryPickPiece(screenPos, out _activePiece, out _grabbedCellOffset, out _activeRoot))
            return;

        // 2) cache start pose + base Y and apply lift + highlight
        _startWorldPos = _activeRoot.position;
        _baseWorldY = _activeRoot.position.y;
        _originalLocalScale = _activeRoot.localScale;

        _activeRoot.position = new Vector3(_activeRoot.position.x, _baseWorldY + dragLiftY, _activeRoot.position.z);
        _activeRoot.localScale = _originalLocalScale * dragScale;

        _dragging = true;

        // 3) immediately place preview under the pointer
        ContinueDrag(screenPos);
    }

    private void ContinueDrag(Vector2 screenPos)
    {
        if (_activePiece == null || !_dragging) return;

        // Map pointer to board cell
        if (!rayToBoard.TryScreenToCell(screenPos, out Vector2Int cellUnderPointer))
            return;

        // Keep the grabbed cell exactly under the pointer: anchor = pointerCell - grabbedOffset
        _previewAnchor = cellUnderPointer - _grabbedCellOffset;

        // Convert anchor -> world center for the piece root
        Vector3 target = board.CellCenterWorld(_previewAnchor);
        // maintain Y lift during drag
        target.y = _baseWorldY + dragLiftY;

        _activeRoot.position = target;
    }

    private void EndDrag()
    {
        if (_activePiece == null) { _dragging = false; return; }

        // drop visual lift + highlight
        _activeRoot.localScale = _originalLocalScale;

        if (acceptWithoutValidationForNow)
        {
            // Accept new position (snap to anchor cell center at base Y)
            Vector3 settled = board.CellCenterWorld(_previewAnchor);
            settled.y = _baseWorldY;
            _activeRoot.position = settled;
        }
        else
        {
            // Revert to where we started (until PlacementSystem is wired)
            _activeRoot.position = _startWorldPos;
        }

        // clear state
        _activePiece = null;
        _activeRoot = null;
        _dragging = false;
    }

    // ---------------- HELPERS ----------------

    /// <summary>
    /// Raycasts the scene to find a UnitCell, returns its Piece and the cell offset clicked.
    /// </summary>
    private bool TryPickPiece(Vector2 screenPos, out Piece piece, out Vector2Int grabbedOffset, out Transform pieceRoot)
    {
        piece = null;
        grabbedOffset = default;
        pieceRoot = null;

        if (targetCamera == null) return false;

        Ray ray = targetCamera.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, pickLayerMask, QueryTriggerInteraction.Ignore))
            return false;

        // find the Piece up the hierarchy
        piece = hit.collider.GetComponentInParent<Piece>();
        if (piece == null) return false;

        pieceRoot = piece.transform;

        // determine which local cell we clicked, by rounding the hit child's localPosition / cellSize
        // (assumes UnitCells are direct or nested children positioned on whole-number local X/Z)
        Transform cellTf = hit.collider.transform;
        // Walk up until we reach the immediate child of piece root, in case of deeper nesting (e.g., Layer_Outer/Inner)
        while (cellTf.parent != null && cellTf.parent != pieceRoot) cellTf = cellTf.parent;

        Vector3 lp = cellTf.localPosition;
        float cs = Mathf.Max(piece.cellSize, 0.0001f);
        int gx = Mathf.RoundToInt(lp.x / cs);
        int gz = Mathf.RoundToInt(lp.z / cs);
        grabbedOffset = new Vector2Int(gx, gz);

        return true;
    }
}
