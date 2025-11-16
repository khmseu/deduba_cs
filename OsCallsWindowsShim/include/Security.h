/**
 * @file Security.h
 * @brief Windows security descriptor operations
 *
 * Provides Win32 API wrappers for reading security descriptors in SDDL format.
 */
#ifndef SECURITY_WINDOWS_H
#define SECURITY_WINDOWS_H

#include "../OsCallsCommonShim/include/ValXfer.h"

namespace OsCallsWindows {

/**
 * @brief Get security descriptor in SDDL format
 * @param path Wide-character path to file
 * @param include_sacl Whether to include SACL (requires SeSecurityPrivilege)
 * @return ValueT* with SDDL string
 */
extern "C" __declspec(dllexport) OsCalls::ValueT *
win_get_sd(const wchar_t *path, bool include_sacl);

} // namespace OsCallsWindows

#endif // SECURITY_WINDOWS_H
