/**
 * @file Security.cpp
 * @brief Windows security descriptor operations implementation
 *
 * Implements Windows security descriptor reading using:
 * - GetNamedSecurityInfoW
 * - ConvertSecurityDescriptorToStringSecurityDescriptorW
 */
#include "Security.h"
// windows.h must be first
#include <windows.h>
// rest of windows headers
#include <sddl.h>
#include <aclapi.h>

namespace OsCallsWindows {

using namespace OsCalls;

/**
 * @brief Handler for win_get_sd results
 */
static bool handle_win_sd(ValueT *value) {
  auto sddl = reinterpret_cast<wchar_t *>(value->Handle.data1);
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      // Convert wide string to UTF-8
      int size = WideCharToMultiByte(CP_UTF8, 0, sddl, -1, nullptr, 0, nullptr, nullptr);
      if (size > 0) {
        auto utf8 = new char[size];
        WideCharToMultiByte(CP_UTF8, 0, sddl, -1, utf8, size, nullptr, nullptr);
        value->String = utf8;
        value->Name = "sddl";
        value->Type = TypeT::IsString;
        return true;
      }
    }
    // Error or conversion failed - fall through
  default:
    if (sddl) {
      LocalFree(sddl); // SDDL strings are allocated by LocalAlloc
    }
    delete value;
    return false;
  }
}

extern "C" __declspec(dllexport) ValueT *win_get_sd(const wchar_t *path,
                                                    bool include_sacl) {
  wchar_t *sddl = nullptr;
  auto v = new ValueT();

  // Determine which security information to retrieve
  SECURITY_INFORMATION secInfo = OWNER_SECURITY_INFORMATION |
                                  GROUP_SECURITY_INFORMATION |
                                  DACL_SECURITY_INFORMATION;
  
  if (include_sacl) {
    secInfo |= SACL_SECURITY_INFORMATION;
  }

  // Get the security descriptor
  PSECURITY_DESCRIPTOR pSD = nullptr;
  DWORD result = GetNamedSecurityInfoW(
      const_cast<wchar_t *>(path),
      SE_FILE_OBJECT,
      secInfo,
      nullptr,  // Owner SID
      nullptr,  // Group SID
      nullptr,  // DACL
      nullptr,  // SACL
      &pSD);

  if (result != ERROR_SUCCESS) {
    // If SACL access was denied, try again without SACL
    if (include_sacl && result == ERROR_PRIVILEGE_NOT_HELD) {
      secInfo &= ~SACL_SECURITY_INFORMATION;
      result = GetNamedSecurityInfoW(
          const_cast<wchar_t *>(path),
          SE_FILE_OBJECT,
          secInfo,
          nullptr,
          nullptr,
          nullptr,
          nullptr,
          &pSD);
    }

    if (result != ERROR_SUCCESS) {
      CreateHandle(v, handle_win_sd, nullptr, nullptr);
      v->Number = result;
      return v;
    }
  }

  // Convert security descriptor to SDDL string
  LPWSTR sddlString = nullptr;
  if (!ConvertSecurityDescriptorToStringSecurityDescriptorW(
          pSD,
          SDDL_REVISION_1,
          secInfo,
          &sddlString,
          nullptr)) {
    DWORD err = GetLastError();
    LocalFree(pSD);
    CreateHandle(v, handle_win_sd, nullptr, nullptr);
    v->Number = err;
    return v;
  }

  // Copy the SDDL string (we need to manage it ourselves)
  size_t len = wcslen(sddlString);
  sddl = reinterpret_cast<wchar_t *>(LocalAlloc(LPTR, (len + 1) * sizeof(wchar_t)));
  wcscpy_s(sddl, len + 1, sddlString);

  // Free the original SDDL string and security descriptor
  LocalFree(sddlString);
  LocalFree(pSD);

  CreateHandle(v, handle_win_sd, sddl, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

} // namespace OsCallsWindows
