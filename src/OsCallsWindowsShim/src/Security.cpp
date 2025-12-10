/**
 * @file Security.cpp
 * @brief Windows security descriptor operations implementation
 *
 * This module reads Windows security descriptors and converts them to
 * Security Descriptor Definition Language (SDDL) format for portable
 * storage and cross-platform understanding.
 *
 * ## SDDL Format Overview
 *
 * SDDL strings represent Windows security descriptors in a compact text format:
 *
 * ```
 * O:owner_sid G:group_sid D:dacl_flags(ace1)(ace2)... S:sacl_flags(ace1)...
 * ```
 *
 * ### Components
 *
 * - **O:** Owner SID (e.g., `O:S-1-5-21-...` or `O:BA` for Built-in
 * Administrators)
 * - **G:** Group SID (e.g., `G:DU` for Domain Users)
 * - **D:** DACL (Discretionary Access Control List) with ACEs
 * - **S:** SACL (System Access Control List) for auditing - requires privilege
 *
 * ### DACL Flags
 * - **P** - Protected (inheritance blocked)
 * - **AI** - Auto-inherited
 * - **AR** - Auto-inherit requested
 *
 * ### ACE Format
 * Each ACE (Access Control Entry) follows this pattern:
 * ```
 * (ace_type;ace_flags;rights;object_guid;inherit_object_guid;account_sid)
 * ```
 *
 * **ACE Types:**
 * - **A** - Access allowed
 * - **D** - Access denied
 * - **AU** - System audit
 * - **AL** - System alarm (rarely used)
 *
 * **Common Rights:**
 * - **FA** - File all access (GENERIC_ALL)
 * - **FR** - File generic read
 * - **FW** - File generic write
 * - **FX** - File generic execute
 * - **GA** - Generic all
 * - **GR** - Generic read
 * - **GW** - Generic write
 * - **GX** - Generic execute
 *
 * **Account SIDs (Well-Known):**
 * - **BA** - Built-in Administrators
 * - **BU** - Built-in Users
 * - **WD** - Everyone (World)
 * - **CO** - Creator Owner
 * - **CG** - Creator Group
 * - **SY** - Local System
 *
 * ### Example SDDL String
 *
 * ```
 * O:BAG:DUD:PAI(A;;FA;;;BA)(A;;FR;;;BU)
 * ```
 *
 * Translation:
 * - Owner: Built-in Administrators (BA)
 * - Group: Domain Users (DU)
 * - DACL: Protected (P) + Auto-inherited (AI)
 *   - Allow (A) Administrators (BA) Full Access (FA)
 *   - Allow (A) Users (BU) Read Access (FR)
 *
 * ## API Usage
 *
 * ### GetNamedSecurityInfoW
 * Retrieves security descriptor from a named object (file/directory):
 * - SE_FILE_OBJECT: Type of object being queried
 * - Security info flags: OWNER | GROUP | DACL | SACL
 * - Returns PSECURITY_DESCRIPTOR structure
 *
 * ### ConvertSecurityDescriptorToStringSecurityDescriptorW
 * Converts binary security descriptor to SDDL string:
 * - SDDL_REVISION_1: Current revision level
 * - Returns LocalAlloc'd wide string (must be freed)
 *
 * ## SACL Privilege Requirements
 *
 * Reading SACL requires SeSecurityPrivilege:
 * - Administrator accounts typically have this
 * - Regular users will get ERROR_PRIVILEGE_NOT_HELD (1314)
 * - Implementation gracefully downgrades to DACL-only if privilege denied
 *
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/secauthz/security-descriptor-string-format
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/aclapi/nf-aclapi-getnamedsecurityinfow
 * @see
 * https://learn.microsoft.com/en-us/windows/win32/api/sddl/nf-sddl-convertsecuritydescriptortostringsecuritydescriptorw
 */
#include "Platform.h"
// Platform.h must come first
#include "Security.h"
// windows.h must be first
// WIN32_LEAN_AND_MEAN and NOMINMAX are defined centrally in Platform.h
#include <windows.h>
// rest of windows headers
#include <aclapi.h>
#include <sddl.h>

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
  (void)value;  // Suppress unused parameter warning
  return false;
}

/**
 * @brief Handler for win_get_sd results - yields SDDL string.
 *
 * Converts wide-character SDDL string to UTF-8 and yields as string value.
 * Uses LocalFree to release the SDDL string allocated by Windows API.
 *
 * @param value Pointer to ValueT with Handle.data1 containing wchar_t* SDDL
 * string.
 * @return true on first call if successful, false to signal completion.
 */
static bool handle_GetNamedSecurityInfoW(ValueT *value) {
  auto               sddl = reinterpret_cast<wchar_t *>(value->Handle.data1);
  static const char *sddl_name = "sddl";  // Stable static pointer
  switch (value->Handle.index) {
  case 0:
    if (value->Type == TypeT::IsOk) {
      // Convert wide string to UTF-8
      int size = WideCharToMultiByte(CP_UTF8, 0, sddl, -1, nullptr, 0, nullptr, nullptr);
      if (size > 0) {
        auto utf8 = new char[size];
        WideCharToMultiByte(CP_UTF8, 0, sddl, -1, utf8, size, nullptr, nullptr);
        value->String = utf8;
        value->Name = sddl_name;  // Use stable static literal
        value->Type = TypeT::IsString;
        return true;
      }
    }
  // Error or conversion failed - fall through
  default:
    if (sddl) {
      LocalFree(sddl);  // SDDL strings are allocated by LocalAlloc
    }
    // REMOVED: delete value; - managed code owns ValueT lifetime
    return false;
  }
}

extern "C" DLL_EXPORT ValueT *
windows_GetNamedSecurityInfoW(const wchar_t *path, bool include_sacl) {
  wchar_t           *sddl = nullptr;
  auto               v = new ValueT();
  static const char *errno_name = "errno";  // Stable static pointer

  // Determine which security information to retrieve
  SECURITY_INFORMATION secInfo = OWNER_SECURITY_INFORMATION | GROUP_SECURITY_INFORMATION |
                                 DACL_SECURITY_INFORMATION;

  if (include_sacl) {
    secInfo |= SACL_SECURITY_INFORMATION;
  }

  // Get the security descriptor
  PSECURITY_DESCRIPTOR pSD = nullptr;
  DWORD                result = GetNamedSecurityInfoW(const_cast<wchar_t *>(path),
                                       SE_FILE_OBJECT,
                                       secInfo,
                                       nullptr,
                                       // Owner SID
                                       nullptr,
                                       // Group SID
                                       nullptr,
                                       // DACL
                                       nullptr,
                                       // SACL
                                       &pSD);

  if (result != ERROR_SUCCESS) {
    // If SACL access was denied, try again without SACL
    if (include_sacl && result == ERROR_PRIVILEGE_NOT_HELD) {
      secInfo &= ~SACL_SECURITY_INFORMATION;
      result = GetNamedSecurityInfoW(const_cast<wchar_t *>(path),
                                     SE_FILE_OBJECT,
                                     secInfo,
                                     nullptr,
                                     nullptr,
                                     nullptr,
                                     nullptr,
                                     &pSD);
    }

    if (result != ERROR_SUCCESS) {
      CreateHandle(v, handle_error, nullptr, nullptr);
      v->Type = TypeT::IsError;
      v->Name = errno_name;
      v->Number = result;
      return v;
    }
  }

  // Convert security descriptor to SDDL string
  LPWSTR sddlString = nullptr;
  if (!ConvertSecurityDescriptorToStringSecurityDescriptorW(
          pSD, SDDL_REVISION_1, secInfo, &sddlString, nullptr)) {
    DWORD err = GetLastError();
    LocalFree(pSD);
    CreateHandle(v, handle_error, nullptr, nullptr);
    v->Type = TypeT::IsError;
    v->Name = errno_name;
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

  CreateHandle(v, handle_GetNamedSecurityInfoW, sddl, nullptr);
  v->Type = TypeT::IsOk;
  return v;
}

// Legacy compatibility wrapper
extern "C" DLL_EXPORT ValueT *win_get_sd(const wchar_t *path, bool include_sacl) {
  return windows_GetNamedSecurityInfoW(path, include_sacl);
}
}  // namespace OsCallsWindows
