# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.3-alpha.1] - 2025-11-17

### Fixed

- Native library loading in test environment via MSBuild copy targets

## [0.1.3-alpha] - 2025-11-17 [YANKED]

### Added

- Cross-platform native interop architecture with C++/C# bridge (ValXfer)
- POSIX ACL support via `OsCallsLinux.Acl` (access and default ACLs)
- Extended attributes (xattr) support via `OsCallsLinux.Xattr`
- Content-addressable storage with SHA-512 hashing and BZip2 compression
- Automatic directory reorganization when prefix directories exceed 255 entries
- Hardlink detection to avoid reprocessing identical inodes
- Comprehensive xUnit test suite for ACL and xattr functionality
- Cross-platform build support (Linux native, Windows cross-compile, Windows native)
- GitHub Actions CI/CD with artifact consolidation
- Automated packaging scripts for Linux (tar.gz) and Windows (zip)
- Automated GitHub release workflow triggered by version tags
- MinVer integration for semantic versioning from git tags
- Comprehensive release documentation (RELEASE.md)

### Changed

- Ported from Perl to C# while maintaining compatibility with original archive format
- Replaced custom binary encoding with JSON serialization for metadata
- Simplified interop: moved `GetNextValue` to common shim eliminating delegate pattern

### Fixed (0.1.3-alpha)

- CI workflow now triggers on version tag pushes (enables automatic releases)
- Native library loading in test environment via MSBuild copy targets
- Platform-conditional test compilation (Linux-only ACL/xattr tests)
- Native shim build output paths and RPATH configuration

## Release Process

To create a new release:

1. **Update docs/CHANGELOG.md** with the new version section
2. **Commit the changes**: `git commit -am "chore: prepare release vX.Y.Z"`
3. **Create and push a version tag**:

   ```bash
   git tag -a vX.Y.Z -m "Release version X.Y.Z"
   git push origin vX.Y.Z
   ```

4. **GitHub Actions automatically**:
   - Builds all platforms (Linux, Windows)
   - Runs all tests
   - Packages binaries
   - Creates a GitHub Release with artifacts attached
   - Generates release notes from commits since last tag

## Version Numbering

This project uses [Semantic Versioning](https://semver.org/):

- **MAJOR** version for incompatible API/archive format changes
- **MINOR** version for new functionality in a backward compatible manner
- **PATCH** version for backward compatible bug fixes
- Pre-release suffixes: `-alpha.N`, `-beta.N`, `-rc.N`

MinVer automatically derives versions from git tags.

---

## Template for New Releases

```markdown
## [X.Y.Z] - YYYY-MM-DD

### Added

- New features

### Changed

- Changes in existing functionality

### Deprecated

- Soon-to-be removed features

### Removed

- Removed features

### Fixed

- Bug fixes

### Security

- Security improvements
```
