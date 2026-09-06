# Enemy roster and per-stage waves

Authored 2026-09-06. Stages 3-20 each have their own `Stage_NN.asset` `LevelConfig`.
Stages 1-2 still share `Spawner2.asset` (one wave, 4 Reapers) and were deliberately left alone.

---

## The problem this solved

All six enemy prefabs had **identical** base stats — atk 35, hp 120, def 30, atkSpd 1, move 3.5.
Six different sprites on one stat block, so there was no "order of power" to introduce them in.
The tiers below are new: each archetype now has its own `UnitStatsSO` numbers.

`Enemy_Reaper_Man_01` was left at exactly its authored values, because it is what Stages 1-2
already fight and their difficulty must not shift underneath them.

## Tiers (weakest first)

| tier | enemy | atk | hp | def | atkSpd | move | role | first appears |
|---|---|---|---|---|---|---|---|---|
| 1 | `Enemy_Reaper_Man_01` | 35 | 120 | 30 | 1.00 | 3.5 | fast skirmisher (baseline) | stage 1 |
| 2 | `Enemy_Zombie_villager` | 32 | 170 | 28 | 0.90 | 3.0 | slow chaff, soaks hits | **stage 3** |
| 3 | `Enemy_Orc` | 46 | 160 | 35 | 1.00 | 3.4 | bruiser, hits hard | **stage 6** |
| 4 | `Enemy_Skeleton_Crusader_1` | 44 | 205 | 52 | 0.95 | 3.2 | armoured | **stage 9** |
| 5 | `Enemy_Golem_01` | 58 | 275 | 65 | 0.85 | 2.9 | tank | **stage 13** |
| 6 | `Enemy_Golem_02` | 70 | 340 | 78 | 0.85 | 3.0 | elite tank | **stage 17** |

Spacing is 3, 3, 4, 4 stages. Move speed is kept inside 2.9-3.5 on purpose — the pursuit,
formation and crowd-separation tuning was built around ~3.5, and a genuinely slow unit
(2.0 or below) has not been tested against it.

These are on top of the global curve: `EnemyManager.Initialize(stage)` sets `unitLevel = stage`
and `EnemyProgression` compounds roughly +8%/stage on atk and HP. So a stage-17 Golem_02 is far
above the raw 70/340 in the table.

## Wave plan

Two waves per stage. `EnemySpawner.RunLevel()` waits for `_alive == 0` before starting the next
wave, so wave 2 only arrives once wave 1 is wiped — `delayBeforeWave` is the beat after that.
Both waves use `TwoRows`, front row anchored at MinY (nearest the player), `rowYOffset` 1.2,
`secondRowDelay` 0.35.

`F` = front row, `B` = back row. The heavier type is put in front, chaff behind.

| stage | wave 1 | wave 2 | total |
|---|---|---|---|
| 3 | Reaper ×3 F | Zombie ×2 F, Reaper ×2 B | 7 |
| 4 | Reaper ×3 F, Zombie ×1 B | Zombie ×3 F, Reaper ×2 B | 9 |
| 5 | Zombie ×2 F, Reaper ×2 B | Zombie ×3 F, Reaper ×3 B | 10 |
| 6 | Reaper ×3 F, Zombie ×2 B | **Orc ×2 F**, Zombie ×2 B | 9 |
| 7 | Zombie ×3 F, Orc ×1 B | Orc ×2 F, Reaper ×3 B | 9 |
| 8 | Orc ×1 F, Reaper ×2 B, Zombie ×2 B | Orc ×3 F, Zombie ×2 B | 10 |
| 9 | Zombie ×3 F, Orc ×2 B | **Skeleton ×2 F**, Orc ×2 B | 9 |
| 10 | Orc ×3 F, Reaper ×2 B | Skeleton ×2 F, Zombie ×3 B | 10 |
| 11 | Skeleton ×1 F, Orc ×2 F, Zombie ×2 B | Skeleton ×3 F, Orc ×2 B | 10 |
| 12 | Orc ×3 F, Skeleton ×2 F | Skeleton ×3 F, Zombie ×3 B | 11 |
| 13 | Orc ×2 F, Skeleton ×2 F, Zombie ×2 B | **Golem_01 ×2 F**, Skeleton ×2 B | 10 |
| 14 | Skeleton ×3 F, Orc ×2 B | Golem_01 ×2 F, Orc ×3 B | 10 |
| 15 | Skeleton ×3 F, Golem_01 ×1 F | Golem_01 ×2 F, Skeleton ×3 B | 9 |
| 16 | Skeleton ×3 F, Orc ×3 B | Golem_01 ×3 F, Skeleton ×2 B | 11 |
| 17 | Golem_01 ×2 F, Skeleton ×3 B | **Golem_02 ×2 F**, Golem_01 ×2 B | 9 |
| 18 | Golem_01 ×2 F, Skeleton ×3 B | Golem_02 ×2 F, Skeleton ×3 B | 10 |
| 19 | Golem_01 ×3 F, Skeleton ×3 B | Golem_02 ×3 F, Golem_01 ×2 B | 11 |
| 20 | Golem_01 ×3 F, Skeleton ×3 B | Golem_02 ×4 F, Golem_01 ×2 B | 12 |

Head counts stay flat-ish (7 → 12) on purpose: the difficulty ramp is carried by the tier mix and
the per-stage stat curve, not by flooding the field. Ten to twelve units is also roughly what the
gate-relative spawn box holds without overlap.

## Two things worth knowing

**`WaveEntry.unitLevel` does nothing.** `EnemySpawner.SpawnOne` sets `em.unitLevel = entry.unitLevel`
and then `em.Initialize(stageLevel)` immediately overwrites it with the stage index. So per-entry
level cannot be used for balancing — only the archetype and the count matter. Left at 1 everywhere.

**`levelNumber` is now the stage number on each asset.** `RunLevel()` falls back to
`levelConfig.levelNumber` when `LevelManager.Instance` is null, which is exactly what happens when
you press Play directly on a stage scene instead of coming through `StarterScene`. Before this,
every stage tested that way fought stage-1 enemies; now a direct Play on stage 12 gets stage-12
difficulty.

## Spawn rectangle

`spawnMin (-3, 4.25)` / `spawnMax (3, 6.25)` are copied from `Spawner2` and only matter for stages
**not yet on `LevelTemplate.prefab`** — those still have `spawnRelativeToEnemyGate` OFF and use
absolute world coordinates. Converted stages (1-3) have it ON and measure the box from the enemy
gate instead, ignoring these values. Once stages 4-20 are converted, the fields become dead.
