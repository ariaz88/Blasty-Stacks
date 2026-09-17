# Session hand-off — 2026-09-16 / 17
## The CP enforcement layer was removed, and levels 1-4 were re-tuned by hand

Read this before touching CP, combat stats, or levels 1-4. It records what changed,
what was measured, and — most importantly — **what is currently broken**.

---

## 0. READ FIRST — the one thing that is broken right now

**Level 4 is deadlocked. Combat never starts.**

Measured in play mode: 47 seconds, `totalBlows = 0`, nobody moved, nobody acquired a
target, castle slowly chipped by nothing.

Cause: the level 4 enemies were moved to the field corners (`x = ±4.5`) at Arash's
request, but `EnemyLocoMotion.fairDistanceToPlayer = 2.00` — a unit only acquires a
target within **2 units**. The hero deploys near the middle (gate slots at x −2.08,
−0.70, +0.69, +2.09), so the horizontal gap is ~3.8 — nearly double the engage range.
The enemies march straight down their own far-left / far-right columns, walk past the
hero without seeing it, and park at the player castle.

Arash was asked to choose and **has not answered yet**:

| option | effect |
|---|---|
| **A** (recommended) | raise `fairDistanceToPlayer` 2.0 → ~5.0. Corner layout works as drawn. Changes targeting on every level. |
| **B** | move enemies back to `x = ±2.0`. Works immediately, no side effects, but the hero can get caught between them. |
| **C** | A, but as a per-prefab override for level 4 only. |

Until one is applied, **do not ask Arash to playtest level 4** — he will watch three
units stand still, and he has already lost time to exactly that.

---

## 1. What this session was about

Two complaints, both correct:

1. **Units-menu upgrades had no effect on battles.**
2. **The code decided the winner before the fight.**

Both traced to `CPBattleController`, which rescaled every hero to a fixed
`PerHeroCP(level)` at battle start (erasing upgrades) and derived `PlayerShouldWin`
from `MatchesCleared`, then enforced it with damage budgets and a protected "champion".

Arash's model, now implemented: **CP is bottom-up and descriptive.** Each unit's CP
comes from its own stats; a side's total is the plain sum; the battle decides the
winner; CP only *predicts*.

---

## 2. Code changes

### `CPBattleController` — gutted to a read-only reporter (kept, not deleted)

**Removed:** hero rescale (`teamFactor`/`ApplyFactor`), `PlayerShouldWin` + its throw,
`ChooseChampion`, `LimitDamageToChampion`, `ScriptTutorialBlow`,
`AdjustIncomingDamage`, `IsProtected`, `IsTutorialExchange`, `BattleIsAnExpectedWin`,
`CountEngagedFlankers` / `ChampionIsFlanked` / `ChampionIsSurrounded`,
`ScaleToCP` / `EnemyScale` (top-down CP distribution), `CalibrateHero`.

**Kept:** `EnsureRecovery` (attaches `MeleeContactRecovery` — unrelated to CP, units
stick on contact without it), CP reporting (`PlayerCP` / `EnemyCP` / `Ratio`, summed
live, never written back), telemetry (`HeroDeaths`, `EnemyDeaths`,
`FewestHitsBeforeDeath`, `TotalBlowsThisBattle`), and `HasLivingDefenders`.

`HasLivingDefenders` was deliberately kept against the original plan: it is
**outcome-neutral** (never asks who should win) and stops a single fast unit ending a
match by running past a live battle.

**Added:** `LogEveryBlow` (static bool, currently **true**) — every landed blow prints
`[BLOW #n] attacker -> target, dmg, HP before->after, N blows taken`, with
`*** DEAD after N blow(s) ***` on the kill. Set false when tuning is finished.

Also added: `RegisterHero` (replaces `CalibrateHero`) and it is now called
**unconditionally for every hero at every stage**. The old call was double-gated, so
**heroes from stage 6 on never received `MeleeContactRecovery`** — a latent bug.

### `LevelBattleRules` — every CP target and outcome rule deleted

Gone: `PerHeroCP`, `EnemyUnits` / `EnemyUnitCPs`, `ReferenceEnemyCP`,
`FirstWinningMatch`, `DamageBudgetPerEnemy`, `MinHitsToSpendBudget`,
`FlankedDamageScale`, `FlankEngageRange`, `SurroundedEnemyCount`,
`SurroundedDamagePerHit`, `IsTutorialPresentation`, `TutorialHitsToKillEnemy`,
`TutorialHeroDamagePerHit`, `MaxHitsToKillAnyone`, `DefenseForMaxHits`,
`HitsToKill(defence)`.

`MinHitsToKillAnyone` → **`BaseStateBlowsToKill = 8`**. The rename is the point: it is
an **authoring target for level-1 stats**, not a runtime rule.

Kept: `Deployments`, `TotalPairs`/`HeroesForMatch`/`TotalHeroes`, `EnemyWaves`,
`ResolveLevel`, `BaseChipPerBlow`.

### `CharacterStats` — all runtime bounds on blows-to-die removed

- The `maxHP/11` damage **floor** went first. It lifted every weak blow to the same
  value, so a feeble attacker and a strong one landed identical damage — ATK stopped
  affecting outcomes and CP could only predict through attack speed.
- The `maxHP/8` **ceiling** went next, once Arash explained the model fully: 8 is
  where the *base state* should sit, not a law for the whole campaign.
- **`SafetyBlowFloor = 4`** is all that remains — a blow may never exceed `maxHP/4`.
  It is an anti-one-shot guard, **not balance**. Do not raise it back toward 8.
- **The pause gate moved in here**: `if (GameplayPause.IsPaused ||
  !LevelGameManager.IsBattleRunning) return 0f;`. It previously lived only inside the
  deleted `AdjustIncomingDamage`, and `PlayerStats`/`EnemyStats` never checked it.
- `ReportDeath` no longer prints "RULE BROKEN" for fast kills — it reports
  *"fast - this unit is out-levelled"*, which is a signal, not a fault.

**Why the drift is intended:** enemies grow every stage, the player upgrades every ~5,
so a hero is *meant* to slide 8 → 7 → 5 blows inside a cycle. That slide is the
"go upgrade" signal. A hard floor erased it.

### `PlayerManager` — one constant speed

`marchSpeedBoost` (1.3) and the `MarchSpeed` property **deleted**. Both paths now use
`CurrentMoveSpeed`. Previously a hero ran at **0.78** with no target and dropped to
**0.60** the instant it locked one — a visible 23% slowdown exactly when it turned to
chase the next enemy. Only a full stop (combat / attack / lock) may change speed now.

### `HeroDeploymentSequencer` — race fixed (this one cost a whole battle)

`RunQueue` snapshotted `EarnedBatches` the instant BATTLE was pressed, believing
`SealForBattle` made it final. **It does not** — `PlannedWaveLoop` awards a batch only
after `nextWaveDelay` (0.75s). Press BATTLE inside that window and the snapshot is
empty → `TotalLoads = 0` → coroutine exits → **no hero ever deploys**, enemies walk an
undefended castle, automatic loss.

Reproduced live: `EarnedBatches=1` but `TotalLoads=0`, `CurrentLoadIndex=-1`, gate
500→458. Fixed by waiting for `MatchesReleased >= MatchesCleared`, bounded by new
`awardWaitTimeout` (5s) with a warning on timeout.

Not a timeScale bug — the 6s load uses `Time.deltaTime` and scales correctly.

### `EnemyManager` — first-strike rule

**The hero now always opens an engagement.** `MayStrike()` holds an enemy's opening
swing until it has taken at least one hit; after that it fights normally.
`firstStrikeGrace = 3s` (Inspector, all 10 enemy prefabs) is a **deadlock guard** — if
the hero never arrives, the enemy eventually swings rather than freezing.

Why it was needed: the enemy was not faster. Both sides start `currentRecoveryTimer` at
**0.00** on every prefab, and reach is near-identical (hero `maxAttackRange` 0.85,
enemy `stoppingDistance` 0.83). It was pure **sequence** — the enemy spawns at BATTLE,
walks down, and is standing still and ready by the time the hero finishes its 6s load
and closes. The hero is still moving into its slot, so the enemy got a free first blow.

Also removed: dead numbered siblings `Initialize1` / `RebuildFromBase1`.

### `CPCalculator` — the divisor is now the CP-display knob

`DisplayDivisor` **200 → 168 → 178 → 190** across the session.

**Arash's rule, follow it:** tune stats for how the *fight* plays, then move the
divisor to make CP read ~100. It scales every unit equally, so no ratio, ordering or
outcome can shift. Re-derive as:

```
divisor = average(ATK x AtkSpd x maxHP x (1 + DEF/100)) / 100
```

The CP formula itself is **unchanged** and correct — `ATK × AtkSpd × maxHP × (1+DEF/100) / divisor`,
the duel condition rearranged, untouched since commit `d505719`.

### `LevelGameManager`

Removed the `if (CPBattleController.Instance.IsPrepared) return;` guard in `Update`.
The reporter is now prepared for *every* battle, so that check would have disabled
mutual-wipe detection permanently and hung stages in `Playing` forever.

---

## 3. Current stat values

### Heroes — all 8, `Assets/Scriptable Objects/Stats/BaseStats/New ChratersSTats/`

| group | ATK | HP | DEF | AtkSpd | moveSpeed |
|---|---|---|---|---|---|
| CowMinotaur_2, Dark_Oracle_01, Golem_3, Minotaur_02, Player_Dark_Oracle_3 | **90** | 281 | 25.0 | 0.70 | 0.60 |
| Player_Minotaur_01, PlayerFallen_Angels_02, PlayerValkir3 | **90** | 291 | 25.9 | 0.70 | 0.60 |

Started at ATK 64/72, HP 100. HP was raised to reach the 8-blow baseline; ATK was
raised repeatedly to win duels (see §4).

### Levels 1-3 — dedicated tutorial copies, DO NOT reuse elsewhere

`Assets/Scriptable Objects/Stats/BaseStats/Enemy Base Stats/Tutorial/`

| asset | ATK | HP | DEF | used by |
|---|---|---|---|---|
| `Enemy_Reaper_Man_01_Tutorial` | 3.78 | 393 | 30 | L1, L2 (via `Spawner2`) |
| `Enemy_Reaper_Man_01_Tutorial_L3` | 2.80 | 394 | 30 | L3 only |
| `Enemy_Zombie_villager_Tutorial` | 2.80 | 400 | 28 | L3 only |

Enemies are crippled through **ATK** while **HP is high**, so the fight still takes ~8
blows and does not look instant. Achieved with **no code exception** — there is no
`if (level <= 3)` anywhere.

**These are now badly out of date.** They were sized when hero CP was ~30; hero CP is
now ~116-121, so the tutorial ratio has drifted from the intended 3.7:1 to roughly
**12:1**. Still guaranteed wins, just far more lopsided than Arash asked for. Rescaling
them is open work.

### Level 4 — the shared originals (also used by L5-L20, untuned)

| asset | base ATK | base HP | base DEF | base AtkSpd |
|---|---|---|---|---|
| `Enemy_Reaper_Man_01` | 59.55 | 204.7 | 22.74 | 0.660 |
| `Enemy_Zombie_villager` | 62.11 | 213.5 | 23.72 | 0.660 |

Grown at stage 4 these become ATK 74.6 / 77.8, HP 271 / 282, DEF 24.1 / 25.1, AS 0.70 —
deliberately mirroring the hero's shape at −7% and −3% per stat.

Note: cutting each stat 7% gives **~15% less CP**, not 7%, because CP multiplies three
stats. For exactly −7% CP, cut each stat ~2.4%.

### Level 4 waves

- `LevelBattleRules.EnemyWaves[4]` = **`[2, 2]`** (was `[2,3]`) — 4 enemies total.
- Both waves: `AllTogetherGrid`, `gridColumns = 2`, so one row, same Y.
- Spawn x = **±4.5** ← **this is what deadlocks the level, see §0.**

---

## 4. The five findings that actually mattered

### 4.1 Integer blow counts hide sub-blow advantages — the big one

A duel was measured at:

```
hero needs 4.05 swings  ->  5 blows
enemy needs 4.52 swings ->  5 blows
```

A real **12% advantage became zero** because both rounded into the same bucket. With
equal attack speeds the fight then came down to who swung first, and the hero lost.

**Any advantage smaller than one whole blow is worth nothing.** Two +10% ATK buffs
changed nothing (4.5 → 4.2 → 4.05 swings, all "5"); the final 1.2 attack points that
crossed into "4" mattered more than the previous 20% combined.

**When checking a matchup, read the exact swing count, not CP.** Two units can differ
15% in CP and be perfectly tied in practice.

### 4.2 Blows are counted per VICTIM, not per attacker

`hitsTaken` counts blows from **everyone**. Two enemies that each need 5 blows kill a
hero in **~5 total** (2-3 visible from each), not 10. The 8-blow baseline was only ever
true for a 1-v-1 duel; against two attackers it halves, against three it thirds.

This is why heroes "died in 4 hits" while every stat said 5.

### 4.3 The hero/enemy count mismatch is why the old fake existed

Stages 1-5 deploy 3, 4, 5, 8, 12 heroes against 1, 2, 3, 5, 5 enemies. Melee advantage
is superlinear in count (Lanchester), so those fights ended instantly and had to be
scripted to look like fights. Bringing counts closer together is open work.

### 4.4 One hero cannot beat four enemies, and should not

At level 4: hero ~116 CP vs 4 enemies totalling ~356-387 → **R ≈ 0.30**. Losing is
correct. Arash's own reference workbook agrees:

```
4  match 1   125 vs 400   Fast Loss
4  match 2   250 vs 400   Fast Loss
4  match 3   500 vs 400   Fast Win   <- 75% of the board
```

**Level 4 requires 3 cleared matches to win.** Testing with one match will always lose,
however the stats are tuned.

### 4.5 The reference workbook is a no-upgrade baseline, and its CP scale is not the game's

`Wittle_Defender_Levels_6-10_Balance.xlsx` (in `Assets/Documentation for scripts/`)
assumes Player CP 100 / 125 flat. The game's heroes were **28-31.5**. That 100 never
existed in the game — the deleted normalization *forced* heroes to 125 to make reality
match the sheet.

Its Battle Result column is reproducible from the CP ratio alone, verified against all
51 rows with zero mismatches — now a **prediction to measure**, not a rule to enforce:

```
R < 0.75  Fast Loss     R < 1.00  Slow Loss
R < 1.15  Normal Win    else      Fast Win
```

---

## 5. Two more bugs found and fixed along the way

- **Stage 3's new enemy type could never spawn.** `EnemyWaveCounts(3) = [3]` is ONE
  wave, and `BuildWavesFromRules` only reads the asset's wave 0 in that case — which
  held Reapers only, with the Zombie tucked into wave 1. Fixed by putting both types in
  wave 0 (spreads as 2+1).
- **`Spawner1.asset` is dead** — referenced by no scene and no prefab. Stages 1 and 2
  both inherit `Spawner2` from `LevelTemplate.prefab`.

## 6. A trap that cost three wrong edits

`EnemySpawner.spawnRelativeToEnemyGate` is a **per-scene** toggle. When ON (it is, in
`Level_1_Stage_4`), the wave asset's `spawnMin`/`spawnMax` are **ignored** and the box
comes from `gateRelativeMin`/`gateRelativeMax` on the *component*. But
`minSlotSpacing` is still read from the asset — so setting a spacing wider than the
component's box silently clamps every unit onto the same point.

**Check `ResolveSpawnArea` before editing any spawn geometry.**

---

## 7. Verification status

**Play-tested and passing:**
- Stage 1, full real sequence (1 match → BATTLE → enemy spawns → 6s load bar →
  hero deploys → jumps in → fights): `Won`, enemy died in **8 blows**, hero finished
  **78.8/100 HP**, castle untouched **500/500**. Predicted enemy blow 3.02 damage,
  actual 3.02.

**Not verified:**
- Levels 2, 3 since the last stat changes.
- Level 4 — **cannot be tested until the deadlock in §0 is resolved.**
- Levels 5-20 — never touched, still carry the old broken calibration.
- The first-strike rule — compiles, never seen in play.

## 8. Open work, roughly in priority order

1. **Resolve the level 4 deadlock** (§0) — blocks everything else.
2. **Rescale levels 1-3 tutorial enemies** — ratio drifted to ~12:1 (§3).
3. **Decide the hero/enemy count relationship** (§4.3).
4. **Retune `ProgressionConfigSO`** — enemy growth compounds to ~+22% CP per stage
   (≈44× by stage 20). Sizing: `U = g^N` for upgrades every N stages; at N=5 a
   1.25→0.95 ratio swing wants ~+6%/stage. Per-type curves need **no code** —
   `progression` is a per-prefab field, so one asset per enemy type is enough.
5. **Levels 5-20** are untuned.
6. **Hero and enemy moveSpeed are not equal** — 0.60 vs 0.50, despite an older
   `SESSIONS.md` note claiming they are. Arash wanted them equal; never actioned.
7. **Build the win-rate harness** — `Assets/Scripts/Editor/CPCombatVerification.cs` is
   still a single pass over stages 1-5. To make CP trustworthy it needs to sweep R from
   ~0.7 to ~1.5 and team sizes 2v2→12v12, ~200 runs per cell. Target: R ≥ 1.20 wins
   ≥ 88%, R = 1.00 lands 40-60%, R ≤ 0.85 wins ≤ 12%, **no cell at 0% or 100%**.
8. **Set `CPBattleController.LogEveryBlow = false`** when tuning is done.

## 9. Standing decisions — do not re-litigate

- CP **describes**, never commands. No code may decide a winner.
- Stats are tuned for the **fight**; the **divisor** is what makes CP read ~100.
- Blows-to-die is **emergent**. 8 is a base-state authoring target, not a runtime rule.
  `SafetyBlowFloor = 4` is an anti-one-shot guard only.
- A hero moves at **one constant speed**; only a full stop may change it.
- The **hero always lands the first blow** of an engagement.
- Level exceptions are made with **data** (dedicated stat assets), never with
  `if (level == N)` in code.
