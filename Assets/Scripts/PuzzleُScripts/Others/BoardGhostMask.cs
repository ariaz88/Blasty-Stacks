using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]  // run after BoardGridXY

public class BoardGhostMask : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardGridXY board;

    [Header("Behavior")]
    [Tooltip("Apply the mask automatically whenever you edit it in the Inspector.")]
    [SerializeField] private bool autoApply = true;

    [Tooltip("Also mark ghosted cells as blocked (safer if other systems only check 'blocked').")]
    [SerializeField] private bool alsoMarkBlocked = true;

    [Header("Mask (Width x Height, row-major by Y then X)")]
    [SerializeField, HideInInspector] private List<bool> mask = new List<bool>();

    // Keep track of last board size to preserve painted area on resize
    [SerializeField, HideInInspector] private int lastWidth = -1;
    [SerializeField, HideInInspector] private int lastHeight = -1;

    private void Reset()
    {
        if (!board) board = GetComponentInChildren<BoardGridXY>();
        ResizeMaskToBoard(); // initialize mask
    }

    private void OnEnable()
    {
        if (!board) board = GetComponentInChildren<BoardGridXY>();

        if (!board) return;

        board.EnsureReady();       // < make sure arrays exist

        ResizeMaskToBoard();
        if (autoApply) ApplyGhostMask();
    }

    private void OnValidate()
    {
        if (!board) board = GetComponentInChildren<BoardGridXY>();

        if (!board) return;

        board.EnsureReady();       // < make sure arrays exist

        ResizeMaskToBoard();
        if (autoApply) ApplyGhostMask();
    }

    /// <summary>Ensure mask length matches board size. Preserves overlap when resizing.</summary>
    public void ResizeMaskToBoard()
    {
        if (!board) return;
        int w = Mathf.Max(1, board.Width);
        int h = Mathf.Max(1, board.Height);

        if (mask == null) mask = new List<bool>(w * h);

        if (w != lastWidth || h != lastHeight || mask.Count != w * h)
        {
            var newMask = new List<bool>(w * h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool v = false;
                    if (lastWidth > 0 && lastHeight > 0 && y < lastHeight && x < lastWidth)
                    {
                        int oldIdx = y * lastWidth + x;
                        if (oldIdx >= 0 && oldIdx < mask.Count) v = mask[oldIdx];
                    }
                    newMask.Add(v);
                }
            }
            mask = newMask;
            lastWidth = w;
            lastHeight = h;
        }
    }

    /// <summary>Apply the painted mask to the board (ghost + optional blocked) and refresh visuals.</summary>
    public void ApplyGhostMask()
    {
        if (!board) return;
        //ResizeMaskToBoard();

        // If you added ghost support to BoardGridXY (recommended), clear old ghosts first:
        // board.ClearGhost();
        board.EnsureReady();       // < be safe


        int w = board.Width;
        int h = board.Height;

        // First clear: remove ghosts/blocks we previously set (only if autoApply is frequent, you can skip full clear)
        // We'll do a safe pass: set everything not painted to NOT ghosted/NOT blocked (only if you want full control).
        // If you prefer additive, remove this clearing section.
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                bool isGhost = (idx >= 0 && idx < mask.Count) ? mask[idx] : false;

                // Set state
                if (isGhost)
                {
                    // If you implemented SetGhost on BoardGridXY:
                    // board.SetGhost(new Vector2Int(x, y), true, alsoMarkBlocked);

                    // Minimal approach (works even without SetGhost): at least block the cell
                    if (alsoMarkBlocked) board.SetBlocked(new Vector2Int(x, y), true);
                }
                else
                {
                    // Optional: un-block if it was blocked by mask earlier (comment out if you don't want this)
                    // board.SetGhost(new Vector2Int(x, y), false, alsoMarkBlocked);
                    if (alsoMarkBlocked) board.SetBlocked(new Vector2Int(x, y), false);
                }
            }
        }

        // Refresh visuals if you're using the BoardGridCubes renderer
        var cubes = GetComponentInChildren<BoardGridCubes>();
        if (cubes)
        {
            // If your BoardGridCubes supports hiding ghosted cells, call RefreshColors.
            // Otherwise this will at least recolor blocked cells.
            cubes.RefreshColors();
        }
    }

    /// <summary>Clear the whole mask (no ghosts anywhere) and update the board.</summary>
    public void ClearMask()
    {
        for (int i = 0; i < mask.Count; i++) mask[i] = false;
        ApplyGhostMask();
    }

    /// <summary>Set all cells ghosted and apply.</summary>
    public void FillMask()
    {
        for (int i = 0; i < mask.Count; i++) mask[i] = true;
        ApplyGhostMask();
    }

    // --- API used by the custom inspector ---

    public bool GetCell(int x, int y)
    {
        if (!board) return false;
        int w = board.Width;
        if (x < 0 || y < 0 || x >= w || y >= board.Height) return false;
        int idx = y * w + x;
        return (idx >= 0 && idx < mask.Count) && mask[idx];
    }

    public void SetCell(int x, int y, bool value)
    {
        if (!board) return;
        int w = board.Width;
        if (x < 0 || y < 0 || x >= w || y >= board.Height) return;
        int idx = y * w + x;
        if (idx < 0 || idx >= mask.Count) return;
        mask[idx] = value;

        if (autoApply) ApplyGhostMask();
    }
}
