#ifndef FILESYSTEM_H
#define FILESYSTEM_H

#include "ValXfer.h"

namespace OsCalls
{
    extern "C" {
    ValueT* lstat(const char* path);
    ValueT* readlink(const char* path);
    ValueT* canonicalize_file_name(const char* path);
    }
} // namespace OsCalls

#endif // FILESYSTEM_H
