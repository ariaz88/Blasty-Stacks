using UnityEngine;

public class PieceShapeLayout : MonoBehaviour
{
    public void ApplyLayout(PieceSimple piece)
    {
        if (piece == null || piece.Board == null)
            return;

        float pitch = piece.Board.CellSize + piece.Board.CellPadding;

        switch (piece.ShapeId)
        {
            case "B1":
                // Single cell → do nothing
                return;

            case "B2":
                ApplyB2(piece, pitch);
                return;

            case "B3":
                ApplyB3(piece, pitch);
                return;

            case "B4":
                ApplyB4(piece, pitch);
                return;

            case "B5":
                ApplyB5(piece, pitch);
                return;

                     case "B6":
                ApplyB6(piece, pitch);
                return;

            case "B7":
                ApplyB7(piece, pitch);
                return;



            // b3, b4, b6, b7 → intentionally empty for now
            default:
                return;
        }
    }

    // ---------------- B2 ----------------
    private void ApplyB2(PieceSimple piece, float pitch)
    {
        Transform visual = null;

        foreach (Transform t in piece.transform)
        {
            if (TryCellOffset(t.name, out var off))
            {
                // Cell_0_0 → (0,0)
                // Cell_0_1 → (0,pitch)
                t.localPosition = new Vector3(
                    0f,
                    off.y * pitch,
                    t.localPosition.z
                );
            }
            else if (t.name.StartsWith("Visual"))
            {
                visual = t;
            }
        }

        if (visual != null)
        {
            visual.localPosition = new Vector3(
                visual.localPosition.x,
                pitch * 0.5f,
                visual.localPosition.z
            );
        }
    }
    // ---------------- B3 ----------------
    private void ApplyB3(PieceSimple piece, float pitch)
    {
        Transform visual = null;

        foreach (Transform t in piece.transform)
        {
            if (TryCellOffset(t.name, out var off))
            {
                // Cell_0_0 → (0,0)
                // Cell_1_0 → (pitch,0)
                t.localPosition = new Vector3(
                   off.x * pitch,
                   0f,
                    t.localPosition.z
                );
            }
            else if (t.name.StartsWith("Visual"))
            {
                visual = t;
            }
        }

        if (visual != null)
        {
            visual.localPosition = new Vector3(
                 pitch * 0.5f,
                  0,
               
                visual.localPosition.z
            );
        }
    }
    // ---------------- B4 ----------------
    private void ApplyB4(PieceSimple piece, float pitch)
    {
        Transform visual = null;

        foreach (Transform t in piece.transform)
        {
            if (TryCellOffset(t.name, out var off))
            {
                // Explicit positions:
                // Cell_0_0 → (0,0)
                // Cell_0_1 → (0,pitch)
                // Cell_1_1 → (pitch,pitch)
                t.localPosition = new Vector3(
                    -off.x * pitch,
                    off.y * pitch,
                    t.localPosition.z
                );
            }
            else if (t.name.StartsWith("Visual"))
            {
                visual = t;
            }
        }

        if (visual != null)
        {
            visual.localPosition = new Vector3(
                -pitch * 0.5f,
                pitch * 0.5f,
                visual.localPosition.z
            );
        }
    }

    // ---------------- B5 ----------------
    private void ApplyB5(PieceSimple piece, float pitch)
    {
        Transform visual = null;

        foreach (Transform t in piece.transform)
        {
            if (TryCellOffset(t.name, out var off))
            {
                // Explicit positions:
                // Cell_0_0 → (0,0)
                // Cell_0_1 → (0,pitch)
                // Cell_1_1 → (pitch,pitch)
                t.localPosition = new Vector3(
                    off.x * pitch,
                    off.y * pitch,
                    t.localPosition.z
                );
            }
            else if (t.name.StartsWith("Visual"))
            {
                visual = t;
            }
        }

        if (visual != null)
        {
            visual.localPosition = new Vector3(
                pitch * 0.5f,
                pitch * 0.5f,
                visual.localPosition.z
            );
        }
    }


    // ---------------- B6 ----------------
    private void ApplyB6(PieceSimple piece, float pitch)
    {
        Transform visual = null;

        foreach (Transform t in piece.transform)
        {
            if (TryCellOffset(t.name, out var off))
            {
                // Explicit positions:
                // Cell_0_0 → (0,0)
                // Cell_1_0 → (0,pitch)
                // Cell_1_1 → (pitch,pitch)
                t.localPosition = new Vector3(
                    off.x * pitch,
                    off.y * pitch,
                    t.localPosition.z
                );
            }
            else if (t.name.StartsWith("Visual"))
            {
                visual = t;
            }
        }

        if (visual != null)
        {
            visual.localPosition = new Vector3(
                pitch * 0.5f,
                pitch * 0.5f,
                visual.localPosition.z
            );
        }
    }

    // ---------------- B7 ----------------
    private void ApplyB7(PieceSimple piece, float pitch)
    {
        Transform visual = null;

        foreach (Transform t in piece.transform)
        {
            if (TryCellOffset(t.name, out var off))
            {
                // Explicit positions:
                // Cell_0_0 → (0,0)
                // Cell_1_0 → (0,pitch)
                // Cell_0_1 → (pitch,pitch)
                t.localPosition = new Vector3(
                    off.x * pitch,
                    off.y * pitch,
                    t.localPosition.z
                );
            }
            else if (t.name.StartsWith("Visual"))
            {
                visual = t;
            }
        }

        if (visual != null)
        {
            visual.localPosition = new Vector3(
                pitch * 0.5f,
                pitch * 0.5f,
                visual.localPosition.z
            );
        }
    }



    // ---------------- helper ----------------
    private bool TryCellOffset(string name, out Vector2Int offset)
    {
        offset = default;

        if (string.IsNullOrEmpty(name)) return false;
        if (!name.StartsWith("Cell")) return false;

        // Accepts: Cell_0_1, Cell(1,0), Cell1_0
        string n = name
            .Replace("Cell", "")
            .Replace("(", "")
            .Replace(")", "")
            .Replace(",", "_");

        var parts = n.Split('_');
        if (parts.Length < 2) return false;

        if (int.TryParse(parts[parts.Length - 2], out int x) &&
            int.TryParse(parts[parts.Length - 1], out int y))
        {
            offset = new Vector2Int(x, y);
            return true;
        }

        return false;
    }
}
