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
                set_val(Number, "st_dev", stbuf->st_dev);
                return true;
            }
        // else fall through
        default:
            delete reinterpret_cast<struct stat*>(value->Handle.data1);
            delete value;
            return false;
        case 1:
            set_val(Number, "st_ino", stbuf->st_ino);
            return true;
        case 2:
            set_val(Number, "st_mode", stbuf->st_mode);
            return true;
        case 3:
            set_val(Boolean, "S_ISBLK",
#ifdef S_ISBLK
                S_ISBLK(stbuf->st_mode)
#else
                false
#endif
            );
            return true;
        case 4:
            set_val(Boolean, "S_ISCHR",
#ifdef S_ISCHR
                S_ISCHR(stbuf->st_mode)
#else
                false
#endif
            );
            return true;
        case 5:
            set_val(Boolean, "S_ISDIR", S_ISDIR(stbuf->st_mode));
            return true;
        case 6:
            set_val(Boolean, "S_ISFIFO",
#ifdef S_ISFIFO
                S_ISFIFO(stbuf->st_mode)
#else
                false
#endif
            );
            return true;
        case 7:
            set_val(Boolean, "S_ISLNK", S_ISLNK(stbuf->st_mode));
            return true;
        case 8:
            set_val(Boolean, "S_ISREG", S_ISREG(stbuf->st_mode));
            return true;
        case 9:
            set_val(Boolean, "S_ISSOCK",
#ifdef S_ISSOCK
                S_ISSOCK(stbuf->st_mode)
#else
                false
#endif
            );
            return true;
        case 10:
            set_val(Boolean, "S_TYPEISMQ",
#ifdef S_TYPEISMQ
                S_TYPEISMQ(stbuf)
#else
                false
#endif
            );
            return true;
        case 11:
            set_val(Boolean, "S_TYPEISSEM",
#ifdef S_TYPEISSEM
                S_TYPEISSEM(stbuf)
#else
                false
#endif
            );
            return true;
        case 12:
            set_val(Boolean, "S_TYPEISSHM",
#ifdef S_TYPEISSHM
                S_TYPEISSHM(stbuf)
#else
                false
#endif
            );
            return true;
        case 13:
            set_val(Boolean, "S_TYPEISTMO",
#ifdef S_TYPEISTMO
                S_TYPEISTMO(stbuf)
#else
                false
#endif
            );
            return true;
        case 14:
            set_val(Number, "st_nlink", stbuf->st_nlink);
            return true;
        case 15:
            set_val(Number, "st_uid", stbuf->st_uid);
            return true;
        case 16:
            set_val(Number, "st_gid", stbuf->st_gid);
            return true;
        case 17:
            set_val(Number, "st_rdev", stbuf->st_rdev);
            return true;
        case 18:
            set_val(Number, "st_size", stbuf->st_size);
            return true;
        case 19:
            set_val(TimeSpec, "st_atim", stbuf->st_atim);
            return true;
        case 20:
            set_val(TimeSpec, "st_mtim", stbuf->st_mtim);
            return true;
        case 21:
            set_val(TimeSpec, "st_ctim", stbuf->st_ctim);
            return true;
        case 22:
            set_val(Number, "st_blksize", stbuf->st_blksize);
            return true;
        case 23:
            set_val(Number, "st_blocks", stbuf->st_blocks);
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
                set_val(String, "path", cfn);
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
                set_val(String, "path", cfn);
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
