#include "Xattr.h"
#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <sys/xattr.h>

namespace OsCalls
{
    /**
     * @brief Context structure for llistxattr iteration
     */
    struct XattrListContext
    {
        char* buffer;     // Original buffer start
        char* current;    // Current position
        char* end;        // End of buffer (buffer + size)
    };

    /**
     * @brief Handler for llistxattr that returns an array of attribute names.
     * The data1 pointer contains a XattrListContext structure.
     */
    bool handle_llistxattr(ValueT* value)
    {
        auto ctx = reinterpret_cast<XattrListContext*>(value->Handle.data1);

        // First call after initialization
        if (value->Type == TypeT::IsOk)
        {
            // If we have data and haven't reached the end
            if (ctx != nullptr && ctx->current < ctx->end && *ctx->current != '\0')
            {
                value->Name = "[]";
                set_val(String, "[]", ctx->current);
                
                // Move to next attribute name
                size_t len = strlen(ctx->current) + 1;
                ctx->current += len;
                return true;
            }
        }
        
        // Subsequent calls - check if there's more data
        if (ctx != nullptr && ctx->current < ctx->end && *ctx->current != '\0')
        {
            set_val(String, "[]", ctx->current);
            
            // Move to next attribute name
            size_t len = strlen(ctx->current) + 1;
            ctx->current += len;
            return true;
        }

        // Cleanup on completion
        if (ctx != nullptr)
        {
            if (ctx->buffer != nullptr)
                free(ctx->buffer);
            delete ctx;
        }
        delete value;
        return false;
    }

    /**
     * @brief Handler for lgetxattr that returns the attribute value as a string.
     */
    bool handle_lgetxattr(ValueT* value)
    {
        auto attr_value = reinterpret_cast<char*>(value->Handle.data1);
        switch (value->Handle.index)
        {
        case 0:
            if (value->Type == TypeT::IsOk)
            {
                set_val(String, "value", attr_value);
                return true;
            }
        // else fall through
        default:
            if (attr_value != nullptr)
                free(attr_value);
            delete value;
            return false;
        }
    }

    extern "C" {
    ValueT* llistxattr(const char* path)
    {
        errno = 0;
        
        // First call to get the size needed
        ssize_t buflen = ::llistxattr(path, nullptr, 0);
        auto en = errno;
        
        char* buffer = nullptr;
        XattrListContext* ctx = nullptr;
        
        if (buflen > 0)
        {
            // Allocate buffer and get the attribute names
            buffer = static_cast<char*>(malloc(buflen));
            errno = 0;
            buflen = ::llistxattr(path, buffer, buflen);
            en = errno;
            
            if (buflen >= 0)
            {
                // Create context structure
                ctx = new XattrListContext{buffer, buffer, buffer + buflen};
            }
            else
            {
                // Error occurred, clean up
                free(buffer);
                buffer = nullptr;
            }
        }
        
        auto v = new ValueT();
        CreateHandle(v, handle_llistxattr, ctx, nullptr);
        
        if (buflen >= 0)
            v->Type = TypeT::IsOk;
        else
            v->Number = en;
        
        return v;
    }

    ValueT* lgetxattr(const char* path, const char* name)
    {
        errno = 0;
        
        // First call to get the size needed
        ssize_t buflen = ::lgetxattr(path, name, nullptr, 0);
        auto en = errno;
        
        char* buffer = nullptr;
        if (buflen > 0)
        {
            // Allocate buffer and get the attribute value
            buffer = static_cast<char*>(malloc(buflen + 1)); // +1 for null terminator
            errno = 0;
            buflen = ::lgetxattr(path, name, buffer, buflen);
            en = errno;
            
            if (buflen >= 0)
                buffer[buflen] = '\0'; // Null-terminate the string
        }
        
        auto v = new ValueT();
        CreateHandle(v, handle_lgetxattr, buffer, nullptr);
        
        if (buflen >= 0)
            v->Type = TypeT::IsOk;
        else
            v->Number = en;
        
        return v;
    }
    }
} // namespace OsCalls
