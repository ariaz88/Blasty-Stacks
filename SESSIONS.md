# SESSIONS.md — Shared Session Log / لاگ مشترک سشن‌ها

> **فارسی:** این فایل حافظه‌ی مشترک بین همه‌ی سشن‌های Claude Code در این پروژه است.
> در شروع هر سشن به‌صورت خودکار تزریق می‌شود، پس **باید کوتاه بماند**.
> متن کامل و مفصل هر چیزی در `SESSIONS-ARCHIVE.md` است که تزریق نمی‌شود.
>
> **English:** Cross-session memory for this repo. It is injected into every session by a
> `SessionStart` hook, so **it must stay small**. The full, long-form text of every entry and
> thread lives in `SESSIONS-ARCHIVE.md`, which is NOT injected — open it on demand.

---

## Protocol (for Claude — follow exactly)

**At the START of every session:**
1. Read this whole file. Check **Open Threads** and **Decisions** before proposing anything;
   do not re-litigate a recorded decision or redo logged work.
2. Need the detail behind a one-liner? Grep `SESSIONS-ARCHIVE.md` for that date/title.
   Do not read the archive whole.
3. If an entry names a file, class, or flag, verify it still exists — entries reflect what was
   true when written.

**At the END of every session, or after any meaningful change:**
1. Append one entry to the **top** of the Session Log — **one line, max two.**
   Format: `- **YYYY-MM-DD** — <what changed>. <files/areas>. <verified or NOT>.`
2. If the work needs more than two lines to be useful, put the long form in
   `SESSIONS-ARCHIVE.md` under the same date+title, and keep the one-liner here.
3. Update **Open Threads** — add what you left unfinished, **delete what you closed.**
   A thread is max 3 lines. Longer detail goes to the archive.
4. Add to **Decisions** only for a durable choice (architecture, naming, a rejected approach
   and why). Not for routine edits.

**HARD SIZE CAPS — enforce these, they are the point of this file:**
- This file stays **under ~400 lines / ~30 KB**. Check with `wc -l SESSIONS.md` before you finish.
- Session Log: **last 25 one-liners.** Older ones are deleted from here (the archive keeps them).
- Open Threads: **max 3 lines per thread.** If you are writing a fourth, archive the detail.
- Over the cap? Trim this file as part of your session — that IS session work, not a chore.

**Rules:**
- One entry per session, not per message. Edit your own entry as the session continues.
- Absolute dates (`2026-09-14`), never "today" / "yesterday".
- Reference code as relative paths, e.g. `Assets/Scripts/Core/LevelManager.cs:42`.
- Facts only: what changed, why, what broke, what is left. No summaries of chat.
- Parallel sessions: never overwrite another entry — append yours and note the overlap.
- Unity note: `.unity` / `.prefab` / `.asset` edits are invisible in git. If you changed one
  **by hand in the Editor**, say so explicitly — the next session cannot see it in the diff.
- Standing instruction from Arash: **all notes in English.**

---

## Open Threads

_Unfinished work any session may pick up. **Delete a line when it is genuinely closed.**
Max 3 lines each — long form goes to `SESSIONS-ARCHIVE.md`._

### Release blockers

- **[2026-10-04] First release ships STAGES 1-10 ONLY.** Build list: `Level_1_Stage_1..10` ticked, `11..20` UNTICKED (kept in the
  list), `ComingSoonScene` added. Winning stage 10 / BATTLE on 11+ -> ComingSoonScene (`StageBuildAvailability`). To ship more: tick
  them back (or `Tools/Blasty/Coming Soon/Build Scene + Ship Stages 1-10` with a new `LastShippedStage`). Win-at-10 path NOT played.
- **Ads release blockers (2026-08-21), all still set for testing:** `BattleStartController.doNotPersistAllowance`
  must go OFF; `AdManager.useTestIds` OFF with real unit ids; `GoogleMobileAdsSettings.asset` still holds
  Google TEST app ids; Android `applicationIdentifier` is UNSET. **Nothing has ever run on a device.**
- **[2026-09-07] Three features are OFF and must be switched back ON next version.**
  `Assets/Scripts/Core/GameFeatureFlags.cs` — `RogueliteEnabled`, `LastStandOfferEnabled`,
  `HeroBuyBackEnabled` all `false`. Nothing deleted. Re-enabling `LastStandOfferEnabled` alone also
  needs its `requireBuyBacksSpent` turned off.

- **[2026-09-23] Guided onboarding: the 4-step menu chain now passes end-to-end (driven via `ExecuteEvents`);
  only the LOOK is unverified.**
  Play `Assets/Scenes/TestScenes/OnBoarding Test.unity` (needs `Tools/Testing/Play Any Scene Directly`).
  Still unverified by eye: tooltip bubble placement/size, the hand landing on the right pixel, and
  whether steps 2-4 chain cleanly, and **the hole-freeze-on-press fix (needs a real mouse press; legacy
  `Input` cannot be faked from a script)**. **Never re-enable `TutorialFocusGate.blockedTapsToAbort`** — at
  6 it let a tester unlock the whole game by tapping around. `TutorialSandbox` is EDITOR-ONLY, keep it so.
  **Re-check (2026-09-25):** a sandbox session once showed the gate BLOCKING with no tutorial running.

- **[2026-09-25] Attack VFX: 4 Deployed heroes + the 3 enemy types of stages 1-10 wired** (via `HeroAttackVfx`, also
  called from `EnemyAnimatorManager`). Still NO attack VFX: non-deployed heroes (Golem_3, Fallen_*, Dark_Oracle_3) and
  enemies from stage 13+ (Orc, Golem_01/02). Generators: `Assets/VFXKit/Effects/<name>/make_arc.py.txt`.

- **[2026-09-22] `Assets/Resources/StageSpawnerIndex.asset` is BUILT and correct (1-2 `Spawner2`,
  3-20 `Stage_NN`). RE-RUN `Tools/Blasty/Stage CP/Rebuild Stage Spawner Index` whenever a stage's
  spawner config changes**, or Home's "TOTAL CP" silently shows the old stage's enemy CP.

### Open design questions — base HP only half-controls base durability

- **[2026-09-23] The PLAYER base ignores its own HP while any hero lives.** `PlayerGateStats`
  replaces the blow with `maxHealth * LevelBattleRules.BaseChipPerBlow` (0.01) — **exactly 100 blows
  to fell it, at ANY HP.** Raising HP only lengthens the phase after the army is already dead. The
  knob for the defended phase is `BaseChipPerBlow`; Arash has not been asked to change it yet.
- **[2026-09-23] The ENEMY base is fully IMMUNE while any enemy lives** (`EnemyGateStats
  .ApplyDamageToEnemy` early-returns on `CPBattleController.HasLivingDefenders`). So its whole HP bar
  is spent by the SURVIVING heroes after the field is clear — that is where "many attackers at once"
  bites hardest, and it is why the enemy base was sized at 4× rather than 3×.

### Never played — compile / edit-mode verified only

_Each of these compiles clean and has NEVER been in Play mode. Detail + what to judge: archive._

- **[2026-09-14] PHASE 2 card rewards — the whole animation is unplayed.** Only the fan's REST pose
  was posed and screenshotted in edit mode. The fly-in, flip, collapse, white flash, teal glow, fade,
  all timings, and the anchor's real position above the player base have never run. `HeroCardRevealDirector`.
- **[2026-09-21] The new upgrade cost tables are Editor-verified but UNPLAYED.** `EarlyCampaignEconomyVerification.Run()`
  passes, so the wallet math is proven — what is NOT known is how a deck of four level-3 heroes actually
  fares in stage 6-9 combat now that upgrade #1 costs 50 instead of 40. (The "hero XP is farmable by
  replaying" half of this thread was CLOSED 2026-09-22: replays now pay coins only.)
- **[2026-09-28] `StageDeploymentPlan.asset` overrides load times for stages 1-10** (Arash: Valkir3 6 /
  Minotaur_02 9 / Dark_Oracle_1 15 / CowMinotaur_2 18.5; 11-20 keep hero defaults 5.5/7/8/9). Resolution
  checked in editor; NOT yet seen in a played battle. `matches` left empty = random types unchanged.
- **[2026-09-12] Re-tuned weapon hitboxes** (all 15 characters, default in `Assets/Scripts/Editor/WeaponHitboxDefaults.json`).
  A MITIGATION for splash-kills, not the fix. Replay level 4, read `[HITS]`; if kills still show two
  attackers, the real fix is filtering a swing to `currentTarget` — a combat-feel change Arash has not approved.
  Undo: `Tools/Blasty/Weapon Hitboxes/Restore default widths`.
- **[2026-09-17] Hero "unhandled enemy first" targeting is at v3 and UNPLAYED.** v1 failed (heroes
  chose while locked on gates), v2 still failed (level 4 spawns in TWO waves, so the first
  deployment doubled up correctly then never re-balanced). v3 adds rule 1b `TryRebalance`.
  Judge from the `[TARGET]` log: `others≥1` persisting while another enemy is free = still broken.
- **[2026-09-17] The ×2 HP pass is unplayed; it makes every fight from LEVEL 4 ON take twice as long.**
  Intended (4 blows → 8). **Stages 1-3 are NOT part of it** — the 3 `Tutorial/` assets were doubled
  by mistake and reverted 2026-09-19; Arash authored 393/394/400 and they are off-limits.
- **[2026-09-16] UNASSISTED COMBAT IS COMPLETELY UNPLAYED — this is now the biggest unknown.**
  Every stage runs real combat for the first time. Play 1-5 and watch for what the old script hid:
  fights ending instantly (heroes outnumber enemies 12-v-5 at L5, and melee advantage is superlinear),
  or dragging forever now that nothing lifts a weak blow. Read the `[CP Battle]` and `[HITS]` logs.
- **[2026-09-16] Enemy growth is ~+22% CP/stage (≈44× by stage 20) and needs retuning to ~+6%.**
  `ProgressionConfigSO` (atk 8% × hp 10% × atkSpd 2%). Sizing: `U = g^N` for upgrades every N stages.
  Per-type curves need NO code — `progression` is per-prefab; make one asset per enemy type.
  Also open: **level-1 base HP is not calibrated** — author it so `EffectiveHP/ATK_opponent ≈ 8`
  (heroes are worst: ATK 64-96 on only 100 HP, so most fights end in 1-4 blows today), plus
  Arash asked for base `attackSpeed` −30% on every character (uniform, so R is unchanged).
- **[2026-09-12] Enemy target LOCK** (no retarget once acquired; fixes the spinning middle enemy).
  Play stage 4 or 5. If the lock looks stupid, the middle ground is `PlayerManager.retargetHysteresis`.
- **[2026-09-11] Melee side-approach** (`MeleeEngagement`, enemy movement → FixedUpdate, `UnitDepthSorter`).
  Enemies now move at true authored speed instead of ~0.83× — pacing may have shifted. Play `Level_1_Stage_1..5`.
- **[2026-09-11] Two enemy animator controllers had a DEGENERATE blend tree** — `Reaper_Man_01.controller`
  and `Zombie_Villager 1.controller` had both children at (0,0), so the walk clip played permanently and no
  code could stop it. Both fixed. **ASSET EDIT — will not read clearly in the diff.** Play stage 1-5.
- **[2026-09-06] New enemy tiers + per-stage waves** (`Assets/Scriptable Objects/Spawner/Stage_NN.asset`,
  5 of 6 enemy `UnitStatsSO` rewritten — they had been identical). Judge tier gaps, 7-12 units/stage,
  and whether Golem at move 2.9 breaks pursuit tuning built around 3.5. Table: `.../README_EnemyRoster.md`.
- **[2026-09-06] ALL 20 stages are `LevelTemplate.prefab` instances and NONE has been played.**
  Start with stage 3, spot-check 11 and 20. **Do not press "Apply All" on a stage instance** — it
  publishes that stage's board to every other stage.
- **[2026-09-06] LastStandOffer buy-back gate + one-purchase-per-card.** No purchase has ever been made.
- **[2026-09-06] Revive is off; the "straight to Lose" path is unplayed.** **Do not delete or disable the
  `ReviveManager` object — it IS the Lose panel's presenter.**
- **[2026-08-30] Mutual-wipe defeat.** `stalemateGraceSeconds` (2.5) never judged at a real frame rate.
- **[2026-08-25] Board drag rewrite** (`BoardInputController`, continuous anchor). Test on
  `Level_1_Stage_5.unity`; judge `settleDuration` (0.07) and `maxSubStepCells` (0.25).
- **[2026-08-24] HP-bar work has NEVER RUN** — not even compile-checked (Unity was closed).
  `HealthBar.KeepUnmirrored` + the yellow damage trail. Judge `trailHoldSeconds`/`trailDrainPerSecond`/`trailFadeSeconds`.
- **[2026-08-23] Summon arrival VFX + ground telegraph.** Edit-mode capture only. `StarterScene` has no
  `PlayerWaveManager` — test in a stage scene. `SummonPillar.vfx` does not exist, so `Auto` always resolves to Particles.
- **[2026-08-23] Reinforcement gate pose, hidden HP bar, 80% trigger, purchase, DOTween pulse — all unverified.**
  Re-play a WON stage to confirm the `HeroRoster` scene-load reset fixed the replay bug.
- **Heroes Stats panel: the WIPED look and the gem buy-back have never executed.** Building cells is confirmed.
- **`GroundProjected` shadow mode has never been run** (default is `StickToCharacter`).

### Registered defects — analysed, deliberately NOT fixed

- **[2026-10-04] Home BATTLE: the FIRST press of each Editor Play session freezes ~12.6 s (later presses ~1.2 s).** Cause: the Google Ads
  EDITOR placeholder banner (`ADAPTIVE(Clone)`, legacy `Text` with Best Fit, from the plugin's `Editor/Resources`) requested by the stage's
  `AdBannerSlot` - font cache 7.1 s + IMGUI skin 3.6 s on the asset lock. MCP A/B: AdManager removed -> 1.54 s. Editor-only. FIXED 2026-10-04 (untested): `AdBannerSlot.showTestBannerInEditor` (off) + BATTLE loads async. A black fade was REJECTED by Arash (no black screen ever) and deleted.

- **[2026-10-04] Tutorial → menu: the last ~0.5 s in the Editor is Unity's forced GC on a Single scene load — phone timing NEVER
  measured.** `Tutorial_Board_01` now HOLDS a MenuScene preload, which blocks every other scene load: keep that scene single-exit
  (or untick `TutorialTrigger.preloadSceneOnComplete`). Additive switch was rejected — see `TutorialTrigger.txt`.
- **[2026-10-04] Tutorial hand — two small defects found while fixing the blast replay, NOT fixed.** `TutorialHand.Release`
  ignores a new touch (press in the hand's last 0.2 s → it shows fading at the old destination); `TutorialRunner.ResolveMatchHint`
  aims at BOOKED cell centres, so with no-snap the hand can start up to half a cell off a stack resting between cells.
- **[2026-09-07] The CP system has 9 registered defects, none fixed.** Full analysis:
  `Docs/cp-analysis/CP_SYSTEM_ANALYSIS.md`. Worst two: `defPctByLevel`/`rangePctByLevel` in
  `PlayerProgressionConfig.asset` are authored at negative time and clamp flat (L20 hero has ×3.17 intended
  defense); and the menus apply 4 of 6 growth multipliers while `PlayerStatsApplier.cs:135-142` applies all 6.
- **[2026-09-07] `CPWeightMath.cs:41-42` never samples `meleeMultByLevel`** (line missing, `rangedMult`
  assigned twice) — **every `typeMult` in the game is 1.0**. Both flavour curves in `Player CP.asset` are
  also off-axis; re-drag them onto the level axis before editing or the edit appears to do nothing.
- **[2026-09-07] CP does not track combat strength and the menu "TOTAL CP" is fake.** `CP ≈ ATK + 0.15·HP`;
  the 128-DPS hero profile displays CP 81 while the 108-DPS profile displays CP 89. `MenuScene.unity:24724,36403`
  are literal TextMeshPro strings bound to no script. `CPCalculator.EffectiveHP` and `SquadCP` exist with zero call sites.
- **[2026-09-09] ⚠ A 2026-09-07 CP finding was RETRACTED.** "CP ranks the roster backwards" was wrong —
  it assumed `attackSpeed` multiplies DPS; it does not (cadence is a flat `recoveryTime = 0.6`).
  The proven defect is CP naming the wrong winner in 9.8% of 407 duels, always overrating the player.
- **[2026-09-09] CP redesign is designed but BLOCKED on Arash's Excel** of intended CP progression for
  levels 1-20 + two sawtooth numbers. Four changes specified in `Docs/cp-analysis/CP_DISCUSSION_LOG.md`.
  Two traps: naive movement wiring makes players 7× / enemies 15× faster; enabling `attackSpeed` at
  today's 2.0/1.5 makes heroes 1.5-2× stronger.
- **[2026-09-06] The hero roster is two stat profiles wearing eight costumes**, and three heroes read each
  other's stat assets (`Valkir3`+`PlayerFallen_Minotaur_01` share `Player_Minotaur_01`;
  `PlayerFallen_Angels_02` reads `PlayerValkir3`; `Golem_3` displays as "Minotaur_2"). `respawnGemCost`
  is 0 on all eight, so buy-back is free. Audited, not changed: `.../UnitDef-SOs/README_HeroRoster.md`.
- **[2026-09-06] Every enemy has `xpValue = 0`**, so the roguelite XP bar may never fill. Related: Roguelite
  XP has zero live call sites — `RogueliteManager.AddXP` / `NotifyEnemyKilled` are never called
  (`EnemyManager`'s call is commented out). Confirm whether this is intentional WIP or a lost wire-up.
- **[2026-09-06] `RogueliteManager.ShowSkillSelection()` pauses BEFORE it checks it has a panel**
  (`Assets/Scripts/Roguelite/RogueliteManager.cs:540`) — a scene missing that panel freezes the battle
  with nothing to dismiss it. Not currently biting; a landmine for anyone tidying "inactive" UI.
- **[2026-08-25] `BoardBootstrapper.cs:23` calls `AutoBuildOffsetsFromChildren()` unconditionally** —
  the `if` the comment describes is missing. It rewrites `shapeOffsets` without occupying the board and
  races `PieceSimple.Start`, so `TryPlace` can run with `pieceId == 0` and claim nothing.
- **`PlayerAttackState.cs:69` and `PlayerDeathState.cs:9` write `linearVelocity` on a Static body**,
  flooding the console every FixedUpdate. Two-line guard each — copy from `PlayerLockState.cs:22` (fixed 2026-08-22).
- **`PlayerStats` disagrees with itself about max HP** — `Start` passes `maxHealth`, `ApplyDamageToPlayer`
  passes `PlayerManager.statsBase.maxHP` (`PlayerStats.cs:14` and `:22`). `EnemyStats` is fine.
- **`WinPanel.RewardValues` is summed cumulatively across HP tiers** in `StageRewardCalculator` while
  `HomeManager.TryGetStageRewardPreview` passes hpCase=1 — the stage-card reward preview likely understates.
- **`UnitsPanelController.HandleDeploySave` likely swaps the highlight id arguments** post-swap.
  Cosmetic only; save data is correct.
- **`EnemyManager` target selection never got the hysteresis treatment** `PlayerManager` did. If enemies
  flip-flop left/right, that is where to look.
- **[2026-10-04] CONFIRMED live: "hero frozen in place, animation looping" = deploy jump skipped `Jump Loop`.** `FrogJumpTransformOnly`
  (in `SimpleJump2D.cs`) swaps to the loop only if a frame lands in [0.14 s, end) of a 0.28 s hop; one long frame skips it, the OverrideLayer
  stays in `Jump Start` (no exit -> `isInteracting` never reset) and `PlayerPursueTargetState:16` returns forever. FIXED 2026-10-04 (guard removed, loop always plays) - compiled, NOT play-tested.

### Open design questions

- **The board can empty BEFORE the move budget is spent.** Nothing signals it and nothing advances, and
  `PlayerWaveManager.WaveLoop` exits PERMANENTLY once the board is empty — no further hero waves that stage.
  Decide: refill the board, auto-start the battle, or hide the counter.
- **Heroes still LOCKED on the gates when BATTLE is pressed are stranded permanently** (the board is hidden,
  so no match can release them). `HeroRoster` deliberately does not count them (see Decisions). Whether to
  auto-release or refuse the BATTLE press is undecided.
- **[2026-08-23] `LastStandOffer` is wired but INERT and the scene was never SAVED.** `offeredUnit` has no
  `UnitDefinitionSO` assigned. Verify the whole wiring still exists before assuming it does; also check the
  stray `Image` on `LastStandOffer ` — an uncontrolled white sprite would sit on the HUD all match.
- `gemsPerHero` on `HeroStatsPanel` is a placeholder 50 (× squad size); reference art shows 200.
  Either set that or fill `respawnGemCost` per `UnitDefinitionSO` (which overrides it).
- **[2026-08-25] `Pink` (id 3) and `MidPink` (id 4) use the same sprite** (`#EF7CC1`) — the two block types
  are visually identical when they shatter. Art decision needed.
- **[2026-08-25] The shard-burst match VFX** has had one tuning pass, unverified in play since. Knobs:
  `shardsPerCell` (40), `shardSizeRange` (0.10-0.28), `sortingOrder` (20), Limit Velocity `dampen 0.42`.
  `FractureObject` is still in the scenes as the fallback and can be deleted once signed off.
- **Nothing sets `Application.targetFrameRate`.** On Android that can cap the game low. One line in a boot
  script rules it out — only chase it if drag still feels slow on a device after the rewrite is tested.

### Cleanup backlog

- `Assets/Scripts/_Legacy/` holds 16 quarantined scripts. Confirm each is truly unused, then delete.
- Data assets still live under `Assets/Scripts/` (`REGULITE/RogueliteScriptableObjects/`,
  `TowertDefenseScripts/Prefabs/` + `Test Prefabs/`, `UI/UI-SOs/`). Move them out of the script tree.
- Duplicate type names to disambiguate: `Piece` and `GameState` are each declared in two files.
- **[2026-09-01] Package Manager not re-checked since NavMeshPlus was removed** from `manifest.json` /
  `packages-lock.json`. Also dead: `using UnityEngine.AI;` at `Assets/Scripts/Combat/Player/PlayerManager.cs:3`.
- **The TMP Static bake is not proven end-to-end** — nobody has deleted `Library/` and confirmed fonts
  survive reimport. Re-run `Tools/Blasty/Fonts/Bake All TMP Fonts To Static` after adding any font.
- Duplicate `.ttf` with no live reference: `Lilita_One/LilitaOne-Regular.ttf` vs `LILITAONE-REGULAR.TTF`;
  `Dangrek/Dangrek-Regular.ttf` vs `Dangrek-Regular 1.ttf`.
- `Dangrek-Regular 1 SDF.asset` uses a 2048×2048 atlas for ~42 characters (~8 MB of hex in the repo).
  Shrink by hand in Font Asset Creator — the bake tool does not change atlas dimensions.
- **[2026-08-24] Optional: bake real `DelayedBar` objects into the 13 `HealthBar` prefabs.** The trail is
  cloned at runtime (see Decisions). Editor script written, never ran. Only worth it for per-prefab restyling.
- **The broken `shadowProgressCurve` is still serialized in all 12 character prefabs** (first key at time
  `-0.104`). `ShadowProgress01()` rejects it and falls back to t², so it is harmless — but clear the curve
  before re-enabling `GroundProjected` and wondering why authoring is ignored.
- **Per-script docs behind** for the 2026-08-21 scripts (`PlayerManager`, `CrowdSeparation2D`,
  `AttackSlotRegistry`, `EnemySpawner`, `BattleStartController`, `FormationGapFiller`,
  `PlayerPursueTargetState`) and the 2026-08-22 fixes (deliberate debt — Arash asked for no docs on those).
- A full per-script reference exists at `Assets/Documentation for scripts/` (101 `.txt` files). Read the
  target's doc before editing it blind — each lists dead numbered siblings, magic strings, and known bugs.
- `/feature-doc` was invoked with no feature named. Waiting on Arash to specify.

---

## Decisions

_Durable choices with their reasons, so no session reopens them blindly.
Reasoning in full: `SESSIONS-ARCHIVE.md`._

| Date | Decision | Why (short) |
|------|----------|-----|
| 2026-08-20 | Cross-session state lives in `SESSIONS.md` at the repo root; the **read** half is automated by a `SessionStart` hook, the **write** half by the `/wrap` command. | One git-tracked file readable by human and session. No hook event reliably means "session ending", and `Stop` fires after every response. |
| 2026-09-14 | `SESSIONS.md` is a **short index**; all long-form text moves to `SESSIONS-ARCHIVE.md`, which is not injected. Hard caps are written into the Protocol above. | The file had grown to 2814 lines / 233 KB and was being injected into every session — roughly 60k tokens of context spent before the first message. |
| 2026-08-20 | TMP font assets are **Static** atlas population with `ClearDynamicDataOnBuild` off. | Dynamic treats the glyph atlas as a rebuildable cache, so it does not survive a `Library/` wipe or a build. Exception: any font rendering Persian/Arabic or player-typed text stays Dynamic **with its Source Font File assigned**. |
| 2026-08-20 | The TMP conversion is a repeatable editor tool (`TMPFontAssetStaticBaker.cs`), not hand-editing or YAML patching. | Only TMP's `TryAddCharacters` can repopulate an atlas, and it needs a loaded font face. The tool re-runs safely on correct assets. |
| 2026-08-21 | **Units have ZERO physics interaction.** Look-ahead steering around allies + a small personal space that runs only while walking. | Continuous separation push was built and rejected TWICE — it shoved heroes away from enemies they were trying to hit. A unit that has stopped must be left completely alone; steering prevents overlap rather than correcting it. |
| 2026-08-21 | **An attack spot must ALWAYS be within `maxAttackRange` of its target** — attackers fan out on an ARC, never a flat sideways offset. | `PlayerPursueTargetState` measures distance to the TARGET while the mover walks to the SLOT. A flat `+0.90` offset deadlocked units: mover said "arrived", state said "too far". |
| 2026-08-21 | **Every "which is nearest / which side" decision needs hysteresis or a deterministic tie-break.** | The same bug appeared FIVE times (spinning units, two units picking the same side because `0 > 0` is false for both). Margins compare in SQUARED space — square your linear margin. |
| 2026-08-21 | **Battle-gate flags default to "ungated"** (`waitForBattleStart = false`, `BattleIsRunning = true`, `EnemiesHaveAppeared = true`). | Stages 1-20 predate the puzzle-first flow; defaulting to gated would have silently frozen all of them. Presence-based gating means new scenes opt IN. |
| 2026-08-21 | AdMob + EDM4U install from **git URLs**, not Google's scoped registry. | `unityregistry-pa.googleapis.com` was unreachable from this machine. If the registry is restored, drop the EDM4U git url — do not keep both. |
| 2026-08-22 | **A render-order override that exists for combat must be scoped to combat.** `HealthBar` captures its canvas's AUTHORED sortingOrder and restores it outside `GameState.Playing`. | `sortingOrder = 500` fixed bars hiding behind units but outranked everything forever, punching through the win panel. Restoring the AUTHORED value is provably the config that worked before. |
| 2026-08-22 | **`LevelGameManager.OnGameStateChanged` is the authoritative "is the battle over?" signal.** | The battle ends when a GATE hits 0 HP, and revive RESUMES combat — any listener keying off one panel or a one-way bool gets revive wrong. |
| 2026-08-22 | **Jump shadows stay glued to the character** (`ShadowJumpMode.StickToCharacter`); ground-projected is kept as opt-in. | Arash's call, flagged provisional. An enum default also meant zero prefab edits — the 12 prefabs fall through to the C# field initializer. |
| 2026-08-22 | **A view component must DECLARE its visual states, never capture whatever the scene was authored with.** `HeroStatCell.aliveTint` is an explicit field. | The capture version shipped a bug within the hour: `Hero avatar` was authored blue for a placeholder, so every LIVING hero rendered as a dark blue silhouette. |
| 2026-08-22 | **`HeroRoster` counts a hero only once `PlayerManager.isUnlocked` is true.** | Heroes spawn onto gates LOCKED and can be stranded there. Counting them would pin `alive/total` above zero forever, so the buy-back button (which only appears at 0) could never be reached. |
| 2026-08-22 | **Battle-phase UI choreography lives in `BattlePhaseTransition`, not in each panel.** | Only it knows when the camera has arrived. Consequence: a panel in `fadeInAfterMove` must be left INACTIVE in the scene, and `hideButtonAfterBattleStarts` had to go OFF. |
| 2026-08-23 | **A per-scene trigger arms on an EVENT that fired in this scene — never on a static flag, and never on the ABSENCE of static state.** | Both cheaper options were the same bug: static `HeroRoster` state survived a scene reload so the offer appeared during the puzzle phase, and `BattleIsRunning` would have failed identically (it DEFAULTS TO TRUE). An event cannot lie about its scene. |
| 2026-08-23 | **Reinforcements reuse `PlayerWaveManager.SpawnReinforcements` verbatim; new callers add a trigger and a look, never a second arrival path.** | Five coupled behaviours bottom out in one call, so one fix fixes everywhere. Note `SetAlive(0)` was NOT reused for the offer look — it greys the portrait, right for "wiped out", wrong for a purchase prompt. |
| 2026-08-24 | **A "keep me upright" correction measures the WORLD AXIS of the thing it corrects, never a parent's `lossyScale`.** | `lossyScale` is blind to a 180° rotation. Enemy prefabs use a mirrored root plus three 180° Y rotations and two negative scales, so a parent-sign correction was a fifth inversion on top of four. |
| 2026-08-24 | **The damage-trail Image is CLONED from the main bar at runtime**, not authored per prefab; `delayedBar` stays serialized so a hand override wins. | Cloning inherits the enemy prefabs' hand-authored mirroring for free, reaches the castle-gate bars that live in 20 SCENES rather than a prefab, and shipped with the Editor closed. |
| 2026-09-06 | **A stage scene is ONE `LevelTemplate.prefab` instance plus that stage's board pieces.** The board stays a per-instance override. | A prefab cannot serialize a reference to a scene object, and this wiring crosses every boundary a split would draw — `LevelTemplate_UI` + `_World` would have nulled those arrays silently. **"Apply All" on an instance is destructive.** |
| 2026-09-06 | **Stages 2 and 3 adopted Stage 1's world layout wholesale**, keeping only their own board *contents*. | Arash's rule, and Stage 1's base distance had just been retuned. Piece positions are local to `BoardBG`, so moving the board preserves which cell every piece occupies. |
| 2026-09-19 | **Stages 1-3 are excluded from every campaign-wide stat pass. Tutorial `maxHP` (393/394/400) is Arash's and off-limits.** | He tuned and play-tested it; the 2026-09-17 blanket ×2 HP pass swept it up and pushed the tutorial from 6-7 blows per kill to 12-14 — wrong feel for the first three stages. A "uniform change to every character" must stop at level 4. |
| 2026-09-22 | **Saving is UNCONDITIONAL and has no user-facing switch.** A "fresh run" means an actually empty save, produced by a wipe — never by a gate that makes the game ignore what is on disk. | Arash's call. A gate that silently ignores a real save is a trap: it hid stage-11 progress all through the 09-20/21 balance passes, and it shipped as a release blocker that had to be remembered. The wipe is honest — what you see on disk is what the game will load. Testing need is served by the editor-only `resetProgressOnPlay` tick, which cannot exist in a build (`#if UNITY_EDITOR`). |
| 2026-09-22 | **The first-run tutorial is gated ONLY by its save flag (`board_basics`), never by a serialized on/off bool.** | `MenuLoader.enableFirstRunTutorial` sat `false` from 2026-08-31 and silently removed the tutorial from the game for three weeks. A bool in a scene can be stale; the save flag cannot lie about whether the player has seen it. |
| 2026-09-19 | **Tutorial enemy ATK is authored as a SHARE OF HERO HP PER 8 BLOWS, never as an absolute number.** Type 1 (Reaper) 25%, type 2 (Zombie) 30%. | Arash's spec. Absolute ATK silently breaks whenever hero HP moves — doubling hero HP left an enemy blow at 0.54% of it. Re-derive with `baseATK = share × heroMaxHP × (1 + heroDEF/100) / (8 × atkGrowth(stage))`, sized on the 562/DEF-25 hero (the profile taking the most *percentage* damage) so the other lands under the cap, not over. |

---

## Session Log

- **2026-10-05** — **Units-menu portraits now framed by visible body, not PNG canvas**: new `PortraitFrame` (UI/Common) used by `UnitCardView` (MyCard → new PortraitMask/RectMask2D) + `BucketStatRow` (StatsRow-VS1 MaskImage); 2 Minotaur portraits set to Tight mesh; StageTounlock label 196 px + auto-size. Numbers checked in editor (all bodies same height, centred); NOT play-tested.
- **2026-10-05** — **Heroes 1.1× on stage → 1.0× landed** (`FrogJumpTransformOnly`: new `stageScaleMultiplier`, `landedScale` replaces `landedScaleMultiplier`, absolute vs authored size so later jumps don't shrink). **Dark_Oracle_3 invisible-hero bug fixed** at source `Vector Parts_Dark_Oracle03/Dark_Oracle.prefab`: 10 blank sprites + transparent staff + empty face `TextureController.Sprites` copied from the identical `Dark_Oracle_3.prefab` rig. SimpleJump2D.cs, HealthBar.cs (+docs). Play-mode verified (spawn 1.1, land 1.0, 2nd jump 1.0, DO3 renders with face); NOT checked in a full level.
- **2026-10-05** — **Same HP-bar gap above the head for all 15 character prefabs**: bar root y = measured head top + 0.201 (the zombie's preview-matched gap) → 1.303–1.319. Was 0.125–0.272. Checked in an edit-mode render (deck heroes + 6 enemies; bars within 1 px, Golem_01 3 px lower because its art sits lower). NOT play-tested.
- **2026-10-05** — **All 14 characters equalized to Player_Minotaur_02's height (1.117 u, 286 px @256ppu)**: visual child (Animator node) scaled uniformly by k; weapon collider objects scaled 1/k so world size + hand-bone anchor are unchanged; HP bar y shifted by head-top delta; 8 `UnitDefinitionSO.uiVisualScale` set to (150/0.27)×visual scale. Prefabs in New Characters/ (+Player_Pref) and UI-SOs. Pixel-measured in edit mode (gameplay + UI art all 286 px, Golem_01 287); NOT play-tested. Heroes still land at 0.9× (`landedScaleMultiplier`) - not touched. `Player_Dark_Oracle_3` gameplay prefab has 10/12 null sprites (pre-existing, invisible in game).
- **2026-10-05** — **Hero HP bar now the same size as the enemy's.** Heroes land at 0.9x (`landedScaleMultiplier`), so their bar was 90% size; `HealthBar.KeepConstantWorldSize` (new `keepConstantWorldSize`, default on) holds the bar's authored world size whatever the unit's scale. `HealthBar.cs` (+doc). Verified in Play mode with a 0.9x test hero (0.588x0.161 both); NOT checked in a real battle.
- **2026-10-05** — **Tutorial tooltips use Gameplay_Gem-holder_H3P** (5-piece layout: caps + centre-with-tail fixed, 1-px strips stretch, so the tail never widens; flips when below target) - `TutorialTooltip.cs` (+doc), TutorialOverlay.prefab gets a saved Tooltip, texture re-sliced (original `_0` sprite + ID kept for LevelTemplate). **Stage-6 onboarding now fires on direct play too**: `requireUpgradeAffordable` OFF in Level_1_Stage_6 (resetProgressOnPlay left the run broke) - SCENE EDIT. Edit-mode verified; NOT play-tested.
- **2026-10-05** — **Arrival capsule on a hero's first stage appearance**: play-tested Stage_6 via MCP (SendMessage HandleBlast x3 + StartBattle) - capsules spawn and render on the first appearance (screenshot), stages 0/2/3 correct for 3 cards. Hardened anyway: `GateArrivalCapsule.Update` clamps dt to 1/30s so the heavy all-heroes-at-once frame can't skip the effect (+doc). NOTE: play mode alternately routes to Tutorial_Board_01 on quick restarts.
- **2026-10-05** — **Hero stage fixed per card + 0.3s empty gap**: card panel only appears after the 1.1s camera pan, so the 1s wait fell back to centre stages and heroes later moved; now `StageLane.home` is chosen once from the cards (wait ≤ `cardWaitTimeout` 4s) and kept; `restageGap` 0.3s after landing. `PlayerWaveManager` (+doc). Compiles; NOT play-tested.
- **2026-10-05** — **Heroes now wait ON their stage for the whole card load**: first of each type appears at BATTLE (arrival capsule), leaps the instant its load fills, next appears once it lands. `PlayerWaveManager` StageLane/ServiceStageLanes + `StageHeroes` (gateQueue/pendingDeploys removed; staged heroes stay isUnlocked=false), `HeroDeploymentSequencer` stages at build, `HeroStatsPanel.HasDeployCards` (+3 docs). Compiles; NOT play-tested.
- **2026-10-05** — **Deploy stage no longer random: each hero spawns on the stage in front of its own HUD card** (ordered screen-X match; waits for its own stage if busy; 1→stage 2, 2→1,2, 3→0,2,3, 4→all). `PlayerWaveManager` (PickDeployStage/ResolveHomeStage, gateQueue→List), `HeroStatsPanel.GetDeployCardScreenXs` (+docs). Compiles; match simulated on Level_1_Stage_1 geometry; NOT play-tested.
- **2026-10-05** — **Enemy HP bars use their own art set** (`Player HP/ٍEnemy HP/`): frame / red loader (Fill) / InstantDamage (Hit) swapped on all 10 EnemyManager prefabs, player mask kept; loader+InstantDamage cropped to (3,2,53,12), pivot centre, border 26, same recipe as the heroes. Zombie bar y 1.212→1.253 to match the preview gap. HealthBar.txt updated. Checked in an edit-mode capture; NOT play-tested.
- **2026-10-05** — **Hero arrival capsule rebuilt 1:1 from Hero Appearance animation/Preview_mockup.png**: new `GateArrivalCapsule` (sprites, not particles; width = stage cap, bottom on cap rim, top mid-head, 4 stars at mockup spots rising WITH their tails, star 2 tail-less); `PlayerWaveManager.PlayGateArrivalVfx(pm, gate)`; new prefab VFXKit/hero_arrival_capsule wired in LevelTemplate (ASSET EDIT); 8 new sprites → Single import (Main Glow was auto-sliced in half). Pass 2: glow drawn SLICED (23 px bottom border) so its rounded bottom = the cap's front-rim half-ellipse; hero "Shadow" shrunk to 57% of cap during the stand only. Pass 3: 2-phase timeline ~1.3 s — glow + bare stars appear, then glow lifts off/rises/fades while stars climb and grow tails; capsule outlives the 0.5/0.6 s gate hold. Pass 4: hero shadow kept UNDER the capsule (lifted inside the glow base + sorted below hero), restored at the leap. Pass 5 (CapsuleFlame shader reveal/burn) REJECTED + deleted. Pass 6: glow = landing pillar from `Arts/Reference videos/VFX ANIM` — shoots up 0.14 s, then (pass 7) lifts off and rises like a flame (top outruns bottom, thins, flickers, sways) while fading, ~0.95 s; pass 8: climb cut + lower parts fade first via new alpha-only `CapsuleRiseFade.shader`; pass 9: last wisp climbs a bit higher (riseBottom/Top 0.4/0.95, fadeStart 0.72); colour never touched. Then: heroes spawn at the CENTRE of the stage circle (`PlayerWaveManager.GetStageStandPoint`, standFromCapTop 33/82, +0.24 on 1-1) and the stage's old "Throw" Animator clip on Top_0 fires again on the same frame as the leap (`PlayStageThrow`) - NOT play-tested. Old VFXKit/stage_capsule now unreferenced. Edit-mode frames only; NOT play-tested.
- **2026-10-05** — **Battle hero card "card  Button" rebuilt to preview_Battle_Mana.png**: Crafting loader-frame/loader(Filled L→R)/gloss at exact slice rects, separate "DEPLOYED" text (count hidden, never "0/N"), coloured frame kept, white + deployed gloss sprites (linear-space alpha fit). `HeroStatCell.cs` (+doc), PREFAB + SCENE (Level_1_Stage_1) EDIT, 2 new PNGs in Crafting-Button/. Pixel-diffed vs preview in edit mode; NOT play-tested.
- **2026-10-04** — **Board tutorial: (1) hand no longer replays a just-blasted pair** (read the board on the release frame, 0.1 s before `MatchResolver`; new `BoardInputController.IsSettling`/`TutorialRunner.BoardIsSettling`); **(2) tutorial → menu 6.4 s → ~1.4 s (Editor)**: `TutorialTrigger` preloads MenuScene, `GameStartManager` starts DOTween at boot, 'Nice!' 0.5 s + `loadSceneDelay` 0 (ASSET + SCENE EDIT). MCP-measured; Arash's check pending. (4) ComingSoonScene after stage 10 + stages 11-20 unticked - screenshot-verified, win path unplayed. (3) Stage-arrival capsule VFX (`PlayerWaveManager.gateArrivalVfx` -> VFXKit `stage_capsule`, from Arts/Stage VFX sprites); gate holds 0.5/0.6 s (ASSET EDIT) - NOT play-tested.
- **2026-09-29** — **Hero-per-match retune 3-10** (`LevelBattleRules.Deployments`) + deploy timers → 5/7/11/13.5 s in `StageDeploymentPlan.asset` (ASSET EDIT). Editor-verified; NOT play-tested. _(full: archive, 2026-10-04 trim)_
- **2026-09-28** — **Base deadlock fixed**: an enemy at the player base now steps toward a hero it can't reach (`EnemyLocoMotion`, `baseApproachLeash` 1.5). MCP-verified stage 6. _(full: archive, 2026-10-04 trim)_
- **2026-09-27** — **Deployment = parallel per-type timers** (`HeroDeploymentSequencer` rewritten; `PlayerWaveManager.DeployOne`); `deployInterval` ASSET EDITS. MCP play-tested stages 4/6/11. _(full: archive, 2026-10-04 trim)_
- **2026-09-26** — **Board "gap / shake, never match" fixed by BODY COLLISION** (`BoardInputController` collides with real transforms; no snap anywhere). MCP-verified Level_1_Stage_2; NOT hand-played. _(full: archive, 2026-10-04 trim)_
- **2026-09-25** — **Onboarding upgrades all 4 deployed heroes** (`Tut_Onboard_UnitsUpgrade` 13 steps, `ifHeroNotUpgradable` guard, `TutorialRunner.FinishSequence`). Play-verified in OnBoarding Test; guard paths NOT. _(full: archive, 2026-10-04 trim)_
- **2026-09-25** — **Enemy attack VFX, stages 1-10**: `EnemyAnimatorManager` plays an optional `HeroAttackVfx` (Reaper, Zombie villager, Skeleton_Crusader_1). Play-tested Stage_6 + Stage_1. _(full: archive, 2026-10-04 trim)_
- **2026-09-25** — **Hero attack VFX for the rest of the ref clip**: `oracle_slash`, `cow_slash`, `mino_slash` (+ `candy_slash`, `ember_arc`). 4 heroes MCP-tested 1v1 in Stage_1. _(full: archive, 2026-10-04 trim)_
- **2026-09-25** — **Valkyrie attack VFX v3**: arc-only, re-read from the ref frame by frame (timing table in `make_arc.py.txt`); v3.1 added `sparks_start`/`sparks_hit` back. _(full: archive, 2026-09-27 trim)_
- **2026-09-25** — **`UnitUpgradeFx` v4** rebuilt off the SECOND upgrade with measured luminance curves. Edit-mode side-by-side only; **NOT play-tested**. _(full: archive, 2026-10-04 trim)_
- **2026-09-25** — **`UnitUpgradeFx` rebuilt after v1 was rejected**, frame by frame against the ref (rules in `UnitUpgradeFx.txt`). Edit-mode only; test with Play stopped. _(full: archive, 2026-10-04 trim)_
- **2026-09-25** — **Hero-upgrade juice** from the onboarding ref: new self-bootstrapping `UnitUpgradeFx` plays on every successful upgrade (pillar + flash over the hero, per-stat flare). _(full: archive, 2026-09-27 trim)_
- **2026-09-24** — **Onboarding juice, measured from the ref**: spotlight dim (`dimAlpha 0.42` on all 5 steps), gold halo, tap burst, tab pop — new `TutorialFocusFx` (UI Images, not particles). _(full: archive, 2026-09-27 trim)_
- **2026-09-23** — **Guided onboarding built** (tooltip + finger on ONE element, rest dead): lose stage 6 → LEAVE STAGE → Units → first card → Upgrade → Back. New `TutorialFocusGate`. _(full: archive, 2026-09-27 trim)_
- **2026-09-23** — **Base HP authored for stages 1-10 at ≥4× the hits to kill one unit. SCENE EDITS ×10** (enemy base had been 350 flat; player base fell back to 500 for 6-10). _(full: archive, 2026-09-27 trim)_
- **2026-09-23** — **Hero gate pose cut 1.20 s → 0.30 s. ASSET EDIT (`LevelTemplate.prefab`)** — Arash: the hero read as stuck on the turret. _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **HUD chip overlap fixed: the three chips sat on three different anchors. ASSET EDIT (`LevelTemplate.prefab`).** _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **HUD resource chips hidden for the whole stage, revealed on the win** (`HudCurrencyView.resourceVisibility` Auto; `RevealResourcesForWin` from `WinPanel.Show`). _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **New player owns NOTHING; hero XP + gems first-clear only; Home "TOTAL CP" = real per-stage enemy CP** (`GameStartManager.FreshStart*` = 0). _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **Units menu cost rows fixed** ("1/6/200": the hero-XP row's white label was never wired), K formatting added, locked-hero rows hidden. _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **Save/load switch DELETED; saving is unconditional; the Tutorial is back as the first-run detour** (`SavePersistence*` removed). _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **Nearest-claimant lock: a hero only hands a fight to someone CLOSER** (`TargetClaimRegistry.IsNearestClaimant`). _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **An ENGAGED hero never leaves its enemy** (engagement lock in rule 1b, `PlayerManager.IsEngagedWithTarget`; cause was `TryRebalance`). _(full: archive, 2026-09-27 trim)_
- **2026-09-22** — **Stages 7-10 re-authored**: only the 3 stage-6 enemy types, new wave splits, 12 new per-stage enemy assets (`_L7`…`_L10`), CP pinned to reference ratios. _(full: archive, 2026-09-27 trim)_
- **2026-09-21** — **Upgrade costs are AUTHORED INTEGER TABLES** (`UpgradeCostSO.authoredCoinCosts` 50, 80, 200, …) so one hero can't eat the whole economy. _(full: archive, 2026-09-27 trim)_

_Entries older than the ones above are in `SESSIONS-ARCHIVE.md` — grep it for the date. Trimmed to the last 25 on 2026-09-22, 2026-09-27 and 2026-10-04 (long entries shortened, full text archived verbatim under "trimmed from SESSIONS.md on <that date>"); nothing was lost._
