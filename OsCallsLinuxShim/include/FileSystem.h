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
 *
 * Returns file metadata including type (S_ISDIR, S_ISREG, S_ISLNK, etc.),
 * permissions (st_mode), size (st_size), ownership (st_uid, st_gid),
 * timestamps (st_atim, st_mtim, st_ctim), and inode information (st_ino,
 * st_dev).
 *
 * @param path Input file system path.
 * @return ValueT cursor of key/value pairs describing the stat buffer.
 */
ValueT *lstat(const char *path);

/**
 * @brief readlink(2) equivalent returning the symlink target as a string value.
 *
 * Does not follow the symlink itself. Returns the raw target path string
 * which may be relative or absolute.
 *
 * @param path Path to symbolic link.
 * @return ValueT cursor with "path" field containing symlink target.
 */
ValueT *readlink(const char *path);

/**
 * @brief Resolves a path to its canonical absolute form (glibc
 * canonicalize_file_name).
 *
 * Expands all symbolic links, resolves relative components (. and ..),
 * and returns the absolute path. Follows symlinks unlike lstat.
 *
 * @param path Input path (may be relative or contain symlinks).
 * @return ValueT cursor with "path" field containing canonical absolute path.
 */
ValueT *canonicalize_file_name(const char *path);
}
} // namespace OsCalls

#endif // FILESYSTEM_H
