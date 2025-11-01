---
applyTo: "**"
---

# Commit All Changes

When the user requests to commit changes (or says "commit all", "check in", "checkin", etc.):

1. **Check git status** to identify all modified, added, or deleted files
2. **Review the diff** for all changed files to understand what was modified
3. **Verify the build** (if applicable) to ensure changes don't break compilation
4. **Stage all changes** using git add
5. **Commit with a descriptive message** that:
   - Follows conventional commit format (e.g., `feat:`, `fix:`, `refactor:`, `style:`, `docs:`, `build:`, `chore:`)
   - Summarizes the key changes in the first line
   - Includes bullet points for multiple changed files or significant changes
   - References file names or key changes for clarity

**Example commit messages:**

- `style: apply CSharpier formatting to test files and utilities`
- `fix: resolve docs generation task exit 126 and update gitignore`
- `refactor: apply C# naming conventions to private methods`

**Do not** create separate documentation files unless explicitly requested.
