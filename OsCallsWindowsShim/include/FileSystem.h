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
/**
 * @brief Get file status without following reparse points (like lstat)
 *
 * Returns file metadata including attributes, size, timestamps, file ID
 * (inode equivalent), volume serial (device equivalent), and link count.
 * Compatible with POSIX stat structure layout for cross-platform code.
 *
 * @param path Wide-character path to file
 * @return ValueT* with file attributes, timestamps, size, file ID
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_lstat(const wchar_t *path);

/**
 * @brief Read reparse point target (symlink/junction/mount point)
 *
 * Retrieves the target path from a reparse point without following it.
 * Handles both symbolic links and junction points (mount points).
 *
 * @param path Wide-character path to reparse point
 * @return ValueT* with reparse type and target path
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_readlink(const wchar_t *path);

/**
 * @brief Canonicalize file path using GetFinalPathNameByHandle
 *
 * Resolves the path to its canonical absolute form, following reparse
 * points and normalizing path components. Strips \\\\?\\ prefix if present.
 *
 * @param path Wide-character path to resolve
 * @return ValueT* with canonical path string
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_canonicalize_file_name(const wchar_t *path);
} // namespace OsCallsWindows

#endif // FILESYSTEM_WINDOWS_H
