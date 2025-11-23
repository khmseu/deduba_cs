#ifndef OC_EXPORT_H
#define OC_EXPORT_H

/*
 * OcExport.h
 * Cross-platform DLL/export macro used by the native shim projects.
 * This header should be included first in each .cpp file, before other
 * includes. It defines DLL_EXPORT appropriately for Windows via
 * __declspec(dllexport) and for other platforms as a no-op or GCC visibility
 * attribute.
 *
 * When generating Doxygen docs (or when compiling for documentation), define
 * DOXYGEN or set PREDEFINED in Doxygen to avoid Doxygen confusing attributes.
 */

#if defined(DOXYGEN)
/* When Doxygen is running, neutralize attributes so that Doxygen does not get
 * confused. */
#define DLL_EXPORT
#elif defined(_WIN32) || defined(_WIN64) || defined(__MINGW32__) ||            \
    defined(__MINGW64__)
#define DLL_EXPORT __declspec(dllexport)
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
#else
#if defined(__GNUC__) || defined(__clang__)
#define DLL_EXPORT __attribute__((visibility("default")))
#else
#define DLL_EXPORT
#endif
#endif

#endif // OC_EXPORT_H
