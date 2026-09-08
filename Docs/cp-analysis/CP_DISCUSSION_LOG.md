# CP_DISCUSSION_LOG — open questions on the Combat Power system

    Purpose: the living record of the CP discussion between Arash and Claude.
             CP_SYSTEM_ANALYSIS.md is the FINDINGS (what is true).
             This file is the DECISIONS (what we choose to do about it).
    Opened : 2026-09-07
    Status : discussion in progress — NOTHING has been decided, NOTHING has been changed.

> **فارسی:** این فایل دفترچه‌ی گفتگوی ماست. یافته‌ها در `CP_SYSTEM_ANALYSIS.md` هستند؛
> اینجا فقط تصمیم‌ها و سؤال‌های باز ثبت می‌شوند. بخش فارسی در انتهای همین فایل است.

---

## Read these first

| What | Where |
|---|---|
| The findings — formula, data, charts, 9 defects, formula research | `Docs/cp-analysis/CP_SYSTEM_ANALYSIS.md` |
| Same report with interactive charts, EN + FA, printable to PDF | <https://claude.ai/code/artifact/3d635747-5eb6-43f8-85cf-e868e41e783d> |
| The script that produced every number (re-runnable) | `Docs/cp-analysis/tools/` — see `Docs/cp-analysis/README.md` |
| Offline copy of the charted report (open in any browser) | `Docs/cp-analysis/report/CP_System_Report.html` |
| Cross-session project log (auto-injected at session start) | `SESSIONS.md` |

---

## Ground rules for this thread

These were set by Arash and hold until he says otherwise:

1. **Reply in English**, even though he writes in Persian.
2. **Research and analysis only — do not change code, assets, prefabs or scenes** unless he
   explicitly asks for a change. The CP investigation was done under this rule and honoured it:
   as of 2026-09-07 not one `.cs` / `.asset` / `.prefab` / `.unity` file was modified.
3. **Be precise over fast.** He said time does not matter; accuracy does. Every claim should carry
   a `file.cs:line` or an `Asset.asset:line`.
4. Points are raised **one at a time**. Record each one below as it is resolved.
5. **Saving is manual and scoped.** Agreed 2026-09-07: after each point is settled, Claude writes it
   into the Discussion log below and makes **one commit** for it. No auto-commit hook was installed,
   and the raw chat transcript is deliberately **not** stored in the repo. Nothing is pushed by
   Claude — `git push` needs Arash's GitHub credentials, which the Claude Code shell cannot prompt
   for, so **pushing is always his step.** If a session ends with unpushed commits, they are still
   safe locally; they just are not on GitHub yet.

---

## State of play as of 2026-09-07

Arash has read the full report and is now raising specific points for discussion, one by one.
**No decision has been made on any of the 9 defects or on the formula shape.** Nothing below the
"Decisions taken" heading means anything until it has a date and an outcome.

### The three findings the discussion is likely to circle around

1. **`CP ≈ ATK + 0.15·HP`.** At level 1: attack 79.3% of the score, HP 18.6%, the other four stats
   2.1% combined, defense exactly 0.00%. Defense never exceeds 0.17% of CP at any level.
2. **CP ranks the roster backwards.** The 128-DPS hero profile (ATK 64 / AtkSpd 2.0 — ids 1, 6, 7)
   displays **CP 81**; the 108-DPS profile (ATK 72 / AtkSpd 1.5 — ids 2, 3, 4, 5, 8) displays
   **CP 89**. True at every level from 1 to 50.
3. **Four `AnimationCurve`s are authored at negative time** and silently clamp to values nobody
   chose — `defPctByLevel` (flat +6.25%/level → ×3.17 defense at L20), `rangePctByLevel`
   (+6.75%/level), `meleeMultByLevel`, `rangedMultByLevel`. `pctClamp` does not catch them because
   the values sit inside the legal window; they are simply applied at the wrong place.

---

## Open decisions

Nothing here is decided. Each row gets an outcome and a date when it is settled.

### A. The 9 defects — fix, defer, or accept?

Full detail for each is in `CP_SYSTEM_ANALYSIS.md` §8.

| # | Defect (short) | Severity | Decision | Date |
|---|---|---|---|---|
| 01 | Off-axis growth curves inflate defense +6.25%/lvl and range +6.75%/lvl | High | — | — |
| 02 | Menus apply 4 of 6 growth multipliers, combat applies 6 (L10 hero: menu says DEF 25, fights with 43.1) | High | — | — |
| 03 | `CPWeightMath.cs:41-42` never samples `meleeMultByLevel`; `rangedMult` assigned twice | High | — | — |
| 04 | Both flavour curves in `Player CP.asset` authored off-axis → every `typeMult` is 1.0 | High | — | — |
| 05 | Second, divergent CP formula inside `EnemyManager` reachable via an empty Inspector slot | Medium | — | — |
| 06 | Enemy defense never grows — flat across all 20 stages while ATK ×3.6 and HP ×4.9 | Medium | — | — |
| 07 | CP prices `moveSpeed` and `attackRange`, both inert in gameplay | Medium | — | — |
| 08 | Menu `TOTAL CP: 3460 / 32660` are hard-coded strings bound to no script | Low | — | — |
| 09 | Three per-script `.txt` docs describe code that no longer exists | Low | — | — |

### B. The formula shape

| Option | Ranks the roster correctly? | Decision | Date |
|---|---|---|---|
| A — keep the current weighted sum, retune the weights | Only if weights are rebuilt around real combat sensitivities | — | — |
| B — product with √ damping (Pokémon GO shape) | Yes (ratio 1.19, matches true DPS) | — | — |
| C — DPS × EffectiveHP (`CPCalculator.EffectiveHP` already exists, unused) | Yes (ratio 1.19) | — | — |
| Do nothing — CP is cosmetic, accept it | n/a | — | — |

### C. Runtime total CP

| Question | Decision | Date |
|---|---|---|
| Wire up a real squad/wave total CP readout? (`CPCalculator.SquadCP` exists, zero call sites) | — | — |
| Replace the hard-coded menu `TOTAL CP` labels with a computed value? | — | — |

### D. Balance observations surfaced by the CP data (not CP bugs)

| Observation | Decision | Date |
|---|---|---|
| Stage ramp is uneven: +91.9% into stage 3 and +29.9% into 19, vs +3.6% into 6 and +3.8% into 15 | — | — |
| Enemy castle HP is a flat 350 at every stage 1→20 — the win condition never gets harder | — | — |
| The roster is two stat profiles wearing eight costumes; the "fast" three are strictly better | — | — |

### E. Pending directives from Arash — stated, but NOT yet applied

He has said what he wants but explicitly withheld authorisation to change the game. Do not act on
these until he says so.

| Directive | Stated | Blocking question | Applied? |
|---|---|---|---|
| **Do not count `attackRange` in CP for Warrior / melee characters.** Measured cost: the term is 0.052% of CP at L1 and 0.003% at L50; dropping it moves the displayed integer in 3 of 20 hero rows and 5 of 120 enemy rows, always by exactly 1. | 2026-09-08 | All 15 characters are Warrior, so *"exclude for melee"* and *"exclude entirely"* are currently identical. Which does he mean — an `if (!isRanged)` guard that preserves range for future Archer/Mage units, or deleting the term outright? | **NO** |
| ~~**Fix the duplicated `rangedMult` assignment** at `CPWeightMath.cs:41-42`.~~ | 2026-09-08 | — | **✅ DONE 2026-09-08, commit `80ed65b`** — code fix only. See discussion log entry. |
| **Re-author the two flavour curves onto the level axis.** `meleeMultByLevel` (keys at t ≈ −389) and `rangedMultByLevel` (t ≈ −730) in `Player CP.asset` are both off-axis. Until they are redrawn over x = 1…50, `typeMult` stays 1.0 no matter what the code does — editing them in the Inspector still appears to do nothing. | 2026-09-08 (implied by the code fix) | Not authorised. This is a **data** change in the Unity Inspector, not a code change. Also: is it even wanted? While all 15 units are Warrior, `typeMult` is a no-op regardless — see Open decisions B. | **NO** |

### F. Scope

The roster that matters is `Assets/PREFABS/Characters/New Characters/` — **15 prefabs, verified**:
5 Deployed Players, 6 Enemies, 4 Undeployed Players. All are `FighterType.Warrior`, therefore all
melee. Anything outside that folder (the legacy Archer / Horseman / Mage assets, the Old Characters
prefabs) is out of scope unless he says otherwise.

---

## Discussion log

_Newest last. One entry per point Arash raises. Record the question, the answer, and the outcome._

### 2026-09-08 — "Where is the `melee` type? All our units are Warrior."

- **Arash asked:** every `UnitStatsSO` has one of only four types (Warrior, Archer, Horseman, Mage)
  and every character in the game is Warrior — so why do the CP weights talk about
  `meleeMult` / `rangedMult`? What does that mean, and where does a type called `melee` even exist?

- **Answer:** **`melee` is not a type and exists nowhere in the project.** It is the name of the
  `else` branch of a 4→2 fold that `CPCalculator` performs at runtime:

  ```csharp
  bool isRanged = s.type == FighterType.Archer || s.type == FighterType.Mage;  // CPCalculator.cs:26
  float typeMult = isRanged ? w.rangedMult : w.meleeMult;                      // CPCalculator.cs:27
  ```

  Archer + Mage → `rangedMult`; Warrior + Horseman → `meleeMult`. Nothing is ever tagged "melee" —
  it just means "not Archer and not Mage". The source comment on `CPCalculator.cs:25` says so
  directly: *"Map 4 classes → melee / ranged"*.

  The other three types are real in the data but are **fossils of an earlier four-class design**.
  Of the 24 `UnitStatsSO` assets in the project, 20 are type 0 (Warrior) and four are not:

  | type | asset | referenced by |
  |---|---|---|
  | 1 Archer | `UI-SOs/UnitSTats-SO/ArcherStats.asset` | `ArcherDefSO` only |
  | 1 Archer | `BaseStats/PlayerArcher.asset` | **nothing at all** |
  | 2 Horseman | `UI-SOs/UnitSTats-SO/Horseman.asset` | `HorseManDefSO` only |
  | 3 Mage | `UI-SOs/UnitSTats-SO/MageStats.asset` | `MageDefSO` only |

  Verified that `ArcherDefSO`, `HorseManDefSO` and `MageDefSO` are **absent from
  `UnitsDatabaseSO.asset`** (it holds exactly 8 entries, all type 0). They can therefore never enter
  `PlayerUnitsModel`, never spawn, and never reach `CPCalculator`.

- **Consequence — the flavour mechanism is dead three times over:**
  1. **Unreachable by roster design.** Every live hero and every enemy is type 0, so `isRanged` is
     always `false`; the `rangedMult` branch has never executed once.
  2. **The reachable branch is broken.** `meleeMult` is never sampled from the config
     (`CPWeightMath.cs:41-42` — the line is missing, `rangedMult` is assigned twice), so it keeps
     the hard-coded `1f`.
  3. **The data behind it is broken too.** `meleeMultByLevel` in `Player CP.asset` is authored at
     t ≈ −389 and would evaluate to `0.060` even if the line were fixed.

  Net: `typeMult` is exactly 1.0 for every unit in the game and always has been.

- **Latent trap worth remembering:** the day a real Archer or Mage hero is added, the `ranged` branch
  activates for the first time and immediately hits fault 3 — `rangedMultByLevel` is off-axis
  (t ≈ −730, value 0.026) and gets floored to 1.0 by `Mathf.Clamp(..., 1, 5f)`, so the intended 1.05
  ranged bonus would silently not apply.

- **Outcome:** question answered; no action taken. This does not add a new defect — it sharpens
  register items 03 and 04 by establishing that the mechanism is also **unreachable by roster
  design**, not merely buggy. Whether to delete the flavour system, fix it, or leave it for a future
  multi-class roster is still open (see Open decisions B).
- **Files touched:** none (analysis only).

### 2026-09-08 — Scope pinned to the 15 New Characters prefabs; is the melee/ranged split live?

- **Arash established the scope:** the roster that matters is
  `Assets/PREFABS/Characters/New Characters/` — **verified 15 prefabs**: 5 `Deployed Players`
  (Player_CowMinotaur_02, Player_Dark_Oracle_01, Player_Minotaur_02, Player_Pref, Player_Valkyrie),
  6 `Enemies` (Golem_01, Golem_02, Orc, Reaper_Man_01, Skeleton_Crusader_1, Zombie_villager),
  4 `Undployed Players` (Dark_Oracle_3, Fallen_Angels_02, Golem_3, Minotaur_01). All are Warrior,
  therefore all fall in the melee bucket.

- **Arash asked:** is the melee/ranged classification actually applied in the game? If we had a
  Horseman or Warrior, would they really be counted as melee — or is the code simply not active?

- **Answer — the code runs, but it has no effect. Three separate facts:**
  1. **It runs and is correct.** `CPCalculator.cs:26-27` executes on every `UnitCP` call; a Warrior
     or Horseman genuinely takes the `meleeMult` branch.
  2. **It changes nothing.** `meleeMult` is always exactly `1.0` (never sampled from config), and
     multiplying by 1.0 is identity — so "classified melee" is numerically indistinguishable from
     no classification at all.
  3. **It exists only inside CP.** A grep of every `FighterType` use in `Assets/Scripts` shows the
     type is read to make a decision in exactly two places, both CP:

     | Location | Decides |
     |---|---|
     | `CPCalculator.cs:26` | melee vs ranged for `typeMult` |
     | `EnemyManager.cs:152` | same, in the divergent fallback — tests only `Archer`, so it would classify a **Mage as melee**, disagreeing with `CPCalculator` |

     Every other reference is dead storage or an explicit disclaimer: `SkillData.cs:45`
     `allowedTypes` and `:112 AppliesTo()` are labelled **LEGACY** in the source;
     `RogueliteManager.cs:149-150` and `BuffDraw.cs:20-21` carry comments saying they key by
     **unitId, not FighterType**, because *"every UnitDefinitionSO in this project is
     classType = Warrior, so FighterType cannot tell two heroes apart"*; `UnitDefinitionSO.cs:39`
     `classType` is "for UI filtering" but nothing filters by it.

     **Combat never reads `type` at all** — `CombatMath.DamagePerHit` uses only attack and defense;
     targeting, movement, range and AI never consult it.

  Summary: **wired, correct, and completely inert.**

- **Arash's directive (NOTED, NOT APPLIED — he explicitly said do not change the game yet):**
  for Warrior / melee characters, `attackRange` should **not** be counted in the CP calculation.

- **Measured impact of dropping the range term** (computed from the real assets, not estimated):

  | | value |
  |---|---|
  | Range term at L1 | 0.0425 points of a CP of 81 — **0.052%** |
  | Range term at L50 | 0.034 points of 1096 — **0.003%** |
  | Hero rows where displayed CP changes | **3 of 20** |
  | Enemy rows where displayed CP changes | **5 of 120** |
  | Largest change anywhere | **1 point** (rounding-boundary flips only, e.g. slow profile L1: 89 → 88) |

  Two notes attached to that: (a) it makes CP **more honest**, because `attackRange` is dead in
  gameplay anyway — players use the Inspector field `PlayerManager.maxAttackRange` (0.85) and enemies
  use `EnemyLocoMotion.stoppingDistance` (0.83), and the stat-block value is never read (defect 07);
  (b) it **will not fix the ranking inversion**, which is caused by attack sitting at 79% of the
  score while attack speed contributes 0.99% — removing a 0.05% term cannot move that.

- **Open sub-question for when the change is authorised:** because all 15 characters are Warrior,
  *"exclude range for melee"* and *"exclude range entirely"* are currently identical. They diverge
  only when an Archer or Mage is added. That changes the implementation — an `if (!isRanged)` guard
  versus deleting the term outright — so Arash needs to say which he means.

- **Outcome:** question answered; directive recorded as pending. **No change made.**
- **Files touched:** none (analysis only).

### 2026-09-08 — What is `baseScore`? And what range should `typeMult` be in?

- **Arash asked (three parts):** (a) what range *should* `typeMult` be in, and is 1 sensible?
  (b) `rangedMult` appears to be used twice — fix that defect "at the end"; (c) confusion: the CP
  formula was already given, so what is `baseScore` for, and what does
  `return Mathf.RoundToInt(baseScore * typeMult)` actually show?

- **(c) `baseScore` is not a second calculation — it IS the parenthesis.** The one-line formula and
  the code are the same thing written two ways:

  ```
  CP = round( ( wA·ATK + wH·HP + wMv·MoveSpeed + wAS·AtkSpeed + wD·DEF + wR·Range ) × typeMult )
              └───────────────── this entire parenthesis = baseScore ─────────────┘
  ```

  `baseScore` names the weighted sum, i.e. the CP *before* the type multiplier.
  `Mathf.RoundToInt(baseScore * typeMult)` returns the final integer CP — the number the units
  screen displays. Substituting `baseScore` back into the return line reproduces the one-line
  formula exactly. The split exists purely for readability and debuggability.

  Worked example, "fast" hero at L1:
  `baseScore = 1.00×64 + 0.15×100 + 0.25×3.5 + 0.40×2.0 + 0.00×25 + 0.05×0.85 = 80.7175`,
  then `CP = round(80.7175 × 1.0) = 81`.

  **Notation key** (a follow-up question showed the maths/code notation gap is a real trip hazard —
  `w.wA * s.attack` does not *look* like `wA·ATK`, but is identical). In C# the dot means "the field
  inside this object" — it is an address, not an operation. Two objects are in play inside `UnitCP`:
  `s` is the unit's stat block (the method parameter, `CPCalculator.cs:10`) and `w` is the weights
  sampled at this unit's level (`var w = CPWeightMath.Evaluate(level, cfg);`, `CPCalculator.cs:14`).
  Maths notation omits which object a value came from; code cannot, because many units exist at once.

  | Formula shorthand | Actual code | Note |
  |---|---|---|
  | `wA` | `w.wA` | `w.` = from the weights struct |
  | `ATK` | `s.attack` | `ATK` is a doc shorthand; the field is named `attack` |
  | `HP` | `s.maxHP` | shorthand |
  | `MoveSpeed` | `s.moveSpeed` | same word, C# casing |
  | `AtkSpeed` | `s.attackSpeed` | shorthand |
  | `DEF` | `s.defense` | shorthand |
  | `Range` | `s.attackRange` | shorthand |
  | `·` | `*` | both mean multiply |

  Written purely in code names, the formula is character-for-character the source:
  `CP = round( ( w.wA·s.attack + w.wH·s.maxHP + w.wMv·s.moveSpeed + w.wAS·s.attackSpeed
  + w.wD·s.defense + w.wR·s.attackRange ) × typeMult )`.
  The `ATK`/`HP`/`DEF` shorthands are this documentation's own labels, chosen to match the UI — they
  are not different values.

- **(a) What range should `typeMult` be in?**
  **While every unit is one type, 1.0 is the only value that means anything.** A multiplier applied
  identically to every unit changes nothing comparative — if `typeMult` were 1.5 for all 15
  characters, every CP would be 50% larger and every ranking identical. It is cosmetic inflation.
  `typeMult` only earns its place once units differ in type.

  For a future multi-class roster:

  | | |
  |---|---|
  | Convention | one class is the baseline at **1.00**; others relative to it |
  | Sensible band | **0.85 – 1.25** (±15–25%) |
  | Rationale | the weighted sum already prices the stats — a ranged unit's reach is already in `wR·Range`. `typeMult` should only capture what the stats do NOT model (e.g. taking less return damage by attacking from outside melee reach), which is a second-order effect. |
  | Beyond ±25% | the multiplier starts dominating; fix the **weights** instead. |

  **The current clamp is badly chosen** (`Mathf.Clamp(..., 1, 5f)`, `CPWeightMath.cs:41`):
  the floor of 1 means ranged can never be worth *less* than melee — "fragile mage worth less per
  stat point" is inexpressible; the ceiling of 5 permits a multiplier that would swamp every other
  term. A band like `[0.5, 2.0]` would be honest. Also noted: authoring **both** melee and ranged as
  separate curves is over-engineering — one fixed baseline plus one relative multiplier suffices,
  and that redundancy is exactly what produced the copy-paste bug.

- **(b) Fixing the double `rangedMult` alone will change nothing.** The intended fix is
  `w.meleeMult = Mathf.Clamp(cfg.meleeMultByLevel.Evaluate(L), 1, 5f)`. `meleeMultByLevel` is
  authored off-axis and evaluates to `0.060`; the clamp floor of 1 raises it straight back to
  **1.0** — precisely the hardcoded value `meleeMult` already carries. Output is byte-identical
  before and after.

  Making `typeMult` actually do something requires **all three** of:
  1. fix the double assignment (code bug, `CPWeightMath.cs:41-42`)
  2. re-author `meleeMultByLevel` and `rangedMultByLevel` onto the level axis (data bug, `Player CP.asset:159-206`)
  3. have units that are not all the same type (roster)

  None of the three is currently true, so the fix is correctness hygiene: worth doing so the code
  says what it means, but it moves no number.

- **Outcome:** questions answered. The `rangedMult` fix is **authorised but deferred** — Arash said
  "fix it at the end". Open sub-question: does he want only #1, or #1 and #2 together?
- **Files touched:** none (analysis only).

### 2026-09-08 — FIRST CODE CHANGE: the duplicated `rangedMult` assignment is fixed

- **Arash asked:** "did you fix this bug? if not, fix it." — explicit authorisation, so the deferral
  from the previous entry ends here. Interpreted as the **code fix only** (#1); the flavour curves
  are a separate Inspector/data change and were **not** touched.

- **The change**, `Assets/Scripts/Data/CombatPower/CPWeightMath.cs:41` — a one-line diff:

  ```diff
  - w.rangedMult = Mathf.Clamp(cfg.rangedMultByLevel.Evaluate(L), 1, 5f);
  + w.meleeMult  = Mathf.Clamp(cfg.meleeMultByLevel.Evaluate(L), 1, 5f);
    w.rangedMult = Mathf.Clamp(cfg.rangedMultByLevel.Evaluate(L), 1, 5f);
  ```

  The clamp mirrors the existing `rangedMult` treatment exactly — deliberately a minimal, faithful
  fix rather than a redesign of the (badly chosen) `1..5` band, which was not authorised.

- **Verified to change NO CP value.** Parsed both config assets' raw YAML and reimplemented
  `AnimationCurve.Evaluate` outside Unity, comparing `meleeMult` before (hard-coded `1f`) against
  after (`Clamp(Evaluate(L), 1, 5)`) at **every level 1–50**:

  | Asset | `meleeMultByLevel` | raw Evaluate(L) | after clamp | before | max diff |
  |---|---|---|---|---|---|
  | `Player CP.asset` | off-axis, keys at t = −389.47 / −357.95 | 0.060408 (all L) | **1.000000** | 1.0 | **0** |
  | `EnemyCP.asset` | clean, flat 1.0 → 1.0 | 1.000000 | **1.000000** | 1.0 | **0** |

  The `Clamp(..., 1, 5f)` floor lifts the broken curve's 0.060 straight back to the 1.0 the buggy
  code already produced. Every number in this analysis remains valid — no report figure changed.

- **What this fix does and does not achieve.** It makes the code do what it plainly intended, so
  `meleeMultByLevel` is now genuinely read. It does **not** make `typeMult` functional, which still
  needs both of: the `Player CP.asset` flavour curves re-authored onto x = 1…50 (now tracked as a
  separate pending directive), and a roster containing units that are not all Warrior. Editing the
  melee curve in the Inspector today *still* appears to do nothing.

- **Outcome:** defect register item 03 marked FIXED in `CP_SYSTEM_ANALYSIS.md` (both language
  sections). Item 04 (off-axis flavour curves) remains open and is now the sole blocker on this path.
- **Files touched:** `Assets/Scripts/Data/CombatPower/CPWeightMath.cs` (1 line) and its reference doc
  `Assets/Documentation for scripts/CPWeightMath.txt`, committed together as `80ed65b`, separately
  from the analysis docs per Arash's standing preference. **Not verified in Play mode** — no
  behaviour changed, so there is nothing to observe.

### 2026-09-08 — "What is the ProgressionMath loop for?"

- **Arash asked:** having seen the CP formula, what are the `ProgressionMath.cs:20-45` compound-growth
  formulas *for*?

- **Answer — they are a different system from CP, and the distinction is the whole point:**

  > **`ProgressionMath` changes the game. `CPCalculator` only changes the display.**
  > Delete `CPCalculator` and the game plays identically, minus a number on the units screen.
  > Delete `ProgressionMath` and upgrades stop working — every hero is stuck at level-1 strength.

  `UnitStatsSO` stores only **level-1** values. Nothing in the asset says what a level-10 hero
  should have. `ProgressionMath` answers that, and it produces **multipliers**, not stats:

  ```
  UnitStatsSO       level-1 base            attack = 64
        │
  ProgressionMath   level → multipliers     gA = 1.9155
        │
  UnitStatsRuntime  base × multiplier       attack = 122.59
        ├───────────► COMBAT (CombatMath deals real damage from this)
        └───────────► CPCalculator → CP 155 (display only)
  ```

  So it runs **before** CP and feeds it. A level-10 hero shows CP 155 instead of 81 not because CP
  changed, but because the stats it reads grew.

- **How the loop works — three details worth noting:**
  1. **Starts at `l = 2`.** Level 1 is the base and gets no growth; asking for level 1 skips the loop
     entirely and every multiplier stays `1.0`.
  2. **`*=` is multiply-accumulate, not add** — compound interest:
     `gA = (1+pct(2)) × (1+pct(3)) × … × (1+pct(L))`.
  3. **`Evaluate(l)` uses the loop counter `l`, not the target level `L`** — each level looks up its
     *own* percentage. That is the entire reason the config uses curves rather than constants: the
     designer can taper the rate.

- **Worked example, "fast" hero (base attack 64), from the real `atkPctByLevel` curve (+8% → +3%):**

  | Level | that level's % | running `gA` | actual attack |
  |---|---|---|---|
  | 1 | — (loop skipped) | 1.0000 | 64.00 |
  | 2 | +7.898% | 1.0790 | 69.05 |
  | 3 | +7.796% | 1.1631 | 74.44 |
  | 5 | +7.592% | 1.3477 | 86.25 |
  | 10 | +7.082% | 1.9155 | 122.59 |
  | 20 | +6.061% | 3.6025 | 230.56 |

  The percentage falls every level while the absolute gain rises (level 2 adds ~5 attack, level 20
  adds ~13). Compounding against a tapering rate — a deliberate and sensible design.

- **`ClampPct(..., cfg.pctClamp)` is the safety rail** that forces each percentage into −0.25…+0.50.
  Note this is **the guard that failed to catch the two off-axis curves**: `0.0625` sits comfortably
  inside the legal window. The *value* is reasonable; its *position on the axis* is what is wrong.

- **Six separate curves in one loop is precisely why stats do not grow together** — this loop is the
  origin of the ×3.60 attack / ×4.94 HP / ×1.40 attack-speed divergence at level 20. Two of its six
  lines (`g.gD`, `g.gR`) read the broken curves. The loop itself is correct; its data is not.

- **Call sites — 11, verified by grep:**

  | Caller | Purpose | Multipliers applied |
  |---|---|---|
  | `PlayerStatsApplier.cs:129` | real combat stats | **all 6** |
  | `EnemyManager.cs:113` | real enemy stats (level = stage) | only 4 |
  | `UnitsPanelController.cs:516, 524, 578, 880, 961` | units-screen display + next-level preview | only 4 |
  | `NewCharacterStats.cs:234` | new-hero popup | only 4 |
  | `PlayerProgressionService.cs:233-234` | correct 6-stat snapshot — **nothing calls it** | all 6 |
  | `EnemyManager.cs:71` | dead sibling `RebuildFromBase1` | only 4 |

  That mismatch in the last column **is defect 02**: one function, called everywhere, but callers
  consume different subsets of its output. Combat applies six multipliers, every menu applies four —
  which is why a level-10 hero fights with defense 43.1 while the screen says 25.

- **Outcome:** question answered; no action taken.
- **Files touched:** none (analysis only).

<!--
Template for each point:

### YYYY-MM-DD — <the point, in one line>
- **Arash asked:** ...
- **Answer / finding:** ... (cite file:line)
- **Outcome:** decided X / deferred / needs more investigation / no action
- **Files touched:** ... or `none`
-->

---

## Decisions taken

_Empty. Move a row here from "Open decisions" once it is settled, with the reasoning._

---

## How to resume this on another computer

1. `git clone https://github.com/ariaz88/Blasty-Stacks.git` (or `git pull` if you already have it).
2. Open the repo in Claude Code. The `SessionStart` hook in `.claude/settings.json` injects
   `SESSIONS.md` automatically, so the new session already knows the project history.
3. Tell it: **"read `Docs/cp-analysis/CP_DISCUSSION_LOG.md` and
   `Docs/cp-analysis/CP_SYSTEM_ANALYSIS.md`, then continue the CP discussion."** Those two files
   plus `SESSIONS.md` contain everything the original session concluded.
4. The charted report is at the artifact URL above — it opens in a browser on any machine you are
   logged into claude.ai with.

**What does NOT transfer:** the literal Claude Code chat transcript. It lives only on the machine
that ran it (`~/.claude/projects/<project>/<session-id>.jsonl`) and Claude Code does not sync
sessions between computers. That is exactly why this file exists — it carries the *conclusions*
so a fresh session does not have to redo the investigation.

---
---

# بخش فارسی

## این فایل چیست

`CP_SYSTEM_ANALYSIS.md` = **یافته‌ها** (چه چیزی درست است).
این فایل = **تصمیم‌ها** (قرار است چه کار کنیم).

از تاریخ ۲۰۲۶-۰۹-۰۷: **هیچ تصمیمی گرفته نشده و هیچ کدی تغییر نکرده است.**

## قواعد این گفتگو

۱. پاسخ‌ها **به انگلیسی**، حتی وقتی سؤال فارسی است.
۲. **فقط تحقیق و تحلیل — بدون تغییر کد، اسِت، پریفب یا صحنه**، مگر اینکه آرش صریحاً تغییر بخواهد.
۳. **دقت مهم‌تر از سرعت است.** هر ادعا باید یک `file.cs:line` یا `Asset.asset:line` همراه داشته باشد.
۴. نکته‌ها **یکی‌یکی** مطرح می‌شوند و هرکدام در «Discussion log» بالا ثبت می‌شود.

## سه یافته‌ای که احتمالاً محور بحث خواهند بود

۱. **`CP ≈ ATK + 0.15·HP`** — در لِوِل ۱: حمله ۷۹٫۳٪، جان ۱۸٫۶٪، چهار استت دیگر روی‌هم ۲٫۱٪،
   و دفاع دقیقاً ۰٫۰۰٪. سهم دفاع در هیچ لِوِلی از ۰٫۱۷٪ فراتر نمی‌رود.
۲. **CP روستر را وارونه رتبه‌بندی می‌کند** — پروفایل ۱۲۸ DPS عدد **۸۱** نشان می‌دهد و پروفایل
   ۱۰۸ DPS عدد **۸۹**. این در تمام لِوِل‌های ۱ تا ۵۰ برقرار است.
۳. **چهار `AnimationCurve` روی زمان منفی نوشته شده‌اند** و بی‌سروصدا به مقادیری clamp می‌شوند که
   کسی انتخابشان نکرده — `defPctByLevel` (ثابت ۶٫۲۵٪+ در هر لِوِل)، `rangePctByLevel` (۶٫۷۵٪+)،
   و هر دو منحنی سبک. `pctClamp` جلویشان را نمی‌گیرد چون مقادیر داخل بازه‌ی مجازند.

## ادامه‌دادن از یک کامپیوتر دیگر

۱. `git clone https://github.com/ariaz88/Blasty-Stacks.git` (یا `git pull`).
۲. پروژه را در Claude Code باز کنید. هوک `SessionStart` فایل `SESSIONS.md` را خودکار تزریق می‌کند،
   پس سشن جدید تاریخچه‌ی پروژه را از قبل می‌داند.
۳. بگویید: **«فایل‌های `CP_DISCUSSION_LOG.md` و `CP_SYSTEM_ANALYSIS.md` را بخوان و بحث CP را ادامه بده.»**
۴. گزارش نموداری از طریق لینک artifact بالا روی هر کامپیوتری که به claude.ai لاگین باشید باز می‌شود.

**چه چیزی منتقل نمی‌شود:** خودِ متن چت Claude Code. آن فقط روی همان کامپیوتر ذخیره می‌شود
(`~/.claude/projects/<project>/<session-id>.jsonl`) و Claude Code سشن‌ها را بین کامپیوترها
همگام‌سازی نمی‌کند. دقیقاً به همین دلیل این فایل ساخته شده — **نتیجه‌ها** را منتقل می‌کند تا سشن
جدید مجبور نباشد کل تحقیق را از اول انجام دهد.
