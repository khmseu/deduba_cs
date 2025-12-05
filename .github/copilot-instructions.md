# DeDuBa - Deduplicating Backup System

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
   ```markdown
   # DeDuBa — Copilot instructions for AI coding agents

   Goal: help an AI agent quickly understand, build, test and modify DeDuBa
   with minimal back-and-forth. Keep edits small, focused, and well-tested.

   - Architecture (one-paragraph): DeDuBa is a C#/.NET (net8.0) backup
      application using content-addressable storage (SHA-512 chunks + BZip2)
      and a three-layer native interop: C# platform wrappers → OsCallsCommon
      bridge (ValXfer) → native shims (OsCalls*Shim). Key projects: `DeDuBa`,
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

   - Files to read first when changing behavior:
      - `DeDuBa/Deduba.cs` — main backup logic and `Backup_worker`.
      - `DeDuBa/ArchiveStore.cs` and `IArchiveStore` — storage semantics and APIs.
      - `OsCallsCommon/ValXfer.cs` and `OsCallsCommonShim/include/ValXfer.h` — native iterator protocol.
      - `OsCallsLinux/FileSystem.cs`, `OsCallsWindows/FileSystem.cs` and shim C++ sources under `OsCalls*Shim/src`.

   - CI and release notes:
      - Repo uses GitHub Actions; tags `v*` trigger the release workflow.
      - When creating a tag, update `docs/CHANGELOG.md` and push tag; CI will build packages and create a release.

   - Troubleshooting quick tips:
      - If you see `DllNotFoundException` or missing entry points: ensure native shim builds and that native DLL/.so are copied to managed output (check CMake outputs and csproj copy tasks).
      - Windows-specific issues often relate to exports/packing (`ValXfer.h` packing, `.def` or CMake export header), and native -> managed struct alignment.
      - For test flakiness: reset per-run static state (look at `DedubaClass` reset logic) and prefer deterministic logging (canonical paths).

   - When editing code:
      1. Run `dotnet build` and fix compile issues.
      2. Run affected unit/integration tests locally (`dotnet test --filter "FullyQualifiedName~YourTest"`).
      3. If native changes were made, rebuild shims via CMake and ensure the resulting native library is present in test output.

   If anything here is unclear or you want more detail (examples of implementing a new OS capability, script templates for running a Windows-native test locally, or exact CMake commands), tell me which area to expand.
   ```
./demo-xattr.sh        &num; Demo xattr functionality
