/**
 * @file Streams.cpp
 * @brief Windows Alternate Data Streams operations implementation
 *
 * This module implements enumeration and reading of NTFS Alternate Data Streams
 * (ADS). ADS allow storing multiple data streams within a single file, commonly
 * used for:
 * - Security metadata (Zone.Identifier for downloaded file origin)
 * - Resource forks (compatibility with HFS+ when migrating from macOS)
 * - Custom application metadata
 * - Hidden data storage
 *
 * ## ADS Syntax
 *
 * Alternate Data Streams are accessed using colon notation:
 * @code
 * filename:streamname:&#36;DATA
 * @endcode
 *
 * ### Examples
 * - <code>document.txt::&#36;DATA</code> - Default stream (main file content)
 * - <code>document.txt:Author:&#36;DATA</code> - "Author" alternate stream
 * - <code>document.txt:Zone.Identifier:&#36;DATA</code> - Internet Explorer
 * download zone
 *
 * ### Stream Types
 * While DATA is most common, NTFS supports other stream types:
 * - DATA - File data (default and alternates)
 * - `$INDEX_ALLOCATION` - Directory indexes
 * - `$BITMAP` - Allocation bitmaps
 * - `$EA` - Extended attributes
 *
 * Most user-accessible streams are DATA type.
 *
 * ## Common ADS Use Cases
 *
 * ### Zone.Identifier (Security)
 * Windows marks files downloaded from the internet:
 * ```
 * [ZoneTransfer]
 * ZoneId=3
 * ReferrerUrl=https://example.com/download
 * ```
 * - ZoneId=3: Internet Zone (untrusted)
 * - Used by SmartScreen and file blocking warnings
 *
 * ### Thumbnails and Metadata
 * Windows Explorer may cache:
 * - Thumbnail images
 * - Summary information
 * - Custom properties
 *
 * ### Application-Specific Data
 * Applications can store:
 * - Cryptographic signatures
 * - Version information
 * - User annotations
 * - Backup metadata
 *
 * ## API Usage
 *
 * ### FindFirstStreamW / FindNextStreamW
 * Enumerate all streams attached to a file:
 * - FindStreamInfoStandard: Returns WIN32_FIND_STREAM_DATA
 * - cStreamName: Stream name with syntax ::\c DATA or :Name::\c DATA
 * - StreamSize: Size of stream data in bytes
 * - Includes default stream (often largest)
 *
 * ### CreateFileW with Stream Syntax
 * Open a specific stream for reading/writing:
 * @code{.cpp}
 * CreateFileW(L"file.txt:StreamName:&#36;DATA", GENERIC_READ, ...)
 * @endcode
 * - Stream behaves like a separate file
 * - Can be read, written, and sized independently
 * - Deleted when all streams in file are deleted
 *
 * ## Important Notes
 *
 * ### Filesystem Support
 * - **NTFS only** - ADS not supported on FAT32, exFAT, or network shares
 * - Copying to non-NTFS volumes loses alternate streams
 * - Some backup tools may skip ADS unless explicitly configured
 *
 * ### Security Implications
 * - ADS can hide data (not visible in directory listings)
 * - Malware has used ADS for persistence
 * - Antivirus should scan all streams
 * - Windows Defender scans ADS by default
 *
 * ### Performance
 * - Each stream has minimal overhead (metadata)
 * - Stream data is stored in MFT for small streams (<1KB typically)
 * - Larger streams allocated in normal cluster runs
 * - Fragmentation can occur independently per stream
 *
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-findfirststreamw
 * @see https://learn.microsoft.com/en-us/windows/win32/fileio/file-streams
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew
 */
#include "Platform.h"
// Platform.h must come first
#include "Streams.h"
#include <algorithm>
#include <string>
#include <vector>
// WIN32_LEAN_AND_MEAN and NOMINMAX are defined centrally in Platform.h
#include <windows.h>

namespace OsCallsWindows {
using namespace OsCalls;

/**
 * @brief No-op handler for error ValueT structures.
 *
 * Error returns need initialized Handle to prevent AccessViolation when
 * GetNextValue dereferences the handler function pointer. This handler
 * always returns false (no more values) since errors have only errno field.
 *
 * @param value Unused - error ValueT has no iteration.
 * @return false Always (no values to iterate).
 */
static bool handle_error(ValueT *value) {
  (void)value; // Suppress unused parameter warning
  return false;
}

/**
 * @brief Structure to hold stream information
 */
struct StreamInfo {
  std::vector<std::wstring>  names;
  std::vector<LARGE_INTEGER> sizes;
  size_t                     currentIndex;
};

/**
 * @brief Handler for win_list_streams results - iterates through stream list.
 *
 * Yields each stream as a complex value containing "name" and "size" fields.
 * Stream names are converted from wide characters to UTF-8. Cleans up
 * StreamInfo on completion.
 *
 * @param value Pointer to ValueT with Handle.data1 containing StreamInfo*.
 * @return true if more streams remain, false when iteration completes.
 */
static bool handle_FindFirstStreamW(ValueT *value) {
  auto streams = reinterpret_cast<StreamInfo *>(value->Handle.data1);

  if (value->Handle.index == 0) {
    if (value->Type != TypeT::IsOk) {
      // Error case
      delete streams;
      // REMOVED: delete value; - managed code owns ValueT lifetime
      return false;
    }
    // Start iteration - return first stream if available
    if (streams->names.empty()) {
      delete streams;
      // REMOVED: delete value; - managed code owns ValueT lifetime
      return false;
    }
    streams->currentIndex = 0;
  }

  size_t idx = streams->currentIndex;

  if (idx >= streams->names.size()) {
    // End of iteration
    delete streams;
    // REMOVED: delete value; - managed code owns ValueT lifetime
    return false;
  }

  // Return stream info as a complex value (object with name and size)
  // Create a single ValueT for the stream object with its own iterator
  auto streamObj = new ValueT();
  memset(streamObj, 0, sizeof(ValueT));

  // Allocate data to hold name and size
  struct StreamObjectData {
    char   *name;
    int64_t size;
    int     fieldIndex;
  };
  auto data = new StreamObjectData();

  // Convert stream name to UTF-8
  int nameSize = WideCharToMultiByte(CP_UTF8, 0, streams->names[idx].c_str(), -1, nullptr, 0,
                                     nullptr, nullptr);
  data->name = new char[nameSize];
  WideCharToMultiByte(CP_UTF8, 0, streams->names[idx].c_str(), -1, data->name, nameSize, nullptr,
                      nullptr);
  data->size = streams->sizes[idx].QuadPart;
  data->fieldIndex = 0;

  // Create iterator for the stream object fields
  auto handler = [](ValueT *v) -> bool {
    auto sod = reinterpret_cast<StreamObjectData *>(v->Handle.data1);
    switch (v->Handle.index) {
    case 0:
      v->Type = TypeT::IsString;
      v->Name = "name"; // String literal has static storage duration
      v->String = sod->name;
      v->Handle.index++;
      return true;
    case 1:
      v->Type = TypeT::IsNumber;
      v->Name = "size"; // String literal has static storage duration
      v->Number = sod->size;
      v->Handle.index++;
      return true;
    default:
      delete[] sod->name;
      delete sod;
      // REMOVED: delete v; - managed code owns ValueT lifetime
      return false;
    }
  };

  CreateHandle(streamObj, handler, data, nullptr);
  streamObj->Type = TypeT::IsOk;

  value->Type = TypeT::IsComplex;
  value->Name = "[]"; // Array element
  value->Complex = streamObj;

  streams->currentIndex++;
  return true;
}

extern "C" DLL_EXPORT ValueT *windows_FindFirstStreamW(const wchar_t *path) {
  auto               streams = new StreamInfo{};
  auto               v = new ValueT();
  static const char *errno_name = "errno"; // Stable static pointer

  WIN32_FIND_STREAM_DATA findStreamData;
  HANDLE                 hFind = FindFirstStreamW(path, FindStreamInfoStandard, &findStreamData, 0);

  if (hFind == INVALID_HANDLE_VALUE) {
    DWORD err = GetLastError();
    delete streams;
    CreateHandle(v, handle_error, nullptr, nullptr);
    v->Type = TypeT::IsError;
    v->Name = errno_name;
    v->Number = err;
    return v;
  }

  // Enumerate all streams
  do {
    std::wstring streamName = findStreamData.cStreamName;

    // Skip the default ::\c DATA stream (or optionally include it with a flag)
    // For now, include all streams
    streams->names.push_back(streamName);
    streams->sizes.push_back(findStreamData.StreamSize);
  } while (FindNextStreamW(hFind, &findStreamData));

  DWORD lastErr = GetLastError();
  FindClose(hFind);

  // ERROR_HANDLE_EOF is expected at the end of enumeration
  if (lastErr != ERROR_HANDLE_EOF && streams->names.empty()) {
    delete streams;
    CreateHandle(v, handle_error, nullptr, nullptr);
    v->Type = TypeT::IsError;
    v->Name = errno_name;
    v->Number = lastErr;
    return v;
  }

  CreateHandle(v, handle_FindFirstStreamW, streams, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

// Legacy compatibility wrapper
extern "C" DLL_EXPORT ValueT *win_list_streams(const wchar_t *path) {
  return windows_FindFirstStreamW(path);
}

/**
 * @brief Structure to hold stream data
 */
struct StreamData {
  std::vector<BYTE> data;
};

/**
 * @brief Handler for win_read_stream results - yields stream content.
 *
 * Returns the alternate data stream content as a string value.
 * Cleans up StreamData on completion.
 *
 * @param value Pointer to ValueT with Handle.data1 containing StreamData*.
 * @return true on first call if successful, false to signal completion.
 */
static bool handle_ReadFile_Stream(ValueT *value) {
  auto               streamData = reinterpret_cast<StreamData *>(value->Handle.data1);
  static const char *content_name = "content"; // Stable static pointer
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      // Return data as string (or could be bytes)
      // For simplicity, treat as UTF-8 text
      size_t dataSize = streamData->data.size();
      auto   str = new char[dataSize + 1];
      memcpy(str, streamData->data.data(), dataSize);
      str[dataSize] = '\0';
      value->String = str;
      value->Name = content_name; // Use stable static literal
      value->Type = TypeT::IsString;
      return true;
    }
  // Error case - fall through
  default:
    delete streamData;
    // REMOVED: delete value; - managed code owns ValueT lifetime
    return false;
  }
}

extern "C" DLL_EXPORT ValueT *
windows_ReadFile_Stream(const wchar_t *path, const wchar_t *stream_name) {
  auto               streamData = new StreamData{};
  auto               v = new ValueT();
  static const char *errno_name = "errno"; // Stable static pointer

  // Construct full stream path: path:streamname:\c DATA
  std::wstring fullPath = path;
  fullPath += L":";
  fullPath += stream_name;
  if (wcsstr(stream_name, L":$DATA") == nullptr) {
    fullPath += L":$DATA";
  }

  // Open the stream
  HANDLE hFile = CreateFileW(fullPath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                             OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);

  if (hFile == INVALID_HANDLE_VALUE) {
    DWORD err = GetLastError();
    delete streamData;
    CreateHandle(v, handle_error, nullptr, nullptr);
    v->Type = TypeT::IsError;
    v->Name = errno_name;
    v->Number = err;
    return v;
  }

  // Get file size
  LARGE_INTEGER fileSize;
  if (!GetFileSizeEx(hFile, &fileSize)) {
    DWORD err = GetLastError();
    CloseHandle(hFile);
    delete streamData;
    CreateHandle(v, handle_error, nullptr, nullptr);
    v->Type = TypeT::IsError;
    v->Name = errno_name;
    v->Number = err;
    return v;
  }

  // Read stream content (limit to reasonable size)
  const DWORD MAX_STREAM_SIZE = 10 * 1024 * 1024; // 10 MB limit
  DWORD       bytesToRead =
      static_cast<DWORD>(std::min(static_cast<LONGLONG>(MAX_STREAM_SIZE), fileSize.QuadPart));

  streamData->data.resize(bytesToRead);
  DWORD bytesRead = 0;
  if (!ReadFile(hFile, streamData->data.data(), bytesToRead, &bytesRead, nullptr)) {
    DWORD err = GetLastError();
    CloseHandle(hFile);
    delete streamData;
    CreateHandle(v, handle_error, nullptr, nullptr);
    v->Type = TypeT::IsError;
    v->Name = errno_name;
    v->Number = err;
    return v;
  }

  CloseHandle(hFile);
  streamData->data.resize(bytesRead); // Adjust to actual bytes read

  CreateHandle(v, handle_ReadFile_Stream, streamData, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

// Legacy compatibility wrapper
extern "C" DLL_EXPORT ValueT *win_read_stream(const wchar_t *path, const wchar_t *stream_name) {
  return windows_ReadFile_Stream(path, stream_name);
}
} // namespace OsCallsWindows
