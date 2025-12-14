namespace OsCallsCommon;

/// <summary>
///     Exception hierarchy for OS API errors with normalized error kinds.
///     Maps platform-specific errors (errno/HRESULT) to common categories.
/// </summary>
public class OsException : Exception
{
    /// <summary>
    ///     Create a new <see cref="OsException" /> with a human readable
    ///     message, a normalized <see cref="ErrorKind" />, and an optional
    ///     inner exception.
    /// </summary>
    /// <param name="message">A descriptive error message.</param>
    /// <param name="kind">Normalized error category.</param>
    /// <param name="innerException">Optional inner exception that caused this error.</param>
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
    Unknown,
}
