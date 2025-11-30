#ifndef PLATFORM_H
#define PLATFORM_H

/*
 * Platform.h
 * Cross-platform DLL_EXPORT macro used by the native shim projects.
 * This header should be included first in each .cpp file, before other
 * includes. It uses CMake's generate_export_header to properly handle
 * DLL exports on Windows and visibility on other platforms.
 *
 * When generating Doxygen docs (or when compiling for documentation), define
 * DOXYGEN or set PREDEFINED in Doxygen to avoid Doxygen confusing attributes.
 */

// Use the CMake-generated export header
#include "OsCallsCommonShim_export.h"

#if defined(DOXYGEN)
#define DLL_EXPORT
#else
/*
 * If we are building OsCallsCommonShim target, OSCALLS_COMMON_SHIM_EXPORT
 * expands to dllexport (Windows) or default visibility (ELF). When building
 * other shim targets (OsCallsWindowsShim / OsCallsLinuxShim), we still need
 * to export their own extern "C" functions. Those targets will NOT have
 * OsCallsCommonShim_EXPORTS defined, making OSCALLS_COMMON_SHIM_EXPORT
 * expand to dllimport on Windows. To avoid erroneous dllimport on function
 * definitions in those other libs, fall back to explicit export when not
 * building the common shim.
 */
#if defined(OsCallsCommonShim_EXPORTS)
#define DLL_EXPORT OSCALLS_COMMON_SHIM_EXPORT
#else
#if defined(_WIN32) || defined(_WIN64)
#define DLL_EXPORT __declspec(dllexport)
#else
#define DLL_EXPORT __attribute__((visibility("default")))
#endif
#endif
#endif

/*
 * Non-export Windows-specific defines. These must be available regardless of
 * whether DOXYGEN is defined (the DOXYGEN guard above only controls the
 * export symbol used for function declarations in docs).
 */
#if defined(_WIN32) || defined(_WIN64) || defined(__MINGW32__) || defined(__MINGW64__)
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif
#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef UNICODE
#define UNICODE
#endif
#ifndef _UNICODE
#define _UNICODE
#endif
#ifndef _CRT_SECURE_NO_WARNINGS
#define _CRT_SECURE_NO_WARNINGS
#endif
#ifndef FIELD_OFFSET
#define FIELD_OFFSET(type, field) ((LONG)(LONG_PTR) & (((type *)0)->field))
#endif
#endif

/*
 * Linux feature macros
 *
 * These feature-test macros are commonly defined for Linux builds so that
 * system header behavior is consistent across environments. They are set here
 * as fallbacks if the build system (CMake) did not already define them.
 */
#if defined(__linux__) || defined(__gnu_linux__)
#ifndef _GNU_SOURCE
#define _GNU_SOURCE
#endif
#ifndef _DEFAULT_SOURCE
#define _DEFAULT_SOURCE
#endif
#ifndef _ATFILE_SOURCE
#define _ATFILE_SOURCE
#endif
#ifndef _FILE_OFFSET_BITS
#define _FILE_OFFSET_BITS 64
#endif
#endif

#endif // PLATFORM_H
