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

- **[2026-09-12] Stages 8-20 are MISSING from Build Settings — progression past stage 7 is
  expected to fail in an Android build.** Rows point at nonexistent `Assets/Scenes/Level_1_Stage_N.unity`;
  real scenes are in `Assets/Scenes/TestScenes/GamePlay Scenes/`. Fix tool exists but has NOT been run:
  `Tools/Testing/Enable All Level Scenes In Build Settings`. Editor direct play is unaffected.
- **Ads release blockers (2026-08-21), all still set for testing:** `BattleStartController.doNotPersistAllowance`
  must go OFF; `AdManager.useTestIds` OFF with real unit ids; `GoogleMobileAdsSettings.asset` still holds
  Google TEST app ids; Android `applicationIdentifier` is UNSET. **Nothing has ever run on a device.**
- **[2026-09-20] `SavePersistence.Enabled` is OFF — save/load is gated off for balance testing.**
  Every Play run boots stage 1 / 0 resources; nothing is written to disk. Must be ON before any
  release build (release players are hard-wired ON regardless). Flip: in-game panel, or
  `Tools > Save System > Save & Load Enabled`. Replaces the old `resetBool` boot-wipe thread (gone in c94a908).
- **[2026-09-07] Three features are OFF and must be switched back ON next version.**
  `Assets/Scripts/Core/GameFeatureFlags.cs` — `RogueliteEnabled`, `LastStandOfferEnabled`,
  `HeroBuyBackEnabled` all `false`. Nothing deleted. Re-enabling `LastStandOfferEnabled` alone also
  needs its `requireBuyBacksSpent` turned off.

### Never played — compile / edit-mode verified only

_Each of these compiles clean and has NEVER been in Play mode. Detail + what to judge: archive._

- **[2026-09-14] PHASE 2 card rewards — the whole animation is unplayed.** Only the fan's REST pose
  was posed and screenshotted in edit mode. The fly-in, flip, collapse, white flash, teal glow, fade,
  all timings, and the anchor's real position above the player base have never run. `HeroCardRevealDirector`.
- **[2026-09-14] PHASE 2 deployment queue is unplayed.** After BATTLE, `HeroDeploymentSequencer` runs
  one 6s load per match, then spawns that batch to the rear lane; `HeroStatsPanel` became the queue
  (portrait + "xN" + cyan fill). None of it has run. 4 matches = 24s before the last hero lands.
- **[2026-09-14] `StageDeploymentPlanSO` asset does not exist yet.** Until one is created
  (Create ▸ Blasty ▸ Stage Deployment Plan) and assigned on `[PlayerWaveManager]` in
  `LevelTemplate.prefab`, every level uses the random-type fallback. The authored path has never run.
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
- **Unconfirmed:** "back row plays its walk animation but stays in place" mid-battle. Not reproduced; the
  proposed pursuit-vs-personal-space cause is probably wrong. Likely `PlayerPursueTargetState` calling
  `SetAnimMoving(true)` after `HandleMoveToTarget` already zeroed velocity. Catch it live.

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
| 2026-09-19 | **Tutorial enemy ATK is authored as a SHARE OF HERO HP PER 8 BLOWS, never as an absolute number.** Type 1 (Reaper) 25%, type 2 (Zombie) 30%. | Arash's spec. Absolute ATK silently breaks whenever hero HP moves — doubling hero HP left an enemy blow at 0.54% of it. Re-derive with `baseATK = share × heroMaxHP × (1 + heroDEF/100) / (8 × atkGrowth(stage))`, sized on the 562/DEF-25 hero (the profile taking the most *percentage* damage) so the other lands under the cap, not over. |

---

## Session Log

_Newest first, **one line each**. Full write-up of any entry: search `SESSIONS-ARCHIVE.md` for its date._

- **2026-09-21** — **Every non-tutorial enemy raised to 1.80x CP** (heroes don't scale with stage, only with upgrades, so an unupgraded player met stage 6 with the stage-4 hero). ATK x**1.80^0.60=1.422864**, HP x**1.80^0.40=1.265054** (Arash: attack carries 60% of the rise), DEF and atkSpd untouched — exponent split, so the product is exactly 1.80 and **all enemy-to-enemy ratios are preserved** (Skeleton Swordsman still 1.200x Zombie). 7 assets: Reaper, Zombie, Orc, Skeleton_Crusader_1, Skeleton_Swordsman, Golem_01, Golem_02; **`Tutorial/` (L1-3) NOT touched**. Stepped 1.30x → 1.70x → 1.80x; 1.30x moved the hero only 9.6→8.2 blows: **60% of a 30% CP rise is just +17% ATK, and enemy HP does not affect how fast a hero dies** (`DamagePerHit = ATK x 100/(100+DEF)`, no HP term). Now ~**6.7-8.0 blows / 9.6-11.4 s** at L6, and **stage 6 finally beats a full unupgraded roster** (10 heroes 1164.7 vs 1192.3) (`SafetyBlowFloor=4` is the hard floor). Stage totals 4:697 · 5:967 · 6:1192 · 7:1512 · 8:1618 · 9:2200 · 10:2508. **`Docs/balance/*` CP figures and `latest-playmode.csv` are now stale by 1.80x.** ASSET EDITS — invisible in diff. Edit-mode verified; NOT played.
- **2026-09-21** — **Enemy spawn formations are now CENTRED, spacing capped to the box.** Two bugs: the `Stage_NN` `spawnMin/Max` are DEAD (template has `spawnRelativeToEnemyGate`, live box is 6 wide, not 27), and `GenerateGridPositions` left-aligned + clamped, so 3 enemies put two on ONE spot. Standard now: 2 → ±1.5, 3 → −3/0/+3, spacing 3. Also `Stage_04` spacing 9→3 and template spawn line raised 2 units. **4-enemy waves (L9/L10 w2) compress to 2-unit spacing.** Edit-mode verified; NOT played. Archive: this date.
- **2026-09-21** — **Stage 6 = 5 enemies (2+3): 1 Reaper, 2 Zombie, 2 NEW `Enemy_Skeleton_Swordsman` (both last wave); Orc moves to L7.** Skeleton CP **149.7 = 1.200×** Zombie, atkSpd matched to the others; new asset so L9+ Crusader untouched. Fixed `CopyWaveWithTotal` overwriting authored per-type counts, and `RunLevel` now uses `ResolveLevel` like `StartPreparedBattle` (stage drift = likely cause of the "4 enemies"). New `Tools > Testing > Report Stage Composition`. **OPEN: "enemies always win unupgraded" NOT met — needs ~+76% CP, not +20%.** NOT played. Archive: this date.
- **2026-09-20/21** — **Save/load gated OFF behind new `SavePersistence` (default off, own PlayerPrefs key, hard-wired ON in release builds).** Every run boots stage 1 / 0 resources; nothing written to disk. Toggle: auto-spawning `SavePersistenceDebugPanel` (**menu scenes only**) or `Tools > Save System`, which also wipes the save the gate merely ignores. Cause was c94a908 removing the `resetBool` boot wipe. Also: stage 1 now pays **1 Hero XP** (`EarlyHeroXp[0]` 0→1; upgrade milestones unchanged, coins still bind at stage 5); economy-verification asserts updated. NOT played. Archive: this date.
- **2026-09-19** — **Tutorial enemy ATK re-authored so the hero actually takes damage in L1-3.** Doubling hero HP had left an enemy blow at 3.02 vs 562 HP (0.54% each, ~93 blows to kill). Now sized to Arash's spec — one enemy removes 25% (type 1 Reaper) / 30% (type 2 Zombie) of hero HP per 8 blows: `Enemy_Reaper_Man_01_Tutorial` 3.78→**20.3462**, `_Tutorial_L3` 2.80→**18.8747**, `Enemy_Zombie_villager_Tutorial` 2.80→**22.6497**. ATK only — HP/DEF/AtkSpd untouched, and **level 4+ assets untouched**. Lands 23.17%/25%/25%/30% (L1/L2 Reaper, L3 Reaper, L3 Zombie) on the 562-HP hero; L1 is under because one asset serves both L1 and L2 while ATK grows per stage. **NOT play-tested.**
- **2026-09-19** — **Two mistaken changes REVERTED.** (1) The 3 `Tutorial/` enemy assets are back to Arash's 393/394/400 — the 2026-09-17 ×2 HP pass should never have touched stages 1-3 (see Decisions); L1-3 verified back at 6/7/7 blows per kill, stage 4 unaffected at 8. (2) `PlayerManager.IsEngagedWithTarget` and its guard in `TryRebalance` removed — added while Arash was only ASKING a question. **The engaged-lock is therefore NOT in the code: a hero that is already attacking can still re-balance away.** Un-played either way.
- **2026-09-17** — **Hero targeting: an UNHANDLED enemy now beats a closer one, and a live target is never abandoned.** Fixes a play-reported case — hero A walks at the left enemy, hero B spawns nearby and goes for the SAME enemy, while the right enemy walks into the player base unopposed. New `TargetClaimRegistry` (Combat/Shared): a hero's claim IS its `currentTarget`, ranking is fewest-other-claimants → nearest → instance id, `self` excluded from its own count or two heroes trade targets forever. Rewired `PlayerManager.UpdateTargetSelection` (both phases + sticky early-out) and `TargetDetectionForPlayer.AcquireBestEnemy` (renamed from `AcquireNearestEnemy`). **DELETED** with the re-pick they guarded: `PlayerManager.retargetHysteresis`, `TargetDetectionForPlayer.allowNearestSwitch`/`onlySwitchIfInFront`/`retargetHysteresis`, and the dead `EnsureTarget1`. `IsTargetValid` lost its radius test (a hero is now sent across the field on purpose) and gained an `enemyIsdead` test. **FAILED its first playtest, cause found and fixed — rule 0: a LOCKED hero does not choose.** `Update` ran target selection from instantiation, ~1.2s before `UnlockCurrentWave` releases the wave, while `IsHandling` ignores locked heroes — so a whole wave read "nobody is on anything", all picked the same nearest enemy, and stickiness froze it. Both acquisition paths now return early while `!isUnlocked`. Added `[TARGET]` log (`TargetClaimRegistry.LogPicks`, ON). **That log then proved v2 was STILL wrong, and why:** level 4 spawns enemies in TWO waves, so the first deployment doubled up on the only enemy alive — correct at the time — and stickiness froze it while wave 2 walked in unopposed. **v3 adds rule 1b `PlayerManager.TryRebalance`:** a hero leaves a SHARED target only for one with STRICTLY fewer other claimants, ≤ twice a second; strict-improvement + live self-excluded counting makes it a strict descent, so it provably cannot oscillate. Also fixed: first-detection used a bare `GetComponent<EnemyStats>()` on the overlap hit (now falls back to `GetComponentInParent`, matching `IsCandidate`). Compiles clean via Unity MCP; **NOT play-tested.**
- **2026-09-17** — **Base `maxHP` DOUBLED on every live combat unit + `CPCalculator.DisplayDivisor` 190→380, so displayed CP is unchanged.** Everything at the level-4 base state was dying in 4 blows (the hero's blow was hitting `SafetyBlowFloor = maxHP/4`, so "4" was the clamp, not the stats); ATK untouched, one uniform +100% HP instead. 14 assets: 8 hero + `Enemy_Reaper_Man_01`/`Enemy_Zombie_villager` + Orc/Skeleton/Golem_01/Golem_02. **The 3 `Tutorial/` assets were doubled too and then REVERTED on 2026-09-19 (393/394/400) — Arash authored those values, they must not be touched; L1-3 are back at 6/7/7 blows.** Level 4 now 8 blows for a hero to kill either enemy, 10 for an enemy to kill a hero; CP still 116/121/92/101. Both sides scaled equally ⇒ **no ratio, ordering or outcome moved, only fight DURATION (×2)**. **ASSET EDITS — read as noise in the diff. NOT play-tested.**
- **2026-09-17** — **Full hand-off for the CP rework: `Docs/cp-analysis/SESSION_2026-09-16_CP_REWORK.md`.** Read it before touching CP, combat stats or levels 1-4. **⚠ LEVEL 4 IS DEADLOCKED** — enemies were moved to x=±4.5 but `fairDistanceToPlayer=2.0`, so nobody acquires a target (measured: 47s, 0 blows). Arash must pick: raise engage range to ~5 / move enemies to ±2 / per-prefab override. Also in there: hero ATK 90 + HP 281-291, first-strike rule, tutorial enemy values, and the finding that **integer blow counts erase any advantage under one whole blow** (4.05 vs 4.52 swings both round to 5, so a real 12% edge was worth nothing).
- **2026-09-16** — **FIXED: no hero ever deployed if BATTLE was pressed within ~0.75s of the last match.** `HeroDeploymentSequencer.RunQueue` snapshotted `EarnedBatches` at BATTLE press, believing `SealForBattle` made it final — it does not; `PlannedWaveLoop` awards a batch only after `nextWaveDelay` (0.75s). Empty snapshot → `TotalLoads=0` → coroutine yield-breaks → **zero heroes, enemies walk an undefended base, automatic loss.** Reproduced live on stage 1 (EarnedBatches=1, TotalLoads=0, gate 500→458). Now waits for `MatchesReleased >= MatchesCleared` before snapshotting, bounded by new `awardWaitTimeout` (5s) with a warning on timeout. NOT a timeScale bug — the 6s load uses `Time.deltaTime` and scales correctly.
- **2026-09-16** — **Heroes now move at ONE constant speed.** `PlayerManager.marchSpeedBoost` (1.3) and `MarchSpeed` DELETED; `HandleRoamForward` and `HandleMoveToTarget` both use `CurrentMoveSpeed`. Fixes a real 23% slowdown Arash spotted in play: roaming with no target ran at 0.78, then dropped to 0.60 the instant the next enemy was locked. Only a full STOP (combat/attack/lock) may change speed now. Still affecting *effective* speed (unchanged, documented in PlayerManager.txt): `CrowdSeparation2D` bends direction ~42° around allies (~74% forward progress), attack slots sit on an arc, and `MeleeContactRecovery` skips FixedUpdate while repositioning. **Authored speeds are hero 0.60 vs enemy 0.50 — NOT equal**, despite the older "equal by design" note below.
- **2026-09-16** — Tutorial enemy ATK tuned from play-test feedback: L1/L2 −10% (shared asset), L3 −20% vs L2 via a dedicated `Enemy_Reaper_Man_01_Tutorial_L3`. Stage 3 with ONE hero went from a loss → win at 4.5/100 HP → expected ~19/100 after this cut. Hero's 100 base HP vs 3 enemies is the remaining bottleneck.
- **2026-09-16** — **Stages 1-3 tutorial exception, by DATA not code — and PLAY-TESTED (stage 1 = Won).** New `.../Enemy Base Stats/Tutorial/` holds weakened copies (`Enemy_Reaper_Man_01_Tutorial`, `Enemy_Zombie_villager_Tutorial`: ATK 35→4.2, HP 120→394) so enemy CP is ~27% of a hero's (R 3.7/1.9/1.2 at stages 1/2/3 vs ONE hero) while the hero still needs 8 blows to kill. `Spawner2` (L1+L2, one type) and `Stage_03` (two types) repointed; originals and L4+ untouched; no `if (level<=3)` anywhere. **Fixed while there:** `Stage_03`'s new enemy type could NEVER spawn — `EnemyWaveCounts(3)=[3]` is ONE wave so `BuildWavesFromRules` only ever reads the asset's wave 0, which held Reapers only; both types now live in wave 0 (2+1). `Spawner1` is referenced by no scene/prefab — dead.
- **2026-09-16** — **The CP enforcement layer is GONE; the battle decides its own winner.** `CPBattleController` gutted to a read-only reporter (bottom-up CP: per-unit from its own stats, side total = plain sum, `Ratio` reported only). Deleted: hero rescale to `PerHeroCP` (this was erasing Units-menu upgrades), `PlayerShouldWin`, champion + damage budgets, the levels 1-3 scripted exchange, `ScaleToCP`/`EnemyScale`, and the matching `LevelBattleRules` CP tables. **Every runtime bound on blows-to-die is also gone** — `MaxHitsToKillAnyone`/`HitsToKill(def)`/the maxHP/11 floor, then the maxHP/8 ceiling itself. `MinHitsToKillAnyone` → `LevelBattleRules.BaseStateBlowsToKill = 8`, now an AUTHORING target for level-1 HP, not a rule: enemies grow every stage but the player upgrades every ~5, so a hero is MEANT to slide 8→7→5 blows inside a cycle (that slide is the "go upgrade" signal, and a hard floor erased it). Only `CharacterStats.SafetyBlowFloor = 4` remains, as an anti-one-shot guard. Pause gate moved into `ClampIncomingBlow` (it was the only copy). Compiles clean via Unity MCP; **NOT play-tested.**
- **2026-09-14** — Enemy/hero engagement fixed and PLAY-TESTED on Level 2 (Arash confirmed). Four causes: (1) `reachedGate` pinned the enemy and returned before the hero branch — now breaks off; (2) targeting used `detectionRadius` (20 = SIGHT) for committing — now uses `fairDistanceToPlayer` (ENGAGE, set to **2.0**) for acquire+lock+divert; (3) the gate-attack loop ignored a nearby hero; (4) `MeleeEngagement.RequireVerticalBand=false`. Hero moveSpeed was briefly 1.00 then REVERTED to 0.60 — speeds are equal by design, DO NOT change them.
- **2026-09-14** — Heroes stranded on deploy platforms (only ~half reached the field): the 1.2s gate pose was a coroutine, and any `StopAllCoroutines` during it left the hero locked forever because `PlayerLockState` re-asserts a Static body every FixedUpdate. Hold moved to an `Update` watchdog (`pendingDeploys`) in `PlayerWaveManager` — no coroutine to interrupt. Measured before fix: 4 released / 2 arrived.
- **2026-09-14** — Enemy counts per level now come from `LevelBattleRules.EnemyWaves` (1-10, in waves: L4/5 2+3, L6/7 3+3, L8/9/10 3+4); `EnemySpawner.BuildWavesFromRules` reshapes the LevelConfig's waves to match without mutating the shared asset. Verified want==got for all 10; NOT play-tested.
- **2026-09-14** — PHASE 2 part 2: after BATTLE, one 6s "load" per cleared match releases that match's heroes to the rear lane; Heroes Stats panel became the deployment queue (portrait + "xN" + cyan fill, per Ref2), no more alive/total. New `HeroDeploymentSequencer` (on LevelTemplate.prefab) + `StageDeploymentPlanSO` (authorable types per match, random fallback) + `PlayerWaveManager.DeployBatch`. Compiles; NOT play-tested.
- **2026-09-14** — PHASE 2 part 1: heroes no longer spawn on the gates at all; each match now deals hero CARDS (Ref1) above the player base. `PlayerWaveManager` (`suppressStageSpawning`, `HeroesEarned`, `EarnedHeroes`), new `HeroCardRevealDirector` + `CardBackSpriteBaker` + 3 baked sprites. Compiles; fan pose screenshotted; NOT play-tested.
- **2026-09-14** — SESSIONS.md restructured: 2814 lines → this file; all long-form text moved verbatim to `SESSIONS-ARCHIVE.md` (not injected). Hard size caps added to the Protocol. No code touched.
- **2026-09-12** — Any scene can now be played directly in the Editor (testing only; the build is untouched).
- **2026-09-12** — "The enemy died in 4 blows" traced to weapon SPLASH, not to the clamp; hitboxes re-tuned for all 15 characters.
- **2026-09-11** (4th pass) — Guaranteed-win model rebuilt around ONE protected hero; four global assistance rules deleted.
- **2026-09-11** (3rd pass) — Enemy froze early with its walk cycle still running; degenerate blend trees found in two controllers.
- **2026-09-11** — Stop enemy following Valkyrie's sidestep (Codex).
- **2026-09-11** — Melee units now fight from the SIDE; enemy "slide into the hero" fixed (`MeleeEngagement`, `UnitDepthSorter`).
- **2026-09-10** — Correct CP combat and tutorial spawning (Codex).
- **2026-09-08/09** — CP redesign discussion: one code fix, a retracted finding, and a blocked design decision.
- **2026-09-07** — CP system audited end to end and documented (analysis only, zero code changes); 9 defects registered.
- **2026-09-07** — Three features switched OFF for this version behind `GameFeatureFlags` (nothing deleted).
- **2026-09-06** — Stages 4-20 converted to the template; level design finished to stage 20.
- **2026-09-06** — Every stage is now one `LevelTemplate.prefab` instance; Stages 2 and 3 rebuilt from Stage 1.
- **2026-09-06** — LastStandOffer gated behind the Heroes Stats buy-backs; one buy-back per card per level.
- **2026-09-06** — Revive removed; battle timer stops on level end; enemies could not damage the player base.
- **2026-09-01** — Package Manager resolve failure after a `Library/` wipe: removed the unused NavMeshPlus git package.
- **2026-08-30** — Mutual wipe (both armies dead, neither gate destroyed) now ends the stage as a defeat.
- **2026-08-25** — Match-clear shatter VFX: found the effect was never firing on the merge path, then rebuilt it as mesh-particle shards.
- **2026-08-25** — Board drag felt chunky/cell-by-cell: rewrote `BoardInputController` around a continuous anchor.
- **2026-08-24** — HP bars: enemy bars were mirrored in Play mode, plus a yellow damage trail.
- **2026-08-24** — Summon arrival: the pre-landing ground telegraph (disc → ring).
- **2026-08-23** — Summon arrival VFX (Ludus-style light pillar), two backends.
- **2026-08-23** — Reinforcement gate pose + hidden HP bars, and the new 80% "last stand" gem offer.
- **2026-08-22** — Heroes Stats panel: dynamic per-type "alive/total" HUD + gem buy-back.
- **2026-08-22** — Level1_Stage01 part 2: jump shadow, gap-fill animation, HP-bar sorting on battle end.
- **2026-08-21** — Puzzle-first stage flow: battle gate, jump lanes/formation, unit avoidance, AdMob install.

_Older entries (2026-08-20 and before: TMP Static bake, the `Assets/Scripts/` reorganization, CLAUDE.md init,
and the original session-log setup) are in `SESSIONS-ARCHIVE.md`._
