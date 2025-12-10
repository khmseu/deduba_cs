# Changelog

<!-- markdownlint-configure-file { "MD024": false } -->

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Instance-based logging abstraction `ILogging` and adapter `UtilitiesLogger`.
  - Enables injecting a logger implementation for tests and modular components while
    preserving `CallerArgumentExpression` semantics used by `D(...)` helpers.

### Changed

- Converted several OS-call modules to use an injectable module-level logger
  (defaulting to `UtilitiesLogger`): `OsCallsCommon/ValXfer.cs`,
  `OsCallsLinux/FileSystem.cs`, `OsCallsWindows/FileSystem.cs`.
  - This reduces static coupling to `Utilities` and improves testability of native
    resolver and P/Invoke initialization code.

### Improved

- All unit tests pass after the refactor (local run: 39 passed, 0 failed).

## [0.1.9-alpha] - 2025-12-08

### Fixed

- Fixed critical Windows timestamp bug where negative MTime values were reported due to struct layout mismatch in P/Invoke marshaling
- Created platform-independent `TimeSpec64` struct with explicit `int64_t` fields to ensure consistent layout across Windows x64 (32-bit `long`) and Linux x64 (64-bit `long`)
- Updated both Windows and Linux filesystem shims to use `TimeSpec64` for all timestamp operations
- Added `timespec_to_timespec64` conversion helper for Linux standard `timespec` values

### Changed

- Expanded `.clang-format` configuration with comprehensive explicit formatting rules for consistency across clang-format versions (16.x and 18.x)
- Added detailed formatting specifications for indentation, alignment, spacing, line breaking penalties, and C++ standards
- Removed debug fprintf statements after timestamp fix verification

### Improved

- Enhanced CI formatting compatibility by resolving discrepancies between local (clang-format 16) and CI (clang-format 18) environments
- All CI checks now pass: format-check, build-linux, build-windows-native, build-windows-cross

## [0.1.8-alpha] - 2025-12-08

### Changed

- Simplified `IHighLevelOsApi` interface by removing `LStat` and `CreateInodeDataFromPath(path, statBuf, archiveStore)` methods
- Enforced two-step metadata collection pattern: `CreateMinimalInodeDataFromPath` for fast stat-only operations, then `CompleteInodeDataFromPath` for expensive ACLs/xattrs/hashes

### Added

- Added comprehensive `HighLevelOsApiTests` test suite demonstrating proper usage of minimal and complete inode data APIs

### Fixed

- Applied consistent code formatting (trailing commas, newlines at EOF) across platform implementations

## [0.1.7-alpha] - 2025-12-08

### Added

- Added minimal inode metadata APIs (`CreateMinimalInodeDataFromPath`, `CompleteInodeDataFromPath`) and implemented the Windows path to align platform behavior.

### Changed

- Replaced the JsonElement-based `FileId` with typed `Int128` `Device`/`FileIndex` fields (and `RDev` to `Int128`) across OS APIs and backup logic to improve type safety and future-proof large device identifiers.
- Updated backup traversal to use minimal inode data for device tracking and reuse across completion flows.

## [0.1.6-alpha] - 2025-12-06

### Added

- Centralized platform-specific build constants in Directory.Build.targets; removed duplication from all .csproj files
- Improved platform symbol validation and error reporting in build targets
- Extracted ArchiveStore to a dedicated project and implemented IArchiveStore

### Changed

- Refactored and improved native/managed project structure, scripts, and platform detection logic
- Reverted multi-line JSON formatting in .vscode/tasks.json to single-line style; added missing presentation config for tasks
- Various build, CI, and housekeeping improvements

### Fixed

- Fixed WindowsHighLevelOsApi: corrected Security.GetSecurityDescriptor usage

## [0.1.5-alpha] - 2025-12-05

### Added

- Consolidated all managed and native projects under `src/` to declutter the workspace root.

### Changed

- Updated solution, CMake, CI workflows, packaging, and test/demo scripts to use the new `src/` paths.

### Fixed

- Corrected Doxygen/DocFX input paths and LD_LIBRARY_PATH references so docs-and-includes CI now passes.

## [0.1.4-alpha] - 2025-12-05

### Added

- Extracted the archive store and configuration into `IArchiveStore`/`ArchiveStore` and added `BackupConfig` to centralize archive settings and behavior.
- Introduced the `IHighLevelOsApi` abstraction and moved `InodeData`/`OsException` into `OsCallsCommon` to cleanly separate OS metadata collection from backup logic.
- Initial Windows metadata implementation: minimal `CreateInodeDataFromPath` to enable Windows-targeted integration tests and several Windows shim helpers.

### Changed

- Refactored `Deduba` to use `IHighLevelOsApi.CreateInodeDataFromPath()` and `ListDirectory()` for OS-specific metadata collection, reducing inline platform-specific code in `Backup_worker`.
- ArchiveStore integrated as the single source of truth for content-addressable storage; removed legacy compatibility wrappers and fields from `DedubaClass`.
- CI and build system improvements: migrated shims to CMake, added multi-config handling, improved native output layout, and made tag triggers and caching more robust.

### Fixed

- Multiple Windows shim and native export fixes (entry-point exports, struct packing, .def/generate_export_header fixes) to resolve P/Invoke and test-host crashes on Windows.
- Stabilized Windows native iterator/error handling to avoid AccessViolationException and marshaling bugs (ValXfer/ValueT lifetime and error returns).
- Test reliability fixes: reset per-run static state (Dirtmp, Fs2Ino, Devices, Bstats, counters), add clearer per-entry logging, and canonicalize paths to avoid test-order leakage.
- ArchiveStore: improved stream/hash handling, more robust logging, and minor reorganization logic fixes.

### Docs

- Added and expanded XML documentation for public APIs (`InodeData`, `OsException`, `IHighLevelOsApi`) and improved Doxygen/C++ shim docs.
- Added changelog links in README and updated `FUTURE_PLANS.md` with progress snapshots.

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
