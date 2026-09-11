# CP combat correction verification — 2026-09-10

## Scope

Unity Editor MCP, actual `Assets/Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_1.unity`
through `Level_1_Stage_5.unity`. Character asset references were audited first:
all roster definitions and enemy wave entries reference `Characters/New Characters/`
under `Deployed Players`, `Enemies`, or the existing `Undployed Players` folder.
No obsolete character prefabs were substituted.

## Regression method

`Tools > Combat Power > Verify Real Stages 1-5` runs all 20 match-count cases.
An in-memory roster enables all eight database heroes, with heterogeneous saved-level
inputs (levels 1-4), and deterministic random seeds. Real scenes, spawners, formation,
movement, animation events, weapon collisions, enemy deaths and gate damage run.
Match events are supplied rapidly to exercise queued deployment; puzzle gestures,
the energy purchase and battle-button camera transition are not exercised by this test.
No synthetic damage, remote kills or gate destruction calls advance these scenarios.

The real boot's unconditional save reset is deliberately avoided. Runtime
LevelGameManager is disabled in the test scenes to avoid awarding/saving progression;
the test observes real gate destruction directly instead of opening win/lose panels.
Original scene is restored afterward. Save and stage keys must remain unchanged.

## Expected and checked outcomes

| Stage | Heroes at each match count | Enemy count | Player CP at each match count | Enemy CP | Results |
|---|---|---:|---|---:|---|
| 1 | 1, 2, 3 | 1 | 100, 200, 300 | 80 | W, W, W |
| 2 | 1, 2, 4 | 2 | 125, 250, 500 | 100 | W, W, W |
| 3 | 1, 2, 4, 5 | 3 | 125, 250, 500, 625 | 115 | W, W, W, W |
| 4 | 1, 2, 4, 8 | 4 | 125, 250, 500, 1000 | 400 | L, L, W, W |
| 5 | 1, 2, 4, 6, 8, 12 | 5 | 125, 250, 500, 750, 1000, 1500 | 650 | L, L, L, W, W, W |

PASS: both the initial physical-combat regression and the final regression passed
all 20 cases. The final run includes spawn-time reference calibration and protection
limited to the last surviving winning unit. Saved progress was unchanged, the console
reported zero errors, and the original StarterScene was restored clean in Edit mode.
The detailed final log is in Editor SessionState `CPCombatVerification.result`.

## What this does not prove

- No statistical win-probability claim for small CP differences or arbitrary teams.
- No validation of exact Fast/Normal/Slow durations; those are pacing targets.
- No exhaustive roster/upgrade/random-seed/pathfinding coverage.
- Stage 6+ regression is a code-path/scope review, not an end-to-end playtest.
- No UI progression/energy assertion: these side effects were intentionally bypassed.

The guarantees come from explicit tutorial assistance, not the CP formula: hit-only
damage multipliers, a last-survivor 1%-HP safety floor, and bases protected while their
defenders live. A stuck attacker still needs a real AI/pathfinding fix, never auto-resolution.
