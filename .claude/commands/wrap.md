---
description: Write this session's entry into the shared cross-session log (SESSIONS.md)
---

Close out this session in the shared cross-session log.

1. Read `SESSIONS.md` at the repo root and follow the protocol written in it.
2. Append an entry to the **top** of the Session Log describing what this session actually did,
   using the entry template in that file. Cover every meaningful change from this conversation,
   not just the last one.
   - Fill **Scene/Prefab/SO edits** honestly: manual Unity Editor changes to `.unity`, `.prefab`,
     or `.asset` files are invisible in a git diff, so name them explicitly or write `none`.
   - Fill **Verified** honestly: say "not verified" if the change was never run in Play mode.
3. Update **Open Threads** — add anything left unfinished, remove anything this session closed.
4. Add a row to **Decisions** only if a durable choice was made (architecture, naming, an approach
   rejected and why). Skip it for routine edits.
5. If the log now exceeds ~20 entries, move the oldest ones into `SESSIONS-ARCHIVE.md`.

If this session already has an entry in the log, edit that entry instead of adding a second one.

Then report in one or two lines what you recorded.
