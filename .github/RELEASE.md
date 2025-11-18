# Release Process

This document explains how GitHub releases work for DeDuBa, including automatic version numbering, release notes generation, and the relationship with CHANGELOG.md.

## Overview

DeDuBa uses **automated GitHub releases** triggered by git version tags. The process combines:

- **MinVer** for automatic semantic versioning from git tags
- **GitHub Actions** for building, testing, and packaging
- **Automatic release notes** generated from commit messages
- **Manual CHANGELOG.md** (in `docs/`) for curated release summaries

## How to Create a Release

### 1. Prepare the Release

Update `docs/CHANGELOG.md` with the new version section:

```markdown
## [0.2.0] - 2025-11-17

### Added

- New ACL preservation during restore
- Support for ARM64 Linux

### Fixed

- Memory leak in chunk processing
```

Commit the changes:

```bash
git add docs/CHANGELOG.md
git commit -m "chore: prepare release v0.2.0"
git push origin master
```

### 2. Create and Push a Version Tag

Create an annotated tag following semantic versioning:

```bash
# For a stable release
git tag -a v0.2.0 -m "Release version 0.2.0"

# For a pre-release (alpha, beta, rc)
git tag -a v0.2.0-beta.1 -m "Release version 0.2.0 beta 1"
```

Push the tag to GitHub:

```bash
git push origin v0.2.0
```

### 3. Automatic Build and Release

Once the tag is pushed, GitHub Actions automatically:

1. **Builds** all platforms (Linux x64, Windows x64)
2. **Runs** all unit tests
3. **Packages** binaries:
   - `DeDuBa-0.2.0-linux-x64.tar.gz`
   - `DeDuBa-0.2.0-win-x64.zip`
4. **Creates** a GitHub Release with:
   - All packaged binaries attached
   - Custom description (download instructions)
   - Auto-generated release notes from commits
   - Link to docs/CHANGELOG.md

## Versioning System

### Semantic Versioning (SemVer)

Format: `MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]`

- **MAJOR**: Incompatible API/archive format changes
- **MINOR**: New functionality (backward compatible)
- **PATCH**: Bug fixes (backward compatible)
- **PRERELEASE**: Optional suffix like `-alpha.1`, `-beta.2`, `-rc.1`
- **BUILD**: Git metadata (e.g., `+sha.abc1234`)

Examples:

- `v0.1.0` → stable release 0.1.0
- `v1.2.3-beta.1` → pre-release (marked as "Pre-release" on GitHub)
- `v2.0.0` → major version (breaking changes)

### MinVer: Automatic Version Calculation

**MinVer** derives versions from git tags automatically:

```text
git tag v0.1.0
  ↓
MinVer calculates: 0.1.0

3 commits later (no new tag):
  ↓
MinVer calculates: 0.1.1-alpha.0.3+sha.abc1234
```

**How it works:**

1. Finds the most recent version tag (e.g., `v0.1.0`)
2. Counts commits since that tag (height)
3. If height > 0: auto-increments PATCH and adds `-alpha.0.{height}`
4. Adds build metadata with current commit SHA

**Configuration** (in `Directory.Build.props`):

```xml
<MinVerTagPrefix>v</MinVerTagPrefix>        <!-- Tags start with 'v' -->
<MinVerVerbosity>normal</MinVerVerbosity>   <!-- Show calculation during build -->
```

## Release Notes

The system provides **two sources** of release notes:

### 1. Automatic Release Notes (generate_release_notes: true)

GitHub automatically generates notes from **commit messages** since the last release:

**Example output:**

```markdown
## What's Changed

- feat: add ACL restoration support by @username in #42
- fix: memory leak in chunk processor by @username in #43
- refactor: simplify native interop by @username in #44

**Full Changelog**: https://github.com/user/repo/compare/v0.1.0...v0.2.0
```

**Advantages:**

- Zero effort
- Complete commit history
- Automatic contributor attribution
- Links to PRs

**Best practices for commit messages:**

- Use conventional commits: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`
- Write clear, descriptive messages
- Reference issues/PRs: `fix: resolve crash (#42)`

### 2. docs/CHANGELOG.md (Manual Curation)

A curated, human-friendly summary of **significant changes**:

**Purpose:**

- High-level overview for users
- Grouped by change type (Added, Changed, Fixed, etc.)
- More context than raw commits
- Stable reference for version history

**When to update:**

- Before creating each release tag
- Group related commits into logical features
- Focus on user-facing changes
- Omit internal refactoring unless significant

**Format:**

```markdown
## [0.2.0] - 2025-11-17

### Added

- ACL preservation during restore operations
- Support for ARM64 Linux architecture

### Changed

- Improved chunk processing performance by 30%

### Fixed

- Memory leak in long-running backup sessions
- Race condition in parallel directory scanning

### Security

- Updated dependencies to address CVE-2025-1234
```

### How They Work Together

**In the GitHub Release:**

```text
┌─────────────────────────────────────┐
│ DeDuBa v0.2.0                       │
├─────────────────────────────────────┤
│ ## Downloads                        │ ← Custom body from workflow
│ - Linux: DeDuBa-*-linux-x64.tar.gz  │
│ - Windows: DeDuBa-*-win-x64.zip     │
│                                     │
│ ## What's Changed                   │
│ See docs/CHANGELOG.md for details.  │ ← Link to curated changelog
│                                     │
│ ---                                 │
│                                     │
│ ## What's Changed                   │ ← Auto-generated from commits
│ * feat: add ACL restoration by...   │
│ * fix: memory leak by...            │
│ * refactor: simplify interop by...  │
│                                     │
│ **Full Changelog**: v0.1.0...v0.2.0 │
└─────────────────────────────────────┘
```

**Result:** Users get:

1. Quick download links
2. High-level overview (docs/CHANGELOG.md link)
3. Complete commit history (auto-generated)

## Pre-releases

Mark releases as "Pre-release" using version suffixes:

```bash
git tag -a v1.0.0-alpha.1 -m "First alpha for v1.0.0"
git tag -a v1.0.0-beta.1 -m "First beta for v1.0.0"
git tag -a v1.0.0-rc.1 -m "Release candidate 1"
```

The workflow automatically detects these patterns and marks the GitHub release as "Pre-release":

```yaml
prerelease: ${{ contains(github.ref, '-rc') || contains(github.ref, '-beta') || contains(github.ref, '-alpha') }}
```

**Benefits:**

- Clearly marked on GitHub Releases page
- Not shown as "Latest Release"
- Allows testing before stable release
- MinVer handles version calculations correctly

## CI Workflow Details

The release job in `.github/workflows/ci.yml`:

```yaml
release:
  if: startsWith(github.ref, 'refs/tags/v')  # Only on version tags
  needs: [build-linux, build-windows-cross, build-windows-native]  # Wait for builds
  runs-on: ubuntu-latest
  permissions:
    contents: write  # Required to create releases
  steps:
    - Download all build artifacts
    - Prepare release files (tar.gz, zip)
    - Create GitHub Release with:
      * files: all binaries
      * draft: false (publish immediately)
      * prerelease: auto-detect from tag
      * generate_release_notes: true
      * body: custom download instructions + changelog link
```

## Monitoring Releases

### Check CI Status

```bash
# List recent CI runs
gh run list --limit 10

# View specific run
gh run view 19418218555

# View release job details
gh run view --job=55550720252
```

### List Releases

```bash
# List all releases
gh release list

# View specific release
gh release view v0.2.0

# Download release artifacts
gh release download v0.2.0
```

## Troubleshooting

### "Release already exists" Error

If you need to recreate a release:

```bash
# Delete the release
gh release delete v0.2.0 --yes

# Delete the tag locally and remotely
git tag -d v0.2.0
git push origin :refs/tags/v0.2.0

# Recreate and push
git tag -a v0.2.0 -m "Release version 0.2.0"
git push origin v0.2.0
```

### MinVer Shows Unexpected Version

```bash
# Check current calculated version
dotnet build DeDuBa/DeDuBa.csproj --verbosity minimal | grep MinVer

# List all tags
git tag --list --sort=-v:refname

# Show tag details
git show v0.2.0

# Verify tag is annotated (required by MinVer)
git tag -v v0.2.0  # Shows tag signature/annotation
```

MinVer requires **annotated tags** (created with `-a`). Lightweight tags are ignored.

### Build Fails on Tag Push

Check the CI logs:

```bash
gh run list --limit 5
gh run view <run-id> --log
```

Common issues:

- Test failures
- Native library build errors
- Missing dependencies

Fix the issue, then recreate the tag.

## Best Practices

1. **Always update docs/CHANGELOG.md** before tagging
2. **Use annotated tags** (`git tag -a`) not lightweight tags
3. **Follow semantic versioning** strictly
4. **Write clear commit messages** (they become release notes)
5. **Test pre-releases** (`-alpha`, `-beta`, `-rc`) before stable
6. **Never force-push tags** (creates confusion)
7. **Keep docs/CHANGELOG.md format** consistent

## References

- [Semantic Versioning](https://semver.org/)
- [Keep a Changelog](https://keepachangelog.com/)
- [Conventional Commits](https://www.conventionalcommits.org/)
- [MinVer Documentation](https://github.com/adamralph/minver)
- [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github)
