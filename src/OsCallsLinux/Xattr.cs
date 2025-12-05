using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;

namespace OsCallsLinux;

/// <summary>
///     Wrapper for native extended attributes (xattr) reading functions.
///     Provides methods to list and read extended attributes from filesystem paths.
/// </summary>
public static unsafe partial class Xattr
{
    private const string NativeLibraryName = "libOsCallsLinuxShim.so";

    private static readonly ShimListXattrDelegate? _linux_llistxattr;
    private static readonly ShimGetXattrDelegate? _linux_lgetxattr;

    static Xattr()
    {
        try
        {
            var full = FindNative();
            if (!string.IsNullOrWhiteSpace(full))
            {
                var handle = NativeLibrary.Load(full);
                if (NativeLibrary.TryGetExport(handle, "linux_llistxattr", out var p))
                    _linux_llistxattr =
                        Marshal.GetDelegateForFunctionPointer<ShimListXattrDelegate>(p);
                if (NativeLibrary.TryGetExport(handle, "linux_lgetxattr", out p))
                    _linux_lgetxattr = Marshal.GetDelegateForFunctionPointer<ShimGetXattrDelegate>(
                        p
                    );
            }
        }
        catch
        {
            // ignore - fallback to P/Invoke
        }
    }

    /// <summary>
    ///     Lists all extended attribute names for the specified path (not following symlinks).
    /// </summary>
    /// <param name="path">Filesystem path to read xattrs from.</param>
    /// <returns>JsonArray containing the names of all extended attributes, or error.</returns>
    public static JsonNode ListXattr(string path)
    {
        return LinuxListXattr(path);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for listing xattrs.
    /// </summary>
    public static JsonNode LinuxListXattr(string path)
    {
        if (_linux_llistxattr is not null)
        {
            var ptr = _linux_llistxattr(path);
            return ValXfer.ToNode((ValXfer.ValueT*)ptr, path, "linux_llistxattr");
        }

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
        return LinuxGetXattr(path, name);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for getting a specific xattr.
    /// </summary>
    public static JsonNode LinuxGetXattr(string path, string name)
    {
        if (_linux_lgetxattr is not null)
        {
            var ptr = _linux_lgetxattr(path, name);
            return ValXfer.ToNode((ValXfer.ValueT*)ptr, path, "linux_lgetxattr");
        }

        return ValXfer.ToNode(lgetxattr(path, name), path, nameof(lgetxattr));
    }

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* llistxattr(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValXfer.ValueT* lgetxattr(string path, string name);

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
    private delegate IntPtr ShimListXattrDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    private delegate IntPtr ShimGetXattrDelegate(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name
    );
}
