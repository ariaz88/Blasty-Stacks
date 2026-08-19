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

const context =
  'Contents of SESSIONS.md — the shared cross-session log for this repo. It records what other ' +
  'Claude Code sessions already did, decided, and left unfinished. Read it before proposing or ' +
  'changing anything, and append an entry to its Session Log when you finish meaningful work.\n\n' +
  body;

process.stdout.write(JSON.stringify({
  hookSpecificOutput: {
    hookEventName: 'SessionStart',
    additionalContext: context,
  },
}));
