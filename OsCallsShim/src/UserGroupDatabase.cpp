#include "UserGroupDatabase.h"
#include <pwd.h>
#include <grp.h>
#include <unistd.h>
#include <cerrno>

namespace OsCalls
{
    bool handle_passwd(ValueT* value)
    {
        auto pw = reinterpret_cast<passwd*>(value->Handle.data1);
        switch (value->Handle.index)
        {
        case 0:
            if (value->Type == TypeT::IsOk)
            {
                value->Type = TypeT::IsString;
                value->Name = "pw_name";
                value->String = pw->pw_name;
                return true;
            }
        // else fall through
        default:
            delete reinterpret_cast<passwd*>(value->Handle.data1);
            delete[] reinterpret_cast<char*>(value->Handle.data2);
            delete value;
            return false;
        case 1:
            value->Type = TypeT::IsString;
            value->Name = "pw_passwd";
            value->String = pw->pw_passwd;
            return true;
        case 2:
            value->Type = TypeT::IsNumber;
            value->Name = "pw_uid";
            value->Number = pw->pw_uid;
            return true;
        case 3:
            value->Type = TypeT::IsNumber;
            value->Name = "pw_gid";
            value->Number = pw->pw_gid;
            return true;
        case 4:
            value->Type = TypeT::IsString;
            value->Name = "pw_gecos";
            value->String = pw->pw_gecos;
            return true;
        case 5:
            value->Type = TypeT::IsString;
            value->Name = "pw_dir";
            value->String = pw->pw_dir;
            return true;
        case 6:
            value->Type = TypeT::IsString;
            value->Name = "pw_shell";
            value->String = pw->pw_shell;
            return true;
        }
    }

    bool handle_group_mem(OsCalls::ValueT* value)
    {
        auto mem = reinterpret_cast<char**>(value->Handle.data1);
        if (mem[value->Handle.index] == nullptr)
        {
            delete value;
            return false;
        }
        value->Type = TypeT::IsString;
        value->Name = "[]";
        value->String = mem[value->Handle.index];
        return true;
    }

    bool handle_group(ValueT* value)
    {
        auto gr = reinterpret_cast<group*>(value->Handle.data1);
        switch (value->Handle.index)
        {
        case 0:
            if (value->Type == TypeT::IsOk)
            {
                value->Type = TypeT::IsString;
                value->Name = "gr_name";
                value->String = gr->gr_name;
                return true;
            }
        // else fall through
        default:
            delete reinterpret_cast<group*>(value->Handle.data1);
            delete[] reinterpret_cast<char*>(value->Handle.data2);
            delete value;
            return false;
        case 1:
            value->Type = TypeT::IsNumber;
            value->Name = "gr_gid";
            value->Number = gr->gr_gid;
            return true;
        case 2:
            value->Type = TypeT::IsComplex;
            value->Name = "gr_mem[]";
            value->Complex = new ValueT();
            CreateHandle(value->Complex, handle_group_mem, gr->gr_mem, nullptr);
            value->Complex->Type = TypeT::IsOk;
            return true;
        }
    }

    auto pwbufsz = sysconf(_SC_GETPW_R_SIZE_MAX);
    auto grbufsz = sysconf(_SC_GETGR_R_SIZE_MAX);

    extern "C" {
    ValueT* getpwuid(uint64_t uid)
    {
        if (pwbufsz <= 0)
            pwbufsz = 1024;
        auto pwbuf = new passwd();
        struct passwd* pwbufp = nullptr;
        auto en = 0;
        char* strbuf = nullptr;
        do
        {
            strbuf = new char[pwbufsz];
            en = ::getpwuid_r(uid, pwbuf, strbuf, pwbufsz, &pwbufp);
            if (en == ERANGE)
            {
                pwbufsz <<= 1;
                delete[] strbuf;
            }
        }
        while (en == ERANGE);
        auto v = new ValueT();
        CreateHandle(v, handle_passwd, pwbuf, strbuf);
        if (en == 0)
            v->Type = TypeT::IsOk;
        else
            v->Number = en;
        return v;
    };

    ValueT* getgrgid(uint64_t gid)
    {
        if (grbufsz <= 0)
            grbufsz = 1024;
        auto grbuf = new group();
        struct group* grbufp = nullptr;
        auto en = 0;
        char* strbuf = nullptr;
        do
        {
            strbuf = new char[grbufsz];
            en = ::getgrgid_r(gid, grbuf, strbuf, grbufsz, &grbufp);
            if (en == ERANGE)
            {
                grbufsz <<= 1;
                delete[] strbuf;
            }
        }
        while (en == ERANGE);
        auto v = new ValueT();
        CreateHandle(v, handle_group, grbuf, strbuf);
        if (en == 0)
            v->Type = TypeT::IsOk;
        else
            v->Number = en;
        return v;
    };
    }
}
