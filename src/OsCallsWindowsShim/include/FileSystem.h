/**
 * @file FileSystem.h
 * @brief Windows filesystem operations for backup
 *
 * Provides Win32 API wrappers for file status, reparse points, and canonical
 * paths.
 */
#ifndef FILESYSTEM_WINDOWS_H
#define FILESYSTEM_WINDOWS_H

#include "Platform.h"
#include "ValXfer.h"

namespace OsCallsWindows {
// ============================================================================
// Windows API-named exports (primary implementations)
// ============================================================================

/**
 * @brief Get file information using CreateFileW + GetFileInformationByHandle
 *
 * Opens the file with FILE_FLAG_OPEN_REPARSE_POINT to avoid following symlinks,
 * then retrieves file metadata including attributes, size, timestamps, file ID
 * (inode equivalent), volume serial (device equivalent), and link count.
 *
 * @param path Wide-character path to file
 * @return ValueT* with file attributes, timestamps, size, file ID
 */
extern "C" DLL_EXPORT OsCalls::ValueT *windows_GetFileInformationByHandle(const wchar_t *path);

/**
 * @brief Read reparse point data using DeviceIoControl FSCTL_GET_REPARSE_POINT
 *
 * Opens the reparse point with FILE_FLAG_OPEN_REPARSE_POINT, then uses
 * DeviceIoControl with FSCTL_GET_REPARSE_POINT to retrieve the target path.
 * Handles both symbolic links and junction points (mount points).
 *
 * @param path Wide-character path to reparse point
 * @return ValueT* with reparse type and target path
 */
extern "C" DLL_EXPORT OsCalls::ValueT *windows_DeviceIoControl_GetReparsePoint(const wchar_t *path);

/**
 * @brief Resolve path to canonical form using GetFinalPathNameByHandleW
 *
 * Opens the file/directory, calls GetFinalPathNameByHandleW with
 * FILE_NAME_NORMALIZED | VOLUME_NAME_DOS flags to get canonical path,
 * then strips \\\\?\\ prefix if present.
 *
 * @param path Wide-character path to resolve
 * @return ValueT* with canonical path string
 */
extern "C" DLL_EXPORT OsCalls::ValueT *windows_GetFinalPathNameByHandleW(const wchar_t *path);

// ============================================================================
// Legacy compatibility wrappers (forward to windows_* functions)
// ============================================================================

/**
 * @brief Get file status without following reparse points (like lstat)
 *
 * Legacy wrapper that forwards to windows_GetFileInformationByHandle.
 * Provided for backward compatibility.
 *
 * @param path Wide-character path to file
 * @return ValueT* with file attributes, timestamps, size, file ID
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_lstat(const wchar_t *path);

/**
 * @brief Read reparse point target (symlink/junction/mount point)
 *
 * Legacy wrapper that forwards to windows_DeviceIoControl_GetReparsePoint.
 * Provided for backward compatibility.
 *
 * @param path Wide-character path to reparse point
 * @return ValueT* with reparse type and target path
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_readlink(const wchar_t *path);

/**
 * @brief Canonicalize file path using GetFinalPathNameByHandle
 *
 * Legacy wrapper that forwards to windows_GetFinalPathNameByHandleW.
 * Provided for backward compatibility.
 *
 * @param path Wide-character path to resolve
 * @return ValueT* with canonical path string
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_canonicalize_file_name(const wchar_t *path);
} // namespace OsCallsWindows

#endif // FILESYSTEM_WINDOWS_H
