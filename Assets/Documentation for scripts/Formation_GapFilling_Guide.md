# Hero Formation & Gap Filling — Full Guide

**Date:** 2026-08-21
**Scene:** `Assets/Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_1.unity`
**Audience:** a developer who has never seen this system before.

Read this top to bottom once. Every design decision is explained, not just stated.

---

## 1. What problem are we solving?

When the player clears a match on the puzzle board, a **wave** of heroes is
released from the gate. Each wave picks **1 to 3** heroes at random, spread over
**4** gate slots.

That randomness is the whole problem:

* A wave almost never fills all 4 slots.
* So each rank of heroes lands in the field **with holes in it**.
* After a few waves the field looks like scattered dots, not an army.

What we want instead: when a new rank lands, it should **walk forward into the
holes** left by the ranks in front of it, so the formation stays packed.

And critically, it must **cascade** — if a hero from row 2 steps up into row 1,
the slot it just left is now empty, and somebody from row 3 should take *that*.

---

## 2. The key insight: we already had a grid

This is the part worth understanding, because the whole feature depends on it.

### 2a. Columns come for free

Look at how a hero jumps. In `SimpleJump2D.cs` (class `FrogJumpTransformOnly`):

```csharp
if (hasTargetYOverride)
{
    // Land exactly on the requested lane, whatever the distance.
    endPos = new Vector3(startPos.x, targetWorldY, startPos.z);
    //                   ^^^^^^^^^^
    //                   X IS PRESERVED. Only Y changes.
    hasTargetYOverride = false;
}
```

The jump **only changes Y**. A hero keeps the exact X it had while standing on
its gate slot.

The 4 gate slots sit at fixed X positions, so heroes automatically line up into
**4 vertical columns**:

| column | 0 | 1 | 2 | 3 |
|---|---|---|---|---|
| **world X** | −2.08 | −0.70 | +0.69 | +2.09 |

Nobody designed this on purpose — it falls out of "the jump only moves Y". But
it means we get columns for free.

### 2b. Rows are the jump lanes

Each wave is told which lane to land on. `PlayerWaveManager` holds:

```csharp
[Header("Jump Lanes")]
[Tooltip("Where each successive wave LANDS. 1st match -> element 0, 2nd -> element 1, " +
         "and so on, so waves form ranks instead of piling onto each other. " +
         "Leave empty to keep the old fixed-distance jump.")]
[SerializeField] private Transform[] jumpLanes;
```

and resolves one lane per wave:

```csharp
/// <summary>
/// The world Y the next wave should land on, or null to keep the jumper's
/// own fixed-distance behaviour.
/// </summary>
private float? GetLaneYForNextWave()
{
    if (jumpLanes == null || jumpLanes.Length == 0) return null;

    int index = waveUnlockIndex;
    if (index >= jumpLanes.Length)
    {
        if (!reuseLastLane) return null;
        index = jumpLanes.Length - 1;   // wave 5+ all stack on the last lane
    }

    var lane = jumpLanes[index];
    return lane ? lane.position.y : (float?)null;
}
```

Those lanes are 4 GameObjects under `Jump positions` in the scene:

| row | object | world Y | meaning |
|---|---|---|---|
| 0 | `Jump pos 1` | 7.75 | **front** — closest to the enemy |
| 1 | `Jump pos 2` | 6.61 | |
| 2 | `Jump pos 3` | 5.47 | |
| 3 | `Jump pos 4` | 4.33 | **back** — closest to our own gate |

> **Watch out for this — it trips people up.**
> `rowLanes[0]` is the row **furthest from our gate** and **nearest the enemy**.
> Throughout the code, *"forward"* / *"ahead"* means a **lower** row index and a
> **higher** world Y. *"Behind"* means a **higher** row index.

### 2c. Put them together

Columns (X) × rows (Y) = a **4 × 4 grid of 16 slots**. Every hero in the field
is standing on, or very near, one of these slots. That turns a vague art problem
("make it look tidy") into a simple data problem ("fill the empty cells").

---

## 3. The algorithm, in plain words

Every time a wave finishes landing:

1. **Re-claim reserved slots.** Every hero still *walking* into a gap already owns
   its destination. Mark those slots taken first, before anything else, so no
   second hero can be sent to the same place. (This is the fix from §8a — read it.)
2. **Build the rest of the occupancy map.** For every other hero in the field,
   work out which of the 16 slots it is closest to and mark that slot taken.
   If a slot is *already* taken, the newcomer is **stacked** — it goes into a
   `displaced` list instead of being silently dropped.
3. **Walk the grid front-to-back** (row 0, then 1, 2, 3).
4. For each **empty** slot, find a mover, preferring in this order:
   * the nearest **stacked** hero sitting in a row behind the gap — they are
     pulled out first, because being stacked is the exact defect this pass exists
     to repair;
   * otherwise the nearest hero **standing in any row behind it** — same column or
     not, diagonal is fine.
5. If one is found: mark its old slot **empty**, mark the gap **taken**, record
   the **reservation**, and send it walking there.
6. Continue.

**Why front-to-back matters.** Because we free a mover's old slot *as we go*, and
we haven't reached that row yet, a hero further back will naturally consider it
when we get there. That is the cascade — we get it for free from the loop order,
with no special recursion.

**Moves are forward-only, deliberately.** Both searches in step 4 require the
mover to sit in a row *behind* the gap. A hero never shuffles sideways within its
own rank and never steps backwards. Without this rule a settled front rank
visibly re-arranged itself every time a later wave landed, which read as the
formation randomly reshuffling. The cost of the rule: a stacked hero in the
**front** row cannot be repaired by this pass at all. That is accepted —
`CrowdSeparation2D` nudges it apart instead.

### A worked example

This is the exact case from the screenshot (`..` = empty):

```
BEFORE                      AFTER
row0: A  ..  B  C           row0: A  E  B  C
row1: D  E   F  G     -->   row1: D  H  F  G
row2: H  ..  ..  I          row2: ..  ..  ..  I
row3: .. ..  ..  ..         row3: ..  ..  ..  ..
```

Step by step:

* Row 0, col 1 is empty. Nearest hero behind it is **E** at row 1 col 1
  (directly below). → `E: row1col1 -> row0col1`. Row 1 col 1 is now free.
* Row 1, col 1 is now empty. Nearest hero behind is **H** at row 2 col 0
  (diagonally). → `H: row2col0 -> row1col1`.
* Result: two gaps filled, and the second one only existed *because* of the
  first. **That is the cascade.**

---

## 4. The code

### 4a. `FormationGapFiller.cs` — the component

Path: `Assets/Scripts/Combat/Spawning/FormationGapFiller.cs`

```csharp
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
```

#### Why each guard exists — read this before "simplifying" anything

| Line | Why it is there |
|---|---|
| `if (!pm.isUnlocked) continue;` | Heroes still standing on the gate must be ignored. Without this the algorithm would try to drag a hero off the gate before it has jumped. |
| `if (reserved.ContainsKey(pm)) continue;` | A hero already walking was placed in step 1a from its **reservation**, not its current position. Re-snapping it mid-walk would register the slot it is passing over rather than the one it owns. |
| Reservations applied **before** the position snap (1a before 1b) | This is the §8a fix. A walking hero's *destination* must read as occupied, or the next pass hands the same gap to somebody else. |
| `else displaced.Add((pm, r));` | Two heroes can end up nearest the same slot. The extra one must stay **visible** to the algorithm — the earlier version dropped it, so a stack could never repair itself. |
| `if (entry.row <= targetRow) continue;` | Forward-only. Stacked heroes are still not allowed to move sideways or backwards; see §3. |
| `sqrMagnitude` instead of `Distance` | We only ever *compare* distances, never display them. Skipping the square root in a double loop is free performance. |
| `moveTimeout` | If a hero gets stuck on a collider, the coroutine must still end. Without it, that hero would hold its reservation forever and its slot would be leaked. |
| `CleanUpDeadReservations()` | A hero destroyed mid-walk would otherwise keep its slot reserved permanently, leaving a hole nobody may fill. |
| `next.z = pm.transform.position.z;` | We only reposition on X/Y. Overwriting Z would break 2D sprite draw order. |
| `pm.FaceLeft(dir.x < 0f)` | We deliberately do **not** set `localScale` here. `FaceLeft` contains the mirrored-parent correction; duplicating the flip locally would reintroduce the backwards-facing bug. |
| `pm.SetAnimMoving(true)` before the loop | Without it the hero glides into the gap playing its **idle** animation. The arrival block sets it back to `false`. |
| `remaining > arriveEpsilon * 4f` around the steer call | Avoidance is skipped on the last stretch so the unit settles exactly on its slot instead of circling it. |
| `Mathf.Min(moveSpeed * Time.deltaTime, remaining)` | The swerve lengthens the path; clamping the step to what is left stops the hero overshooting its slot. |

### 4b. `PlayerWaveManager.cs` — the trigger

**Added field:**

```csharp
[Tooltip("Repacks the formation after each rank lands. Left empty = found in " +
         "the scene at Awake; none in the scene = no gap filling.")]
[SerializeField] private FormationGapFiller gapFiller;
```

**Added to `Awake()`:**

```csharp
if (!gapFiller) gapFiller = FindObjectOfType<FormationGapFiller>(true);
```

**Added at the end of `UnlockCurrentWaveViaAnimation()`,** right after every
hero of the wave has been sent off to jump:

```csharp
// Once the whole rank is down, repack the formation so this wave steps
// into any holes the ranks ahead of it left behind.
StartCoroutine(CompactWhenWaveHasLanded(new List<PlayerManager>(currentWave)));
```

**And the new coroutine:**

```csharp
/// <summary>
/// Waits for every hero of a wave to finish its jump, then runs one
/// formation compaction pass for the whole rank.
/// </summary>
private IEnumerator CompactWhenWaveHasLanded(List<PlayerManager> wave)
{
    if (!gapFiller) yield break;

    float t0 = Time.time;

    // Everyone jumps together, so waiting on the slowest is enough.
    bool StillJumping()
    {
        foreach (var pm in wave)
        {
            if (pm == null) continue;
            var j = pm.GetComponent<FrogJumpTransformOnly>();
            if (j != null && j.IsJumping) return true;
        }
        return false;
    }

    while (StillJumping() && Time.time - t0 < jumpWaitTimeout + 1f)
        yield return null;

    gapFiller.Compact();
}
```

#### Two subtle things here

1. **`new List<PlayerManager>(currentWave)` — the copy is mandatory.**
   `currentWave` is a single reusable list that `SpawnLockedWave` calls
   `.Clear()` on for the *next* wave. If we passed the list itself, our
   coroutine would be watching a list that gets emptied out from under it. We
   copy so this coroutine owns a stable snapshot of *its* wave.

2. **We compact once per wave, not once per hero.**
   The trigger lives outside the `foreach` loop. Running it per hero would
   compute the grid 3× for a 3-hero wave and could hand the same gap to two
   different heroes.

---

## 5. How it is wired in the scene

`FormationGapFiller` is on the **same GameObject as `PlayerWaveManager`**.

| field | wired to | why |
|---|---|---|
| `columnAnchors` | the 4 `Deploy Stage` transforms (same objects as `PlayerWaveManager.gatePoints`) | Only X is read. These *are* the columns, because the jump preserves X. |
| `rowLanes` | the same 4 transforms as `jumpLanes` | Only Y is read. Sharing the objects means moving a lane moves the jump target **and** the formation row together — they can never drift apart. |
| `gapFiller` on `PlayerWaveManager` | the component itself | |

**Nothing is hard-coded.** If you drag `Jump pos 2` somewhere else in the Scene
view, both the jump and the formation follow it automatically. That was a
deliberate choice — an earlier version of the battle camera *did* hard-code a
position and it silently broke as soon as the scene moved.

---

## 6. Design decisions and their reasons

| Decision | Why |
|---|---|
| **Nearest gap in any direction** (diagonals allowed) rather than straight-up-only | Vertical-only leaves a permanent hole in any column that has nobody behind it. Allowing diagonals packs the formation properly. This was an explicit product choice. |
| **Gap filling runs during the puzzle phase too**, not only after BATTLE | The formation should already look organised while the player is still solving the board. Note this is the *one* movement allowed before battle — advancing toward the enemy is still gated behind BATTLE + first enemy spawned. |
| Grid **derived from scene transforms**, not constants | Constants rot the moment an artist moves something. |
| Compaction triggered **after landing**, not during the jump | A hero mid-air has a meaningless position, so slot-snapping would pick the wrong cell. |
| **Moves are forward-only** — a mover must sit in a row behind the gap | Allowing sideways or backwards moves made a settled front rank visibly re-arrange itself every time a later wave landed, which read as the formation randomly reshuffling. |
| **Reserve the slot, not the worker** | Tracking only "is this hero busy?" let a second hero be sent to a destination already claimed. See §8a. |
| Movement by **`transform.position`**, not physics forces | These heroes are not in a physics-driven state here, and we want an exact, predictable stop on the slot. Forces would overshoot. |

---

## 7. Tuning and debugging

On the `FormationGapFiller` component:

* **`Move Speed`** (default `1.6`) — units/second while stepping into a gap. This
  is the number most likely to need tuning by feel.
* **`Pre Move Delay`** (default `0.4`) — a beat held after landing before the
  hero steps off, so the rank reads as *land → notice the gap → move* rather
  than sliding the instant it touches down. The hero is already registered in
  `reserved` during this pause, so its destination slot cannot be handed to another
  while it waits. The move timeout starts **after** the pause, so the delay
  never eats into the walk's time budget.
* **`Arrive Epsilon`** (default `0.05`) — how close counts as arrived.
* **`Move Timeout`** (default `6`) — safety cap for a blocked mover.
* **`Log Compaction`** — turn this **on** first when something looks wrong. It
  prints one line per move, e.g.
  `[FormationGapFiller] Player_Valkyrie(Clone): row1col1 -> row0col1`
  (a mover pulled out of the stacked pile logs `stacked -> row0col1` instead),
  plus a summary count. That tells you instantly whether the algorithm decided
  nothing (grid/wiring problem) or decided something wrong (tuning problem).

---

## 8a. BUG FOUND AND FIXED — heroes stacking on one slot (2026-08-21)

**Symptom.** Two heroes walked into the *same* slot and stayed permanently
overlapped, one sprite drawn on top of the other. Caught on video during
playtesting.

**Cause.** The first version tracked walking heroes in a `HashSet` and skipped
them while rebuilding the occupancy map:

```csharp
if (moving.Contains(pm)) continue;   // BUG
```

Skipping the hero meant **its destination slot looked empty** to the next
compaction pass. So when the next wave landed, that pass cheerfully handed the
same gap to a second hero. Both walked there. Both stayed.

A second flaw fed the same symptom: when two heroes snapped to one slot, the
extra one was silently dropped from `occupants`. It then became invisible to the
algorithm — never a mover, never counted — so a stack could never repair itself.

**Fix — two parts:**

1. `moving` (a `HashSet`) became `reserved` (a `Dictionary<PlayerManager,
   Vector2Int>`), storing **which slot** each walking hero is heading to. Those
   slots are marked occupied at the start of every pass, so a destination is
   protected from the moment it is chosen until the hero arrives.
2. Heroes that snap onto an already-owned slot go into a `displaced` list and are
   pulled into the next available gap **first**, since being stacked is exactly
   the defect this system exists to remove.

**Lesson worth remembering.** "Is this unit busy?" was not enough state.
The algorithm needed "**what has this unit claimed?**". Any time work is handed
out asynchronously, reserve the *resource*, not just the *worker*.

---

## 8. Known limits — please read before extending

* **Avoidance is path-only, and only between allies.** While walking into a gap a
  hero bends its route around an ally standing in the way (`CrowdSeparation2D.
  SteerAroundBlockers`) and straightens out again. Standing units are never
  pushed apart, and enemies are never avoided — see the design rule at the top of
  `CrowdSeparation2D.cs` for why both of those were removed rather than fixed.
* **A stacked hero in the front row cannot be repaired by compaction.** Moves are
  forward-only and there is no row ahead of row 0 to pull it into. It is left to
  path avoidance to separate.
* **Compaction only runs on wave landing.** If a hero **dies** and leaves a hole,
  nothing repacks the formation. If you want that, call `gapFiller.Compact()`
  from wherever a player death is handled.
* **Wave 5 and beyond** all land on the last lane (`reuseLastLane`), so they
  arrive already stacked on row 3 and only spread out via gap filling.
* **Play-mode state.** The stacking bug in §8a was caught on video in Play mode
  and fixed. The `SetAnimMoving(true)` fix listed in the guard table has *not*
  been confirmed live yet. Expect `Move Speed` to still need tuning by feel.

---

## 9. Related files

| File | Role |
|---|---|
| `Assets/Scripts/Combat/Spawning/FormationGapFiller.cs` | the feature itself |
| `Assets/Scripts/Combat/Spawning/PlayerWaveManager.cs` | picks the lane per wave, triggers compaction |
| `Assets/Scripts/Combat/Shared/CrowdSeparation2D.cs` | `SteerAroundBlockers` — bends a walk around an ally in the way |
| `Assets/Scripts/Combat/Player/SimpleJump2D.cs` | `TriggerJumpTo` — the X-preserving jump that creates the columns |
| `Assets/Scripts/Combat/Player/PlayerManager.cs` | `FaceLeft` — mirrored-parent-safe sprite flipping; `SetAnimMoving` — walk/idle blend |
| `Assets/Documentation for scripts/PlayerWaveManager.txt` | per-script reference |
