// SessionStart hook: injects SESSIONS.md (the cross-session log) into every session's context.
// Wired up in .claude/settings.json. Fails silently if the file is missing.
const fs = require('fs');
const path = require('path');

const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const file = path.join(root, 'SESSIONS.md');

let body;
try {
  body = fs.readFileSync(file, 'utf8');
} catch (e) {
  process.exit(0); // no log yet — nothing to inject
}

// This file is injected in full into EVERY session, so its size is a per-session context tax.
// It reached 233 KB once before anyone noticed; warn loudly well before that happens again.
const SOFT_CAP = 40 * 1024;
const bytes = Buffer.byteLength(body, 'utf8');

const warning = bytes > SOFT_CAP
  ? '\n\n!! SESSIONS.md IS OVER ITS SIZE CAP (' + Math.round(bytes / 1024) + ' KB, cap 40 KB). ' +
    'It is injected into every session, so this is costing context on every run. Before you ' +
    'finish this session, trim it back under the cap per its own Protocol section: collapse ' +
    'Session Log entries to one line each, cap Open Threads at 3 lines, and move the long-form ' +
    'text verbatim into SESSIONS-ARCHIVE.md (which is NOT injected). Tell the user you did it.'
  : '';

const context =
  'Contents of SESSIONS.md — the shared cross-session log for this repo. It records what other ' +
  'Claude Code sessions already did, decided, and left unfinished. Read it before proposing or ' +
  'changing anything, and append an entry to its Session Log when you finish meaningful work. ' +
  'Long-form detail behind any one-line entry lives in SESSIONS-ARCHIVE.md — grep it on demand, ' +
  'never read it whole.\n\n' +
  body + warning;

process.stdout.write(JSON.stringify({
  hookSpecificOutput: {
    hookEventName: 'SessionStart',
    additionalContext: context,
  },
}));
