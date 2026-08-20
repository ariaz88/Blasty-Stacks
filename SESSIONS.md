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
