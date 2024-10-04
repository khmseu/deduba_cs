#ifndef USERGROUPDATABASE_H
#define USERGROUPDATABASE_H

#include "ValXfer.h"

namespace OsCalls
{
    extern "C" {
    ValueT* getpwuid(uint64_t uid);
    ValueT* getgrgid(uint64_t uid);
    }
} // namespace OsCalls

#endif // USERGROUPDATABASE_H
