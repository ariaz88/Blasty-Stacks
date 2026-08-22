# Unit Avoidance & Non-Collision — every attempt, every result

**Date:** 2026-08-21
**Scene:** `Assets/Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_1.unity`
**Topic name:** *unit avoidance* — units never collide or push, but a moving unit
routes **around** an ally standing in its way.

This is the full history: what was tried, what each attempt actually produced,
why it failed, and what the final version has that none of the earlier ones did.
Read §2 if you only want the rules, §9 if you only want the summary table.

---

## 1. The one-sentence rule

> Units have **zero interaction** with each other — no collision, no pushing, no
> reaction when they meet or overlap. The **only** exception: while a unit is
> **walking**, it steers around an **ally** that is in its path, and keeps a small
> personal space. A unit that has stopped is never touched by anything.

---

## 2. Starting state — what was already true before any of this

This matters, because the first attempt was built on a wrong assumption.

**There was never any physics collision between units.** Verified from the live
project, not assumed:

```
Player vs Player : IGNORED (no collision)
Player vs Enemy  : IGNORED (no collision)
Enemy  vs Enemy  : IGNORED (no collision)
prefabs on the wrong layer : 0
stray solid colliders      : 0
```

`IgnoreSameLayerContacts2D` sits on the prefabs as a second guard on top of the
layer matrix.

So the reported "they push each other" was **never physics**. It was units
converging on the same point with nothing keeping them apart, then overlapping
and jittering as their movement code fought over that point.

There were also **two older separation systems** already inside `PlayerManager`,
both broken, both fighting each other:

| Old system | What it did | Why it was broken |
|---|---|---|
| `ApplyFriendlySeparation` | bent the **move direction** away from neighbours | ended with `(desiredDir + dodge.normalized).normalized` — the `.normalized` **threw away every strength and crowd multiplier computed in the 6 lines above it**, so the dodge was always a full-strength sideways shove. It also derailed pursuit, because a crowded unit walked sideways instead of at its target. |
| `ResolveHorizontalOverlap` | `MovePosition` every `FixedUpdate` | ran **unconditionally** — even mid-jump and while locked on the gate. And with two units at the **same x**, `pushDir = (dx >= 0f) ? 1f : -1f` gave **both** `+1`, so they slid right **together**, forever, never separating. |

---

## 3. Attempt 1 — a continuous separation push

**Idea:** classic boids-style separation. Every `LateUpdate`, each unit
accumulates a push away from neighbours closer than `desiredSpacing`, biased
sideways so it does not get shoved off its objective.

**Simulated result — looked perfect:**

```
3 units stacked on one point at the gate + a 4th arriving
after 4s -> closest pair = 0.602  (target 0.6)   PASS
```

**Actual result in game: NOTHING CHANGED AT ALL.**

**Why:** `ResolveHorizontalOverlap` was still running `MovePosition` every
`FixedUpdate`. Physics runs *after* `LateUpdate`, so it overwrote the new
system's work every single step. The new system was running; it was being erased.

**Lesson:** the very first thing to do was search for an existing implementation.
Adding a second system on top of an unknown first one wasted a whole round.

---

## 4. Attempt 2 — delete the old systems, keep the push

Removed both old systems (112 lines + 5 serialized fields), leaving the new
separation push as the single owner of spacing.

**Result: heroes ORBITED the enemies instead of fighting them.**

**Why:** the separation had `sameTeamOnly = false`, on the reasoning that
"attackers should not stand inside enemies". That was wrong. The push shoved a
hero **away from the enemy** the instant it got close enough to attack:

```
approach -> pushed back out -> approach -> pushed back out -> ... = orbit
```

**Second, deeper problem:** even same-team-only, a continuous shove *is* an
interaction. The requirement was that standing units are left completely alone.
So the whole "push" approach was the wrong shape, not just mistuned.

---

## 5. Attempt 3 — zero interaction + look-ahead avoidance + attack slots

Three changes together:

1. **Deleted the separation push entirely.** `CrowdSeparation2D` lost its
   `LateUpdate`. Standing units are now never moved by anything.
2. **Look-ahead path avoidance** (`SteerAroundBlockers`) — predictive, not
   reactive: probe ahead, and bend around an ally *before* contact.
3. **`AttackSlotRegistry`** — instead of pushing attackers apart at a shared
   target, each claims **its own spot once on arrival** and walks there. Choosing
   a spot on arrival is not an interaction; a continuous shove is.

**Result: heroes FROZE solid in front of the enemy.** Caught live:

```
dist to dest  = 0.046   <<< below the 0.05 arrival threshold -> STOPS DEAD
dist to enemy = 1.57    > maxAttackRange 0.85 -> state machine keeps "pursuing"
velocity      = (0.000, 0.000)
```

**Why — a genuine deadlock between two systems measuring different things:**

* `HandleMoveToTarget` walked to the **attack slot** and reported "arrived".
* `PlayerPursueTargetState` measured distance to the **enemy** and said "too far,
  keep pursuing".

Neither could ever progress. The cause was that the slot offset pushed the
destination **radially outward**: the anchor sat 0.60 from the enemy (inside the
0.85 range), but a `+0.90` slot moved it to **1.57**, and `+1.80` to **2.45**.

**Fix — arc slots.** Attackers now fan out **around** the target on an arc whose
radius is capped by attack range:

```
radius = min(anchorDistance, maxAttackRange * 0.8)
angle  = 0, +28, -28, +56, -56 ...   degrees
```

```
hero      slot   OLD dest / dist      NEW dest / dist     in range?
CowMino1   1     (1.90,11.67) / 1.53  (0.74,11.88) / 0.68   YES
CowMino2   2     (-1.14,11.67)/ 1.61  (0.07,11.86) / 0.68   YES
Valkyrie   3     (2.80,11.67) / 2.41  (0.42,11.96) / 0.68   YES
closest pair of attack spots = 0.33   (still distinct)
```

> **INVARIANT — do not break this again.**
> An attack spot must **always** be within `maxAttackRange` of the target.
> The pursue state and the mover must agree on when a unit has arrived, or they
> deadlock. This is written into the code comment at `ResolveAttackDestination`.

---

## 6. Attempt 4 — context-limited avoidance

The detour was restricted to two situations only: the pre-battle formation, and
the walk-up to the gate. Mid-battle was left completely plain.

**Result: heroes stuck behind each other mid-fight.** The reported symptom was
precise and diagnostic: *"near the gate one changes course, which is good — but
not mid-gameplay"*, and *"the back row plays its walking animation but stays in
place"*.

**Why:** that is exactly the shape of a `headingForGate` condition. The
capability existed; it simply was not running where it was needed.

---

## 7. Attempt 5 — the final version

Removed the restriction. Avoidance now runs **whenever a unit is moving**.

This is safe *only because* of the allies-only filter added in attempt 3: a hero
walking at an enemy never swerves, so it can never circle its own target. That
filter is what makes always-on avoidance possible without re-creating the
attempt-2 orbiting bug.

---

## 8. The recurring root cause — a decision with no tie-break

The same class of bug appeared **five separate times**. Every one was a
nearest/side choice with no margin and no handling of the ambiguous case.

| # | Where | Symptom | Fix |
|---|---|---|---|
| 1 | Target selection — `nearestDistSqr < currentDistSqr` | two enemies at similar range: target swapped every frame | new target must be **20%** closer |
| 2 | Side anchor — `GetOffsetFacingPlayer` returns `dL <= dR` | standing level with a target: side flipped every frame, and since the anchors are on **opposite** sides the destination jumped **across** the target, reversing movement and facing | far side must be **30%** closer, and the choice is **sticky** |
| 3 | `FaceLeft()` re-picked the anchor itself every frame | re-introduced #2 even after #2 was fixed | routed through the same sticky picker |
| 4 | Swerve side — `dot > 0f ? -1f : 1f` with a blocker **dead ahead** | `dot ≈ 0`, noise flipped the sign every frame, unit **spun on the spot** | **commit threshold 0.25** — hold the chosen side until the blocker is clearly off-centre |
| 5 | Swerve side with units **exactly overlapping** | direction normalises to nothing, `dot = 0`, and `0 > 0` is **false for both** — so both picked the **same** side, swerved together and travelled as a **merged blob** | tie-break by **instance ID** so they take **opposite** sides |

Measured evidence for #4 and #5:

```
#4  OLD (no commit)     RRLLRLLRRLRRLLRLLRRLRRLL   flips=13
    NEW (0.25 commit)   RRRRRRRRRRRRRRRRRRRRRRRR   flips=0

#5  OLD  A side=1  B side=1   ->  separation rate = 0.00   (mathematically merged)
    NEW  A side=-1 B side=1   ->  separation rate = 1.34   (they pull apart)
```

> **If spinning or merging ever returns, look for a sixth instance of this exact
> pattern first.** It was the cause every single time.

---

## 9. What the FINAL version has that the earlier ones did not

| Property | A1 push | A2 push, old systems gone | A3 zero-interaction | A4 context-limited | **A5 FINAL** |
|---|---|---|---|---|---|
| Not overwritten by an older system | ✗ | ✓ | ✓ | ✓ | **✓** |
| Standing units never touched | ✗ | ✗ | ✓ | ✓ | **✓** |
| Cannot orbit its own target | ✗ | ✗ | ✓ | ✓ | **✓** |
| Predictive (avoids before contact) | ✗ | ✗ | ✓ | ✓ | **✓** |
| Attack spots stay inside attack range | — | — | ✗ then ✓ | ✓ | **✓** |
| No pursue/mover deadlock | ✗ | ✗ | ✗ then ✓ | ✓ | **✓** |
| Ambiguous choices have hysteresis | ✗ | ✗ | ✗ | partly | **✓** |
| Overlapping units split apart | ✗ | ✗ | ✗ | ✗ | **✓** |
| Works mid-battle, not only at the gate | — | — | — | ✗ | **✓** |
| Every movement path routed through it | ✗ | ✗ | ✗ | ✗ | **✓** |

**The five things unique to the final version:**

1. **Predictive, not reactive.** It bends *before* contact instead of resolving
   an overlap afterwards — which is what lets standing units be left alone.
2. **Allies-only.** The filter that makes always-on avoidance safe. Without it,
   always-on means orbiting enemies (attempt 2).
3. **Every ambiguous decision is settled deterministically** — hysteresis where
   there is a previous choice to keep, instance-ID tie-break where there is not.
4. **The in-range invariant on attack spots**, so the mover and the state machine
   can never disagree about "arrived".
5. **One funnel.** All movement goes through `HandleMoveToTarget` or
   `HandleRoamForward`; there are no raw-velocity bypass paths left. Verified:
   only two `linearVelocity` writes remain, both inside `PlayerManager`, both
   with avoidance applied. A bypass in `PlayerPursueTargetState` was exactly why
   the gate approach ignored avoidance for a whole round.

---

## 10. The final mechanism, concretely

Every frame, for a unit that is **moving**:

```
1. probe:   CircleCastAll(self, radius 0.4, along travelDir, distance 1.3, unit layers)
2. filter:  ALLIES ONLY (same layer). Enemies are never avoided.
3. nearest blocker wins.
4. side:    perpendicular to travel, away from the blocker
              - overlapping (<2cm)  -> instance-ID tie-break, opposite sides
              - dead ahead (|dot|<0.25) and a side already chosen -> HOLD it
              - otherwise            -> pick the far side and remember it
5. blend:   closeness = 1 - distance/1.3        (far = no bend, close = strong)
            dir = (dir + perp * 0.9 * closeness).normalized
6. clear:   no blocker -> forget the committed side, go straight
```

Plus, **while walking only**, a small personal-space correction:

```
two heroes starting FULLY MERGED
 -> 0.540 apart after 1.80s, each having moved only 0.270 units
```

A nudge, not a shove — and it never runs on a unit that has stopped, because it
lives in the pursue path only.

**It is asymmetric, which is what you see on screen.** Only the unit with an ally
*in front of it* swerves. The front unit's corridor is empty, so it walks
straight. That is why it always looks like "the rear one moved aside".

---

## 11. Parameters

On `LevelGameManager` → `CrowdSeparation2D`:

| Field | Value | Meaning |
|---|---|---|
| `unitLayers` | PlayerLayer + EnemyLayer | who counts as a blocker |
| `lookAheadDistance` | 1.3 | how early it starts turning |
| `lookAheadRadius` | 0.4 | corridor width ≈ body radius |
| `avoidStrength` | 0.9 | how wide the arc is |
| `swerveCommitThreshold` | 0.25 | how far off dead-ahead before re-deciding |
| `personalSpace` | 0.55 | closest two walking allies may get |
| `personalSpaceSpeed` | 0.6 | correction speed cap |

On `PlayerManager`:

| Field | Value | Meaning |
|---|---|---|
| `attackSlotAngleStep` | 28° | spacing between attackers around a target |
| `attackStandoffFactor` | 0.8 | fraction of attack range to stand at (**must stay < 1**) |
| `retargetHysteresis` | 0.2 | how much closer a new enemy must be |
| `anchorSwitchHysteresis` | 0.3 | how much closer the far side must be |
| `facingDeadZoneX` | 0.12 | off-centre distance before the sprite flips |

---

## 12. Known limits — accepted, not bugs

* **It prevents collisions; it does not resolve existing ones.** A blocker beside
  or behind you is not in the forward corridor, so nothing steers. Two units that
  are *already* overlapping and standing still stay that way — the direct
  trade-off of "standing units are never touched".
* **No true pathfinding.** A unit can still be funnelled into a corner by several
  allies at once; there is no global path planner.
* **Enemy units** use their own `EnemyManager` target selection, which has **not**
  been given the §8 hysteresis treatment. Their facing has a `facingDeadZoneX`
  guard, but if enemies are seen flip-flopping, that is where to look.
* **Verification is mostly simulation.** The numbers in this document come from
  reproducing each case numerically plus live runtime inspection of the editor.
  Very little of it was confirmed visually in Play mode by the author.

---

## 13. Files

| File | Role |
|---|---|
| `Assets/Scripts/Combat/Shared/CrowdSeparation2D.cs` | `SteerAroundBlockers` + `ResolveOverlap` — the whole avoidance mechanism |
| `Assets/Scripts/Combat/Shared/AttackSlotRegistry.cs` | per-target attack spot claiming |
| `Assets/Scripts/Combat/Player/PlayerManager.cs` | `HandleMoveToTarget`, `HandleRoamForward`, `ResolveAttackDestination`, `PickAttackAnchor`, hysteresis fields |
| `Assets/Scripts/Combat/Player/States/PlayerPursueTargetState.cs` | calls the movers; **must not** write velocity directly |
| `Assets/Scripts/Combat/Spawning/FormationGapFiller.cs` | pre-battle formation walk, uses the same avoidance |
| `Assets/Documentation for scripts/Formation_GapFilling_Guide.md` | the formation system |
