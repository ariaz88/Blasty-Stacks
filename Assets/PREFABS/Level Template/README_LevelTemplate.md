# LevelTemplate.prefab — the shared body of every stage scene

`LevelTemplate.prefab` is a snapshot of **everything in `Level_1_Stage_1` except the puzzle
pieces**. Every stage scene is now a single root: one instance of this prefab. Edit the prefab
once and the change lands in every stage that has been converted.

Built 2026-09-06 from `Level_1_Stage_1`. **All 20 stages are converted.** Every stage scene is a
single root and shows `roots=1` in the hierarchy.

## Per-stage settings at a glance

| stage | board cover | moves allowed | wave config | pieces |
|---|---|---|---|---|
| 1 | `_1` | 8 | `Spawner2` | 30 (keeps all 5 authoring groups) |
| 2 | `_2-10` | 8 | `Spawner2` | 6 |
| 3-6 | `_2-10` | 7 | `Stage_03`…`Stage_06` | 8-12 |
| 7-10 | `_2-10` | 6 | `Stage_07`…`Stage_10` | 12 |
| 11 | `_11-15` | 6 | `Stage_11` | 12 |
| 12-15 | `_11-15` | 5 | `Stage_12`…`Stage_15` | 14-16 |
| 16-20 | `_16-20` | 5 | `Stage_16`…`Stage_20` | 14-18 |

Move budget is the "board gets more restricted as you advance" knob: 8 → 7 → 6 → 5, floored at 5.
It is a plain int on `Input System → PuzzleMoveBudget → Moves Allowed`; `0` means unlimited.

Board covers follow their own names: stage 1 → `_1`, 2-10 → `_2-10`, 11-15 → `_11-15`,
16-20 → `_16-20`. **Stages 11-20 previously had no active cover at all** — that was fixed during
the conversion, not introduced by it.

The boards themselves already ramped before any of this: playable cells go 20 → 24 → 28 → 32 and
piece counts 6 → 18 as the stages advance. None of that was touched.

---

## What lives in the prefab (shared — edit here, not per scene)

| Child of `LevelTemplate` | What it carries |
|---|---|
| `Main Camera` | orthographic size 12.18, position (0, 0, -10) — the stage framing |
| `LevelGameManager` | `LevelGameManager` + `BattlePhaseTransition` (the BATTLE camera move: +7.12 +2 extra, 1.1s, InOutCubic) + `CrowdSeparation2D` |
| `PlayerCastle` | gate stats (500 HP), flags, `Stage Holder/BoardStages` = the 6 deploy slots, gate HP bar |
| `EnemyCastle` | gate stats (350 HP), left/right offsets, gate HP bar |
| `EnemySpawner` | `waitForBattleStart` ON, `spawnRelativeToEnemyGate` ON, anchored to `EnemyCastle` |
| `PlayerWaveManager` | hero prefabs, `gatePoints` → BoardStages, `jumpLanes` → `Jump positions`, `FormationGapFiller` |
| `Input System` | `BoardInputController` + `MatchResolver` + `PuzzleMoveBudget` |
| `Puzzle Board` | `BoardBG` (grid, bootstrapper, ghost mask) **with no pieces**, plus the four board cover sprites |
| `Canvas ` | Feature Panel (BATTLE / potion / battery / hammer), Heros Stats panel, LastStandOffer, Timer + Enemy counters, Roguelite UI, Win / Lose / Revive panels, Ads banner, HUD |
| `Jump positions`, `spawnPosHolder` | the 4 march lanes and the spawn anchors |
| `Enveiroment`, `BackGroundImage`, `Top Mountains`, `Top Shadow` | the backdrop |
| `EventSystem`, `FractureManager` | input + match VFX |

Cross-references (`BattlePhaseTransition.fadeInAfterMove`, `PlayerWaveManager.jumpLanes`,
`BattleStartController.transition`, …) all point **inside** the prefab, which is exactly why the
whole level had to become one prefab rather than a folder of small ones — a prefab cannot store a
reference to a scene object.

---

## What stays per stage (override on the instance, never on the prefab)

1. **Board layout** — `Puzzle Board/BoardBG`
   - `BoardGridXY`: `width`, `height`, `cellSize`, `cellPadding`
   - `BoardGhostMask.mask`: which cells are blocked (this is the stage's board *shape*)
   - the `Blocks` / `RedundantBlocks` children: the authored pieces. They sit on the instance as
     **added GameObjects**, so a prefab edit never touches them.
2. **Board cover art** — under `Puzzle Board`, exactly one of
   `boardsCover_Stage1_1` / `_2-10` / `_11-15` / `_16-20` is active.
3. **Move budget** — `Input System → PuzzleMoveBudget → Moves Allowed`.
   `0` = unlimited. Current values: Stage 1 = 8, Stage 2 = 8, Stage 3 = 7.
   This is the per-level "how restricted is the board" knob.
4. **`EnemySpawner.levelConfig`** — the wave asset for that stage (all three currently `Spawner2`).

**Enemy difficulty is NOT set here.** `EnemySpawner.RunLevel()` reads
`LevelManager.CurrentStage` and passes it to `EnemyManager.Initialize(stageLevel)`, so enemies
scale with the global stage index on their own. Nothing per scene needs to change for it.

---

## Adding a new stage to the template

1. Open the stage scene.
2. Note its `BoardGridXY` size, its `BoardGhostMask` mask, its `Blocks` groups, its active
   `boardsCover_*`, and its `EnemySpawner.levelConfig`.
3. Drag the `Blocks` / `RedundantBlocks` groups out to a temporary scene root.
4. Delete every remaining root.
5. Drag `LevelTemplate.prefab` in at (0, 0, 0).
6. Drop the block groups back under `LevelTemplate/Puzzle Board/BoardBG`.
7. Re-apply the four per-stage values listed above.

(The 2026-09-06 session did steps 1-7 with an editor script over all 20 stages.)

**Trap worth knowing if you script this again:** a `LevelConfig` (or any asset) reference taken
*before* `EditorSceneManager.OpenScene` does **not** survive the load — the managed wrapper comes
back destroyed and silently assigns as `null`. Two conversion runs wrote `levelConfig = null` into
15 scenes this way, and the log said `cfg=NULL` rather than throwing. Load assets **after** the
scene is open and use them immediately.

## Gotchas

- Never press **Apply All** on a stage instance — it would push that stage's board into the
  template and into all the others. Apply single properties deliberately, or edit the prefab.
- `Canvas ` and `EnemyGateProgressBarUI ` have **trailing spaces** in their names. `Find()` calls
  must match them exactly.
- The template still carries Stage 1's inactive leftovers (`PREVIEW`, `PREVIEW (1)`, `PREVIEW (2)`,
  `BoardImage (1)`, `Base Roof_Redundant`, `PlayerGate`, `Regulite Show Panel`, `Revive Panel`,
  `Lose Panel`, `Revive Level `). Level 1 keeps them; Stages 2+ strip them per instance — see below.
  `Player_Valkyrie` and `Enemy_Reaper_Man_01` were deleted from the prefab outright on 2026-09-06,
  so they are gone from every stage including Level 1.

---

## Per-stage cleanup (2026-09-06) — Level 1 keeps them, Stages 2+ do not

Level 1 is the reference scene and keeps every authoring leftover. Every other stage removes
them, as `m_RemovedGameObjects` overrides on its own instance:

| Removed from Stages 2+ | Where |
|---|---|
| `PREVIEW (1)`, `PREVIEW (2)` | root |
| `BoardImage (1)`, `Base Roof_Redundant` | `Puzzle Board` |
| `Blocks (1)`, `Blocks (2)`, `Blocks (3)`, `RedundantBlocks` | `Puzzle Board/BoardBG` |
| the three `boardsCover_*` the stage does not render | `Puzzle Board` |
| `PlayerGate` | `PlayerCastle` |
| `Revive Panel`, `Lose Panel`, `Revive Level ` | `Canvas ` |

Note on the covers: the rule is **keep the one this stage actually renders, drop the other three**.
For Stages 2 and 3 that means keeping `boardsCover_Stage1_2-10`. Deleting all of them by name
would leave the stage with no board art.

**`Regulite Show Panel` must stay** — it was removed on 2026-09-06 and put straight back. It is
what `RogueliteManager.skillSelectPanel` and all three `cardSlots` on `Reg_Manager` point at.
Removing it nulls those fields; every use is null-guarded so nothing throws, but
`ShowSkillSelection()` calls `GameplayPause.SetPaused(true)` and `FreezeBattlefield(true)` *before*
the guard, so a roguelite level-up would freeze the battle with no card picker to dismiss it.
It is inactive in the hierarchy because it only appears on level-up — that is not dead UI.
(Reverting the removal restored the references automatically; no rewiring was needed.)

The three revive/lose panels above are safe to drop by contrast: revive is switched off, `Lose Level`
is the live lose panel, and `RevivePanel.revivePanel` / `.BGImage` are null-guarded at every use.
