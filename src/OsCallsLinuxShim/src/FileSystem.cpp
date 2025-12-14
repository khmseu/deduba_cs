#include "Platform.h"
// Platform.h must come first
#include "FileSystem.h"
#include <cerrno>
#include <climits>
#include <cstdlib>
#include <sys/stat.h>
#include <unistd.h>

// Helper function to convert timespec to TimeSpec64
static OsCalls::TimeSpec64 timespec_to_timespec64(const struct timespec &ts) {
    OsCalls::TimeSpec64 result;
    result.tv_sec = ts.tv_sec;
    result.tv_nsec = ts.tv_nsec;
    return result;
}

// Safe wrappers for file type test macros (not all are available on all
// platforms)
#ifdef S_ISBLK
#define SAFE_S_ISBLK(m) S_ISBLK(m)
#else
#define SAFE_S_ISBLK(m) false
#endif

#ifdef S_ISCHR
#define SAFE_S_ISCHR(m) S_ISCHR(m)
#else
#define SAFE_S_ISCHR(m) false
#endif

#ifdef S_ISFIFO
#define SAFE_S_ISFIFO(m) S_ISFIFO(m)
#else
#define SAFE_S_ISFIFO(m) false
#endif

#ifdef S_ISSOCK
#define SAFE_S_ISSOCK(m) S_ISSOCK(m)
#else
#define SAFE_S_ISSOCK(m) false
#endif

#ifdef S_TYPEISMQ
#define SAFE_S_TYPEISMQ(s) S_TYPEISMQ(s)
#else
#define SAFE_S_TYPEISMQ(s) false
#endif

#ifdef S_TYPEISSEM
#define SAFE_S_TYPEISSEM(s) S_TYPEISSEM(s)
#else
#define SAFE_S_TYPEISSEM(s) false
#endif

#ifdef S_TYPEISSHM
#define SAFE_S_TYPEISSHM(s) S_TYPEISSHM(s)
#else
#define SAFE_S_TYPEISSHM(s) false
#endif

#ifdef S_TYPEISTMO
#define SAFE_S_TYPEISTMO(s) S_TYPEISTMO(s)
#else
#define SAFE_S_TYPEISTMO(s) false
#endif

namespace OsCalls {
/**
 * @brief Handler function for lstat results - iterates through stat structure
 * fields.
 *
 * Yields stat buffer fields sequentially (st_dev, st_ino, st_mode, file type
 * flags, st_nlink, owner/group IDs, size, timestamps, etc.) via the ValueT
 * cursor protocol. Cleans up allocated stat buffer and ValueT on completion or
 * error.
 *
 * @param value Pointer to ValueT with Handle.data1 containing struct stat*.
 * @return true if more fields remain, false when iteration completes.
 */
bool handle_lstat(ValueT *value) {
    auto stbuf = reinterpret_cast<struct stat *>(value->Handle.data1);
    switch (value->Handle.index) {
    case 0:
        if (value->Type == TypeT::IsOk) {
            set_val(Number, "st_dev", stbuf->st_dev);
            return true;
        }
    // else fall through
    default:
        delete reinterpret_cast<struct stat *>(value->Handle.data1);
        delete value;
        return false;
    case 1:
        set_val(Number, "st_ino", stbuf->st_ino);
        return true;
    case 2:
        set_val(Number, "st_mode", stbuf->st_mode);
        return true;
    case 3:
        set_val(Boolean, "S_ISBLK", SAFE_S_ISBLK(stbuf->st_mode));
        return true;
    case 4:
        set_val(Boolean, "S_ISCHR", SAFE_S_ISCHR(stbuf->st_mode));
        return true;
    case 5:
        set_val(Boolean, "S_ISDIR", S_ISDIR(stbuf->st_mode));
        return true;
    case 6:
        set_val(Boolean, "S_ISFIFO", SAFE_S_ISFIFO(stbuf->st_mode));
        return true;
    case 7:
        set_val(Boolean, "S_ISLNK", S_ISLNK(stbuf->st_mode));
        return true;
    case 8:
        set_val(Boolean, "S_ISREG", S_ISREG(stbuf->st_mode));
        return true;
    case 9:
        set_val(Boolean, "S_ISSOCK", SAFE_S_ISSOCK(stbuf->st_mode));
        return true;
    case 10:
        set_val(Boolean, "S_TYPEISMQ", SAFE_S_TYPEISMQ(stbuf));
        return true;
    case 11:
        set_val(Boolean, "S_TYPEISSEM", SAFE_S_TYPEISSEM(stbuf));
        return true;
    case 12:
        set_val(Boolean, "S_TYPEISSHM", SAFE_S_TYPEISSHM(stbuf));
        return true;
    case 13:
        set_val(Boolean, "S_TYPEISTMO", SAFE_S_TYPEISTMO(stbuf));
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
        set_val(TimeSpec, "st_atim", timespec_to_timespec64(stbuf->st_atim));
        return true;
    case 20:
        set_val(TimeSpec, "st_mtim", timespec_to_timespec64(stbuf->st_mtim));
        return true;
    case 21:
        set_val(TimeSpec, "st_ctim", timespec_to_timespec64(stbuf->st_ctim));
        return true;
    case 22:
        set_val(Number, "st_blksize", stbuf->st_blksize);
        return true;
    case 23:
        set_val(Number, "st_blocks", stbuf->st_blocks);
        return true;
    }
}

/**
 * @brief Handler function for readlink results - returns symlink target path.
 *
 * Yields a single string value containing the symlink's target path.
 * Cleans up allocated buffer and ValueT on completion or error.
 *
 * @param value Pointer to ValueT with Handle.data1 containing char* buffer.
 * @return true on first call if successful, false to signal completion.
 */
bool handle_readlink(ValueT *value) {
    auto cfn = reinterpret_cast<const char *>(value->Handle.data1);
    switch (value->Handle.index) {
    case 0:
        if (value->Type == TypeT::IsOk) {
            set_val(String, "path", cfn);
            return true;
        }
    // else fall through
    default:
        delete[] reinterpret_cast<char *>(value->Handle.data1);
        delete value;
        return false;
    }
}

/**
 * @brief Handler function for canonicalize_file_name results - returns
 * canonical path.
 *
 * Yields a single string value containing the resolved absolute path with all
 * symlinks expanded and relative components removed.
 * Uses ::free() on data1 since canonicalize_file_name uses malloc.
 *
 * @param value Pointer to ValueT with Handle.data1 containing char* from
 * canonicalize_file_name.
 * @return true on first call if successful, false to signal completion.
 */
bool handle_cfn(ValueT *value) {
    auto cfn = reinterpret_cast<const char *>(value->Handle.data1);
    switch (value->Handle.index) {
    case 0:
        if (value->Type == TypeT::IsOk) {
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
/**
 * @brief Performs lstat(2) on the specified path and returns results as ValueT
 * cursor.
 *
 * Does not follow symbolic links. Returns file metadata including type,
 * permissions, size, timestamps, and ownership information.
 *
 * @param path Filesystem path to inspect.
 * @return ValueT* cursor initialized with stat data or error number.
 */
ValueT *linux_lstat(const char *path) {
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

// Backwards-compatibility wrapper: call the linux_* prefixed implementation.
ValueT *lstat(const char *path) {
    return linux_lstat(path);
};

/**
 * @brief Reads the target of a symbolic link and returns it as a string.
 *
 * Uses readlink(2) with automatic buffer resizing to handle arbitrarily long
 * paths. Does not follow the symlink itself.
 *
 * @param path Path to the symbolic link.
 * @return ValueT* cursor with symlink target string or error number.
 */
ValueT *linux_readlink(const char *path) {
    if (slbufsz <= 0)
        slbufsz = 1024;
    auto  cnt = 0;
    auto  en = 0;
    char *strbuf = nullptr;
    do {
        strbuf = new char[slbufsz];
        errno = 0;
        cnt = ::readlink(path, strbuf, slbufsz - 1);
        en = errno;
        if (cnt >= slbufsz - 1) {
            slbufsz <<= 1;
            delete[] strbuf;
        }
    } while (cnt >= slbufsz - 1);
    auto v = new ValueT();
    CreateHandle(v, handle_readlink, strbuf, nullptr);
    if (cnt < 0)
        v->Number = en;
    else {
        v->Type = TypeT::IsOk;
        strbuf[cnt] = '\0';
    }
    return v;
};

// Backwards-compatibility wrapper: call the linux_* prefixed implementation.
ValueT *readlink(const char *path) {
    return linux_readlink(path);
};

/**
 * @brief Resolves a path to its canonical absolute form.
 *
 * Uses glibc's canonicalize_file_name to expand all symbolic links and resolve
 * relative path components (. and ..). Follows symlinks unlike lstat.
 *
 * @param path Input path (relative or absolute, may contain symlinks).
 * @return ValueT* cursor with canonical path string or error number.
 */
ValueT *linux_canonicalize_file_name(const char *path) {
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

// Backwards-compatibility wrapper: call the linux_* prefixed implementation.
ValueT *canonicalize_file_name(const char *path) {
    return linux_canonicalize_file_name(path);
};
}
}  // namespace OsCalls
