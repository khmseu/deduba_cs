#include "Acl.h"
#include <acl/libacl.h>
#include <cerrno>
#include <sys/acl.h>

namespace OsCalls
{
    bool handle_acl_text(ValueT* value)
    {
        auto acl_text = reinterpret_cast<const char*>(value->Handle.data1);
        switch (value->Handle.index)
        {
        case 0:
            if (value->Type == TypeT::IsOk)
            {
                set_val(String, "acl_text", acl_text);
                return true;
            }
        // else fall through
        default:
            if (value->Handle.data1 != nullptr)
                acl_free(value->Handle.data1);
            delete value;
            return false;
        }
    }

    extern "C" {
    ValueT* acl_get_file_access(const char* path)
    {
        errno = 0;
        acl_t acl = ::acl_get_file(path, ACL_TYPE_ACCESS);
        auto en = errno;

        char* text = nullptr;
        if (acl != nullptr)
        {
            // Convert to short text form (omits entries equal to mode bits)
            text = ::acl_to_any_text(acl, nullptr, ',', TEXT_ABBREVIATE);
            acl_free(acl);
            en = errno;
        }

        auto v = new ValueT();
        CreateHandle(v, handle_acl_text, text, nullptr);

        if (text != nullptr)
            v->Type = TypeT::IsOk;
        else
            v->Number = en;

        return v;
    }

    ValueT* acl_get_file_default(const char* path)
    {
        errno = 0;
        acl_t acl = ::acl_get_file(path, ACL_TYPE_DEFAULT);
        auto en = errno;

        char* text = nullptr;
        if (acl != nullptr)
        {
            // Convert to short text form (omits entries equal to mode bits)
            text = ::acl_to_any_text(acl, nullptr, ',', TEXT_ABBREVIATE);
            acl_free(acl);
            en = errno;
        }

        auto v = new ValueT();
        CreateHandle(v, handle_acl_text, text, nullptr);

        if (text != nullptr)
            v->Type = TypeT::IsOk;
        else
            v->Number = en;

        return v;
    }
    }
} // namespace OsCalls
