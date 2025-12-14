#include "Platform.h"
// Platform.h must come first
#include "Xattr.h"
#include <cerrno>
#include <cstdlib>
#include <cstring>
#include <sys/xattr.h>

namespace OsCalls {
/**
 * @brief Context structure for llistxattr iteration
 */
struct XattrListContext {
    char *buffer;   // Original buffer start
    char *current;  // Current position
    char *end;      // End of buffer (buffer + size)
};

/**
 * @brief Handler for llistxattr that returns an array of attribute names.
 *
 * Iterates through null-terminated attribute names in the buffer, yielding
 * each as a string array element. Cleans up allocated buffer and context
 * on completion.
 *
 * @param value Pointer to ValueT with Handle.data1 containing
 * XattrListContext*.
 * @return true if more attribute names remain, false when iteration completes.
 */
bool handle_llistxattr(ValueT *value) {
    auto ctx = reinterpret_cast<XattrListContext *>(value->Handle.data1);

    // First call after initialization
    if (value->Type == TypeT::IsOk) {
        // If we have data and haven't reached the end
        if (ctx != nullptr && ctx->current < ctx->end && *ctx->current != '\0') {
            value->Name = "[]";
            set_val(String, "[]", ctx->current);

            // Move to next attribute name
            size_t len = strlen(ctx->current) + 1;
            ctx->current += len;
            return true;
        }
    }

    // Subsequent calls - check if there's more data
    if (ctx != nullptr && ctx->current < ctx->end && *ctx->current != '\0') {
        set_val(String, "[]", ctx->current);

        // Move to next attribute name
        size_t len = strlen(ctx->current) + 1;
        ctx->current += len;
        return true;
    }

    // Cleanup on completion
    if (ctx != nullptr) {
        if (ctx->buffer != nullptr)
            free(ctx->buffer);
        delete ctx;
    }
    delete value;
    return false;
}

/**
 * @brief Handler for lgetxattr that returns the attribute value as a string.
 *
 * Yields a single string value containing the extended attribute's data.
 * Uses ::free() to release the malloc'd buffer.
 *
 * @param value Pointer to ValueT with Handle.data1 containing char* attribute
 * value.
 * @return true on first call if successful, false to signal completion.
 */
bool handle_lgetxattr(ValueT *value) {
    auto attr_value = reinterpret_cast<char *>(value->Handle.data1);
    switch (value->Handle.index) {
    case 0:
        if (value->Type == TypeT::IsOk) {
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
/**
 * @brief Lists all extended attribute names for a path (not following
 * symlinks).
 *
 * Uses llistxattr(2) to retrieve the list of attribute names. The buffer
 * is automatically sized based on the syscall's return value.
 *
 * @param path Filesystem path to read xattrs from.
 * @return ValueT* cursor yielding array of attribute name strings or error
 * number.
 */
ValueT *linux_llistxattr(const char *path) {
    errno = 0;

    // First call to get the size needed
    ssize_t buflen = ::llistxattr(path, nullptr, 0);
    auto    en = errno;

    char             *buffer = nullptr;
    XattrListContext *ctx = nullptr;

    if (buflen > 0) {
        // Allocate buffer and get the attribute names
        buffer = static_cast<char *>(malloc(buflen));
        errno = 0;
        buflen = ::llistxattr(path, buffer, buflen);
        en = errno;

        if (buflen >= 0) {
            // Create context structure
            ctx = new XattrListContext{buffer, buffer, buffer + buflen};
        } else {
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

// Backwards-compatibility wrapper: call the linux_* prefixed implementation.
ValueT *llistxattr(const char *path) {
    return linux_llistxattr(path);
};

/**
 * @brief Gets the value of a specific extended attribute (not following
 * symlinks).
 *
 * Uses lgetxattr(2) to read the attribute value. The buffer is automatically
 * sized and null-terminated for string interpretation.
 *
 * @param path Filesystem path to read xattr from.
 * @param name Name of the extended attribute to retrieve.
 * @return ValueT* cursor with attribute value as string or error number.
 */
ValueT *linux_lgetxattr(const char *path, const char *name) {
    errno = 0;

    // First call to get the size needed
    ssize_t buflen = ::lgetxattr(path, name, nullptr, 0);
    auto    en = errno;

    char *buffer = nullptr;
    if (buflen > 0) {
        // Allocate buffer and get the attribute value
        buffer = static_cast<char *>(malloc(buflen + 1));  // +1 for null terminator
        errno = 0;
        buflen = ::lgetxattr(path, name, buffer, buflen);
        en = errno;

        if (buflen >= 0)
            buffer[buflen] = '\0';  // Null-terminate the string
    }

    auto v = new ValueT();
    CreateHandle(v, handle_lgetxattr, buffer, nullptr);

    if (buflen >= 0)
        v->Type = TypeT::IsOk;
    else
        v->Number = en;

    return v;
}

// Backwards-compatibility wrapper: call the linux_* prefixed implementation.
ValueT *lgetxattr(const char *path, const char *name) {
    return linux_lgetxattr(path, name);
};
}
}  // namespace OsCalls
