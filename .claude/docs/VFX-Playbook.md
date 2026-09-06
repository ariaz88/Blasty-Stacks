# VFX Playbook — Blasty-Stacks

How VFX get briefed, built and verified in this project. Read this before starting any effect;
`/vfx` reads it first by design.

Everything here was paid for once already. The URP-2D constraints in particular each cost a session
before anyone wrote them down — see the Session Log entries for 2026-08-23 through 2026-08-26 in
`SESSIONS.md`.

---

## 1. The brief — six slots

An effect can be built without a round-trip when these six are known. They are listed in order of
how much time each one saves.

### 1. Trigger — *when* it fires and *through which code path*

The single most expensive thing to leave out. The 2026-08-25 shard-burst session spent its first
half discovering the effect never fired at all: `MatchResolver` has **two** clear paths and only one
of them reached the VFX. "Fires when a group clears, via `MatchResolver.FireBurst`" removes that
whole failure mode.

If the trigger is not known, say so explicitly — finding it is then the first task, not an
assumption.

### 2. Anchor and sorting

What the effect parents to (a unit, a board cell, world space), and what it must draw in front of or
behind: the board sprites, the unit sprite, the HP bar canvas. `ShardBurst` sits at `sortingOrder`
20 on `Default`. Getting this wrong is the most common reason an effect "doesn't appear" when it is
in fact rendering behind the board.

### 3. Beats, in seconds

Not "quick", not "snappy" — a timeline:

```
0.00        white flash at the impact point
0.05-0.45   sparks fly out and up
0.45-0.70   sparks fall and fade
```

Vague adjectives produce a wrong first pass every single time; the numbers can then be tuned by
±20% once it is on screen.

### 4. Reference

A `Reference/<slug>/` folder — see `Reference/README.md`. What matters is the **timestamp** and
**what specifically to copy** ("the way it holds full size while rising"), not the volume of
footage.

### 5. Colour source

Fixed palette, or sampled off the actual sprite at runtime?

This project already does the sampling: `PieceTintSampler`
([Assets/Scripts/Puzzle/Match/PieceTintSampler.cs:21](../../Assets/Scripts/Puzzle/Match/PieceTintSampler.cs#L21))
pulls three bands — dark / body / light — off a sprite, and `MatchResolver` passes them into
`ShardBurst.Play` so one material serves every block colour.

**A hand-maintained colour table indexed by `colorId` cannot work here.** `PieceSimple.colorId`
collides: id 8 is used by Orange, Red *and* Yellow. That is recorded in `ShardBurst.txt` and it is
why the sampler exists.

### 6. Budget

Mobile target. How many instances can be alive at once (wave peak, multi-piece clears), and whether
the effect needs pooling. `ShardBurst` pools 6 systems; `SummonVfxDirector` pools its emitters.

---

## 2. Reference material

`ffmpeg` and `ffprobe` are installed at `C:\ffmpeg\ffmpeg-7.1.1-full_build\bin`.

Video is **not** directly readable — frames are. Pull them with:

```powershell
.\.claude\scripts\vfx-frames.ps1 -Video "Reference\<slug>\clip.mp4" -Start 0:12.4 -End 0:14.0
```

Rules:

- **Frames never go under `Assets/`.** Unity imports every PNG it finds there. The script refuses
  such a path outright; the session scratchpad is the right destination.
- Number the beats to the frames, the way `ShardBurst.txt` documents its arc as `fr.1`…`fr.5`. That
  numbering is what makes a later tuning pass discussable — "frame 3 is wider than what we have".
- Old material lives in `Assets/Arts/Reference videos/` (committed and Unity-imported). Left as-is;
  new material goes in `Reference/`.

---

## 3. The house method — author in cells and seconds, build in code

Both existing effect families follow it, and it should be the default for new work.

**Build the ParticleSystem entirely in C#, not as an authored prefab.** `ShardBurst`
([Assets/Scripts/Puzzle/Match/ShardBurst.cs](../../Assets/Scripts/Puzzle/Match/ShardBurst.cs)) builds
every module in `BuildSystem`, self-bootstraps through `[RuntimeInitializeOnLoadMethod]`
(AfterSceneLoad) so there is nothing to wire in a scene, and pools its systems. A prefab would have
to be hand-authored in the Editor and cannot be reviewed in a diff.

**Author the shape in board cells and seconds; derive the physics from it.** This is the important
half. `ShardBurst` exposes `apexHeightCells` (1.20), `fallDepthCells` (2.25), `spreadOutCells`
(1.00) and `totalDuration` (0.58), then *solves* for gravity and launch speed:

```
T  = sqrt(2h/g) + sqrt(2(h+d)/g)
g  = 2 * (sqrt(h) + sqrt(h+d))^2 / T^2
vy = sqrt(2*g*h)
```

Those three authored numbers **together pin gravity — there is no fourth knob.** An earlier version
also authored `riseTime`; the resulting 65 u/s² gravity threw shards 16 cells below the board
instead of 2.25. When a bound is a design requirement, solve for it rather than tuning toward it.

Read `Assets/Documentation for scripts/ShardBurst.txt` in full before building anything ballistic —
it carries the per-shard lifetime solve, the impulse-vs-drift reasoning, and the measured numbers.

**The two exemplars to copy from:**

| Family | Files | Good example of |
|---|---|---|
| Shard burst | `Puzzle/Match/ShardBurst.cs` + `Arts/VFX/ShardUnlit.shader` + `Resources/VFX/ShardMesh_0..3.asset` | mesh particles, derived ballistics, sprite-sampled colour, pooling |
| Summon arrival | `Combat/VFX/SummonVfxDirector.cs`, `SummonEmitterParticles.cs`, `SummonArrivalBinder.cs`, `SummonGroundCircle.cs` | a director + emitter-interface split, a procedural shader, and binding to gameplay events **without touching gameplay code** |

`SummonArrivalBinder` is the pattern to reach for when an effect should not be welded into gameplay:
it subscribes to `SimpleJump2D.Jumped` / `.Landed`
([Assets/Scripts/Combat/Player/SimpleJump2D.cs:84](../../Assets/Scripts/Combat/Player/SimpleJump2D.cs#L84))
rather than editing the jump.

---

## 4. URP 2D hard constraints

Each of these already broke a session.

- **A Lit material is invisible.** The URP 2D Renderer never draws a `UniversalForward` pass, and
  the gameplay scenes contain no `Light` or `Light2D`. Unlit or custom shaders only.

- **Never configure `Universal Render Pipeline/Particles/Unlit` from script.** Its blend state comes
  from the material inspector, so a script-built instance draws every particle as an **opaque white
  square** no matter what the texture alpha says. This is the entire reason
  `Assets/Arts/Shaders/SummonAdditive.shader` exists.

- **Any shader reached via `Shader.Find` must be in Graphics ▸ Always Included Shaders**, or it
  strips out of an Android build and the effect is invisible on device but fine in the Editor.
  `Blasty/SummonAdditive`, `Blasty/SummonGroundCircle` and `Blasty/ShardUnlit` are already listed;
  a new one must be added.

- **Particle velocity curves must all share one curve mode**, or Unity rejects the module with
  *"Particle Velocity curves must all be in the same mode"*.

- **`ParticleSystemRenderer.pivot.y` is `+0.5` to shift a quad UP** — the opposite of the intuition.

- **Mobile budget:** Collision, Noise, Trails and Sub-Emitters stay OFF. Collision is the most
  expensive Shuriken module on mobile; Noise reads as smoke; Trails and Sub-Emitters double the
  draw calls.

- **One material per pooled system, not one shared material**, whenever colour lives in material
  properties rather than the particle COLOR stream (the stream carries one colour per particle, and
  `ShardUnlit` needs three bands). `MaterialPropertyBlock` is not a workaround: those properties
  live in `UnityPerMaterial` and the SRP Batcher ignores per-renderer overrides of them.

---

## 5. What can and cannot be authored from here

**Can:** ParticleSystem effects built in C#, `.shader` files, materials and meshes created from
script, scene/prefab wiring through `Unity_RunCommand`.

**Cannot: a `.vfx` VFX Graph asset.** The package is installed (`com.unity.visualeffectgraph@17.3.0`),
`SummonEmitterVfxGraph.cs` compiles behind the `SUMMON_VFX_GRAPH` define, and a node-by-node build
recipe exists at `Assets/Documentation for scripts/SummonPillarVFX-Recipe.txt` — but
`SummonPillar.vfx` **still does not exist**, because a graph asset has to be built by hand in the
VFX Graph window. If VFX Graph is wanted, the deliverable is a recipe plus the runtime code around
it, and someone has to open the window. Say this up front rather than at the end.

**Unity AI asset generation** (`Unity_AssetGeneration_GenerateAsset`) can make particle textures,
sprites and spritesheets — useful for glow, smoke and spark sheets. It **blocks the session while it
runs** (tens of seconds to minutes) and needs explicit consent first, so ask before the first one.

---

## 6. Verification — simulate and measure, do not eyeball

**First, check whether the Unity Editor is even running** (`Unity_GetConsoleLogs`). If it is closed,
say so before starting: the work will compile but ship unverified, which is exactly how the top
three Open Threads in `SESSIONS.md` got there.

With the Editor open, the loop that works:

1. **Compile** — `Unity_RunCommand`, confirm 0 errors.
2. **Simulate and measure.** `ParticleSystem.Simulate(t, true, true)` at fixed times in edit mode,
   then read `ps.GetParticles()` and compute the actual bounds. `ShardBurst` was signed off against
   numbers, not impressions:

   ```
   highest  Y = +1.17 cells  (target +1.20)
   deepest  Y = -2.78 cells  (target -2.25)
   widest  |X| =  1.41 cells (target <= 1.50)
   last shard dead at t = 0.57  (target 0.58)
   ```

   This is what catches a wrong gravity or a per-particle lifetime bug, which a screenshot never
   will.
3. **Render a filmstrip** through the Main Camera (`Unity_Camera_Capture` /
   `Unity_SceneView_Capture2DScene`) at the beat times, and look at it.
4. **Report the measured numbers against the authored targets**, and state plainly what was *not*
   verified — Play mode behaviour, pooling under load, sorting against a real scene.

A histogram comparison is worth it when colour is the point: the shard tint was signed off by
rendering a block and its shards through the same camera and comparing mean hex per band.

---

## 7. Trigger anchors — where effects hook in

Verified against the repo on 2026-09-05. Re-check before relying on any of them.

**Standing caution: this repo is full of numbered duplicate methods.** `RevivePanel` has both
`ShowLosePanel` ([:489](../../Assets/Scripts/UI/WinLose/RevivePanel.cs#L489)) and `ShowLosePanel1`
([:467](../../Assets/Scripts/UI/WinLose/RevivePanel.cs#L467)); the same pattern appears across the
codebase. **Grep for the live call site before hooking anything** — the newest-looking or
last-defined variant is often the dead one.

### Puzzle — match / blast

- `MatchResolver.FireBurst(...)`
  ([MatchResolver.cs:467](../../Assets/Scripts/Puzzle/Match/MatchResolver.cs#L467)) — the VFX entry
  point. Reached from **both** clear paths via `CollapseThenBurst`
  ([:379](../../Assets/Scripts/Puzzle/Match/MatchResolver.cs#L379)). Hook here, not in one path.
- `MatchResolver.OnBlast` ([:11](../../Assets/Scripts/Puzzle/Match/MatchResolver.cs#L11)) — a static
  `Action<int>`, fired at [:330](../../Assets/Scripts/Puzzle/Match/MatchResolver.cs#L330) and
  [:546](../../Assets/Scripts/Puzzle/Match/MatchResolver.cs#L546). Already has three subscribers
  (`PlayerWaveManager`, `BattleStartController`, `TutorialCondition`). It is never cleared by
  `MatchResolver`, so **anything subscribing must unsubscribe** or it leaks across scene loads.

### Combat — hit / death

Both funnel through one method per side, and death is a flag set inside it rather than a separate
call:

- `PlayerStats.ApplyDamageToPlayer` ([:19](../../Assets/Scripts/Combat/Player/PlayerStats.cs#L19)) —
  sets `playerIsdead` at [:26](../../Assets/Scripts/Combat/Player/PlayerStats.cs#L26).
- `EnemyStats.ApplyDamageToEnemy` ([:43](../../Assets/Scripts/Combat/Enemy/EnemyStats.cs#L43)) —
  sets `enemyIsdead` at [:50](../../Assets/Scripts/Combat/Enemy/EnemyStats.cs#L50).
- Castle gates: `PlayerGateStats.ApplyDamageToPlayerGate`
  ([:32](../../Assets/Scripts/Combat/Player/PlayerGateStats.cs#L32)) and
  `EnemyGateStats.ApplyDamageToEnemy`
  ([:49](../../Assets/Scripts/Combat/Enemy/EnemyGateStats.cs#L49)).
- The player death *animation/state* is `Combat/Player/States/PlayerDeathState.cs`. Enemies have no
  equivalent state file — they run off the `enemyIsdead` flag through `EnemyManager`.
- Note `PlayerStats` disagrees with itself about max HP (`maxHealth` in `Start`,
  `PlayerManager.statsBase.maxHP` in `ApplyDamageToPlayer`) — a known open thread. Don't build a
  health-fraction-driven effect on it without checking which one is right.

### Meta — level-up / skill card / win-lose

- `RogueliteManager.AddXP(float)` ([:453](../../Assets/Scripts/Roguelite/RogueliteManager.cs#L453))
  and `TryLevelUp()` ([:467](../../Assets/Scripts/Roguelite/RogueliteManager.cs#L467)).
- Skill card presentation: `Roguelite/SkillCardUI.cs`.
- Defeat: `LevelGameManager.EnterDefeatFlow`
  ([:173](../../Assets/Scripts/UI/WinLose/LevelGameManager.cs#L173)); `UI/WinLose/RevivePanel.cs`
  for the revive/lose panels (mind the numbered duplicates above).

### Unit arrival

- `SimpleJump2D.Jumped` / `.Landed`
  ([:84-85](../../Assets/Scripts/Combat/Player/SimpleJump2D.cs#L84)), already consumed by
  `SummonArrivalBinder`.

---

## 8. When the effect is done

1. **Write `Assets/Documentation for scripts/<Script>.txt`** for every new script, in the format the
   rest of that folder uses (PURPOSE / FIELDS / METHODS-or-FLOW / HOW IT CONNECTS / NOTES). The
   `PostToolUse` hook will demand it anyway. Put the hard-won gotchas in NOTES with `!!` markers, as
   `ShardBurst.txt` does — that file is the reason this playbook could be written at all.
2. **Append the `SESSIONS.md` entry** (`/wrap`), and be honest in **Verified**: "not play-tested"
   when it was not.
3. **Add any new `Shader.Find` shader to Graphics ▸ Always Included Shaders**, and record that in
   the session entry — it is a project-settings change, invisible in a normal diff.
