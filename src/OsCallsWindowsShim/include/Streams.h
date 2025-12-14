/**
 * @file Streams.h
 * @brief Windows Alternate Data Streams (ADS) operations
 *
 * Provides Win32 API wrappers for enumerating and reading NTFS alternate data
 * streams.
 */
#ifndef STREAMS_WINDOWS_H
#define STREAMS_WINDOWS_H

#include "Platform.h"
#include "ValXfer.h"

namespace OsCallsWindows {
// ============================================================================
// Windows API-named exports (primary implementations)
// ============================================================================

/**
 * @brief Enumerate alternate data streams using FindFirstStreamW + FindNextStreamW
 *
 * Calls FindFirstStreamW to start enumeration, then FindNextStreamW to iterate
 * through all NTFS alternate data streams attached to the specified file,
 * including the default data stream. Returns stream names and sizes.
 *
 * @param path Wide-character path to file
 * @return ValueT* array with stream names and sizes
 */
extern "C" DLL_EXPORT OsCalls::ValueT *windows_FindFirstStreamW(const wchar_t *path);

/**
 * @brief Read alternate data stream content using CreateFileW + ReadFile
 *
 * Opens the specified alternate data stream using CreateFileW with stream
 * syntax (path:streamname:$DATA), reads the content using ReadFile.
 * Automatically appends :$DATA suffix if not present in stream_name.
 * Limits read to 10MB for safety.
 *
 * @param path Wide-character path to file
 * @param stream_name Wide-character stream name (e.g., "Zone.Identifier")
 * @return ValueT* with stream content as string
 */
extern "C" DLL_EXPORT OsCalls::ValueT *windows_ReadFile_Stream(const wchar_t *path, const wchar_t *stream_name);

// ============================================================================
// Legacy compatibility wrappers (forward to windows_* functions)
// ============================================================================

/**
 * @brief List all alternate data streams for a file
 *
 * Legacy wrapper that forwards to windows_FindFirstStreamW.
 * Provided for backward compatibility.
 *
 * @param path Wide-character path to file
 * @return ValueT* array with stream names and sizes
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_list_streams(const wchar_t *path);

/**
 * @brief Read content of a specific alternate data stream
 *
 * Legacy wrapper that forwards to windows_ReadFile_Stream.
 * Provided for backward compatibility.
 *
 * @param path Wide-character path to file
 * @param stream_name Wide-character stream name (e.g., "Zone.Identifier")
 * @return ValueT* with stream content as string
 */
extern "C" DLL_EXPORT OsCalls::ValueT *win_read_stream(const wchar_t *path, const wchar_t *stream_name);
}  // namespace OsCallsWindows

#endif  // STREAMS_WINDOWS_H
