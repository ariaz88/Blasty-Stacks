// FormationGapFiller.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the heroes in the field packed into a tidy formation.
///
/// A wave spawns 1-3 heroes across 4 gate slots, so a rank usually lands with
/// holes in it. After every wave lands, this runs a compaction pass: each hole
/// in a FORWARD rank is filled by the nearest hero standing in a rank BEHIND it,
/// who walks there at normal speed and stops.
///
/// It CASCADES by design. Rows are processed front-to-back and a mover's old
/// slot is freed the moment it is reassigned, so a row-3 hero can step into the
/// slot a row-2 hero just vacated on its way to row 1.
///
/// The grid is derived, never hard-coded: columns come from the gate stages'
/// X positions (FrogJumpTransformOnly preserves X, so heroes keep their gate
/// column), rows come from the jump lanes' Y positions.
/// </summary>
public class FormationGapFiller : MonoBehaviour
{
    [Header("Grid")]
    [Tooltip("The gate stages. Only their X is used - these are the formation columns.")]
    [SerializeField] private Transform[] columnAnchors;

    [Tooltip("The jump lanes, FRONT FIRST (index 0 = closest to the enemy). Only Y is used.")]
    [SerializeField] private Transform[] rowLanes;

    [Header("Movement")]
    [Tooltip("Units per second while stepping into a gap.")]
    [SerializeField, Min(0.1f)] private float moveSpeed = 2f;

    [Tooltip("How close counts as arrived.")]
    [SerializeField, Min(0.01f)] private float arriveEpsilon = 0.05f;

    [Tooltip("Safety cap so a blocked mover can never walk forever.")]
    [SerializeField, Min(0.5f)] private float moveTimeout = 6f;

    [Header("Debug")]
    [SerializeField] private bool logCompaction = false;

    // Heroes currently walking into a gap - never reassigned mid-move.
    private readonly HashSet<PlayerManager> moving = new();

    /// <summary>
    /// Repacks the formation. Safe to call after every wave lands; heroes already
    /// walking are left alone.
    /// </summary>
    public void Compact()
    {
        if (columnAnchors == null || columnAnchors.Length == 0 ||
            rowLanes == null || rowLanes.Length == 0)
        {
            Debug.LogWarning("[FormationGapFiller] Grid not configured - nothing to compact.", this);
            return;
        }

        int rows = rowLanes.Length;
        int cols = columnAnchors.Length;

        // occupants[row, col] - null means a hole.
        var occupants = new PlayerManager[rows, cols];

        // 1) Snap every hero that is actually in the field onto its nearest slot.
        foreach (var pm in FindObjectsOfType<PlayerManager>())
        {
            if (!pm || !pm.isUnlocked) continue;   // still waiting on the gate
            if (moving.Contains(pm)) continue;     // already has a destination

            if (!TryFindNearestSlot(pm.transform.position, rows, cols, out int r, out int c))
                continue;

            // If two heroes land on the same slot, the first one keeps it and the
            // second is treated as free to be reassigned below.
            if (occupants[r, c] == null) occupants[r, c] = pm;
        }

        // 2) Front rank first, so vacated slots cascade backwards.
        int filled = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (occupants[r, c] != null) continue;

                Vector3 gap = SlotPosition(r, c);

                if (!TryTakeNearestFromBehind(occupants, rows, cols, r, gap,
                                              out var mover, out int mr, out int mc))
                    continue;

                occupants[mr, mc] = null;      // vacate - a rank further back may take it
                occupants[r, c] = mover;       // claim the gap

                moving.Add(mover);
                StartCoroutine(WalkTo(mover, gap));
                filled++;

                if (logCompaction)
                    Debug.Log($"[FormationGapFiller] {mover.name}: row{mr}col{mc} -> row{r}col{c}", mover);
            }
        }

        if (logCompaction)
            Debug.Log($"[FormationGapFiller] compaction filled {filled} gap(s).", this);
    }

    /// <summary>
    /// Nearest hero sitting in a rank BEHIND <paramref name="targetRow"/>.
    /// Rows are front-first, so "behind" means a HIGHER row index.
    /// </summary>
    private bool TryTakeNearestFromBehind(PlayerManager[,] occupants, int rows, int cols,
                                          int targetRow, Vector3 gap,
                                          out PlayerManager best, out int bestRow, out int bestCol)
    {
        best = null; bestRow = -1; bestCol = -1;
        float bestDist = float.MaxValue;

        for (int r = targetRow + 1; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var pm = occupants[r, c];
                if (pm == null) continue;

                float d = ((Vector2)pm.transform.position - (Vector2)gap).sqrMagnitude;
                if (d >= bestDist) continue;

                bestDist = d; best = pm; bestRow = r; bestCol = c;
            }
        }

        return best != null;
    }

    private bool TryFindNearestSlot(Vector3 worldPos, int rows, int cols, out int row, out int col)
    {
        row = col = -1;
        float best = float.MaxValue;

        for (int r = 0; r < rows; r++)
        {
            if (!rowLanes[r]) continue;
            for (int c = 0; c < cols; c++)
            {
                if (!columnAnchors[c]) continue;

                float d = ((Vector2)worldPos - (Vector2)SlotPosition(r, c)).sqrMagnitude;
                if (d >= best) continue;

                best = d; row = r; col = c;
            }
        }

        return row >= 0;
    }

    private Vector3 SlotPosition(int row, int col)
    {
        return new Vector3(columnAnchors[col].position.x, rowLanes[row].position.y, 0f);
    }

    private IEnumerator WalkTo(PlayerManager pm, Vector3 target)
    {
        float t0 = Time.time;

        while (pm != null &&
               Vector2.Distance(pm.transform.position, target) > arriveEpsilon &&
               Time.time - t0 < moveTimeout)
        {
            // Face the way we are stepping, using the manager's own flip logic so
            // the mirrored-parent handling stays in one place.
            float dx = target.x - pm.transform.position.x;
            if (Mathf.Abs(dx) > 0.02f) pm.FaceLeft(dx < 0f);

            Vector3 next = Vector3.MoveTowards(pm.transform.position, target, moveSpeed * Time.deltaTime);
            next.z = pm.transform.position.z;
            pm.transform.position = next;

            yield return null;
        }

        if (pm != null)
        {
            var snapped = target;
            snapped.z = pm.transform.position.z;
            pm.transform.position = snapped;
            pm.SetAnimMoving(false);
            moving.Remove(pm);
        }
    }
}
