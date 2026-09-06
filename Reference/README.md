# `Reference/` — visual reference intake

This is where reference material for VFX (and any other visual work) comes in.

**Nothing in here is committed.** `.gitignore` excludes `/Reference/*` and un-excludes only this
README, so you can drop 200 MB of screen recordings in here without touching the repo size. It also
lives **outside `Assets/`**, so Unity never imports it — a video under `Assets/` becomes a VideoClip
asset and a folder of frames becomes hundreds of Texture imports.

---

## The one thing that matters: Claude cannot watch video

Video files are not readable directly. What actually happens is that `ffmpeg` extracts frames and
Claude looks at **the frames**. That works well — the whole shard-burst effect was rebuilt from five
extracted frames — but it means a 4-minute clip with no timestamp is close to useless, because
there is no way to know which two seconds matter.

So: **a video is genuinely useful, as long as it comes with a timestamp.**

Ranked, best to worst:

| What you drop in | Why |
|---|---|
| **3–6 stills** of the key beats (start / peak / dissipate) | Best. Full detail, no extraction step, no motion blur. This is what the shard burst was built from. |
| **A 2–5 s clip**, trimmed to the effect only | Very good. Frames get pulled at ~8 fps and read directly. |
| **A long clip + a timestamp** in `notes.txt` | Good. The timestamp is the whole difference. |
| A long clip, nothing else | Weak. Expect a wrong guess about which moment you meant. |

---

## Layout

One folder per effect, named as a slug:

```
Reference/
├── README.md            ← the only tracked file
├── match-blast/
│   ├── notes.txt
│   ├── beat-1-flash.png
│   ├── beat-2-spread.png
│   └── beat-3-fall.png
└── hero-death/
    ├── notes.txt
    └── clip.mp4
```

## `notes.txt` — what to write

Two things, and they are worth more than more footage:

1. **Where to look** — `explosion at 0:12.4–0:14.0`
2. **What to copy** — the specific quality you want, not the whole look.
   - Good: *"the way the shards keep full size while rising, then only shrink on the fall"*
   - Good: *"the ring expands fast then holds — the hold is the part I want"*
   - Weak: *"make it like this"* — this reproduces whatever gets noticed first, which is usually
     not the thing you cared about.

Anything else that constrains the work belongs here too: it must not cover the HP bar, it has to
read at phone size, it plays 8 times a second at wave peak, and so on.

---

## Using it

Once a folder is in place:

```
/vfx match-blast
```

The `/vfx` command reads `notes.txt`, pulls frames from any clip using your timestamps, and works
through the rest of the brief with you. See [.claude/docs/VFX-Playbook.md](../.claude/docs/VFX-Playbook.md)
for what it does with them.

To pull frames by hand:

```powershell
.\.claude\scripts\vfx-frames.ps1 -Video "Reference\hero-death\clip.mp4" -Start 0:12.4 -End 0:14.0
```

## A note on the old folder

`Assets/Arts/Reference videos/` still exists and its two clips are **committed to git and imported
by Unity**. They are left alone on purpose — earlier sessions reference them by name. New material
goes here instead.
