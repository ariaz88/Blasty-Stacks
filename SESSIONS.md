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

- **RELEASE BLOCKERS from the 2026-08-21 ads work** (all currently set for testing):
  `BattleStartController.doNotPersistAllowance` must go OFF (otherwise the 24h battle cap resets
  every app restart); `AdManager.useTestIds` must go OFF with real unit ids filled in;
  `GoogleMobileAdsSettings.asset` still holds Google's TEST app ids; and the Android
  `applicationIdentifier` is UNSET (only Standalone `com.DefaultCompany.2D-URP` exists) — a real
  package name is required and must match the AdMob console entry.
- `Level_1_Stage_1.unity` is NOT in Build Settings yet (user said they would add it).
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
  `CrowdSeparation2D`, `AttackSlotRegistry`, `HealthBar`, `EnemySpawner`, `BattleStartController`,
  `FormationGapFiller`, `PlayerPursueTargetState`. Two standalone guides WERE written:
  `Formation_GapFilling_Guide.md` and `Unit_Avoidance_And_NonCollision_Guide.md`.
  `SimpleJump2D.txt` was brought fully up to date on 2026-08-22.
  **Additionally behind as of 2026-08-22** (the user explicitly asked for NO docs on those two
  fixes, so this is a deliberate debt, not an oversight): `PlayerManager` (new
  `isFormationStepping`), `PlayerLockState` (formation-step bypass + static-body guard),
  `FormationGapFiller` (flag handover + mid-step battle-start abort), `HealthBar` (authored-order
  capture/restore), `LevelGameManager` (new static `OnGameStateChanged` / `IsBattleRunning`).

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
| 2026-08-20 | The conversion is done by a repeatable editor tool (`Assets/Scripts/Editor/TMPFontAssetStaticBaker.cs`), not by hand-editing the Inspector or patching the `.asset` YAML directly. | Hand-editing does not scale and is not reproducible for the next font added; direct YAML patching was considered and rejected because it cannot repopulate an atlas — only TMP's `TryAddCharacters` can, and it needs a loaded font face. The tool also re-runs safely on assets that are already correct. |

---

## Session Log

_Newest first._

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
