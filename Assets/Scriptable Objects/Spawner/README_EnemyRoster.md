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
| 3 | `Enemy_Orc` | 46 | 160 | 35 | 1.00 | 3.4 | bruiser, hits hard | **stage 7** (was 6) |
| 3b | `Enemy_Skeleton_Swordsman` | — | — | 52 | — | — | armoured newcomer, stage-6 only | **stage 6** |
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
| 6 | Reaper ×1, Zombie ×1 | **Skeleton_Swordsman ×2**, Zombie ×1 | 5 |
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

## Stage 6 was re-authored on 2026-09-21 (Arash)

The wave-plan table above is the ORIGINAL 2026-09-06 authoring. Since 2026-09-14
`LevelBattleRules.EnemyWaves` overrides the head counts for stages 1-10, so those
rows' totals are historical for that range — stage 6 fields 5, not 9.

Stage 6 specifically was re-specified:

- **5 enemies, 2 in wave 1 then 3 in wave 2.**
- **Mix: 1 Reaper (the type-1 enemy from stage 1), 2 Zombie villagers, 2 Skeleton
  Swordsmen — both Skeletons in the LAST wave.**
- **Orc no longer appears at stage 6**; it now debuts at stage 7. The stage-6
  newcomer is the Skeleton Swordsman instead.
- **`Enemy_Skeleton_Swordsman` is a NEW stats asset**, not a change to
  `Enemy_Skeleton_Crusader_1` — stages 9+ keep the original Crusader numbers
  untouched. It reuses the Crusader PREFAB, because that is the only skeleton
  art in the project (it does carry a sword sprite).
- Its CP is **exactly 1.20x the Zombie villager's at stage 6** (149.7 vs 124.7),
  per Arash's "20% stronger than the enemies already in level 6". ATK and HP were
  each scaled by sqrt of the required factor; DEF stayed at 52 to keep the
  armoured identity.
- **Attack speed matches the other stage-6 enemies** (base 0.66041815, the same
  value Reaper and Zombie use, not the Crusader's 0.7).
- **Spawn layout is CENTRED at 3-unit spacing in both waves** — wave 1 at
  x = centre ±1.5, wave 2 at centre -3 / 0 / +3.

Verify any of this without playing: **Tools > Testing > Report Stage Composition**.

## Spawn placement: the standard, and the bug behind it (2026-09-21)

**`spawnMin`/`spawnMax` on these assets are DEAD for every stage built on
`LevelTemplate.prefab`.** The template has `spawnRelativeToEnemyGate: 1`, so the
live box is `gateRelativeMin`/`gateRelativeMax` measured from the enemy gate —
currently `(-3, -2)` to `(3, 0)`, i.e. **6 wide**, while the assets still say 27.
Editing the asset values changes nothing. This cost a round trip: a spacing fix
authored into `Stage_06.asset` had no effect at all.

`EnemySpawner.GenerateGridPositions` was rewritten because the old one placed the
first slot at the box's left edge and clamped overflow. Against the real 6-wide
box with `minSlotSpacing.x` 3 that produced:

- **2 enemies** at centre and centre+3 — the pair visibly shoved right.
- **3 enemies** at centre, centre+3, centre+6 — and centre+6 fell outside, so the
  clamp pulled it back **onto** centre+3. Two enemies on one spot.

It now **centres** the formation and **caps spacing to what the box holds**, so
slots can never collapse. Arash's standard, from his annotated screenshots:

| enemies | offsets from box centre | spacing |
|---|---|---|
| 2 | −1.5, +1.5 | 3 |
| 3 | −3, 0, +3 | 3 |

**Known consequence:** 4-enemy waves (stages 9 and 10, wave 2) need a 9-wide span
and so compress to **2-unit** spacing in the 6-wide box. Widen
`gateRelativeMin/Max.x` on the template if they should keep 3.

**Spawn line raised** the same day: `gateRelative` y went `-4 … -2` → `-2 … 0`,
moving enemies **2 world units closer to their own gate** (Arash marked the spot;
scale came from the pair being 3 units ≈ 105 px apart on his screenshot). This is
on the shared template, so it affects **every** stage using it.

## 2026-09-21 — every non-tutorial enemy +30% CP

Arash: heroes do not scale with the stage (only with upgrades), so an unupgraded player meets
stage 6 with exactly the stage-4 hero — the enemies were too weak. **All seven shared enemy
stat assets were raised to 1.80x their CP**: `Enemy_Reaper_Man_01`, `Enemy_Zombie_villager`,
`Enemy_Orc`, `Enemy_Skeleton_Crusader_1`, `Enemy_Skeleton_Swordsman`, `Enemy_Golem_01`,
`Enemy_Golem_02`.

ATK is multiplied by **1.80^0.60 = 1.422864** and HP by **1.80^0.40 = 1.265054**; DEF and attack
speed untouched. CP is multiplicative, so "ATK carries 60% of the increase" is an EXPONENT
split, and the two factors multiply back to exactly 1.80. It was first applied at 1.30x; stepped 1.30x -> 1.70x -> 1.80x the same day: 1.30x moved the hero only 9.6 -> 8.2 blows, because 60% of a 30% rise is just +17% ATK.
Since `CP = ATK x AtkSpd x maxHP x (1 + DEF/100) / K`, that is exactly 1.30x CP, and because
every enemy got the same factor **all ratios between enemy types are unchanged** — the Skeleton
Swordsman is still exactly 1.20x the Zombie at stage 6.

Stage totals (edit-mode verified), original -> 1.80x: 4: 387.0->696.6 · 5: 537.1->966.7 ·
6: 662.4->1192.3 · 7: 840.0->1512.0 · 8: 898.8->1617.8 · 9: 1222.4->2200.4 · 10: 1393.6->2508.4.
At stage 6 an unupgraded hero dies in ~6.7-8.0 blows (was 9.6-11.4), ~9.6-11.4 s, AND the stage
now beats a full unupgraded roster: 10 heroes = 1164.7 CP against 1192.3.

**The `Enemy Base Stats/Tutorial/` assets (stages 1-3) were NOT touched** — Arash authored those
values and they are off-limits, per the standing directive recorded in `CPCalculator`. So the
tier table above is now historical for base numbers; read the assets for live values.

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
