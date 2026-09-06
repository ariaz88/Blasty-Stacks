# Hero roster — stats, where they come from, and what is wrong with them

Audited 2026-09-06. **Nothing here was changed** — this is a read of the current data, written to
match `Assets/Scriptable Objects/Spawner/README_EnemyRoster.md`.

---

## Which number actually wins

Two stat sources exist per hero and they do **not** have to agree:

| source | used when |
|---|---|
| `UnitDefinitionSO.baseStats` (via `UnitsDatabaseSO.GetById(unitId)`) | **authoritative.** `PlayerStatsApplier.ApplyNow()` reads it, applies `PlayerProgressionConfig` growth for the unit's saved upgrade level, and `PlayerManager` adopts it: `unitStats = playerStatsApplier.CurrentStats` |
| `PlayerManager.statsBase` on the prefab | fallback only — kept when `CurrentStats` is null, which happens when a stage is played **directly** instead of booting through `StarterScene` |

So the table below is the DefSO column. The prefab's own `statsBase` is mostly dead weight, and on
three heroes it points at a *different asset* than the DefSO does (see Problems).

## The 8 registered heroes

`UnitsDatabaseSO.units` holds these 8, in this order. Everything else in this folder is legacy.

| unitId | DefSO | display name | baseStats asset | atk | hp | def | atkSpd | move | range | **DPS** |
|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `Valkir3` | Valkir3 | `Player_Minotaur_01` ⚠ | 64 | 100 | 25 | 2.0 | 3.5 | 0.85 | **128** |
| 2 | `Dark_Oracle_1` | Dark_Oracle_1 | `Dark_Oracle_01` | 72 | 100 | 25 | 1.5 | 3.5 | 0.85 | 108 |
| 3 | `Minotaur_02` | Minotaur_02 | `Minotaur_02` | 72 | 100 | 25 | 1.5 | 3.5 | 0.85 | 108 |
| 4 | `CowMinotaur_2` | Minotaur_2 | `CowMinotaur_2` | 72 | 100 | 25 | 1.5 | 3.5 | 0.85 | 108 |
| 5 | `Golem_3` | **Minotaur_2** ⚠ | `Golem_3` | 72 | 100 | 25 | 1.5 | 3.5 | 0.85 | 108 |
| 6 | `PlayerFallen_Angels_02` | Fallen_Angels_02 | `PlayerValkir3` ⚠ | 64 | 100 | 25 | 2.0 | 3.5 | 0.85 | **128** |
| 7 | `PlayerFallen_Minotaur_01` | Fallen_Minotaur_01 | `Player_Minotaur_01` ⚠ | 64 | 100 | 25 | 2.0 | 3.5 | 0.85 | **128** |
| 8 | `Player_Dark_Oracle_3` | Dark_Oracle_3 | `Player_Dark_Oracle_3` | 72 | 100 | 25 | 1.5 | 3.5 | 0.85 | 108 |

**There are only two stat profiles.** HP, defense, move speed and attack range are identical on all
eight; only attack and attack speed vary, and they vary together:

- **Fast (128 DPS)** — atk 64, atkSpd 2.0 — ids 1, 6, 7
- **Slow (108 DPS)** — atk 72, atkSpd 1.5 — ids 2, 3, 4, 5, 8

The "fast" three are strictly better: more DPS, everything else equal. There is no trade-off, no
tank, no glass cannon — the roster is two numbers wearing eight costumes.

## Unlock gating

| unitId | starts deployed | required level | required stage in level |
|---|---|---|---|
| 1, 2, 3, 4 | yes | 1 | 1 |
| 5, 6 | no | 1 | 1 |
| 7 | no | 1 | 2 |
| 8 | no | 1 | 3 |

`respawnGemCost` is **0 on all eight**, so the Heroes-Stats buy-back is currently free.
`HeroStatsPanel.gemsPerHero` (placeholder 50) is what fills in when this is 0.

## Upgrade growth (`PlayerProgressionConfig`, clamp -25%..+50%)

| level | atk | hp | def | atkSpd | move |
|---|---|---|---|---|---|
| 1 | ×1.000 | ×1.000 | ×1.000 | ×1.000 | ×1.000 |
| 2 | ×1.079 | ×1.099 | ×1.063 | ×1.020 | ×1.020 |
| 3 | ×1.163 | ×1.206 | ×1.129 | ×1.040 | ×1.040 |
| 5 | ×1.348 | ×1.448 | ×1.275 | ×1.080 | ×1.080 |
| 7 | ×1.556 | ×1.731 | ×1.439 | ×1.121 | ×1.121 |
| 10 | ×1.916 | ×2.242 | ×1.726 | ×1.184 | ×1.184 |

HP grows fastest (~+10%/level), attack next (~+8%), attack and move speed barely move (+2%).
Enemies use the separate `EnemyProgression` curve and scale off the **stage index**, while heroes
scale off the player's **saved upgrade level** — the two are unrelated.

---

## Problems found (none fixed)

1. **Three heroes share stat assets with a different hero, cross-wired by name.**
   - `Valkir3` (id 1) reads `Player_Minotaur_01`
   - `PlayerFallen_Minotaur_01` (id 7) reads `Player_Minotaur_01` — the *same asset*
   - `PlayerFallen_Angels_02` (id 6) reads `PlayerValkir3`

   Editing `Player_Minotaur_01` silently retunes the Valkyrie **and** the Fallen Minotaur together.
   A `PlayerValkir3` asset exists but only the Fallen Angel uses it.

2. **Prefab and DefSO disagree on three heroes.** `Player_Valkyrie`'s own `statsBase` is
   `PlayerValkir3`, but id 1's DefSO says `Player_Minotaur_01`. The numbers happen to match, so
   nothing looks broken today — but the two will drift the moment either asset is edited, and the
   prefab value is what you get when playing a stage scene directly.

3. **`Golem_3` (id 5) has `displayName = "Minotaur_2"`** — a copy-paste, duplicating id 4's name.
   This is the string the roster UI shows, so the Golem currently displays as a Minotaur.

4. **Only two heroes can actually spawn.** `PlayerWaveManager.playerPrefabs` in
   `LevelTemplate.prefab` holds `Player_Golem_3` (id 5) and `Player_Dark_Oracle_3` (id 8) — both
   on the 108 DPS profile. The three 128 DPS heroes never reach the field through the wave loop.
   `playerRootPrefab` is `Player_Pref`, which carries unitId 4.

5. **Five legacy DefSOs with no `runtimePrefab`**: `WarriorDefSO` (id 0), `WarriorDefSO 1` (id 15),
   `ArcherDefSO` (10), `HorseManDefSO` (11), `MageDefSO` (12). None are in `UnitsDatabaseSO`, so
   they are inert. Note `WarriorDefSO` claims **unitId 0**, which is `PlayerStatsApplier.unitId`'s
   default — and `PlayerCastle` in the template still has `unitId = 0`. Since id 0 is not in the
   database, `GetById(0)` returns null and that applier logs
   `[PlayerStatsApplier] UnitDefinition or baseStats missing for unitId=0` every stage load. The
   gate takes its HP from `PlayerGateStats` (500) instead, so it is noise rather than a bug.

6. `WarriorDefSO 1` (`PlayerMeleeReaper`) is authored at **96 atk / 4.0 atkSpd = 384 DPS**, three
   times any live hero. Unused, but worth not reviving by accident.
