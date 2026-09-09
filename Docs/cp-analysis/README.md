# Docs/cp-analysis

Analysis of the Combat Power (CP) system, produced 2026-09-07.

**This folder is documentation only. It is deliberately OUTSIDE `Assets/`.**
Unity never scans it, so it generates no `.meta` files, costs nothing at import, and adds nothing
to a build. Nothing in the Unity project references it. Total size ~355 KB, all plain text.

> **فارسی:** این پوشه فقط مستندات است و **عمداً بیرون از `Assets/`** گذاشته شده تا Unity آن را
> ایمپورت نکند، فایل `.meta` نسازد، و روی حجم بیلد اثری نگذارد. هر وقت خواستید، کل پوشه را با یک
> دستور حذف کنید — هیچ‌چیز در پروژه به آن ارجاع نمی‌دهد. راهنمای حذف در انتهای همین فایل.

---

## Contents

| Path | What it is |
|---|---|
| `CP_SYSTEM_ANALYSIS.md` | **The findings.** How CP is computed for heroes and enemies, the authored weight/growth data, per-hero and per-enemy CP tables, per-stage totals, runtime feasibility, a 9-item defect register, and the formula research. Bilingual: full English, then full Persian. |
| `CP_DISCUSSION_LOG.md` | **The decisions.** Open questions, ground rules for the discussion, and a running log. Start here when picking the thread back up. |
| `CP_SESSION_TRANSCRIPT.md` | **The conversation.** Everything Arash asked and everything Claude answered, 2026-09-07 → 08. Arash's messages verbatim; Claude's replies condensed but complete. Read this to continue the thread on another machine. |
| `NEXT_VERSION_CHANGES.md` | **The work list.** Every change queued for the next version — the CP formula, the stat-wiring fixes, the open defects, and the decisions still needed. **Start here when implementing.** |
| `report/CP_Redesign_Report.html` | Before/after evaluation of the formula change, with charts. Offline copy of the published artifact. |
| `report/CP_System_Report.html` | Offline copy of the charted report — open it in any browser, `Ctrl+P` for a PDF. No internet needed except for the web fonts. |
| `tools/cpcalc.js` | Recomputes **every number** in the analysis from the project's own `.asset` files. |
| `tools/build.js` | Rebuilds `report/CP_System_Report.html` from the template + computed data. |
| `tools/report.template.html` | The report page with a `/*__DATA__*/` placeholder that `build.js` fills. |

Live version of the report (any machine logged into claude.ai):
<https://claude.ai/code/artifact/3d635747-5eb6-43f8-85cf-e868e41e783d>

---

## Reproducing the numbers

No Unity, no install step, no dependencies. Node 18+ only.

```bash
node Docs/cp-analysis/tools/cpcalc.js      # parses the .asset YAML, writes tools/out.json
node Docs/cp-analysis/tools/build.js       # regenerates report/CP_System_Report.html
```

`cpcalc.js` is **read-only** with respect to the project — it parses `Assets/` and writes only
`out.json` next to itself. It reimplements, outside Unity:

- `AnimationCurve.Evaluate`, including cubic Hermite interpolation between keys and the
  PreInfinity/PostInfinity **clamp** behaviour — which is what exposed the four curves authored at
  negative time.
- `ProgressionMath.GetGrowthMultipliers` (the compounding `∏(1 + pct(l))` loop).
- `CPWeightMath.Evaluate` — **faithfully, including the bug**: `meleeMult` is never sampled from
  the config. Pass `buggy = false` to that function to model the intended behaviour instead.
- `CPCalculator.UnitCP`.

On finishing it prints seven **sanity checks** against values that were also computed by hand.
All seven must say `PASS`. If you retune a curve in the Editor and re-run, expect them to fail —
that is the script telling you the data moved, not that the script broke. Update the expected
values in the `checks` array at the bottom of `cpcalc.js`.

```
PASS  hero id1 (fast) CP @L1 == 81
PASS  hero id2 (slow) CP @L1 == 89
PASS  player gA @L20 ~= 3.6025
PASS  player gH @L20 ~= 4.9424
PASS  player gD @L20 ~= 3.1650
PASS  player gR @L20 ~= 3.4569
PASS  Golem_02 CP @stage20 == 478
```

The script also re-derives the roster and the per-stage wave composition by resolving asset GUIDs
through `.meta` files, so it stays correct if units are added or waves are re-authored.

---

## Deleting this folder

It is self-contained and nothing references it. When it is no longer wanted:

```bash
git rm -r "Docs/cp-analysis"
git commit -m "Remove the CP analysis docs"
```

Or, to drop the whole `Docs/` tree, remove `Docs/` instead. Because the folder lives outside
`Assets/`, there are no `.meta` files to clean up and no Unity references to break — deleting it
cannot affect the project, the Editor, or a build.

If you would rather keep the files but stop tracking them, add `Docs/` to `.gitignore` and run
`git rm -r --cached "Docs/cp-analysis"`.
