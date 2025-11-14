/**
 * @file FileSystem.h
 * @brief POSIX filesystem bindings exposed to managed code via P/Invoke.
 */
#ifndef FILESYSTEM_H
#define FILESYSTEM_H

#include "ValXfer.h"

namespace OsCalls {
extern "C" {
/**
 * @brief lstat(2) equivalent that does not follow symlinks.
 * @param path Input file system path.
 * @return Stream of key/value pairs describing the stat buffer.
 */
ValueT *lstat(const char *path);
/**
 * @brief readlink(2) equivalent returning the symlink target as a string value.
 */
ValueT *readlink(const char *path);
/**
 * @brief Resolves a path to its canonical absolute form (glibc
 * canonicalize_file_name).
 */
ValueT *canonicalize_file_name(const char *path);
}
} // namespace OsCalls

#endif // FILESYSTEM_H
