# NEXT_VERSION_CHANGES — everything queued for the next version

    Compiled : 2026-09-09
    Source   : the CP redesign discussion (see CP_DISCUSSION_LOG.md) plus the
               9-item defect register in CP_SYSTEM_ANALYSIS.md
    Status   : Group A (A0-A3) + D3 → commit `d505719`.
               Group B (B1, B2)     → commit `5587d2a`.
               C3 + attackSpeed flattening → commit `15ca808` (2026-09-10 directives).
               Remaining: C1, C2, C4, C5, D1, D2, D4-D6.

Every item states what changes, why, the risk, and whether it is blocked. Order within each group is
the order to do them in — later items assume earlier ones.

---

## Legend

| | |
|---|---|
| 🟢 | ready to implement now, no decision needed |
| 🟡 | needs a decision or a number from Arash first |
| 🔴 | blocked on the CP progression spreadsheet |
| ⚠️ | has a balance trap — read the note before starting |

---

# A. The CP formula

### A0 · ✅ DONE — `CPWeightMath` never sampled `meleeMultByLevel`
Commit `80ed65b`. `rangedMult` was assigned twice on consecutive lines and the melee line was
missing, so `meleeMult` kept its hard-coded `1f`. **Verified to change no CP value** — the
`Clamp(...,1,5)` floor lifts the off-axis curve's 0.060 back to the 1.0 it already produced.
*Defect register item 03 — closed.*

### A1 · ✅ DONE — Replace the CP formula (commit `d505719`)
```
OLD:  CP = round( wA·ATK + wH·HP + wMv·Move + wAS·AtkSpd + wD·DEF + wR·Range ) × typeMult
NEW:  CP = round( ATK × AtkSpd × EffectiveHP ÷ K )
      where EffectiveHP = HP × (100 + DEF) / 100
```
- **Files:** `Assets/Scripts/Data/CombatPower/CPCalculator.cs`
- **Why:** it is the duel condition rearranged, so higher CP wins by construction. Scores 100% on
  every stat regime tested; the current formula scores 90.2% with a one-directional bias.
- **Reuse:** `CPCalculator.EffectiveHP` (`CPCalculator.cs:50-54`) already computes exactly this and
  has zero call sites.
- **`K` is a cosmetic display divisor** (~200) so CP stays near today's familiar magnitudes. Dividing
  every CP by the same constant changes no ordering.
- **Consequence:** `CPWeightsConfigSO` and both `.asset` files become almost entirely unused. Do not
  delete them yet — decide that once the new formula is in play.
- **As shipped:** `K = 200`, exposed as `CPCalculator.DisplayDivisor`. **E6 was answered by
  choosing this default** — changing it rescales the whole roster and reorders nothing, so it is a
  one-line cosmetic tweak if the magnitudes feel wrong.
- **Also required by A1:** `EnemyManager.UnitCP_WithFallback` carried a *second*, divergent
  hard-coded CP formula for the `cfg == null` case (register item 05 / D3). Since the new
  `UnitCP` needs no config, that branch was deleted — leaving it would have been actively wrong,
  because it produced numbers on a completely different scale. **There is now exactly one CP
  formula in the project.** D3 is therefore partly closed; what remains of D3 is only "make a null
  config a loud error", which is now moot.
- **Measured effect on the shipped roster** (old → new):

  | unit | old CP | new CP |
  |---|---|---|
  | ArcherStats | 37 | 11 |
  | WarriorStats | 59 | 40 |
  | `Player_Minotaur_01` (ATK 64 × AS 2.0) | **81** | **80** |
  | `Golem_3` (ATK 72 × AS 1.5) | **89** | **68** |
  | Enemy_Golem_02 | 122 | 180 |
  | PlayerMeleeReaper (attackSpeed 4.0) | 114 | 240 |

  The first two highlighted rows are the rank inversion, now corrected: the 128-DPS unit finally
  outranks the 108-DPS one. `PlayerMeleeReaper`'s jump is real — attackSpeed 4.0 is an outlier that
  **B2 is scheduled to re-author**. `GateBase` now scores 0 because its ATK is 0, which is correct
  but will change any UI that showed the old 15.

### A2 · ✅ DONE — Remove `attackRange` from CP **and** from growth (commit `d505719`)
- **Files:** `CPCalculator.cs`, `CPConfigSO.cs`, `ProgressionMath.cs`, `ProgressionConfigSO.cs`
- **Why:** Arash's directive. Range is inert in gameplay — players use
  `PlayerManager.maxAttackRange` (0.85) and enemies `EnemyLocoMotion.stoppingDistance` (0.83); the
  stat-block value is never read.
- **Measured cost:** the range term is 0.052% of CP at L1 and 0.003% at L50. Dropping it moves the
  displayed integer in 3 of 20 hero rows and 5 of 120 enemy rows, always by exactly 1.
- **E4 answered by the directive "Remove attackRange from CP and growth":** the term was **deleted
  outright**, not guarded. A future ranged class would need it re-added — but note the new formula
  has no per-stat weights at all, so range would have to enter some other way regardless.
- **What was actually removed:** `CPWeightMath.Weights.wR`, `CPWeightsConfigSO.wRangeByLevel`,
  `ProgressionMath.Growth.gR`, `ProgressionConfigSO.rangePctByLevel`, the `rangePct` parameter of
  `UnitStatsRuntime.ApplyLevelGrowth`, and all three consumers of `gR`
  (`PlayerStatsApplier.cs:141`, `PlayerProgressionService.cs:242,250`).
- **Still present on purpose:** `UnitStatsSO.attackRange`, `UnitStatsRuntime.attackRange`, the
  `rangeMult` buff parameter of `ApplyMultipliers`, and the range readouts in the UI. Range is still
  a stat — it just neither grows nor scores. Its upgrade delta now always reads 0.
- **Asset note:** the `.asset` files still carry serialized `rangePctByLevel` / `wRangeByLevel`
  blocks. They are already inert; Unity drops them the next time it re-saves those assets.

### A3 · ✅ DONE — Remove `moveSpeed` from CP (keep its growth) (commit `d505719`)
- **Why:** movement does not decide a duel. Arash agreed weight 0.
- **Important:** this removes it from **CP only**. `moveSpeed` still grows, purely to convey
  progression. See C3.

---

# B. Making the stats real (they are currently disconnected from the game)

### B1 · ✅ DONE — Wire movement to `unitStats.moveSpeed` (commit `5587d2a`)
**As shipped:** `PlayerManager.CurrentMoveSpeed` and `EnemyLocoMotion.CurrentMoveSpeed` read the live
stat block, falling back to the serialized Inspector field only when the stat block is missing or not
yet built. All movement sites rewired (PlayerManager 685/741; all five in `EnemyLocoMotion`).
Every `UnitStatsSO.moveSpeed` re-authored to **0.3**, in the same commit, so the trap never fires.
**No prefab was modified** — the `0.5` / `0.2` overrides simply become unused fallbacks.
`TopDownMover2D` still holds a raw `moveSpeed`, but it is on **none** of the 15 character prefabs and
`EnemyManager` only ever disables it — verified, no action needed.

~~⚠️ **Known consequence, deferred to C3:** enemy level = stage number, so enemy `moveSpeed` compounds
with `g.gMv` every stage while a hero only grows on upgrade. Enemies get measurably faster than
heroes late in the campaign.~~ → **FIXED in `15ca808`**: `moveSpeed` was removed from growth
entirely, so it is a flat 0.3 at every stage. See C3.

<details><summary>original entry</summary>

- **Files:** `Assets/Scripts/Combat/Player/PlayerManager.cs` (lines 64, 685, 741),
  `Assets/Scripts/Combat/Enemy/EnemyLocoMotion.cs` (lines 20, 104, 128, 155, 214, 257)
- **The trap:**

  | | stat sheet says | game actually uses |
  |---|---|---|
  | Players | `moveSpeed 3.5` | **0.5** |
  | Enemies | `moveSpeed 2.9–3.5` | **0.2** |

  Wiring the stat through naively makes **players 7× and enemies 15× faster** and destroys the
  battlefield pacing.
- **Do this:** make the stat authoritative **and simultaneously** rewrite the SO `moveSpeed` values
  to **0.3** for both players and enemies (Arash's chosen base). The `0.5`/`0.2` Inspector fields are
  the real tuned values; the `3.5` in the SOs is legacy from a 3D template.
- **Affects:** 15 prefabs + ~14 `UnitStatsSO` assets.
</details>

### B2 · ✅ DONE — Make `attackSpeed` actually control attack rate (commit `5587d2a`)
**As shipped:** `PlayerManager.AttackCadence` and `EnemyManager.AttackCadence` both return
`recoveryTime / attackSpeed`, with `attackSpeed` floored at **0.05** so a zero or negative stat cannot
produce an infinite or negative cooldown. Applied at both player sites and **all three** enemy sites
(`509`, `575`, `704`) — the plan listed only two.

**⚠️ The plan's retune was wrong and was NOT followed literally.** Today every unit swings on a flat
0.6 s regardless of `attackSpeed`, so after B2 each unit's DPS is multiplied by its **new**
`attackSpeed`. Putting players in 0.9–1.3 (mean ≈ 1.10) while leaving enemies at 0.85–1.00
(mean 0.93) would have handed players a **~19% relative power gain** — i.e. made the game *easier*,
the opposite of the stated goal.

**What was done instead:** both sides re-authored into 0.9–1.3, rank preserved *within* each side,
mean ≈ 1.00 on *each* side — so `attackSpeed` becomes a real lever with no difficulty shift:

| side | mean attackSpeed | total DPS before → after | change |
|---|---|---|---|
| player heroes (8) | 1.0125 | 920.0 → 924.0 | **+0.43%** |
| enemy archetypes (6) | 1.0100 | 475.0 → 471.9 | **−0.65%** |

Net relative shift **~1.1%** in the players' favour, versus ~19% if the spec had been followed.

| old attackSpeed | new | who |
|---|---|---|
| 1.5 | **0.90** | Golem_3, Player_Dark_Oracle_3, Minotaur_02, CowMinotaur_2, Dark_Oracle_01 |
| 2.0 | **1.20** | Player_Minotaur_01, PlayerFallen_Angels_02, PlayerValkir3 |
| 0.85 | **0.90** | Enemy_Golem_01, Enemy_Golem_02 |
| 0.90 | **0.97** | Enemy_Zombie_villager |
| 0.95 | **1.05** | Enemy_Skeleton_Crusader_1 |
| 1.00 | **1.12** | Enemy_Orc, Enemy_Reaper_Man_01 |
| 4.00 | **1.30** | PlayerMeleeReaper — a wild outlier, now sane |

`GateBase` untouched (`attackSpeed 0`, deals no damage). 23 assets rewritten in total.

**Note for C5:** cadence now lands at 0.46–0.67 s, straddling the old flat 0.6 s.

<details><summary>original entry</summary>

- **Files:** `Assets/Scripts/Combat/Player/States/PlayerAttackState.cs:76,100`,
  `Assets/Scripts/Combat/Enemy/EnemyManager.cs:579,708`
- **Current behaviour:** cadence is `PlayerAttackAction.recoveryTime`, a flat **0.6 on all 10 attack
  assets**, released purely by a timer (`PlayerManager.cs:798-812`). `attackSpeed` appears nowhere in
  that path — it only sets animator playback speed. **The stat does nothing.**
- **Change:** `cadence = recoveryTime / attackSpeed`
- **The trap:** with today's values (2.0 / 1.5) heroes become **1.5–2× stronger** and enemies weaker.
  Every stage becomes dramatically easier — the opposite of what Arash wants.
- **Do this:** re-author `attackSpeed` into **0.9–1.3** at the same time. Cadence then lands at
  0.46–0.67 s, straddling today's flat 0.6 s. Attack speed becomes a real lever with no free power
  jump.
- **Bonus:** the animation already scales by `attackSpeed`, so animation and cadence stay in sync and
  the damage event lands at the same point in the cycle. Mechanically clean.
</details>

---

# C. Growth curves

### C1 · ✅ DONE — Fix the two off-axis progression curves
- **File:** `Assets/Scriptable Objects/Stats/Progression/PlayerProgressionConfig.asset`
- **Problem:** `defPctByLevel` has keyframes at t ≈ −9.0 and `rangePctByLevel` at t ≈ −1.1 — entirely
  off the level axis. Curves clamp past their last key, so `Evaluate(l)` returns the same value at
  **every** level: a flat **+6.25% defense and +6.75% range per level, compounding, for all 50
  levels**. At level 20 that is a **×3.17 defense multiplier nobody chose**.
- **`pctClamp` does not catch it** — the values sit inside the legal window; only their position on
  the axis is wrong.
- **Must be done in the Unity Inspector** (drag the keys back onto x = 1…50). `rangePctByLevel`
  becomes moot once A2 lands.
- *Defect register item 01.*

### C2 · 🔴 Rebalance the player growth rates
Arash confirmed: **stay at +8% → +3% per level, tapering.** Rejected the +21–33% figure that would
have let the player fully keep pace. Final values depend on the spreadsheet.

### C3 · ✅ RESOLVED — `moveSpeed` has NO growth at all (commit `15ca808`)
**Arash, 2026-09-10:** *"moveSpeed رو نباید داخل فرمول g.g اصلا قرار داد — چه برای پلیر چه انمی. فعلاً
همون 0.3 بمونه و اگر نیاز شد می‌گم کی و چجوری تغییر کنه."*

This **supersedes** the earlier "10–15% every 4–5 levels" idea, which is withdrawn.

`ProgressionMath.Growth` no longer has a `gMv` field; `ProgressionConfigSO` no longer has
`movePctByLevel`; `UnitStatsRuntime.ApplyLevelGrowth` no longer takes `movePct`. All **eight**
consumers were updated — `PlayerStatsApplier`, `PlayerProgressionService` (×2), `EnemyManager` (×2),
`UnitsPanelController` (×5), `NewCharacterStats`.

moveSpeed is now a genuinely constant **0.3** — same at level 1 and at stage 20, players and enemies
alike. **This closes the problem B1 introduced**: enemy level = stage number, so a growing moveSpeed
made enemies outpace heroes late in the campaign. With no growth axis it cannot happen, and the
caution added to `EnemyLocoMotion.txt` is retracted.

Re-introducing movement growth later needs a code change, not just data.

### C3b · ✅ DONE — `attackSpeed` flattened to 0.7 for every unit (commit `15ca808`)
**Arash, 2026-09-10:** *"برای اتک اسپید هم بجای 0.9_1.3 فعلاً روی 0.7 بذار برای همه."*

This **supersedes the 0.9–1.3 retune** shipped in `5587d2a`. All 23 assets are now `attackSpeed: 0.7`
(`GateBase` excepted — it is 0 and deals no damage).

**Balance is exactly untouched**, because a uniform factor cancels out of the comparison:

| | before B2 | at 0.9–1.3 | at 0.7 |
|---|---|---|---|
| hero total DPS | 920.0 | 924.0 | 644.0 |
| enemy total DPS | 475.0 | 471.9 | 332.5 |
| **hero ÷ enemy** | **1.9368** | 1.9580 | **1.9368** |

What it *does* change:
- cadence is a uniform `0.6 / 0.7` = **0.857 s**, so every fight runs **~43% longer**
- every CP drops **30% uniformly** → ordering untouched
- attack animations play at 70% speed, staying in sync with cadence

⚠️ **Design consequence worth knowing:** with `attackSpeed` identical everywhere it no longer
differentiates anyone, so CP order is now decided by **ATK × EffectiveHP alone**. The fast-vs-slow
hero split that A1 was built to rank correctly (64 ATK / 2.0 speed vs 72 ATK / 1.5 speed) **no longer
exists in the data** — every unit swings at the same rate, so raw ATK decides damage. A1 is still
correct; there is simply nothing left for its `attackSpeed` term to separate. Restoring per-unit
attackSpeed later is a **data** change only, no code needed.

### C4 · 🔴 ⚠️ Enemy growth curves — the difficulty sawtooth
- **The problem:** enemy level = stage number (20 levels of growth), but upgrades are gated to every
  ~4–5 stages (~5 upgrades). The gap widens **8.6×** across the campaign:

  | Stage | enemy lvl | player lvl | enemy ÷ player power |
  |---|---|---|---|
  | 1 | 1 | 1 | 5.3× |
  | 8 | 8 | 2 | 13.9× |
  | 20 | 20 | 5 | **45.8×** |

- **The intended design:** a sawtooth — difficulty rises between upgrades, near-parity restored at
  each upgrade.
- **BLOCKED.** Needs the CP progression spreadsheet plus two numbers:
  1. How close is "small gap" at the moment of an upgrade — player slightly ahead (~1.1×), even, or still behind?
  2. How wide should the gap grow just before the next upgrade — 1.5×? 2×?

### C5 · 🔴 Raise HP relative to ATK so fights last longer
- **Why:** this is what makes "higher CP wins" actually hold. The formula is exactly monotonic in
  continuous maths (0 errors in 200,000 duels), but **hits are whole numbers**, and a small CP edge is
  invisible unless it removes a whole hit from the kill.

  | CP margin | higher CP wins |
  |---|---|
  | 0–0.1% | 52.9% — coin flip |
  | 1–2% | 64.4% |
  | 5–10% | 91.9% |
  | 10%+ | 99.4% |

  | hits per kill | CP correct |
  |---|---|
  | **3–6 — the game today** | ~95% |
  | ~9 | 97.7% |
  | **15–20 — recommended** | **~99%** |
  | ~36 | 99.4% |

- **Target 15–20 hits per kill.** Also makes battles read better — units trade blows instead of
  deleting each other in three swings.
- Interacts with C2/C4, so do it as part of the same balance pass.

---

# D. Defects still open (from the 9-item register)

### D1 · ✅ DONE — The menu and the battlefield compute different stats
- **Files:** `UnitsPanelController.cs:517, 579, 881, 962` and `NewCharacterStats.cs:235` apply only
  `gA/gH/gMv/gAS`; `PlayerStatsApplier.cs:135-142` applies all six.
- **Effect:** a level-10 hero **fights with defense 43.1** while every menu — and the CP built on it —
  says **25**.
- **Note:** `PlayerProgressionService.TryGetUnitStatsSnapshot` (`:233-234`) already does all six
  correctly and **nothing calls it**. Route the UI through it.
- *Register item 02. This is the second-biggest correctness bug after C1.*

### D2 · ✅ DONE — Enemy defense never grows
- **File:** `EnemyManager.cs:113-118` — `RebuildFromBase` applies only 4 of 6 multipliers, with a
  comment acknowledging the gap.
- **Effect:** defense is the one axis where the six archetypes meaningfully differ (28 → 78), and it
  stays flat across all 20 stages while attack ×3.6 and HP ×4.9.
- *Register item 06.*

### D3 · ✅ DONE — A second, divergent CP formula inside `EnemyManager` (commit `d505719`)
Deleted as a required consequence of A1. See the A1 entry. The remaining half of the original
suggestion — "make a null config a loud error" — is moot: the new `UnitCP` does not use the config.

<details><summary>original entry</summary>

- **File:** `EnemyManager.cs:146-161`, reachable whenever `cpWeights` is null.
- It has no range term and treats Mage as melee, so it disagrees with `CPCalculator` even given
  identical weights. `EnemySpawner.cs:410` overwrites the prefab's config at spawn, so leaving one
  Inspector slot empty silently downgrades every enemy in a stage to this fallback.
- **Delete it** and make a null config a loud error instead.
- *Register item 05.*
</details>

### D4 · 🟡 The flavour-multiplier system
- Both `meleeMultByLevel` and `rangedMultByLevel` in `Player CP.asset` are authored off-axis
  (t ≈ −389 / −730). Every `typeMult` in the game is 1.0.
- **But A1 removes `typeMult` entirely.** So the choice is: delete the flavour system with the old
  formula, or re-author the curves for a future multi-class roster.
- All 15 units are `FighterType.Warrior`, so it is a no-op either way today.
- *Register item 04. Decision needed.*

### D5 · 🟢 The menu's "TOTAL CP" is hard-coded art
- `MenuScene.unity:24724` reads `3460` and `:36403` reads `32660` under a `TOTAL CP:` caption. Their
  TextMeshPro components are bound to **no script**.
- Either wire them to a real computed total (`CPCalculator.SquadCP` exists with zero call sites) or
  remove them.
- *Register item 08.*

### D6 · 🟢 Documentation drift
- `CPCalculator.txt` names `UnitCardView` / `BucketStatsPanel` as callers — neither calls it.
- `UnitStatsRuntime.txt` and `ProgressionMath.txt` both state `ApplyLevelGrowth` runs at spawn; it has
  **zero call sites** anywhere.
- All three need rewriting after A1 lands anyway.
- *Register item 09.*

---

# E. Decisions needed from Arash

| # | Question | Blocks |
|---|---|---|
| E1 | The CP progression spreadsheet for levels 1–20 | C2, C4, C5 |
| E2 | Sawtooth: how close is the gap at an upgrade? (player ~1.1× ahead / even / behind) | C4 |
| E3 | Sawtooth: how wide does the gap grow before the next upgrade? (1.5× / 2×) | C4 |
| ~~E4~~ | ~~Range: delete outright or guard?~~ **Answered: deleted outright** (A2 shipped) | — |
| E5 | Flavour system: delete it, or re-author the curves for a future multi-class roster? | D4 |
| ~~E6~~ | ~~Display divisor `K`~~ **Shipped at 200** as `CPCalculator.DisplayDivisor`. Revisit only if the displayed magnitudes feel wrong — it reorders nothing | — |

---

# Suggested order

1. ~~**A1 + A2 + A3** — the formula.~~ ✅ **DONE**, commit `d505719`. Took D3 with it.
2. **C1** — the off-axis curves. Pure correctness; the game is silently running numbers nobody chose.
   **This is now the top item.** Must be done in the Unity Inspector.
3. **D1 + D2** — the correctness bugs. Independent of the balance pass.
4. ~~**B1 + B2** — wiring the stats to the game.~~ ✅ **DONE**, commit `5587d2a`.
5. **C2 + C4 + C5** — the balance pass. 🔴 Requires the spreadsheet.
6. **D4 + D5 + D6** — cleanup, once the shape is settled.

**⚠️ B1 + B2 have NOT been verified in Play mode.** They are the first changes in this whole effort
that alter what happens on the battlefield, and nothing in this repo can test them — there is no CLI
build and no authored test suite. **Open `StarterScene` and play a stage before trusting them.**
What to watch for:
- units walk at a sane pace (0.3 base, not crawling and not sprinting)
- heroes and enemies visibly differ in swing rate, and the swing animation still lands its hit
- a stage that used to be beatable still is — the retune is arithmetically neutral, but arithmetic
  neutrality is not the same as feeling the same

Steps 2 and 3 remain safe to start. Step 5 is still waiting on Arash's spreadsheet.

---

# What has NOT been touched

Three commits of game code exist:

| commit | what |
|---|---|
| `80ed65b` | A0 — one line |
| `d505719` | A1 + A2 + A3 + D3 — nine `.cs` files + nine reference docs |
| `5587d2a` | B1 + B2 — four `.cs` files, **23 `.asset` files**, four reference docs |

**No `.prefab` and no `.unity` file has been modified at any point.** The prefab `moveSpeed`
overrides (`0.5` player / `0.2` enemy) are still there; they are simply no longer read.

`.asset` files were first touched in `5587d2a`, and only the `moveSpeed` and `attackSpeed` lines of
`UnitStatsSO` assets. **The growth-curve assets are still untouched**, so C1 — the highest-value
remaining fix — is unaffected and must still be done in the Unity Inspector.

Remaining: **C1–C5, D1, D2, D4–D6**, plus decisions E1, E2, E3, E5.

---

# ⚠️ Scope note — what A1 does and does not guarantee

A1 makes the CP number exact **for a 1v1 duel**. That guarantee does not extend to group battles,
and the difference is not a tuning gap — it is structural. Measured against a simulation of the real
combat rules (nearest-enemy sticky targeting, walk time, individual HP and damage):

| Total CP gap between the two sides | higher-CP side actually wins |
|---|---|
| < 1% | ~51% — a coin flip |
| 1–5% | ~52–60% |
| 5–10% | ~55–71% |
| > 10% | ~64–90% |

The cause: with sticky nearest-enemy targeting the battle decomposes into several simultaneous local
duels, and **which unit gets paired against which is a geometric accident**. Freezing the stats and
re-shuffling only the positions flips the winner in **31% of matchups**. No team-level scalar can see
pairing, so no Total CP formula — sum, product, or otherwise — can predict it. Removing travel time
entirely, or switching to focus-fire targeting, does not help.

See `CP_DISCUSSION_LOG.md` for the full measurement set and the options that remain.
