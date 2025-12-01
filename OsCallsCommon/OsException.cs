namespace OsCallsCommon;

/// <summary>
///     Exception hierarchy for OS API errors with normalized error kinds.
///     Maps platform-specific errors (errno/HRESULT) to common categories.
/// </summary>
public class OsException : Exception
{
    public OsException(string message, ErrorKind kind, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    /// <summary>
    ///     Normalized error category for cross-platform error handling.
    /// </summary>
    public ErrorKind Kind { get; }
}

/// <summary>
///     Normalized error categories across platforms.
///     Maps platform-specific errors to common categories for unified error handling.
/// </summary>
public enum ErrorKind
{
    /// <summary>File or directory not found (ENOENT / ERROR_FILE_NOT_FOUND)</summary>
    NotFound,

    /// <summary>Permission denied (EACCES, EPERM / ERROR_ACCESS_DENIED)</summary>
    PermissionDenied,

    /// <summary>General I/O error (EIO / ERROR_IO_DEVICE)</summary>
    IOError,

    /// <summary>Operation not supported on this platform or filesystem (ENOTSUP)</summary>
    NotSupported,

    /// <summary>Invalid argument or path (EINVAL / ERROR_INVALID_PARAMETER)</summary>
    InvalidArgument,

    /// <summary>Path is not a directory when directory expected (ENOTDIR)</summary>
    NotADirectory,

    /// <summary>Path is not a symlink when symlink expected (EINVAL for readlink)</summary>
    NotASymlink,

    /// <summary>Unknown or unmapped error</summary>
    Unknown
}