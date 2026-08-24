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
- **The Heroes Stats panel exists in `Level_1_Stage_1` ONLY.** Stages 2-20 have no such object,
  so `HeroRoster` still tallies there (PlayerManager always registers) but nothing displays it.
  Copying the panel means re-doing the `HeroStatCell` wiring and adding it to that stage's
  `BattlePhaseTransition.fadeInAfterMove`.
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

---

## Session Log

_Newest first._

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
