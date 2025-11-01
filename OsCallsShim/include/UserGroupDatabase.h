/**
 * @file UserGroupDatabase.h
 * @brief Bindings for querying system user/group databases.
 */
#ifndef USERGROUPDATABASE_H
#define USERGROUPDATABASE_H

#include "ValXfer.h"
#include <cstdint>

namespace OsCalls
{
    extern "C" {
    /** @brief Query passwd database by numeric UID. */
    ValueT* getpwuid(std::uint64_t uid);
    /** @brief Query group database by numeric GID. */
    ValueT* getgrgid(std::uint64_t uid);
    }
} // namespace OsCalls

#endif // USERGROUPDATABASE_H
