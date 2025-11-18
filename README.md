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

## Changelog

For curated release notes, see `docs/CHANGELOG.md`.
