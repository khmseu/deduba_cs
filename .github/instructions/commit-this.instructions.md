```instructions
---
applyTo: '**'
---
Commit-handling guideline for automated agents
```

Commit-handling guideline for automated agents:

When asked to create or update commits in this repository in response to an interactive prompt, follow this exact sequence every time:

1. Call `#get_changed_files` to list files changed since the last check-in and inspect the diffs.
2. Analyze the changes and prepare a focused, GitHub/Conventional Commits style commit message that describes only those changes. Prepare the commit message in a file `commit-message.tmp` that you delete after the commit.
3. Use `#run_in_terminal` to execute the git commands (stage only the intended files, then commit with the prepared message). Do not include unrelated files or bulk-add the working tree.
4. After committing, report the commit hash and a concise summary back to the user.
   - Note: Prefer minor, focused commits. If generated artifacts are created during the work, ask the user whether they should be committed before adding them.

---
