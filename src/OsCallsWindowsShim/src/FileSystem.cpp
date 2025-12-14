/**
 * @file FileSystem.cpp
 * @brief Windows filesystem operations implementation
 *
 * This module implements Windows-specific filesystem operations to provide
 * cross-platform compatibility with POSIX systems. The implementation uses
 * Win32 APIs to query file metadata, read reparse points, and canonicalize
 * paths.
 *
 * ## Architecture
 *
 * Functions follow the ValueT iterator pattern established in OsCallsLinuxShim:
 * 1. Native function allocates data and creates ValueT with handler
 * 2. Handler function yields metadata fields sequentially via GetNextValue
 * 3. C# ValXfer.ToNode converts iterator to JSON object
 *
 * ## Key Windows APIs Used
 *
 * ### CreateFileW
 * Opens files/directories with specific flags:
 * - FILE_FLAG_BACKUP_SEMANTICS: Required to open directories
 * - FILE_FLAG_OPEN_REPARSE_POINT: Prevents following symlinks (like lstat)
 * - OPEN_EXISTING: File must already exist
 *
 * ### GetFileInformationByHandle
 * Retrieves file metadata without additional syscalls:
 * - File attributes (directory, hidden, readonly, reparse point)
 * - File size (high/low 32-bit parts)
 * - Timestamps (creation, access, write)
 * - File ID (volume serial + file index → inode equivalent)
 * - Link count (number of hardlinks)
 *
 * ### DeviceIoControl with FSCTL_GET_REPARSE_POINT
 * Reads reparse point data without following the link:
 * - Returns REPARSE_DATA_BUFFER with tag and target path
 * - Handles both symbolic links and junction points
 * - Requires FILE_FLAG_OPEN_REPARSE_POINT when opening
 *
 * ### GetFinalPathNameByHandleW
 * Resolves paths to canonical form (follows symlinks):
 * - FILE_NAME_NORMALIZED: Returns absolute path with resolved links
 * - VOLUME_NAME_DOS: Uses drive letters (C:\) not volume GUIDs
 * - May return paths with \\?\ prefix (extended-length paths)
 *
 * ## Windows vs POSIX Differences
 *
 * ### File Types
 * Windows has fewer file types than POSIX:
 * - No block/character devices exposed as files
 * - No FIFOs or Unix domain sockets as files
 * - Reparse points approximate symlinks/junctions
 *
 * ### Permissions
 * Windows uses ACLs, not simple permission bits:
 * - Read-only attribute approximates write permission
 * - No direct owner/group/other distinction
 * - Directories always get execute bit (for traversal)
 * - See Security.cpp for full ACL/SDDL support
 *
 * ### Timestamps
 * Windows tracks creation time (birth time), not ctime:
 * - Creation time: when file was created
 * - Access time: last read operation
 * - Write time: last modification
 * - No inode change time (ctime) → use creation time
 *
 * ### File IDs
 * Windows uses volume serial + file index as inode:
 * - Volume serial number (32-bit) → st_dev
 * - File index (64-bit) → st_ino
 * - Unique within volume, persistent across reboots
 * - Hardlinks share same file ID
 *
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfileinformationbyhandle
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/winioctl/ni-winioctl-fsctl_get_reparse_point
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew
 */
#include "Platform.h"
// Platform.h must come first
#include "FileSystem.h"
#include <cstdio>
#include <cstring>
#include <vector>
#if 0
/* Local WIN32 macros moved to Platform.h to keep OS-specific defines centralized. */
#endif
#include <sddl.h>
#include <sstream>
#include <string>
#include <windows.h>
#include <winioctl.h>

// Define REPARSE_DATA_BUFFER if not available in MinGW headers
#ifndef REPARSE_DATA_BUFFER_HEADER_SIZE
typedef struct _REPARSE_DATA_BUFFER {
    ULONG  ReparseTag;
    USHORT ReparseDataLength;
    USHORT Reserved;

    union {
        struct {
            USHORT SubstituteNameOffset;
            USHORT SubstituteNameLength;
            USHORT PrintNameOffset;
            USHORT PrintNameLength;
            ULONG  Flags;
            WCHAR  PathBuffer[1];
        } SymbolicLinkReparseBuffer;

        struct {
            USHORT SubstituteNameOffset;
            USHORT SubstituteNameLength;
            USHORT PrintNameOffset;
            USHORT PrintNameLength;
            WCHAR  PathBuffer[1];
        } MountPointReparseBuffer;

        struct {
            UCHAR DataBuffer[1];
        } GenericReparseBuffer;
    };
} REPARSE_DATA_BUFFER, *PREPARSE_DATA_BUFFER;

#define REPARSE_DATA_BUFFER_HEADER_SIZE FIELD_OFFSET(REPARSE_DATA_BUFFER, GenericReparseBuffer)
#endif

#ifndef IO_REPARSE_TAG_SYMLINK
#define IO_REPARSE_TAG_SYMLINK (0xA000000CL)
#endif

#ifndef IO_REPARSE_TAG_MOUNT_POINT
#define IO_REPARSE_TAG_MOUNT_POINT (0xA0000003L)
#endif

/* FIELD_OFFSET is defined in Platform.h for Windows. */

namespace OsCallsWindows {
using namespace OsCalls;

// Convert SID_NAME_USE to a short keyword similar to S_TYPEIS* macros
static const char *SidNameUseToKeyword(SID_NAME_USE use) {
    switch (use) {
    case SidTypeUser:
        return "user";
    case SidTypeGroup:
        return "group";
    case SidTypeDomain:
        return "domain";
    case SidTypeAlias:
        return "alias";
    case SidTypeWellKnownGroup:
        return "wellknown";
    case SidTypeDeletedAccount:
        return "deleted";
    case SidTypeInvalid:
        return "invalid";
    case SidTypeUnknown:
        return "unknown";
    case SidTypeComputer:
        return "computer";
    case SidTypeLabel:
        return "label";
    default:
        return "unknown";
    }
}

// Convert wide string (UTF-16) to UTF-8 std::string
static std::string WideToUtf8(const std::wstring &w) {
    if (w.empty())
        return std::string();
    int size_needed = WideCharToMultiByte(CP_UTF8, 0, w.c_str(), (int)w.size(), NULL, 0, NULL, NULL);
    if (size_needed <= 0)
        return std::string();
    std::string result(size_needed, 0);
    WideCharToMultiByte(CP_UTF8, 0, w.c_str(), (int)w.size(), &result[0], size_needed, NULL, NULL);
    return result;
}

// Resolve a PSID to a human-readable string: "<kind>: DOMAIN\\Name" or fallback to SID string
static std::string ResolveAccountNameFromSid(PSID sid) {
    if (!sid)
        return std::string();

    DWORD        nameSize = 0;
    DWORD        domainSize = 0;
    SID_NAME_USE use = SidTypeUnknown;

    // First call to get required buffer sizes
    LookupAccountSidW(NULL, sid, NULL, &nameSize, NULL, &domainSize, &use);
    DWORD err = GetLastError();
    if (err != ERROR_INSUFFICIENT_BUFFER && err != ERROR_NONE_MAPPED) {
        // Fallback: return SID string
        LPWSTR sidStr = NULL;
        if (ConvertSidToStringSidW(sid, &sidStr)) {
            std::wstring ws(sidStr);
            LocalFree(sidStr);
            return WideToUtf8(ws);
        }
        return std::string();
    }

    std::wstring name;
    std::wstring domain;
    if (nameSize > 0)
        name.resize(nameSize);
    if (domainSize > 0)
        domain.resize(domainSize);

    if (LookupAccountSidW(NULL, sid, name.data(), &nameSize, domain.data(), &domainSize, &use)) {
        // Trim possible extra nulls from sizes returned
        name.resize(nameSize);
        domain.resize(domainSize);
        // Remove trailing null if present
        if (!name.empty() && name.back() == L'\0')
            name.pop_back();
        if (!domain.empty() && domain.back() == L'\0')
            domain.pop_back();

        std::string        kind = SidNameUseToKeyword(use);
        std::string        dn = WideToUtf8(domain);
        std::string        nn = WideToUtf8(name);
        std::ostringstream out;
        out << kind << ": ";
        if (!dn.empty())
            out << dn << "\\";
        out << nn;
        return out.str();
    }

    // Fallback: return SID string
    LPWSTR sidStr = NULL;
    if (ConvertSidToStringSidW(sid, &sidStr)) {
        std::wstring ws(sidStr);
        LocalFree(sidStr);
        return WideToUtf8(ws);
    }

    return std::string();
}

// Simple helper: construct a SID from NT authority + single RID and resolve it.
// Note: mapping numeric uid/gid to Windows RIDs is not guaranteed; caller should
// only use this for best-effort resolution.
static std::string ResolveAccountNameFromRid(uint32_t rid) {
    SID_IDENTIFIER_AUTHORITY ntAuth = SECURITY_NT_AUTHORITY;
    PSID                     pSid = NULL;
    if (!AllocateAndInitializeSid(&ntAuth, 1, rid, 0, 0, 0, 0, 0, 0, 0, &pSid)) {
        return std::to_string(rid);
    }

    std::string result = ResolveAccountNameFromSid(pSid);
    FreeSid(pSid);
    if (result.empty())
        return std::to_string(rid);
    return result;
}

/**
 * @brief No-op handler for error returns.
 *
 * Error ValueT structures don't need iteration, but GetNextValue is called
 * unconditionally by ToNode. This handler returns false immediately to skip
 * iteration.
 *
 * @param value Pointer to error ValueT (unused).
 * @return false to indicate no values to iterate.
 */
static bool handle_error(ValueT *value) {
    (void)value;  // Unused parameter
    return false;
}

// Helper structure to hold file information
struct WinFileInfo {
    DWORD         fileAttributes;
    LARGE_INTEGER fileSize;
    FILETIME      creationTime;
    FILETIME      lastAccessTime;
    FILETIME      lastWriteTime;
    LARGE_INTEGER fileIndex;           // File ID (inode equivalent)
    DWORD         volumeSerialNumber;  // Device equivalent
    DWORD         numberOfLinks;
    DWORD         reparseTag;
};

/**
 * @brief Helper to open a file or directory with common share + semantic flags.
 *
 * Centralizes the CreateFileW invocation pattern used by win_lstat,
 * win_readlink, and win_canonicalize_file_name. Always requests no direct
 * access (metadata only) and applies FILE_FLAG_BACKUP_SEMANTICS so that
 * directories can be opened. Additional flags (e.g.
 * FILE_FLAG_OPEN_REPARSE_POINT) can be supplied via the extraFlags parameter.
 *
 * @param path      Wide-character path to open.
 * @param extraFlags Additional flags OR'd with FILE_FLAG_BACKUP_SEMANTICS.
 * @param access    Desired access mask (default 0 for metadata only).
 * @return HANDLE   Valid handle on success, INVALID_HANDLE_VALUE on failure.
 */
static HANDLE win_open_path(const wchar_t *path, DWORD extraFlags, DWORD access = 0) {
    return CreateFileW(path,
                       access,
                       FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                       nullptr,
                       OPEN_EXISTING,
                       FILE_FLAG_BACKUP_SEMANTICS | extraFlags,
                       nullptr);
}

/**
 * @brief Convert Windows file attributes to POSIX-like mode bits
 *
 * Windows file attributes don't directly map to POSIX permission bits,
 * so this function provides a reasonable approximation:
 *
 * File type mapping (high 4 bits of st_mode):
 * - Symbolic links (IO_REPARSE_TAG_SYMLINK) → S_IFLNK (0120000)
 * - Junction points (IO_REPARSE_TAG_MOUNT_POINT) → S_IFLNK (0120000)
 * - Directories → S_IFDIR (0040000)
 * - Regular files → S_IFREG (0100000)
 *
 * Permission bits (low 9 bits):
 * - Owner: always readable (0400), writable if not read-only (0200),
 *   executable if directory (0100)
 * - Group and Other: inherit from owner bits (simplified model)
 *
 * @param attrs Windows FILE_ATTRIBUTE_* flags from GetFileInformationByHandle
 * @param reparseTag Reparse tag if FILE_ATTRIBUTE_REPARSE_POINT is set
 * @return POSIX-like mode bits compatible with st_mode field
 */
static DWORD win_attrs_to_mode(DWORD attrs, DWORD reparseTag) {
    DWORD mode = 0;

    // File type bits (high nibble)
    if (attrs & FILE_ATTRIBUTE_REPARSE_POINT) {
        if (reparseTag == IO_REPARSE_TAG_SYMLINK) {
            mode |= 0120000;  // S_IFLNK
        } else if (reparseTag == IO_REPARSE_TAG_MOUNT_POINT) {
            mode |= 0120000;  // Junction treated as symlink
        } else {
            mode |= 0100000;  // S_IFREG (other reparse points as regular)
        }
    } else if (attrs & FILE_ATTRIBUTE_DIRECTORY) {
        mode |= 0040000;  // S_IFDIR
    } else {
        mode |= 0100000;  // S_IFREG
    }

    // Permission bits (simplified - Windows doesn't have direct equivalents)
    // Owner: always read, write if not readonly, execute if directory or .exe
    mode |= 0000400;  // S_IRUSR
    if (!(attrs & FILE_ATTRIBUTE_READONLY)) {
        mode |= 0000200;  // S_IWUSR
    }
    if ((attrs & FILE_ATTRIBUTE_DIRECTORY)) {
        mode |= 0000100;  // S_IXUSR for directories
    }

    // Group and other: inherit from owner (simplified)
    mode |= (mode & 0000700) >> 3;  // Group
    mode |= (mode & 0000700) >> 6;  // Other

    return mode;
}

/**
 * @brief Convert FILETIME to timespec (100-nanosecond intervals since
 * 1601-01-01)
 *
 * Windows FILETIME represents time as 100-nanosecond intervals since
 * January 1, 1601 UTC (the start of the Gregorian calendar cycle).
 *
 * Unix timespec represents time as seconds + nanoseconds since
 * January 1, 1970 UTC (Unix epoch).
 *
 * The difference between these epochs is 11,644,473,600 seconds
 * (369 years, 89 leap days).
 *
 * Conversion steps:
 * 1. Combine FILETIME's low/high parts into 64-bit integer
 * 2. Divide by 10,000,000 to get seconds
 * 3. Subtract epoch difference to get Unix seconds
 * 4. Use modulo to get nanosecond remainder
 *
 * @param ft Windows FILETIME structure (creation/access/write time).
 * @return Unix timespec with tv_sec and tv_nsec fields.
 */
static OsCalls::TimeSpec64 filetime_to_timespec(const FILETIME &ft) {
    // FILETIME is 100-nanosecond intervals since 1601-01-01
    // Unix epoch is 1970-01-01, difference is 11644473600 seconds
    ULARGE_INTEGER ull;
    ull.LowPart = ft.dwLowDateTime;
    ull.HighPart = ft.dwHighDateTime;

    // Convert to seconds and nanoseconds
    const uint64_t EPOCH_DIFF = 11644473600ULL;
    uint64_t       total_100ns = ull.QuadPart;
    uint64_t       seconds_since_1601 = total_100ns / 10000000ULL;

    // DEBUG: Always output to stderr to verify this code path is executed
    fprintf(stderr,
            "[DEBUG filetime_to_timespec] QuadPart=%llu, seconds_since_1601=%llu, EPOCH_DIFF=%llu\n",
            (unsigned long long)ull.QuadPart,
            (unsigned long long)seconds_since_1601,
            (unsigned long long)EPOCH_DIFF);

    // Handle timestamps before Unix epoch (1970) or zero/invalid timestamps
    // by clamping to epoch+1 (1 second) as a sentinel value for debugging
    int64_t seconds;
    if (seconds_since_1601 < EPOCH_DIFF) {
        fprintf(stderr, "[DEBUG filetime_to_timespec] CLAMPING to 1 (pre-epoch detected)\n");
        seconds = 1;  // Clamp to Unix epoch+1 for pre-1970 or invalid timestamps (sentinel for testing)
    } else {
        seconds = static_cast<int64_t>(seconds_since_1601 - EPOCH_DIFF);
    }

    int64_t nanoseconds = static_cast<int64_t>((total_100ns % 10000000ULL) * 100);

    OsCalls::TimeSpec64 ts;
    ts.tv_sec = seconds;
    ts.tv_nsec = nanoseconds;

    return ts;
}

/**
 * @brief Resolve a numeric RID (Relative ID) to an account name via LookupAccountSid.
 *
 * Converts a numeric RID to a Windows account name using the LookupAccountSid
 * Win32 API. Similar to how we use S_ISDIR, S_ISREG macros on POSIX, this
 * function yields SID_NAME_USE as textual flags (user, group, alias, etc).
 *
 * Only well-known RIDs (like SYSTEM=18) reliably map to SIDs without a domain
 * context. Other RIDs return numeric fallback.
 *
 * @param rid Relative ID (typically st_uid or st_gid value).
 * @return Textual account name, or numeric string on failure.
 */
static std::string LookupAccountName(uint32_t rid) {
    // Only well-known RIDs are reliably available
    if (rid == 0)
        return "system";
    if (rid == 18)
        return "system";

    // For other RIDs, return numeric ID as fallback
    return std::to_string(rid);
}

/**
 * @brief Convert SID_NAME_USE enum to a string flag (similar to S_ISDIR, etc).
 *
 * Maps Windows SID_NAME_USE values to human-readable flags like we do for
 * POSIX file type macros (S_ISDIR → "dir", S_ISREG → "reg").
 *
 * Example: SidTypeUser → "user", SidTypeGroup → "group", SidTypeAlias → "alias"
 *
 * @param use SID_NAME_USE enum value from LookupAccountSid.
 * @return Static string representation (user, group, alias, wellknowngroup,
 * etc).
 */
static const char *sid_use_to_string(SID_NAME_USE use) {
    switch (use) {
    case SidTypeUser:
        return "user";
    case SidTypeGroup:
        return "group";
    case SidTypeDomain:
        return "domain";
    case SidTypeAlias:
        return "alias";
    case SidTypeWellKnownGroup:
        return "wellknowngroup";
    case SidTypeDeletedAccount:
        return "deleted";
    case SidTypeInvalid:
        return "invalid";
    case SidTypeUnknown:
        return "unknown";
    case SidTypeComputer:
        return "computer";
    case SidTypeLabel:
        return "label";
    default:
        return "unknown";
    }
}

/**
 * @brief Handler for windows_GetFileInformationByHandle results - iterates through file metadata
 * fields.
 *
 * Yields file information fields sequentially in POSIX stat order: st_dev,
 * st_ino, st_mode, file type flags (S_ISDIR, S_ISREG, S_ISLNK, etc.), st_nlink,
 * st_uid/gid (always 0 on Windows), st_size, timestamps (st_atim, st_mtim,
 * st_ctim), st_blksize, and st_blocks. Cleans up WinFileInfo on completion.
 *
 * @param value Pointer to ValueT with Handle.data1 containing WinFileInfo*.
 * @return true if more fields remain, false when iteration completes.
 */
static bool handle_GetFileInformationByHandle(ValueT *value) {
    auto info = reinterpret_cast<WinFileInfo *>(value->Handle.data1);
    switch (value->Handle.index) {
    case 0:
        if (value->Type == TypeT::IsOk) {
            set_val(Number, "st_dev", info->volumeSerialNumber);
            return true;
        }
    // Error case - fall through to cleanup
    default:
        if (info)
            delete info;  // Null-safe cleanup
        return false;
    case 1:
        set_val(Number, "st_ino", info->fileIndex.QuadPart);
        return true;
    case 2: {
        DWORD mode = win_attrs_to_mode(info->fileAttributes, info->reparseTag);
        set_val(Number, "st_mode", mode);
        return true;
    }
    case 3:
        set_val(Boolean, "S_ISBLK", false);  // Windows doesn't have block devices
        return true;
    case 4:
        set_val(Boolean, "S_ISCHR",
                false);  // Windows doesn't expose char devices this way
        return true;
    case 5:
        set_val(Boolean, "S_ISDIR", (info->fileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0);
        return true;
    case 6:
        set_val(Boolean, "S_ISFIFO", false);  // Windows doesn't have FIFOs
        return true;
    case 7: {
        bool is_symlink =
            (info->fileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) &&
            (info->reparseTag == IO_REPARSE_TAG_SYMLINK || info->reparseTag == IO_REPARSE_TAG_MOUNT_POINT);
        set_val(Boolean, "S_ISLNK", is_symlink);
        return true;
    }
    case 8: {
        bool is_regular =
            !(info->fileAttributes & FILE_ATTRIBUTE_DIRECTORY) &&
            !((info->fileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) &&
              (info->reparseTag == IO_REPARSE_TAG_SYMLINK || info->reparseTag == IO_REPARSE_TAG_MOUNT_POINT));
        set_val(Boolean, "S_ISREG", is_regular);
        return true;
    }
    case 9:
        set_val(Boolean, "S_ISSOCK",
                false);  // Windows doesn't have Unix sockets as files
        return true;
    case 10:
        set_val(Boolean, "S_TYPEISMQ",
                false);  // Message queues not exposed as files
        return true;
    case 11:
        set_val(Boolean, "S_TYPEISSEM", false);  // Semaphores not exposed as files
        return true;
    case 12:
        set_val(Boolean, "S_TYPEISSHM",
                false);  // Shared memory not exposed as files
        return true;
    case 13:
        set_val(Boolean, "S_TYPEISTMO",
                false);  // Typed memory objects not supported
        return true;
    case 14:
        set_val(Number, "st_nlink", info->numberOfLinks);
        return true;
    case 15:
        set_val(Number, "st_uid", 0);  // Windows doesn't have Unix UIDs
        return true;
    case 16:
        set_val(Number, "st_gid", 0);  // Windows doesn't have Unix GIDs
        return true;
    case 17:
        set_val(Number, "st_rdev", 0);  // Device files not supported
        return true;
    case 18:
        set_val(Number, "st_size", info->fileSize.QuadPart);
        return true;
    case 19: {
        OsCalls::TimeSpec64 ts = filetime_to_timespec(info->lastAccessTime);
        set_val(TimeSpec, "st_atim", ts);
        return true;
    }
    case 20: {
        OsCalls::TimeSpec64 ts = filetime_to_timespec(info->lastWriteTime);
        set_val(TimeSpec, "st_mtim", ts);
        return true;
    }
    case 21: {
        OsCalls::TimeSpec64 ts = filetime_to_timespec(info->creationTime);
        set_val(TimeSpec, "st_ctim", ts);  // Using creation time as ctime
        return true;
    }
    case 22:
        set_val(Number, "st_blksize", 4096);  // Default block size
        return true;
    case 23: {
        // Calculate blocks (512-byte units)
        int64_t blocks = (info->fileSize.QuadPart + 511) / 512;
        set_val(Number, "st_blocks", blocks);
        return true;
    }
    }
}

extern "C" DLL_EXPORT ValueT *windows_GetFileInformationByHandle(const wchar_t *path) {
    auto               info = new WinFileInfo{};
    auto               v = new ValueT();
    static const char *errno_name = "errno";

    // Open file/directory without following reparse points
    HANDLE hFile = win_open_path(path, FILE_FLAG_OPEN_REPARSE_POINT);

    if (hFile == INVALID_HANDLE_VALUE) {
        DWORD err = GetLastError();
        // Return error as errno field for consistency with LStat expectations
        delete info;
        CreateHandle(v, handle_error, nullptr, nullptr);
        v->Type = TypeT::IsError;
        v->Name = errno_name;  // Use static literal
        v->Number = err;
        return v;
    }

    // Get basic file information
    BY_HANDLE_FILE_INFORMATION fileInfo;
    if (!GetFileInformationByHandle(hFile, &fileInfo)) {
        DWORD err = GetLastError();
        CloseHandle(hFile);
        CreateHandle(v, handle_error, nullptr, nullptr);
        CreateHandle(v, handle_error, nullptr, nullptr);
        delete info;
        v->Type = TypeT::IsError;
        v->Name = errno_name;  // Use static literal
        v->Number = err;
        return v;
    }

    // Populate our structure
    info->fileAttributes = fileInfo.dwFileAttributes;
    info->fileSize.LowPart = fileInfo.nFileSizeLow;
    info->fileSize.HighPart = fileInfo.nFileSizeHigh;
    info->creationTime = fileInfo.ftCreationTime;
    info->lastAccessTime = fileInfo.ftLastAccessTime;
    info->lastWriteTime = fileInfo.ftLastWriteTime;
    info->volumeSerialNumber = fileInfo.dwVolumeSerialNumber;
    info->numberOfLinks = fileInfo.nNumberOfLinks;

    // Combine file index parts to create inode-like value
    info->fileIndex.LowPart = fileInfo.nFileIndexLow;
    info->fileIndex.HighPart = fileInfo.nFileIndexHigh;

    // Get reparse tag if this is a reparse point
    info->reparseTag = 0;
    if (info->fileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) {
        // Query reparse point to get tag
        BYTE  buffer[MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
        DWORD bytesReturned;
        if (DeviceIoControl(
                hFile, FSCTL_GET_REPARSE_POINT, nullptr, 0, buffer, sizeof(buffer), &bytesReturned, nullptr)) {
            auto reparseData = reinterpret_cast<REPARSE_DATA_BUFFER *>(buffer);
            info->reparseTag = reparseData->ReparseTag;
        }
    }

    CloseHandle(hFile);

    CreateHandle(v, handle_GetFileInformationByHandle, info, nullptr);
    v->Type = TypeT::IsOk;
    return v;
}

// Legacy compatibility wrapper
extern "C" DLL_EXPORT ValueT *win_lstat(const wchar_t *path) {
    return windows_GetFileInformationByHandle(path);
}

/**
 * @brief Handler for win_readlink results - yields reparse point target path.
 *
 * Converts wide-character target path to UTF-8 and yields as string value.
 * Cleans up allocated buffer on completion.
 *
 * @param value Pointer to ValueT with Handle.data1 containing wchar_t* target
 * path.
 * @return true on first call if successful, false to signal completion.
 */
static bool handle_DeviceIoControl_GetReparsePoint(ValueT *value) {
    auto target = reinterpret_cast<wchar_t *>(value->Handle.data1);
    switch (value->Handle.index) {
    case 0:
        if (value->Type == TypeT::IsOk) {
            // Convert wide string to UTF-8
            int size = WideCharToMultiByte(CP_UTF8, 0, target, -1, nullptr, 0, nullptr, nullptr);
            if (size > 0) {
                auto utf8 = new char[size];
                WideCharToMultiByte(CP_UTF8, 0, target, -1, utf8, size, nullptr, nullptr);
                value->String = utf8;
                value->Name = "path";
                value->Type = TypeT::IsString;
                return true;
            }
        }
    // Error or conversion failed - fall through
    default:
        delete[] target;
        return false;
    }
}

extern "C" DLL_EXPORT ValueT *windows_DeviceIoControl_GetReparsePoint(const wchar_t *path) {
    wchar_t *target = nullptr;
    auto     v = new ValueT();

    // Open the reparse point
    HANDLE hFile = win_open_path(path, FILE_FLAG_OPEN_REPARSE_POINT);

    if (hFile == INVALID_HANDLE_VALUE) {
        DWORD err = GetLastError();
        target = new wchar_t[1];
        target[0] = L'\0';
        CreateHandle(v, handle_DeviceIoControl_GetReparsePoint, target, nullptr);
        v->Number = err;
        return v;
    }

    // Get reparse point data
    BYTE  buffer[MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
    DWORD bytesReturned;
    if (!DeviceIoControl(hFile, FSCTL_GET_REPARSE_POINT, nullptr, 0, buffer, sizeof(buffer), &bytesReturned, nullptr)) {
        DWORD err = GetLastError();
        CloseHandle(hFile);
        target = new wchar_t[1];
        target[0] = L'\0';
        CreateHandle(v, handle_DeviceIoControl_GetReparsePoint, target, nullptr);
        v->Number = err;
        return v;
    }

    CloseHandle(hFile);

    auto reparseData = reinterpret_cast<REPARSE_DATA_BUFFER *>(buffer);

    // Extract target path based on reparse tag
    if (reparseData->ReparseTag == IO_REPARSE_TAG_SYMLINK) {
        USHORT targetLength = reparseData->SymbolicLinkReparseBuffer.PrintNameLength / sizeof(WCHAR);
        USHORT targetOffset = reparseData->SymbolicLinkReparseBuffer.PrintNameOffset / sizeof(WCHAR);
        target = new wchar_t[targetLength + 1];
        wcsncpy_s(
            target, targetLength + 1, &reparseData->SymbolicLinkReparseBuffer.PathBuffer[targetOffset], targetLength);
        target[targetLength] = L'\0';
    } else if (reparseData->ReparseTag == IO_REPARSE_TAG_MOUNT_POINT) {
        USHORT targetLength = reparseData->MountPointReparseBuffer.PrintNameLength / sizeof(WCHAR);
        USHORT targetOffset = reparseData->MountPointReparseBuffer.PrintNameOffset / sizeof(WCHAR);
        target = new wchar_t[targetLength + 1];
        wcsncpy_s(
            target, targetLength + 1, &reparseData->MountPointReparseBuffer.PathBuffer[targetOffset], targetLength);
        target[targetLength] = L'\0';
    } else {
        // Unsupported reparse point type
        target = new wchar_t[1];
        target[0] = L'\0';
        CreateHandle(v, handle_DeviceIoControl_GetReparsePoint, target, nullptr);
        v->Number = ERROR_NOT_SUPPORTED;
        return v;
    }

    CreateHandle(v, handle_DeviceIoControl_GetReparsePoint, target, nullptr);
    v->Type = TypeT::IsOk;
    return v;
}

// Legacy compatibility wrapper
extern "C" DLL_EXPORT ValueT *win_readlink(const wchar_t *path) {
    return windows_DeviceIoControl_GetReparsePoint(path);
}

/**
 * @brief Handler for win_canonicalize_file_name results - yields canonical
 * path.
 *
 * Converts wide-character canonical path to UTF-8 and yields as string value.
 * Cleans up allocated buffer on completion.
 *
 * @param value Pointer to ValueT with Handle.data1 containing wchar_t*
 * canonical path.
 * @return true on first call if successful, false to signal completion.
 */
static bool handle_GetFinalPathNameByHandleW(ValueT *value) {
    auto               cfn = reinterpret_cast<wchar_t *>(value->Handle.data1);
    static const char *static_name = "path";
    switch (value->Handle.index) {
    case 0:
        if (value->Type == TypeT::IsOk) {
            // Convert wide string to UTF-8
            int size = WideCharToMultiByte(CP_UTF8, 0, cfn, -1, nullptr, 0, nullptr, nullptr);
            if (size > 0) {
                auto utf8 = new char[size];
                WideCharToMultiByte(CP_UTF8, 0, cfn, -1, utf8, size, nullptr, nullptr);
                value->String = utf8;
                value->Name = static_name;  // Use stable static literal
                value->Type = TypeT::IsString;
                return true;
            }
        }
    // Error or conversion failed - fall through
    default:
        delete[] cfn;
        return false;
    }
}

extern "C" DLL_EXPORT ValueT *windows_GetFinalPathNameByHandleW(const wchar_t *path) {
    wchar_t           *canonical = nullptr;
    auto               v = new ValueT();
    static const char *errno_name = "errno";

    // Open the file/directory
    HANDLE hFile = win_open_path(path, 0);
    CreateHandle(v, handle_error, nullptr, nullptr);

    if (hFile == INVALID_HANDLE_VALUE) {
        DWORD err = GetLastError();
        v->Type = TypeT::IsError;
        v->Name = errno_name;  // Use static literal
        v->Number = err;
        return v;
    }

    // Get the final path name
    DWORD bufferSize = GetFinalPathNameByHandleW(hFile, nullptr, 0, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
    if (bufferSize == 0) {
        CreateHandle(v, handle_error, nullptr, nullptr);
        DWORD err = GetLastError();
        CloseHandle(hFile);
        v->Type = TypeT::IsError;
        v->Name = errno_name;  // Use static literal
        v->Number = err;
        return v;
    }

    canonical = new wchar_t[bufferSize];
    DWORD result = GetFinalPathNameByHandleW(hFile, canonical, bufferSize, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
    CloseHandle(hFile);

    if (result == 0 || result >= bufferSize) {
        DWORD err = GetLastError();
        delete[] canonical;
        CreateHandle(v, handle_error, nullptr, nullptr);
        v->Type = TypeT::IsError;
        v->Name = errno_name;  // Use static literal
        v->Number = err;
        return v;
    }

    // Strip \\?\ prefix if present
    wchar_t *finalPath = canonical;
    if (wcsncmp(canonical, L"\\\\?\\", 4) == 0) {
        // Skip the \\?\ prefix
        finalPath = canonical + 4;
        // If it's a UNC path (\\?\UNC\), convert back to \\server\share format
        if (wcsncmp(finalPath, L"UNC\\", 4) == 0) {
            finalPath[0] = L'\\';
            finalPath = finalPath + 2;  // Skip "UN", keep "C\" which becomes "\\"
        }
    }

    // Create a new buffer with the stripped path
    size_t len = wcslen(finalPath);
    auto   strippedPath = new wchar_t[len + 1];
    wcscpy_s(strippedPath, len + 1, finalPath);
    delete[] canonical;

    CreateHandle(v, handle_GetFinalPathNameByHandleW, strippedPath, nullptr);
    v->Type = TypeT::IsOk;
    return v;
}

// Legacy compatibility wrapper
extern "C" DLL_EXPORT ValueT *win_canonicalize_file_name(const wchar_t *path) {
    return windows_GetFinalPathNameByHandleW(path);
}
}  // namespace OsCallsWindows
