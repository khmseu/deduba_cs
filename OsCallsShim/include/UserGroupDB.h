#ifndef USERGROUPDB_H
#define USERGROUPDB_H

#include "ValXfer.h"

namespace OsCalls
{
    extern "C" {
    TValue* getpwuid(uint64_t uid);
    TValue* getpwuid(uint64_t uid);
    }
} // namespace OsCalls

#endif // USERGROUPDB_H
