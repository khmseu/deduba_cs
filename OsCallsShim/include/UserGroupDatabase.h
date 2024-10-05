#ifndef USERGROUPDATABASE_H
#define USERGROUPDATABASE_H

#include "ValXfer.h"
#include <cstdint>

namespace OsCalls
{
    extern "C" {
    ValueT* getpwuid(std::uint64_t uid);
    ValueT* getgrgid(std::uint64_t uid);
    }
} // namespace OsCalls

#endif // USERGROUPDATABASE_H
