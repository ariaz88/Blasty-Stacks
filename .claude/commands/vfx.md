---
description: Build a VFX for this project - collects the brief, reads the reference frames, and verifies the result
argument-hint: <effect slug or a full brief>
---

Build a visual effect for Blasty-Stacks: `$ARGUMENTS`

Work through the steps below in order. Do not skip step 1 or step 4.

## 1. Read the playbook first

Read `.claude/docs/VFX-Playbook.md` in full. It carries the house method (author in board cells and
seconds, build the ParticleSystem in C#), the URP-2D constraints that make effects invisible when
ignored, the trigger anchor map, and the verification loop. Also read
`Assets/Documentation for scripts/ShardBurst.txt` if the effect involves ballistics or particles
with an arc — it is the worked example.

## 2. Fill the six-slot brief

From what the user wrote, fill in as much of this as you can:

1. **Trigger** — when it fires, and through which code path
2. **Anchor and sorting** — what it parents to, what it draws in front of / behind
3. **Beats in seconds** — e.g. `0.00 flash / 0.05-0.45 spread / 0.45-0.70 fall and fade`
4. **Reference** — which `Reference/<slug>/` folder, which timestamp, what specifically to copy
5. **Colour source** — fixed palette, or sampled off the sprite the way `PieceTintSampler` does
6. **Budget** — how many alive at once, mobile

Then ask — once, with `AskUserQuestion` — only about the slots whose absence would genuinely change
what you build. Do not interrogate the user for all six. In particular:

- **Beats and budget can be proposed rather than asked.** Offer a concrete timeline based on the
  reference and let the user correct it; a wrong 0.6 s is easy to fix, a blocking question is not.
- **The trigger is worth asking about** if it is ambiguous, because guessing it wrong wastes the
  whole build. If the user does not know, finding it in the code is your first task — say that.
- If the user gave a complete brief, ask nothing and proceed.

## 3. Ingest the reference

If a `Reference/<slug>/` folder exists, read its `notes.txt` first, then:

- pull frames from any clip with `.claude/scripts/vfx-frames.ps1` using the timestamps in the notes
  (never write frames under `Assets/`),
- read the stills and the extracted frames,
- restate the effect as **numbered beats tied to specific frames** (`fr.1`…`fr.5`), the way
  `ShardBurst.txt` does, and confirm that reading with the user before building.

If there is no reference folder, say so and work from the described beats.

## 4. Confirm the trigger in the code — do not assume it

Grep for the call site and check **every** path that can reach it. `MatchResolver` has two clear
paths that both must reach the effect; an effect wired into only one of them silently never fires,
which is exactly what happened on 2026-08-25.

Watch for the numbered-duplicate pattern documented in `CLAUDE.md` — `ShowLosePanel` vs
`ShowLosePanel1` and friends. Confirm which variant is actually live before hooking it.

## 5. Report the Editor state before building

Call `Unity_GetConsoleLogs`. If the Unity Editor is not running, tell the user up front that the
effect will be written and compiled but **not verified**, and let them decide whether to open Unity
first. Do not discover this at the end.

## 6. Build it, following the house method

- ParticleSystem built entirely in C#, self-bootstrapping, pooled. No hand-authored prefab.
- Author the shape in **board cells and seconds**, and derive the physics from those numbers.
- Unlit or custom shader only. Any shader reached by `Shader.Find` goes into
  Graphics ▸ Always Included Shaders.
- Collision / Noise / Trails / Sub-Emitters stay off.
- Prefer binding to an existing gameplay event over editing gameplay code, the way
  `SummonArrivalBinder` binds to `SimpleJump2D.Jumped`/`.Landed`.

## 7. Verify by measuring, not by eyeballing

1. Compile — 0 errors.
2. `ParticleSystem.Simulate` at fixed times, read `ps.GetParticles()`, and compute the actual
   bounds (highest, deepest, widest, time of last death).
3. Capture a filmstrip through the Main Camera at the beat times and look at it.
4. **Report the measured numbers against the authored targets**, and state plainly what was not
   verified — Play mode, pooling under load, sorting in a real scene.

## 8. Close out

- Write `Assets/Documentation for scripts/<Script>.txt` for every new script, in the folder's
  format, with the gotchas in NOTES marked `!!`.
- Append the `SESSIONS.md` entry, honest about what was and was not play-tested.
- If a project setting changed (Always Included Shaders, a scripting define), record it explicitly —
  it does not show up in a diff.
