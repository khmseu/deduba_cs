/**
 * @file Streams.h
 * @brief Windows Alternate Data Streams (ADS) operations
 *
 * Provides Win32 API wrappers for enumerating and reading NTFS alternate data
 * streams.
 */
#ifndef STREAMS_WINDOWS_H
#define STREAMS_WINDOWS_H

#include "../OsCallsCommonShim/include/ValXfer.h"

namespace OsCallsWindows {

/**
 * @brief List all alternate data streams for a file
 * @param path Wide-character path to file
 * @return ValueT* array with stream names and sizes
 */
extern "C" __declspec(dllexport) OsCalls::ValueT *
win_list_streams(const wchar_t *path);

/**
 * @brief Read content of a specific alternate data stream
 * @param path Wide-character path to file
 * @param stream_name Wide-character stream name
 * @return ValueT* with stream content
 */
extern "C" __declspec(dllexport) OsCalls::ValueT *
win_read_stream(const wchar_t *path, const wchar_t *stream_name);

} // namespace OsCallsWindows

#endif // STREAMS_WINDOWS_H
