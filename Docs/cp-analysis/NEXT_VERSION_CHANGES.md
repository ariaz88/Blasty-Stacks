# NEXT_VERSION_CHANGES — everything queued for the next version

    Compiled : 2026-09-09
    Source   : the CP redesign discussion (see CP_DISCUSSION_LOG.md) plus the
               9-item defect register in CP_SYSTEM_ANALYSIS.md
    Status   : NOTHING below is implemented except A0.

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

### A1 · 🟢 Replace the CP formula
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

### A2 · 🟢 Remove `attackRange` from CP **and** from growth
- **Files:** `CPCalculator.cs`, `CPConfigSO.cs`, `ProgressionMath.cs`, `ProgressionConfigSO.cs`
- **Why:** Arash's directive. Range is inert in gameplay — players use
  `PlayerManager.maxAttackRange` (0.85) and enemies `EnemyLocoMotion.stoppingDistance` (0.83); the
  stat-block value is never read.
- **Measured cost:** the range term is 0.052% of CP at L1 and 0.003% at L50. Dropping it moves the
  displayed integer in 3 of 20 hero rows and 5 of 120 enemy rows, always by exactly 1.
- **Note:** all 15 units are Warrior, so "exclude for melee" and "exclude entirely" are identical
  today. Deleting the term outright is simpler; an `if (!isRanged)` guard would preserve it for a
  future Archer/Mage. **Arash to confirm which.** 🟡

### A3 · 🟢 Remove `moveSpeed` from CP (keep its growth)
- **Why:** movement does not decide a duel. Arash agreed weight 0.
- **Important:** this removes it from **CP only**. `moveSpeed` still grows, purely to convey
  progression. See C3.

---

# B. Making the stats real (they are currently disconnected from the game)

### B1 · ⚠️ 🟢 Wire movement to `unitStats.moveSpeed`
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

### B2 · ⚠️ 🟢 Make `attackSpeed` actually control attack rate
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

---

# C. Growth curves

### C1 · 🟢 ⚠️ Fix the two off-axis progression curves
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

### C3 · 🔴 `moveSpeed` growth rate
Arash: base ~0.3, then rising with upgrades — roughly **10–15% every 4–5 levels** — purely for the
feel of progression. **Explicitly deferred: do not set this until he says.**

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

### D1 · 🟢 The menu and the battlefield compute different stats
- **Files:** `UnitsPanelController.cs:517, 579, 881, 962` and `NewCharacterStats.cs:235` apply only
  `gA/gH/gMv/gAS`; `PlayerStatsApplier.cs:135-142` applies all six.
- **Effect:** a level-10 hero **fights with defense 43.1** while every menu — and the CP built on it —
  says **25**.
- **Note:** `PlayerProgressionService.TryGetUnitStatsSnapshot` (`:233-234`) already does all six
  correctly and **nothing calls it**. Route the UI through it.
- *Register item 02. This is the second-biggest correctness bug after C1.*

### D2 · 🟢 Enemy defense never grows
- **File:** `EnemyManager.cs:113-118` — `RebuildFromBase` applies only 4 of 6 multipliers, with a
  comment acknowledging the gap.
- **Effect:** defense is the one axis where the six archetypes meaningfully differ (28 → 78), and it
  stays flat across all 20 stages while attack ×3.6 and HP ×4.9.
- *Register item 06.*

### D3 · 🟢 A second, divergent CP formula inside `EnemyManager`
- **File:** `EnemyManager.cs:146-161`, reachable whenever `cpWeights` is null.
- It has no range term and treats Mage as melee, so it disagrees with `CPCalculator` even given
  identical weights. `EnemySpawner.cs:410` overwrites the prefab's config at spawn, so leaving one
  Inspector slot empty silently downgrades every enemy in a stage to this fallback.
- **Delete it** and make a null config a loud error instead.
- *Register item 05.*

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
| E4 | Range: delete the term outright, or guard it with `if (!isRanged)` for future ranged units? | A2 |
| E5 | Flavour system: delete it, or re-author the curves for a future multi-class roster? | D4 |
| E6 | Display divisor `K` — is ~200 right, i.e. should CP stay near today's magnitudes? | A1 |

---

# Suggested order

1. **A1 + A2 + A3** — the formula. Self-contained, no balance risk, immediately fixes the 9.8% duel error.
2. **C1** — the off-axis curves. Pure correctness; the game is silently running numbers nobody chose.
3. **D1 + D2 + D3** — the correctness bugs. Independent of the balance pass.
4. **B1 + B2** — wiring the stats to the game. ⚠️ Both carry balance traps; do them together with their retunes.
5. **C2 + C4 + C5** — the balance pass. 🔴 Requires the spreadsheet.
6. **D4 + D5 + D6** — cleanup, once the shape is settled.

**Steps 1–3 can start immediately.** Step 4 needs a Play-mode check afterwards. Step 5 is the only
part waiting on Arash.

---

# What has NOT been touched

For the record: across this entire discussion exactly **one line of game code** changed —
`CPWeightMath.cs:41` (A0). No `.asset`, `.prefab` or `.unity` file has been modified. Everything else
listed above is designed and evidenced but unimplemented.
