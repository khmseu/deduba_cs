using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;
using static OsCallsCommon.ValXfer;

namespace OsCallsWindows;

/// <summary>
///     Thin P/Invoke wrapper around native Windows filesystem calls exposed by OsCallsWindowsShim.dll.
///     Converts native iterator-style ValueT streams into JSON nodes via <see cref="ValXfer.ToNode" />.
/// </summary>
public static unsafe partial class FileSystem
{
    [LibraryImport("OsCallsWindowsShimNative.dll", StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* win_lstat(string path);

    [LibraryImport("OsCallsWindowsShimNative.dll", StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* win_readlink(string path);

    [LibraryImport("OsCallsWindowsShimNative.dll", StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* win_canonicalize_file_name(string path);

    /// <summary>
    ///     Gets file status for the supplied path (like Win32 GetFileAttributesEx), without following reparse points.
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A JsonObject containing file attributes (size, timestamps, attributes, file ID, etc.).</returns>
    public static JsonNode LStat(string path)
    {
        return ToNode(win_lstat(path), path, nameof(win_lstat));
    }

    /// <summary>
    ///     Reads the target of a reparse point (symlink/junction/mount point).
    /// </summary>
    /// <param name="path">Path of the reparse point to read.</param>
    /// <returns>A JsonNode with reparse type and target information.</returns>
    public static JsonNode ReadLink(string path)
    {
        return ToNode(win_readlink(path), path, nameof(win_readlink));
    }

    /// <summary>
    ///     Resolves a path to its canonical absolute form using GetFinalPathNameByHandle.
    /// </summary>
    /// <param name="path">Original input path.</param>
    /// <returns>A JsonNode with a <c>path</c> field set to the canonical path.</returns>
    public static JsonNode Canonicalizefilename(string path)
    {
        return ToNode(win_canonicalize_file_name(path), path, nameof(win_canonicalize_file_name));
    }

    // Additional Windows-specific methods will be added here:
    // - ListStreams (Alternate Data Streams)
    // - GetStream (read ADS content)
    // - ListHardlinks (enumerate hardlink paths)
}
