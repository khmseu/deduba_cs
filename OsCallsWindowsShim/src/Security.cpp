/**
 * @file Security.cpp
 * @brief Windows security descriptor operations implementation
 *
 * TODO: Implement using:
 * - GetNamedSecurityInfoW
 * - ConvertSecurityDescriptorToStringSecurityDescriptorW
 */
#include "Security.h"
#include <windows.h>
#include <sddl.h>

namespace OsCallsWindows {

using namespace OsCalls;

extern "C" __declspec(dllexport) ValueT *win_get_sd(const wchar_t *path,
                                                    bool include_sacl) {
  // TODO: Implement using GetNamedSecurityInfoW +
  // ConvertSecurityDescriptorToStringSecurityDescriptor
  auto *val = new ValueT{};
  val->Type = TypeT::IsError;
  val->Number = ERROR_CALL_NOT_IMPLEMENTED;
  val->String = "win_get_sd not yet implemented";
  return val;
}

} // namespace OsCallsWindows
