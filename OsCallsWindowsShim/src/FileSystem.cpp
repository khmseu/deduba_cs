/**
 * @file FileSystem.cpp
 * @brief Windows filesystem operations implementation
 *
 * TODO: Implement using:
 * - CreateFileW with FILE_FLAG_OPEN_REPARSE_POINT
 * - GetFileInformationByHandleEx (FileBasicInfo, FileIdInfo)
 * - DeviceIoControl with FSCTL_GET_REPARSE_POINT
 * - GetFinalPathNameByHandleW
 */
#include "FileSystem.h"
#include <windows.h>

namespace OsCallsWindows {

using namespace OsCalls;

extern "C" __declspec(dllexport) ValueT *win_lstat(const wchar_t *path) {
  // TODO: Implement using CreateFileW + GetFileInformationByHandleEx
  auto *val = new ValueT{};
  val->Type = TypeT::IsError;
  val->Number = ERROR_CALL_NOT_IMPLEMENTED;
  val->String = "win_lstat not yet implemented";
  return val;
}

extern "C" __declspec(dllexport) ValueT *win_readlink(const wchar_t *path) {
  // TODO: Implement using DeviceIoControl + FSCTL_GET_REPARSE_POINT
  auto *val = new ValueT{};
  val->Type = TypeT::IsError;
  val->Number = ERROR_CALL_NOT_IMPLEMENTED;
  val->String = "win_readlink not yet implemented";
  return val;
}

extern "C" __declspec(dllexport) ValueT *
win_canonicalize_file_name(const wchar_t *path) {
  // TODO: Implement using GetFinalPathNameByHandleW
  auto *val = new ValueT{};
  val->Type = TypeT::IsError;
  val->Number = ERROR_CALL_NOT_IMPLEMENTED;
  val->String = "win_canonicalize_file_name not yet implemented";
  return val;
}

} // namespace OsCallsWindows
