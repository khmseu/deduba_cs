using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;

namespace OsCallsWindows;

/// <summary>
///     Wrapper for Alternate Data Streams (ADS) enumeration and reading.
///     ADS are NTFS-specific metadata streams attached to files.
/// </summary>
public static unsafe partial class Streams
{
    private const string NativeLibraryName = "OsCallsWindowsShimNative.dll";

    /// <summary>
    ///     Lists all alternate data streams for the specified path.
    /// </summary>
    /// <param name="path">Filesystem path to enumerate streams from.</param>
    /// <returns>JsonArray containing stream names and sizes, or error.</returns>
    public static JsonNode ListStreams(string path)
    {
        return ValXfer.ToNode(win_list_streams(path), path, nameof(win_list_streams));
    }

    /// <summary>
    ///     Reads the content of a specific alternate data stream.
    /// </summary>
    /// <param name="path">Filesystem path.</param>
    /// <param name="streamName">Name of the stream (e.g., "Zone.Identifier").</param>
    /// <returns>JsonNode with stream content as bytes or string, or error.</returns>
    public static JsonNode ReadStream(string path, string streamName)
    {
        return ValXfer.ToNode(win_read_stream(path, streamName), path, nameof(win_read_stream));
    }

    /// <summary>
    ///     Enumerates alternate data streams using Windows FindFirstStreamW API.
    ///     Primary implementation - wraps windows_FindFirstStreamW native export.
    /// </summary>
    /// <param name="path">Filesystem path to enumerate streams from.</param>
    /// <returns>JsonArray containing stream names and sizes, or error.</returns>
    public static JsonNode WindowsFindFirstStreamW(string path)
    {
        return ValXfer.ToNode(
            windows_FindFirstStreamW(path),
            path,
            nameof(windows_FindFirstStreamW)
        );
    }

    /// <summary>
    ///     Reads alternate data stream content using Windows CreateFileW and ReadFile APIs.
    ///     Primary implementation - wraps windows_ReadFile_Stream native export.
    /// </summary>
    /// <param name="path">Filesystem path.</param>
    /// <param name="streamName">Name of the stream (e.g., "Zone.Identifier").</param>
    /// <returns>JsonNode with stream content as bytes or string, or error.</returns>
    public static JsonNode WindowsReadFileStream(string path, string streamName)
    {
        return ValXfer.ToNode(
            windows_ReadFile_Stream(path, streamName),
            path,
            nameof(windows_ReadFile_Stream)
        );
    }

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValXfer.ValueT* windows_FindFirstStreamW(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValXfer.ValueT* windows_ReadFile_Stream(string path, string streamName);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValXfer.ValueT* win_list_streams(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValXfer.ValueT* win_read_stream(string path, string streamName);
}
