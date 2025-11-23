/**
 * @file Streams.h
 * @brief Windows Alternate Data Streams (ADS) operations
 *
 * Provides Win32 API wrappers for enumerating and reading NTFS alternate data
 * streams.
 */
#ifndef STREAMS_WINDOWS_H
#define STREAMS_WINDOWS_H

#include "OcExport.h"
#include "ValXfer.h"

namespace OsCallsWindows {
/**
 * @brief List all alternate data streams for a file
 *
 * Enumerates all NTFS alternate data streams attached to the specified file,
 * including the default ::$DATA stream. Returns stream names and sizes.
 *
 * @param path Wide-character path to file
 * @return ValueT* array with stream names and sizes
 */
extern "C" DLL_EXPORT OsCalls::ValueT *
win_list_streams(const wchar_t *path);

/**
 * @brief Read content of a specific alternate data stream
 *
 * Opens and reads the specified alternate data stream. Automatically appends
 * :$DATA suffix if not present in stream_name. Limits read to 10MB for safety.
 *
 * @param path Wide-character path to file
 * @param stream_name Wide-character stream name (e.g., "Zone.Identifier")
 * @return ValueT* with stream content as string
 */
extern "C" DLL_EXPORT OsCalls::ValueT *
win_read_stream(const wchar_t *path, const wchar_t *stream_name);
} // namespace OsCallsWindows

#endif // STREAMS_WINDOWS_H
