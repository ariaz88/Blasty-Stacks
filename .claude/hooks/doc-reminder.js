// PostToolUse hook (Write|Edit): after a script under Assets/Scripts/**/*.cs changes
// (excluding Assets/Scripts/_Legacy/**), reminds the model to update or create the
// matching reference doc in "Assets/Documentation for scripts/".
let data = '';
process.stdin.on('data', (c) => { data += c; });
process.stdin.on('end', () => {
  try {
    const input = JSON.parse(data || '{}');
    const path =
      (input.tool_response && input.tool_response.filePath) ||
      (input.tool_input && input.tool_input.file_path) ||
      '';
    const norm = path.split('\\').join('/');

    const inScripts = norm.includes('/Assets/Scripts/') || norm.startsWith('Assets/Scripts/');
    const isLegacy =
      norm.includes('/Assets/Scripts/_Legacy/') || norm.startsWith('Assets/Scripts/_Legacy/');

    if (inScripts && norm.endsWith('.cs') && !isLegacy) {
      const base = norm.split('/').pop().replace(/\.cs$/, '');
      const docPath = 'Assets/Documentation for scripts/' + base + '.txt';
      const msg =
        'Script changed: ' + norm + '\n' +
        'Project convention: update (or create, for a new script) its reference doc at "' +
        docPath + '" before finishing this task. Read the current source, then write ' +
        'PURPOSE / FIELDS / METHODS-or-FLOW / HOW IT CONNECTS / NOTES, matching the format ' +
        'already used throughout "Assets/Documentation for scripts/".';
      process.stdout.write(JSON.stringify({
        hookSpecificOutput: { hookEventName: 'PostToolUse', additionalContext: msg },
      }));
    }
  } catch (e) {
    // never block the tool call on a hook parsing error
  }
});
