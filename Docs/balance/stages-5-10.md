# Early campaign balance — candidate under play-test

> **2026-09-21: every non-tutorial enemy was raised to 1.80x CP** (ATK x 1.80^0.60 = 1.422864, HP x 1.80^0.40 = 1.265054 — Arash: attack carries 60% of the rise;
> DEF and attack speed untouched), because heroes do not scale with the stage — only with
> upgrades — so an unupgraded player meets stage 6 with the stage-4 hero. All enemy-to-enemy
> ratios are unchanged. Every enemy CP and total below predates this and must be multiplied by
> 1.80; `latest-playmode.csv` likewise. New stage totals: 4: 696.6 · 5: 966.7 · 6: 1192.3 ·
> 7: 1512.0 · 8: 1617.8 · 9: 2200.4 · 10: 2508.4. An unupgraded hero dies in ~6.7-8.0 blows at
> stage 6 (was 9.6-11.4). Stage 6 now beats a FULL unupgraded roster (10 heroes = 1164.7 vs
> 1192.3), which was the "enemies always win without upgrades" target. Stages 1-3 (`Tutorial/` assets) untouched.

Stage means stage within chapter 1. Stages 1–4 combat assets and the starting four heroes' base stats are preserved. Actual baseline hero CP is approximately 116–121, rather than the spreadsheet's 125; stage-4 Reaper/Zombie CP is approximately 92/101. CP is calculated from stats, never assigned at battle start to force an outcome.

## Scenario targets

These are acceptance targets, not measured results. W = intended win, L = intended loss. Only the final match should give a fast/easy win. Other winning scenarios should become slower/harder as fewer heroes are earned.

| Stage | Heroes after successive matches | Target results | Enemies | Expected starting-roster level |
|---|---|---|---|---|
| 5 | 1, 2, 4, 6, 8, 12 | L, L, slow L, slow W, normal W, fast W | 5 | 1 |
| 6 | 1, 2, 4, 6, 10 | L, L, L, normal W, fast W | 5 | 2 |
| 7 | 1, 2, 4, 5, 7, 9, 13 | L, L, L, L, slow W, normal W, fast W | 6 | 2 |
| 8 | 1, 2, 4, 6, 8, 12 | L, L, L, slow L, normal W, fast W | 6 | 2 |
| 9 | 1, 3, 5, 7, 9, 13 | L, L, L, slow L, normal W, fast W | 7 | 2 |
| 10 | 1, 2, 4, 6, 8, 11, 15 | L, L, L, L, slow L, normal W, fast W | 7 | 3 |

Stage 6's final deployment is four heroes, following the explicit game rule; the reference's final two is superseded. Stage 5/7/10 non-final Fast Win cells are also superseded by the requested pacing.

Initial same-type CP increments at stages 5–10: 10%, 12%, 10%, 7%, 6%, 14%. Each increment is split multiplicatively between attack and HP; defense and attack speed hold steady over these stages. Army CP growth also includes count and composition changes. These percentages require actual combat validation.

**Superseded for stage 6 on 2026-09-21 (Arash).** The stage-6 newcomer is now the **Skeleton Swordsman**, not the Orc, at **+20%** over the Zombie villager's stage-6 CP (149.7 vs 124.7) rather than +15% over the strongest existing enemy. Its attack speed matches the other stage-6 enemies. Stage 6 fields 5 enemies as 2 + 3: 1 Reaper, 2 Zombies, 2 Skeletons, with both Skeletons in the last wave. The Orc's debut moves to stage 7. `Enemy_Skeleton_Swordsman` is a new stats asset, so `Enemy_Skeleton_Crusader_1` and stages 9+ are unchanged. Stage 6 total enemy CP moves 620.2 → 662.4.

Spawn placement was also standardised that day: formations are **centred** on the spawn box at **3-unit spacing** (2 enemies at ±1.5, 3 enemies at −3 / 0 / +3), and the spawn line moved 2 units closer to the enemy gate. This came out of a layout bug in `EnemySpawner.GenerateGridPositions`, which left-aligned the row and clamped overflow — against the real 6-wide gate-relative box that put 2-enemy waves off-centre and dropped two of a 3-enemy wave onto the same point. Note the per-wave `spawnMin`/`spawnMax` in the `Stage_NN` assets are dead for stages built on `LevelTemplate.prefab`; the live box is `gateRelativeMin`/`Max`. 4-enemy waves (stages 9–10) compress to 2-unit spacing in that box.

Orc enters at stage 6 with CP 15% above the strongest existing enemy at that stage. Skeleton enters at stage 9 with CP 15% above Orc. Counts plateau when new types arrive. New hero Golem unlocks at stage 8 into Undeployed; its level-1 CP is 10% above the strongest starter at upgrade level 2. Future hero milestones 12/16/20 are provisional and outside this combat test scope.

## Upgrade economy

| Stage won | Minimum coins | Hero XP |
|---|---:|---:|
| 1 | 20 | 1 |
| 2 | 30 | 1 |
| 3 | 40 | 1 |
| 4 | 50 | 1 |
| 5 | 60 | 1 |
| 6 | 70 | 1 |
| 7 | 80 | 2 |
| 8 | 90 | 2 |
| 9 | 100 | 3 |
| 10 | 110 | 3 |

Coins receive 10%/20% bonuses for HP cases 2/3; Hero XP does not receive an HP multiplier. Gems retain their existing calculation. First upgrade: 40 coins + 1 Hero XP per hero. Second: 60 + 2. Third: 90 + 3. No automatic hero upgrades occur on entering a stage.

With one minimum-tier win at each stage, stages 1–5 provide 200 coins and 5 XP, funding four first upgrades for 160 coins and 4 XP. Stages 6–9 add 340 coins and 8 XP, funding four second upgrades for 240 coins and 8 XP.

Stage 1 was changed from 0 to 1 Hero XP on 2026-09-21 (directive: the player should earn Hero XP from their very first win). Coins remain the binding constraint on the four first upgrades — 140 banked after stage 4 against a 160 cost — so the stage-5 timing above is unchanged; only the XP requirement is met a stage earlier, leaving 1 spare. The second-upgrade milestone is untouched. This assumes spending on the original four heroes; purchases, repeated wins, optional rewards and upgrading a new hero can change the timing. This is affordability, not a stage lock on upgrading.

## Verification

`EarlyCampaignEconomyVerification.Run()` exercises the actual wallet and progression service, checks both milestones, resource-shortage atomicity and cap behavior, and restores the original save.

`EarlyCampaignVerification.Start(5,10,1)` runs every match-count scenario in the real scenes using the actual battle start, six-second deployment queue, animations, hitboxes and gates. It isolates/restores saves and the original scene. Results are written to `latest-playmode.csv`. A timeout is reported separately and must not be interpreted as a loss. A single seed is diagnostic and cannot establish 95–99% reliability. The automated harness awards puzzle matches through the existing match event; it does not prove puzzle-board solvability.

Total battle duration includes deployment and gate travel. `armyDefeatedSeconds` and casualty counts should also be reviewed: serial six-second reinforcement batches can make the full-match wall time longer despite an easier fight. Fast/normal/slow labels are design targets, not forced runtime outcomes.

## Fortress Merge evidence

The [official mobile listing](https://play.google.com/store/apps/details?hl=en_US&id=com.ttt.fortressmerge) describes the wave-defense/merge progression. Available public evidence did not provide a verified enemy-count series for stages 1–20. The plateau counts above are a candidate design for this project, not a claimed reproduction of Fortress Merge. Search results describing different games or unsupported exact growth percentages were not used as balancing evidence.
