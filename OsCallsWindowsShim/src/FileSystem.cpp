/**
 * @file FileSystem.cpp
 * @brief Windows filesystem operations implementation
 *
 * Implements Windows-specific filesystem operations using Win32 APIs:
 * - CreateFileW with FILE_FLAG_OPEN_REPARSE_POINT
 * - GetFileInformationByHandleEx (FileBasicInfo, FileIdInfo)
 * - DeviceIoControl with FSCTL_GET_REPARSE_POINT
 * - GetFinalPathNameByHandleW
 */
#include "FileSystem.h"
#include <windows.h>
#include <winioctl.h>
#include <cstring>
#include <vector>

// Define REPARSE_DATA_BUFFER if not available in MinGW headers
#ifndef REPARSE_DATA_BUFFER_HEADER_SIZE
typedef struct _REPARSE_DATA_BUFFER {
  ULONG ReparseTag;
  USHORT ReparseDataLength;
  USHORT Reserved;
  union {
    struct {
      USHORT SubstituteNameOffset;
      USHORT SubstituteNameLength;
      USHORT PrintNameOffset;
      USHORT PrintNameLength;
      ULONG Flags;
      WCHAR PathBuffer[1];
    } SymbolicLinkReparseBuffer;
    struct {
      USHORT SubstituteNameOffset;
      USHORT SubstituteNameLength;
      USHORT PrintNameOffset;
      USHORT PrintNameLength;
      WCHAR PathBuffer[1];
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

#ifndef FIELD_OFFSET
#define FIELD_OFFSET(type, field) ((LONG)(LONG_PTR)&(((type *)0)->field))
#endif

namespace OsCallsWindows {

using namespace OsCalls;

// Helper structure to hold file information
struct WinFileInfo {
  DWORD fileAttributes;
  LARGE_INTEGER fileSize;
  FILETIME creationTime;
  FILETIME lastAccessTime;
  FILETIME lastWriteTime;
  LARGE_INTEGER fileIndex; // File ID (inode equivalent)
  DWORD volumeSerialNumber; // Device equivalent
  DWORD numberOfLinks;
  DWORD reparseTag;
};

/**
 * @brief Convert Windows file attributes to POSIX-like mode bits
 */
static DWORD win_attrs_to_mode(DWORD attrs, DWORD reparseTag) {
  DWORD mode = 0;

  // File type bits (high nibble)
  if (attrs & FILE_ATTRIBUTE_REPARSE_POINT) {
    if (reparseTag == IO_REPARSE_TAG_SYMLINK) {
      mode |= 0120000; // S_IFLNK
    } else if (reparseTag == IO_REPARSE_TAG_MOUNT_POINT) {
      mode |= 0120000; // Junction treated as symlink
    } else {
      mode |= 0100000; // S_IFREG (other reparse points as regular)
    }
  } else if (attrs & FILE_ATTRIBUTE_DIRECTORY) {
    mode |= 0040000; // S_IFDIR
  } else {
    mode |= 0100000; // S_IFREG
  }

  // Permission bits (simplified - Windows doesn't have direct equivalents)
  // Owner: always read, write if not readonly, execute if directory or .exe
  mode |= 0000400; // S_IRUSR
  if (!(attrs & FILE_ATTRIBUTE_READONLY)) {
    mode |= 0000200; // S_IWUSR
  }
  if ((attrs & FILE_ATTRIBUTE_DIRECTORY)) {
    mode |= 0000100; // S_IXUSR for directories
  }

  // Group and other: inherit from owner (simplified)
  mode |= (mode & 0000700) >> 3; // Group
  mode |= (mode & 0000700) >> 6; // Other

  return mode;
}

/**
 * @brief Convert FILETIME to timespec (100-nanosecond intervals since 1601-01-01)
 */
static timespec filetime_to_timespec(const FILETIME &ft) {
  // FILETIME is 100-nanosecond intervals since 1601-01-01
  // Unix epoch is 1970-01-01, difference is 11644473600 seconds
  ULARGE_INTEGER ull;
  ull.LowPart = ft.dwLowDateTime;
  ull.HighPart = ft.dwHighDateTime;

  // Convert to seconds and nanoseconds
  const uint64_t EPOCH_DIFF = 11644473600ULL;
  uint64_t total_100ns = ull.QuadPart;
  uint64_t seconds = total_100ns / 10000000ULL - EPOCH_DIFF;
  uint64_t nanoseconds = (total_100ns % 10000000ULL) * 100;

  timespec ts;
  ts.tv_sec = static_cast<time_t>(seconds);
  ts.tv_nsec = static_cast<long>(nanoseconds);
  return ts;
}

/**
 * @brief Handler for win_lstat results - iterates through file metadata fields
 */
static bool handle_win_lstat(ValueT *value) {
  auto info = reinterpret_cast<WinFileInfo *>(value->Handle.data1);
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      set_val(Number, "st_dev", info->volumeSerialNumber);
      return true;
    }
    // Error case - fall through to cleanup
  default:
    delete info;
    delete value;
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
    set_val(Boolean, "S_ISBLK", false); // Windows doesn't have block devices
    return true;
  case 4:
    set_val(Boolean, "S_ISCHR", false); // Windows doesn't expose char devices this way
    return true;
  case 5:
    set_val(Boolean, "S_ISDIR", (info->fileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0);
    return true;
  case 6:
    set_val(Boolean, "S_ISFIFO", false); // Windows doesn't have FIFOs
    return true;
  case 7: {
    bool is_symlink = (info->fileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) &&
                      (info->reparseTag == IO_REPARSE_TAG_SYMLINK ||
                       info->reparseTag == IO_REPARSE_TAG_MOUNT_POINT);
    set_val(Boolean, "S_ISLNK", is_symlink);
    return true;
  }
  case 8: {
    bool is_regular = !(info->fileAttributes & FILE_ATTRIBUTE_DIRECTORY) &&
                      !((info->fileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) &&
                        (info->reparseTag == IO_REPARSE_TAG_SYMLINK ||
                         info->reparseTag == IO_REPARSE_TAG_MOUNT_POINT));
    set_val(Boolean, "S_ISREG", is_regular);
    return true;
  }
  case 9:
    set_val(Boolean, "S_ISSOCK", false); // Windows doesn't have Unix sockets as files
    return true;
  case 10:
    set_val(Boolean, "S_TYPEISMQ", false); // Message queues not exposed as files
    return true;
  case 11:
    set_val(Boolean, "S_TYPEISSEM", false); // Semaphores not exposed as files
    return true;
  case 12:
    set_val(Boolean, "S_TYPEISSHM", false); // Shared memory not exposed as files
    return true;
  case 13:
    set_val(Boolean, "S_TYPEISTMO", false); // Typed memory objects not supported
    return true;
  case 14:
    set_val(Number, "st_nlink", info->numberOfLinks);
    return true;
  case 15:
    set_val(Number, "st_uid", 0); // Windows doesn't have Unix UIDs
    return true;
  case 16:
    set_val(Number, "st_gid", 0); // Windows doesn't have Unix GIDs
    return true;
  case 17:
    set_val(Number, "st_rdev", 0); // Device files not supported
    return true;
  case 18:
    set_val(Number, "st_size", info->fileSize.QuadPart);
    return true;
  case 19: {
    timespec ts = filetime_to_timespec(info->lastAccessTime);
    set_val(TimeSpec, "st_atim", ts);
    return true;
  }
  case 20: {
    timespec ts = filetime_to_timespec(info->lastWriteTime);
    set_val(TimeSpec, "st_mtim", ts);
    return true;
  }
  case 21: {
    timespec ts = filetime_to_timespec(info->creationTime);
    set_val(TimeSpec, "st_ctim", ts); // Using creation time as ctime
    return true;
  }
  case 22:
    set_val(Number, "st_blksize", 4096); // Default block size
    return true;
  case 23: {
    // Calculate blocks (512-byte units)
    int64_t blocks = (info->fileSize.QuadPart + 511) / 512;
    set_val(Number, "st_blocks", blocks);
    return true;
  }
  }
}

extern "C" __declspec(dllexport) ValueT *win_lstat(const wchar_t *path) {
  auto info = new WinFileInfo{};
  auto v = new ValueT();

  // Open file/directory without following reparse points
  HANDLE hFile = CreateFileW(
      path,
      0, // No access needed for metadata
      FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
      nullptr,
      OPEN_EXISTING,
      FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
      nullptr);

  if (hFile == INVALID_HANDLE_VALUE) {
    DWORD err = GetLastError();
    CreateHandle(v, handle_win_lstat, info, nullptr);
    v->Number = err;
    return v;
  }

  // Get basic file information
  BY_HANDLE_FILE_INFORMATION fileInfo;
  if (!GetFileInformationByHandle(hFile, &fileInfo)) {
    DWORD err = GetLastError();
    CloseHandle(hFile);
    CreateHandle(v, handle_win_lstat, info, nullptr);
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
    BYTE buffer[MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
    DWORD bytesReturned;
    if (DeviceIoControl(hFile, FSCTL_GET_REPARSE_POINT, nullptr, 0,
                        buffer, sizeof(buffer), &bytesReturned, nullptr)) {
      auto reparseData = reinterpret_cast<REPARSE_DATA_BUFFER *>(buffer);
      info->reparseTag = reparseData->ReparseTag;
    }
  }

  CloseHandle(hFile);

  CreateHandle(v, handle_win_lstat, info, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

/**
 * @brief Handler for win_readlink results
 */
static bool handle_win_readlink(ValueT *value) {
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
    delete value;
    return false;
  }
}

extern "C" __declspec(dllexport) ValueT *win_readlink(const wchar_t *path) {
  wchar_t *target = nullptr;
  auto v = new ValueT();

  // Open the reparse point
  HANDLE hFile = CreateFileW(
      path,
      0, // No access needed
      FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
      nullptr,
      OPEN_EXISTING,
      FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
      nullptr);

  if (hFile == INVALID_HANDLE_VALUE) {
    DWORD err = GetLastError();
    target = new wchar_t[1];
    target[0] = L'\0';
    CreateHandle(v, handle_win_readlink, target, nullptr);
    v->Number = err;
    return v;
  }

  // Get reparse point data
  BYTE buffer[MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
  DWORD bytesReturned;
  if (!DeviceIoControl(hFile, FSCTL_GET_REPARSE_POINT, nullptr, 0,
                       buffer, sizeof(buffer), &bytesReturned, nullptr)) {
    DWORD err = GetLastError();
    CloseHandle(hFile);
    target = new wchar_t[1];
    target[0] = L'\0';
    CreateHandle(v, handle_win_readlink, target, nullptr);
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
    wcsncpy_s(target, targetLength + 1,
              &reparseData->SymbolicLinkReparseBuffer.PathBuffer[targetOffset],
              targetLength);
    target[targetLength] = L'\0';
  } else if (reparseData->ReparseTag == IO_REPARSE_TAG_MOUNT_POINT) {
    USHORT targetLength = reparseData->MountPointReparseBuffer.PrintNameLength / sizeof(WCHAR);
    USHORT targetOffset = reparseData->MountPointReparseBuffer.PrintNameOffset / sizeof(WCHAR);
    target = new wchar_t[targetLength + 1];
    wcsncpy_s(target, targetLength + 1,
              &reparseData->MountPointReparseBuffer.PathBuffer[targetOffset],
              targetLength);
    target[targetLength] = L'\0';
  } else {
    // Unsupported reparse point type
    target = new wchar_t[1];
    target[0] = L'\0';
    CreateHandle(v, handle_win_readlink, target, nullptr);
    v->Number = ERROR_NOT_SUPPORTED;
    return v;
  }

  CreateHandle(v, handle_win_readlink, target, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

/**
 * @brief Handler for win_canonicalize_file_name results
 */
static bool handle_win_cfn(ValueT *value) {
  auto cfn = reinterpret_cast<wchar_t *>(value->Handle.data1);
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      // Convert wide string to UTF-8
      int size = WideCharToMultiByte(CP_UTF8, 0, cfn, -1, nullptr, 0, nullptr, nullptr);
      if (size > 0) {
        auto utf8 = new char[size];
        WideCharToMultiByte(CP_UTF8, 0, cfn, -1, utf8, size, nullptr, nullptr);
        value->String = utf8;
        value->Name = "path";
        value->Type = TypeT::IsString;
        return true;
      }
    }
    // Error or conversion failed - fall through
  default:
    delete[] cfn;
    delete value;
    return false;
  }
}

extern "C" __declspec(dllexport) ValueT *
win_canonicalize_file_name(const wchar_t *path) {
  wchar_t *canonical = nullptr;
  auto v = new ValueT();

  // Open the file/directory
  HANDLE hFile = CreateFileW(
      path,
      0, // No access needed
      FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
      nullptr,
      OPEN_EXISTING,
      FILE_FLAG_BACKUP_SEMANTICS, // Follow reparse points for canonical path
      nullptr);

  if (hFile == INVALID_HANDLE_VALUE) {
    DWORD err = GetLastError();
    canonical = new wchar_t[1];
    canonical[0] = L'\0';
    CreateHandle(v, handle_win_cfn, canonical, nullptr);
    v->Number = err;
    return v;
  }

  // Get the final path name
  DWORD bufferSize = GetFinalPathNameByHandleW(hFile, nullptr, 0, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
  if (bufferSize == 0) {
    DWORD err = GetLastError();
    CloseHandle(hFile);
    canonical = new wchar_t[1];
    canonical[0] = L'\0';
    CreateHandle(v, handle_win_cfn, canonical, nullptr);
    v->Number = err;
    return v;
  }

  canonical = new wchar_t[bufferSize];
  DWORD result = GetFinalPathNameByHandleW(hFile, canonical, bufferSize, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
  CloseHandle(hFile);

  if (result == 0 || result >= bufferSize) {
    DWORD err = GetLastError();
    delete[] canonical;
    canonical = new wchar_t[1];
    canonical[0] = L'\0';
    CreateHandle(v, handle_win_cfn, canonical, nullptr);
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
      finalPath = finalPath + 2; // Skip "UN", keep "C\" which becomes "\\"
    }
  }

  // Create a new buffer with the stripped path
  size_t len = wcslen(finalPath);
  auto strippedPath = new wchar_t[len + 1];
  wcscpy_s(strippedPath, len + 1, finalPath);
  delete[] canonical;

  CreateHandle(v, handle_win_cfn, strippedPath, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

} // namespace OsCallsWindows
