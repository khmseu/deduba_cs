#include "FileSystem.h"
#include <cstdlib>
#include <cerrno>
#include <unistd.h>
#include <sys/stat.h>
#include <climits>

namespace OsCalls
{
    bool handle_lstat(ValueT* value)
    {
        auto stbuf = reinterpret_cast<struct stat*>(value->Handle.data1);
        switch (value->Handle.index)
        {
        case 0:
            if (value->Type == TypeT::IsOk)
            {
                value->Type = TypeT::IsNumber;
                value->Name = "st_dev";
                value->Number = stbuf->st_dev;
                return true;
            }
        // else fall through
        default:
            delete reinterpret_cast<struct stat*>(value->Handle.data1);
            delete value;
            return false;
        case 1:
            value->Type = TypeT::IsNumber;
            value->Name = "st_ino";
            value->Number = stbuf->st_ino;
            return true;
        case 2:
            value->Type = TypeT::IsNumber;
            value->Name = "st_mode";
            value->Number = stbuf->st_mode;
            return true;
        case 3:
            value->Type = TypeT::IsNumber;
            value->Name = "st_nlink";
            value->Number = stbuf->st_nlink;
            return true;
        case 4:
            value->Type = TypeT::IsNumber;
            value->Name = "st_uid";
            value->Number = stbuf->st_uid;
            return true;
        case 5:
            value->Type = TypeT::IsNumber;
            value->Name = "st_gid";
            value->Number = stbuf->st_gid;
            return true;
        case 6:
            value->Type = TypeT::IsNumber;
            value->Name = "st_rdev";
            value->Number = stbuf->st_rdev;
            return true;
        case 7:
            value->Type = TypeT::IsNumber;
            value->Name = "st_size";
            value->Number = stbuf->st_size;
            return true;
        case 8:
            value->Type = TypeT::IsTimeSpec;
            value->Name = "st_atim";
            value->TimeSpec = stbuf->st_atim;
            return true;
        case 9:
            value->Type = TypeT::IsTimeSpec;
            value->Name = "st_mtim";
            value->TimeSpec = stbuf->st_mtim;
            return true;
        case 10:
            value->Type = TypeT::IsTimeSpec;
            value->Name = "st_ctim";
            value->TimeSpec = stbuf->st_ctim;
            return true;
        case 11:
            value->Type = TypeT::IsNumber;
            value->Name = "st_blksize";
            value->Number = stbuf->st_blksize;
            return true;
        case 12:
            value->Type = TypeT::IsNumber;
            value->Name = "st_blocks";
            value->Number = stbuf->st_blocks;
            return true;
        }
    }

    bool handle_readlink(ValueT* value)
    {
        auto cfn = reinterpret_cast<const char*>(value->Handle.data1);
        switch (value->Handle.index)
        {
        case 0:
            if (value->Type == TypeT::IsOk)
            {
                value->Type = TypeT::IsString;
                value->Name = "path";
                value->String = cfn;
                return true;
            }
        // else fall through
        default:
            delete[] reinterpret_cast<char*>(value->Handle.data1);
            delete value;
            return false;
        }
    }

    bool handle_cfn(ValueT* value)
    {
        auto cfn = reinterpret_cast<const char*>(value->Handle.data1);
        switch (value->Handle.index)
        {
        case 0:
            if (value->Type == TypeT::IsOk)
            {
                value->Type = TypeT::IsString;
                value->Name = "path";
                value->String = cfn;
                return true;
            }
        // else fall through
        default:
            ::free(value->Handle.data1);
            delete value;
            return false;
        }
    }

    auto slbufsz = _POSIX_PATH_MAX;

    extern "C" {
    ValueT* lstat(const char* path)
    {
        auto stbuf = new struct stat();
        errno = 0;
        auto rc = ::lstat(path, stbuf);
        auto en = errno;
        auto v = new ValueT();
        CreateHandle(v, handle_lstat, stbuf, nullptr);
        if (rc < 0)
            v->Number = en;
        else
            v->Type = TypeT::IsOk;
        return v;
    };

    ValueT* readlink(const char* path)
    {
        if (slbufsz <= 0)
            slbufsz = 1024;
        auto cnt = 0;
        auto en = 0;
        char* strbuf = nullptr;
        do
        {
            strbuf = new char[slbufsz];
            errno = 0;
            cnt = ::readlink(path, strbuf, slbufsz - 1);
            en = errno;
            if (cnt >= slbufsz - 1)
            {
                slbufsz <<= 1;
                delete[] strbuf;
            }
        }
        while (cnt >= slbufsz - 1);
        auto v = new ValueT();
        CreateHandle(v, handle_readlink, strbuf, nullptr);
        if (cnt < 0)
            v->Number = en;
        else
        {
            v->Type = TypeT::IsOk;
            strbuf[cnt] = '\0';
        }
        return v;
    };

    ValueT* canonicalize_file_name(const char* path)
    {
        // ::chown("*** before ***", errno, (intptr_t)path);
        errno = 0;
        auto cfn = ::canonicalize_file_name(path);
        auto en = errno;
        // ::chown("*** after ***", errno, (intptr_t)cfn);
        auto v = new ValueT();
        CreateHandle(v, handle_cfn, cfn, nullptr);
        if (cfn != nullptr)
            v->Type = TypeT::IsOk;
        else
            v->Number = en;
        return v;
    };
    }
}
