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
   - `ToNode(ValueT* value)` iterates native cursor → JsonNode
   - Single source of truth for ValueT protocol

2. **Platform wrappers**:
   - `OsCallsLinux/FileSystem.cs`: `[LibraryImport("libOsCallsLinuxShim.so")]`
   - `OsCallsWindows/FileSystem.cs`: `[LibraryImport("OsCallsWindowsShim.dll")]`
   - Static constructor sets `ValXfer.PlatformGetNextValue = GetNextValue`

3. **Native implementations**:
   - Linux: POSIX (lstat, readlink, acl_get_file, llistxattr, getpwuid)
   - Windows: Win32 stubs (TODO: CreateFileW, GetFinalPathNameByHandleW)
   - Both call `CreateHandle()` from `OsCallsCommonShim/src/ValXfer.cpp`

**Critical files:**

- `OsCallsCommon/ValXfer.cs` - Unsafe pointer→JSON with delegate pattern
- `OsCallsCommonShim/include/ValXfer.h` - Shared HandleT/ValueT/TypeT structs
- `OsCallsLinuxShim/src/*.cpp` - Native handlers (handle_lstat, handle_readlink)
- `OsCallsLinuxShim/Makefile` - Links `-lOsCallsCommonShim` with RPATH

### Content-Addressable Storage

Files chunked (1GB), SHA-512 hashed, stored hierarchically:

```text
DATA/ab/cd/abcdef123456789...  &num; hex(sha512(chunk))
```

Auto-reorganizes when prefix dir exceeds 255 entries (splits by next 2 hex digits).

### Building

```bash
dotnet build  &num; Builds all 9 projects including C++ shims via Makefile/CMake
```

**Build internals:**

- `OsCallsLinuxShim.csproj` → runs `make` via `<Target Name="Make">`
- `OsCallsCommonShim.csproj` → builds `libOsCallsCommonShim.so` first (dependency)
- Makefile safety: Checks `BIN` != empty/root before `clean` (prevents `rm /*`)

### Running & Testing

```bash
# Scripts auto-derive paths from script location
./test.sh              &num; Run backup on workspace, silent
./test-show.sh         &num; Same with full output to ../test.ansi
./demo-xattr.sh        &num; Demo xattr functionality

# Or manually:
export LD_LIBRARY_PATH="${PWD}/OsCallsCommonShim/bin/Debug/net8.0:${PWD}/OsCallsLinuxShim/bin/Debug/net8.0"
dotnet run --project=DeDuBa -- <files-to-backup>
```

**Library path requirements:**

- MUST include both `OsCallsCommonShim` and `OsCallsLinuxShim` directories
- Scripts use `SCRIPT_DIR` pattern: `$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)`
- Missing LD_LIBRARY_PATH → cryptic `DllNotFoundException`

### Debugging

`.vscode/launch.json` has two configs:

- **"C&num;: DeDuBa Debug"** - Managed debugging with coreclr
- **"C++: DeDuBa Debug"** - Native debugging with gdb (set breakpoints in .cpp files)

Both auto-prompt for platform (linux-x64/win-x64) and set LD_LIBRARY_PATH correctly.

### Error Handling Pattern

**NEVER throw directly.** Always use:

```csharp
Utilities.Error(file, operation, exception);
```

This captures:

- Caller context: `[CallerFilePath]`, `[CallerLineNumber]`, `[CallerMemberName]`
- Full exception chain via recursive `InnerException`
- Dual output: console (if `Testing=true`) + `_log` StreamWriter

### Metadata Serialization

**C&num; uses standard JSON serialization** via `Sdpack`/`Sdunpack` wrappers:

```csharp
Sdpack(object? v, string name)   // → JsonSerializer.Serialize(v)
Sdunpack(string value)            // → JsonSerializer.Deserialize<object>(value)
```

The Perl original used custom binary encoding (`pack 'w'` for variable-length integers, prefix chars like `'s'`, `'n'`, `'l'`, `'u'`), but the C&num; port simplified this to JSON for easier debugging and cross-platform compatibility.

### Directory Tracking

`Dirtmp` dictionary accumulates entries during traversal → serialized as packed lists → stored in parent dir's inode data. Enables incremental reconstruction.

### Hardlink Detection

Track files by `(st_dev, st_ino)` in `Fs2Ino` dict. Duplicate inode → skip reprocessing.

1. **Linking errors**: If `undefined symbol: CreateHandle`:
   - Verify `OsCallsLinuxShim/Makefile` links `-lOsCallsCommonShim`
   - Check RPATH: `readelf -d libOsCallsLinuxShim.so | grep RUNPATH`
   - Ensure `OsCallsLinuxShim.csproj` has `<ProjectReference>` to `OsCallsCommonShim`

2. **Makefile variables**: Use `$(if $(VAR),$(VAR),default)` pattern for OUT_DIR/PROJECT_DIR
   - Safety checks in `clean` target prevent `rm /*` disasters

3. **Path normalization**: Always use `FileSystem.Canonicalizefilename()` for user inputs (resolves symlinks/relative paths)

4. **Platform detection**: Currently Linux-only at runtime. DeDuBa directly imports `OsCallsLinux` namespace. - Future: Add runtime platform check to switch between OsCallsLinux/OsCallsWindows
   Default `Utilities.Testing = true`:

- Uses local archive `~/projects/Backup/ARCHIVE4` (not `/archive/backup`)
- Verbose `ConWrite()` output with timestamps/locations
- All `Dumper()` diagnostics enabled

## Windows Implementation Status

**OsCallsWindows/OsCallsWindowsShim implementation:**

- Native Win32 implementations for filesystem, security, and ADS operations
- Uses Win32 APIs:
  - `CreateFileW` + `FILE_FLAG_OPEN_REPARSE_POINT`
  - `GetFileInformationByHandleEx` (FileBasicInfo, FileIdInfo)
  - `DeviceIoControl` + `FSCTL_GET_REPARSE_POINT`
  - `GetFinalPathNameByHandleW`
  - `GetNamedSecurityInfoW` → SDDL string
  - `FindFirstStreamW/FindNextStreamW` (ADS enumeration)

See `docs/OsCallsWindowsShim.md` for build instructions and implementation details.

## Documentation

```bash
# Generate C++ API docs (Doxygen → docs/doxygen/html)
doxygen docs/Doxyfile

# Generate C&num; API docs (DocFX → docs/_site)
docfx docs/docfx.json

# Or run task: "Docs: Generate all (Doxygen + DocFX)"
```

Doxygen processes all shim headers: `OsCallsCommonShim/include`, `OsCallsLinuxShim/include`, `OsCallsWindowsShim/include`.

## Historical Context

**Active Perl → C&num; port.** Original `deduba.pl` is reference implementation. Test harness for Perl/C&num; comparison was retired after JSON encoding unification.

## Commit Message Conventions

Follow **Conventional Commits** format: `<type>(<scope>): <subject>`

**Types:**

- `feat:` New feature
- `fix:` Bug fix
- `docs:` Documentation only
- `style:` Code formatting (no logic change)
- `refactor:` Code restructuring (no behavior change)
- `perf:` Performance improvement
- `test:` Add/update tests
- `build:` Build system or dependencies
- `ci:` CI/CD configuration
- `chore:` Maintenance tasks

**Rules:**

- Subject line: 50-72 chars, imperative mood ("add" not "added"), no period
- Body (required): add a blank line after the subject, wrap at ~72 chars, and explain
  what changed and why; use bullet points when covering multiple notable changes
- Optional footer: reference issues (Fixes &num;123), breaking changes (BREAKING CHANGE:)

**Examples:**

```markdown
feat(linux): implement ACL support via acl_get_file

- Add P/Invoke wrapper for acl_get_file
- Serialize ACL entries into JSON
- Cover with xUnit tests (AclTests)
```

```markdown
fix(valxfer): handle null pointers in ToNode cursor

Prevent NRE when native cursor returns null. Add unit test; no behavior
change otherwise.
```

```markdown
docs: update cross-platform interop architecture

Document 3-layer interop pattern and library path requirements.
```

```markdown
refactor(deduba): simplify hardlink detection logic

Extract helper method and remove duplication; no behavior change.
```

```markdown
build(shim): add RPATH to OsCallsLinuxShim Makefile

Ensure runtime linker resolves libOsCallsCommonShim without manual
LD_LIBRARY_PATH.
```
