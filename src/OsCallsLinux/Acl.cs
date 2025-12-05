using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;

namespace OsCallsLinux;

/// <summary>
///     Wrapper for native ACL (Access Control List) reading functions.
///     Provides methods to read POSIX ACLs in short text format.
/// </summary>
public static unsafe partial class Acl
{
    private const string NativeLibraryName = "libOsCallsLinuxShim.so";

    private static readonly ShimAclDelegate? _linux_acl_get_file_access;
    private static readonly ShimAclDelegate? _linux_acl_get_file_default;

    static Acl()
    {
        try
        {
            var full = FindNative();
            if (!string.IsNullOrWhiteSpace(full))
            {
                var handle = NativeLibrary.Load(full);
                if (NativeLibrary.TryGetExport(handle, "linux_acl_get_file_access", out var p))
                    _linux_acl_get_file_access =
                        Marshal.GetDelegateForFunctionPointer<ShimAclDelegate>(p);
                if (NativeLibrary.TryGetExport(handle, "linux_acl_get_file_default", out p))
                    _linux_acl_get_file_default =
                        Marshal.GetDelegateForFunctionPointer<ShimAclDelegate>(p);
            }
        }
        catch { }
    }

    /// <summary>
    ///     Reads the access ACL from the specified filesystem path.
    ///     Returns the ACL in short text format (e.g., "u::rwx,g::r-x,o::r--").
    /// </summary>
    /// <param name="path">Filesystem path to read ACL from.</param>
    /// <returns>JsonNode with "acl_text" field containing the ACL string, or error number.</returns>
    public static JsonNode GetFileAccess(string path)
    {
        return LinuxGetFileAccess(path);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for GetFileAccess.
    /// </summary>
    public static JsonNode LinuxGetFileAccess(string path)
    {
        if (_linux_acl_get_file_access is not null)
        {
            var ptr = _linux_acl_get_file_access(path);
            return ValXfer.ToNode((ValXfer.ValueT*)ptr, path, "linux_acl_get_file_access");
        }

        return ValXfer.ToNode(acl_get_file_access(path), path, nameof(acl_get_file_access));
    }

    /// <summary>
    ///     Reads the default ACL from the specified filesystem path (directory).
    ///     Returns the ACL in short text format.
    /// </summary>
    /// <param name="path">Filesystem path (must be a directory).</param>
    /// <returns>JsonNode with "acl_text" field containing the ACL string, or error number.</returns>
    public static JsonNode GetFileDefault(string path)
    {
        return LinuxGetFileDefault(path);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for GetFileDefault.
    /// </summary>
    public static JsonNode LinuxGetFileDefault(string path)
    {
        if (_linux_acl_get_file_default is not null)
        {
            var ptr = _linux_acl_get_file_default(path);
            return ValXfer.ToNode((ValXfer.ValueT*)ptr, path, "linux_acl_get_file_default");
        }

        return ValXfer.ToNode(acl_get_file_default(path), path, nameof(acl_get_file_default));
    }

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* acl_get_file_access(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* acl_get_file_default(string path);

    private static string? FindNative()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidateDebug = Path.Combine(
            baseDir,
            "OsCallsLinuxShim",
            "bin",
            "Debug",
            "net8.0",
            NativeLibraryName
        );
        var candidateRelease = Path.Combine(
            baseDir,
            "OsCallsLinuxShim",
            "bin",
            "Release",
            "net8.0",
            NativeLibraryName
        );
        if (File.Exists(candidateDebug))
            return candidateDebug;
        if (File.Exists(candidateRelease))
            return candidateRelease;
        return null;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ShimAclDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string path);
}
