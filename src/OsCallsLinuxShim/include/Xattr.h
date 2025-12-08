/**
 * @file Xattr.h
 * @brief Native extended attributes (xattr) reading APIs for POSIX systems.
 *
 * Exposes functions to list and read extended attributes from filesystem paths
 * without following symlinks (llistxattr and lgetxattr equivalents).
 */
#ifndef XATTR_H
#define XATTR_H

#include "ValXfer.h"

namespace OsCalls {
/**
 * @name Extended attribute operations
 * Functions exported with C linkage for consumption via P/Invoke.
 * @{
 */
extern "C" {
/**
 * @brief List all extended attribute names for a path (not following symlinks).
 * @param path Filesystem path to read xattrs from.
 * @return ValueT cursor with array of xattr names or error.
 */
ValueT *llistxattr(const char *path);

/**
 * @brief Get the value of a specific extended attribute (not following
 * symlinks).
 * @param path Filesystem path to read xattr from.
 * @param name Name of the extended attribute to retrieve.
 * @return ValueT cursor with xattr value as string or error.
 */
ValueT *lgetxattr(const char *path, const char *name);
/* Linux-prefixed shim exports */
ValueT *linux_llistxattr(const char *path);
ValueT *linux_lgetxattr(const char *path, const char *name);
}

/** @} */
}  // namespace OsCalls

#endif  // XATTR_H
