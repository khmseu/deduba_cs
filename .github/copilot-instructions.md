# DeDuBa - Deduplicating Backup System

## Architecture Overview

DeDuBa is a deduplicating backup system being ported from Perl to C#. It uses content-addressable storage with SHA-512 hashing and BZip2 compression.

**Core Components:**

- **DeDuBa** (main): C# port of the backup logic (`DeDuBa/Deduba.cs`)
- **OsCalls**: C# wrapper for native filesystem operations
- **OsCallsShim**: C++ shared library (`libOsCallsShim.so`) exposing POSIX syscalls (lstat, readlink, canonicalize_file_name)
- **UtilitiesLibrary**: Shared utilities for logging, debugging, and JSON serialization

## Critical Architecture Patterns

### Native Interop Layer

The system uses a unique **C# → C++ → POSIX** interop pattern via P/Invoke:

1. C# calls into `libOsCallsShim.so` using `[DllImport]`
2. C++ exposes POSIX syscalls returning complex `ValueT*` structures
3. `ValXfer.cs` converts pointer-based data to JSON using a state machine iterator pattern (`GetNextValue()`)

**Key files:**

- `OsCalls/ValXfer.cs` - The bridging layer using unsafe pointers
- `OsCallsShim/src/FileSystem.cpp` - Native implementations
- `OsCallsShim/include/ValXfer.h` - Shared struct definitions

### Content-Addressable Storage

Files are chunked (1GB chunks), SHA-512 hashed, and stored in a hierarchical directory structure under `DATA/`:

```
DATA/ab/cd/abcdef123456789...  # hex(sha512(chunk))
```

When a prefix directory exceeds 255 entries, it's automatically reorganized into subdirectories by the next 2 hex digits.

## Developer Workflows

### Building

```bash
# Build everything (including C++ shim via Makefile)
dotnet build

# The OsCallsShim.csproj hooks into Makefile for C++ compilation
# See custom targets: Make, MakeClean, CopySharedLibrary, CopyNativeLibs
```

### Running & Debugging

```bash
# Set library path for libOsCallsShim.so
export LD_LIBRARY_PATH=/bigdata/KAI/projects/Backup/deduba_cs/OsCallsShim/bin/Debug/net8.0

# Run with test archive
dotnet run --project=DeDuBa -- <files-to-backup>

# Comparative testing (C# vs Perl)
./tester.sh  # Runs both implementations, generates diff report
```

**Debug configurations** (`.vscode/launch.json`):

- "C#: DeDuBa Debug" - Managed debugging
- "C++: DeDuBa Debug" - Native debugging with gdb

Both require `LD_LIBRARY_PATH` set correctly. Use platform input prompt (linux-x64/win-x64).

### Testing Architecture

`tester.sh` runs **both** C# and original Perl implementations, comparing log outputs via `logfilter.pl` to verify correctness during the port. Diffs are saved as `artest<date>.diff`.

## Project-Specific Conventions

### Error Handling

All errors use `Utilities.Error(file, operation, exception)` which:

- Includes caller context via `[CallerFilePath]`, `[CallerLineNumber]`, `[CallerMemberName]`
- Writes to both console (if `Testing = true`) and `_log` StreamWriter
- Preserves full exception chains via recursive `InnerException` handling

**Never** throw directly—always route through `Utilities.Error()`.

### Data Serialization

The system uses custom binary serialization (`Sdpack`/`Sdunpack`) with variable-length integer encoding (`pack_w`/`unpack_w`):

- `'s'` prefix = string
- `'n'`/`'N'` prefix = positive/negative packed integer
- `'l'` prefix = list with packed count and element lengths
- `'u'` prefix = null/undefined

This compact format reduces metadata overhead in the archive.

### Directory Tracking

`Dirtmp` dictionary accumulates directory entries during traversal, then serializes them as packed lists stored in the parent directory's inode data. This enables incremental directory reconstruction.

### Hardlink Detection

Files are tracked by `(st_dev, st_ino)` in `Fs2Ino` dictionary. When the same inode is encountered again, it's marked as duplicate without reprocessing.

## Testing Mode

Set `Utilities.Testing = true` (default) to:

- Use local test archive `/home/kai/projects/Backup/ARCHIVE3` instead of `/archive/backup`
- Enable verbose `ConWrite()` output with timestamps/locations
- Output all `Dumper()` diagnostics

## Common Pitfalls

1. **Library loading**: Always set `LD_LIBRARY_PATH` before running. Missing this causes cryptic DllNotFoundException.
2. **Struct layout**: `ValXfer.ValueT` uses `[StructLayout(LayoutKind.Sequential)]` matching C++ memory layout. Any changes require coordination across C#/C++ boundaries.
3. **Path normalization**: Use `FileSystem.Canonicalizefilename()` for all user inputs to resolve symlinks/relative paths consistently.
4. **Memory safety**: `ValXfer.ToNode()` uses unsafe pointers. The C++ side manages lifetime via handle callbacks (`handle_lstat`, `handle_readlink`, `handle_cfn`).

## Port Status

This is an **active Perl → C# port**. Reference `deduba.pl` when C# behavior is unclear. The test harness validates equivalence between implementations.
