using System.Collections.Generic;
using UnityEngine;


[DisallowMultipleComponent]
public class BoardGridCubes : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardGridXY board;

    [Header("Appearance")]
    [SerializeField] private Material freeMat;         // e.g., semi-transparent blue/gray
    [SerializeField] private Material blockedMat;      // red fill for blocked cells
    [SerializeField] private Material strokeMat;       // outline material (e.g., bright red)
    [SerializeField] private float thickness = 0.08f;  // cube height
    [SerializeField] private float inset = 0.03f;      // gap so cell borders are visible
    [SerializeField] private float zOffset = 0.003f;   // small lift to avoid z-fighting
    [SerializeField] private Transform holder;         // parent for all spawned visuals (auto if null)

    [Header("Stroke (blocked cells only)")]
    [SerializeField] private bool drawStrokeForBlocked = true;
    [SerializeField] private float strokeWidth = 0.02f;   // strip thickness along the border
    [SerializeField] private float strokeHeight = 0.08f;  // height of the outline strips

    [Header("Behavior")]
    [SerializeField] private bool autoGenerateOnStart = true;
    [SerializeField] private bool addColliderToBlocked = false; // make blocked cells physical (optional)
    [SerializeField] private string gridLayerName = "Board";

    // per-cell references
    private GameObject[,] cells;     // [x,y] visual cube
    private GameObject[,] strokes;   // [x,y] stroke root (4 strips), only for blocked
    [SerializeField] private float blockedHeightExtra = 0.06f; // extra height for blocked cells

    [SerializeField] private BoardGhostMask ghostMask;   // <- assign your mask component here
    [SerializeField] private bool hideGhostedCells = true; // leave ON to skip cubes for ghosts

    private void Reset()
    {
        if (!board) board = GetComponent<BoardGridXY>();
    }

    private void Start()
    {
        if (!board) board = GetComponent<BoardGridXY>();
        //board.SetBlocked(new Vector2Int(2, 2), true);
        //board.SetBlocked(new Vector2Int(4, 3), true);
        if (autoGenerateOnStart) Regenerate();
    }

    private bool IsGhosted(int x, int y)
    {
        if (!hideGhostedCells) return false;
        if (ghostMask) return ghostMask.GetCell(x, y);   // use the mask you paint in the Inspector

        // If you ALSO added IsGhosted to BoardGridXY, prefer that:
        // return board && board.IsGhosted(new Vector2Int(x, y));

        return false;
    }



    /// <summary>Create/replace all cell cubes and strokes.</summary>
    public void Regenerate()
    {
        if (!board)
        {
            Debug.LogWarning("BoardGridCubes: no BoardGridXY assigned.", this);
            return;
        }

        Clear();

        if (!holder)
        {
            var h = new GameObject("GridCubes");
            h.transform.SetPositionAndRotation(board.transform.position, board.transform.rotation);
            h.transform.SetParent(board.transform, true);
            holder = h.transform;
        }

        cells = new GameObject[board.Width, board.Height];
        strokes = new GameObject[board.Width, board.Height];

        int gridLayer = LayerMask.NameToLayer(gridLayerName);
        // Ensure Board layer exists, fallback to Default if not found
        if (gridLayer == -1)
        {
            Debug.LogWarning($"BoardGridCubes: Layer '{gridLayerName}' not found, using Default layer instead.");
            gridLayer = 0; // Default layer
        }

        float s = board.CellSize;
        float sxy = Mathf.Max(0.01f, s - 2f * inset);
        //float yCenter = board.BoardWorldZ + zOffset + thickness * 0.5f;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                // 🚫 Don't create a cube for ghosted cells
                if (IsGhosted(x, y))
                {
                    cells[x, y] = null;   // ensure table is empty
                    continue;
                }

                var cellPos = board.CellCenterWorld(new Vector2Int(x, y));

                // base visual cube
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Cell_{x}_{y}";
                go.layer = gridLayer;
                go.transform.SetParent(holder, true);

                // --- NEW: height per cell ---
                bool isBlocked = board.IsBlocked(new Vector2Int(x, y));
                float h = thickness + (isBlocked ? blockedHeightExtra : 0f);
                float yCenter = board.BoardWorldZ +4.3f /*+ zOffset + h * 0.5f*/;

                go.transform.position = new Vector3(cellPos.x, cellPos.y, yCenter);
                go.transform.rotation = board.transform.rotation;
                go.transform.localScale = new Vector3(sxy, sxy, h);

                // remove default collider => visual-only grid
                var col = go.GetComponent<Collider>();
                if (col) Destroy(col);

                // set material by blocked state
                var rend = go.GetComponent<MeshRenderer>();
                bool blocked = board.IsBlocked(new Vector2Int(x, y));
                rend.sharedMaterial = blocked ? blockedMat : freeMat;

                // optional physical collider only for blocked cells
                if (addColliderToBlocked && blocked)
                {
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = Vector3.one; // matches cube mesh
                    bc.isTrigger = false;
                }

                cells[x, y] = go;

                // create outline stroke only for blocked cells
                EnsureStrokeForCell(x, y, blocked, cellPos, yCenter, sxy);
            }
        }
    }

    /// <summary>Update fill materials, colliders, and strokes after blocked changes.</summary>
    public void RefreshColors()
    {
        if (cells == null || board == null) return;

        float s = board.CellSize;
        float sxy = Mathf.Max(0.01f, s - 2f * inset);
        float yCenter = board.BoardWorldZ + zOffset + thickness * 0.5f;

        for (int x = 0; x < board.Width; x++)
        {
            for (int y = 0; y < board.Height; y++)
            {
                bool ghost = IsGhosted(x, y);

                if (ghost)
                {
                    // If a cube exists here, remove it (ghosts are invisible)
                    if (cells[x, y])
                    {
                        if (Application.isPlaying) Destroy(cells[x, y]);
                        else DestroyImmediate(cells[x, y]);
                        cells[x, y] = null;
                    }
                    continue; // nothing else to update for ghost cells
                }
                else
                {
                    // If this cell was ghosted before and has no cube, create one now
                    if (!cells[x, y])
                    {
                        CreateCellVisual(x, y); // helper below
                    }
                }



                var go = cells[x, y];
                if (!go) continue;

                bool blocked = board.IsBlocked(new Vector2Int(x, y));

                // update fill
                var rend = go.GetComponent<MeshRenderer>();
                rend.sharedMaterial = blocked ? blockedMat : freeMat;

                // update optional collider
                if (addColliderToBlocked)
                {
                    var bc = go.GetComponent<BoxCollider>();
                    if (blocked)
                    {
                        if (!bc) bc = go.AddComponent<BoxCollider>();
                        bc.size = Vector3.one;
                        bc.isTrigger = false;
                    }
                    else
                    {
                        if (bc) Destroy(bc);
                    }
                }

                // update stroke presence
                var cellPos = board.CellCenterWorld(new Vector2Int(x, y));
                EnsureStrokeForCell(x, y, blocked, cellPos, yCenter, sxy);
            }
        }
    }
    private void CreateCellVisual(int x, int y)
    {
        float s = board.CellSize;
        float sxy = Mathf.Max(0.01f, s - 2f * inset);
        bool isBlocked = board.IsBlocked(new Vector2Int(x, y));

        var cellPos = board.CellCenterWorld(new Vector2Int(x, y));
        float h = thickness + (isBlocked ? blockedHeightExtra : 0f);
        float yCenter = board.BoardWorldZ + zOffset + h * 0.5f;

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = $"Cell_{x}_{y}";

        int gridLayer = LayerMask.NameToLayer(gridLayerName);
        if (gridLayer == -1)
        {
            Debug.LogWarning($"BoardGridCubes: Layer '{gridLayerName}' not found, using Default layer instead.");
            gridLayer = 0; // Default layer
        }
        go.layer = gridLayer;

        go.transform.SetParent(holder, true);
        go.transform.position = new Vector3(cellPos.x, cellPos.y, yCenter);
        go.transform.rotation = board.transform.rotation;
        go.transform.localScale = new Vector3(sxy, sxy, h);

        var col = go.GetComponent<Collider>(); if (col) Destroy(col);
        var rend = go.GetComponent<MeshRenderer>();
        rend.sharedMaterial = isBlocked ? blockedMat : freeMat;

        if (addColliderToBlocked && isBlocked)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.size = Vector3.one; bc.isTrigger = false;
        }

        cells[x, y] = go;
    }


    /// <summary>Destroy previously generated cubes/strokes.</summary>
    public void Clear()
    {
        if (cells != null)
        {
            for (int x = 0; x < cells.GetLength(0); x++)
            {
                for (int y = 0; y < cells.GetLength(1); y++)
                {
                    if (cells[x, y])
                    {
                        if (Application.isPlaying) Destroy(cells[x, y]);
                        else DestroyImmediate(cells[x, y]);
                    }
                }
            }
        }

        if (strokes != null)
        {
            for (int x = 0; x < strokes.GetLength(0); x++)
            {
                for (int y = 0; y < strokes.GetLength(1); y++)
                {
                    if (strokes[x, y])
                    {
                        if (Application.isPlaying) Destroy(strokes[x, y]);
                        else DestroyImmediate(strokes[x, y]);
                    }
                }
            }
        }

        cells = null;
        strokes = null;
    }

    // ---------- stroke helpers ----------

    private void EnsureStrokeForCell(int x, int y, bool shouldHave, Vector3 cellCenterWorld, float yCenter, float sxy)
    {
        // remove if shouldn't have
        if (!shouldHave)
        {
            if (strokes != null && strokes[x, y])
            {
                if (Application.isPlaying) Destroy(strokes[x, y]);
                else DestroyImmediate(strokes[x, y]);
                strokes[x, y] = null;
            }
            return;
        }

        // create if missing
        if (strokes[x, y] == null)
        {
            strokes[x, y] = CreateStrokeAt(cellCenterWorld, yCenter, sxy);
        }
        else
        {
            // ensure it sits at the right place (in case board moved)
            var root = strokes[x, y].transform;
            root.position = new Vector3(cellCenterWorld.x, cellCenterWorld.y, yCenter);
            root.rotation = board.transform.rotation;
        }
    }

    private GameObject CreateStrokeAt(Vector3 cellCenterWorld, float yCenter, float sxy)
    {
        // root
        var root = new GameObject("Stroke");
        root.transform.SetParent(holder, true);
        root.transform.position = new Vector3(cellCenterWorld.x, yCenter, cellCenterWorld.z);
        root.transform.rotation = board.transform.rotation;

        int gridLayer = LayerMask.NameToLayer(gridLayerName);
        if (gridLayer == -1)
        {
            Debug.LogWarning($"BoardGridCubes: Layer '{gridLayerName}' not found, using Default layer instead.");
            gridLayer = 0; // Default layer
        }
        root.layer = gridLayer;

        // build 4 thin cubes around the edges (XY plane, thin along Z)
CreateStrip(root.transform, new Vector3(0f,  (sxy * 0.5f - strokeWidth * 0.5f), 0f), new Vector3(sxy, strokeWidth, strokeHeight));
CreateStrip(root.transform, new Vector3(0f, -(sxy * 0.5f - strokeWidth * 0.5f), 0f), new Vector3(sxy, strokeWidth, strokeHeight));
CreateStrip(root.transform, new Vector3( (sxy * 0.5f - strokeWidth * 0.5f), 0f, 0f), new Vector3(strokeWidth, sxy, strokeHeight));
CreateStrip(root.transform, new Vector3(-(sxy * 0.5f - strokeWidth * 0.5f), 0f, 0f), new Vector3(strokeWidth, sxy, strokeHeight));
return root;

    }

    private void CreateStrip(Transform parent, Vector3 localOffset, Vector3 worldScale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "StrokeStrip";
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = worldScale;

        var col = go.GetComponent<Collider>();   // keep strokes visual-only
        if (col) Destroy(col);

        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = strokeMat ? strokeMat : blockedMat;

        int gridLayer = LayerMask.NameToLayer(gridLayerName);
        if (gridLayer == -1) gridLayer = 0; // Default layer fallback
        go.layer = gridLayer;
    }
}

