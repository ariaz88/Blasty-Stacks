# CP level and battle rules

Recorded from Arash's design explanation and the supplied `Levels 1-5` and `Summary` screenshots.
Implementation update (2026-09-10): the spawn sequences below apply ONLY to stages 1-5.
Stage 6 onward retains the previous spawning and combat behavior. The generalized rule
below is a future design direction, NOT currently applied beyond stage 5.

## Latest correction: physical combat, not timed CP depletion

The timer-driven CP-budget implementation was rejected after playtesting. It has been
removed: no automatic health drain, remote enemy kills, or automatic gate destruction.
Only actual weapon hits deal damage. The opposing gate must physically reach zero HP
before the level can end; mutual-wipe auto-defeat is disabled for these five stages.

Enemy counts are fixed at 1, 2, 3, 4, 5 for stages 1, 2, 3, 4, 5 respectively.
These latest fixed counts supersede earlier suggestions based on how many heroes deploy.
Enemy team CP is 80, 100, 115, 400, 650 respectively. Runtime enemy ATK and HP are
scaled together to reach each total, preserving differences among the authored enemy
types. Individual enemy CP need not be equal (400/4 = 100 is only an equal-type example).
Heroes are calibrated to reference CP 100 in stage 1 and 125 in stages 2-5, preserving
their relative ATK/HP shape, defense, attack speed, movement, range and identity.
This tutorial normalization intentionally limits the raw CP benefit of upgrades here;
upgrades and ordinary stats remain unnormalized in stage 6 onward.

CP = ATK * AttackSpeed * MaxHP * (1 + DEF / 100) / 200.
This is a reference strength score, NOT a guarantee of physical battle outcomes.
Explicit encounter assistance is separate from that number:

- Both teams take ordinary incoming damage. The former blanket 75% damage reduction
  for winners and bonus damage against losers have been removed to allow casualties.
- Each hit removes at most 25% of the victim's maximum HP. A positive-hit counter also
  prevents death before the fourth hit, including floating-point edge cases.
- The winning side's last surviving unit cannot fall below 1% HP from incoming
  attacks. Other winning units can die normally; this is a last-survivor safeguard.
- Stage 5 with four heroes uses 60% incoming damage on heroes for its Slow Loss,
  giving them more time to damage and kill enemies before losing.
- Bases are protected while their own defending units remain alive. Once those units
  die in combat, normal incoming weapon hits damage and destroy the base.
- No timer forces Fast/Normal/Slow durations. Those spreadsheet labels remain pacing
  targets; real movement, attack animations and siege duration determine elapsed time.

These safeguards enforce the intended tutorial advantage, not a claim of perfect AI:
broken pathfinding, missing attack events, or unreachable gates can still stall combat.
Never repair a stall by remotely killing a unit or declaring a win with both bases alive.

Production assets are unmodified: the overrides happen at runtime in EnemySpawner and
PlayerWaveManager. Verified scene references already use Characters/New Characters/
Deployed Players, Enemies, and the existing folder spelled Undployed Players.
Only Level_1_Stage_1 through Level_1_Stage_5 are in scope.

## Required variables

- `Level`
- `Match`
- `Total Pairs`
- `Board Solved`
- `Heroes Deployed`
- `Total Heroes`
- `Player CP`
- `Enemy CP`
- `Battle Result`

`Match` is the number of solved pairs so far. `Board Solved` is `Match / Total Pairs`. `Heroes
Deployed` is the number added by the current match, while `Total Heroes` is the cumulative number
on the battlefield. `Player CP` and `Enemy CP` are total team values.

## Levels 1-3: special deployment rules

| Level | Total Pairs | Deployment Sequence | Hero CP | Maximum Heroes | Enemy CP |
|---:|---:|---|---:|---:|---:|
| 1 | 3 | `1 → 1 → 1` | 100 | 3 | 80 |
| 2 | 3 | `1 → 1 → 2` | 125 | 4 | 100 |
| 3 | 4 | `1 → 1 → 2 → 1` | 125 | 5 | 115 |

## Levels 4 onward: standard deployment rule

- The first match deploys 1 hero.
- The second match deploys 1 hero.
- Middle matches deploy 2 heroes each.
- The final match deploys 4 heroes (the full-spawn reward).
- Therefore, for `N` pairs, the maximum cumulative hero count is `2 × N`.

Confirmed examples:

| Level | Total Pairs | Deployment Sequence | Hero CP | Maximum Heroes | Enemy CP |
|---:|---:|---|---:|---:|---:|
| 4 | 4 | `1 → 1 → 2 → 4` | 125 | 8 | 400 |
| 5 | 6 | `1 → 1 → 2 → 2 → 2 → 4` | 125 | 12 | 650 |

## Exact Levels 1-5 outcomes

| Level | Match | Total Pairs | Board Solved | Heroes Deployed | Total Heroes | Player CP | Enemy CP | Battle Result |
|---:|---:|---:|---:|---:|---:|---:|---:|---|
| 1 | 1 | 3 | 33% | 1 | 1 | 100 | 80 | Fast Win |
| 1 | 2 | 3 | 67% | 1 | 2 | 200 | 80 | Fast Win |
| 1 | 3 | 3 | 100% | 1 | 3 | 300 | 80 | Fast Win |
| 2 | 1 | 3 | 33% | 1 | 1 | 125 | 100 | Fast Win |
| 2 | 2 | 3 | 67% | 1 | 2 | 250 | 100 | Fast Win |
| 2 | 3 | 3 | 100% | 2 | 4 | 500 | 100 | Fast Win |
| 3 | 1 | 4 | 25% | 1 | 1 | 125 | 115 | Normal Win |
| 3 | 2 | 4 | 50% | 1 | 2 | 250 | 115 | Fast Win |
| 3 | 3 | 4 | 75% | 2 | 4 | 500 | 115 | Fast Win |
| 3 | 4 | 4 | 100% | 1 | 5 | 625 | 115 | Fast Win |
| 4 | 1 | 4 | 25% | 1 | 1 | 125 | 400 | Fast Loss |
| 4 | 2 | 4 | 50% | 1 | 2 | 250 | 400 | Fast Loss |
| 4 | 3 | 4 | 75% | 2 | 4 | 500 | 400 | Fast Win |
| 4 | 4 | 4 | 100% | 4 | 8 | 1000 | 400 | Fast Win |
| 5 | 1 | 6 | 17% | 1 | 1 | 125 | 650 | Fast Loss |
| 5 | 2 | 6 | 33% | 1 | 2 | 250 | 650 | Fast Loss |
| 5 | 3 | 6 | 50% | 2 | 4 | 500 | 650 | Slow Loss |
| 5 | 4 | 6 | 67% | 2 | 6 | 750 | 650 | Fast Win |
| 5 | 5 | 6 | 83% | 2 | 8 | 1000 | 650 | Fast Win |
| 5 | 6 | 6 | 100% | 4 | 12 | 1500 | 650 | Fast Win |

## Interpretation constraints

- These rows are intended outcomes and balance targets, not proof that CP predicts combat.
- The `Enemy CP` values shown for Levels 1-5 are provisional and may be changed during balancing.
- The example tables assume equal CP among all heroes and equal CP among all enemies only to make
  the spreadsheet easy to reason about. The real roster contains units with different individual CP.
- The required win/loss transition is authoritative even when the provisional CP values change.
  For example, at Level 4, solving only one or two matches must produce a player loss, so the final
  enemy team must be stronger than those player deployments. Solving three or four matches must
  produce a player win.
- Enemy count, individual enemy strength, hero count, individual hero strength, composition,
  targeting, and battlefield behaviour are balancing inputs. They must be evaluated rather than
  inferred from the simplified equal-unit examples.
- The screenshots only expose the detailed data for Levels 1-5. The workbook tab names indicate
  that Levels 6-15 and summaries exist, but their values are not visible in the supplied images.
- The spoken description called Level 5 a five-pair level in one place, but both screenshots show
  six pairs and six matches. The six-pair table is recorded as authoritative pending correction.
- `Deck CP` appears in the first screenshot (400 for Level 1 and 500 for Levels 2-5), but it was not
  included in the requested variable list, so it is recorded as supplementary context rather than
  part of the battle table.
