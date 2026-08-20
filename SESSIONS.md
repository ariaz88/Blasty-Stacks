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

- Reopen Unity and confirm the Console is clean after the 2026-08-20 script reorg. Nothing
  was compiled or Play-tested, because the Editor had to be closed for the move.
- Build Profiles points `Level_1_Stage_8..20` at the stale path `Scenes/Level_1_Stage_N.unity`;
  the real files are under `Scenes/TestScenes/GamePlay Scenes/`. Stages 8-20 are not in the build.
- `Assets/Scripts/_Legacy/` holds 16 quarantined scripts. Confirm each is truly unused, then delete.
- Data assets still live under `Assets/Scripts/` (`REGULITE/RogueliteScriptableObjects/`,
  `TowertDefenseScripts/Prefabs/` + `Test Prefabs/`, `UI/UI-SOs/`). Move them out of the script tree.
- Duplicate type names to disambiguate: `Piece` and `GameState` are each declared in two files.
- `/feature-doc` was invoked with no feature named. Waiting on the user to specify: feature name,
  which scripts implement it, and what broke along the way.

---

## Decisions

_Durable choices with their reasons, so no session reopens them blindly._

| Date | Decision | Why |
|------|----------|-----|
| 2026-08-20 | Cross-session state lives in `SESSIONS.md` at the repo root. | One file, tracked by git, readable by a human and by every session. |
| 2026-08-20 | The **read** half is automated by a `SessionStart` hook (`.claude/hooks/inject-sessions.js`, wired in `.claude/settings.json`) that injects this file's contents into every session. | Relying on each session to remember to open the file made awareness optional; the hook makes it unconditional. |
| 2026-08-20 | The **write** half is triggered by the `/wrap` slash command (`.claude/commands/wrap.md`), not by a hook. | No hook event reliably means "the session is ending", and a `Stop` hook fires after every response — far too noisy. An explicit one-word command is the honest mechanism. |

---

## Session Log

_Newest first._

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

#### PAUSED 2026-08-20 — per-script documentation, 66 of 101 done

**Task:** read every live gameplay script line by line and write one explanatory
`.txt` per script into `Assets/Documentation for scripts/`. Paused at the user's
request (hourly limits), to be resumed.

**Done: 66 / 101.** All of Data, Core, Puzzle, Combat/Player (incl. States),
Combat/Shared, Combat/Enemy (except EnemyManager), and the Editor script.

**Remaining 35 files, in the order to tackle them:**
1. The three big combat brains — `Combat/Enemy/EnemyManager.cs` (794 lines),
   `Combat/Player/PlayerManager.cs` (660), `Combat/Player/SimpleJump2D.cs` (428)
2. Spawning — `EnemySpawner.cs` (306), `LevelConfig.cs` (79),
   `PlayerWaveManager.cs` (822)
3. Roguelite — `RogueliteManager.cs` (426), `PlayerStatsApplier.cs` (173),
   `SkillCardUI.cs` (82), `SkillData.cs` (21)
4. UI/WinLose — `RevivePanel.cs` (648), `WinPanel.cs` (381),
   `LevelGameManager.cs` (165), `LoseGame.cs` (128),
   `StageRewardCalculator.cs` (68), `RewardItemUI.cs` (18)
5. UI/Units — `UnitsPanelController.cs` (1428), `UnitDetailView.cs` (493),
   `DeployOverlayController.cs` (430), `UnitCardView.cs` (336),
   `BucketStatRow.cs` (73), `BucketStatsPanel.cs` (72), `BucketHeader.cs` (57)
6. UI/Home — `HomeManager.cs` (909), `HomeCardsPager.cs` (500),
   `NewCharacterStats.cs` (267), `StageCard.cs` (112)
7. UI/HUD + Managers + Common — `HudCurrencyView.cs` (270),
   `ResourcesAnimationManager.cs` (160), `CurrencyManager.cs` (170),
   `MainMenuPanelController.cs` (296), `BackButtonRelay.cs` (15),
   `UIButtonPressScaler.cs` (120), `ScrollOnlyUp.cs` (39)
8. `Debug/DebugStageManager.cs` (238)

**Method to keep using (it works well):**
- Read the source with `cat`, never document a file unseen. (One entry,
  `PieceColorPalette`, was drafted from assumption and had to be rewritten —
  always read first.)
- Write a bundle file to the scratchpad with `<<<FILE: Name.txt>>>` separators,
  then split it with `scratchpad/split-docs.js` (copy it to the repo root, run,
  then delete it — do not leave it in the repo).
- Doc template: PURPOSE / FIELDS / METHODS or FLOW / HOW IT CONNECTS / NOTES.
- **Avoid backticks and backslashes in bundle text** — the Bash heredoc mangles
  both. Use the Write tool for the bundles.

**Still owed to the user after the docs are finished:** package this whole
workflow as a reusable SKILL so the same documentation can be generated in any
other project, for both existing and future scripts.

**Bugs found while documenting (reported, not fixed):**
- `GameStartManager.Awake()` hard-codes `resetBool = true` then calls
  `OnResetButtonClicked()` — **the save is wiped on every launch.** Critical.
- `SaveSystem.LoadInternal()` calls `Save()` during migration while `_cache` is
  still null, causing re-entrant loads; the `saveDepth > 10` guard papers over it.
- `CPWeightMath.Evaluate` never reads `cfg.meleeMultByLevel` and assigns
  `rangedMult` twice, so the melee curve is ignored.
- `PlayerProgressionService` charges **coins**, while its comments and
  `UpgradeCostSO` both say gems.
- `PlayerStats` refreshes the health bar against `statsBase.maxHP` in
  `ApplyDamageToPlayer` but `maxHealth` in `Start` — wrong ratio after a
  runtime max-HP buff.
- `RaycastToBoard` and `PieceDragHandlerSimple` still use XZ/3D plane maths on
  this XY board.
- A prefab using `FrogJumpTransformOnly` has `playerRigidbody` serialized as a
  3D `Rigidbody` where a `Rigidbody2D` is expected — null in builds.

#### Continuation (same session) — dead-code purge, verified in the Editor

- **Reopened Unity:** Console clean, 0 errors. Opened all 9 ticked build scenes: 3,197 objects /
  9,970 components / **0 missing scripts** — the reorg broke nothing.
- **Architecture review (no code changed):** built a cross-layer dependency matrix. Key results —
  **the Puzzle layer has ZERO outgoing dependencies** (it reaches the rest of the game only through
  `MatchResolver.OnBlast`); gameplay wrongly reaches **up** into UI in 3 files
  (`EnemyGateStats` → WinPanel/HomeManager/LevelGameManager, `EnemySpawner` → WinPanel/HudCurrencyView,
  `PlayerManager` → HudCurrencyView); `CurrencyManager` is a core economy service **misfiled** under
  `UI/Managers/` (it imports only System + UnityEngine, zero UI code) — moving it to `Core/` would
  erase most of the Core→UI coupling. Singleton reach-through counts: `CurrencyManager.Instance` 43,
  `SaveSystem.Data` 26, `LevelManager.Instance` 19, `GameStartManager.Instance` 13.
- **PHASE 1 — deleted 47 numbered duplicate types** across 27 files (**−8,937 lines**).
  Rule applied: the type name ends in a digit **and** it has zero real-code references, counted with
  a comment/string-aware scanner so mentions inside comments don't create false positives. All 47
  scored 0. Every file kept its main class — verified with a survives-vs-deleted table before applying.
  Note: `SimpleJump2D.cs` contains no class of that name; Unity resolves such a file to its **first**
  class, here `FrogJumpTransformOnly` (used by 12 prefabs). It was preserved; only
  `FrogJumpTransformOnly1/2` and `FrogJump2D1/2` were removed.
- **PHASE 2 — removed 2,537 lines of commented-out code** across 55 files. The classifier groups
  contiguous `//` blocks and scores them; a block is deleted only when ≥50% of its lines look like
  C# (ends in `;`/`{`/`}`, starts with a keyword, is an attribute, an assignment, or a call).
  `///` XML doc comments are always kept, and so are prose comments. Empty `#region` blocks pruned.
- **Result:** 30,778 → **18,947 lines** (−38%); comment ratio 17% → **9%**.
  `EnemyManager` 1830→794, `RevivePanel` 1743→648, `HomeCardsPager` 1539→522, `WinPanel` 1316→385,
  `MainMenuPanelController` 1043→297, `BoardGridXY` 1052→345, `FractureObject` 1098→608.
- **Verified:** braces balanced across all 101 live scripts; Unity recompiled with **0 errors**;
  re-audited all 9 build scenes → **0 missing scripts**; `git status` shows **no `.unity` or
  `.prefab` modified**. Still **not** Play-mode tested — that remains the open check.
- **Gotchas:**
  - The multi-class files that remain (`LevelConfig`, `SaveData`, `PlayerUnitsModel`, `WinPanel`,
    `StageRewardCalculator`, `SaveOnStop`, `DebugStageManager`) hold legitimate companion/nested
    types (`Wave`, `WaveEntry`, `UnitState`, `RewardValues`, …). **Do not "clean" these.**
  - `DebugStageManagerEditor` lives in a non-Editor folder but is correctly wrapped in
    `#if UNITY_EDITOR`, so player builds are unaffected.
  - **Latent pre-existing bug found** (not caused by this work): Unity warns
    *"field 'playerRigidbody' expects 'Rigidbody2D' but the stored reference is a 'Rigidbody' —
    the reference is treated as null (and serialized as null in player builds)"* on a prefab using
    `FrogJumpTransformOnly`. It must be re-assigned in the Inspector or jumping silently no-ops
    in a build.
  - Writing Markdown/JS through a shell heredoc mangles both backslashes **and** backticks in this
    environment. Use the Write tool for any content containing them.

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
