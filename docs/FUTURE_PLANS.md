# Future Plans & Architecture Roadmap

This document captures completed work, near-term, and mid-term enhancements for DeDuBa covering distribution, architecture refactoring, and high-level API design.

---

## Part 1: Distribution & Release Automation

### 1. CI Integration ✅ COMPLETED

**Status:** Implemented in `.github/workflows/ci.yml`

- ✅ CI workflow builds and tests on Linux (native) and Windows (cross-build + native)
- ✅ Matrix for `Debug` (smoke) and `Release` (publish) builds
- ✅ Native shim build integrated (CMake for both Linux and Windows)
- ✅ Tests run on both platforms with proper `LD_LIBRARY_PATH` configuration
- ✅ Native Windows runner validates on actual Windows environment
- ⚠️ TODO: Cache `~/.nuget/packages` and native build intermediates to speed up runs

### 2. Artifact Upload ✅ COMPLETED

**Status:** Implemented in CI workflow

- ✅ Versioned archives uploaded: `DeDuBa-<version>-<rid>.tar.gz` and `.zip`
- ✅ MinVer used for automatic versioning from git tags
- ✅ Artifacts retained for 30 days (Release builds) / 90 days (consolidated)
- ✅ Release job triggers on `v*` tags; promotes artifacts to GitHub Release with auto-generated changelog
- ⚠️ TODO: Optionally include symbol files (`*.pdb`) and XML docs for developer builds

### 3. Checksums & Integrity ❌ PENDING

**Status:** Not yet implemented

- ❌ Generate SHA-512 checksum files alongside archives: `DeDuBa-<version>-<rid>.tar.gz.sha512`
- ❌ Provide verification snippet in README (`shasum -a 512 -c`)
- 💡 Future: Signed checksums (GPG) if distribution extends beyond internal use

### 4. Packaging Scripts ✅ COMPLETED

**Status:** Implemented in `scripts/package.sh`

- ✅ Automated packaging for `linux-x64` and `win-x64` with MinVer versioning
- ✅ Produces self-contained archives with native shims
- ✅ Includes `run.sh` wrapper for Linux (sets `LD_LIBRARY_PATH`)
- ⚠️ TODO: `clean` mode to prune outdated versioned artifacts (retain last N)
- ⚠️ TODO: Add CI job to auto-delete stale artifacts older than X days on non-release branches

### 5. Windows Validation ✅ COMPLETED

**Status:** Implemented in CI workflow

- ✅ Native Windows runner validates build on actual Windows environment
- ✅ Smoke test ensures binary loads runtime correctly
- ⚠️ TODO: Add automated Wine-based smoke test on Linux for cross-compiled Windows binary

### 6. Release Automation ✅ COMPLETED

**Status:** Implemented in CI workflow

- ✅ MinVer used for versioning; tags follow `v<semver>` format
- ✅ Auto-generated changelog from commit messages (via `generate_release_notes: true`)
- ✅ Release created automatically on version tags with artifacts attached
- ✅ Prerelease detection for `-rc`, `-beta`, `-alpha` tags

### 7. Extended Packaging ❌ PENDING

**Status:** Not yet implemented

- ❌ Container image (`ghcr.io/<org>/deduba:<version>`) with runtime dependencies
- ❌ Homebrew tap formula / winget manifest as optional distribution channels

### 8. Observability Hooks ❌ PENDING

**Status:** Not yet implemented

- ❌ Runtime metrics export (processed bytes, dedupe ratio) via stdout JSON lines or Prometheus endpoint
- ❌ Structured log ingestion pipeline for long-running archival tasks
- 💡 Consider integration with OpenTelemetry for distributed tracing

### 9. Security & Hardening ❌ PENDING

**Status:** Not yet implemented

- ❌ SBOM generation (CycloneDX) during publish
- ❌ Binary signing (Linux: minisign; Windows: signtool) for official releases
- 💡 Consider reproducible builds for enhanced security

---

## Part 2: ArchiveStore Architecture

### 2.1 What Was Refactored ✅ COMPLETED

**Status:** Implemented in `DeDuBa/ArchiveStore.cs` and `DeDuBa/IArchiveStore.cs`

- ✅ Extracted `Arlist`, `Preflist` state and management from `DedubaClass`
- ✅ `Mkarlist` → `ArchiveStore.BuildIndex`
- ✅ `Hash2Fn` → `ArchiveStore.GetTargetPathForHash`
- ✅ `Save_data` → `ArchiveStore.SaveData`
- ✅ `Save_file` → `ArchiveStore.SaveStream`
- ✅ Helper functions moved internal to `ArchiveStore`

### 2.2 Design Choices ✅ COMPLETED

**Status:** Implemented with best practices

- ✅ `BackupConfig` centralizes configuration (`ArchiveRoot`, `DataPath`, `ChunkSize`, `PrefixSplitThreshold`, `Testing`, `Verbose`)
- ✅ `IArchiveStore` interface for testability and DI
- ✅ `ArchiveStore` uses `ConcurrentDictionary` and careful locking for reorganization
- ✅ `Preflist` stores `HashSet<string>` per prefix rather than `\0`-delimited strings
- ✅ Tests included for `BuildIndex`, `SaveData`, `SaveStream`, and `Reorg` in `DeDuBa.Test/ArchiveStoreTests.cs`

### 2.3 Future Enhancements for ArchiveStore ❌ PENDING

**Performance & Scalability:**

- ❌ Add async variants: `SaveStreamAsync`, `BuildIndexAsync`
- ❌ Replace `HashSet` in `Preflist` with `ConcurrentHashSet` for highly concurrent code
- ❌ Persist `Arlist`/`Preflist` to on-disk index (JSON or embedded DB) to avoid `BuildIndex()` re-indexing
- ❌ Improve BuildIndex performance using change journals or file monitoring for incremental updates

**Reliability & Data Integrity:**

- ❌ Consider writing compressed files to temporary path and `File.Move` for atomic writes
- ❌ Add transactional and inter-process safety (file locks or leader election) for multi-process writes
- ❌ Add end-to-end restore/integrity verification tests (read-back chunks, verify checksums and metadata)
- ❌ Add support for verification/digest-only mode to scan archive for missing or corrupted files

**Operations & Management:**

- ❌ Add GC for unused chunks (safe compaction algorithm)
- ❌ Implement snapshot exports and compaction (dedup/garbage-collect) to reduce archive size
- ❌ Add retention and pruning policy: remove chunks no longer referenced by any inode
- ❌ Add safe migration path for existing archives: utilities to import/export index formats
- ❌ Add CLI tooling for inspecting archive, listing prefixes, and restore operations

**Observability:**

- ❌ Add `IArchiveLogger` or `ILogger<T>` for better logging and tracing
- ❌ Integrate Prometheus-style metrics for saved/duplicate blocks
- ❌ Add metadata snapshots for quick restore of directory structures

**Advanced Features:**

- ❌ Add support for partial uploads and resumable chunking
- ❌ Consider encryption-at-rest options (AES-GCM) and key rotation features
- ❌ Add more comprehensive concurrency tests: simultaneous saves, reorg under load

---

## Part 3: High-Level OS API Architecture

### 3.1 Design Goals ❌ NOT STARTED

**Motivation:** Create a single high-level OS API for all OS interactions while reading source data. Centralize platform-specific behavior into well-defined interfaces, simplify testing with mockable interfaces, and create a versioned boundary between backup logic and platform nuances.

**Target Interface:** `IHighLevelOsApi` in `OsCallsCommon` with implementations:

- `OsCallsLinux.HighLevelOsApi`
- `OsCallsWindows.HighLevelOsApi`

### 3.2 Proposed API Surface ❌ NOT STARTED

**File Operations & Metadata:**

- `FileInfoResult GetFileInfo(string path, bool followSymlink = true)`
- `Stream OpenFileRead(string path)`
- `byte[] ReadFileSegment(string path, long offset, int length)`
- `string ReadLink(string path)`
- `bool FileExists(string path)`
- `bool IsDirectory(string path)`

**Directory Traversal:**

- `IAsyncEnumerable<DirectoryEntry> ListDirectory(string path)`
- `DirectoryEntry[] ListDirectorySync(string path)`

**ACL / Extended Attributes / ADS:**

- `XAttrs GetXattrs(string path)` - returns `Dictionary<string, byte[]>`
- `Acls GetAcls(string path)` - platform-specific, normalized
- `AlternateDataStreams GetADS(string path)` - Windows-only, empty on Linux

**User/Group Lookups:**

- `string GetUserName(ulong uid)`
- `string GetGroupName(ulong gid)`
- `ulong LookupUser(string username)`
- `ulong LookupGroup(string groupname)`

**Path Helpers:**

- `string CanonicalizePath(string path)`
- `string NormalizePathCase(string path)`

**File Type & Inode Info:**

- `FileType GetFileType(string path)` - enum: File, Directory, Symlink, BlockDevice, CharDevice, FIFO, Socket, Unknown
- `long GetFilesystemId(string path)`
- `InodeInfo GetInodeInfo(string path)` - for hardlink recognition (dev+ino)

### 3.3 POCO Types ❌ NOT STARTED

**Proposed Records:**

- `FileInfoResult`: Path, FileType, Mode, Uid, Gid, Size, Mtime, Atime, Ctime, LinkTarget, Device, Inode, LinkCount
- `DirectoryEntry`: Name, FullPath, FileType, Size, Mode, Uid, Gid
- `XAttrs`: `Dictionary<string, byte[]>`
- `Acls`: OwnerAcl, GroupAcl, OtherAcl, AdditionalAclEntries
- `AlternateDataStreams`: `Dictionary<string, byte[]>`
- `InodeInfo`: Device, Inode

### 3.4 Platform Differences to Handle ❌ NOT STARTED

**User/Group vs SID:**

- Linux: uid/gid mapped to names via `getpwuid`/`getgrgid`
- Windows: SIDs converted to `"DOMAIN\\Account"` or SDDL

**Inodes:**

- Linux: native POSIX inodes
- Windows: emulate via `GetFileInformationByHandle` (file ID)

**Extended Attributes:**

- Linux: `getxattr`/`listxattr`
- Windows: NTFS Alternate Data Streams (ADS)

**ACLs:**

- Linux: POSIX ACLs via `acl_get_file`
- Windows: Security descriptors (DACL entries)

**Symlinks:**

- Linux: ubiquitous, preserved as-is
- Windows: reparse points, detect and translate to `FileType.Symlink`

**Error Handling:**

- Define `HighLevelOsApiException` with `ErrorKind` enum (NotFound, PermissionDenied, IOError, NotSupported, InvalidArgument, Unknown)
- Wrap native errors consistently across platforms

### 3.5 Migration Strategy ❌ NOT STARTED

**Phased Approach:**

1. ❌ Add `OsCallsCommon/IHighLevelOsApi.cs` with minimal method set
2. ❌ Implement in `OsCallsLinux/HighLevelOsApi.cs` using existing wrappers (`FileSystem.cs`, `Xattr.cs`, `UserGroupDatabase.cs`)
3. ❌ Implement in `OsCallsWindows/HighLevelOsApi.cs` using `FileSystem.cs`, `Streams.cs`, `Security.cs`
4. ❌ Add unit tests mocking the interface in `DeDuBa.Test`
5. ❌ Gradually replace direct P/Invoke calls in `DeDuBa/Deduba.cs` with `IHighLevelOsApi` calls
6. ❌ Provide temporary compatibility adapter for short-term coexistence

**Migration Order:**

1. Read-only metadata retrieval for directory entries
2. File content reads (OpenFileRead/Stream)
3. UID/GID lookups and name mapping
4. ACL/xattr/ADS retrieval

### 3.6 Testing Strategy ❌ NOT STARTED

**Unit Tests:**

- Mock `IHighLevelOsApi` to exercise `DeDuBa` logic without native OS
- Test backup logic independently of platform

**Integration Tests:**

- Linux: controlled directory fixtures with various file types, symlinks, ACLs, xattrs
- Windows: similar tests on Windows VM/host or CI Windows runner
- Cross-platform: verify normalized output (`FileType` enum, `InodeInfo`) behaves consistently

### 3.7 Developer Experience ❌ NOT STARTED

**Tooling:**

- ❌ `HighLevelOsApiFactory` with `GetForLocalPlatform()` to return correct implementation
- ❌ DI-friendly: `DedubaClass` receives `IHighLevelOsApi` via constructor injection
- ❌ Streaming API for directories: prefer `IAsyncEnumerable<DirectoryEntry>` over large arrays

**Documentation:**

- ❌ Document platform normalization decisions (ACL/xattr serialization format, preserved fields)
- ❌ Add examples showing usage patterns

### 3.8 Open Questions ❌ NOT STARTED

- How much normalization between platforms? (e.g., Windows SDDL vs Linux ACLs)
- Which fields must be preserved exactly for faithful backup?
- Single serialization format for ACL/xattrs across OSs (JSON) or raw bytes with metadata?

### 3.9 Timeline Estimate (When Prioritized) ❌ NOT STARTED

- **Week 1:** Draft interface, prototypes for Linux + Windows, prototype unit tests
- **Week 2:** Implement Linux, replace core metadata reads in `DeDuBa`
- **Week 3:** Implement Windows, replicate for Windows-sensitive code paths
- **Week 4:** Finish migration and tests, add CI cross-platform coverage

---

## Backlog Summary Table

| Area                        | Status     | Priority | Notes                                                |
|-----------------------------|------------|----------|------------------------------------------------------|
| **Distribution & CI**       |            |          |                                                      |
| CI build matrix             | ✅ Done     | High     | Implemented in `.github/workflows/ci.yml`            |
| Artifact upload             | ✅ Done     | High     | Versioned archives uploaded on Release builds        |
| Release automation          | ✅ Done     | High     | Auto-release on `v*` tags with changelog             |
| Packaging scripts           | ✅ Done     | High     | `scripts/package.sh` with MinVer versioning          |
| Windows validation          | ✅ Done     | High     | Native Windows runner + smoke test                   |
| Checksums                   | ❌ Pending  | High     | SHA-512 checksums not yet generated                  |
| CI caching                  | ⚠️ Partial | Medium   | TODO: Cache NuGet packages and native intermediates  |
| Wine smoke test             | ❌ Pending  | Medium   | Optional: test cross-compiled Windows binary on Wine |
| Cleanup script              | ❌ Pending  | Low      | `package.sh clean` mode to prune old artifacts       |
| Container image             | 💡 Idea    | Medium   | Future: Docker/OCI image for deployment              |
| Brew/winget                 | 💡 Idea    | Low      | Distribution via package managers                    |
| SBOM & signing              | 💡 Idea    | Medium   | Security: generate SBOM, sign binaries               |
| **ArchiveStore**            |            |          |                                                      |
| Core refactor               | ✅ Done     | High     | `IArchiveStore` interface implemented with tests     |
| Async variants              | ❌ Pending  | Medium   | `SaveStreamAsync`, `BuildIndexAsync`                 |
| Index persistence           | ❌ Pending  | High     | Avoid full re-index on startup                       |
| Atomic writes               | ❌ Pending  | Medium   | Temp file + move for reliability                     |
| Compaction/GC               | ❌ Pending  | Medium   | Remove unused chunks, retention policies             |
| Restore tooling             | ❌ Pending  | High     | CLI for restore, integrity verification              |
| Encryption at rest          | 💡 Idea    | Low      | AES-GCM with key rotation                            |
| Observability               | ❌ Pending  | Medium   | ILogger integration, Prometheus metrics              |
| Concurrency tests           | ❌ Pending  | Medium   | Simultaneous saves, reorg under load                 |
| **High-Level OS API**       |            |          |                                                      |
| Interface design            | 💡 Idea    | High     | `IHighLevelOsApi` in `OsCallsCommon`                 |
| Linux implementation        | 💡 Idea    | High     | Wrap existing `FileSystem`/`Xattr`/`Acl`             |
| Windows implementation      | 💡 Idea    | High     | Wrap `FileSystem`/`Streams`/`Security`               |
| Unit tests with mocks       | 💡 Idea    | High     | Mock interface for backup logic tests                |
| Migration to new API        | 💡 Idea    | High     | Replace direct P/Invoke in `Deduba.cs`               |
| Cross-platform integration  | 💡 Idea    | Medium   | Validate normalized output across platforms          |
| **Observability (General)** |            |          |                                                      |
| Metrics export              | 💡 Idea    | Low      | Stdout JSON lines or Prometheus endpoint             |
| Structured logging          | ❌ Pending  | Medium   | Pipeline for long-running tasks                      |
| OpenTelemetry integration   | 💡 Idea    | Low      | Distributed tracing for complex workflows            |

---

## Immediate Next Steps (Priority Order)

1. **High Priority:**
   - ❌ Add SHA-512 checksum generation to `scripts/package.sh`
   - ❌ Implement index persistence for `ArchiveStore` (avoid full re-index)
   - ❌ Design and prototype `IHighLevelOsApi` interface

2. **Medium Priority:**
   - ❌ Add CI caching for NuGet packages and native build intermediates
   - ❌ Implement `ArchiveStore` async variants (`SaveStreamAsync`, `BuildIndexAsync`)
   - ❌ Add restore CLI tooling and integrity verification

3. **Low Priority:**
   - ❌ Implement `package.sh clean` mode for artifact pruning
   - ❌ Add Wine-based smoke test for cross-compiled Windows binary
   - ❌ Design container image for deployment

---

**Document History:**

- 2025-11-16: Initial creation (distribution roadmap)
- 2025-11-23: Merged ArchiveStore refactor notes and High-Level OS API plan; added completion status for all items
