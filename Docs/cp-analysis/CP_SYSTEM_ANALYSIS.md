# CP_SYSTEM_ANALYSIS — Combat Power, end to end

    Scope : Assets/Scripts/Data/CombatPower/, Data/Stats/, Data/Units/ + every call site
    Date  : 2026-09-07
    Status: ANALYSIS ONLY — no .cs, .asset, .prefab or .unity file was modified.

**Interactive version with charts:** <https://claude.ai/code/artifact/3d635747-5eb6-43f8-85cf-e868e41e783d>
(same content, bilingual, printable to PDF with Ctrl+P)

> **فارسی:** نسخه‌ی فارسی این سند از بخش «بخش فارسی» در پایین همین فایل شروع می‌شود.

---

## How the numbers in this document were produced

Every figure was recomputed **outside Unity** by parsing the raw `.asset` YAML and
reimplementing `AnimationCurve.Evaluate` (including PreInfinity/PostInfinity clamp behaviour),
`ProgressionMath.GetGrowthMultipliers`, `CPWeightMath.Evaluate` and `CPCalculator.UnitCP`.
Seven reference values were also computed by hand and matched against the script output before
anything here was written. Nothing was transcribed by eye from the Inspector.

---

# 1. The five answers

| Question | Answer |
|---|---|
| How is CP computed? | A flat **weighted sum of six raw stats** × a melee/ranged multiplier. The weights are `AnimationCurve`s sampled at the unit's level, so the recipe itself drifts with level. |
| CP 100 → 120 = +20% on every stat? | **No.** Each stat compounds a *different* curve, and CP weights them so unevenly that four of six are invisible. In practice **CP ≈ ATK + 0.15·HP**. |
| Does it differ per hero / per enemy? | **Growth rates do not.** All 8 heroes share one `ProgressionConfigSO`; all 6 enemy archetypes share another. Only base sheets differ. For enemies the *level* differs — an enemy's level **is the stage number**. |
| Total CP at runtime? | **Obtainable, and the code already exists unused.** Enemies compute CP into a debug field nothing reads; players compute none at all; `CPCalculator.SquadCP` has zero call sites. |
| Is the formula right? | **It is the weakest standard shape and currently ranks the roster backwards.** See §9. |

---

# 2. The formula

```
CP = round( ( wA·ATK + wH·HP + wMv·MoveSpeed + wAS·AtkSpeed + wD·DEF + wR·Range ) × typeMult )
```

`Assets/Scripts/Data/CombatPower/CPCalculator.cs:10-30`. Each `w` is an `AnimationCurve`
evaluated at the unit's level, clamped to `wClamp` (−10…10), then scaled by
`globalWeightScaleByLevel` (clamped 1…10). `typeMult` is `rangedMult` for Archer/Mage,
`meleeMult` for Warrior/Horseman.

There is **no interaction between terms**. HP and DEF never combine into survivability;
ATK and AtkSpeed never combine into damage output. Each stat is priced alone and the prices
are summed. §9 is about the consequences.

## Authored weights — `Assets/Scriptable Objects/CP/Player CP.asset`

| Level | wA attack | wH HP | wMv move | wAS atk spd | wD defense | wR range | typeMult |
|---|---|---|---|---|---|---|---|
| 1  | 1.000 | 0.150 | 0.250 | 0.400 | 0.000 | 0.050 | 1.00 |
| 10 | 0.982 | 0.144 | 0.241 | 0.409 | 0.009 | 0.048 | 1.00 |
| 20 | 0.961 | 0.138 | 0.231 | 0.419 | 0.019 | 0.046 | 1.00 |
| 50 | 0.900 | 0.120 | 0.200 | 0.450 | 0.050 | 0.040 | 1.00 |

`EnemyCP.asset` carries identical stat weights. It has **no `wRangeByLevel` curve at all** (the
asset predates the field), so that weight falls back to the C# initializer
`AnimationCurve.Linear(1, 0.05f, 50, 0.04f)`.

**Attack is worth roughly 20× HP per point, and defense is worth literally nothing at level 1.**

## typeMult is 1.00 everywhere, by accident

Two faults compound:

1. ~~`CPWeightMath.cs:41-42` never reads `meleeMultByLevel`.~~ **FIXED 2026-09-08 (commit `80ed65b`).**
   The line was missing and `rangedMult` was assigned twice on consecutive lines, so `meleeMult` kept
   its hard-coded `1f`. The curve is now sampled — but **the fix changed no CP value**, because of
   fault 2 below: the clamp floor lifts the off-axis curve's `0.060` back to exactly the `1.0` the
   buggy code already produced. Verified at every level 1–50 against both config assets.
2. Both flavour curves in `Player CP.asset` are authored at **negative time** — `meleeMultByLevel`
   at t ≈ −389, `rangedMultByLevel` at t ≈ −730 — where they evaluate to `0.060` and `0.026`.
   `rangedMult` is only rescued to 1.0 by the `Mathf.Clamp(…, 1, 5f)` floor.

All 8 heroes are `FighterType.Warrior`, so no unit in the game has ever received a flavour bonus.

---

# 3. Where the stats come from

```
UnitStatsSO ──FromSO──> UnitStatsRuntime ──level growth──> ┬─> menus:  gA gH gMv gAS      ──> CPCalculator
 (design)                  (live copy)     ProgressionMath  └─> combat: gA gH gMv gAS gD gR
                                                                        ↑ roguelite multipliers (disabled)
```

`ProgressionMath.GetGrowthMultipliers` compounds `∏(1 + pct(l))` for `l = 2..L`, with a
**separate curve per stat**. Six stats, six independent curves — the first reason stats cannot
rise together.

## Two of the six curves are broken the same way as the flavour curves

In `PlayerProgressionConfig.asset`, `defPctByLevel` has keys at t ≈ −9.0 and `rangePctByLevel`
at t ≈ −1.1. Because curves clamp past the last key, `Evaluate(l)` returns the same value at
**every** level:

| Curve | Evaluated at L=1 | L=10 | L=25 | L=50 |
|---|---|---|---|---|
| `defPctByLevel` | 0.062515 | 0.062515 | 0.062515 | 0.062515 |
| `rangePctByLevel` | 0.067460 | 0.067460 | 0.067460 | 0.067460 |

A flat **+6.25% defense and +6.75% range per level, compounding, for all 50 levels**. At level 20
that is a **×3.17 defense multiplier** nobody chose. `pctClamp` (−0.25…0.50) does not catch it —
the values are inside the allowed window, they are simply applied at the wrong place.

`EnemyProgression.asset` omits both curves entirely, so the enemy side falls back to the healthy
C# initializers (`0.02 → 0.005` and `0.01 → 0.003`) — which `EnemyManager` then never applies
anyway.

---

# 4. Does everything grow at the same rate? — No

## Reason one: the stats diverge

Compound growth multipliers from `PlayerProgressionConfig.asset`:

| Level | gA attack | gH HP | gMv move | gAS atk spd | gD defense ⚠ | gR range ⚠ |
|---|---|---|---|---|---|---|
| 1  | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 |
| 5  | 1.348 | 1.448 | 1.080 | 1.080 | 1.274 | 1.298 |
| 10 | 1.916 | 2.242 | 1.184 | 1.184 | 1.726 | 1.800 |
| 20 | 3.603 | 4.942 | 1.402 | 1.402 | 3.165 | 3.457 |
| 30 | 6.153 | 9.722 | 1.628 | 1.628 | 5.804 | 6.641 |
| 50 | 13.399 | 26.597 | 2.064 | 2.064 | 19.518 | 24.504 |

⚠ = produced by the off-axis curves in §3, not by design intent.

Note `gMv` and `gAS` are driven by curves with identical keys and are therefore **always exactly
equal**.

## Reason two: CP prices the stats unevenly

Share of the weighted sum, for the ATK 64 / AtkSpd 2.0 hero profile:

| Level | CP | attack | maxHP | moveSpeed | attackSpeed | defense | attackRange |
|---|---|---|---|---|---|---|---|
| 1  | 81   | 79.29% | 18.58% | 1.08% | 0.99% | 0.00% | 0.053% |
| 5  | 109  | 78.59% | 19.63% | 0.85% | 0.80% | 0.09% | 0.038% |
| 10 | 155  | 77.65% | 20.91% | 0.64% | 0.63% | 0.15% | 0.026% |
| 20 | 293  | 75.68% | 23.35% | 0.39% | 0.40% | 0.17% | 0.013% |
| 50 | 1096 | 70.45% | 29.13% | 0.13% | 0.17% | 0.11% | 0.003% |

**Defense never exceeds 0.17% of CP at any level. Range never exceeds 0.06%.**

## The direct answer

A CP move of 100 → 120 is very nearly **+20% attack and nothing else**. Concretely, taking a hero
from level 1 to level 10 raises CP ×1.91, while attack rises ×1.92, HP ×2.24, attack speed only
×1.18, and defense either ×1.00 (what the menu shows) or ×1.73 (what actually fights) — see §8
defect 02.

---

# 5. Player CP, hero by hero

Eight registered heroes, **two distinct stat profiles**. HP (100), defense (25), move speed (3.5)
and attack range (0.85) are identical on all eight; only attack and attack speed vary, inversely.
All eight are `FighterType.Warrior`.

| id | Hero | baseStats asset | ATK | AtkSpd | True DPS | CP L1 | CP L10 | CP L20 |
|---|---|---|---|---|---|---|---|---|
| 1 | Valkir3 | `Player_Minotaur_01` | 64 | 2.0 | **128** | **81** | 155 | 293 |
| 2 | Dark_Oracle_1 | `Dark_Oracle_01` | 72 | 1.5 | 108 | **89** | 170 | 320 |
| 3 | Minotaur_02 | `Minotaur_02` | 72 | 1.5 | 108 | 89 | 170 | 320 |
| 4 | Minotaur_2 | `CowMinotaur_2` | 72 | 1.5 | 108 | 89 | 170 | 320 |
| 5 | Golem_3 | `Golem_3` | 72 | 1.5 | 108 | 89 | 170 | 320 |
| 6 | Fallen_Angels_02 | `PlayerValkir3` | 64 | 2.0 | **128** | **81** | 155 | 293 |
| 7 | Fallen_Minotaur_01 | `Player_Minotaur_01` | 64 | 2.0 | **128** | **81** | 155 | 293 |
| 8 | Dark_Oracle_3 | `Player_Dark_Oracle_3` | 72 | 1.5 | 108 | 89 | 170 | 320 |

## ⚠ CORRECTED 2026-09-09 — the "ranking is inverted" claim was WRONG

> **This section originally claimed CP ranks the roster backwards. That was an error and has been
> retracted.** It assumed `trueDPS = attack × attackSpeed`, taken from the `UnitStatsSO` field
> comment ("hits/sec") without verifying the game implements it. **It does not.**
>
> `attackSpeed` does not affect a player's rate of attack at all. Cadence is set by
> `PlayerAttackAction.recoveryTime` (`PlayerAttackState.cs:99-100`) and released purely by a timer
> (`PlayerManager.cs:798-812`); `attackSpeed` appears nowhere in that path and is used only as the
> animator playback speed. **All 10 `PlayerAttackAction` assets carry `recoveryTime = 0.6`**, so every
> hero attacks once per 0.6 s and real output is `attack ÷ 0.6`:
>
> | Profile | ATK | AtkSpd | originally claimed | **actual** |
> |---|---|---|---|---|
> | "fast" (ids 1, 6, 7) | 64 | 2.0 | 128 DPS | **106.7 DPS** |
> | "slow" (ids 2, 3, 4, 5, 8) | 72 | 1.5 | 108 DPS | **120.0 DPS** |
>
> The "slow" profile is genuinely **12.5% stronger**, and CP rates it 9.9% higher (89 vs 81).
> **CP ranks these two profiles correctly.**

## What IS wrong with CP — established 2026-09-09 over 407 simulated duels

CP is not inverted, but it **is** an unreliable predictor, and its errors are systematic.
Simulating 407 real player-vs-enemy matchups (2 hero profiles × levels 1/3/5/10/15/20 × 6 enemy
archetypes × stages 1/3/5/10/15/20) with the project's own combat model:

**CP named the wrong winner in 40 of 407 matchups (9.8%)** — and the bias is entirely
one-directional:

| | count |
|---|---|
| CP said **player** wins, enemy actually won | **40** |
| CP said **enemy** wins, player actually won | **0** |

**CP systematically overrates the player.** Worked counterexample — "fast" hero @L3 vs
Skeleton_Crusader_1 @S1:

| | CP | ATK | DEF | HP | kills in |
|---|---|---|---|---|---|
| Player | **94** | 74 | 28 | 121 | 5 hits (3.0 s) |
| Skeleton | **76** | 44 | 52 | 205 | **4 hits (2.4 s)** |

The cause is the weights, not the ordering of the two hero profiles: at level 10 CP prices attack at
**0.982**, HP at **0.144** and defense at **0.009**, while the enemies are built as tanks (DEF 52–78,
HP 205–340) and the heroes as damage dealers. CP sees the heroes' one strength clearly and the
enemies' two strengths barely at all.

Reproduce with `tools/duel.js` and `tools/duel2.js`.

---

# 6. Enemy CP, stage by stage

**There is no separate difficulty system.** No `stageMultiplier`, no `hpMult`, no per-stage scaling
asset. The spawner hands the stage number to the enemy and the enemy uses it as its progression
level:

```csharp
// EnemyManager.cs:90-99
public void Initialize(int stageLevelFromSpawner)
{
    stageLevel = stageLevelFromSpawner;
    // IMPORTANT: use the stage as this enemy's "unit level" for progression.
    unitLevel = stageLevelFromSpawner;
    RebuildFromBase();
}
```

`RebuildFromBase` applies only **four of six** growth multipliers — defense and range never grow
(`EnemyManager.cs:113-118`). Defense is the one axis where the archetypes have real spread
(28 → 78), and it stays flat across all twenty stages while attack ×3.6 and HP ×4.9.

| Archetype | ATK | DEF | HP | AtkSpd | CP @S1 | CP @S5 | CP @S10 | CP @S20 |
|---|---|---|---|---|---|---|---|---|
| Reaper_Man_01 | 35 | 30 | 120 | 1.00 | 54 | 74 | 106 | 206 |
| Zombie_villager | 32 | 28 | 170 | 0.90 | 59 | 80 | 117 | 229 |
| Orc | 46 | 35 | 160 | 1.00 | 71 | 97 | 140 | 271 |
| Skeleton_Crusader_1 | 44 | 52 | 205 | 0.95 | 76 | 104 | 151 | 295 |
| Golem_01 | 58 | 65 | 275 | 0.85 | 100 | 138 | 200 | 392 |
| Golem_02 | 70 | 78 | 340 | 0.85 | 122 | 168 | 244 | 478 |

## Total CP fielded per stage

Summed across both waves, parsed from the eighteen `Stage_NN.asset` configs plus `Spawner2.asset`
for stages 1–2:

| Stage | Headcount | Total CP | Step | Stage | Headcount | Total CP | Step |
|---|---|---|---|---|---|---|---|
| 1  | 4  | 216  | —      | 11 | 10 | 1500 | +16.7% |
| 2  | 4  | 236  | +9.3%  | 12 | 11 | 1758 | +17.2% |
| 3  | 7  | 453  | **+91.9%** | 13 | 10 | 1876 | +6.7% |
| 4  | 9  | 641  | +41.5% | 14 | 10 | 2055 | +9.5% |
| 5  | 10 | 770  | +20.1% | 15 | 9  | 2133 | **+3.8%** |
| 6  | 9  | 798  | **+3.6%** | 16 | 11 | 2682 | +25.7% |
| 7  | 9  | 879  | +10.2% | 17 | 9  | 2818 | +5.1% |
| 8  | 10 | 1072 | +22.0% | 18 | 10 | 3092 | +9.7% |
| 9  | 9  | 1131 | +5.5%  | 19 | 11 | 4018 | **+29.9%** |
| 10 | 10 | 1285 | +13.6% | 20 | 12 | 4757 | +18.4% |

**Balance observation (data, not a change request):** the ramp is uneven. Stages 6 and 15 add
almost no threat over the stage before them; stages 3 and 19 spike. Separately, the enemy castle's
HP is a flat **350 at every stage from 1 to 20** — the win condition never gets harder even as the
defending army grows 22×.

---

# 7. Total CP at runtime

**Yes for both sides, and most of the work is already done.**

- **Enemies already carry a CP number.** `EnemyManager.RebuildFromBase` assigns `cp` on every
  spawn (`EnemyManager.cs:133`). It is declared `[Header("Debug")] public int cp;` and is read by
  no UI, no balancing code, no wave logic — an Inspector readout only. Summing across live enemies
  is a loop over the already-tracked list.
- **Players compute no CP during a battle at all.** The only CP call sites in the project are six
  calls inside `UnitsPanelController` (menu code). The battle HUD (`HeroStatsPanel`,
  `HeroStatCell`) shows only alive/total and a gem price — no power number, no attack value, no HP
  value is shown during a fight. The inputs exist: `PlayerStatsApplier.CurrentStats` is the live
  `UnitStatsRuntime`, and `HeroRoster` already tracks who is alive.
- **`CPCalculator.SquadCP`** (both overloads, `CPCalculator.cs:32-48`) exists for exactly this
  purpose and has **zero call sites**.

**The "TOTAL CP" already on the menu is not real.** Two labels in `MenuScene.unity` read `3460`
(line 24724) and `32660` (line 36403) under a `TOTAL CP:` caption. Their TextMeshPro components are
referenced by no script — static art, not a computed value.

**Caveat if this is ever wired up:** `EnemyManager.cs:146-161` holds a *second*, divergent CP
formula used as a fallback when `cpWeights` is null. It omits the range term and classifies Mage as
melee, so it disagrees with `CPCalculator` even given identical weights. And `EnemySpawner.cs:410`
overwrites each prefab's `cpWeights` with the spawner's own field — one empty Inspector slot
silently downgrades every enemy in that stage to the fallback.

---

# 8. Defect register

Recorded only. **Nothing here has been changed.**

| # | Severity | Defect | Location |
|---|---|---|---|
| 01 | High | Off-axis growth curves silently inflate defense (+6.25%/lvl) and range (+6.75%/lvl) for all 50 levels. A L20 hero has ×3.17 the intended defense. | `PlayerProgressionConfig.asset:111-158` |
| 02 | High | Menu and battlefield compute different stats. Five UI sites apply only `gA/gH/gMv/gAS`; `PlayerStatsApplier` applies all six. A L10 hero fights with defense 43.1 while every menu — and the CP built on it — says 25. | `UnitsPanelController.cs:517,579,881,962` · `NewCharacterStats.cs:235` vs `PlayerStatsApplier.cs:135-142` |
| 03 | ~~High~~ **FIXED** | ~~`meleeMult` never read from config; the sampling line is missing and `rangedMult` is assigned twice on consecutive lines.~~ Fixed 2026-09-08, commit `80ed65b`. **Changed no CP value** — the clamp floor lifts the off-axis curve back to the same 1.0. Still blocked by defect 04. | `CPWeightMath.cs:41-42` |
| 04 | High | Both flavour curves authored off-axis (t ≈ −389 / −730), evaluating to 0.060 / 0.026. No unit ever receives a type bonus. | `Player CP.asset:159-206` |
| 05 | Medium | A second, divergent CP formula in `EnemyManager` (no range term, Mage treated as melee), reachable by leaving one Inspector slot empty. | `EnemyManager.cs:146-161` · `EnemySpawner.cs:410` |
| 06 | Medium | Enemy defense never grows — four of six multipliers applied. The one axis with real tier spread stays flat across 20 stages. | `EnemyManager.cs:113-118` |
| 07 | Medium | CP prices two stats that do nothing in gameplay. Players move at `PlayerManager.moveSpeed` (0.5), enemies at `EnemyLocoMotion.moveSpeed` (0.2) — separate Inspector fields never fed from the stat block. The SO's 3.5 is inert. | `PlayerManager.cs:60,64` · `EnemyLocoMotion.cs:20` |
| 08 | Low | The menu's "TOTAL CP" figures are hard-coded strings bound to no script. | `MenuScene.unity:24724,36403` |
| 09 | Low | Doc drift. `CPCalculator.txt` names `UnitCardView`/`BucketStatsPanel` as callers — neither calls it. `UnitStatsRuntime.txt` and `ProgressionMath.txt` both state `ApplyLevelGrowth` runs at spawn; it has zero call sites. | `Assets/Documentation for scripts/` |

---

# 9. Formula research — is a weighted sum the right shape?

**Research only. Nothing was implemented and nothing is being recommended for implementation.**

The current design is a **linear weighted sum**: price each stat, add the prices. It is the most
common shape in shipped games and the one with the best-documented failure mode. Two properties
matter here, and this project exhibits both:

1. **One term can swallow the others.** When one weight is ~20× another, the score becomes a proxy
   for that single stat. Yours sits at 79% attack.
2. **A weighted sum has no notion of balance.** A unit with enormous attack and no survivability
   scores the same as a balanced unit with the same total — which is why the number stops
   predicting who wins.

**Shop Titans** is the closest published analogue: same weighted-sum shape, and its own community's
analysis concludes the metric "is a lie" for evaluating actual hero performance, because the
coefficients make ten points of defense worth more than ten points of attack regardless of what the
combat model does with them. Structurally the same failure as the inversion in §5.

## The two standard alternatives

**B — a product with dampened terms.** Pokémon GO uses
`CP = floor(ATK × √DEF × √STA × CPM² / 10)`. Multiplying rather than adding means a unit good at
only one thing cannot score highly; the square roots stop defense and HP from dominating while
still letting them matter.

**C — DPS × Effective HP.** Offense × survivability, where `EffectiveHP = HP × (100 + DEF)/100`.
Not a heuristic — the closed form of "how long it lives × how fast it kills", derived from the
damage equation the game actually runs. **This project already contains it:**
`CPCalculator.EffectiveHP` (`CPCalculator.cs:50-54`) mirrors `CombatMath` exactly and is dead code
with zero call sites.

## How the three score the real roster

> **⚠ CORRECTED 2026-09-09.** The original version of this section tested the formulas against
> `trueDPS = attack × attackSpeed`, which is not how the game works (see the correction in §5).
> That test has been **replaced** with a far stronger one: simulating all 408 real duels and asking
> which metric names the actual winner.

**The real test — which metric predicts who wins?** (408 matchups, `tools/duel2.js`)

| Metric | correct predictions | |
|---|---|---|
| **C — ATK × EffectiveHP** | **408 / 408 = 100.0%** | exact |
| B — product with √ damping | 388 / 408 = 95.1% | |
| A′ — current CP minus the 3 inert stats | 369 / 408 = 90.4% | |
| A — current shipped CP | 368 / 408 = 90.2% | |

**C is 100% because it is the duel condition rearranged, not a heuristic.** Both sides share a fixed
0.6 s cadence, so:

```
player needs   enemyEHP ÷ playerATK   hits,   where EHP = HP × (100 + DEF)/100
enemy  needs   playerEHP ÷ enemyATK   hits
player wins  ⟺  playerATK × playerEHP  >  enemyATK × enemyEHP
```

Whoever has the larger `ATK × EffectiveHP` wins. `CPCalculator.EffectiveHP` already computes exactly
that and has **zero call sites**.

Note that A′ beats A by only 0.2 points: **the three inert stats are not the real problem — the
near-zero defense weight is.**

**Robustness.** A weighted sum *can* be tuned to 100% on this roster (`wA 1.00, wH 0.35, wD 0.90`),
but it is fitted, not structural — 95.4% on a wider sweep within current ranges, 87.9% over a wide
range, 85.6% in a high-DEF regime. The product holds 100% in every regime, because DEF multiplies HP
and a linear DEF term cannot express that.

**Enemy test** — does the formula reflect the real size of the tier gap? Indexed to Reaper = 100:

| Archetype | True DPS | EffectiveHP | A (current) | B | C |
|---|---|---|---|---|---|
| Reaper_Man_01 | 35.0 | 156 | 54 (100) | 210 (100) | 55 (100) |
| Zombie_villager | 28.8 | 218 | 59 (109) | 198 (94) | 63 (115) |
| Orc | 46.0 | 216 | 71 (131) | 344 (164) | 99 (180) |
| Skeleton_Crusader_1 | 41.8 | 312 | 76 (141) | 431 (205) | 130 (236) |
| Golem_01 | 49.3 | 454 | 100 (185) | 659 (314) | 224 (407) |
| Golem_02 | 59.5 | 605 | 122 (**226**) | 968 (461) | 360 (**655**) |

The current formula compresses a **6.5× real power gap into a 2.3× displayed gap**. A Golem_02
looks about twice as dangerous as a Reaper when it is really about six times.

## Where this lands

The weighted sum is not wrong in principle — it is transparent, cheap, and easy to reason about,
which is why it is everywhere. It fails here for two fixable reasons: the coefficients are far from
the combat model's actual sensitivities, and the two naturally *multiplicative* pairings
(attack × attack-speed, HP × defense) are being added instead of multiplied. Shapes B and C both
fix the ordering with no change to the underlying stat data.

### Sources

- Pokémon GO Combat Power mechanics — <https://pokemongohub.net/post/wiki/cp-mechanics/>
- Calculating the Power Rating of a Hero, Shop Titans Central — <https://st-central.net/calculating-the-power-rating-of-a-hero/>
- Epic Seven — Combat Power — <https://epic-seven.fandom.com/wiki/Combat_Power>
- GameDev.net — RPG combat & levelling formulas — <https://gamedev.net/forums/topic/660352-formulas-math-and-theories-for-rpg-combatleveling-systems/>
- How multipliers stack in idle RPGs — <https://missionszanx.com/guides/how-multipliers-stack-in-idle-rpgs>

---
---

# بخش فارسی — قدرت نبرد (CP)، از ابتدا تا انتها

    دامنه : Assets/Scripts/Data/CombatPower/ و Data/Stats/ و Data/Units/ به‌همراه تمام محل‌های فراخوانی
    تاریخ : ۲۰۲۶-۰۹-۰۷
    وضعیت: فقط تحلیل — هیچ فایل .cs یا .asset یا .prefab یا .unity تغییر نکرده است.

**نسخه‌ی تصویری با نمودارها:** <https://claude.ai/code/artifact/3d635747-5eb6-43f8-85cf-e868e41e783d>
(همان محتوا، دوزبانه، با Ctrl+P قابل تبدیل به PDF)

---

## اعداد این سند چطور تولید شده‌اند

همه‌ی مقادیر **بیرون از Unity** دوباره محاسبه شده‌اند: YAML خام فایل‌های `.asset` پارس شد و
`AnimationCurve.Evaluate` (به‌همراه رفتار clamp در PreInfinity/PostInfinity) و
`ProgressionMath.GetGrowthMultipliers` و `CPWeightMath.Evaluate` و `CPCalculator.UnitCP` بازنویسی
شدند. هفت مقدار مرجع هم دستی حساب و با خروجی اسکریپت تطبیق داده شد. هیچ عددی از روی Inspector
چشمی رونویسی نشده است.

---

# ۱. پنج پاسخ

| پرسش | پاسخ |
|---|---|
| CP چطور محاسبه می‌شود؟ | یک **مجموع وزن‌دار ساده از شش استت خام** ضربدر یک ضریب سبک (نزدیک‌زن/دوربرد). وزن‌ها `AnimationCurve` هستند که در لِوِل همان واحد نمونه‌برداری می‌شوند، پس خود دستور محاسبه هم با لِوِل تغییر می‌کند. |
| CP از ۱۰۰ به ۱۲۰ یعنی ۲۰٪+ روی همه‌ی استت‌ها؟ | **نه.** هر استت منحنی *متفاوتی* را مرکب می‌کند، و CP وزن‌ها را چنان نابرابر پخش کرده که چهار استت از شش‌تا نامرئی‌اند. در عمل **CP ≈ ATK + 0.15·HP**. |
| برای قهرمان‌ها/دشمن‌های مختلف فرق دارد؟ | **نرخ رشد فرق نمی‌کند.** هر ۸ قهرمان یک `ProgressionConfigSO` مشترک دارند و هر ۶ کهن‌الگوی دشمن یکی دیگر. فقط برگه‌ی پایه فرق دارد. برای دشمن‌ها *لِوِل* فرق می‌کند — لِوِل هر دشمن **همان شماره‌ی استیج** است. |
| total CP در ران‌تایم؟ | **قابل گرفتن است و کدش از قبل نوشته شده ولی استفاده نمی‌شود.** دشمن‌ها CP را در فیلد دیباگی می‌ریزند که کسی نمی‌خواند؛ بازیکن اصلاً CP محاسبه نمی‌کند؛ `CPCalculator.SquadCP` صفر فراخوانی دارد. |
| آیا فرمول درست است؟ | ⚠ **CORRECTED 2026-09-09** — the earlier "ranks the roster backwards" claim was wrong and is retracted. CP ranks the two hero profiles correctly. The real defect: over 407 simulated duels CP names the wrong winner **9.8%** of the time, and the bias is entirely one-directional (40 cases of "CP said player wins, enemy won"; 0 the other way), caused by defense carrying a near-zero weight. See §5 and §9. |

---

# ۲. فرمول

```
CP = round( ( wA·ATK + wH·HP + wMv·MoveSpeed + wAS·AtkSpeed + wD·DEF + wR·Range ) × typeMult )
```

`Assets/Scripts/Data/CombatPower/CPCalculator.cs:10-30`. هر `w` یک `AnimationCurve` است که در
لِوِل واحد ارزیابی و با `wClamp` (بازه‌ی −۱۰ تا ۱۰) محدود می‌شود، سپس در `globalWeightScaleByLevel`
(محدودشده به ۱ تا ۱۰) ضرب می‌شود. `typeMult` برای Archer و Mage برابر `rangedMult` و برای Warrior
و Horseman برابر `meleeMult` است.

**هیچ تعاملی بین جمله‌ها وجود ندارد.** HP و DEF هیچ‌وقت با هم ترکیب نمی‌شوند تا «بقا» بسازند؛
ATK و AtkSpeed هیچ‌وقت ترکیب نمی‌شوند تا «خروجی آسیب» بسازند. هر استت جدا قیمت می‌خورد و
قیمت‌ها جمع می‌شوند. بخش ۹ درباره‌ی پیامدهای همین است.

## وزن‌های نوشته‌شده — `Assets/Scriptable Objects/CP/Player CP.asset`

| لِوِل | wA حمله | wH جان | wMv حرکت | wAS سرعت حمله | wD دفاع | wR برد | typeMult |
|---|---|---|---|---|---|---|---|
| ۱  | 1.000 | 0.150 | 0.250 | 0.400 | 0.000 | 0.050 | 1.00 |
| ۱۰ | 0.982 | 0.144 | 0.241 | 0.409 | 0.009 | 0.048 | 1.00 |
| ۲۰ | 0.961 | 0.138 | 0.231 | 0.419 | 0.019 | 0.046 | 1.00 |
| ۵۰ | 0.900 | 0.120 | 0.200 | 0.450 | 0.050 | 0.040 | 1.00 |

`EnemyCP.asset` وزن‌های استتی یکسانی دارد، اما **اصلاً منحنی `wRangeByLevel` ندارد** (فایل قدیمی‌تر
از خودِ فیلد است)، پس آن وزن به مقدار اولیه‌ی C# یعنی `AnimationCurve.Linear(1, 0.05f, 50, 0.04f)`
برمی‌گردد.

**هر واحد ATK حدود ۲۰ برابر هر واحد HP می‌ارزد، و DEF در لِوِل ۱ به‌معنای واقعی هیچ ارزشی ندارد.**

## ستون typeMult همه‌جا ۱٫۰۰ است — و این یک اتفاق است

دو ایراد روی هم افتاده‌اند:

۱. `CPWeightMath.cs:41-42` اصلاً `meleeMultByLevel` را نمی‌خواند. آن خط جا افتاده و `rangedMult`
   دو بار پشت سر هم مقداردهی شده، پس `meleeMult` همان مقدار هاردکد `1f` می‌ماند.
۲. هر دو منحنی سبک در `Player CP.asset` روی **زمان منفی** نوشته شده‌اند — `meleeMultByLevel` روی
   t ≈ −۳۸۹ و `rangedMultByLevel` روی t ≈ −۷۳۰ — و آنجا مقدارشان `0.060` و `0.026` است.
   `rangedMult` فقط به‌خاطر کفِ `Mathf.Clamp(…, 1, 5f)` به ۱٫۰ نجات پیدا می‌کند.

هر ۸ قهرمان `FighterType.Warrior` هستند، پس هیچ واحدی در بازی تا امروز بونوس سبک نگرفته است.

---

# ۳. استت‌ها از کجا می‌آیند

```
UnitStatsSO ──FromSO──> UnitStatsRuntime ──رشد لِوِل──> ┬─> منوها: gA gH gMv gAS      ──> CPCalculator
 (داده‌ی طراحی)             (کپی زنده)   ProgressionMath └─> نبرد:  gA gH gMv gAS gD gR
                                                                    ↑ ضرایب روگ‌لایت (خاموش)
```

`ProgressionMath.GetGrowthMultipliers` حاصل‌ضرب `∏(1 + pct(l))` را برای `l = 2..L` حساب می‌کند، با
**یک منحنی جدا برای هر استت**. شش استت، شش منحنی مستقل — این اولین دلیلی است که استت‌ها نمی‌توانند
با هم بالا بروند.

## دوتا از این شش منحنی دقیقاً مثل منحنی‌های سبک خراب‌اند

در `PlayerProgressionConfig.asset` کلیدهای `defPctByLevel` روی t ≈ −۹٫۰ و `rangePctByLevel` روی
t ≈ −۱٫۱ نشسته‌اند. چون منحنی بعد از آخرین کلید clamp می‌شود، `Evaluate(l)` در **هر** لِوِل یک
مقدار ثابت برمی‌گرداند:

| منحنی | L=۱ | L=۱۰ | L=۲۵ | L=۵۰ |
|---|---|---|---|---|
| `defPctByLevel` | 0.062515 | 0.062515 | 0.062515 | 0.062515 |
| `rangePctByLevel` | 0.067460 | 0.067460 | 0.067460 | 0.067460 |

یعنی **۶٫۲۵٪+ دفاع و ۶٫۷۵٪+ برد در هر لِوِل، به‌صورت مرکب، برای هر ۵۰ لِوِل**. در لِوِل ۲۰ این
یعنی ضریب **×۳٫۱۷ روی دفاع** که هیچ‌کس انتخابش نکرده. `pctClamp` (بازه‌ی −۰٫۲۵ تا ۰٫۵۰) جلویش را
نمی‌گیرد — این مقادیر داخل بازه‌ی مجازند، فقط در جای اشتباه اعمال می‌شوند.

`EnemyProgression.asset` هر دو منحنی را اصلاً ندارد، پس سمت دشمن به مقادیر اولیه‌ی سالم C#
(`0.02 → 0.005` و `0.01 → 0.003`) برمی‌گردد — که البته `EnemyManager` هیچ‌وقت اعمالشان نمی‌کند.

---

# ۴. آیا همه‌چیز با یک شیب رشد می‌کند؟ — نه

## دلیل اول: استت‌ها از هم واگرا می‌شوند

ضرایب رشد مرکب از `PlayerProgressionConfig.asset`:

| لِوِل | gA حمله | gH جان | gMv حرکت | gAS سرعت حمله | gD دفاع ⚠ | gR برد ⚠ |
|---|---|---|---|---|---|---|
| ۱  | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 | 1.000 |
| ۵  | 1.348 | 1.448 | 1.080 | 1.080 | 1.274 | 1.298 |
| ۱۰ | 1.916 | 2.242 | 1.184 | 1.184 | 1.726 | 1.800 |
| ۲۰ | 3.603 | 4.942 | 1.402 | 1.402 | 3.165 | 3.457 |
| ۳۰ | 6.153 | 9.722 | 1.628 | 1.628 | 5.804 | 6.641 |
| ۵۰ | 13.399 | 26.597 | 2.064 | 2.064 | 19.518 | 24.504 |

⚠ = خروجی منحنی‌های بیرون‌محورِ بخش ۳، نه نتیجه‌ی تصمیم طراحی.

توجه: `gMv` و `gAS` از منحنی‌هایی با کلیدهای یکسان می‌آیند و بنابراین **همیشه دقیقاً برابرند**.

## دلیل دوم: CP استت‌ها را نابرابر قیمت می‌گذارد

سهم هر جمله از مجموع وزن‌دار، برای پروفایل ATK 64 / AtkSpd 2.0:

| لِوِل | CP | حمله | جان | سرعت حرکت | سرعت حمله | دفاع | برد |
|---|---|---|---|---|---|---|---|
| ۱  | 81   | 79.29% | 18.58% | 1.08% | 0.99% | 0.00% | 0.053% |
| ۵  | 109  | 78.59% | 19.63% | 0.85% | 0.80% | 0.09% | 0.038% |
| ۱۰ | 155  | 77.65% | 20.91% | 0.64% | 0.63% | 0.15% | 0.026% |
| ۲۰ | 293  | 75.68% | 23.35% | 0.39% | 0.40% | 0.17% | 0.013% |
| ۵۰ | 1096 | 70.45% | 29.13% | 0.13% | 0.17% | 0.11% | 0.003% |

**سهم دفاع در هیچ لِوِلی از ۰٫۱۷٪ فراتر نمی‌رود. سهم برد از ۰٫۰۶٪.**

## پاسخ مستقیم

جابه‌جایی CP از ۱۰۰ به ۱۲۰ تقریباً دقیقاً یعنی **۲۰٪ افزایش حمله و تقریباً هیچ چیز دیگر**. مشخص‌تر:
بردن یک قهرمان از لِوِل ۱ به ۱۰، CP را ×۱٫۹۱ می‌کند، در حالی که حمله ×۱٫۹۲، جان ×۲٫۲۴، سرعت حمله فقط
×۱٫۱۸، و دفاع یا ×۱٫۰۰ (آنچه منو نشان می‌دهد) یا ×۱٫۷۳ (آنچه واقعاً می‌جنگد) — ایراد ۰۲ در بخش ۸.

---

# ۵. CP بازیکن، قهرمان به قهرمان

هشت قهرمان ثبت‌شده، اما **فقط دو پروفایل استتی متمایز**. جان (۱۰۰)، دفاع (۲۵)، سرعت حرکت (۳٫۵) و
برد (۰٫۸۵) روی هر هشت‌تا یکسان است؛ فقط حمله و سرعت حمله فرق دارند، آن هم معکوس هم. هر هشت‌تا
`FighterType.Warrior` هستند.

| id | قهرمان | فایل baseStats | ATK | سرعت حمله | DPS واقعی | CP L1 | CP L10 | CP L20 |
|---|---|---|---|---|---|---|---|---|
| ۱ | Valkir3 | `Player_Minotaur_01` | 64 | 2.0 | **128** | **81** | 155 | 293 |
| ۲ | Dark_Oracle_1 | `Dark_Oracle_01` | 72 | 1.5 | 108 | **89** | 170 | 320 |
| ۳ | Minotaur_02 | `Minotaur_02` | 72 | 1.5 | 108 | 89 | 170 | 320 |
| ۴ | Minotaur_2 | `CowMinotaur_2` | 72 | 1.5 | 108 | 89 | 170 | 320 |
| ۵ | Golem_3 | `Golem_3` | 72 | 1.5 | 108 | 89 | 170 | 320 |
| ۶ | Fallen_Angels_02 | `PlayerValkir3` | 64 | 2.0 | **128** | **81** | 155 | 293 |
| ۷ | Fallen_Minotaur_01 | `Player_Minotaur_01` | 64 | 2.0 | **128** | **81** | 155 | 293 |
| ۸ | Dark_Oracle_3 | `Player_Dark_Oracle_3` | 72 | 1.5 | 108 | 89 | 170 | 320 |

## ⚠ اصلاح‌شده در ۲۰۲۶-۰۹-۰۹ — ادعای «رتبه‌بندی وارونه» غلط بود

> **این بخش قبلاً ادعا می‌کرد CP روستر را وارونه رتبه‌بندی می‌کند. آن یک خطا بود و پس گرفته شد.**
> فرض شده بود `trueDPS = attack × attackSpeed`، برداشته از کامنت فیلد در `UnitStatsSO`
> («hits/sec»)، بدون اینکه تأیید شود بازی واقعاً آن را پیاده کرده. **پیاده نکرده است.**
>
> `attackSpeed` اصلاً روی نرخ حمله اثر ندارد. کِیدنس با `PlayerAttackAction.recoveryTime` تعیین
> می‌شود (`PlayerAttackState.cs:99-100`) و فقط با یک تایمر آزاد می‌شود
> (`PlayerManager.cs:798-812`)؛ `attackSpeed` در آن مسیر نیست و تنها سرعت پخش انیمیشن را تعیین
> می‌کند. **هر ۱۰ فایل `PlayerAttackAction` مقدار `recoveryTime = 0.6` دارند**، پس هر قهرمان یک بار
> در هر ۰٫۶ ثانیه حمله می‌کند و خروجی واقعی `attack ÷ 0.6` است:
>
> | پروفایل | ATK | AtkSpd | ادعای اولیه | **واقعی** |
> |---|---|---|---|---|
> | «سریع» (ids 1, 6, 7) | 64 | 2.0 | ۱۲۸ DPS | **۱۰۶٫۷ DPS** |
> | «کند» (ids 2, 3, 4, 5, 8) | 72 | 1.5 | ۱۰۸ DPS | **۱۲۰٫۰ DPS** |
>
> پروفایل «کند» واقعاً **۱۲٫۵٪ قوی‌تر** است و CP آن را ۹٫۹٪ بالاتر می‌سنجد (۸۹ در برابر ۸۱).
> **CP این دو پروفایل را درست رتبه‌بندی می‌کند.**

## مشکل واقعی CP — اثبات‌شده روی ۴۰۷ دوئل شبیه‌سازی‌شده

CP وارونه نیست، اما پیش‌بینی‌کننده‌ی قابل‌اعتمادی هم نیست، و خطاهایش سیستماتیک‌اند.

**CP در ۴۰ مورد از ۴۰۷ (۹٫۸٪) برنده را اشتباه اعلام کرد** — با سوگیری کاملاً یک‌طرفه:

| | تعداد |
|---|---|
| CP گفت **بازیکن** می‌برد، دشمن برد | **۴۰** |
| CP گفت **دشمن** می‌برد، بازیکن برد | **۰** |

**CP به‌طور سیستماتیک بازیکن را بیش‌ارزش‌گذاری می‌کند.** مثال — قهرمان «سریع» در L3 برابر
Skeleton_Crusader_1 در S1:

| | CP | ATK | DEF | HP | کشتن در |
|---|---|---|---|---|---|
| بازیکن | **۹۴** | 74 | 28 | 121 | ۵ ضربه (۳٫۰ ثانیه) |
| اسکلت | **۷۶** | 44 | 52 | 205 | **۴ ضربه (۲٫۴ ثانیه)** |

علت در وزن‌هاست: در لِوِل ۱۰، CP حمله را **۰٫۹۸۲**، جان را **۰٫۱۴۴** و دفاع را **۰٫۰۰۹** قیمت
می‌گذارد، در حالی که دشمن‌ها تانک‌اند (DEF ۵۲ تا ۷۸، HP ۲۰۵ تا ۳۴۰) و قهرمان‌ها دمیج‌دیلر.
بازتولید با `tools/duel.js` و `tools/duel2.js`.

---

# ۶. CP دشمن، استیج به استیج

**هیچ سیستم سختی جداگانه‌ای وجود ندارد.** نه `stageMultiplier`، نه `hpMult`، نه فایل مقیاس‌دهی
به‌ازای استیج. اسپاونر شماره‌ی استیج را به دشمن می‌دهد و دشمن آن را به‌عنوان لِوِل پیشرفتش به کار
می‌برد:

```csharp
// EnemyManager.cs:90-99
public void Initialize(int stageLevelFromSpawner)
{
    stageLevel = stageLevelFromSpawner;
    // IMPORTANT: use the stage as this enemy's "unit level" for progression.
    unitLevel = stageLevelFromSpawner;
    RebuildFromBase();
}
```

`RebuildFromBase` فقط **چهار ضریب از شش‌تا** را اعمال می‌کند — دفاع و برد هیچ‌وقت رشد نمی‌کنند
(`EnemyManager.cs:113-118`). دفاع تنها محوری است که کهن‌الگوها در آن تفاوت واقعی دارند (۲۸ تا ۷۸)،
و در تمام بیست استیج صاف می‌ماند در حالی که حمله ×۳٫۶ و جان ×۴٫۹ می‌شود.

| کهن‌الگو | ATK | DEF | HP | سرعت حمله | CP@S1 | CP@S5 | CP@S10 | CP@S20 |
|---|---|---|---|---|---|---|---|---|
| Reaper_Man_01 | 35 | 30 | 120 | 1.00 | 54 | 74 | 106 | 206 |
| Zombie_villager | 32 | 28 | 170 | 0.90 | 59 | 80 | 117 | 229 |
| Orc | 46 | 35 | 160 | 1.00 | 71 | 97 | 140 | 271 |
| Skeleton_Crusader_1 | 44 | 52 | 205 | 0.95 | 76 | 104 | 151 | 295 |
| Golem_01 | 58 | 65 | 275 | 0.85 | 100 | 138 | 200 | 392 |
| Golem_02 | 70 | 78 | 340 | 0.85 | 122 | 168 | 244 | 478 |

## مجموع CP میدان‌شده در هر استیج

جمع هر دو ویو، پارس‌شده از هجده فایل `Stage_NN.asset` به‌علاوه‌ی `Spawner2.asset` برای استیج ۱ و ۲:

| استیج | تعداد | مجموع CP | تغییر | استیج | تعداد | مجموع CP | تغییر |
|---|---|---|---|---|---|---|---|
| ۱  | ۴  | 216  | —      | ۱۱ | ۱۰ | 1500 | +16.7% |
| ۲  | ۴  | 236  | +9.3%  | ۱۲ | ۱۱ | 1758 | +17.2% |
| ۳  | ۷  | 453  | **+91.9%** | ۱۳ | ۱۰ | 1876 | +6.7% |
| ۴  | ۹  | 641  | +41.5% | ۱۴ | ۱۰ | 2055 | +9.5% |
| ۵  | ۱۰ | 770  | +20.1% | ۱۵ | ۹  | 2133 | **+3.8%** |
| ۶  | ۹  | 798  | **+3.6%** | ۱۶ | ۱۱ | 2682 | +25.7% |
| ۷  | ۹  | 879  | +10.2% | ۱۷ | ۹  | 2818 | +5.1% |
| ۸  | ۱۰ | 1072 | +22.0% | ۱۸ | ۱۰ | 3092 | +9.7% |
| ۹  | ۹  | 1131 | +5.5%  | ۱۹ | ۱۱ | 4018 | **+29.9%** |
| ۱۰ | ۱۰ | 1285 | +13.6% | ۲۰ | ۱۲ | 4757 | +18.4% |

**مشاهده‌ی بالانسی (داده، نه درخواست تغییر):** شیب ناهموار است. استیج‌های ۶ و ۱۵ تقریباً هیچ تهدید
تازه‌ای نسبت به استیج قبلشان اضافه نمی‌کنند؛ استیج‌های ۳ و ۱۹ جهش دارند. جدا از این، جان قلعه‌ی
دشمن در تمام استیج‌های ۱ تا ۲۰ صافِ **۳۵۰** است — شرط برد هیچ‌وقت سخت‌تر نمی‌شود، حتی وقتی ارتش
مدافع ۲۲ برابر می‌شود.

---

# ۷. total CP در ران‌تایم

**برای هر دو طرف بله، و بیشتر کارش انجام شده.**

- **دشمن‌ها از قبل یک عدد CP دارند.** `EnemyManager.RebuildFromBase` در هر اسپاون به `cp` مقدار
  می‌دهد (`EnemyManager.cs:133`). این فیلد `[Header("Debug")] public int cp;` است و هیچ UI، هیچ کد
  بالانسی و هیچ منطق ویوی آن را نمی‌خواند — فقط نمایش Inspector. جمع‌زدنش روی دشمن‌های زنده صرفاً
  یک حلقه روی لیستی است که از قبل نگه‌داشته می‌شود.
- **برای بازیکن در طول نبرد اصلاً CP محاسبه نمی‌شود.** تنها محل‌های فراخوانی CP در کل پروژه، شش
  فراخوانی داخل `UnitsPanelController` (کد منو) است. HUD نبرد (`HeroStatsPanel` و `HeroStatCell`)
  فقط زنده/کل و قیمت جم را نشان می‌دهد — هیچ عدد قدرتی، هیچ حمله‌ای و هیچ جانی در طول نبرد نمایش
  داده نمی‌شود. اما ورودی‌ها حاضرند: `PlayerStatsApplier.CurrentStats` همان `UnitStatsRuntime`
  زنده است و `HeroRoster` از قبل زنده‌ها را رد می‌گیرد.
- **`CPCalculator.SquadCP`** (هر دو overload، `CPCalculator.cs:32-48`) دقیقاً برای همین نوشته شده و
  **صفر جای فراخوانی** دارد.

**همان «TOTAL CP» که روی منو هست واقعی نیست.** دو لیبل در `MenuScene.unity` اعداد `3460` (خط ۲۴۷۲۴)
و `32660` (خط ۳۶۴۰۳) را زیر عنوان `TOTAL CP:` نشان می‌دهند. کامپوننت‌های TextMeshPro آن‌ها به هیچ
اسکریپتی ارجاع ندارند — آرت ثابت، نه مقدار محاسبه‌شده.

**یک هشدار اگر روزی این را سیم‌کشی کردید:** `EnemyManager.cs:146-161` یک فرمول CP *دوم* و واگرا
دارد که وقتی `cpWeights` خالی باشد به‌عنوان fallback استفاده می‌شود. جمله‌ی برد ندارد و Mage را
نزدیک‌زن حساب می‌کند، پس حتی با وزن‌های یکسان هم با `CPCalculator` هم‌خوانی ندارد. ضمناً
`EnemySpawner.cs:410` مقدار `cpWeights` هر پریفب را با فیلد خود اسپاونر بازنویسی می‌کند — یک اسلات
خالی در Inspector بی‌سروصدا تمام دشمن‌های آن استیج را به همان fallback تنزل می‌دهد.

---

# ۸. فهرست ایرادها

فقط ثبت شده. **هیچ‌کدام تغییر داده نشده است.**

| # | شدت | ایراد | محل |
|---|---|---|---|
| ۰۱ | بالا | منحنی‌های رشدِ بیرون‌محور، بی‌سروصدا دفاع (۶٫۲۵٪+ در هر لِوِل) و برد (۶٫۷۵٪+) را برای هر ۵۰ لِوِل باد می‌کنند. قهرمان L20 ×۳٫۱۷ دفاعِ موردنظر را دارد. | `PlayerProgressionConfig.asset:111-158` |
| ۰۲ | بالا | منو و میدان نبرد استت‌های متفاوتی حساب می‌کنند. پنج محل UI فقط `gA/gH/gMv/gAS` را اعمال می‌کنند؛ `PlayerStatsApplier` هر شش‌تا را. قهرمان L10 با دفاع ۴۳٫۱ می‌جنگد ولی همه‌ی منوها — و CP ساخته‌شده روی آن — عدد ۲۵ را می‌گویند. | `UnitsPanelController.cs:517,579,881,962` · `NewCharacterStats.cs:235` در برابر `PlayerStatsApplier.cs:135-142` |
| ۰۳ | ~~بالا~~ **رفع شد** | ~~`meleeMult` هیچ‌وقت از کانفیگ خوانده نمی‌شود؛ خط نمونه‌برداری جا افتاده و `rangedMult` دو بار پشت سر هم مقداردهی شده.~~ در ۲۰۲۶-۰۹-۰۸ با کامیت `80ed65b` رفع شد. **هیچ مقدار CP را تغییر نداد** — کفِ clamp مقدار منحنی بیرون‌محور را به همان ۱٫۰ قبلی برمی‌گرداند. هنوز به ایراد ۰۴ گره خورده است. | `CPWeightMath.cs:41-42` |
| ۰۴ | بالا | هر دو منحنی سبک بیرون‌محور نوشته شده‌اند (t ≈ −۳۸۹ / −۷۳۰) و مقدارشان ۰٫۰۶۰ / ۰٫۰۲۶ است. هیچ واحدی هیچ‌وقت بونوس سبک نمی‌گیرد. | `Player CP.asset:159-206` |
| ۰۵ | متوسط | یک فرمول CP دوم و واگرا در `EnemyManager` (بدون جمله‌ی برد، Mage نزدیک‌زن)، که با خالی‌گذاشتن یک اسلات Inspector قابل رسیدن است. | `EnemyManager.cs:146-161` · `EnemySpawner.cs:410` |
| ۰۶ | متوسط | دفاع دشمن هیچ‌وقت رشد نمی‌کند — چهار ضریب از شش‌تا اعمال می‌شود. تنها محوری که تفاوت واقعی تیر دارد در ۲۰ استیج صاف می‌ماند. | `EnemyManager.cs:113-118` |
| ۰۷ | متوسط | CP برای دو استتی قیمت می‌گذارد که در گیم‌پلی کاری نمی‌کنند. بازیکن‌ها با `PlayerManager.moveSpeed` (۰٫۵) و دشمن‌ها با `EnemyLocoMotion.moveSpeed` (۰٫۲) حرکت می‌کنند — فیلدهای جدای Inspector که از بلوک استت تغذیه نمی‌شوند. مقدار ۳٫۵ در SO بی‌اثر است. | `PlayerManager.cs:60,64` · `EnemyLocoMotion.cs:20` |
| ۰۸ | پایین | اعداد «TOTAL CP» در منو رشته‌های هاردکد شده‌اند که به هیچ اسکریپتی وصل نیستند. | `MenuScene.unity:24724,36403` |
| ۰۹ | پایین | ناهماهنگی مستندات. `CPCalculator.txt` نام `UnitCardView`/`BucketStatsPanel` را به‌عنوان فراخوان می‌آورد — هیچ‌کدام آن را صدا نمی‌زنند. `UnitStatsRuntime.txt` و `ProgressionMath.txt` می‌گویند `ApplyLevelGrowth` موقع اسپاون اجرا می‌شود؛ صفر جای فراخوانی دارد. | `Assets/Documentation for scripts/` |

---

# ۹. تحقیق درباره‌ی فرمول — آیا مجموع وزن‌دار شکل درستی است؟

**فقط تحقیق. هیچ‌چیز پیاده‌سازی نشده و هیچ تغییری پیشنهاد نمی‌شود.**

طراحی فعلی یک **مجموع وزن‌دار خطی** است: به هر استت قیمت بده، قیمت‌ها را جمع کن. رایج‌ترین شکل در
بازی‌های منتشرشده است و در عین حال شکلی که حالت شکستش بهتر از همه مستند شده. دو ویژگی اینجا مهم‌اند
و پروژه‌ی شما هر دو را نشان می‌دهد:

۱. **یک جمله می‌تواند بقیه را ببلعد.** وقتی یک وزن حدود ۲۰ برابر دیگری باشد، امتیاز عملاً نماینده‌ی
   همان یک استت می‌شود. مالِ شما روی ۷۹٪ حمله ایستاده.
۲. **مجموع وزن‌دار هیچ درکی از تعادل ندارد.** واحدی با حمله‌ی نجومی و بقای صفر همان امتیازی را
   می‌گیرد که یک واحد متعادل با همان مجموع می‌گیرد — و دقیقاً برای همین این عدد دیگر پیش‌بینی
   نمی‌کند چه کسی می‌برد.

**Shop Titans** نزدیک‌ترین نمونه‌ی منتشرشده است: همان شکل مجموع وزن‌دار، و تحلیل خودِ جامعه‌اش
نتیجه می‌گیرد که این معیار برای سنجش عملکرد واقعی قهرمان «یک دروغ» است، چون ضرایب باعث می‌شوند ده
واحد دفاع بیشتر از ده واحد حمله بیارزد، فارغ از اینکه مدل نبرد با آن‌ها چه می‌کند. ساختاراً همان
شکستی که در بخش ۵ دیدیم.

## دو جایگزین استاندارد

**B — ضرب با جمله‌های میراشده.** Pokémon GO از `CP = floor(ATK × √DEF × √STA × CPM² / 10)` استفاده
می‌کند. ضرب‌کردن به‌جای جمع‌کردن یعنی واحدی که فقط در یک چیز خوب است نمی‌تواند امتیاز بالا بگیرد؛
جذرها جلوی مسلط‌شدن دفاع و جان را می‌گیرند بدون اینکه بی‌اثرشان کنند.

**C — DPS × جانِ مؤثر.** حمله × بقا، که در آن `EffectiveHP = HP × (100 + DEF)/100`. این یک حدس
مهندسی نیست — فرم بسته‌ی «چقدر زنده می‌ماند × چقدر سریع می‌کشد» است که مستقیماً از همان معادله‌ی
آسیبی که بازی اجرا می‌کند مشتق شده. **پروژه‌ی شما همین حالا آن را دارد:**
`CPCalculator.EffectiveHP` (`CPCalculator.cs:50-54`) دقیقاً `CombatMath` را بازتاب می‌دهد و کد
مرده است با صفر فراخوانی.

## این سه فرمول روی روستر واقعی چه امتیازی می‌دهند

> **⚠ اصلاح‌شده در ۲۰۲۶-۰۹-۰۹.** نسخه‌ی اولیه‌ی این بخش فرمول‌ها را با
> `trueDPS = attack × attackSpeed` می‌سنجید، که روش کار بازی نیست (اصلاح در بخش ۵ را ببینید).
> آن آزمون با آزمونی به‌مراتب قوی‌تر **جایگزین شد**: شبیه‌سازی هر ۴۰۸ دوئل واقعی و پرسیدن اینکه
> کدام معیار برنده‌ی واقعی را درست می‌گوید.

**آزمون واقعی — کدام معیار برنده را پیش‌بینی می‌کند؟** (۴۰۸ رویارویی، `tools/duel2.js`)

| معیار | پیش‌بینی درست | |
|---|---|---|
| **C — ATK × جان مؤثر** | **۴۰۸ / ۴۰۸ = ۱۰۰٫۰٪** | دقیق |
| B — ضرب با میرایی جذری | ۳۸۸ / ۴۰۸ = ۹۵٫۱٪ | |
| ′A — CP فعلی منهای سه استت بی‌اثر | ۳۶۹ / ۴۰۸ = ۹۰٫۴٪ | |
| A — CP فعلی | ۳۶۸ / ۴۰۸ = ۹۰٫۲٪ | |

**C دقیقاً ۱۰۰٪ است چون خودِ شرط دوئل است، نه یک حدس مهندسی.** چون هر دو طرف کِیدنس ثابت ۰٫۶
ثانیه دارند:

```
بازیکن نیاز دارد به   enemyEHP ÷ playerATK   ضربه،   که EHP = HP × (100 + DEF)/100
دشمن  نیاز دارد به   playerEHP ÷ enemyATK   ضربه
بازیکن می‌برد  ⟺  playerATK × playerEHP  >  enemyATK × enemyEHP
```

هرکس `ATK × EffectiveHP` بزرگ‌تری داشته باشد می‌برد. `CPCalculator.EffectiveHP` همین را حساب
می‌کند و **صفر فراخوانی** دارد.

توجه: ′A فقط ۰٫۲ واحد از A بهتر است — **سه استت بی‌اثر مشکل اصلی نیستند؛ وزن نزدیک‌به‌صفرِ دفاع
مشکل اصلی است.**

**پایداری.** یک مجموع وزن‌دار *می‌تواند* روی همین روستر به ۱۰۰٪ تنظیم شود
(`wA 1.00, wH 0.35, wD 0.90`)، اما این برازش است نه ساختار — ۹۵٫۴٪ روی بازه‌ی گسترده‌تر در محدوده‌ی
فعلی، ۸۷٫۹٪ روی بازه‌ی وسیع، و ۸۵٫۶٪ در رژیم دفاع بالا. فرم ضربی در همه‌ی رژیم‌ها ۱۰۰٪ می‌ماند،
چون دفاع در جان **ضرب** می‌شود و یک جمله‌ی خطی برای دفاع نمی‌تواند این را بیان کند.

**آزمون دشمن‌ها** — آیا فرمول اندازه‌ی واقعی فاصله‌ی تیرها را بازتاب می‌دهد؟ شاخص‌سازی نسبت به
Reaper = ۱۰۰:

| کهن‌الگو | DPS واقعی | جان مؤثر | A (فعلی) | B | C |
|---|---|---|---|---|---|
| Reaper_Man_01 | 35.0 | 156 | 54 (100) | 210 (100) | 55 (100) |
| Zombie_villager | 28.8 | 218 | 59 (109) | 198 (94) | 63 (115) |
| Orc | 46.0 | 216 | 71 (131) | 344 (164) | 99 (180) |
| Skeleton_Crusader_1 | 41.8 | 312 | 76 (141) | 431 (205) | 130 (236) |
| Golem_01 | 49.3 | 454 | 100 (185) | 659 (314) | 224 (407) |
| Golem_02 | 59.5 | 605 | 122 (**226**) | 968 (461) | 360 (**655**) |

فرمول فعلی یک **فاصله‌ی واقعی ۶٫۵ برابری را به فاصله‌ی نمایشی ۲٫۳ برابری فشرده می‌کند**. یک
Golem_02 حدوداً دو برابر خطرناک‌تر از Reaper به نظر می‌رسد در حالی که واقعاً حدود شش برابر است.

## جمع‌بندی

مجموع وزن‌دار در اصل غلط نیست — شفاف است، ارزان است و راحت می‌شود درباره‌اش فکر کرد، و به همین
دلیل همه‌جا هست. اینجا به دو دلیل قابل‌رفع شکست می‌خورد: ضرایب از حساسیت‌های واقعی مدل نبرد خیلی
دورند، و دو جفت استت که ذاتاً ضربی‌اند (حمله × سرعت حمله، جان × دفاع) دارند جمع می‌شوند به‌جای
ضرب. هر دو شکل B و C ترتیب را بدون هیچ تغییری در داده‌ی استت‌ها درست می‌کنند.

### منابع

- مکانیک Combat Power در Pokémon GO — <https://pokemongohub.net/post/wiki/cp-mechanics/>
- محاسبه‌ی Power Rating یک قهرمان، Shop Titans Central — <https://st-central.net/calculating-the-power-rating-of-a-hero/>
- Epic Seven — Combat Power — <https://epic-seven.fandom.com/wiki/Combat_Power>
- GameDev.net — فرمول‌های نبرد و لِوِلینگ در RPG — <https://gamedev.net/forums/topic/660352-formulas-math-and-theories-for-rpg-combatleveling-systems/>
- نحوه‌ی روی‌هم‌رفتن ضرایب در Idle RPGها — <https://missionszanx.com/guides/how-multipliers-stack-in-idle-rpgs>
