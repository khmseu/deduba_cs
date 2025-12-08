/**
 * @file Acl.h
 * @brief Native ACL (Access Control List) reading APIs for POSIX ACLs.
 *
 * Exposes functions to read access and default ACLs from filesystem paths
 * and convert them to short text representation using libacl functions.
 */
#ifndef ACL_H
#define ACL_H

#include "ValXfer.h"

namespace OsCalls {
/**
 * @name ACL operations
 * Functions exported with C linkage for consumption via P/Invoke.
 * @{
 */
extern "C" {
/**
 * @brief Read access ACL from a path and return as short text.
 * @param path Filesystem path to read ACL from.
 * @return ValueT cursor with ACL text or error.
 */
ValueT *acl_get_file_access(const char *path);

/**
 * @brief Read default ACL from a path and return as short text.
 * @param path Filesystem path (must be a directory).
 * @return ValueT cursor with ACL text or error.
 */
ValueT *acl_get_file_default(const char *path);
/* Linux-prefixed shim exports */
ValueT *linux_acl_get_file_access(const char *path);
ValueT *linux_acl_get_file_default(const char *path);
}

/** @} */
}  // namespace OsCalls

#endif  // ACL_H
