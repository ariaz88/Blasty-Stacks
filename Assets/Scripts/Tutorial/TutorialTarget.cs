using System;
using UnityEngine;

/// <summary>
/// WHERE a tutorial step points. This is the only place in the tutorial system
/// that knows how to turn "the thing I mean" into a screen position, which is
/// what keeps the rest of it generic: a step never holds a scene reference.
///
/// Kinds:
///   BoardCell   - a cell on the puzzle BoardGridXY. Use cellOffset to aim at
///                 the CENTRE of a multi-cell stack (a vertical 2-cell piece
///                 anchored at (4,2) has its centre at cell (4,2) + (0, 0.5)).
///   SceneAnchor - any object tagged with a TutorialAnchor component. Works for
///                 both world objects and UI RectTransforms, so this is what a
///                 future "press THIS button" tutorial will use.
///   ScreenPoint - a normalised (0..1) screen position. Escape hatch.
///   WorldPoint  - a fixed world position.
/// </summary>
[Serializable]
public struct TutorialTarget
{
    public enum Kind
    {
        BoardCell = 0,
        SceneAnchor = 1,
        ScreenPoint = 2,
        WorldPoint = 3,
    }

    [Tooltip("How the target is addressed.")]
    public Kind kind;

    [Header("BoardCell")]
    [Tooltip("Cell on the BoardGridXY.")]
    public Vector2Int boardCell;

    [Tooltip("Extra offset IN CELLS from that cell's centre, in board space. " +
             "(0, 0.5) aims at the middle of a vertical 2-cell stack.")]
    public Vector2 cellOffset;

    [Header("SceneAnchor")]
    [Tooltip("anchorId of a TutorialAnchor somewhere in the loaded scene.")]
    public string anchorId;

    [Header("ScreenPoint / WorldPoint")]
    [Tooltip("Normalised screen position, (0,0) bottom-left .. (1,1) top-right.")]
    public Vector2 screenPoint01;

    public Vector3 worldPoint;

    [Header("All kinds")]
    [Tooltip("Final nudge in screen pixels, applied after resolving.")]
    public Vector2 pixelOffset;

    /// <summary>
    /// Resolves to a screen-space position. Returns false when the target
    /// cannot be resolved right now (no board, anchor not in the scene, ...),
    /// so the caller can warn instead of pointing the hand at (0,0).
    /// </summary>
    public bool TryResolveScreen(Camera worldCamera, BoardGridXY board, out Vector2 screenPos)
    {
        screenPos = default;

        switch (kind)
        {
            case Kind.BoardCell:
            {
                if (!board || !worldCamera) return false;

                Vector3 world = board.CellCenterWorld(boardCell);

                // cellOffset is expressed in cells, in the board's own space, so
                // it keeps working if the board is ever rotated or scaled.
                if (cellOffset != Vector2.zero)
                {
                    float pitch = board.CellPitch;
                    world += board.transform.right * (cellOffset.x * pitch)
                           + board.transform.up * (cellOffset.y * pitch);
                }

                screenPos = worldCamera.WorldToScreenPoint(world);
                break;
            }

            case Kind.WorldPoint:
            {
                if (!worldCamera) return false;
                screenPos = worldCamera.WorldToScreenPoint(worldPoint);
                break;
            }

            case Kind.SceneAnchor:
            {
                var anchor = TutorialAnchor.Find(anchorId);
                if (!anchor) return false;
                if (!anchor.TryGetScreenPosition(worldCamera, out screenPos)) return false;
                break;
            }

            case Kind.ScreenPoint:
            {
                screenPos = new Vector2(screenPoint01.x * Screen.width,
                                        screenPoint01.y * Screen.height);
                break;
            }

            default:
                return false;
        }

        screenPos += pixelOffset;
        return true;
    }

    /// <summary>Convenience for building targets from code (used by the scene builder).</summary>
    public static TutorialTarget Cell(int x, int y, float offsetX = 0f, float offsetY = 0f)
    {
        return new TutorialTarget
        {
            kind = Kind.BoardCell,
            boardCell = new Vector2Int(x, y),
            cellOffset = new Vector2(offsetX, offsetY)
        };
    }
}
