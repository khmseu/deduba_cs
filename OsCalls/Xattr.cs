using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace OsCalls;

/// <summary>
///     Wrapper for native extended attributes (xattr) reading functions.
///     Provides methods to list and read extended attributes from filesystem paths.
/// </summary>
public static unsafe partial class Xattr
{
    /// <summary>
    ///     Lists all extended attribute names for the specified path (not following symlinks).
    /// </summary>
    /// <param name="path">Filesystem path to read xattrs from.</param>
    /// <returns>JsonArray containing the names of all extended attributes, or error.</returns>
    public static JsonNode ListXattr(string path)
    {
        return ValXfer.ToNode(llistxattr(path), path, nameof(llistxattr));
    }

    /// <summary>
    ///     Gets the value of a specific extended attribute (not following symlinks).
    /// </summary>
    /// <param name="path">Filesystem path to read xattr from.</param>
    /// <param name="name">Name of the extended attribute to retrieve.</param>
    /// <returns>JsonNode with "value" field containing the attribute value as a string, or error.</returns>
    public static JsonNode GetXattr(string path, string name)
    {
        return ValXfer.ToNode(lgetxattr(path, name), path, nameof(lgetxattr));
    }

    [LibraryImport("libOsCallsShim.so", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* llistxattr(string path);

    [LibraryImport("libOsCallsShim.so", StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* lgetxattr(string path, string name);
}
