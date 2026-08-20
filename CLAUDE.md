# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Cross-session log — read first, write last

`SESSIONS.md` (repo root) is the shared memory between all Claude Code sessions on this project.

- **Start of a session:** its contents are injected automatically by the `SessionStart` hook in
  `.claude/settings.json` (`.claude/hooks/inject-sessions.js`). If for any reason it was not
  injected, read `SESSIONS.md` before proposing or changing anything.
- **End of a session, or after any meaningful change:** append an entry to the top of its Session
  Log and update its Open Threads, following the protocol written in that file. The `/wrap`
  command does exactly this.

## Per-script documentation — keep it in sync

`Assets/Documentation for scripts/` holds one `.txt` per live script (same basename as the
`.cs` file), covering PURPOSE / FIELDS / METHODS-or-FLOW / HOW IT CONNECTS / NOTES — including
dead numbered siblings, magic strings/animator-parameter contracts, and known bugs found while
reading the source. Read the matching doc before editing a script blind.

- **A `PostToolUse` hook** (`.claude/settings.json` → `.claude/hooks/doc-reminder.js`) fires after
  every `Write`/`Edit` that touches `Assets/Scripts/**/*.cs` (excluding `Assets/Scripts/_Legacy/`)
  and reminds the model to update or create the matching doc.
- **When the reminder fires:** re-read the changed script and update its `.txt` to match — or, for
  a brand-new script, write one from scratch in the same format as the rest of the folder.
  Don't skip this because the change looks small; a stale doc is worse than no doc.
- `_Legacy/` scripts are excluded on purpose — they are quarantined, not live.

## Project overview

"Blasty-Stacks" (aka "Stacky Warriors 2D") is a Unity 2D game combining a grid-based block-placement puzzle with a tower-defense / roguelite auto-battler combat layer. It is a standard Unity Editor project — there is no CLI build/test pipeline in this repo.

- Engine: Unity **6000.3.21f1** (Unity 6), Universal Render Pipeline (URP 17.3.0)
- Open and run the project through Unity Hub / Unity Editor (matching version above). Play mode in the Editor is the primary way to test changes.
- No asmdef files exist — all scripts compile into the single default `Assembly-CSharp`.
- `com.unity.test-framework` is a listed dependency but there are no authored test scripts in the repo; there is no automated test suite to run. Verify changes by entering Play mode on the relevant scene (see Scenes below).
- Scenes: `Assets/Scenes/MenuScene.unity` (main menu / home), `Assets/Scenes/StarterScene.unity` (gameplay), plus ad-hoc `Test Scene.unity` / `Test Scene 1.unity` for isolated testing.

## Architecture

`Assets/Scripts/` was reorganized on 2026-08-20 into feature folders (moves only — no code edits,
GUIDs preserved). Current layout:

```
Assets/Scripts/
├── Core/       Boot/ (GameStartManager, MenuLoader), Progression/ (LevelManager,
│               PlayerProgressionService, PlayerUnitsModel, StageUnlockRelay), SaveLoad/
├── Puzzle/     Board/, Pieces/, Input/, Match/
├── Combat/     Player/ (+ Player/States/), Enemy/, Shared/, Spawning/
├── Roguelite/  RogueliteManager, SkillData, SkillCardUI, PlayerStatsApplier
├── Data/       Stats/, CombatPower/, Units/   (ScriptableObject definitions + math)
├── UI/         Home/, Units/, HUD/, WinLose/, Managers/, Common/
├── Debug/      DebugStageManager
├── Editor/     editor-only scripts (must stay in a folder named `Editor`)
└── _Legacy/    16 scripts with no scene reference and no inbound code reference.
                Quarantined, not deleted. Verify before reviving or removing.
```

The game loop stitches together two gameplay systems plus shared meta/progression systems:

1. **Puzzle board** (`Assets/Scripts/Puzzle/`) — a grid-based piece-placement board.
   - `BoardGridXY` is the grid/coordinate authority (world↔cell conversion, occupancy).
   - `PieceSimple` represents a placeable multi-cell piece; shape offsets can be authored or auto-derived from child colliders.
   - `BoardBootstrapper` snaps all pieces already in the scene onto the grid at `Start()`.
   - `BoardInputController` (Puzzle/Input) handles pointer input. `InputRouter` / `RaycastSwitcher` are in `_Legacy/` — unused.
   - `MatchResolver` resolves matches once pieces are placed.

2. **Combat / tower-defense layer** (`Assets/Scripts/Combat/`) — spawns player units and enemies that fight automatically.
   - Player behavior is a state machine: `PlayerState` (abstract, `Tick(PlayerManager, PlayerStats, PlayerAnimatitorManager) → PlayerState`) with concrete states under `Combat/Player/States/` (Idle, PursueTarget, Combat, Attack, Lock, GameComplete, Death; `PlayerUnlockState` is in `_Legacy/`). Enemies follow an analogous but simpler pattern under `Enemy/`.
   - Stats flow through ScriptableObjects: `UnitStatsSO` (design-time data) → `UnitStatsRuntime` (per-instance runtime copy that supports live multiplier application, used by the roguelite skill layer). `CPCalculator` / `CPConfigSO` / `CPWeightMath` compute "combat power".
   - `Combat/Spawning/` holds `EnemySpawner` + `LevelConfig` (enemy waves) and `PlayerWaveManager` (player waves, spawned by `MatchResolver.OnBlast`).

3. **Roguelite skill layer** (`Assets/Scripts/Roguelite/`) — `RogueliteManager` grants XP on enemy kill, triggers level-up, and presents a skill-card pick (`SkillData`, `SkillCardUI`) that applies stacking multipliers onto active players' `UnitStatsRuntime`. It tracks active players/enemies (`PlayerStatsApplier`, `EnemyManager`) either via registration calls or `FindObjectsOfType` refresh.

4. **Progression / meta** (`Assets/Scripts/Core/`, config SOs in `Assets/Scripts/Data/`)
   - `LevelManager` (singleton, `DontDestroyOnLoad`) owns the current global stage and converts it to/from (level, stage-within-level); persists via `PlayerPrefs`; optionally auto-loads a scene per stage.
   - `GameStartManager` (singleton) is the boot orchestrator: builds `PlayerUnitsModel` from `UnitsDatabaseSO`, decides first-run vs. normal-boot seeding, wires `PlayerProgressionService`, and loads currency into `CurrencyManager`.
   - `SaveSystem` is a static class backing a single cached `SaveData` object, serialized as JSON into one `PlayerPrefs` key (`GAME_SAVE_V1`) via `JsonUtility`. All persistence (units, currency, stage stars/unlocks) funnels through this cache + `Save()`; it includes a save-recursion guard and a `version`-gated migration path in `LoadInternal()`.
   - `UnitsDatabaseSO` / `UpgradeCostSO` / `ProgressionConfigSO` are the design-time ScriptableObject configs; `PlayerUnitsModel` (plain C# class, not a `MonoBehaviour`) is the runtime unit roster model.

5. **UI** (`Assets/Scripts/UI/`) is organized by feature: `Units/` (roster/upgrade panels), `Home/` (stage select), `HUD/`, `WinLose/` (win, lose, revive, rewards), `Managers/` (`CurrencyManager`, main-menu + back-button routing), `Common/` (shared widgets). The UI-facing ScriptableObject *classes* now live in `Assets/Scripts/Data/Units/`.

## Codebase conventions to know before editing

- **Numbered duplicate methods/classes are a recurring pattern here** (e.g. `Save()`/`Save1()`, `InitializeServices()`/`InitializeServices1()`/`InitializeServices2()`, `GameStartManager`/`GameStartManager2`, `BoardBootstrapper`/`BoardBootstrapper2`, large commented-out class blocks at the bottom of files). These are leftover iterations, not intentional overloads. **Before modifying behavior, grep for call sites to confirm which variant is actually wired up** (via Inspector references or active call paths) rather than assuming the newest-looking or last-defined one is live. Don't add another numbered variant — extend the active method in place, and feel free to delete confirmed-dead variants when touching that file.
- Singletons follow a consistent hand-rolled pattern: `public static X Instance`, guard-and-destroy duplicate in `Awake()`, `DontDestroyOnLoad(gameObject)`. Used by `LevelManager`, `GameStartManager`, `CurrencyManager`, etc.
- Persistence always goes through `SaveSystem` (static, `PlayerPrefs` + `JsonUtility`) — don't read/write `PlayerPrefs` directly from gameplay code for save data.
- The old script folders that carried a hidden Arabic diacritic (combining damma, U+064F) are **gone** as of the 2026-08-20 reorg. Some non-script folders elsewhere still carry it (e.g. `Assets/TD/ُScripts/`) — if a typed path mysteriously fails to match, suspect an invisible U+064F and use Glob/Grep instead.
- Some **data assets still live under `Assets/Scripts/`** and were deliberately left in place by the reorg (only `.cs` files were moved): `REGULITE/RogueliteScriptableObjects/` (SkillData assets), `TowertDefenseScripts/Prefabs/` + `Test Prefabs/`, and `UI/UI-SOs/` (unit/upgrade SO assets). Moving these is a separate, still-open cleanup.
- DOTween (`Assets/Plugins/Demigiant/DOTween/`) is available for tweening.
