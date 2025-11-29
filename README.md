# DeDuBa

Deduplicating backup system (Perl original ported to C#) with native shims for OS-specific filesystem calls.

## Distribution

Create distributable archives for Linux and Windows using the packaging script (version embedded from MinVer):

```bash
# From the repo root
scripts/package.sh all Release

# Or individually
scripts/package.sh linux-x64 Debug
scripts/package.sh win-x64 Release
```

Artifacts are written to `dist/` (both versioned and previous non-versioned naming may coexist):

- `dist/DeDuBa-<version>-linux-x64/` with the `DeDuBa` binary, native `.so` shims, and a `run.sh` wrapper
- `dist/DeDuBa-<version>-linux-x64.tar.gz`
- `dist/DeDuBa-<version>-win-x64/` with `DeDuBa.exe` and native `.dll` shims
- `dist/DeDuBa-<version>-win-x64.zip`

Run on Linux via the wrapper to ensure the dynamic linker finds the native libs:

```bash
cd dist/DeDuBa-linux-x64
./run.sh <files-to-backup>
```

On Windows, just run `DeDuBa.exe` in-place.

### Verifying Downloads

The packaging script generates SHA-512 checksum files for integrity verification:

```bash
# Verify a single archive
sha512sum -c dist/DeDuBa-<version>-linux-x64.tar.gz.sha512

# Verify all archives in dist/
sha512sum -c dist/*.sha512
```

Successful verification will output:

```
DeDuBa-<version>-linux-x64.tar.gz: OK
```

If checksums don't match, the download may be corrupted or tampered with. Re-download the archive from a trusted source.

## Changelog

For curated release notes, see `docs/CHANGELOG.md`.

## Commit message best practices

Please avoid using literal `\\n` (backslash + letter n) escape sequences in commit messages. These often appear when commit messages are passed to `git -m` using double-quoted strings (e.g., `git commit -m "line1\\nline2"`), which results in a single-line commit message containing literal `\\n` characters. Use actual line breaks instead, such as:

- Use the interactive editor for multi-line messages: `git commit` and let the editor open
- Or create a message file and use `git commit -F message.txt`
- Or use `$'...'` quoting: `git commit -m $'First line\n\nBody line 1'`

The CI pipeline includes a lint job that rejects commits containing literal `\\n` sequences to keep history readable and tooling-friendly.

## Local Git hooks

To get the same commit message checks locally that CI enforces, install the repository's git hooks by running:

```bash
./scripts/install-git-hooks.sh
```

This sets `core.hooksPath` to the `.githooks/` directory and makes the `commit-msg` hook executable. The hook will reject raw `\\n` occurrences (unless they are single or double-quoted like `'\\n'` or `"\\n"`).

To undo: `git config --unset core.hooksPath`
