/**
 * @file UserGroupDatabase.h
 * @brief Bindings for querying system user/group databases.
 */
#ifndef USERGROUPDATABASE_H
#define USERGROUPDATABASE_H

#include "ValXfer.h"
#include <cstdint>

namespace OsCalls {
extern "C" {
/**
 * @brief Query passwd database by numeric UID.
 * @param uid User ID to look up.
 * @return ValueT cursor with passwd structure fields (pw_name, pw_uid, pw_gid,
 * etc.).
 */
ValueT *getpwuid(std::int64_t uid);

/**
 * @brief Query group database by numeric GID.
 * @param gid Group ID to look up.
 * @return ValueT cursor with group structure fields (gr_name, gr_gid, gr_mem).
 */
ValueT *getgrgid(std::int64_t gid);
/* Linux-prefixed shim exports */
ValueT *linux_getpwuid(std::int64_t uid);
ValueT *linux_getgrgid(std::int64_t gid);
}
} // namespace OsCalls

#endif // USERGROUPDATABASE_H
