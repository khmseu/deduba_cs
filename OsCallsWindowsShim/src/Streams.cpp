/**
 * @file Streams.cpp
 * @brief Windows Alternate Data Streams operations implementation
 *
 * TODO: Implement using:
 * - FindFirstStreamW / FindNextStreamW
 * - CreateFileW with stream syntax (path:streamname:$DATA)
 */
#include "Streams.h"
#include <windows.h>

namespace OsCallsWindows {

using namespace OsCalls;

extern "C" __declspec(dllexport) ValueT *win_list_streams(const wchar_t *path) {
  // TODO: Implement using FindFirstStreamW / FindNextStreamW
  auto *val = new ValueT{};
  val->Type = TypeT::IsError;
  val->Number = ERROR_CALL_NOT_IMPLEMENTED;
  val->String = "win_list_streams not yet implemented";
  return val;
}

extern "C" __declspec(dllexport) ValueT *
win_read_stream(const wchar_t *path, const wchar_t *stream_name) {
  // TODO: Implement by opening path:streamname:$DATA
  auto *val = new ValueT{};
  val->Type = TypeT::IsError;
  val->Number = ERROR_CALL_NOT_IMPLEMENTED;
  val->String = "win_read_stream not yet implemented";
  return val;
}

} // namespace OsCallsWindows
