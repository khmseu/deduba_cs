/**
 * @file Security.h
 * @brief Windows security descriptor operations
 *
 * Provides Win32 API wrappers for reading security descriptors in SDDL format.
 */
#ifndef SECURITY_WINDOWS_H
#define SECURITY_WINDOWS_H

#include "Platform.h"
#include "ValXfer.h"

namespace OsCallsWindows {
// ============================================================================
// Windows API-named exports (primary implementations)
// ============================================================================

/**
 * @brief Get security descriptor using GetNamedSecurityInfoW and convert to SDDL
 *
 * Calls GetNamedSecurityInfoW to retrieve security descriptor (owner, group,
 * DACL, optional SACL), then ConvertSecurityDescriptorToStringSecurityDescriptorW
 * to convert to SDDL string format for portable storage and analysis.
 * If include_sacl is true but SeSecurityPrivilege is not held, gracefully
 * downgrades to DACL-only retrieval.
 *
 * @param path Wide-character path to file
 * @param include_sacl Whether to include SACL (requires SeSecurityPrivilege)
 * @return ValueT* with SDDL string
 */
extern "C" DLL_EXPORT OsCalls::ValueT                       *
windows_GetNamedSecurityInfoW(const wchar_t *path, bool include_sacl);

// ============================================================================
// Legacy compatibility wrappers (forward to windows_* functions)
// ============================================================================

/**
 * @brief Get security descriptor in SDDL format
 *
 * Legacy wrapper that forwards to windows_GetNamedSecurityInfoW.
 * Provided for backward compatibility.
 *
 * @param path Wide-character path to file
 * @param include_sacl Whether to include SACL (requires SeSecurityPrivilege)
 * @return ValueT* with SDDL string
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_get_sd(const wchar_t *path, bool include_sacl);
} // namespace OsCallsWindows

#endif // SECURITY_WINDOWS_H
