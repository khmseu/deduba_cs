# High-Level OS API Plan for DeDuBa

## Summary

Create a single high-level OS API used for all OS interactions while reading source data for the backup system. This API will be implemented per-platform (Linux and Windows) and should be the canonical way DeDuBa accesses the OS for file metadata, content, user/group information, ACL/xattr/ADS, and directory traversal.

Motivation:
- Centralize platform-specific behavior into a small set of well-defined interfaces.
- Simplify testing (mockable interfaces) and reduce repeated platform-checks throughout the code.
- Make a clearly-versioned, documented boundary between backup logic and platform nuances.

Outcome: A `IHighLevelOsApi` interface (in `OsCallsCommon`) with two implementations: `OsCallsLinux.HighLevelOsApi` and `OsCallsWindows.HighLevelOsApi`. Code in `DeDuBa` should be refactored to call the API instead of calling into P/Invoke wrappers directly.

---

## API Design Goals

- Minimal, backup-oriented surface: include operations required by the backup, not the full OS API.
- Common abstractions: normalize fields across OSs (uids vs SIDs, file modes vs NT attributes) where the backup semantics expect them.
- Deterministic failures: map native errors to a standard set of exceptions and structured error codes.
- Performance: maintain the ability for the shim or underlying OS layer to stream values (cursor pattern) while exposing a higher-level API for DeDuBa to consume.
- Testability: provide clear, unit-test friendly interfaces and in-memory/mocked implementations for tests.

---

## Suggested Interface: `IHighLevelOsApi`

Namespace: `OsCallsCommon` (or `DeDuBa.OsCallsCommon` if clearer) so both Linux and Windows implementations can implement it.

Basic responsibilities and returns. Use POCO records for return values.

- File operations & metadata
  - `FileInfoResult GetFileInfo(string path, bool followSymlink = true)`
  - `StreamResult OpenFileRead(string path)` // returns Read-only stream (optionally with length) or throws
  - `byte[] ReadFileSegment(string path, long offset, int length)` // to support partial reads
  - `string ReadLink(string path)`
  - `void ReleaseStream(StreamResult)` // optional when we return handles
  - `bool FileExists(string path)`
  - `bool IsDirectory(string path)`

- Directory traversal
  - `IAsyncEnumerable<DirectoryEntry> ListDirectory(string path)` // yields entries efficiently
  - `DirectoryEntry[] ListDirectorySync(string path)` // optional synchronous convenience method

- ACL / xattrs / ADS (platform-specific)
  - `XAttrs GetXattrs(string path)` // returns map<string, bytes>
  - `Acls GetAcls(string path)` // platform-specific, normalized
  - `AlternateDataStreams GetADS(string path)` // Windows-only, empty on Linux

- User/Group/Name lookups
  - `string GetUserName(ulong uid)` // returns `uid`->`username` (Linux) or legacy SIDs converted to string (Windows)
  - `string GetGroupName(ulong gid)`
  - `ulong LookupUser(string username)` // reverse lookup if needed
  - `ulong LookupGroup(string groupname)`

- Basic path helpers
  - `string CanonicalizePath(string path)` // resolve symlinks (optional) and normalize separators
  - `string NormalizePathCase(string path)` // optional, for case-insensitive systems

- File type / special types
  - `FileType GetFileType(string path)` // enum: File, Directory, Symlink, BlockDevice, CharDevice, FIFO, Socket, Unknown

- Misc
  - `long GetFilesystemId(string path)` // for dedup / device checks
  - `InodeInfo GetInodeInfo(string path)` // for hardlink recognition (dev+ino canonicalization)

---

## POCO Types (examples)

- `FileInfoResult`: { Path, FileType, Mode, Uid, Gid, Size, Mtime, Atime, Ctime, LinkTarget (for symlink), Device, Inode, LinkCount }
- `DirectoryEntry`: { Name, FullPath, FileType, Size, Mode, Uid, Gid }
- `XAttrs`: { Dictionary<string, byte[]> }
- `Acls`: { OwnerAcl, GroupAcl, OtherAcl, AdditionalAclEntries }
- `AlternateDataStreams`: { Dictionary<string, byte[]> }
- `InodeInfo`: { Device, Inode }

Note: Use `System.Text.Json` friendly classes or `JsonNode` if we want to serialize across components.

---

## Handling Differences Between Linux and Windows

1. User/Group vs SID:
   - Linux: use uid/gid and map to names via getpwuid/getgrgid.
   - Windows: map to SIDs and return a human-readable `"DOMAIN\\Account"` or SDDL depending on desired behavior.
   - Interface returns strings for username/group name; the platform-specific implementations handle conversion/lookup.

2. Quirky fields (inodes, device numbers):
   - Windows doesn't have POSIX inodes in the same way; the Windows impl can emulate `InodeInfo` from file ID or similar (via GetFileInformationByHandle) and return a stable device/fileid tuple.

3. Extended attributes and ADS:
   - Linux: `getxattr`/`listxattr` support.
   - Windows: NTFS uses Alternate Data Streams; return ADS as a normalized set or empty dictionary.

4. ACLs:
   - Linux ACLs via `acl_get_file` — normalize to a canonical ACL structure.
   - Windows: Read security descriptor, then convert to a similar structure (owner, group, DACL entries) for the API.

5. Path Canonicalization:
   - Normalize separators and canonicalize path using the platform rule set.
   - Keep an optional `followSymlink` parameter to preserve symlink metadata for backup when needed.

6. File types:
   - Some types (block/char devices, sockets, FIFOs) exist on Linux but are less relevant or different on Windows — return `Unknown` or a generic fallback when not applicable.

7. Symbolic link behavior:
   - On Linux, symlinks are ubiquitous and can be preserved.
   - On Windows, we need to correctly detect reparse points and translate them to defined `FileType.Symlink` with link target if appropriate.

8. Error handling
   - Define `HighLevelOsApiException` with an enum `ErrorKind` (e.g., NotFound, PermissionDenied, IOError, NotSupported, InvalidArgument, Unknown) and optionally carry inner exceptions and native error code. Platform impl should wrap native errors accordingly.

---

## Migration Strategy

1. Start with a small interface scope: `GetFileInfo`, `OpenFileRead` (`Stream`), `ListDirectory`, `GetUserName`, `GetGroupName`, `GetXattrs`, `ReadLink`, `InodeInfo`.
2. Implement `IHighLevelOsApi` in `OsCallsLinux` using existing wrappers in `FileSystem.cs`, `Xattr.cs`, and `UserGroupDatabase.cs`.
3. Implement `IHighLevelOsApi` in `OsCallsWindows` using `FileSystem.cs`, `Streams.cs`, and `Security.cs`.
4. Add unit tests mocking the interface to `DeDuBa.Test` so we can exercise backup logic independent of the OS.
5. Gradually replace direct P/Invoke or shim calls in `DeDuBa` with `IHighLevelOsApi` calls. Migrate the high-level paths in the following order:
   1. Non-coding hot path: read-only metadata retrieval for directory entries.
   2. File content reads (OpenFileRead/Stream). Ensure stream patterns and async read behavior are preserved.
   3. UID/GID lookups and name mapping.
   4. ACL/xattr/ADS retrieval.
6. Provide a temporary compatibility adapter/module (for the short term) that intercepts old API calls and forwards them to the new API, to limit dependency churn across the codebase.

---

## Testing Strategy

- Unit tests: `IHighLevelOsApi` mocked or stubbed to exercise `DeDuBa` logic without touching native OS.
- Integration tests:
  - Linux: run backup against controlled directory fixtures with various file types, symlinks, ACLs, xattrs.
  - Windows: run similar tests using Windows VM/host, or annotate that Windows-only tests should run in CI Windows runner.
- Cross-platform: tests verifying normalized output (e.g., `FileType` enum, `InodeInfo` mapping) to ensure backup logic behaves the same across OS.

---

## Instrumentation & Performance

- Keep a streaming API for directories. Prefer `IAsyncEnumerable<DirectoryEntry>` to avoid building big arrays in memory.
- Provide `Batch`/`Cursor` style helpers if low-level shim supports cursoring (like `ValXfer` currently), to avoid repeated allocations.
- Ensure `OpenFileRead` returns a `Stream` so we can leverage .NET built-in buffered reads and length-limited read: we can optionally return a `SeekableStream`.

---

## Error Mapping and Logging

- Map native errors to `HighLevelOsApiException` with `ErrorKind` to avoid leaking platform-specific code.
- Continue to use `Utilities.Error(file, op, exception)` where appropriate for dual logging (console + _log files) and consistent error report semantics.

---

## Developer Experience

- Put `IHighLevelOsApi` in `OsCallsCommon` (shared) so both Linux/Windows implement it.
- Name suggestions: `IHighLevelOsApi`, `HighLevelOsApiLinux`, `HighLevelOsApiWindows`.
- Add a `HighLevelOsApiFactory` with `GetForLocalPlatform()` to return the right implementation at runtime.
- Ensure DI-friendly implementation so `DeDuBa` can receive an `IHighLevelOsApi` via constructor injection.

---

## Minimal Prototype Implementation Steps

1. Add `OsCallsCommon/IHighLevelOsApi.cs` with the minimal set of methods.
2. Add `OsCallsLinux/HighLevelOsApi.cs` and implement methods using existing wrappers (`FileSystem.cs`, `Xattr.cs`, `UserGroupDatabase.cs`)—these are the initial thin wrappers.
3. Add `OsCallsWindows/HighLevelOsApi.cs` and map to Windows methods (`FileSystem.cs`, `Streams.cs`, `Security.cs`).
4. Refactor `DeDuBa/Deduba.cs` and `DeDuBa.Test` to inject `IHighLevelOsApi` and stop calling lower-level OS methods directly.

---

## Roadmap & Timeline (Short Proposal)

1. Week 1: Draft interface, prototypes for Linux + Windows, and prototype unit tests.
2. Week 2: Implement Linux, replace a few core metadata reads in `DeDuBa` with interface calls; update tests.
3. Week 3: Implement Windows; replicate replacements for Windows-sensitive code paths.
4. Week 4: Finish migration and tests; add a CI job for cross-platform coverage.

---

## Considerations & Open Questions

- How much do we want to normalize between platforms? For example, Windows SDDL vs Linux ACLs—should the API convert both to a single, canonical representation suitable for the backup, or return native structures the backup will understand differently?
- Which fields must be preserved exactly for a faithful backup (e.g., mtime, ctime, symlink target, owner, group, permission bits) vs those that can be normalized or omitted if unsupported?
- Do we need a single serialization format for ACL/xattrs across both OSs (e.g., store them as JSON maps), or should we store as raw bytes with metadata about origin and type?

---

## Next Steps

1. Confirm API scope and POCO shapes with the team.
2. Create `IHighLevelOsApi` and a minimal prototype for Linux.
3. Add unit tests and a migration checklist; add CI integration.

---

## Appendices: Example Interface (C#)

```csharp
namespace OsCallsCommon;

public interface IHighLevelOsApi
{
    FileInfoResult GetFileInfo(string path, bool followSymlink = true);
    Stream OpenFileRead(string path); // throws HighLevelOsApiException
    IAsyncEnumerable<DirectoryEntry> ListDirectory(string path);
    string GetUserName(ulong uid);
    string GetGroupName(ulong gid);
    IDictionary<string, byte[]> GetXattrs(string path);
    string ReadLink(string path);
    InodeInfo GetInodeInfo(string path);
}

public record FileInfoResult(string Path, FileType Type, ulong Uid, ulong Gid, long Size, DateTimeOffset Mtime, long Device, long Inode, int Mode = 0, int LinkCount = 0, string? LinkTarget = null);

public record DirectoryEntry(string Name, string FullPath, FileType Type, long Size, ulong Uid, ulong Gid);
public record InodeInfo(ulong Device, ulong Inode);
public enum FileType { File, Directory, Symlink, BlockDevice, CharDevice, FIFO, Socket, Unknown }
```


---

This captures the current plan and the migration steps to get us from tightly-coupled P/Invoke wrappers to a single high-level OS API that DeDuBa can use across platforms. If you'd like, I can now scaffold the interface and Linux/Windows prototypes and update a sample `DeDuBa` file to use the new interface. 
