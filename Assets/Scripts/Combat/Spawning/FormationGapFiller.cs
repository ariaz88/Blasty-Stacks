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
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.6f;

    [Tooltip("Beat to wait after landing before stepping off, so the rank reads as " +
             "'land, notice the gap, then move' instead of sliding the instant it touches down.")]
    [SerializeField, Min(0f)] private float preMoveDelay = 0.4f;

    [Tooltip("How close counts as arrived.")]
    [SerializeField, Min(0.01f)] private float arriveEpsilon = 0.05f;

    [Tooltip("Safety cap so a blocked mover can never walk forever.")]
    [SerializeField, Min(0.5f)] private float moveTimeout = 6f;

    [Header("Debug")]
    [SerializeField] private bool logCompaction = false;

    // Heroes currently walking into a gap, mapped to the slot they RESERVED.
    // Storing the destination (not just "is moving") is essential: a later
    // compaction pass must treat that slot as TAKEN, otherwise it hands the same
    // gap to a second hero and the two end up permanently stacked.
    private readonly Dictionary<PlayerManager, Vector2Int> reserved = new();

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

        // Heroes sitting on a slot somebody else already owns - they are STACKED.
        // They stay eligible to move, but ONLY FORWARD like everyone else (see
        // the note on same-row moves below), paired with the row they sit in.
        var displaced = new List<(PlayerManager pm, int row)>();

        // 1a) Reserved destinations first. A hero still walking owns its target
        //     slot, so nobody else may be sent there.
        CleanUpDeadReservations();
        foreach (var kv in reserved)
        {
            var slot = kv.Value;
            if (slot.x < 0 || slot.x >= rows || slot.y < 0 || slot.y >= cols) continue;
            occupants[slot.x, slot.y] = kv.Key;
        }

        // 1b) Snap every other hero in the field onto its nearest slot.
        foreach (var pm in FindObjectsOfType<PlayerManager>())
        {
            if (!pm || !pm.isUnlocked) continue;   // still waiting on the gate
            if (reserved.ContainsKey(pm)) continue; // already placed in 1a

            if (!TryFindNearestSlot(pm.transform.position, rows, cols, out int r, out int c))
                continue;

            if (occupants[r, c] == null) occupants[r, c] = pm;
            else displaced.Add((pm, r));   // stacked - still eligible, forward only
        }

        // 2) Front rank first, so vacated slots cascade backwards.
        int filled = 0;
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                if (occupants[r, c] != null) continue;

                Vector3 gap = SlotPosition(r, c);

                PlayerManager mover;
                int mr = -1, mc = -1;

                // MOVES ARE FORWARD-ONLY, deliberately.
                // Both searches below require the mover to sit in a row BEHIND this
                // gap, so a hero never shuffles sideways within its own rank and
                // never steps backwards. Without this rule a settled front rank
                // visibly re-arranged itself every time a later wave landed, which
                // read as the formation randomly reshuffling.
                // A stacked hero in the FRONT row therefore cannot be fixed here at
                // all - that is intentional; CrowdSeparation2D nudges it apart.
                if (!TryTakeNearestDisplaced(displaced, r, gap, out mover) &&
                    !TryTakeNearestFromBehind(occupants, rows, cols, r, gap, out mover, out mr, out mc))
                    continue;

                if (mr >= 0) occupants[mr, mc] = null;   // vacate - a rank further back may take it
                occupants[r, c] = mover;                 // claim the gap

                reserved[mover] = new Vector2Int(r, c);  // hold it until arrival
                StartCoroutine(WalkTo(mover, gap, new Vector2Int(r, c)));
                filled++;

                if (logCompaction)
                    Debug.Log($"[FormationGapFiller] {mover.name}: " +
                              (mr >= 0 ? $"row{mr}col{mc}" : "stacked") + $" -> row{r}col{c}", mover);
            }
        }

        if (logCompaction)
            Debug.Log($"[FormationGapFiller] compaction filled {filled} gap(s).", this);
    }

    /// <summary>
    /// Nearest hero from the stacked pile, removed from that pile if found.
    /// These are pulled out before anyone else, because leaving them stacked is
    /// the exact defect this pass exists to repair.
    /// </summary>
    private bool TryTakeNearestDisplaced(List<(PlayerManager pm, int row)> displaced,
                                         int targetRow, Vector3 gap, out PlayerManager best)
    {
        best = null;
        int bestIndex = -1;
        float bestDist = float.MaxValue;

        for (int i = 0; i < displaced.Count; i++)
        {
            var entry = displaced[i];
            if (entry.pm == null) continue;

            // Forward-only: the stacked hero must be BEHIND the gap it fills.
            if (entry.row <= targetRow) continue;

            float d = ((Vector2)entry.pm.transform.position - (Vector2)gap).sqrMagnitude;
            if (d >= bestDist) continue;

            bestDist = d; best = entry.pm; bestIndex = i;
        }

        if (bestIndex < 0) return false;

        displaced.RemoveAt(bestIndex);
        return true;
    }

    /// <summary>Drops reservations whose hero was destroyed, so slots are not leaked.</summary>
    private void CleanUpDeadReservations()
    {
        var dead = new List<PlayerManager>();
        foreach (var kv in reserved)
            if (kv.Key == null) dead.Add(kv.Key);

        foreach (var d in dead) reserved.Remove(d);
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

    private IEnumerator WalkTo(PlayerManager pm, Vector3 target, Vector2Int slot)
    {
        // Hold still for a beat first. The hero already holds its reservation, so
        // the slot stays protected for the whole pause.
        if (preMoveDelay > 0f)
            yield return new WaitForSeconds(preMoveDelay);

        if (pm == null)
        {
            CleanUpDeadReservations();
            yield break;
        }

        // Play the walk cycle for the step. The arrival block below puts the
        // hero back to idle.
        pm.SetAnimMoving(true);

        // Start the timeout AFTER the pause, so the delay never eats into the
        // time budget the walk itself is allowed.
        float t0 = Time.time;

        while (pm != null &&
               Vector2.Distance(pm.transform.position, target) > arriveEpsilon &&
               Time.time - t0 < moveTimeout)
        {
            Vector3 toTarget = target - pm.transform.position;
            float remaining = ((Vector2)toTarget).magnitude;

            Vector2 dir = ((Vector2)toTarget).normalized;

            // Arc AROUND an ally standing in the way rather than walking through
            // them. This is one of the only two places avoidance is wanted: the
            // pre-battle formation. It is skipped on the last stretch so the unit
            // always settles exactly on its slot instead of circling it.
            if (CrowdSeparation2D.Instance != null && remaining > arriveEpsilon * 4f)
                dir = CrowdSeparation2D.Instance.SteerAroundBlockers(pm.transform, dir);

            // Face the way we are stepping, using the manager's own flip logic so
            // the mirrored-parent handling stays in one place.
            if (Mathf.Abs(dir.x) > 0.02f) pm.FaceLeft(dir.x < 0f);

            // Never overshoot the slot, even when the swerve lengthens the path.
            float step = Mathf.Min(moveSpeed * Time.deltaTime, remaining);

            Vector3 next = pm.transform.position + (Vector3)(dir * step);
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
        }

        // Release the reservation only now. Until this point the slot stayed
        // reserved, so no second hero could ever be sent to it.
        if (pm != null) reserved.Remove(pm);
        CleanUpDeadReservations();
    }
}
