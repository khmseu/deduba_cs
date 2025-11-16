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
    /// <summary>
    ///     Reads the access ACL from the specified filesystem path.
    ///     Returns the ACL in short text format (e.g., "u::rwx,g::r-x,o::r--").
    /// </summary>
    /// <param name="path">Filesystem path to read ACL from.</param>
    /// <returns>JsonNode with "acl_text" field containing the ACL string, or error number.</returns>
    public static JsonNode GetFileAccess(string path)
    {
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
        return ValXfer.ToNode(acl_get_file_default(path), path, nameof(acl_get_file_default));
    }

    [LibraryImport("libOsCallsLinuxShim.so", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* acl_get_file_access(string path);

    [LibraryImport("libOsCallsLinuxShim.so", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* acl_get_file_default(string path);
}
