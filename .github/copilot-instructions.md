# DeDuBa - Deduplicating Backup System — Copilot instructions for AI coding agents

Goal: help an AI agent quickly understand, build, test and modify DeDuBa
with minimal back-and-forth. Keep edits small, focused, and well-tested.

- Architecture (one-paragraph): DeDuBa is a C#/.NET (net8.0) backup
  application using content-addressable storage (SHA-512 chunks + BZip2)
  and a three-layer native interop: C# platform wrappers → OsCallsCommon
  bridge (ValXfer) → native shims (OsCalls\*Shim). Key projects: `DeDuBa`,
  `OsCallsCommon`, `OsCallsCommonShim`, `OsCallsLinux[Shim]`, `OsCallsWindows[Shim]`,
  `ArchiveStore` (content-addressable store) and `DeDuBa.Test` (xUnit).

- Essential workflows:
  - Build full workspace: `dotnet build` (CMake/Make invoked by shim csproj).
  - Run tests: `dotnet test DeDuBa.Test` or run a single test with `--filter`.
  - Run app locally: set native path then `dotnet run --project DeDuBa -- <args>`;
    e.g. `export LD_LIBRARY_PATH="$PWD/OsCallsCommonShim/bin/Debug/net8.0:$PWD/OsCallsLinuxShim/bin/Debug/net8.0"`.
  - Rebuild native shims: they are produced by CMake in `OsCalls*Shim` (look at CMakeLists).

- Project-specific conventions (do not deviate):
  - Use the `IHighLevelOsApi` abstraction for OS metadata collection; implement
    platform behavior in `OsCallsLinux` / `OsCallsWindows` (see `WindowsHighLevelOsApi.cs`).
  - Error handling: prefer `Utilities.Error(file, op, ex)` to throwing directly.
  - Commit messages: follow Conventional Commits and avoid literal `\n` sequences in `-m` text.
  - Commit-handling guideline for automated agents: when asked to create or update commits in this repository in response to an interactive prompt, follow this exact sequence every time:
    1. Call `#get_changed_files` to list files changed since the last check-in and inspect the diffs.
    2. Analyze the changes and prepare a focused, GitHub/Conventional Commits style commit message that describes only those changes. Prepare the commit message in a file `commit-message.tmp` that you delete after the commit.
    3. Use `#run_in_terminal` to execute the git commands (stage only the intended files, then commit with the prepared message). Do not include unrelated files or bulk-add the working tree.
    4. After committing, report the commit hash and a concise summary back to the user.
       - Note: Prefer minor, focused commits. If generated artifacts are created during the work, ask the user whether they should be committed before adding them.

- Files to read first when changing behavior:
  - `DeDuBa/Deduba.cs` — main backup logic and `Backup_worker`.
  - `DeDuBa/ArchiveStore.cs` and `IArchiveStore` — storage semantics and APIs.
  - `OsCallsCommon/ValXfer.cs` and `OsCallsCommonShim/include/ValXfer.h` — native iterator protocol.
  - `OsCallsLinux/FileSystem.cs`, `OsCallsWindows/FileSystem.cs` and shim C++ sources under `OsCalls*Shim/src`.

- CI and release notes:
  - Repo uses GitHub Actions; tags `v*` trigger the release workflow.
  - When creating a tag or a release, update `docs/CHANGELOG.md` and push tag; CI will build packages and create a release.

- Troubleshooting quick tips:
  - If you see `DllNotFoundException` or missing entry points: ensure native shim builds and that native DLL/.so are copied to managed output (check CMake outputs and csproj copy tasks).
  - Windows-specific issues often relate to exports/packing (`ValXfer.h` packing, `.def` or CMake export header), and native -> managed struct alignment.
  - For test flakiness: reset per-run static state (look at `DedubaClass` reset logic) and prefer deterministic logging (canonical paths).

- When editing code:
  1. Run `dotnet build` and fix compile issues.
  2. Run affected unit/integration tests locally (`dotnet test --filter "FullyQualifiedName~YourTest"`).
  3. If native changes were made, rebuild shims via CMake and ensure the resulting native library is present in test output.

If anything here is unclear or you want more detail (examples of implementing a new OS capability, script templates for running a Windows-native test locally, or exact CMake commands), tell me which area to expand.

## Architecture Overview

DeDuBa is a deduplicating backup system ported from Perl to C&num;. Uses content-addressable storage with SHA-512 hashing and BZip2 compression.

**9-Project Solution Structure:**

1. **DeDuBa** - Main backup logic (`DeDuBa/Deduba.cs`)
2. **DeDuBa.Test** - xUnit tests for ACLs, xattrs, core functionality
3. **OsCallsCommon** - Platform-agnostic C&num; ValueT→JSON bridge
4. **OsCallsCommonShim** - Shared C++ ValueT cursor (CreateHandle/GetNextValue)
5. **OsCallsLinux** - Linux C&num; wrappers (lstat, ACLs, xattrs via P/Invoke)
6. **OsCallsLinuxShim** - Linux native POSIX implementations (`libOsCallsLinuxShim.so`)
7. **OsCallsWindows** - Windows C&num; wrappers (stubs for security, ADS)
8. **OsCallsWindowsShim** - Windows native stubs (`OsCallsWindowsShim.dll`)
9. **UtilitiesLibrary** - Logging, debugging, JSON serialization

## Critical Architecture Patterns

### Cross-Platform Native Interop (3-Layer Design)

#### Flow: C&num; Platform Wrapper → Common Bridge → Platform Native Impl

1. **Common bridge** (`OsCallsCommon/ValXfer.cs`):
   - `PlatformGetNextValue` delegate set at runtime by OsCallsLinux/Windows

## Release checklist (quick)

When preparing a release, follow these steps to ensure CI and artifacts are consistent:

- Update `docs/CHANGELOG.md` with a new section for the version (date + summary) from the git comments since the previous release on that branch.
- Commit the changelog entry (use a conventional commit like `docs(changelog): prepare vX.Y.Z`).
- Create an annotated tag: `git tag -a vX.Y.Z -m "vX.Y.Z: short summary"`.
- Push commits and tag: `git push origin master && git push origin vX.Y.Z`.
- Wait for the GitHub Actions run to complete successfully for the tag (use `gh run list --limit 5 --repo <owner>/<repo>` and `gh run watch <run-id>`).
- If CI artifacts are produced, verify checksums and that `release-files/release-metadata.json` is present.
- Create a GitHub Release (draft) with the changelog notes or use the `docs/CHANGELOG.md` entry as the release body.

Examples (preferred):

```bash
# push local work and tag
git push origin master
git tag -a v0.1.4-alpha -m "v0.1.4-alpha: first version barely portable to Windows"
git push origin v0.1.4-alpha

# create a draft release using gh (recommended)
gh release create v0.1.4-alpha \
  --title "v0.1.4-alpha" \
  --notes-file docs/CHANGELOG.md --repo khmseu/deduba_cs --draft

# watch CI runs
gh run list --repo khmseu/deduba_cs --limit 5
gh run watch --repo khmseu/deduba_cs <run-id>
```

### Prefer `gh` over raw HTTP

This repository authorizes the `gh` CLI for the account and repo. Use `gh` for creating releases, querying runs, and fetching artifacts instead of raw HTTP requests (curl/wget/fetch), because `gh` handles authentication, redirects, artifact downloads, and rate-limiting more reliably. Example operations:

- `gh release create` — create a release with notes and artifacts
- `gh run download <run-id>` — fetch workflow artifacts
- `gh run list`, `gh run watch` — inspect workflow run status

If `gh` is not available on the runner, fall back to the documented HTTP endpoints, but prefer `gh` whenever possible.

./demo-xattr.sh &num; Demo xattr functionality

## Post-release: ensure future commits show an increased version

This repo uses MinVer (see `Directory.Build.props`) which derives the build
version from git tags. After publishing a release (for example `v0.1.4-alpha`),
you have two options to ensure subsequent commits are reported under an
incremented base version:

- Option A (recommended): Update the changelog and commit the next development
  intent. Edit `docs/CHANGELOG.md` to add an `Unreleased` or the next version
  header (e.g. `## [0.1.5-alpha] - UNRELEASED`) and commit with a conventional
  message such as:

  ```bash
  git add docs/CHANGELOG.md
  git commit -m "docs(changelog): prepare 0.1.5-alpha (post-release)"
  git push origin master
  ```

  This documents the next target version for humans and automated docs.

- Option B: If you need the _base_ semver to advance immediately (so builds
  show `0.1.5-alpha` rather than `0.1.4-alpha.<height>`), create the next
  pre-release tag right after publishing the release:

  ```bash
  git tag -a v0.1.5-alpha -m "v0.1.5-alpha: start next development cycle"
  git push origin v0.1.5-alpha
  ```

Notes

- If you do nothing, MinVer will continue to show the released tag plus a
  commit height suffix (e.g., `0.1.4-alpha.1`), which is valid but keeps the
  base semver identical to the last tag.
- Pick Option A for a clean changelog-driven workflow; pick Option B when
  other systems expect an immediate semantic bump.

## Included: Release Process (merged from `.github/RELEASE.md`)

The following content was merged from `.github/RELEASE.md` to centralize release
and tagging guidance in this single instructions file. It documents the release
process, MinVer behaviour, CI interactions, and best practices for creating
GitHub Releases for DeDuBa. Keep this section synchronized with the original
release workflow files if those change.

---

## Release Process

This document explains how GitHub releases work for DeDuBa. It covers
automatic version numbering, release notes generation, and how releases
relate to `docs/CHANGELOG.md`.

## Overview

DeDuBa uses **automated GitHub releases** triggered by git version tags. The process combines:

- **MinVer** for automatic semantic versioning from git tags
- **GitHub Actions** for building, testing, and packaging
- **Automatic release notes** generated from commit messages
- **Manual CHANGELOG.md** (in `docs/`) for curated release summaries

## How to Create a Release

### 1. Prepare the Release

Update `docs/CHANGELOG.md` with the new version section, then commit the changes.

### 2. Create and Push a Version Tag

Create an annotated tag following semantic versioning, e.g. `git tag -a v0.2.0 -m "Release version 0.2.0"`, then `git push origin v0.2.0`.

### 3. Automatic Build and Release

Once the tag is pushed, GitHub Actions will build, run tests, package binaries and create a GitHub Release with artifacts and notes.

## Versioning System

### Semantic Versioning (SemVer)

Format: `MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]`

### MinVer: Automatic Version Calculation

MinVer derives versions from git tags automatically; it finds the most recent tag, counts commits since that tag, and generates an appropriate version suffix.

## Release Notes

Two sources of release notes are used:

1. Automatic release notes (from commits)
2. `docs/CHANGELOG.md` (manual, curated)

Best practices: Use Conventional Commits; write clear, descriptive messages.

## Pre-releases

Mark pre-releases with suffixes like `-alpha`, `-beta`, `-rc` and create annotated tags accordingly.

## CI Workflow Details

The release job triggers on `refs/tags/v*` and bundles build artifacts into the GitHub Release.

## Monitoring Releases & Troubleshooting

Use `gh run list`, `gh release list`, and related `gh` commands to inspect runs and releases. If a release needs to be recreated, delete the release and the tag, then recreate it.

## Best Practices (summary)

1. Always update `docs/CHANGELOG.md` before tagging
2. Use annotated tags (`git tag -a`)
3. Follow semantic versioning
4. Write clear commit messages
5. Test pre-releases before stable
6. Avoid force-pushing tags
7. Keep `docs/CHANGELOG.md` format consistent

---

## Included: Commit All Changes Instruction (merged from `.github/instructions/commit_all.instructions.md`)

The following policy text was merged from `.github/instructions/commit_all.instructions.md` to keep repository workflow guidance centralized. It describes the recommended sequence when the user asks to "commit all" changes.

When the user requests to commit changes (or says "commit all", "check in", "checkin", etc.):

1. **Check git status** to identify all modified, added, or deleted files
2. **Review the diff** for all changed files to understand what was modified
3. **Verify the build** (if applicable) to ensure changes don't break compilation
4. **Stage all changes** using `git add`
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

---

End of merged content.
