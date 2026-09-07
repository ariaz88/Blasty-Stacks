# CP_DISCUSSION_LOG — open questions on the Combat Power system

    Purpose: the living record of the CP discussion between Arash and Claude.
             CP_SYSTEM_ANALYSIS.md is the FINDINGS (what is true).
             This file is the DECISIONS (what we choose to do about it).
    Opened : 2026-09-07
    Status : discussion in progress — NOTHING has been decided, NOTHING has been changed.

> **فارسی:** این فایل دفترچه‌ی گفتگوی ماست. یافته‌ها در `CP_SYSTEM_ANALYSIS.md` هستند؛
> اینجا فقط تصمیم‌ها و سؤال‌های باز ثبت می‌شوند. بخش فارسی در انتهای همین فایل است.

---

## Read these first

| What | Where |
|---|---|
| The findings — formula, data, charts, 9 defects, formula research | `Docs/cp-analysis/CP_SYSTEM_ANALYSIS.md` |
| Same report with interactive charts, EN + FA, printable to PDF | <https://claude.ai/code/artifact/3d635747-5eb6-43f8-85cf-e868e41e783d> |
| The script that produced every number (re-runnable) | `Docs/cp-analysis/tools/` — see `Docs/cp-analysis/README.md` |
| Offline copy of the charted report (open in any browser) | `Docs/cp-analysis/report/CP_System_Report.html` |
| Cross-session project log (auto-injected at session start) | `SESSIONS.md` |

---

## Ground rules for this thread

These were set by Arash and hold until he says otherwise:

1. **Reply in English**, even though he writes in Persian.
2. **Research and analysis only — do not change code, assets, prefabs or scenes** unless he
   explicitly asks for a change. The CP investigation was done under this rule and honoured it:
   as of 2026-09-07 not one `.cs` / `.asset` / `.prefab` / `.unity` file was modified.
3. **Be precise over fast.** He said time does not matter; accuracy does. Every claim should carry
   a `file.cs:line` or an `Asset.asset:line`.
4. Points are raised **one at a time**. Record each one below as it is resolved.
5. **Saving is manual and scoped.** Agreed 2026-09-07: after each point is settled, Claude writes it
   into the Discussion log below and makes **one commit** for it. No auto-commit hook was installed,
   and the raw chat transcript is deliberately **not** stored in the repo. Nothing is pushed by
   Claude — `git push` needs Arash's GitHub credentials, which the Claude Code shell cannot prompt
   for, so **pushing is always his step.** If a session ends with unpushed commits, they are still
   safe locally; they just are not on GitHub yet.

---

## State of play as of 2026-09-07

Arash has read the full report and is now raising specific points for discussion, one by one.
**No decision has been made on any of the 9 defects or on the formula shape.** Nothing below the
"Decisions taken" heading means anything until it has a date and an outcome.

### The three findings the discussion is likely to circle around

1. **`CP ≈ ATK + 0.15·HP`.** At level 1: attack 79.3% of the score, HP 18.6%, the other four stats
   2.1% combined, defense exactly 0.00%. Defense never exceeds 0.17% of CP at any level.
2. **CP ranks the roster backwards.** The 128-DPS hero profile (ATK 64 / AtkSpd 2.0 — ids 1, 6, 7)
   displays **CP 81**; the 108-DPS profile (ATK 72 / AtkSpd 1.5 — ids 2, 3, 4, 5, 8) displays
   **CP 89**. True at every level from 1 to 50.
3. **Four `AnimationCurve`s are authored at negative time** and silently clamp to values nobody
   chose — `defPctByLevel` (flat +6.25%/level → ×3.17 defense at L20), `rangePctByLevel`
   (+6.75%/level), `meleeMultByLevel`, `rangedMultByLevel`. `pctClamp` does not catch them because
   the values sit inside the legal window; they are simply applied at the wrong place.

---

## Open decisions

Nothing here is decided. Each row gets an outcome and a date when it is settled.

### A. The 9 defects — fix, defer, or accept?

Full detail for each is in `CP_SYSTEM_ANALYSIS.md` §8.

| # | Defect (short) | Severity | Decision | Date |
|---|---|---|---|---|
| 01 | Off-axis growth curves inflate defense +6.25%/lvl and range +6.75%/lvl | High | — | — |
| 02 | Menus apply 4 of 6 growth multipliers, combat applies 6 (L10 hero: menu says DEF 25, fights with 43.1) | High | — | — |
| 03 | `CPWeightMath.cs:41-42` never samples `meleeMultByLevel`; `rangedMult` assigned twice | High | — | — |
| 04 | Both flavour curves in `Player CP.asset` authored off-axis → every `typeMult` is 1.0 | High | — | — |
| 05 | Second, divergent CP formula inside `EnemyManager` reachable via an empty Inspector slot | Medium | — | — |
| 06 | Enemy defense never grows — flat across all 20 stages while ATK ×3.6 and HP ×4.9 | Medium | — | — |
| 07 | CP prices `moveSpeed` and `attackRange`, both inert in gameplay | Medium | — | — |
| 08 | Menu `TOTAL CP: 3460 / 32660` are hard-coded strings bound to no script | Low | — | — |
| 09 | Three per-script `.txt` docs describe code that no longer exists | Low | — | — |

### B. The formula shape

| Option | Ranks the roster correctly? | Decision | Date |
|---|---|---|---|
| A — keep the current weighted sum, retune the weights | Only if weights are rebuilt around real combat sensitivities | — | — |
| B — product with √ damping (Pokémon GO shape) | Yes (ratio 1.19, matches true DPS) | — | — |
| C — DPS × EffectiveHP (`CPCalculator.EffectiveHP` already exists, unused) | Yes (ratio 1.19) | — | — |
| Do nothing — CP is cosmetic, accept it | n/a | — | — |

### C. Runtime total CP

| Question | Decision | Date |
|---|---|---|
| Wire up a real squad/wave total CP readout? (`CPCalculator.SquadCP` exists, zero call sites) | — | — |
| Replace the hard-coded menu `TOTAL CP` labels with a computed value? | — | — |

### D. Balance observations surfaced by the CP data (not CP bugs)

| Observation | Decision | Date |
|---|---|---|
| Stage ramp is uneven: +91.9% into stage 3 and +29.9% into 19, vs +3.6% into 6 and +3.8% into 15 | — | — |
| Enemy castle HP is a flat 350 at every stage 1→20 — the win condition never gets harder | — | — |
| The roster is two stat profiles wearing eight costumes; the "fast" three are strictly better | — | — |

---

## Discussion log

_Newest last. One entry per point Arash raises. Record the question, the answer, and the outcome._

### (no points recorded yet — discussion started 2026-09-07)

<!--
Template for each point:

### YYYY-MM-DD — <the point, in one line>
- **Arash asked:** ...
- **Answer / finding:** ... (cite file:line)
- **Outcome:** decided X / deferred / needs more investigation / no action
- **Files touched:** ... or `none`
-->

---

## Decisions taken

_Empty. Move a row here from "Open decisions" once it is settled, with the reasoning._

---

## How to resume this on another computer

1. `git clone https://github.com/ariaz88/Blasty-Stacks.git` (or `git pull` if you already have it).
2. Open the repo in Claude Code. The `SessionStart` hook in `.claude/settings.json` injects
   `SESSIONS.md` automatically, so the new session already knows the project history.
3. Tell it: **"read `Docs/cp-analysis/CP_DISCUSSION_LOG.md` and
   `Docs/cp-analysis/CP_SYSTEM_ANALYSIS.md`, then continue the CP discussion."** Those two files
   plus `SESSIONS.md` contain everything the original session concluded.
4. The charted report is at the artifact URL above — it opens in a browser on any machine you are
   logged into claude.ai with.

**What does NOT transfer:** the literal Claude Code chat transcript. It lives only on the machine
that ran it (`~/.claude/projects/<project>/<session-id>.jsonl`) and Claude Code does not sync
sessions between computers. That is exactly why this file exists — it carries the *conclusions*
so a fresh session does not have to redo the investigation.

---
---

# بخش فارسی

## این فایل چیست

`CP_SYSTEM_ANALYSIS.md` = **یافته‌ها** (چه چیزی درست است).
این فایل = **تصمیم‌ها** (قرار است چه کار کنیم).

از تاریخ ۲۰۲۶-۰۹-۰۷: **هیچ تصمیمی گرفته نشده و هیچ کدی تغییر نکرده است.**

## قواعد این گفتگو

۱. پاسخ‌ها **به انگلیسی**، حتی وقتی سؤال فارسی است.
۲. **فقط تحقیق و تحلیل — بدون تغییر کد، اسِت، پریفب یا صحنه**، مگر اینکه آرش صریحاً تغییر بخواهد.
۳. **دقت مهم‌تر از سرعت است.** هر ادعا باید یک `file.cs:line` یا `Asset.asset:line` همراه داشته باشد.
۴. نکته‌ها **یکی‌یکی** مطرح می‌شوند و هرکدام در «Discussion log» بالا ثبت می‌شود.

## سه یافته‌ای که احتمالاً محور بحث خواهند بود

۱. **`CP ≈ ATK + 0.15·HP`** — در لِوِل ۱: حمله ۷۹٫۳٪، جان ۱۸٫۶٪، چهار استت دیگر روی‌هم ۲٫۱٪،
   و دفاع دقیقاً ۰٫۰۰٪. سهم دفاع در هیچ لِوِلی از ۰٫۱۷٪ فراتر نمی‌رود.
۲. **CP روستر را وارونه رتبه‌بندی می‌کند** — پروفایل ۱۲۸ DPS عدد **۸۱** نشان می‌دهد و پروفایل
   ۱۰۸ DPS عدد **۸۹**. این در تمام لِوِل‌های ۱ تا ۵۰ برقرار است.
۳. **چهار `AnimationCurve` روی زمان منفی نوشته شده‌اند** و بی‌سروصدا به مقادیری clamp می‌شوند که
   کسی انتخابشان نکرده — `defPctByLevel` (ثابت ۶٫۲۵٪+ در هر لِوِل)، `rangePctByLevel` (۶٫۷۵٪+)،
   و هر دو منحنی سبک. `pctClamp` جلویشان را نمی‌گیرد چون مقادیر داخل بازه‌ی مجازند.

## ادامه‌دادن از یک کامپیوتر دیگر

۱. `git clone https://github.com/ariaz88/Blasty-Stacks.git` (یا `git pull`).
۲. پروژه را در Claude Code باز کنید. هوک `SessionStart` فایل `SESSIONS.md` را خودکار تزریق می‌کند،
   پس سشن جدید تاریخچه‌ی پروژه را از قبل می‌داند.
۳. بگویید: **«فایل‌های `CP_DISCUSSION_LOG.md` و `CP_SYSTEM_ANALYSIS.md` را بخوان و بحث CP را ادامه بده.»**
۴. گزارش نموداری از طریق لینک artifact بالا روی هر کامپیوتری که به claude.ai لاگین باشید باز می‌شود.

**چه چیزی منتقل نمی‌شود:** خودِ متن چت Claude Code. آن فقط روی همان کامپیوتر ذخیره می‌شود
(`~/.claude/projects/<project>/<session-id>.jsonl`) و Claude Code سشن‌ها را بین کامپیوترها
همگام‌سازی نمی‌کند. دقیقاً به همین دلیل این فایل ساخته شده — **نتیجه‌ها** را منتقل می‌کند تا سشن
جدید مجبور نباشد کل تحقیق را از اول انجام دهد.
