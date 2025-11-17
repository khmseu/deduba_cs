/**
 * @file Streams.cpp
 * @brief Windows Alternate Data Streams operations implementation
 *
 * Implements ADS enumeration and reading using:
 * - FindFirstStreamW / FindNextStreamW
 * - CreateFileW with stream syntax (path:streamname:$DATA)
 */
#include "Streams.h"
#include <windows.h>
#include <vector>
#include <string>
#include <algorithm>

namespace OsCallsWindows {

using namespace OsCalls;

/**
 * @brief Structure to hold stream information
 */
struct StreamInfo {
  std::vector<std::wstring> names;
  std::vector<LARGE_INTEGER> sizes;
  size_t currentIndex;
};

/**
 * @brief Handler for win_list_streams results - iterates through stream list
 */
static bool handle_win_streams(ValueT *value) {
  auto streams = reinterpret_cast<StreamInfo *>(value->Handle.data1);
  
  if (value->Handle.index == 0) {
    if (value->Type != TypeT::IsOk) {
      // Error case
      delete streams;
      delete value;
      return false;
    }
    // Start iteration - return first stream if available
    if (streams->names.empty()) {
      delete streams;
      delete value;
      return false;
    }
    streams->currentIndex = 0;
  }

  size_t idx = streams->currentIndex;
  
  if (idx >= streams->names.size()) {
    // End of iteration
    delete streams;
    delete value;
    return false;
  }

  // Return stream info as a complex value (object with name and size)
  auto streamObj = new ValueT[2]; // Array with 2 elements
  
  // Stream name
  int nameSize = WideCharToMultiByte(CP_UTF8, 0, streams->names[idx].c_str(), -1, 
                                      nullptr, 0, nullptr, nullptr);
  auto nameUtf8 = new char[nameSize];
  WideCharToMultiByte(CP_UTF8, 0, streams->names[idx].c_str(), -1, 
                       nameUtf8, nameSize, nullptr, nullptr);
  
  streamObj[0].Type = TypeT::IsString;
  streamObj[0].Name = "name";
  streamObj[0].String = nameUtf8;
  
  // Stream size
  streamObj[1].Type = TypeT::IsNumber;
  streamObj[1].Name = "size";
  streamObj[1].Number = streams->sizes[idx].QuadPart;
  
  value->Type = TypeT::IsComplex;
  value->Name = nullptr; // Array element
  value->Complex = streamObj;
  
  streams->currentIndex++;
  return true;
}

extern "C" __declspec(dllexport) ValueT *win_list_streams(const wchar_t *path) {
  auto streams = new StreamInfo{};
  auto v = new ValueT();

  WIN32_FIND_STREAM_DATA findStreamData;
  HANDLE hFind = FindFirstStreamW(path, FindStreamInfoStandard, &findStreamData, 0);

  if (hFind == INVALID_HANDLE_VALUE) {
    DWORD err = GetLastError();
    CreateHandle(v, handle_win_streams, streams, nullptr);
    v->Number = err;
    return v;
  }

  // Enumerate all streams
  do {
    std::wstring streamName = findStreamData.cStreamName;
    
    // Skip the default ::$DATA stream (or optionally include it with a flag)
    // For now, include all streams
    streams->names.push_back(streamName);
    streams->sizes.push_back(findStreamData.StreamSize);
    
  } while (FindNextStreamW(hFind, &findStreamData));

  DWORD lastErr = GetLastError();
  FindClose(hFind);

  // ERROR_HANDLE_EOF is expected at the end of enumeration
  if (lastErr != ERROR_HANDLE_EOF && streams->names.empty()) {
    CreateHandle(v, handle_win_streams, streams, nullptr);
    v->Number = lastErr;
    return v;
  }

  CreateHandle(v, handle_win_streams, streams, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

/**
 * @brief Structure to hold stream data
 */
struct StreamData {
  std::vector<BYTE> data;
};

/**
 * @brief Handler for win_read_stream results
 */
static bool handle_win_stream_data(ValueT *value) {
  auto streamData = reinterpret_cast<StreamData *>(value->Handle.data1);
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      // Return data as string (or could be bytes)
      // For simplicity, treat as UTF-8 text
      size_t dataSize = streamData->data.size();
      auto str = new char[dataSize + 1];
      memcpy(str, streamData->data.data(), dataSize);
      str[dataSize] = '\0';
      value->String = str;
      value->Name = "content";
      value->Type = TypeT::IsString;
      return true;
    }
    // Error case - fall through
  default:
    delete streamData;
    delete value;
    return false;
  }
}

extern "C" __declspec(dllexport) ValueT *
win_read_stream(const wchar_t *path, const wchar_t *stream_name) {
  auto streamData = new StreamData{};
  auto v = new ValueT();

  // Construct full stream path: path:streamname:$DATA
  std::wstring fullPath = path;
  fullPath += L":";
  fullPath += stream_name;
  if (wcsstr(stream_name, L":$DATA") == nullptr) {
    fullPath += L":$DATA";
  }

  // Open the stream
  HANDLE hFile = CreateFileW(
      fullPath.c_str(),
      GENERIC_READ,
      FILE_SHARE_READ,
      nullptr,
      OPEN_EXISTING,
      FILE_ATTRIBUTE_NORMAL,
      nullptr);

  if (hFile == INVALID_HANDLE_VALUE) {
    DWORD err = GetLastError();
    CreateHandle(v, handle_win_stream_data, streamData, nullptr);
    v->Number = err;
    return v;
  }

  // Get file size
  LARGE_INTEGER fileSize;
  if (!GetFileSizeEx(hFile, &fileSize)) {
    DWORD err = GetLastError();
    CloseHandle(hFile);
    CreateHandle(v, handle_win_stream_data, streamData, nullptr);
    v->Number = err;
    return v;
  }

  // Read stream content (limit to reasonable size)
  const DWORD MAX_STREAM_SIZE = 10 * 1024 * 1024; // 10 MB limit
  DWORD bytesToRead = static_cast<DWORD>(std::min(static_cast<LONGLONG>(MAX_STREAM_SIZE), fileSize.QuadPart));
  
  streamData->data.resize(bytesToRead);
  DWORD bytesRead = 0;
  if (!ReadFile(hFile, streamData->data.data(), bytesToRead, &bytesRead, nullptr)) {
    DWORD err = GetLastError();
    CloseHandle(hFile);
    CreateHandle(v, handle_win_stream_data, streamData, nullptr);
    v->Number = err;
    return v;
  }

  CloseHandle(hFile);
  streamData->data.resize(bytesRead); // Adjust to actual bytes read

  CreateHandle(v, handle_win_stream_data, streamData, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

} // namespace OsCallsWindows
