# SESSIONS.md — Shared Session Log / لاگ مشترک سشن‌ها

> **فارسی:** این فایل حافظه‌ی مشترک بین همه‌ی سشن‌های Claude Code در این پروژه است.
> هر سشن قبل از شروع کار این فایل را می‌خواند تا بداند سشن‌های قبلی چه کرده‌اند،
> و در پایان کار (یا بعد از هر تغییر مهم) یک ورودی جدید به بخش «Session Log» اضافه می‌کند.
>
> **English:** This is the cross-session memory for this repo. Every Claude Code session
> reads it at the start and appends an entry when it finishes meaningful work.

---

## Protocol (for Claude — follow exactly)

**At the START of every session:**
1. Read this whole file (it is short by design — keep it that way).
2. Check **Open Threads** and **Decisions** before proposing anything; do not re-litigate
   a decision already recorded here, and do not redo work already logged.
3. If an entry names a file, class, or flag, verify it still exists before relying on it —
   entries reflect what was true when written.

**At the END of every session, or after any meaningful change:**
1. Append a new entry to the **top** of the Session Log (newest first) using the template below.
2. Update **Open Threads** — add what you left unfinished, remove what you closed.
3. Add to **Decisions** only when a durable choice was made (architecture, naming, a rejected
   approach and why). Not for routine edits.
4. Keep the log to the last ~20 entries; move anything older into `SESSIONS-ARCHIVE.md`.

**Rules:**
- One entry per session, not per message. Edit your own entry as the session continues.
- Write absolute dates (`2026-08-20`), never "today" / "yesterday".
- Reference code as clickable relative paths, e.g. `Assets/Scripts/Managers/LevelManager.cs:42`.
- Facts only: what changed, why, what broke, what is left. No summaries of chat.
- If two sessions run in parallel, do not overwrite another entry — append yours and note the
  overlap in **Open Threads** so the conflict is visible.
- Unity note: `.unity` / `.prefab` / `.asset` edits are hard to review in git. If you changed a
  scene, prefab, or ScriptableObject **by hand in the Editor**, say so explicitly — the next
  session cannot see it in the diff.

### Entry template

```markdown
### YYYY-MM-DD — <short title>
- **Goal:** what the user asked for, in one line.
- **Status:** done | partial | blocked | abandoned
- **Changed:** `path/to/File.cs` — what and why. (one bullet per file/area)
- **Scene/Prefab/SO edits:** manual Editor changes the diff won't show, or `none`.
- **Verified:** how it was tested (Play mode on which scene / build / not verified).
- **Gotchas:** anything that surprised you and would surprise the next session.
- **Next:** what should happen next, or `nothing pending`.
```

---

## Open Threads

_Unfinished work any session may pick up. Delete a line when it is genuinely closed._

- **[2026-09-06] Stages 2+ have no `Regulite Show Panel`, so a roguelite level-up there pauses the
  battle with nothing to dismiss it.** The panel was deleted from Stages 2-3 at the user's request;
  it is what `RogueliteManager.skillSelectPanel` and all three `cardSlots` on `Reg_Manager` point
  at, so those fields are now null. Every *use* is null-guarded — nothing throws — but
  `ShowSkillSelection()` calls `GameplayPause.SetPaused(true)` and `FreezeBattlefield(true)` BEFORE
  the guard, so the game freezes with no card picker. Two ways out, the user's call: switch the
  `RogueliteManager` component OFF on `Reg_Manager` for those stages (if levels 2+ are meant to
  have no roguelite), or move the early-return above the pause in
  `Assets/Scripts/Roguelite/RogueliteManager.cs` so a missing panel simply skips the level-up.
  Not reproduced in Play mode — derived from reading the wiring.

- **[2026-09-06] Stages 4-20 are still the OLD, drifted scenes; Stages 1-3 have never been played
  since the rebuild.** `Assets/PREFABS/Level Template/LevelTemplate.prefab` is now the whole body
  of a stage scene and Stages 1, 2, 3 are instances of it. Two jobs remain.
  (a) **Play-test 2 and 3 first** — press BATTLE and watch the camera move, the waves spawn from
  the enemy gate, heroes release from the deploy slots and march the `Jump positions` lanes, and
  the win/lose panels fire. Nothing was play-tested; only edit-mode captures and a full
  reference-resolution sweep were done. Watch especially for anything that used to be wired to a
  scene object and now silently resolves to the prefab's copy.
  (b) Then run the same conversion on Stages 4-20 — the recipe is in
  `Assets/PREFABS/Level Template/README_LevelTemplate.md`; the editor script that did 2 and 3 only
  needs its `STAGE` and `MOVES_ALLOWED` constants changed. Per stage, carry over ONLY: the
  `BoardGridXY` size, the `BoardGhostMask` mask, the `Blocks`/`RedundantBlocks` groups, the active
  `boardsCover_*`, and `EnemySpawner.levelConfig`. Everything else comes from the prefab.
  **Do not press "Apply All" on a stage instance** — it would push that stage's board out to all
  the others.

- **[2026-09-06] The LastStandOffer buy-back gate and the one-purchase-per-card rule have never
  run in Play mode.** Both compile clean against the live Editor and neither needed a scene edit,
  but no purchase has actually been made. On `Level_1_Stage_1`: (a) wipe one hero type, buy it back,
  wipe it again — the price must NOT return, the card should sit grey showing `0/N`; (b) spend every
  card, then let the army fall past 80% — the last-stand offer must appear only after the LAST card
  is spent, and not before. Watch for the intended dead-end too: if one hero type survives, its card
  is never spendable and the offer correctly never shows.

- **[2026-09-01] Package Manager has not been re-checked in the Editor since NavMeshPlus was
  removed.** `com.h8man.2d.navmeshplus` was dropped from `Packages/manifest.json` and
  `packages-lock.json` (it was unused — zero GUID references anywhere in `Assets/`). Reopen Unity
  and confirm Package Manager resolves with no errors. If GitHub still resets while fetching the two
  remaining git packages (`com.google.ads.mobile`, `com.google.external-dependency-manager`),
  the next escalation is embedding them as local packages under `Packages/` rather than fighting the
  network. Also unresolved: the stray `using UnityEngine.AI;` at
  `Assets/Scripts/Combat/Player/PlayerManager.cs:3` — dead import, harmless, not removed.

- **[2026-09-06] Revive is off; the "straight to Lose" path has not been play-tested.** Compiles
  clean, never played. Break the player base with enemies and confirm the Lose panel fades in
  directly — no orange revive window, no 9-second countdown. Then decide the optional tidy-up:
  delete the (now permanently hidden) "Revive Level Panel" object and the unused RedundantBlocks
  root from the 20 stage scenes, and/or rename `RevivePanel.cs`, which is now only the Lose
  presenter. **Do not delete or disable the `ReviveManager` object itself — it IS the Lose panel's
  presenter.**

- **[2026-08-30] The mutual-wipe defeat has never run in Play mode.** It compiles clean but was
  never played. To test, pick a stage whose `LevelConfig` has ONE small wave, press BATTLE, and let
  the last hero and the last enemy trade fatal blows: after ~2.5s the console should print
  `[LevelGameManager] Mutual wipe ...`, gameplay should pause, and — since 2026-09-06 — the LOSE
  panel should appear directly (this test originally expected the revive offer; that path is gone,
  and with it the `postReviveSettleSeconds` re-fire case, which can no longer happen because
  nothing ever resumes the battle). Also confirm the three no-false-positive cases: the puzzle
  phase before BATTLE, the gap between two waves where all spawned enemies are dead but a wave
  remains, and the two normal win/lose paths. `stalemateGraceSeconds` (2.5) has not been judged at
  a real frame rate — it doubles as the window a `LastStandOffer` purchase has to land in.

- **[2026-08-25] The shard-burst match VFX has had one tuning pass; the shape of the burst is still
  open.** Play-tested once — spawn pattern and density were called good, timing and colour were
  fixed in a follow-up (lifetime 0.80–0.95s, tint sampled off the sprite). Still unverified in play
  *after* that follow-up. Remaining knobs if it needs more: `shardsPerCell` (40), `shardSizeRange`
  (0.10–0.28 world units, cell is ~1.086), `sortingOrder` (20) if shards ever draw behind the board,
  and the Limit Velocity `dampen 0.42` which is what stops the cloud flying off.
  `FractureObject` is still in the scenes as the fallback and can be deleted once this is signed off.

- **[2026-08-25] `Pink` (id 3) and `MidPink` (id 4) stack prefabs use the same sprite.** Both sample
  to `#EF7CC1`, so the two block types are visually identical when they shatter. Art issue, not a
  code one — worth a decision on whether MidPink should have its own colour.
- **[2026-08-25] The board drag rewrite has never been play-tested.** `BoardInputController` was
  rebuilt around a continuous anchor and compiles clean, but Play mode was never entered. Test on
  `Assets/Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_5.unity` (real 8×6 board, `cellPadding
  0.055`): slow circle drag must stay glued to the cursor with no drift; diagonal in open space
  must be a straight line, not a staircase; diagonal into a corner must slide along the wall;
  a fast flick must stop flush and never tunnel; axis-locked pieces must not budge on the locked
  axis. Then judge `settleDuration` (0.07) and `maxSubStepCells` (0.25) at a real frame rate.
  Also confirm `Tutorial_Board_01` hints still line up and the move budget counts one per move.
- **[2026-08-25] Sanity-check multi-cell pieces across the stages after the `CellPitch` fix.**
  `PieceSimple.SnapSubBlocksToOffsets` no longer re-spaces sub-blocks at `CellSize`, so pieces now
  render at the authored pitch spacing and line up with their cells. Verified in
  `Tutorial_Board_01`; the other stages have not been eyeballed. Any prefab that was hand-nudged
  to *compensate* for the old wrong spacing will now look slightly wide — check 3+ cell pieces.
- **[2026-08-25] `BoardBootstrapper` calls `AutoBuildOffsetsFromChildren()` unconditionally**
  (`BoardBootstrapper.cs:23`) even though the comment says "if offsets were not authored" — the
  `if` is missing. That method *rewrites* `shapeOffsets` from collider centres and sets `_anchor`
  directly **without occupying the board**, bypassing `TryPlace`. It also races `PieceSimple.Start`,
  which is where `pieceId` is assigned — so if the bootstrapper wins, `TryPlace` runs with
  `pieceId == 0` and `OccupyCells(cells, 0)` writes 0, i.e. "empty", claiming nothing. Not touched
  this session and not proven to bite, but it is fragile and worth a look.
- **[2026-08-25] Nothing in the project sets `Application.targetFrameRate`.** On Android that can
  leave the game at a low cap that no input code can compensate for. One line in a boot script
  would rule it out — only worth chasing if the drag still feels slow on a device *after* the
  rewrite is play-tested.
- **[2026-08-24] The HP-bar work has NEVER RUN.** Unity was closed for that whole session, so the
  mirroring fix (`HealthBar.KeepUnmirrored`) and the new yellow damage trail are not verified and
  not even compile-checked. Play a stage and confirm both together: the enemy bar should drain
  right→left like the players', and a yellow chunk should peel away behind every hit. Also judge
  the three tunables (`trailHoldSeconds` 0.18, `trailDrainPerSecond` 0.9, `trailFadeSeconds` 0.12)
  at a real frame rate, and decide whether the CASTLE GATE bars should keep the trail — they get
  one automatically and nobody has asked for it either way.
- **[2026-08-24] Optional follow-up: bake real `DelayedBar` objects into the prefabs.** The trail
  is currently cloned at runtime (see Decisions). An editor script to create authored objects in
  all 13 `HealthBar` prefabs and wire `delayedBar` was written but never ran — the Editor was
  closed. Only worth doing if a designer wants to restyle the trail per prefab; the runtime clone
  is otherwise strictly less to maintain.
- **[2026-08-24] `PlayerStats` disagrees with itself about max HP.** `Start` passes `maxHealth`,
  `ApplyDamageToPlayer` passes `PlayerManager.statsBase.maxHP`
  (`Assets/Scripts/Combat/Player/PlayerStats.cs:14` and `:22`). If those two values ever differ,
  the first hit silently changes the denominator. Pre-existing; the damage trail just makes it
  visible. `EnemyStats` uses `maxHealth` in both places and is fine.
- **RELEASE BLOCKERS from the 2026-08-21 ads work** (all currently set for testing):
  `BattleStartController.doNotPersistAllowance` must go OFF (otherwise the 24h battle cap resets
  every app restart); `AdManager.useTestIds` must go OFF with real unit ids filled in;
  `GoogleMobileAdsSettings.asset` still holds Google's TEST app ids; and the Android
  `applicationIdentifier` is UNSET (only Standalone `com.DefaultCompany.2D-URP` exists) — a real
  package name is required and must match the AdMob console entry.
- `Level_1_Stage_1.unity` is NOT in Build Settings yet (user said they would add it).
- **[2026-08-23] `LastStandOffer` is wired but INERT and the scene is UNSAVED.** `offeredUnit` on
  `Canvas /LastStandOffer ` has no `UnitDefinitionSO` assigned, so the 80%-dead offer can never
  appear. Everything else (component added, `cellTemplate` → `OfferCell `, template set inactive)
  was done over MCP with Undo but **the scene was never saved** — verify all of it still exists
  before assuming it does. Also check the stray `Image` on `LastStandOffer `: the script does not
  control it, so a leftover white sprite would sit on the HUD for the whole match.
- **[2026-08-23] The summon arrival VFX has never run in Play mode.** Everything was verified by
  edit-mode capture only. Unverified: the trail following a real jump, the flash landing on the
  exact frame, emitter pooling/recycling, sorting against the board and the unit sprite, and the
  binder's behaviour when a unit dies mid-jump. `StarterScene` has no `PlayerWaveManager`, so test
  in a stage scene. Also still open: `SummonPillar.vfx` does not exist — the VFX Graph backend
  compiles and is package-installed + define-enabled, but until that asset is built the director's
  `Auto` backend always resolves to Particles (by design).
  **[2026-08-24]** the ground telegraph (`SummonGroundCircle`) is in the same state — its
  disc→ring lifecycle is confirmed by capture, but `circleLeadTime` (0.05s, "one frame before
  touchdown") has never been judged at a real frame rate, and the burst's own `GroundRing` was
  left switched ON as instructed, so watch for a double ring.
- **[2026-08-23] Nothing from the reinforcement-arrival work has run in Play mode.** The gate pose
  (`reinforcementGateHold`, 0.75s), the HP bar hidden during it (`HealthBar.SetHiddenOnGate`), the
  80% trigger, the purchase, and the DOTween pulse are ALL unverified. The only Play-mode
  observation was the replay bug — i.e. the trigger firing at the wrong time. Re-play a WON stage
  too, to confirm the `HeroRoster` scene-load reset actually fixed it.
- **Heroes Stats panel: two of its three states have never executed.** Building the cells is
  confirmed working in Play mode; the WIPED look (`Gray out Avatar` on, count hidden, `Parent `
  buy button shown) and the gem buy-back (`PlayerWaveManager.SpawnReinforcements` → spawn at the
  gates, jump to the REAR `jumpLanes` entry, march) have never run. Test both before trusting them.
- **[2026-09-06 — partly closed] The Heroes Stats panel used to exist in `Level_1_Stage_1` ONLY.**
  Stages 1-3 now all get it from `LevelTemplate.prefab`, already wired into
  `BattlePhaseTransition.fadeInAfterMove`. **Stages 4-20 still have no such object** — they are
  closed by converting them to the template (see the thread below), not by hand-copying the panel.
- **Heroes still LOCKED on the castle gates when BATTLE is pressed are stranded permanently** —
  the board is hidden from that point, so no puzzle match can ever release them. They just stand
  on the gates for the rest of the stage. `HeroRoster` deliberately does not count them (see
  Decisions), but whether the stranding itself should be fixed — auto-release them on battle
  start, or refuse the BATTLE press while a wave is unreleased — is an open design question.
- `gemsPerHero` on `HeroStatsPanel` is a placeholder 50 (× squad size). The reference art shows
  200. Either set that, or fill `respawnGemCost` per `UnitDefinitionSO` (which overrides it).
- **Nothing has been run on a device.** No Android build was produced this session, so the AdMob
  banner has never actually been seen on a phone. `JAVA_HOME` was repointed to the 6000.3.21f1
  OpenJDK (it dangled at an uninstalled 2021.3.42f1) — Unity must be restarted for that to apply.
- **`PlayerAttackState.cs:69` and `PlayerDeathState.cs:9` write `linearVelocity` on a body they
  just set to Static**, spamming "Cannot use 'linearVelocity' on a static body" every FixedUpdate.
  Harmless but it floods the console and hides real errors. Two-line guard each.
  **2026-08-22: the third site, `PlayerLockState`, is FIXED** (guarded while fixing the gap-fill
  animation). These two remain — the user was offered the fix and has not taken it yet; the error
  is still visible in their latest screenshot. Copy the guard from `PlayerLockState.cs:22`.
- **`EnemyManager` target selection never got the hysteresis treatment** that `PlayerManager` did
  (see the 2026-08-21 Decisions row). Enemy FACING has a `facingDeadZoneX` guard, but if enemies
  are seen flip-flopping between left and right, that is where to look.
- **Unconfirmed report:** "back row plays its walk animation but stays in place" mid-battle
  (user video 483, case 2). Simulating a perfectly-aligned rear hero did NOT reproduce it, so the
  proposed cause (pursuit vs personal-space cancelling out) is probably wrong. A likely alternative
  is `PlayerPursueTargetState` calling `SetAnimMoving(true)` while `HandleMoveToTarget` has already
  hit its arrival threshold and zeroed velocity. Catch it live and read the state.
- **Design gap:** the board can empty BEFORE the move budget is spent (observed: board cleared in
  3 of 8 moves). Nothing signals it and nothing advances, so the only way forward is BATTLE. Worse,
  `PlayerWaveManager.WaveLoop` exits PERMANENTLY once the board is empty, so no further hero waves
  can ever spawn that stage. Decide: refill the board, auto-start the battle, or hide the counter.
- **Per-script docs are behind** for the scripts touched on 2026-08-21: `PlayerManager`,
  `CrowdSeparation2D`, `AttackSlotRegistry`, `EnemySpawner`, `BattleStartController`,
  `FormationGapFiller`, `PlayerPursueTargetState`. (`HealthBar` was fully rewritten 2026-08-23 and
  is no longer behind.) Two standalone guides WERE written:
  `Formation_GapFilling_Guide.md` and `Unit_Avoidance_And_NonCollision_Guide.md`.
  `SimpleJump2D.txt` was brought fully up to date on 2026-08-22.
  **Additionally behind as of 2026-08-22** (the user explicitly asked for NO docs on those two
  fixes, so this is a deliberate debt, not an oversight): `PlayerManager` (new
  `isFormationStepping`), `PlayerLockState` (formation-step bypass + static-body guard),
  `FormationGapFiller` (flag handover + mid-step battle-start abort),
  `LevelGameManager` (new static `OnGameStateChanged` / `IsBattleRunning`).
  (`HealthBar`'s authored-order capture/restore IS now documented — rewritten 2026-08-23.)

- **`GroundProjected` shadow mode has never been run.** The 2026-08-22 shadow work defaults every
  unit to `StickToCharacter`, so the detach/slide path — including its two fixes (delta-based
  travel, `OnDisable` reattach) — is entirely untested. If that look is ever wanted back, test it
  before trusting it.

- **The broken `shadowProgressCurve` is still serialized in all 12 character prefabs** (first key
  at time `-0.104`, value `0.994`). `ShadowProgress01()` now rejects it and falls back to t^2, so
  it is harmless, but anyone who re-enables `GroundProjected` and wonders why their authored curve
  is ignored should clear the curve in the Inspector first.

- Build Profiles points `Level_1_Stage_8..20` at the stale path `Scenes/Level_1_Stage_N.unity`;
  the real files are under `Scenes/TestScenes/GamePlay Scenes/`. Stages 8-20 are not in the build.
- `Assets/Scripts/_Legacy/` holds 16 quarantined scripts. Confirm each is truly unused, then delete.
- Data assets still live under `Assets/Scripts/` (`REGULITE/RogueliteScriptableObjects/`,
  `TowertDefenseScripts/Prefabs/` + `Test Prefabs/`, `UI/UI-SOs/`). Move them out of the script tree.
- Duplicate type names to disambiguate: `Piece` and `GameState` are each declared in two files.
- `/feature-doc` was invoked with no feature named. Waiting on the user to specify: feature name,
  which scripts implement it, and what broke along the way.
- **`GameStartManager.Awake()` hard-codes a full save wipe on every launch** (`resetBool = true;
  OnResetButtonClicked();`) — confirmed still present as of the doc pass. Almost certainly a
  debug leftover; needs the two lines removed or `resetBool` exposed as a real toggle before
  this project can retain any player progress across sessions.
  **2026-08-21: the user explicitly asked to KEEP this while testing.** Note it is NOT in the
  gameplay scenes (it reaches them via `DontDestroyOnLoad` from `StarterScene`), so pressing Play
  directly on a stage skips the wipe entirely — which is why `BattleEnergyService.SessionOnly`
  exists rather than relying on this reset.
- Roguelite XP is fully implemented but never triggered in play: `RogueliteManager.AddXP` /
  `NotifyEnemyKilled` have zero live call sites (`EnemyManager`'s call is commented out).
  Confirm whether this is intentional WIP or a lost wire-up.
- `WinPanel.RewardValues` is summed cumulatively across HP tiers in
  `StageRewardCalculator` (hpCase 3 = r1+r2+r3, not just r3) while
  `HomeManager.TryGetStageRewardPreview` passes hpCase=1 despite its own comment saying
  "best case" — the stage-card reward preview likely understates the real payout.
  See `Assets/Documentation for scripts/StageRewardCalculator.txt` and `HomeManager.txt`.
- `UnitsPanelController.HandleDeploySave` likely swaps the `highlightUndeployedId`/
  `highlightDeployedId` arguments when refreshing the deploy overlay post-swap — cosmetic only
  (save data is correct) but the just-swapped cards probably lose their highlight. See
  `Assets/Documentation for scripts/UnitsPanelController.txt`.
- **The TMP Static bake has not been proven end-to-end.** All font assets are Static with
  clear-on-build off as of `ab1297e`, but nobody has yet deleted `Library/` and confirmed the fonts
  survive the reimport without a manual "Generate Font Atlas". Do that once and this closes.
  Re-run `Tools/Blasty/Fonts/Bake All TMP Fonts To Static` after adding or regenerating any font —
  it is idempotent and skips assets that are already correct.
- Duplicate `.ttf` files with no live reference: `Assets/Arts/FONTS/Lilita_One/LilitaOne-Regular.ttf`
  duplicates `LILITAONE-REGULAR.TTF`, and `Dangrek/Dangrek-Regular.ttf` duplicates
  `Dangrek-Regular 1.ttf`. Confirm which is unused and delete, so name-based font matching stops
  being ambiguous.
- `Dangrek-Regular 1 SDF.asset` uses a 2048×2048 atlas for ~42 characters (~8 MB of hex in the
  repo). Shrink to 512 or 1024 by hand in Font Asset Creator — the bake tool does not change atlas
  dimensions.
- A full per-script reference now exists at `Assets/Documentation for scripts/` (101 `.txt`
  files, one per live script reachable from the 9 build scenes). Read the target file's doc
  there before editing it blind — each one lists dead numbered siblings, magic strings, and
  known bugs found while writing it.

---

## Decisions

_Durable choices with their reasons, so no session reopens them blindly._

| Date | Decision | Why |
|------|----------|-----|
| 2026-08-20 | Cross-session state lives in `SESSIONS.md` at the repo root. | One file, tracked by git, readable by a human and by every session. |
| 2026-08-20 | The **read** half is automated by a `SessionStart` hook (`.claude/hooks/inject-sessions.js`, wired in `.claude/settings.json`) that injects this file's contents into every session. | Relying on each session to remember to open the file made awareness optional; the hook makes it unconditional. |
| 2026-08-20 | The **write** half is triggered by the `/wrap` slash command (`.claude/commands/wrap.md`), not by a hook. | No hook event reliably means "the session is ending", and a `Stop` hook fires after every response — far too noisy. An explicit one-word command is the honest mechanism. |
| 2026-08-20 | TMP font assets in this project are **Static** atlas population, not Dynamic, with `ClearDynamicDataOnBuild` off (including the project-wide default in `TMP Settings.asset`). | Dynamic treats the glyph atlas as a rebuildable cache, so it does not survive a `Library/` wipe, a fresh clone, or a build — it forced a manual "Generate Font Atlas" every time. Static bakes glyphs into the `.asset`. Accepted cost: Static cannot add glyphs at runtime, so any font that must render Persian/Arabic or player-typed text is an explicit exception and stays Dynamic **with its Source Font File assigned**. |
| 2026-08-21 | **Units have ZERO physics interaction.** They never collide or push. A moving unit instead uses LOOK-AHEAD steering to route around an ALLY in its path, plus a small personal space that runs only while walking. | A continuous separation push was built and rejected TWICE. First it shoved heroes away from ENEMIES the moment they closed to attack range, so they orbited their target instead of fighting it. Second, even same-team-only, a constant shove is still an interaction — the requirement is that a unit which has stopped is left completely alone. Steering is predictive (it prevents the overlap) rather than corrective (fighting one that already exists), which is what makes "don't touch standing units" possible at all. Full history in `Assets/Documentation for scripts/Unit_Avoidance_And_NonCollision_Guide.md`. |
| 2026-08-21 | **An attack spot must ALWAYS be within `maxAttackRange` of its target.** Attackers fan out on an ARC around the target (`radius = min(anchorDistance, maxAttackRange * 0.8)`), never on a flat sideways offset. | `PlayerPursueTargetState` decides pursue-vs-combat by distance to the TARGET while the mover walks to the SLOT. A flat `+0.90` offset put a hero 1.57 from an enemy with a range of 0.85, so the mover reported "arrived" (0.046 away) while the state reported "too far" — a hard deadlock, units frozen on the spot. Any future slot scheme must preserve this invariant. |
| 2026-08-21 | **Every "which is nearest / which side" decision needs hysteresis or a deterministic tie-break.** | The identical bug shape appeared FIVE times: target selection, side-anchor selection, `FaceLeft` re-picking the anchor, swerve side with a blocker dead-ahead, and swerve side with units exactly overlapped. Symptoms were units spinning left-right on the spot, or two units picking the SAME side (`0 > 0` is false for both) and travelling as one merged blob. Margins are compared in SQUARED space, so a linear margin must be squared. |
| 2026-08-21 | **Battle-gate flags all default to "ungated"**: `waitForBattleStart = false`, `BattleIsRunning = true`, `EnemiesHaveAppeared = true`. Only a scene that actually has the new components flips them. | Stages 1-20 were authored before the puzzle-first flow. Defaulting to gated would have silently frozen every one of them. Presence-based gating means the new scene opts IN and nothing else changes behaviour. |
| 2026-08-21 | AdMob + EDM4U are installed from **git URLs**, not Google's scoped registry. | `https://unityregistry-pa.googleapis.com` was unreachable from this machine; the resolve failed with "Package [com.google.external-dependency-manager@1.2.187] cannot be found". If the registry is ever restored, drop the EDM4U git url at the same time — do not keep both. |
| 2026-08-22 | **A render-order override that exists for combat must be scoped to combat, never applied as a constant.** `HealthBar` captures its canvas's AUTHORED sortingOrder and restores it whenever the level leaves `GameState.Playing`. | The `sortingOrder = 500` added on 2026-08-21 fixed bars hiding behind other units, but 500 outranks everything in the scene forever — so the bars punched through the win panel. The user chose lowering the order over hiding the bars, explicitly because the roguelite skill panel will hit the same wall later. Restoring the AUTHORED value (not some hand-picked low number) is what guarantees it lands under the UI: it is provably the configuration that worked before the override existed. |
| 2026-08-22 | **`LevelGameManager.OnGameStateChanged` is the authoritative "is the battle over?" signal.** The battle ends when a GATE reaches 0 HP — enemy gate = won, player gate = lost/revive — and resumes only when a revive is accepted. | Confirmed by the user as the real rule. Win, lose and revive are three different paths and revive RESUMES combat, so any listener keying off a single panel or a one-way bool gets revive wrong. Routing `CurrentState` through a property setter means the existing five assignment sites need no changes and cannot forget to fire it. |
| 2026-08-22 | **Jump shadows stay glued to the character (`ShadowJumpMode.StickToCharacter`).** The ground-projected shadow is kept as an opt-in mode, not deleted. | The user's call, flagged as provisional ("maybe we change this functionality later"), so the old look had to remain reachable rather than being ripped out. Making it an enum default also meant zero prefab edits — the 12 character prefabs simply fall through to the C# field initializer. |
| 2026-08-22 | **A view component must DECLARE its visual states, never infer one by capturing whatever the scene was authored with.** `HeroStatCell.aliveTint` is an explicit serialized field defaulting to white; it used to be `avatar.color` read at `Awake()`. | The capture version shipped a bug within the hour. `Hero avatar` was authored blue for the `UISprite` placeholder it originally held, so once `Bind()` swapped in the real portrait, "alive" faithfully restored a tint that was only ever meant for a placeholder — every LIVING hero rendered as a dark blue silhouette and it read as "the grey-out is inverted". A captured default silently inherits authoring accidents; a declared one cannot. Corollary now in the code: when `deadOverlay` is assigned, the avatar's colour is never touched at all. |
| 2026-08-22 | **`HeroRoster` counts a hero only once `PlayerManager.isUnlocked` is true** — i.e. once a puzzle match has actually thrown it into the field. | Heroes spawn onto the castle gates LOCKED. Press BATTLE with a wave still sitting there and it is stranded for the rest of the stage (the board is hidden, so nothing can release it) — it never fights and never dies. Counting those would pin `alive/total` permanently above zero, and the buy-back button, which only appears at 0, could never be reached. |
| 2026-08-22 | **Battle-phase UI choreography lives in `BattlePhaseTransition`, not in each panel.** It owns `fadeOutOnPress` (fires before the camera moves) and `fadeInAfterMove` (fires after it settles); panels are passive and just get switched on. | The transition is the only thing that knows when the camera has arrived, so it is the only thing that can sequence against it. Keeping the ordering in one component means a new battle panel is wired by dropping it into an array rather than by teaching it about the camera. Consequence to remember: a panel in `fadeInAfterMove` must be left INACTIVE in the scene, and `BattleStartController.hideButtonAfterBattleStarts` had to go OFF so the BATTLE button fades with its panel instead of popping out first. |
| 2026-08-23 | **A per-scene trigger arms on an EVENT that fired in this scene — never on a static flag, and never on the ABSENCE of static state.** `LastStandOffer` arms only on `BattleStartController.OnAnyBattleStarted`; `HeroRoster` clears itself on `sceneLoaded`. | Both cheaper-looking options are the same bug. (1) The first version relied on "`TotalStarting()` is 0 until the battle-start snapshot" as an implicit guard — true on a fresh Play session, FALSE after a scene reload, because `HeroRoster` is static, `ResetStatics()` runs once per PLAY SESSION, and `ClearAll()` had zero call sites. A replayed stage inherited the last battle's `StartingCount` while `TotalAlive()` was 0, so the ratio read "army wiped out" and the offer appeared during the puzzle phase. (2) Reading `BattleStartController.BattleIsRunning` instead would have failed identically one layer down: it is also static, it DEFAULTS TO TRUE, and the new stage only sets it false in `Awake` — a component enabling first reads the previous stage's value. An event cannot lie about which scene it came from. Corollary: any NEW reader of a static tally must assume the tally is stale until something in this scene says otherwise. |
| 2026-08-23 | **Reinforcements reuse `PlayerWaveManager.SpawnReinforcements` verbatim; new callers add a trigger and a look, never a second arrival path.** | The gate pose, the hidden HP bar, the lane choice, the 90% landing and the FSM handover are five coupled behaviours. `LastStandOffer` is ~190 lines of trigger + UI that bottom out in one call, so a fix to the arrival is a fix everywhere. The same rule is why `HeroStatCell` gained a third look (`ShowAsOffer()`) instead of the offer growing its own cell class — and why `SetAlive(0)` was NOT reused for it: that state greys the portrait, which is correct for "wiped out" and wrong for a purchase prompt. Reuse the mechanism, not the state that happens to look similar. |
| 2026-08-20 | The conversion is done by a repeatable editor tool (`Assets/Scripts/Editor/TMPFontAssetStaticBaker.cs`), not by hand-editing the Inspector or patching the `.asset` YAML directly. | Hand-editing does not scale and is not reproducible for the next font added; direct YAML patching was considered and rejected because it cannot repopulate an atlas — only TMP's `TryAddCharacters` can, and it needs a loaded font face. The tool also re-runs safely on assets that are already correct. |
| 2026-08-24 | **A "keep me upright" correction measures the WORLD AXIS of the thing it is correcting, never a parent's `lossyScale`.** `HealthBar.KeepUnmirrored` probes `healthBar.transform.localToWorldMatrix.MultiplyVector(Vector3.right).x`. | `lossyScale` is blind to a 180° rotation, and reading the PARENT ignores every flip authored below it. The enemy prefabs use both — a mirrored root, then three 180° Y rotations and two negative scales further down that cancel it — so a parent-sign correction was a fifth inversion on top of four, and the bar drained backwards in Play mode while looking correct in the Scene view. Probing the actual Image's world axis is self-correcting and needs no knowledge of how a prefab was authored. Corollary: the fix is prefab-agnostic, so the three-flip authoring was left in place rather than "cleaned up" — undoing it by hand across 6 prefabs would have been 6 chances to get it wrong for no gain. |
| 2026-08-24 | **The damage-trail Image is CLONED from the main bar at runtime (`HealthBar.BuildTrail`), not authored per prefab.** `delayedBar` stays serialized so a hand-assigned override still wins. | Three reasons, in order of weight. (1) Cloning inherits the enemy prefabs' hand-authored mirroring for free; a from-scratch Image would have to re-derive it and would silently drift the next time someone re-authors a prefab. (2) The castle-gate bars live in the SCENES, not a prefab, so authored objects would have to be wired into all 20 stage scenes by hand — the clone reaches them automatically. (3) It made the feature deliverable with the Editor closed. Accepted cost: one extra GameObject per bar at spawn, and designers cannot restyle the trail per prefab without assigning `delayedBar` manually. |
| 2026-09-06 | **A stage scene is ONE prefab instance (`LevelTemplate.prefab`) plus that stage's board pieces — not a folder of per-feature prefabs.** The board (`BoardGridXY` size, `BoardGhostMask.mask`, the `Blocks` groups) stays a per-instance override; everything else is shared. | A prefab cannot serialize a reference to a scene object, and this level's wiring crosses every boundary a "sensible" split would draw: `BattlePhaseTransition` alone reaches the camera, the Puzzle Board, `Top Shadow` and four Canvas panels; `PlayerWaveManager` reaches `BoardStages` under PlayerCastle, `Jump positions`, `EnemySpawner` and `MatchResolver`. Splitting into `LevelTemplate_UI` + `LevelTemplate_World` would have nulled those arrays silently — they would look fine in the Inspector of the prefab and be empty in the scene. The monolith is the only shape where "edit once, applies to all 20 levels" is actually true. Accepted cost: editing a stage means entering prefab mode or applying single properties, and **"Apply All" on an instance is destructive** (it publishes that stage's board to every other stage). |
| 2026-09-06 | **Stages 2 and 3 adopted Stage 1's world layout wholesale — castle positions, camera framing, board world position — keeping only their own board *contents*.** | The user's rule was "everything except the table layout and the enemy progression comes from Level 1", and the base distance had just been retuned in Stage 1 (commit `b147e5e`). Stage 2's gates were 9.64 apart against Stage 1's 13.83, so keeping the old spacing would have meant Stage 1's camera move (`+7.12 +2`) framing the wrong thing. Piece positions are local to `BoardBG`, so moving the board to Stage 1's spot preserves which cell every piece occupies — the authored layout survives, only its placement on screen changed. Enemy difficulty needed no per-scene work at all: `EnemySpawner.RunLevel()` already reads `LevelManager.CurrentStage`. |

---

## Session Log

_Newest first._

### 2026-09-06 — Every stage is now one `LevelTemplate.prefab` instance; Stages 2 and 3 rebuilt from Stage 1

- **Goal:** roll Level 1's design out to the other stages. Everything from `Level_1_Stage_1` —
  gates, bases, camera framing, the BATTLE camera move, board behaviour, the bottom menus
  (Feature panel, Heroes Stats, LastStandOffer, counters), all of it — becomes a prefab in a new
  folder under the main `PREFABS`, so a later edit reaches all 20 levels. **Two things must NOT be
  copied:** each stage's authored board layout, and the per-stage enemy progression. Asked for
  Stages 2 and 3 only, for now.
- **Status:** done for Stages 1-3. NOT play-tested (Editor only, edit mode).
- **Changed:**
  - `Assets/PREFABS/Level Template/LevelTemplate.prefab` — **new.** One root holding all 22 of
    Stage 1's former scene roots. Created with `PrefabUtility.SaveAsPrefabAssetAndConnect`, so
    Stage 1 itself became the first instance rather than a copy.
  - `Assets/PREFABS/Level Template/README_LevelTemplate.md` — **new.** What is shared, what is
    per-stage, and the 7-step recipe for converting the remaining stages.
  - `Level_1_Stage_1/2/3.unity` — each is now a single root (`LevelTemplate`) plus that stage's
    own block groups as prefab-instance additions. Stage 2 dropped 535 KB → ~40 KB.
- **Scene/Prefab/SO edits (not visible in the diff as intent):** all of the above was done over
  MCP `Unity_RunCommand`, not by hand, but the result IS a scene/prefab rewrite. Stages 2 and 3
  had **14 roots each deleted** and replaced. What was carried across from the old scenes, and
  nothing else: `BoardGridXY` size, `BoardGhostMask.mask`, the `Blocks`/`RedundantBlocks` groups,
  `EnemySpawner.levelConfig` + `cpWeights`, and which `boardsCover_*` was active.
  Two small fixes were folded into the template while it was being built: three extra board-cover
  sprites (`boardsCover_Stage1_2-10 / _11-15 / _16-20`) were added under `Puzzle Board` so any
  stage can pick its cover, and four references that had been relying on `FindObjectOfType` were
  wired explicitly (`BattleStartController.enemySpawner` / `.transition`,
  `LastStandOffer.waveManager` / `.heroStatsPanel`, plus `BoardInputController.moveBudget`).
- **Verified:** all three scenes reopen with 0 console errors and 0 missing components; every
  serialized reference in the prefab resolves *inside* the prefab (checked by walking every
  `SerializedProperty` of the 13 key components); orthographic captures of all three stages show
  the correct castles, environment, deploy slots and each stage's own pieces. **Play mode was
  never entered.**
- **Gotchas:**
  - The whole level had to become **one** prefab, not a folder of small ones. `BattlePhaseTransition`
    alone references the camera, the board, `Top Shadow`, and four Canvas panels; a prefab cannot
    hold a reference to a scene object, so any split would have silently nulled those arrays.
  - Stage 2/3's world layout was different from Stage 1 (PlayerCastle y = -2.04 vs 2.84, gates
    9.64 apart vs 13.83). The template's Stage 1 layout won, by design — but that means the board
    also moved to Stage 1's world position. Piece positions are **local to `BoardBG`**, so which
    cell each piece sits in is unchanged; only the whole board's placement moved.
  - Stage 2 had a second `BoardStages` under `BoardBG`, duplicating the one under
    `PlayerCastle/Stage Holder`. It was deliberately NOT carried over.
  - `EnemySpawner.waitForBattleStart` is now ON in Stages 2-3 (it was OFF). That is the point —
    they now have Stage 1's puzzle-then-BATTLE phase instead of spawning on load.
  - **Never press "Apply All" on a stage instance.** It would push that stage's board into the
    template and out to every other stage.
- **Follow-up in the same session — two rounds of cleanup:**
  1. `EnemySpawner` flag flip moved from `Start()` to `Awake()`. The first hero wave was showing
     its HP bars for the whole puzzle phase: `HealthBar.Awake` reads the static
     `EnemySpawner.EnemiesHaveAppeared`, which defaults to TRUE, and `PlayerWaveManager.Start` →
     `WaveLoop` spawns that first wave synchronously before its first `yield`. Unordered Starts,
     so whichever won decided it. Awake runs before every Start, so the gate is now deterministic.
  2. Per-stage object stripping, at the user's request: Level 1 keeps every authoring leftover,
     Stages 2+ remove them as `m_RemovedGameObjects` overrides — `PREVIEW (1)`/`(2)`,
     `BoardImage (1)`, `Base Roof_Redundant`, `Blocks (1..3)`, `RedundantBlocks`, `PlayerGate`,
     the three unused `boardsCover_*`, and four Canvas panels (`Regulite Show Panel`,
     `Revive Panel`, `Lose Panel`, `Revive Level `). `Player_Valkyrie` and `Enemy_Reaper_Man_01`
     came out of the **prefab** instead, so they are gone from Level 1 too (0 inbound refs,
     checked first). Table + rationale in the Level Template README.
- **Next:** play-test Stage 2 and Stage 3 end to end (BATTLE press → camera move → waves →
  win/lose), then run the same conversion on Stages 4-20. Tune `PuzzleMoveBudget.movesAllowed`
  per stage (currently 8 / 8 / 7) — that is the "board gets more restricted as you go" knob the
  user asked for, and it is a plain Inspector int on `Input System`. **Decide the roguelite
  question below.**

### 2026-09-06 — LastStandOffer gated behind the Heroes Stats buy-backs; one buy-back per card per level

- **Goal:** two rules on the last-stand offer, from the user. (1) It must not appear while ANY card
  in the Heroes Stats panel still has an unused buy-back — with three hero types on the field, all
  three cards have to have been bought before the offer may show. (2) A card's buy button is good
  for exactly ONE purchase per level; after that it stays off for the rest of the stage and never
  re-arms.
- **Status:** done (code + docs). Compile-verified against the live Editor; NOT play-tested.
- **Changed:**
  - `Assets/Scripts/UI/HUD/HeroStatCell.cs` — new `IsSpent` latch + `MarkSpent()`. `SetAlive` now
    splits `wiped` (drives the FRAME) from `canBuy = wiped && !IsSpent` (drives the count/price
    swap), so a spent card that is wiped a second time shows the grey frame with `0/3` on it rather
    than re-offering the price. `SetAffordable` ANDs in `!IsSpent` — without that the panel's next
    Refresh, fired by any gem change, would hand a spent card its button straight back. `Bind`
    resets the latch so a fresh clone starts unspent.
  - `Assets/Scripts/UI/HUD/HeroStatsPanel.cs` — `HandleBuyBack` bails on `cell.IsSpent` AHEAD of the
    gem charge, and calls `cell.MarkSpent()` after the spawn but BEFORE `Refresh()`, so `SetAlive`
    sees the flag. New `public bool BuyBacksExhausted` (requires `built`, then every cell spent) and
    `public event Action OnBuyBackSpent`, raised last so a handler reading the property gets the
    settled answer.
  - `Assets/Scripts/UI/HUD/LastStandOffer.cs` — new `requireBuyBacksSpent` (default ON) and a
    `heroStatsPanel` ref, auto-found with `FindObjectOfType<HeroStatsPanel>(true)` — inactive
    INCLUDED, because that panel is switched on by `BattlePhaseTransition` and is inactive when this
    Awake runs (confirmed live: `Heros Stats panel` is `activeInHierarchy=False` in
    `Level_1_Stage_1`). `Evaluate()` gained the gate after the dead-fraction test; it also
    subscribes to `OnBuyBackSpent`, since spending the last card is the only thing that can open the
    gate without the roster moving.
  - Docs updated for all three: `HeroStatCell.txt`, `HeroStatsPanel.txt`, `LastStandOffer.txt`.
- **Scene/Prefab/SO edits:** **none, and none needed.** Both new serialized fields are ABSENT from
  the scene YAML, so the C# initializers apply (`requireBuyBacksSpent = true`, `heroStatsPanel =
  null` → auto-found). The stage works without anyone opening the Inspector.
- **Verified:** compile-verified through the MCP `RunCommand` sandbox — a probe script referencing
  `panel.BuyBacksExhausted`, `cell.IsSpent` and `cell.MarkSpent()` compiled and ran clean against
  the live Assembly-CSharp, and the console holds 0 errors. The same probe confirmed exactly one
  `LastStandOffer` and one `HeroStatsPanel`, both in `Level_1_Stage_1`. **Play mode was never
  entered** — no purchase was actually made, so neither rule has been seen working.
- **Gotchas:**
  - **The gate has a real consequence, and it is intended:** a card is only spendable once its type
    is WIPED OUT, so a hero type that survives never spends its card — and the last-stand offer then
    never appears in that battle at all, however far past 80% the army is. If a stage wants the old
    behaviour, turn `requireBuyBacksSpent` off on that stage's component.
  - The gate deliberately does NOT latch: failing it leaves `spent` false, so the offer is postponed,
    not cancelled. The `spent`-on-Show rule (a shown-and-ignored offer never returns) is untouched.
  - A missing `HeroStatsPanel` SKIPS the gate (with a one-off warning in Awake) rather than blocking
    forever — a panel-less scene must not silently kill the feature. But a panel that exists and has
    not built its cells yet BLOCKS: its buy-backs are unspent, not absent.
  - The spent look is grey-frame-plus-`0/3`, not a permanently greyed-out PRICE. A dead price button
    reads as "you cannot afford this", which is the wrong message. One line in
    `HeroStatCell.SetAlive` if that call needs revisiting.
- **Next:** play-test on `Level_1_Stage_1`. Wipe one hero type, buy it back, and confirm the button
  does not come back when that type is wiped again; then wipe every type, spend every card, and
  confirm the last-stand offer only appears after the LAST card is spent.

### 2026-09-06 — Revive removed; battle timer stops on level end; enemies could not damage the player base

- **Goal:** (1) Stop offering the gem revive when an enemy reaches and destroys the player base — go
  straight to the Lose panel — and take revive out of the game entirely. (2) Stop the HUD battle
  timer the moment the enemy castle falls; it was still counting under the win panel. (3) Enemies
  were hitting the player base with no HP loss at all.
- **Status:** done (code + docs). Not play-tested — see Next.
- **Changed:**
  - `Assets/Scripts/Combat/Enemy/EnemyManager.cs:570` — **the base-damage bug.** Added the missing
    `IsAttacking = true;` to the GATE branch of `HandleCurrentAction()`. `EnemyDamageCollider`'s
    castle branch gates every hit on `enemyManager.IsAttacking`, so the swing animation played in
    full, the animation events opened the weapon collider, the trigger fired on the castle — and the
    hit was discarded one line before `ApplyDamageToPlayerGate`. `IsAttacking` is only ever raised
    inside `AttackTarget()`, which the gate branch does not call (it can't: `AttackTarget()`
    dereferences `enemyLocoMotion.currentTarget` and there is no hero target at the gate, so the
    body was copied inline and the flag lost in the copy). Hero-vs-hero damage was never affected.
  - `Assets/Scripts/UI/HUD/BattleHudCounters.cs` — **the timer fix.** `StopTimer()` had existed
    since the script was written but *nothing ever called it*. Now subscribes to
    `LevelGameManager.OnGameStateChanged` and calls it for any state != `Playing`. Added a
    `finished` latch, because `StopTimer()` alone was not enough: the auto-start poll in `Update()`
    only tested `BattleStartController.BattleIsRunning && EnemySpawner.EnemiesHaveAppeared`, and
    **both stay true after a win**, so `running` would have flipped straight back on the next
    frame. Also added `LevelGameManager.IsBattleRunning` to that poll so a HUD root toggled off/on
    after the level ends (which runs `ResetCounters` and clears the latch) still can't restart it.
  - `Assets/Scripts/UI/WinLose/LevelGameManager.cs` — added
    `private static readonly bool OfferRevive = false;` and changed the defeat fork to
    `if (!OfferRevive || (allowSingleRevivePerStage && hasRevivedThisStage))`. The Lose branch is
    now unconditional; the `ReviveOffer` branch is intact but unreachable.
  - `Assets/Scripts/UI/WinLose/RevivePanel.cs` — added
    `private static readonly bool ReviveEnabled = false;` and guarded four entry points on it:
    `Start()` (buttons never wired), `Update()` (countdown never runs), `ShowRevivePanel()` (falls
    through to `ShowLosePanel()`), `ReviveLevel()` / `NoThanksClick()` (return immediately).
  - Docs: `RevivePanel.txt` and `LevelGameManager.txt` each got a `*** STATUS: REVIVE IS
    DISABLED ***` block at the top plus flow/NOTES corrections; `BattleHudCounters.txt` got an
    `END OF BATTLE - HOLDING THE FINAL TIME` section.
- **Scene/Prefab/SO edits:** none. Deliberately — see Gotchas.
- **Verified:** compiles clean (forced `AssetDatabase.Refresh` + `RequestScriptCompilation` through
  the Unity MCP server; console reports 0 errors). Play mode NOT entered.
- **Gotchas:**
  - **Why the timer only misbehaved on a WIN:** on a loss `EnterDefeatFlow` calls
    `GameplayPause.SetPaused(true)` and `Update()`'s `IsPaused` check froze the clock as a side
    effect. `OnEnemyGateDestroyed` deliberately does *not* pause (the coin/gem/XP reward animations
    need `Time.timeScale` running), so nothing stopped it on a win.
  - **`BattleHudCounters` is still only in `Level_1_Stage_1.unity`** (verified by GUID search) — the
    other 19 stages have undriven timer/kill labels, so the fix is only observable in stage 1 until
    the component is rolled out.
  - **`ReviveManager` / `RevivePanel` must never be deleted or disabled.** That component is also
    the LOSE-PANEL PRESENTER: `ShowLosePanel()` lives there and it owns the `losePanel`,
    `loseCanvasGroup` and `BGImage` references in all ~20 stage scenes. Switching the object off
    removes the Lose screen from the game. This is why revive was disabled in code rather than by
    deactivating the GameObject.
  - The **"Revive Level Panel" GameObject is still authored `m_IsActive: 1`** in every stage scene.
    It is hidden only by `revivePanel.SetActive(false)` in `RevivePanel.Awake()` — that line must
    stay, or the orange window shows from frame one.
  - Both switches are `static readonly`, not `[SerializeField]`, on purpose: the scenes already
    carry a serialized `allowSingleRevivePerStage = true`, and an Inspector toggle would eventually
    get flipped back on in one stage out of twenty.
  - Now-dead code kept, not deleted (user asked explicitly): `LevelGameManager.NotifyReviveAccepted`
    / `NotifyReviveDeclined`, `EnemyManager.ResetAfterRevive`, `PlayerWaveManager.RestartAfterRevive`
    — RevivePanel was the only caller of all three. `hasRevivedThisStage` stays false forever, so
    `postReviveSettleSeconds` / `suppressStalemateUntil` never arm. The **RedundantBlocks root** in
    every stage scene is now never swapped in.
  - The Revive / No Thanks buttons still carry persistent `onClick` entries to
    `ReviveLevel` / `NoThanksClick` in the scene YAML (they were double-wired: persistent call +
    `AddListener`). Harmless — the panel is never shown.
- **Next:** play `Level_1_Stage_1` and confirm both: (a) breaking the player base fades the Lose
  panel in directly, with no revive window and no countdown; (b) destroying the enemy castle freezes
  the timer on its final value while the win panel's reward animations still play. Optional tidy-up,
  needs a decision: delete the
  "Revive Level Panel" object (and the RedundantBlocks root) from the 20 stage scenes, and/or rename
  `RevivePanel.cs` to something like `DefeatPanel.cs` (safe — same `.meta` GUID keeps all scene
  references — but it is a 20-scene re-serialize).

### 2026-09-01 — Package Manager resolve failure after a `Library/` wipe: removed the unused NavMeshPlus git package

- **Goal:** the user deleted `Library/` to save space, reopened the project, and Package Manager
  refused to resolve: `com.h8man.2d.navmeshplus: Error when executing git command. error: RPC failed;
  curl 56 OpenSSL SSL_read: Connection was reset, errno 10054 ... fatal: early EOF`.
- **Status:** done (not yet re-opened in the Editor by the user).
- **Root cause:** the package was declared as a **bare git URL with no `#tag`/`#commit`**
  (`Packages/manifest.json:5`). Unity cannot know which revision an unpinned URL points at, so it
  must contact GitHub on *every* resolve — and a `Library/` wipe forces a full resolve. The project's
  two other git packages (`com.google.ads.mobile#v11.2.0`,
  `com.google.external-dependency-manager#v1.2.187`) are pinned, resolve from cache, and never
  errored. The `curl 56` reset itself is a network-level failure reaching github.com, not a project
  fault, and it is intermittent — `Library/PackageCache/com.h8man.2d.navmeshplus@3fdf1984803c` was
  in fact fully downloaded at 11:03 with a hash matching the lock file.
- **Changed:** `Packages/manifest.json` — removed the `com.h8man.2d.navmeshplus` dependency line.
  `Packages/packages-lock.json` — removed the matching locked entry. Both re-validated as JSON.
- **Scene/Prefab/SO edits:** none.
- **Verified:** JSON parses; zero remaining `h8man` matches in either file. **Not** re-opened in
  Unity — the user still has to let the Editor re-resolve and confirm Package Manager is clean.
- **Gotchas:**
  - **NavMeshPlus was completely unused.** All 33 script GUIDs from the cached package were
    extracted and grepped across `Assets/` (scenes, prefabs, scripts) — zero references. The only
    NavMesh-adjacent trace left is a stray `using UnityEngine.AI;` at
    `Assets/Scripts/Combat/Player/PlayerManager.cs:3` with no `NavMeshAgent` usage in that file; it
    still compiles because `com.unity.modules.ai` is a separate manifest entry. Left in place.
  - **Deleting `Library/` does nothing for repo size** — it is gitignored at `.gitignore:6` and was
    never committed. It only frees disk, at the cost of a full asset re-import plus this network
    resolve. Worth telling the user before they do it again.
  - Applied two **global** git settings on this machine (user-approved) to make GitHub fetches
    survive a flaky connection: `http.version=HTTP/1.1` and `http.postBuffer=524288000`. These are
    machine-wide, not repo-scoped, and also affect the two remaining git packages.
  - `Packages/packages-lock.json` also shows an unrelated pre-existing diff from the re-resolve:
    `com.unity.searcher` 4.9.5 → 4.9.4. Harmless, left alone.
- **Next:** user reopens Unity and confirms Package Manager resolves clean. If a 2D NavMesh is ever
  wanted later, re-add the package **pinned**:
  `https://github.com/h8man/NavMeshPlus.git#3fdf1984803c4518eafea98fcb416c8a3aa09f26`.

### 2026-08-30 — Mutual wipe (both armies dead, neither gate destroyed) now ends the stage as a defeat

- **Goal:** the user hit a state in play where every hero AND every enemy died at the same moment.
  Neither gate was destroyed, so nothing ended the level — it should count as a loss.
- **Status:** done (code + docs). NOT play-tested.
- **The bug.** `LevelGameManager` ended a level on exactly two inputs:
  `EnemyGateStats.OnGateDestroyed` (Won) and `PlayerGateStats.OnGateDestroyed` (Revive/Lost).
  That is normally sufficient because a surviving side always marches on the opposite gate. It is
  NOT sufficient when the last hero and the last enemy kill each other and the spawner has no waves
  left: both gates stand, the puzzle board is hidden so `PlayerWaveManager` can never unlock another
  wave, `EnemySpawner.RunLevel` exits, and the stage sits in `Playing` forever with no panel.
- **Changed:** `Assets/Scripts/Combat/Spawning/EnemySpawner.cs` — exposed two things it already
  tracked privately: `AliveEnemyCount` (read-only `_alive`) and `AllWavesDispatched`, set on the
  LAST wave *after* the spawn and *before* its `WaitUntil(_alive == 0)` (so it means "no more are
  coming", not "the field is empty" — always pair the two).
- **Changed:** `Assets/Scripts/UI/WinLose/LevelGameManager.cs` — extracted the body of
  `OnPlayerGateDestroyed` into `EnterDefeatFlow()` (no behaviour change on the gate path), then
  added an `Update()` watchdog that calls the same `EnterDefeatFlow` once the wipe has held for
  `stalemateGraceSeconds` (2.5). The condition needs ALL of: spawner exists + `BattleStarted` +
  `HasSpawnedFirstEnemy` (this is what keeps it inert through the puzzle phase),
  `AllWavesDispatched`, both gates alive, `HeroRoster.TotalAlive() == 0`, `AliveEnemyCount == 0`,
  and a confirming `EnemyStats` scene sweep. New inspector fields `detectMutualWipe`,
  `stalemateGraceSeconds`, `postReviveSettleSeconds`.
- **Design decisions (user-chosen):** it routes through the SAME flow a destroyed player gate uses —
  revive offer first, Lose panel if the revive was already spent — not straight to Lose. And the
  gem buy-backs (`LastStandOffer` / `HeroStatsPanel`) do NOT block it: the grace window is the only
  chance to spend, after which the level ends regardless of gems held.
- **Scene/Prefab/SO edits:** none. Detection lives inside the existing `LevelGameManager`
  specifically so no stage needs re-wiring; the new fields serialize to their defaults everywhere.
- **Verified:** compiles clean in Unity (0 errors; the two new CS0618 `FindObjectOfType` warnings
  match what the rest of this file and `RevivePanel` already do). Play mode NOT entered.
- **Gotchas:** `NotifyReviveAccepted` now arms `postReviveSettleSeconds` (5s). This is REQUIRED, not
  defensive — `RevivePanel.ReviveTheStage()` destroys every locked hero and restarts
  `PlayerWaveManager`, so `TotalAlive()` is legitimately 0 for a second or two right after a revive
  and the watchdog would otherwise re-declare defeat the instant the player paid.
- **Next:** play-test it (see Open Threads).

### 2026-08-25 — Match-clear shatter VFX: found the effect was never firing on the merge path, then rebuilt it as mesh-particle shards

**The complaint.** The match effect "isn't good" — a previous attempt (`FractureObject`, referred
to as FractionManager) sprayed sprite drops and did not resemble the reference. Reference material:
`Assets/Arts/Reference videos/Stack movement.mp4` plus a still of a yellow block shatter.

**The actual root cause, found before touching any VFX.** `MatchResolver` has two clear paths:

- `ClearGroup` → `ScaleDownAndExplode` → called `FractureObject.Explode`. ✔ had an effect
- `MergePieceInto` → `FadeAndScaleDownThenDestroy` → **never touched the VFX at all.** ✘

`preferMergeIfImmovablePresent` defaults to true and `FindNearestWithWarriors` almost always returns
a piece, so the merge path is what most real matches take. Confirmed against the live Editor console:
every match in the log read `[MatchResolver] Merge Yellow 2X (2) → Yellow 2X`. So most matches were
producing **no shatter effect whatsoever**, regardless of how `FractureObject` was tuned. Note also
that `DOTWEEN_ENABLED` is never defined — the project defines `DOTWEEN`, not `DOTWEEN_ENABLED` — so
every `#if DOTWEEN_ENABLED` branch in that file is dead code.

**What the reference actually does** (measured frame-by-frame at 30 fps, ffmpeg from KMPlayer's
LAVFilters — there is no ffmpeg on PATH):

| t | what happens |
|---|---|
| 0 ms | block intact |
| 0–100 ms | splits into one cube per board cell, each collapsing **in place** with a slight inward pull |
| 100 ms | cloud appears **already at full width** (~2 cells). No grow-in, no expanding fireball |
| 100–470 ms | tumbles, barely travels, sinks ~1 cell, fades from the top down |

The anticipation beat is the part that reads as "it broke" rather than "it disappeared", and it was
entirely missing before.

**What was built.**

- `Assets/Arts/VFX/ShardUnlit.shader` — unlit, fakes facet contrast against a fixed light vector.
  Necessary because the gameplay scenes contain no Light/Light2D and the URP **2D Renderer** never
  draws a `UniversalForward` pass, so any Lit material is invisible. The pass carries no LightMode
  tag so it falls into `SRPDefaultUnlit`, which `Render2DLightingPass` does collect. Tint comes from
  the particle COLOR stream, so one material serves every block colour.
- `Assets/Resources/VFX/ShardMesh_0..3.asset` — baked out of the existing
  `Assets/Arts/VFX/NewCubeFrags.fbx` (Blender Cell Fracture, 60 pieces). Four picked for distinct
  silhouettes (chunky / wedge / splinter / rubble), re-centred on their bounding box, scaled so the
  longest axis is exactly 1 unit, flat-shaded by un-sharing every vertex. 12–20 tris each.
  **No Blender round-trip was needed.**
- `Assets/Scripts/Puzzle/Match/ShardBurst.cs` — pooled mesh-particle systems, all built in code.
  Self-bootstraps via `[RuntimeInitializeOnLoadMethod]`, loads meshes from `Resources/VFX`, builds
  its material from the shader, and **copies its colour palette off any `FractureObject` already in
  the scene** so previously tuned colours carry over. Nothing to wire in the Inspector.
- `MatchResolver` — both clear paths now delegate to a shared `CollapseThenBurst`, so the merge path
  finally produces an effect. Manager lookups cached in fields (the old code called
  `FindObjectOfType<FractureObject>()` once **per piece per clear**). `killAnimTime` 0.15 → 0.10.

**Verified in the live Editor** (Unity MCP bridge — the Editor was open the whole session):
shader compiles clean and `isSupported`; a 3×3 static probe renders with crisp light/shadow facets;
a real `ShardBurst.Play()` simulated at t=133 ms yields 80 particles for a 2×1 footprint, Mesh mode,
4 meshes, `Blasty/ShardUnlit`, sorting `Default/20`. Test objects were removed afterwards.

**Not done:** never play-tested in a live match. GPU instancing is off — `Blasty/ShardUnlit` has no
`procedural:ParticleInstancingSetup` path yet, which would collapse a burst to one draw call.

**Process note worth keeping.** The first pass on this task delivered only a written spec and then
asked whether to implement — the user reasonably read that as "you did nothing", because `git status`
on `Assets/` was genuinely empty. When the ask is "this effect is bad, fix it", build the thing.

#### Follow-up the same day, after play-testing

Spawn pattern and density were good. Two things were not.

**1. It vanished too quickly.** Lifetime `0.26–0.42s` → `0.80–0.95s`. Gravity had to come down with
it (`0.95` → `0.45`) or the longer fall carried the shards ~3.6 cells instead of ~2. The size and
alpha ramps were widened to match — size now holds to 0.30 of life then ramps down across the whole
rest, alpha holds to 0.40. The old curves had a late cliff, which is what made it read as vanishing.
Also **removed the colour-over-lifetime cool-down toward grey**: invisible at 0.42s, but over 0.95s
it read as dusty and washed out, and it fought the exact-tint work below.

**2. The shard colours were wrong** — and the cause is worth remembering. The stack prefabs are
tinted white (`m_Color 1,1,1,1`); **all colour lives in the sprite texture**, so there is no Color
field anywhere to read. The shard colours had been inherited from `FractureObject`'s ten named
Color fields, which had drifted from the art. The project had TWO parallel colour tables that
disagree on id order:

    FractureObject     blue=0 crimson=1 green=2 pink=3 midPink=4 darkPink=5 purple=6
                       midPurple=7 orange=8 yellow=9
    PieceColorPalette  blue=0 green=1 orange=2 pink=3 purple=4 red=5 yellow=6
                       (dead — referenced by nothing)

The art filenames lie too: `Mid Pink 1X.png` is actually purple `#A46ACD`, and `Purple 1X.png` is a
light lavender `#C49CCF`.

Fixed by sampling the pixels instead — new `PieceTintSampler`. It blits the sprite through a
RenderTexture (so it works on textures **without** Read/Write enabled, which is all of them),
histograms the opaque pixels ignoring the black outline and soft rim, and takes the most common
bucket. The blocks are a light top face over a bevel band and dark base, and the top face is ~59% of
the sprite, so the mode lands on the body colour every time. Cached per Sprite.

`MatchResolver` samples the piece at the *start* of `CollapseThenBurst`, while the GameObject still
exists, and passes the Color into `ShardBurst.Play(..., tint)`. The serialized palette is now only a
fallback.

Verified three ways: an independent read of the source PNGs in PowerShell/System.Drawing, the
in-engine sampler (identical hex for all 10 families), and a rendered strip of each sprite next to
its sampled swatch. Plus a 4-frame filmstrip at t=0.10/0.40/0.65/0.90 confirming the longer
hold-then-shrink tail with the tint held constant.

Found along the way: **`Pink` (3) and `MidPink` (4) use the same sprite** and sample identically.

### 2026-08-25 — Board drag felt chunky/cell-by-cell: rewrote BoardInputController around a continuous anchor

- **Goal:** user reported that dragging a stack doesn't move like a free object — it slides
  cell-to-cell instead of following the finger, mild on horizontal/vertical, blatant on diagonals.
  Wanted free movement in every direction with no friction or stepping.
- **Status:** done (code + compile verified; NOT yet play-tested — see Next)
- **Changed:** `Assets/Scripts/Puzzle/Input/BoardInputController.cs` — rewrote the drag core.
  The piece now lives at a **continuous** `Vector2 freeAnchor` (cell units) instead of being
  parked on a cell centre and `SmoothDamp`ed toward it. Four separate causes were fixed:
  (1) the visual target was always `CellCenterWorld(anchor)`, so pointer motion inside a cell
  produced *zero* movement and a boundary crossing produced a full-cell lurch;
  (2) collision consumed the **entire X delta before looking at Y**, so a diagonal drag traced an
  L-shaped staircase and a "Y-first" diagonal stalled outright — now resolved per axis in
  ≤0.25-cell sub-steps, with a legal-diagonal fast path and independent per-axis wall clamps, so
  the piece glides along a wall instead of snagging;
  (3) **real bug** — pointer→cell divided by `CellSize` instead of `CellPitch`
  (`cellSize + cellPadding`); with the authored `1.086 / 0.055` that is a ~5% error *per cell*,
  so the piece fell almost half a cell behind the finger across the board;
  (4) pickup teleported the piece to the cell centre and kept only a whole-cell grab offset —
  now `grabOffsetLocal` preserves the exact sub-cell grab point in board-local units.
  Also: pointer→world now uses a proper `Plane.Raycast` (correct under perspective/rotated board);
  pickup takes the anchor from `piece.Anchor` instead of the old fallback that clamped
  `Vector2Int.zero` and could teleport a piece to the bottom-left corner; release eases onto the
  cell centre over `settleDuration` (0.07s) instead of popping up to half a cell.
  Removed the now-meaningless `smoothTime` / `maxSpeed` fields.
- **Changed:** `Assets/Documentation for scripts/BoardInputController.txt` — rewritten to match.
- **Scene/Prefab/SO edits:** none. 24 scenes still carry stale `smoothTime`/`maxSpeed` YAML lines;
  Unity ignores serialized fields that no longer exist, so nothing breaks. `liftWhileDragging`
  (`-0.5` in scenes) was deliberately kept under the same name.
- **Verified:** full recompile through Unity MCP, **0 errors**, no warnings from the changed file.
  Not play-tested — Play mode was not entered this session.
- **Gotchas:**
  - `PieceDragHandlerSimple.cs` looks like the drag path but has **zero** scene/prefab references
    (verified by GUID search across `Assets/**`). It is dead. `BoardInputController` is the only
    live one, present in all `Level_1_Stage_*`, `Stage0/1` and `Tutorial_Board_01`.
  - **Match resolution timing moved.** `MatchResolver.ResolveFrom` now fires at the *end* of the
    release settle (~70 ms after the pointer lifts), not on the release frame. `CompleteSettle()`
    is force-called at the top of `TryBeginDrag` so a fast second tap can never see stale
    occupancy. Set `settleDuration` to 0 to get the old instant behaviour back.
  - The whole design rests on one invariant: `freeAnchor` is only ever assigned a value that
    `IsFreeAnchorLegal` accepted. `RoundToInt(freeAnchor)` therefore always lands on an already
    proven-legal corner anchor — that is why `lastValidAnchor` needs no check of its own.
  - `maxSubStepCells` **must stay ≤ 0.5**, or a fast flick can skip an integer bracket entirely
    and tunnel through an occupied cell.
- **Round 2 (same day), after the user play-tested `Tutorial_Board_01`:** movement confirmed smooth,
  but three real problems remained. All fixed:
  - **Pieces jammed at the mouth of a gap and the pointer ran away.** With a genuinely free 1:1
    drag, a corridor exactly as tall as the piece requires holding the perpendicular axis at a
    float-exact whole cell — impossible by hand, so the piece stuck. Added a **gap assist**
    (`ResolveAxisAssisted`): when the axis being pushed is walled, retry after pulling the other
    axis onto its nearest cell line (≤ `gapAssistCells`, default 0.4). The pulled axis is
    lane-locked for the rest of the frame or the perpendicular resolve fights it sub-step by
    sub-step, which showed up as jitter.
  - **Pointer ran ahead of a blocked piece forever.** `ClampPointerOvershoot` bleeds excess out of
    `grabOffsetLocal` so at most `maxPointerOvershootCells` (0.5) of slack accumulates.
  - **Wall shiver.** `SnapNearWhole` (1e-4) in `IsFreeAnchorLegal` and after each move; rounding
    switched from `Mathf.RoundToInt` (half-to-EVEN — snapped left or right by cell parity, looking
    random) to explicit round-half-up.
- **Round 3 — the release snap. TRIED AND REVERTED.** User asked to fix "it snaps somewhere else
  when I let go". This is NOT a bug: with a 1:1 drag the piece can be half a cell from any centre
  at release and must land on a whole cell, so it always travels; `lastValidAnchor` is always the
  nearest legal cell. Offered four remedies; user picked a **landing preview ghost** (translucent
  square on each cell the piece would land on). Built it, user play-tested it, called it bad, and
  asked to revert. **The ghost is fully removed** — code, fields and docs — back to the round-2
  state they had approved. Movement itself was never touched by round 3, so nothing they liked was
  lost. Reverted surgically rather than via git, because nothing this session is committed and a
  `git checkout` would have thrown away rounds 1 and 2 as well.
  **The release snap therefore still exists and is unaddressed.** Remaining untried options:
  light magnetism toward the nearest legal cell while dragging, or simply raising `settleDuration`
  (0.07 → ~0.15) so the travel reads as motion instead of a teleport — that one needs no new code.
  If a preview is ever rebuilt: do NOT `Instantiate` the piece to make it (brings `PieceSimple`,
  which claims a second `pieceId` and occupies real cells) — generated quads only.
- **Changed (round 2/3):** `Assets/Scripts/Puzzle/Pieces/PieceSimple.cs` — `SnapSubBlocksToOffsets`
  spaced sub-blocks at `offset * CellSize` while the grid steps by `CellPitch`. **This was the
  deferred item below and I had it wrong: it is not cosmetic.** The authored prefabs are already
  correct at pitch spacing (verified: `Cell_1_0.localPosition.x == 1.1410 == pitch`), and
  `PieceShapeLayout` also uses pitch — so this ran inside `TryPlace` during `Start` and *corrupted*
  the authored layout at runtime, also drifting the collider centres
  `TrySolveAnchorFromChildren` reads back. Now pitch for SPACING, `CellSize` for collider SIZE.
  `CollectChildCells`' fallback divisor fixed the same way.
- **Verified (round 2/3):** compiles clean, 0 errors. Confirmed via Unity MCP in Play mode that
  sub-blocks now sit at `/pitch = 1.0000` at runtime (they would have been `1.0506` before).
  The gap assist and the ghost are NOT play-tested by me — the user tests those.
- **Round 4 — the release snap, properly fixed.** User restated the requirement precisely: *wherever
  I release the stack it must sit there, movement free in every direction, and all existing rules
  keep working.* Diagnosis: **most of the snap was my own regression from round 2**, not the
  inherent half-cell rounding. Three things stacked up to move the piece off the finger before
  release: the gap assist yanking it up to **0.4 cell** sideways while the finger stayed put;
  `ClampPointerOvershoot` **mutating `grabOffsetLocal`** every blocked frame, cumulatively across
  separate blocking events; and only then the ≤0.5 cell rounding. Worst case ≈ a full cell.
  - **Deleted `ClampPointerOvershoot`.** `grabOffsetLocal` is now fixed for the whole drag, so the
    piece is rigidly attached to the grab point. A piece shoved into a wall separates and
    re-attaches exactly on the way back.
  - **`gapAssistCells` 0.4 → 0.15.** It displaces the piece without moving the finger, so it must
    stay small; magnetism does its job properly now.
  - ~~**Added detent magnetism (`magnetStrength`, default 0.6)**~~ — **WRONG, REVERTED SAME DAY.
    Do not re-introduce in any form.** Per axis,
    `k = Lerp(1,4,strength)`, `f<0.5 → 0.5*(2f)^k`, else `1-0.5*(2(1-f))^k`. Flat near a cell,
    steep only at the boundary: the piece rests on a cell while the finger crosses most of it, so
    at release there is essentially nothing left to travel. Tracking stays 1:1; applied per axis
    so movement is still free in **every** direction. `k==1` is the identity, so strength 0 is
    genuinely today's free movement.
    Curve choices rejected with numbers: plain Lerp-to-centre (rubbery, constant lag);
    `Mathf.SmoothStep` (too weak — at 0.3 across a cell it leaves the piece 0.25 away vs 0.12).
    **Why it was wrong:** bending the position toward cells *is* cell-to-cell movement — the
    piece visibly lingers on a cell then jumps the boundary, which is exactly the stepped,
    robotic feel the whole rewrite exists to remove. The user tested it and rejected it
    immediately and correctly. **Any** position-bending scheme contradicts the requirement
    ("like moving your finger through the air"). Removed entirely.
  - **Fixed a real teleport bug:** `EndDrag` fell back to `p.Anchor` when `TryPlace` failed, but
    `PieceSimple._anchor` can hold a never-validated value (`AutoBuildOffsetsFromChildren` assigns
    it directly and `BoardBootstrapper` calls that unconditionally). Now falls back to
    `dragStartAnchor`.
  - `settleDuration` default 0.07 → 0.10.
- **Why not remove the grid entirely** (the user asked): `MatchResolver.FindConnectedIdenticalGroup`
  builds the 4-neighbour cell set around the footprint and reads `board.GetOccupant(nCell)` — the
  "any part touching counts" rule **is** the integer occupancy grid, as are `BoardGhostMask`,
  `PuzzleMoveBudget`, `TutorialBoardHints` and the win check. Continuous resting positions would
  break the very rules the user asked to keep. Hence "already be on a cell when released" instead.
- **Reference videos** (`Assets/Arts/Reference videos/`): frames pulled with ffmpeg.
  `ScreenRecorderProject491.mkv` — every *resting* frame is grid-aligned; the off-grid positions at
  t≈18.4/27.5/28.9 are pieces mid fly-to-merge. `Stack movement.mp4` — pieces there carry ↔/↕
  arrows and are locked to one axis; **user explicitly said those are special blocks and NOT the
  general rule — his blocks must move freely in any direction.** Do not restrict axes.
- **Round 5 — real no-snap, behind a toggle.** After the magnetism was reverted, the user chose
  "show me both". Added **`restExactlyWhereReleased`** (default OFF) on `BoardInputController`:
  - ON: `EndDrag` leaves the transform's X/Y completely untouched (only the drag lift is dropped)
    and books the piece onto **every cell its body overlaps** via the new
    `PieceSimple.TryPlaceExact(anchor, cells, snapRootToAnchor:false)`. Reserving the overlap —
    not just the rounded cell — is what stops a straddling piece from visually overlapping the
    next piece placed beside it, and keeps the occupancy grid truthful so matching, blocked cells
    and the move budget need no rule changes. **Cost: an off-grid drop consumes 2 cells, so the
    board fills faster.**
  - `MatchResolver.FindConnectedIdenticalGroup` now builds its 4-neighbour set from
    `piece.OccupiedCells` (new accessor for `_lastOccupied`) instead of `anchor + shapeOffsets`,
    via a new `BuildCellNeighbors4` overload — a straddling piece would otherwise probe the wrong
    cells and miss matches. The user's "any part touching counts" rule is unchanged.
  - `TryBeginDrag` now seeds `freeAnchor` from the piece's **actual transform**, not its
    whole-cell anchor; otherwise touching an off-grid piece teleported it back onto its cell.
  - OFF: unchanged behaviour (ease onto the nearest cell over `settleDuration`).
- **Round 6 — no-snap matched across a visible gap; fixed with a geometric touch test.**
  User likes no-snap mode but reported pieces matching with a one-or-two-cell visible gap.
  **Cause, and it was a real flaw in round 5:** a piece resting at x=3.4 reserves cells 3 AND 4
  (correct, that is what stops visual overlap) while its body only spans 3.4→4.4. The BFS then
  expanded one *more* cell out from that inflated reservation, so match reach became
  `body + up to 1 cell inflation + 1 cell neighbour search`. Using the reservation for collision
  is right; using it for matching is not.
  - The cell sweep is now only a **candidate filter** (over-reaching is fine for a broad phase).
    Each candidate is confirmed by **`MatchResolver.ArePiecesTouching`**: each sub-block is a 1×1
    box in continuous cell units, and two boxes touch when they overlap on one axis by
    ≥ `minTouchOverlapCells` (0.2) and are separated on the other by ≤ `touchTolerance` (0.05).
  - New `BoardGridXY.WorldToContinuousAnchor(world)` — the fractional version of `TryWorldToCell`.
  - **For grid-aligned pieces this reduces exactly to the original 4-neighbour rule**, so snap
    mode is unchanged. Verified numerically over 7 cases (aligned/gap/diagonal/straddling/pushed).
  - Toggle `requireGeometricTouch` (default ON) if it ever needs disabling.
  - **Consequence:** a straddling piece reserves the cell it partially covers, so a neighbour can
    never sit flush against it *on that axis* — in no-snap mode you generally must PUSH pieces
    together to match. Collision parks a pushed piece exactly on the whole-cell boundary, which is
    a true touch, so pushing always works.
- **Next:** play-test `Tutorial_Board_01` and flip `restExactlyWhereReleased` in Play mode to
  compare. Watch specifically for: pieces visually overlapping (should be impossible), matches
  still firing on a single-cell touch, and whether the 2-cells-per-off-grid-drop cost makes levels
  unsolvable. Also re-check `Level_1_Stage_5` — circle drag, diagonal into a corner, fast flick
  into an obstacle, the corridor pass, and the move budget.

### 2026-08-24 — HP bars: enemy bars were mirrored in Play mode, plus a yellow damage trail

- **Goal:** two things, asked in order. (1) "Why are the enemy HP bars reversed?" — answer first,
  change nothing. (2) Make damage read as a *size* instead of a teleport: add a second bar in a
  different colour that holds the pre-hit health, drains away and fades, for players AND enemies,
  in the UI as well as in code.
- **Status:** done — but **nothing ran**. The Unity Editor was CLOSED for this entire session.
- **Commits:** none — working tree only.

**(1) Why the enemy bars were mirrored — root cause, verified by reading the prefab YAML.**
The enemy prefabs and `HealthBar` were each correcting the SAME flip, so the corrections stacked
to an odd number. Every one of the 6 prefabs in `Assets/PREFABS/Characters/New Characters/Enemies/`
is authored identically:

| object | rotation | scale.x |
|---|---|---|
| root (e.g. `Enemy_Orc`) | identity | **-1** (art faces left) |
| `PlayerProgressBarUI (1)` (Canvas + `HealthBar`) | **180° on Y** | +1 |
| `Bar (1)` — the Image wired to `healthBar` | **180° on Y** | **-1** |
| `BG (1)` | **180° on Y** | **-1** |
| `Bar` / `BG` (originals) | — | — **disabled**, left in place |

Four inversions cancel to "upright", which is why the bar looked CORRECT in the Prefab/Scene view.
The old `LateUpdate` then added a fifth at runtime — it mirrored itself whenever the PARENT's
`lossyScale.x` was negative, which on an enemy root is always. Hence: mirrored in **Play mode only**,
draining left→right against the player's right→left. And because that code forced the Canvas sign
to equal the root sign, the parity never changed with facing — it was wrong whichever way the enemy
turned. Player prefabs are clean (identity all the way down) and were never affected.

- **Changed:** `Assets/Scripts/Combat/Player/HealthBar.cs`
  - `LateUpdate` split into `TickDamageTrail()` + `KeepUnmirrored()`.
  - `KeepUnmirrored()` rewritten: it now measures the **fill Image's own world axis**
    (`healthBar.transform.localToWorldMatrix.MultiplyVector(Vector3.right).x`) and only corrects
    when that points along world -X. Two reasons over the old `lossyScale` read — `lossyScale`
    cannot see a 180° rotation, and probing the IMAGE (not this transform) accounts for flips
    authored BELOW the Canvas. Early-outs at ~0 (bar edge-on) and when the Image is not a
    descendant (it could not fix what it measures, and would flip-flop forever).
    Side effect: player bars now measure positive and are **never written to at all**; the old
    version rewrote every bar's localScale every frame.
  - **New damage trail.** `delayedBar` Image renders one sibling slot BEHIND the main bar.
    On a hit the main bar snaps to the real value while the trail keeps the pre-hit one, holds
    `trailHoldSeconds` (0.18) dead still, drains at `trailDrainPerSecond` (0.9 of a full bar/sec,
    so a 20% chip ≈ 0.22s and a big hit visibly takes longer), then fades over `trailFadeSeconds`
    (0.12). Colour `delayedColor` = 1.0/0.82/0.15 yellow, forced in `Awake` so no prefab drifts.
    Heal/unchanged snaps it up and hides — a chunk must never be stranded ABOVE real health.
    `trailSeeded` treats the FIRST `SetCurrentHealth` (from `EnemyStats.Start` / `PlayerStats.Start`)
    as the spawn value, not a hit, so nothing flashes on frame one. Scaled `Time.deltaTime` on
    purpose: the chunk should freeze with the pause menu, not finish behind it.
  - `BuildTrail()` — creates that Image at Awake by CLONING the main bar (see Decisions).
    Hard stop included: if the fill Image shares its GameObject with a `HealthBar` (the
    `GetComponent<Image>()` fallback), cloning would clone this script and the clone's Awake
    would clone again forever. Logs a warning and bails.
  - Corrected the stale `// fill from right → left` comment on the `fillOrigin = Left` line.
- **Changed:** `Assets/Documentation for scripts/HealthBar.txt` — PURPOSE, the new fields, the
  rewritten `LateUpdate`/`KeepUnmirrored`, and `SetCurrentHealth` / `TickDamageTrail` /
  `BuildTrail` / `HideTrail`. The full prefab-parity explanation above is recorded there too.
- **Scene/Prefab/SO edits:** **none.** No prefab and no scene was touched — that is the point of
  building the trail at runtime.
- **Verified:** **NOT VERIFIED.** Unity was closed the whole session (`Temp/UnityLockfile` dated
  2026-08-21), so neither change has ever run, and the code is not even compile-checked.
  Note for the next session: `Unity_GetConsoleLogs` returned `success` with an empty log list
  while the Editor was closed — that empty result is NOT evidence of a clean compile. It was
  briefly reported as such in this session before `Unity_RunCommand` failed with
  "Unity not detected" and exposed it.
- **Gotchas:**
  - The mirroring is a **Play-mode-only** symptom by construction. Comparing the Scene view
    against Play mode is the fastest way to confirm the fix; they should now agree.
  - The disabled `Bar` / `BG` duplicates sitting in every enemy prefab are the fingerprint of
    someone previously fighting this same flip by hand. Left alone deliberately.
  - `PlayerStats.Start` passes `maxHealth` but `ApplyDamageToPlayer` passes
    `PlayerManager.statsBase.maxHP`. If those differ, the first hit changes the denominator, so
    the fill jumps for a reason that is not damage and the trail shows a chunk of the wrong size.
    The trail did not cause this — it makes it visible. `EnemyStats` uses `maxHealth` in both.
- **Next:** Play a stage and confirm both at once (enemy bar drains right→left; yellow chunk peels
  away behind it). Then decide whether the gate bars should keep the trail they now get for free.


### 2026-08-24 — Summon arrival: the pre-landing ground telegraph (disc → ring)
- **Goal:** the user watched the previous session's result against the reference clip and said
  it was still off. Their correction: **before** the unit lands there is a full filled circle;
  it then empties from the centre outward into a ring, and the ring fades. They asked for the
  circle ONLY — explicitly no changes to the other VFX layers.
- **Status:** done and verified by capture. Still never run in Play mode.
- **Commits:** none — working tree only.

- **The user was right and the previous session's reading was wrong.** The 2026-08-23 entry
  states "there is no pre-glow telegraph". That is incorrect — it was concluded from the landing
  frames without checking the run-up. Re-examined `ScreenRecorderProject486.mkv` frame by frame
  on the bee summon (lands on **f17**):
  `f14-15` nothing → **`f16` filled white ellipse pops in while the bee is STILL AIRBORNE** →
  `f17-19` holds filled (~0.13s) → `f20-21` centre opens into a ring (~0.07s) →
  `f21-25` ring expands and fades (~0.15s) → `f26` gone. **Total ~0.30-0.35s.**
  It is WHITE (not the pillar's gold), an ELLIPSE, and its outer edge grows only ~1.3x —
  far less than the landing burst's ring.

- **Key design point:** filled disc and ring are NOT two states. They are one animated value —
  the shader's `_InnerRadius`, 0 = filled disc (the inner smoothstep evaluates to 1 everywhere),
  ~0.78 = thin ring. There is no "disc mode" branch. This is also why it is a procedural shader
  and not a texture: a texture bakes ONE fixed inner radius, so the morph would need a flipbook.

- **New:** `Assets/Arts/Shaders/SummonGroundCircle.shader` — textureless additive annulus with
  `_InnerRadius` / `_OuterRadius` / `_Edge` / `_Alpha`.
- **New:** `Assets/Scripts/Combat/VFX/SummonGroundCircle.cs` — MeshRenderer + generated quad,
  three explicit phases (fill hold / open / expand+fade), all tunable. Public `Seek(t)` so the
  effect can be scrubbed frame by frame against the reference instead of caught live at 30fps.
- **Changed:** `SummonVfxAssets.cs` — added `GroundCircleMaterial` and `UnitQuad`.
- **Changed:** `SummonVfxDirector.cs` — `groundCircleEnabled` / `circleLeadTime` (0.05) /
  `circleDiameter` (0.9) / `circleTint` (**white**), a small circle pool, `PlayGroundCircle()`.
- **Changed:** `SummonArrivalBinder.cs` — an `Update()` poll that fires the telegraph at
  `TimeUntilLanding <= circleLeadTime`, latched once per jump.
- **Changed:** `Assets/Scripts/Combat/Player/SimpleJump2D.cs` — added read-only
  `LandingPosition` and `TimeUntilLanding`.
- **Docs:** new `SummonGroundCircle.txt`; updated `SummonVfxAssets.txt`,
  `SummonVfxDirector.txt`, `SummonArrivalBinder.txt`, `SimpleJump2D.txt`.
- **Project settings:** `Blasty/SummonGroundCircle` added to Graphics > Always Included Shaders.
- **Scene/Prefab/SO edits:** none; scene left unsaved and not dirty.

- **Verified:** 0 compile errors, shader compiles clean. Captured a 6-step scrub of the circle
  over a green board stand-in: filled ellipse → centre opens → ring → expands → fades, matching
  the reference lifecycle. Also captured the circle COMBINED with the landing burst at correct
  relative timing (circle starts 0.05s before the burst) — the four composite frames line up
  with reference f16 / f17-18 / f20 / f21-22.
- **NOT verified:** anything in Play mode. In particular the LEAD TIME is unproven — the disc is
  placed at `LandingPosition` from a polled threshold, and whether 0.05s reads correctly at real
  frame rates has never been seen.

- **Gotchas:**
  - `Update()` here is a deliberate poll, and it is the only one in the feature. "A moment BEFORE
    landing" is not an event the jumper can raise — it is a threshold on remaining flight time.
    Everything else still hangs off `Jumped`/`Landed`.
  - The **landing burst still has its own `GroundRing`**, untouched as instructed. In the composite
    captures it hides under the flame base rather than reading as a second ring, but if a double
    ring ever shows up that is where it comes from — the burst's ring, not this telegraph.
  - `Unity_RunCommand` **blocks `System.Reflection`**, which is why `SummonGroundCircle.Seek()`
    is public API rather than the preview harness poking at privates.
  - `sed` with `\n` inside a replacement expands to REAL newlines and will split a C# string
    literal across lines — it broke a `[Tooltip]` mid-edit. Keep inserted tooltips single-line.

- **Next:** play-test a real arrival and tune `circleLeadTime` / `circleDiameter` against the
  clip. Then decide whether the burst's own `GroundRing` should be turned off now that the
  telegraph owns the ground read.


### 2026-08-23 — Summon arrival VFX (Ludus-style light pillar), two backends
- **Goal:** copy the summon effect from a reference clip of the game *Ludus*
  (`Assets/Arts/Reference videos/ScreenRecorderProject486.mkv`). User asked for VFX Graph, with a
  fallback for low-end devices.
- **Status:** done for the ParticleSystem backend (built, rendered and visually verified in the
  Editor). The VFX Graph backend compiles but its `.vfx` asset does NOT exist yet — a build recipe
  was written instead. Nothing has run in Play mode.
- **Commits:** none — working tree only.

- **Read the reference properly first, and it overturned the read from the stills.** The clip is
  118 frames @30fps. One summon is ~14 frames ≈ **0.45s**: the unit FALLS IN along an arc (tilted,
  or Y-stretched ~1.8x) with a warm trail → lands, squashes, white flash + expanding ground ring →
  a yellow→white flame column **erupts UPWARD from the ground**, ~1 cell wide and 2.5–3 tall, with
  a torn flickering top → narrows and fades upward. There is **no sky-beam**, and the unit is
  **not** dissolved into existence — it lands and the pillar is the consequence.
  **CORRECTED 2026-08-24: the claim that there is "no pre-glow telegraph" was WRONG.** There
  IS one — a filled white disc one frame before touchdown that opens into a ring. It was missed
  by reading only the landing frames and never the run-up. See the 2026-08-24 entry.

- **Key finding: most of this already existed.** Do not build a second fall animation. The gate
  Animator's `"Throw"` → `UnlockCurrentWaveViaAnimation` → `JumpThenSwitch` →
  `FrogJumpTransformOnly.TriggerJumpTo(laneY)` chain already IS the arced toss-in, shadow and apex
  scale included. Only the three VISUALS were missing. The user's "locked until it lands"
  requirement also needed **zero code**: `PlayerWaveManager.cs:1024` already withholds
  `PlayerPursueTargetState` until the jump finishes and `WaitForCombatLive()` passes.

- **Changed:** `Assets/Scripts/Combat/Player/SimpleJump2D.cs` — added `public event Action Jumped`
  (end of `BeginJump`) and `Landed` (end of `Land`). Only edit to an existing combat script.
- **Changed:** `Assets/Scripts/Combat/Spawning/PlayerWaveManager.cs` — one `AddComponent` in
  `SpawnUnitAt`, the single funnel both waves and reinforcements already share.
- **New:** `Assets/Scripts/Combat/VFX/` — `ISummonEmitter`, `SummonVfxAssets`,
  `SummonEmitterParticles`, `SummonEmitterVfxGraph` (whole file behind `#if SUMMON_VFX_GRAPH`),
  `SummonVfxDirector`, `SummonArrivalBinder`.
- **New:** `Assets/Arts/Shaders/SummonAdditive.shader`.
- **Docs:** one `.txt` per new script, plus `SummonPillarVFX-Recipe.txt` (the node-by-node graph
  build), and `SimpleJump2D.txt` / `PlayerWaveManager.txt` updated.

- **Project settings edited (the diff shows these, but they were made over MCP):**
  installed `com.unity.visualeffectgraph@17.3.0`; added `SUMMON_VFX_GRAPH` to Scripting Define
  Symbols for Android, Standalone and iOS; added `Blasty/SummonAdditive` to
  **Graphics > Always Included Shaders**.
- **Scene/Prefab/SO edits:** none. No director was placed in any scene and no scene was saved —
  `SummonVfxDirector.Instance` self-creates, so the effect works without one.

- **Verified:** Editor only, via `Unity_RunCommand` + `Unity_Camera_Capture` — emitters built in
  edit mode, `ParticleSystem.Simulate` frozen at set times, rendered through the Main Camera and
  looked at. Confirmed: 0 compile errors; `Jumped`/`Landed` exist; `SummonEmitterVfxGraph` compiled
  into `Assembly-CSharp` and implements `ISummonEmitter`; the pillar renders as an additive flame
  with a white-hot core, stays anchored to the ground through the jet, reaches the requested height
  and fades upward. **NOT verified: anything in Play mode.** No real arrival, no trail, no binder,
  no pooling, no device build.

- **Gotchas — three real traps, all found by capture, not by reading:**
  1. **Never configure `Universal Render Pipeline/Particles/Unlit` from script.** Its blend state
     is applied by URP's material EDITOR, not by `_SrcBlend`/`_DstBlend`, so a runtime-built
     material draws every particle as an **opaque white square** whatever the texture alpha says.
     That is the entire reason `SummonAdditive.shader` exists. It is reached only via
     `Shader.Find`, so it MUST stay in Always Included Shaders or it strips out of an Android
     build and the effect silently goes flat.
  2. **All three `velocityOverLifetime` axes must share one curve mode.** A constant X with a
     curve Y makes Unity reject the module and log *"Particle Velocity curves must all be in the
     same mode"* on every emit.
  3. **A pillar must be a JET, not a burst.** `Emit(count)` launches everything on one frame, the
     cloud rises together, and the column visibly **peels off the ground**, leaving a gap at the
     unit's feet. Feeding continuously (`main.duration` = jet duration + `rateOverTime`) keeps the
     foot lit while the head climbs. The recipe warns about this for the graph too.
  - Also: `ParticleSystemRenderer.pivot.y` is **+0.5** to shift a quad UP — the opposite of what
    the name suggests. And `StarterScene` contains **no `PlayerWaveManager`**, so a real arrival
    cannot be tested there.

- **Next:** play-test a real wave arrival (trail timing, flash landing on the exact frame, sorting
  against the board and the unit sprite); then build `SummonPillar.vfx` from the recipe and compare
  the two backends via the director's `Backend` enum.

### 2026-08-23 — Reinforcement gate pose + hidden HP bars, and the new 80% "last stand" gem offer
- **Goal:** four things, in order. (1) Explain why the Heroes-panel buy-back button sat with
  `interactable` off and re-disabled itself when ticked by hand. (2) Make a bought hero LAND ON
  THE GATE and pause before it jumps in, instead of spawning and leaping the same frame.
  (3) Keep its HP bar hidden during that pose. (4) NEW MECHANIC: when 80% of the whole army is
  dead, offer one designer-chosen hero for gems, reusing the exact same arrival.
- **Status:** partial. All code done and compiling; the scene is wired except one field and was
  **NOT saved by me**. Only the replay BUG was ever seen in Play mode — no intended path is verified.
- **Commits:** none — working tree only.

- **Answered, no code (task 1):** the button is not broken. `HeroStatsPanel.Refresh()` writes
  `cell.SetAffordable(gems >= cell.GemCost)` → `buyButton.interactable` on every roster/currency
  change, so a hand-ticked checkbox is overwritten within a frame. The click itself most likely DID
  fire, failed `TrySpendGems`, and that failure path calls `Refresh()` — which is what switched it
  back off. Also: the `200` on screen is not hardcoded anywhere; it is
  `gemsPerHero (50) × squadSize (4)` at `Assets/Scripts/Combat/Spawning/PlayerWaveManager.cs:185`,
  because no `UnitDefinitionSO` has `respawnGemCost` set.

- **Changed — gate pose before the jump (task 2):**
  - `Assets/Scripts/Combat/Spawning/PlayerWaveManager.cs` — new `reinforcementGateHold` (0.75s,
    0 = old instant behaviour) and `HoldOnGateThenJump(pm, laneY)`. `SpawnReinforcementsRoutine`
    now `ApplyLock(pm, true)` on spawn and hands off to that coroutine, which waits then calls the
    UNCHANGED `JumpThenSwitch`. Started PER HERO, not awaited inline, so the hold overlaps the
    stagger — a squad of 4 deploys in ~1.1s, not ~3.5s.
  - `pm.isUnlocked = true` is deliberately still set AT SPAWN, not after the hold: `HeroRoster`
    gates `AliveCount` on it, so deferring it would keep the cell at `0/N`, keep the buy button up,
    and let the player pay twice for a squad already walking out.

- **Changed — HP bar hidden on the gate (task 3):**
  - `Assets/Scripts/Combat/Player/HealthBar.cs` — new public `SetHiddenOnGate(bool)` + a
    `hiddenOnGate` latch that `ShowForBattle()` now respects. Un-hiding refuses to punch through the
    older battle gate (if `hideUntilBattleStarts` is on and no enemy has appeared, that gate still
    owns the bar).
  - `Assets/Scripts/Combat/Spawning/PlayerWaveManager.cs` — new `SetHealthBarsHiddenOnGate(pm,bool)`
    fanning over `GetComponentsInChildren<HealthBar>(true)`, called at both ends of the hold.
  - WHY a new API was needed: `hideUntilBattleStarts` keys off the FIRST ENEMY APPEARING and is
    resolved once in `Awake`. Reinforcements are bought long after that, so `ApplyBattleGate()`
    hits its early return and can never hide them.
  - Also ANSWERED, no code: yes, heroes already land at 90% (`SimpleJump2D.landedScaleMultiplier`),
    and reinforcements always did — same `JumpThenSwitch` path.

- **Changed — the 80% last-stand offer (task 4):**
  - `Assets/Scripts/UI/HUD/LastStandOffer.cs` — **NEW.** Watches the WHOLE roster; once
    `deadFraction` (0.8) of the starting army is dead it shows a one-time buy prompt for a
    designer-chosen `offeredUnit`. Paying calls `PlayerWaveManager.SpawnReinforcements`, so the
    arrival is tasks 2+3 reused verbatim — no arrival logic is duplicated. Also owns a DOTween
    attention pulse (yoyo 1 → `pulseScale` 0.8, `Ease.InOutSine`, 0.75s per direction,
    `SetUpdate(true)` so a `timeScale = 0` pause does not freeze it mid-breath).
  - `Assets/Scripts/Combat/Shared/HeroRoster.cs` — added `TotalAlive()` / `TotalStarting()`
    (it only tracked per-type counts before).
  - `Assets/Scripts/UI/HUD/HeroStatCell.cs` — added `ShowAsOffer()`, a THIRD look: portrait in
    COLOUR, no count, buy button on. Not `SetAlive(0)` — that is the "wiped out" state and it greys
    the portrait, which is the wrong sell for a prompt inviting a purchase.
  - `spent` is latched on SHOW, not on buy. Latching on purchase would make an ignored offer
    re-fire on every subsequent death, since the ratio only gets worse from there.

- **Changed — BUG I introduced and then fixed:** the offer appeared the instant a REPLAYED stage
  opened (user hit it after winning stage 1 and restarting).
  - ROOT CAUSE: `HeroRoster` is static, `ResetStatics()` runs once per PLAY SESSION not per scene
    load, and **`ClearAll()` had zero call sites** — so the previous battle's `StartingCount`
    survived into the new scene while `TotalAlive()` was still 0. The ratio read "army wiped out".
  - Fixed in BOTH layers on purpose: `HeroRoster` now subscribes `SceneManager.sceneLoaded →
    ClearAll()` (skipping `LoadSceneMode.Additive`), and `LastStandOffer` gained an `armed` flag set
    only by `BattleStartController.OnAnyBattleStarted`.
  - My original guard was IMPLICIT ("TotalStarting is 0 before the battle"), true only on a fresh
    Play session. `HeroStatsPanel` never noticed because it re-snapshots at battle start.

- **Docs:** new `Assets/Documentation for scripts/LastStandOffer.txt`; updated
  `PlayerWaveManager.txt`, `HeroRoster.txt`, `HeroStatCell.txt`, and `HealthBar.txt` — the last was
  fully REWRITTEN (it still described only the fill + counter-flip and none of the render-order,
  battle-gate or `LevelGameManager` behaviour), which clears the HealthBar doc debt listed in Open
  Threads since 2026-08-21/22. No new doc debt added.

- **Scene/Prefab/SO edits — `Level_1_Stage_1.unity`, and READ THIS BEFORE TOUCHING THAT SCENE:**
  - The USER hand-built, under the root `Canvas ` object: `LastStandOffer ` (an empty UI object
    carrying an `Image`) → `OfferCell ` → `Hero avatar` + `Hero  info` (the price pill, has the
    `Button`) → `gem Amount` + `Gem`. They also wired `HeroStatCell` on `OfferCell ` themselves.
  - I then changed four things over MCP, each registered with **Undo**: added the `LastStandOffer`
    component to `LastStandOffer `; wired its `cellTemplate` → `OfferCell `; set `OfferCell `
    **inactive** (it is a template — active means visible from frame 1); and cleared two BROKEN
    (`Missing`, not `None`) `deadOverlay` / `countText` refs left over from the duplicate.
  - **`offeredUnit` is still UNSET** — the feature cannot fire until it is assigned.
  - **I did NOT save the scene.** It was dirty at wrap time. Unsaved, all of the above is lost.
  - `Assets/Scriptable Objects/Spawner/Spawner2.asset` is also modified in the working tree — that
    is the user's, not this session's.

- **Verified:** compiles clean, 0 errors, checked over MCP after every edit. Scene state was read
  back from the live editor (that is how the missing component and the `activeSelf=True` template
  were found). **NOT VERIFIED — nothing on this list has run correctly in Play mode:** the gate
  pose, the hidden HP bar, the 80% trigger firing at the right moment, the purchase, or the pulse.
  The ONLY Play-mode observation this session was the replay bug, i.e. the trigger firing WRONGLY.

- **Gotchas:**
  - **`HeroRoster.ClearAll()` had never been called by anything.** Any future reader of its counts
    would have inherited the same stale-snapshot bug. Now hooked to `sceneLoaded`.
  - **`BattleStartController.BattleIsRunning` cannot be used as a "has this scene's battle started"
    check.** It is static, it DEFAULTS TO TRUE, and the new stage only sets it false in `Awake` —
    so a component enabling before that `Awake` reads the PREVIOUS stage's value. Use the event.
  - `SimpleJump2D` calls `CacheBaseScales()` at EVERY jump start, so `landedScaleMultiplier` 0.9
    **compounds**: two jumps = 0.81, three = 0.729. Harmless now (reinforcements jump once) but it
    will bite anything that re-triggers a jump.
  - `PlayerManager.Start()` sets `bodyType = Dynamic` unconditionally, AFTER `ApplyLock` runs on the
    spawn frame. The gate pose survives only because `PlayerLockState.Tick` re-asserts Static every
    FixedUpdate and the prefabs have `m_GravityScale: 0`. Both must stay true.
  - **Trailing spaces in object names are endemic in this scene**: `Canvas `, `LastStandOffer `,
    `OfferCell `, and the older `Parent `. Every `GameObject.Find` by exact name will miss them.
  - `Heros Stats panel` is INACTIVE in the scene at edit time, so nothing that needs `OnEnable` at
    scene start can live under it.
  - A DOTween loop must be killed BEFORE the `Destroy` of the Transform it drives, or it throws on
    its next step. `Hide()` and `OnDestroy()` both do it.

- **Next:**
  1. **Assign `offeredUnit`** on `LastStandOffer ` and **SAVE the scene.**
  2. Check the `Image` on `LastStandOffer ` — the script never touches it, so if it is a leftover
     white sprite rather than a deliberate backdrop it will sit on the HUD all match.
  3. Play a stage, lose down to ≤20% of the army, and verify the whole chain: offer appears (not
     before), pulse looks right, purchase charges gems, hero poses on the gate with NO HP bar for
     0.75s, jumps to the rear lane at 90%, bar returns.
  4. Re-play a won stage to confirm the replay bug is actually gone.

### 2026-08-22 — Heroes Stats panel: dynamic per-type "alive/total" HUD + gem buy-back
- **Goal:** the new "Heros Stats panel" shown during battle must build its cells DYNAMICALLY
  (one per hero type actually on the field, centred), show `alive/total` per type, and — when a
  type is wiped out — grey the avatar and offer a gem price that respawns the whole squad.
  Mid-session the user added two more requirements: the Feature Panel must fade out fast the
  instant BATTLE is pressed, and the Heroes panel must fade in only after the camera has
  finished its move.
- **Status:** partial. Code done, scene wired and saved by me, and the panel was seen BUILDING
  CORRECTLY in Play mode. The death grey-out and the gem buy-back are still unverified.
- **Changed:**
  - `Assets/Scripts/Combat/Shared/HeroRoster.cs` — NEW. Static per-`unitId` tally of living
    heroes + a frozen `StartingCount` snapshot. `[RuntimeInitializeOnLoadMethod]` reset so it
    survives domain-reload-off. Key rule: a hero only counts once `isUnlocked` is true.
  - `Assets/Scripts/UI/HUD/HeroStatsPanel.cs` — NEW. Clones the authored "Hero 1" cell once per
    type at battle start, sorts by `UnitsDatabaseSO.IndexOf`, forces `MiddleCenter` on the
    layout group, and owns the gem purchase.
  - `Assets/Scripts/UI/HUD/HeroStatCell.cs` — NEW. View only: alive look vs. wiped look.
    Revised later in the session: `aliveTint` became an explicit serialized field defaulting to
    white instead of being CAPTURED from the avatar's authored colour in `Awake()`, and when
    `deadOverlay` is assigned the avatar's colour is never touched at all. See Gotchas.
  - `Assets/Scripts/Combat/Player/PlayerManager.cs` — registers into `HeroRoster` at the end of
    `Start()` (NOT Awake — `SetUnitId` runs after Awake), unregisters in a new `OnDestroy`.
  - `Assets/Scripts/Combat/Spawning/PlayerWaveManager.cs` — extracted `SpawnUnitAt(def,…)` out
    of `SpawnOneAt`; added `CanSpawnReinforcements` / `SpawnReinforcements` / `GetRearLaneY`
    and a `reinforcementStagger` field. Bought squads spawn at the gates and jump into the
    REAR lane, unlocked, then march.
  - `Assets/Scripts/Data/Units/UnitDefinitionSO.cs` — added `respawnGemCost` (0 = use the
    panel's `gemsPerHero × squad size` fallback).
  - Docs: new `HeroRoster.txt`, `HeroStatsPanel.txt`, `HeroStatCell.txt`; updated
    `PlayerManager.txt`, `PlayerWaveManager.txt`, `UnitDefinitionSO.txt`,
    `BattlePhaseTransition.txt`. No doc debt was added this session.
  - `Assets/Scripts/Combat/Spawning/BattlePhaseTransition.cs` — added `fadeOutOnPress` /
    `pressFadeOutDuration` (Feature Panel fades out the instant BATTLE is pressed) and
    `fadeInAfterMove` / `arriveFadeInDuration` (Heroes panel fades in only once the camera
    has finished its move). `CanvasGroup` is added on demand; alpha is set to 0 *before*
    `SetActive(true)` so there is no one-frame flash.
- **Scene/Prefab/SO edits — `Level_1_Stage_1.unity`, done BY ME over Unity MCP and SAVED
  (the diff will look large; this is what changed):**
  - `Heros Stats panel`: added `HeroStatsPanel`, `HorizontalLayoutGroup` (MiddleCenter,
    spacing 24, no child control/expand) and a `CanvasGroup`. Left INACTIVE — the transition
    now owns activating it.
  - `Hero 1`: added `HeroStatCell`, wired avatar → `Hero avatar`, countText → `Hero Count`,
    buyRoot/buyButton → `Parent ` (note the trailing space in that object's name),
    gemCostText → `gem Amount`, deadOverlay → `Gray out Avatar` (the user authored that
    object mid-session; it is a spriteless Image, RGBA 0.109/0.082/0.091/0.58, kept inactive).
  - `Hero avatar` Image colour: **0.325/0.443/0.627 (blue) → white**. That tint was authored
    for the `UISprite` placeholder the object originally held; once `Bind()` swaps in the real
    portrait it painted every LIVING hero into a dark blue silhouette, which read as
    "the grey-out is inverted".
  - `Feature Panel`: added a `CanvasGroup`; moved OUT of `hideWhenBattleStarts` and INTO
    `fadeOutOnPress`. `hideWhenBattleStarts` now holds only `Puzzle Board`.
  - `BattleStartController.hideButtonAfterBattleStarts` turned OFF — the whole panel fades
    now, so popping the button out first read as a glitch.
- **Verified:** project compiles clean; scene wiring read back from the editor and confirmed.
  The USER ran Play mode once: the panel built three cells correctly (`Hero_Valkir3`,
  `Hero_Dark_Oracle_1`, `Hero_Minotaur_2`, counts `1/1`, `2/2`, `1/1`, centred). **NOT verified:
  the grey-out when a type is wiped out, and the gem buy-back / reinforcement spawn.** Neither
  path has ever executed.
- **Gotchas:**
  - **The "grey-out is inverted" bug was NOT inverted logic.** `Hero avatar`'s Image was
    authored blue for the `UISprite` placeholder it originally held. `Bind()` swaps in the real
    portrait but the tint stayed, so every LIVING hero rendered as a dark blue silhouette, and
    `aliveTint` — captured from that same authored colour at `Awake` — faithfully preserved it.
    Diagnosis came from dumping the live values over MCP (`deadTint` was untouched and the
    counts were visible, which only happens in the ALIVE branch), not from the screenshot.
    Lesson recorded in Decisions.
  - Heroes still sitting LOCKED on the castle gates when BATTLE is pressed are stranded there
    forever (the board is hidden, so no match can release them). They are deliberately excluded
    from the roster — counting them would pin the label above zero and the buy-back could never
    appear. If that stranding is itself considered a bug, it is a separate fix.
  - `BattleStartController.BattleIsRunning` DEFAULTS TO TRUE for stages with no
    `BattleStartController`, so the panel defers its first build to `WaitForEndOfFrame`.
  - `HeroStatCell` hides the count with `countText.enabled = false`, not `SetActive`, in case
    the "Parent" buy button is authored as a child of the "Hero Count" label. (It turned out to
    be a SIBLING, but the guard costs nothing.)
  - The child object is literally named `"Parent "` — with a trailing space. Any lookup by exact
    name will miss it.
  - Scene edits cannot be made while the editor is in Play mode. I called
    `EditorApplication.ExitPlaymode()`, which also RESTORED the pre-Play scene (StarterScene),
    so the stage scene had to be reopened before the fix could be applied. I left the editor on
    **StarterScene** at the end, since that is where Play must start.
- **Next:**
  1. Play `Level_1_Stage_1` from StarterScene and let a whole hero type die. Confirm
     `Gray out Avatar` switches on, the count hides, `Parent ` appears with its price, and that
     paying spawns the squad at the gates and marches it in.
  2. Set the real gem price — `gemsPerHero` on the panel (currently 50) or `respawnGemCost`
     per `UnitDefinitionSO`. The reference art shows 200.
  3. Only `Level_1_Stage_1` is wired. Stages 2-20 have no Heroes Stats panel at all yet.
  4. Always Play from **StarterScene**; booting a stage directly leaves `GameStartManager`
     missing, so stats and the unit database never resolve.

### 2026-08-22 — Level1_Stage01 part 2: jump shadow, gap-fill animation, HP-bar sorting on battle end
- **Goal:** three polish bugs the user found while playing the new puzzle-first flow — the jump
  shadow flashing to the landing spot, gap-filling heroes sliding with no walk animation, and unit
  HP bars drawing on top of the win panel.
- **Status:** done (all three fixed in code; none run in Play mode)
- **Commits:** none — working tree only.

- **Changed — jump shadow flash** (`Assets/Scripts/Combat/Player/SimpleJump2D.cs`):
  - ROOT CAUSE: all 12 character prefabs carry a hand-dragged `shadowProgressCurve` whose FIRST
    key sits at time `-0.104`, value `0.994` (see `Player_Pref.prefab:1503`). At `t01 = 0` it
    evaluated to ~1.0, so the detached shadow was placed on the LANDING SPOT on frame one, then
    walked backwards as the curve dipped to 0.66 at t=0.35. The documented "if the curve is null,
    fall back to t^2" guard was DEAD CODE: **a serialized `AnimationCurve` field is never null in
    Unity** — it deserializes as an empty curve — so `curve != null` was always true.
  - Replaced the `detachShadowDuringJump` bool with a `ShadowJumpMode` enum defaulting to
    `StickToCharacter` (shadow stays parented, script never writes its position). `GroundProjected`
    preserves the old detach-and-slide look. **No prefab edits needed** — the new field is absent
    from all 12 prefab YAMLs, so the C# field initializer applies; the orphan
    `detachShadowDuringJump: 1` keys are inert and vanish on the next prefab re-save.
  - New `ShadowProgress01()` validates the curve (>= 2 keys AND `Evaluate(0) <= 0.05`) before
    trusting it, else falls back to t^2.
  - Fixed a LATENT bug only reachable in `GroundProjected`: the follow lerped to `endPos`
    ABSOLUTELY, discarding the shadow's authored local offset (its under-the-feet placement).
    Because `Land()` re-parents with `worldPositionStays = true`, the loss was permanent after the
    first jump. Both the per-frame path and `Land()` now advance by the travel DELTA.
  - Added `OnDisable -> ReattachShadowIfDetached()`: a unit dying mid-jump used to orphan its
    detached shadow as a root object forever.
  - Deleted three confirmed-dead numbered siblings: `BeginJump1`, `Land1`, `UpdateShadowScale1`.

- **Changed — no walk animation while filling formation gaps:**
  - ROOT CAUSE: `PlayerWaveManager.ApplyLock(pm, false)` only flips `canMove`/`isUnlocked` — it
    NEVER changes the state. The switch to `PlayerPursueTargetState` happens in `JumpThenSwitch`,
    which blocks on `WaitForCombatLive()`. So through the whole puzzle phase heroes are still
    ticking `PlayerLockState`, whose `Tick` runs in **FixedUpdate** and calls `SetAnimMoving(false)`
    every physics step. `FormationGapFiller.WalkTo` set `SetAnimMoving(true)` once before its loop
    and lock state erased it ~2ms later.
  - RULED OUT the animator: `Jump Loop` lives on an Override Layer and exits at `exitTime 0` into
    an empty state carrying `ResetAnimationsBool` (which clears `isInteracting`), so the base-layer
    locomotion blend tree is already visible well before `preMoveDelay` (0.4s) elapses. The
    Horizontal/Vertical blend floats were the right lever all along.
  - `Assets/Scripts/Combat/Player/PlayerManager.cs` — new `[HideInInspector] public bool
    isFormationStepping`.
  - `Assets/Scripts/Combat/Player/States/PlayerLockState.cs` — skips the locomotion/animator
    clobber while that flag is set. Everything else (root motion off, Z clamp, staying in state)
    still runs. Also guarded the `linearVelocity`-on-a-Static-body write here (one of the console
    spam sources in Open Threads) — it now only touches velocity on a non-Static body.
  - `Assets/Scripts/Combat/Spawning/FormationGapFiller.cs` — raises the flag before the walk,
    clears it on arrival. Also closed the race the flag made reachable: the walk loop now aborts
    if the hero leaves lock state mid-step (battle starting mid-walk had the gap filler AND
    `PlayerPursueTargetState` both driving position), and the arrival snap is skipped in that case
    so a marching hero is not yanked back onto its slot.

- **Changed — HP bars drawing over the win/lose/revive panels:**
  - ROOT CAUSE: the `sortingOrder = 500` added on 2026-08-21 to stop bars hiding behind other
    units. It is applied once in `Awake` and nothing ever lowers it, so it outranks every
    end-of-battle panel. A combat-time need expressed as a permanent constant.
  - `Assets/Scripts/UI/WinLose/LevelGameManager.cs` — `CurrentState` converted from an
    auto-property to a backing field whose setter fires a new static
    `event Action<GameState> OnGameStateChanged`, plus a static `IsBattleRunning` helper. All five
    existing `CurrentState = X` assignments are untouched. The gate-death handlers already sit on
    `EnemyGateStats.OnGateDestroyed` / `PlayerGateStats.OnGateDestroyed`, which is exactly the
    signal the user named as authoritative (a gate at 0 HP ends the battle).
  - `Assets/Scripts/Combat/Player/HealthBar.cs` — captures the canvas's AUTHORED
    sortingOrder/layer before overriding, and swaps between authored and 500 as the state changes.
    Restoring the authored value (rather than picking some low number) is deliberate: it is
    provably the configuration that worked before 2026-08-21, so it cannot accidentally still
    outrank a panel. Revive is handled — `NotifyReviveAccepted()` returns the state to `Playing`
    and re-raises the bars, so this is not a one-way trip. A bar whose `Awake` runs AFTER the
    battle already ended resolves `IsBattleRunning` directly instead of waiting for an event.
  - GATE bars are covered for free: `PlayerGateStats` and `EnemyGateStats` both hold a `HealthBar`
    reference, so it is the same class and the same fix.

- **Scene/Prefab/SO edits:** **none.** All three fixes are pure code; no `.unity`, `.prefab` or
  `.asset` was touched. Deliberate — the shadow fix in particular was designed to avoid editing
  12 character prefabs.

- **Verified:** **NOT VERIFIED — nothing was run in Play mode this session.** Diagnosis was done by
  reading source and by inspecting the serialized prefab/controller YAML directly (the broken
  shadow curve keys, the `Jump Loop` transition's `exitTime 0`, `detachShadowDuringJump: 1` on all
  12 prefabs). The `linearVelocity` console error is still visible in the user's screenshot from
  the two sites listed in Open Threads.

- **Gotchas:**
  - **A serialized `AnimationCurve` is NEVER null in Unity.** Any `curve != null` fallback in this
    codebase is dead code — validate the curve's SHAPE instead.
  - `PlayerLockState.Tick` runs in **FixedUpdate**, so anything it writes beats a value another
    system set once outside the physics loop. Check for a lock-state clobber first when an
    animation "does not play" pre-battle.
  - `ApplyLock(pm, false)` does NOT leave `PlayerLockState`. Heroes stay locked for the entire
    puzzle phase; only `JumpThenSwitch` moves them on, and only after `WaitForCombatLive()`.
  - Re-parenting with `worldPositionStays: true` BAKES whatever world position you last wrote into
    the new local offset — a wrong position written once becomes permanent.

- **Next:** see Open Threads. The user has a running list of `Level_1_Stage_1` revisions and is
  working through them one at a time.

### 2026-08-21 — Puzzle-first stage flow: battle gate, jump lanes/formation, unit avoidance, AdMob install
- **Goal:** rebuild `Level_1_Stage_1` around a new loop — puzzle phase first, combat only after
  the BATTLE button — then fix the movement/formation problems that surfaced from it.
- **Status:** partial (core flow works; see Open Threads for what is unverified)
- **Commits:** `c0b9210 jump formation`, `976a0ec سس`, `c74f706 physics`, `d599ca2 Fomations settings`

- **Changed — battle gate / energy:**
  - `Assets/Scripts/Combat/Spawning/EnemySpawner.cs` — added `waitForBattleStart` (default OFF so
    stages 1-20 keep auto-starting) + idempotent `StartBattle()`; `HasSpawnedFirstEnemy` /
    `OnFirstEnemySpawned` plus static `EnemiesHaveAppeared` / `OnAnyFirstEnemySpawned`; optional
    gate-relative spawn box (`spawnRelativeToEnemyGate`, `gateRelativeMin/Max`) because the shared
    `Spawner2` asset holds ABSOLUTE coords that broke when the enemy gate moved to y=14.43.
  - `Assets/Scripts/Combat/Spawning/BattleStartController.cs` (new) — drives the BATTLE button,
    charges the allowance, then releases the spawner. Static `BattleIsRunning` / `OnAnyBattleStarted`.
  - `Assets/Scripts/Core/Progression/BattleEnergyService.cs` (new) — 25 battles per ROLLING 24h,
    global, window opens on first battle. Energy is a PLACEHOLDER (nothing grants it yet).
    `SessionOnly` test mode keeps it in memory only.
  - `Assets/Scripts/Core/SaveLoad/SaveData.cs` + `SaveSystem.cs` — `BattleEnergyState` section,
    `GetBattleEnergy` / `SetBattleEnergy`. Also FIXED a pre-existing migration recursion in
    `LoadInternal()` (it called `Save()` before publishing `_cache`, so the v2 migration never
    persisted and re-ran every launch).

- **Changed — puzzle:**
  - `Assets/Scripts/Puzzle/Board/PuzzleMoveBudget.cs` (new) + `Puzzle/Input/BoardInputController.cs`
    — N moves per level. A move counts ONLY when a piece lands on a DIFFERENT cell; a tap, or a
    drag the board refused, does not. Board stops accepting new pickups when spent.

- **Changed — camera / phase transition:**
  - `Assets/Scripts/Combat/Spawning/BattlePhaseTransition.cs` (new) — on BATTLE, moves
    `Main Camera` + `BackGroundImage` UP by a relative **+6.75** (DOTween) and switches off
    `Puzzle Board` + `Feature Panel`. Spawner is released only on completion, so waves never
    start while the camera is still travelling. An earlier absolute "Battle View Anchor" approach
    was replaced by the relative offset and that object deleted.

- **Changed — jump / formation:**
  - `Assets/Scripts/Combat/Player/SimpleJump2D.cs` — `TriggerJumpTo(worldY)` (lands on an exact
    lane, X preserved); `landedScaleMultiplier` 0.9 eased across the jump so there is no pop on
    landing; duration/arc now SCALE WITH DISTANCE (duration by sqrt of the ratio, arc linearly),
    fixing the first jump looking fired from a cannon (9.4 vs 1.8 u/s → 7.9 vs 2.9).
  - `Assets/Scripts/Combat/Spawning/PlayerWaveManager.cs` — `jumpLanes[]` (wave N → lane N,
    lane 1 = furthest), `holdUntilFirstEnemySpawns`, and a per-wave formation compaction pass.
  - `Assets/Scripts/Combat/Spawning/FormationGapFiller.cs` (new) — 4x4 grid DERIVED from the gate
    stages (X) and jump lanes (Y). Fills holes forward-only, cascading. Reserves a mover's
    destination slot (a `Dictionary`, not a `HashSet`) — without that, a second hero was sent to a
    slot someone was already walking to and the two stacked permanently.

- **Changed — unit avoidance (the largest thread; 5 attempts):**
  - `Assets/Scripts/Combat/Player/PlayerManager.cs` — DELETED both old separation systems
    (`ApplyFriendlySeparation`, `ResolveHorizontalOverlap`, 112 lines + 5 fields). Added
    `HandleRoamForward()`, `ResolveAttackDestination()`, `PickAttackAnchor()`, and hysteresis
    fields. Also fixed FACING being inverted on the gate: `PlayerCastle` has
    `lossyScale.x = -1`, so `FaceLeft` writing LOCAL scale under that mirrored parent rendered
    backwards; it now converts the wanted WORLD sign into the local one.
  - `Assets/Scripts/Combat/Shared/CrowdSeparation2D.cs` (new) — ended up as look-ahead path
    avoidance + a small walking-only personal space. No `LateUpdate`, no push on standing units.
  - `Assets/Scripts/Combat/Shared/AttackSlotRegistry.cs` (new) — per-target attack spots claimed
    once on arrival, fanned on an ARC inside attack range.
  - `Assets/Scripts/Combat/Player/States/PlayerPursueTargetState.cs` — the no-target march used to
    write `linearVelocity` inline, bypassing all avoidance; now calls `HandleRoamForward()`.

- **Changed — health bars / ads:**
  - `Assets/Scripts/Combat/Player/HealthBar.cs` — forces its Canvas to `sortingOrder 500` (all 23
    character prefabs sat at order 1, tied with the sprites, so bars hid behind other units);
    `hideUntilBattleStarts` reveals unit bars when the FIRST ENEMY APPEARS (not on the button press).
  - `Assets/Scripts/Core/Ads/AdManager.cs` — anchored ADAPTIVE banner, load/fail events, retry;
    `Assets/Scripts/Core/Ads/AdBannerSlot.cs` (new) reserves the on-screen strip (uses
    `SetSizeWithCurrentAnchors`, since that panel is stretch-anchored).

- **Scene/Prefab/SO edits (invisible in a git diff):**
  - `Level_1_Stage_1.unity` — created `Jump pos 4` and repositioned all four lanes to
    7.75 / 6.61 / 5.47 / 4.33; added `BattlePhaseTransition` + `CrowdSeparation2D` to
    `LevelGameManager`, `FormationGapFiller` to the `PlayerWaveManager` object, `AdBannerSlot` to
    `Ads Banner panel`; wired `jumpLanes`, `gapFiller`, `columnAnchors`, `rowLanes`,
    `transition`; `moveSpeed` 2→1.6, `jumpWaitTimeout` 0.5→1.5 (a scaled first jump is 0.54s and
    would otherwise have been cut short). Deleted the obsolete `Battle View Anchor`.
    NOTE the user renamed this scene from `Level_1_Stage_0` mid-session.
  - `StarterScene.unity` — added an `AdManager` GameObject (done ADDITIVELY so the user's open
    scene was never touched).
  - **23 character prefabs** — `HealthBar.hideUntilBattleStarts = true`. Gate bars
    (`PlayerGateProgressBarUI`, `EnemyGateProgressBarUI`) deliberately left always-visible.
  - `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` — created, filled with
    Google's TEST app ids (an empty app id crashes an Android build on startup).
  - `Packages/manifest.json` + ProjectSettings — AdMob v11.2.0 and EDM4U v1.2.187, BOTH as git
    URLs; `GOOGLE_MOBILE_ADS` define for Android, iOS AND Standalone.

- **Verified:** almost everything was checked by SIMULATING the exact case and by inspecting the
  RUNNING editor over MCP, not by watching Play mode. Live inspection is what found the real
  causes: the pursue/mover deadlock (`dist to dest 0.046` vs `dist to enemy 1.57`), the mirrored
  `PlayerCastle` scale, and the empty-board state. Frame extraction (ffmpeg) on the user's videos
  identified the stacking and merged-blob bugs. The user visually CONFIRMED: facing on the gate,
  the 90% landed scale, HP bars appearing with the enemies, and the final avoidance behaviour.
  NOT VERIFIED: anything on a real device (no Android build was made, the banner has never been
  seen on a phone), and the "walking in place" report from video 483 — my simulation did NOT
  reproduce it, so that explanation is unconfirmed.

- **Gotchas:**
  - `Camera.main` ortho size is 12.18 and the layout is TALL — screen-half facing logic and
    absolute spawn coords both break when things move vertically.
  - `PlayerCastle` is MIRRORED (`lossyScale.x = -1`). Anything writing `localScale.x` on a child
    of it renders backwards.
  - Editing a scene while the editor is in PLAY MODE silently loses the work; and scene edits made
    during play are reverted on stop. Verify serialized refs by reading them back.
  - The MCP `RunCommand` sandbox rejects `System.Reflection`, and its logger only substitutes
    simple `{0}` placeholders — `{0:F2}` prints literally.

- **Next:** see Open Threads.

### 2026-08-20 — TMP fonts go blank after deleting `Library/`: root cause + bulk Static-bake editor tool
- **Goal:** user reported that every time they delete `Library/`, the game's fonts render wrong and
  they have to manually re-open Font Asset Creator and press "Generate Font Atlas". Explain why,
  then fix it for **all** fonts at once — explicitly not by selecting each asset by hand.
- **Status:** done — tool written, documented, run by the user in the Editor, and committed as
  `ab1297e "Bake Fonts Attlas"`.
- **Root cause (read out of the raw `.asset` YAML, not inferred) — describes the state BEFORE the
  bake; all of this is fixed as of `ab1297e`:** every TMP font asset in this project was
  `m_AtlasPopulationMode: 1` (Dynamic) with `m_ClearDynamicDataOnBuild: 1`. In Dynamic
  mode TextMeshPro treats the glyph table + atlas texture as a *rebuildable cache*: on import it
  re-opens the source `.ttf` face to validate the cached atlas and discards it on failure. Deleting
  `Library/` forces a reimport of every `.ttf`, the validation fails, the atlas is dropped, and the
  only recovery is a manual Generate. Confirmed field-by-field:
  - `Assets/Arts/FONTS/LILITAONE-REGULAR SDF.asset` — Dynamic, clear-on-build on, and
    **`m_SourceFontFileGUID` empty + `m_SourceFontFile: {fileID: 0}`** — a Dynamic font asset with
    no font to be dynamic *from*. It can never self-repair, and with clear-on-build set it would
    have shipped with **empty text in a player build**, not just in the Editor.
  - `Assets/Arts/FONTS/Dangrek/Dangrek-Regular 1 SDF.asset` — Dynamic, clear-on-build on, source
    GUID intact (`cb7b19cc…` → `Dangrek-Regular 1.ttf`). 2048×2048 atlas for ~42 characters.
  - `Assets/TextMesh Pro/Resources/TMP Settings.asset` — carries its own
    `m_ClearDynamicDataOnBuild: 1`, which `TMP_FontAsset.CreateFontAsset()` seeds onto every
    **newly created** font asset. So the bug reproduces itself on the next font anyone generates.
  - Only **4** TMP font assets exist project-wide (found via the `TMP_FontAsset` script guid
    `71c1514a6bd24e1e882cebbe1904ce04`): the two above plus `LiberationSans SDF` and
    `LiberationSans SDF - Fallback`.
- **Changed:**
  - `Assets/Scripts/Editor/TMPFontAssetStaticBaker.cs` — **new.** Static editor class, two menu
    items under `Tools/Blasty/Fonts/`: "Bake All TMP Fonts To Static" (confirm dialog, writes) and
    "Report Only (Dry Run)" (logs only). Per font: resolves the source `.ttf` (live ref → stored
    GUID → name match across the project, same-folder candidates winning ties), temporarily forces
    Dynamic, `ReadFontAssetDefinition()` + `TryAddCharacters()` over *ASCII 32–126 ∪ every unicode
    already in `characterTable`* (so a bake never loses previously generated glyphs), then freezes
    to Static with clear-on-build off. Also clears the project-wide flag on `TMP Settings.asset`.
    Handles the already-damaged case (Static but empty character table) by repopulating first.
  - `Assets/Documentation for scripts/TMPFontAssetStaticBaker.txt` — **new**, per the project's
    doc-sync convention (the `PostToolUse` hook fired as expected).
- **Scene/Prefab/SO edits:** none typed by this session, but the tool the user ran **rewrote
  `.asset` files by design** — both font assets and `TMP Settings.asset`. Those rewrites are the
  deliverable, not a side effect. (Unrelated in-flight user work was also in the tree at wrap time:
  ~19 modified `.prefab` files and a `Level_1_Stage_01` → `Level_1_Stage_0` scene rename. Not this
  session's doing.)
- **Result — confirmed post-bake by re-reading the `.asset` YAML at `ab1297e`:**
  - `LILITAONE-REGULAR SDF.asset` → `m_AtlasPopulationMode: 0` (Static),
    `m_ClearDynamicDataOnBuild: 0`, and **`m_SourceFontFileGUID: d2972f88bd092c046a7487e67cb80a87`
    recovered** — exactly the `LILITAONE-REGULAR.TTF` the name-match heuristic was predicted to
    find, and it correctly preferred it over the same-named duplicate in `Lilita_One/`. 207
    characters retained (unchanged — printable ASCII was already a subset), atlas blob intact.
  - `Dangrek-Regular 1 SDF.asset` → Static, clear-on-build off, source GUID intact. Character count
    went **42 → 95**, i.e. the ASCII-baseline union worked as intended and added the missing
    printable range rather than replacing what was there.
  - `TMP Settings.asset` → `m_ClearDynamicDataOnBuild: 0`. Future font assets no longer inherit it.
- **Verified:** the bake itself is **verified by asset inspection** (the six field values above) and
  the tool compiled and ran in the Editor without failing — but the **end-to-end check is still
  outstanding**: nobody has yet deleted `Library/` and confirmed the fonts come back clean, which
  is the actual symptom this was meant to cure. Design-time correctness confirmed; the reimport
  survival test is not.
  What was verified *before* running, by reading the actual package source at
  `Library/PackageCache/com.unity.ugui@e20f1880fa04/Runtime/TMP/TMP_FontAsset.cs`: the enum values
  (`Static = 0, Dynamic = 1, DynamicOS = 2`), that `ReadFontAssetDefinition()` and
  `TryAddCharacters(string, out string, bool)` are public, that `TryAddCharacters` early-returns
  with a warning if the asset is Static (hence the temporary flip to Dynamic), and that
  `clearDynamicDataOnBuild` / the `sourceFontFile` setter / `m_SourceFontFileGUID` /
  `SourceFont_EditorRef` are all **`internal`** — unreachable from `Assembly-CSharp`, which is why
  every write in the tool goes through `SerializedObject` by field name. Brace balance and API call
  sites checked by grep; **not compiled**.
- **Gotchas:**
  - The Bash-heredoc hazard already noted in the reorg entry bit again, differently: `cat <<'EOF'`
    failed outright on a C# file (`unexpected EOF while looking for matching '`) — it is not just
    backslashes, quotes/apostrophes in the payload break it too. Use the Write tool for source
    files, always.
  - Setting `atlasPopulationMode = Static` through TMP's own public property **nulls
    `m_SourceFontFile` while keeping `m_SourceFontFileGUID`** — that is TMP's convention, not a
    bug, and it is what lets Font Asset Creator find the `.ttf` again later. The tool mirrors it.
    A null `sourceFontFile` on a *Static* asset is normal; on a *Dynamic* one it is the bug.
  - Both fonts have **duplicate `.ttf` files** that normalize to the same name key:
    `LILITAONE-REGULAR.TTF` vs `Lilita_One/LilitaOne-Regular.ttf`, and `Dangrek-Regular.ttf` vs
    `Dangrek-Regular 1.ttf`. Only the same-folder tiebreak picks the right one — check the "note"
    lines the dry run prints.
  - Static **cannot add glyphs at runtime.** Safe here (English UI + digits) but if this game ever
    renders Persian/Arabic or player-typed text, that font must stay Dynamic *with its Source Font
    File assigned*.
  - The font `.asset` diffs are enormous because the atlas is an embedded hex blob. Expected —
    commit once and the churn stops, which is the entire point of the change.
  - The user's Font Asset Creator screenshot showed "Missing characters: 99" — that is just
    Extended ASCII against a font (Lilita One) that has no glyphs for those codepoints, not a
    symptom of this bug. The tool bakes printable ASCII only, so it will not reproduce that noise.
- **Next:** delete `Library/` once and confirm the fonts render correctly without any manual
  "Generate Font Atlas" — that is the one check the bake has not yet proven. The tool is idempotent
  and safe to re-run whenever a font is added or regenerated (it skips assets already correct).

### 2026-08-20 — Script audit against the 9 ticked build scenes + full `Assets/Scripts/` reorganization
- **Goal:** review every script, classify by whether the shipping scenes (StarterScene, MenuScene,
  Level_1_Stage_1..7 — the 9 ticked entries in Build Profiles) actually use it, then reorganize
  `Assets/Scripts/` into real feature folders.
- **Status:** done
- **Method (reproducible):** two independent passes, both scripted against the raw YAML/source —
  (1) **scene reachability**: parse `guid:` refs out of each scene, recurse through referenced
  prefabs/`.asset`/`.controller`/`.mat`, resolve GUIDs via every `.meta` under `Assets/`, collect
  which `.cs` each scene transitively reaches; (2) **static code refs**: strip comments, extract
  declared type names per file, count inbound mentions from other files. A script is only "dead"
  if it fails *both*. Pass 2 matters because plain C# classes, abstract bases (`PlayerState`) and
  static utils (`SaveSystem`) never appear in scene YAML at all.
- **Findings:** 117 project scripts (excluding DOTween / Spriter2UnityDX / TutorialInfo).
  **52 CORE** (in ≥7 scenes), **22 ACTIVE** (1–2 scenes, mostly MenuScene meta-UI),
  **26 SUPPORT** (no scene ref but code depends on them), **1 EDITOR**, **16 DEAD**.
- **Changed:**
  - `Assets/Scripts/**` — 115 `.cs` files moved into a new tree:
    `Core/{Boot,Progression,SaveLoad}`, `Puzzle/{Board,Pieces,Input,Match}`,
    `Combat/{Player,Player/States,Enemy,Shared,Spawning}`, `Roguelite/`,
    `Data/{Stats,CombatPower,Units}`, `UI/{Home,Units,HUD,WinLose,Managers,Common}`,
    `Debug/`, `Editor/`, `_Legacy/`. **Moves only — not one line of C# was edited.**
  - Pulled in 2 scripts that were loose at the `Assets/` root: `PlayerLockState.cs`
    (→ `Combat/Player/States/`, it is a live FSM state used by 7 files) and `ScrollOnlyUp.cs`
    (→ `UI/Common/`). Also `BoardGhostMaskEditor.cs` out of
    `Assets/Arts/2D Arts & Animations/OldArts/Editor/` → `Assets/Scripts/Editor/`.
  - `_Legacy/` (16 files, quarantined not deleted): CombatAgent, OldWinPanel, BoardCameraFramer,
    RaycastSwitcher, CameraBoardDebug, PieceIdAllocator, InputRouter, MouseForwarderToParent,
    PiecePainterMaterialsOnly, PlayerLocomotionManager, PlayerUnlockState, CurrencyTopBarView,
    ModalCanvasUtil, DevResetButton, UpgradeButtonController, and `BoardInputController.EMPTY.cs`
    (a 0-byte duplicate that was sitting in `Assets/TD/ُScripts/Others/`).
  - Deleted 16 now-empty folder `.meta` files (`Save-Load-Data.meta`, `UI Tabs.meta`, the
    diacritic `ُManager.meta`, …). No asset was deleted.
  - `CLAUDE.md` — Architecture section rewritten to the new paths + a layout diagram; the
    U+064F diacritic note updated (those *script* folders no longer exist).
- **Scene/Prefab/SO edits:** none. No `.unity`/`.prefab`/`.asset` file was opened or modified.
- **Verified:**
  - Unity was confirmed **closed** before moving (moving `.cs` while the editor watches `Assets/`
    can make it regenerate a `.meta` with a fresh GUID and orphan every reference).
  - Each `.cs` was moved **together with its `.meta`** via `git mv` → `git status` shows
    **227 renames and zero modified `.meta` files**, i.e. every GUID survived.
  - Re-ran the scene-reachability scan after the move: **76 scripts reached before, 76 after,
    identical set** — so no scene/prefab script reference broke.
  - **Not** verified in Play mode / not compiled — Unity had to stay closed for the move. Reopen
    the Editor and confirm the Console is clean; that is the one remaining check.
- **Gotchas:**
  - Build Profiles lists `Level_1_Stage_8..20` at the stale path `Scenes/Level_1_Stage_N.unity`
    (all show "Deleted"). The scenes do exist, at
    `Scenes/TestScenes/GamePlay Scenes/Level_1_Stage_N.unity`. Stages 8–20 are therefore **not in
    the build** — re-add them from the correct path when that content is meant to ship.
  - **The Bash tool collapses `\\` to `\` even inside a quoted heredoc.** Writing JS/regex via
    `cat <<'EOF'` silently turned `'\\b'` into a literal backspace and produced wrong results
    before it was caught. Use the Write tool for any script containing backslash escapes.
  - Two type names are each declared in two different files: `Piece`
    (`Puzzle/Pieces/Piece.cs` + `Data/Units/UpgradeCostSO.cs`) and `GameState`
    (`Core/SaveLoad/GameState.cs` + `UI/WinLose/LevelGameManager.cs`). They compile today only
    because the collisions are nested/scoped differently — worth disambiguating.
  - Data assets still sit under `Assets/Scripts/`: `REGULITE/RogueliteScriptableObjects/`,
    `TowertDefenseScripts/Prefabs/` + `Test Prefabs/`, `UI/UI-SOs/`. Left deliberately (the task
    was scripts); this is why the old `REGULITE`/`TowertDefenseScripts` folder names still exist.
  - `_Legacy/` is a judgement call from static analysis. Anything instantiated purely by name/
    reflection, or referenced only from a scene **not** in the ticked 9, would look dead here.
- **Next:** fix the Stage 8-20 build paths; move leftover data assets out of `Assets/Scripts/`;
  empty `_Legacy/`; then namespaces + the gameplay→UI event refactor.

#### Continuation (later session) — per-script reference documentation, 101/101 complete

- **Goal:** read every live gameplay script line by line and write one explanatory
  `.txt` file per script into `Assets/Documentation for scripts/`, covering every script
  reachable from the 9 ticked build scenes (matches the CORE+ACTIVE set from the reorg pass
  above). Paused once mid-task at the user's request (hourly limit) at 66/101 and resumed in a
  later session; this entry covers the full arc from 0 to 101.
- **Status:** done — verified every live `.cs` under `Assets/Scripts` (excluding `_Legacy/`) has
  a matching `.txt` by filename.
- **Changed:**
  - `Assets/Documentation for scripts/` — 101 new `.txt` files (plus Unity-generated `.meta`
    siblings), one per script, each following PURPOSE / FIELDS / METHODS-or-FLOW /
    HOW IT CONNECTS / NOTES. NOTES call out dead numbered siblings, magic
    strings/animator-parameter contracts, and any bug found while reading the source.
- **Scene/Prefab/SO edits:** none.
- **Verified:** every doc was written only after reading the actual source (one early exception —
  `PieceColorPalette.txt` was drafted from assumption and had to be rewritten after actually
  reading the file; documented as a process note, not repeated after). Cross-checked file list at
  the end: `find Assets/Scripts -name '*.cs' -not -path '*_Legacy*'` vs the doc folder — exact
  1:1 match, 101/101.
- **Findings surfaced while writing the docs (added to Open Threads above):**
  - `GameStartManager.Awake()` wipes the save on every launch (`resetBool = true` +
    `OnResetButtonClicked()`), confirmed still present.
  - Roguelite XP (`RogueliteManager.AddXP` / `NotifyEnemyKilled`) has zero live call sites —
    the whole skill-card system is unreachable through normal play as currently wired.
  - Unit auto-unlock-by-stage in `UnitsPanelController.ProcessRequirementUnlocks` and
    `PlayerProgressionService.ProcessStageUnlocks` are both dead code (never subscribed/called).
    The REAL live unlock path is `HomeManager.Start() → CachePendingCharacterUnlocks() →
    PlayerProgressionService.GetReachableButLockedUnits() → NewCharacterStats claim popup →
    ProgressionService.UnlockUnit()`, triggered every time the player returns to the Home screen.
  - `StageRewardCalculator` sums HP tiers cumulatively (hpCase 3 = r1+r2+r3), while
    `HomeManager.TryGetStageRewardPreview` passes hpCase=1 despite its own comment claiming
    "best case" — the stage-card reward preview likely understates the real payout substantially.
  - `UnitsPanelController.HandleDeploySave` appears to pass `highlightUndeployedId`/
    `highlightDeployedId` swapped when refreshing the deploy overlay post-swap (cosmetic only;
    the actual saved model/order data is correct).
  - Deploying a unit via the swap overlay makes the candidate INHERIT the replaced unit's level,
    and resets the replaced unit to level 1 — a real, easy-to-miss game-design consequence
    (gems spent upgrading a benched unit are lost) documented in `UnitsPanelController.txt`.
- **Gotchas:**
  - Writing bundle text through a Bash heredoc mangles both backslashes (`\\b` → literal
    backspace) and backticks/markdown code-spans in this environment — every multi-file batch
    had to go through the Write tool instead, then be split by a small Node script
    (`split-docs.js`, kept in the scratchpad and copied into the repo root only for the instant
    it runs, then deleted) that parses `<<<FILE: Name.txt>>>` markers.
  - `SessionStart`-injected `SESSIONS.md` context is large; large edits to this file are best done
    with a small Node splice script against known anchor lines rather than the Edit tool, since
    the file changes across turns as other sessions/this session append to it.
- **Next:** none for the documentation task itself. See the Open Threads list above for what the
  findings imply (all six items were added there, not just logged here).

#### Continuation (same session) — standing hook to keep script docs in sync going forward

- **Goal:** the user asked that from now on, any script change or new script triggers an update
  (or creation) of its doc in `Assets/Documentation for scripts/`, enforced automatically rather
  than relying on a future session remembering.
- **Status:** done.
- **Changed:**
  - `.claude/hooks/doc-reminder.js` — new `PostToolUse` hook script. Reads the tool-call JSON on
    stdin, extracts the touched file path (`tool_response.filePath` or `tool_input.file_path`),
    and if it matches `Assets/Scripts/**/*.cs` (excluding `Assets/Scripts/_Legacy/**`), emits
    `hookSpecificOutput.additionalContext` reminding the model to update/create
    `Assets/Documentation for scripts/<basename>.txt` in the same PURPOSE/FIELDS/METHODS-or-FLOW/
    HOW IT CONNECTS/NOTES format used by the rest of the folder. Fails silently (never blocks the
    tool call) on unparseable input.
  - `.claude/settings.json` — added a `PostToolUse` entry with `matcher: "Write|Edit"` calling the
    hook above, merged alongside the existing `SessionStart` hook (not replaced).
  - `CLAUDE.md` — added a "Per-script documentation — keep it in sync" section documenting the
    convention for humans and future sessions, pointing at the hook and explaining the exclusion.
- **Scene/Prefab/SO edits:** none.
- **Verified:** pipe-tested the raw hook against six synthesized stdin payloads (Edit on a live
  script → fires; Write on a brand-new script path → fires; Edit under `_Legacy/` → silent; Edit
  on a non-script file → silent; `tool_response.filePath`-shaped input → fires; malformed JSON →
  exits 0, no crash). Validated `.claude/settings.json` is well-formed JSON and the hook entry
  shape matches schema via a Node script (no `jq` on this machine, matching the note already in
  `CLAUDE.md`). **Proved it fires live**: made a real, trivial `Edit` on
  `Assets/Scripts/UI/Common/ScrollOnlyUp.cs` (added then immediately removed a
  `// hook-fire-test` comment line) — the reminder appeared in the actual `PostToolUse:Edit`
  system-reminder both times, then confirmed `git diff` on that file is empty (clean revert, no
  net change committed or left behind).
- **Gotchas:**
  - This project's settings watcher was already active from earlier in the session (the
    `SessionStart` hook has been firing all along), so no `/hooks` reload or restart was needed
    for the new `PostToolUse` hook to take effect immediately — confirmed by the live fire test
    above, not assumed.
- **Next:** nothing pending for this. Going forward, any session that edits or adds a script under
  `Assets/Scripts/` (outside `_Legacy/`) will get an automatic reminder to keep
  `Assets/Documentation for scripts/` current — actually updating the doc is still on the model,
  the hook only reminds, it does not write the doc itself.

### 2026-08-20 — CLAUDE.md init; /ads command + guide added to project; /feature-doc blocked
- **Goal:** run `/init` to generate the repo's `CLAUDE.md`; separately, bring the `/ads` slash
  command and its companion integration guide into this project (docs only); then run
  `/feature-doc` for a new feature.
- **Status:** partial — first two goals done, `/feature-doc` blocked on missing input.
- **Changed:**
  - `CLAUDE.md` — created via `/init`. Documents: Unity 6000.3.21f1 + URP, no CLI build/test
    pipeline (Play mode is the verification path), the puzzle-board / combat-state-machine /
    roguelite-skill / progression-save architecture, and codebase conventions — most notably the
    pervasive numbered-duplicate-method/class pattern (`Save()`/`Save1()`,
    `InitializeServices()`/`1`/`2`, `GameStartManager`/`GameStartManager2`,
    `BoardBootstrapper`/`BoardBootstrapper2`) confirmed by reading
    `Assets/Scripts/Save-Load-Data/SaveSystem.cs`, `Assets/Scripts/UI/GameStartManager.cs`, and
    `Assets/Scripts/PuzzleُScripts/Others/BoardBootstrapper.cs` — plus the hand-rolled singleton
    pattern, SaveSystem-only persistence rule, and the hidden Arabic diacritic (U+064F) in the
    `PuzzleُScripts` / `ُManager` folder names.
  - `.claude/commands/ads.md` — added (copied verbatim from `~/.claude/commands/ads.md`), makes
    `/ads` available in this project.
  - `.claude/docs/Unity-AdMob-Integration-Guide.md` — added (copied verbatim from
    `~/.claude/docs/Unity-AdMob-Integration-Guide.md`), the companion "why" doc `ads.md` refers to.
- **Scene/Prefab/SO edits:** none.
- **Verified:** not verified in Play mode — this session only added Markdown docs/config, no
  gameplay code. Confirmed via `grep`/`find` that this project has **no** AdMob integration yet
  (no `com.google.ads.mobile` in `Packages/manifest.json`, no `AdManager` script, no
  `Assets/Plugins/Android/`) — the two files above are reference material only. Per explicit user
  instruction, the actual AdMob package/script/build was **not** performed this session.
  - Also confirmed by grep/find (not by Play mode) that this repo has no `asmdef` files and no
    authored test scripts, matching what `CLAUDE.md` now states.
- **Gotchas:**
  - User was explicit: only the two `.md` files should land in the repo. Do not install the
    `com.google.ads.mobile` package, add `AdManager.cs`, touch Gradle files, or run a build unless
    asked again.
  - A stray background `find /` (task id `b5xgw8r0k`, launched while locating the global AdMob
    guide) never produced a completion record and was later reported "stopped" by the harness with
    no transcript marker — it was superseded by a targeted `~/.claude` search that found the file,
    so no action was needed. Worth knowing this can happen if a future session searches from `/`.
  - User also set a standing preference (not a repo file change): reply in English by default even
    when they write in Persian, and switch to Persian output only when explicitly asked to
    translate.
- **Next:** `/feature-doc` needs the user to name the feature to document (name, implementing
  files, what broke) — see Open Threads.

### 2026-08-20 — Set up the shared session log + automatic loading
- **Goal:** make every session aware of what the other sessions did.
- **Status:** done
- **Changed:**
  - `SESSIONS.md` — created (this file: protocol, open threads, decisions, log).
  - `CLAUDE.md` — added a "Cross-session log" pointer near the top.
  - `.claude/hooks/inject-sessions.js` — SessionStart hook script; prints this file as
    `hookSpecificOutput.additionalContext`, exits silently if the file is missing.
  - `.claude/settings.json` — created; registers the SessionStart hook.
  - `.claude/commands/wrap.md` — `/wrap` command that writes the end-of-session entry.
- **Scene/Prefab/SO edits:** none.
- **Verified:** hook script pipe-tested (`echo '{}' | node .claude/hooks/inject-sessions.js`) —
  exit 0, valid JSON, full file body in `additionalContext`; `settings.json` parsed and the
  command path read back. **The hook is confirmed live:** it fired later in the same session and
  injected this file's contents as SessionStart context. (It was inert until then, because
  `.claude/settings.json` did not exist when the session started, so the settings watcher was not
  watching `.claude/` yet — expect that one-time delay whenever this file is created from scratch.)
  No Unity Play-mode testing applies; nothing under `Assets/` was touched.
- **Gotchas:**
  - `jq` is not installed on this machine — the hook uses `node` (on PATH) instead. Any future
    hook here should avoid `jq`.
  - Writing the entry cannot be automated; if a session is closed abruptly its work goes unlogged.
  - `/wrap` does not end the session — work can continue after it, and re-running it edits the
    existing entry instead of adding a second one.
  - Recognizing "which entry is mine" is not robust: after a compaction, or with two sessions
    logging on the same date, a session may fail to find its own entry and add a duplicate. A
    short session ID in the entry heading would fix this if it starts happening.
- **Next:** nothing pending.
