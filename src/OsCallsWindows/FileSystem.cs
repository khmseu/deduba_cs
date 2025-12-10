using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;
using UtilitiesLibrary;
using static OsCallsCommon.ValXfer;

namespace OsCallsWindows;

/// <summary>
///     Thin P/Invoke wrapper around native Windows filesystem calls exposed by OsCallsWindowsShim.dll.
///     Converts native iterator-style ValueT streams into JSON nodes via <see cref="ValXfer.ToNode" />.
/// </summary>
public static unsafe partial class FileSystem
{
    /// <summary>
    /// Instance logger for this module. Replaceable for tests; defaults to adapter.
    /// </summary>
    public static UtilitiesLibrary.ILogging Logger { get; set; } =
        UtilitiesLibrary.UtilitiesLogger.Instance;
    private const string NativeLibraryName = "OsCallsWindowsShimNative.dll";

    static FileSystem()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(FileSystem).Assembly, Resolver);
            if (Logger.IsNativeDebugEnabled())
                Logger.ConWrite(
                    $"OsCallsWindows.FileSystem resolver registered: searching for '{NativeLibraryName}'"
                );
            // Preload to improve deterministic behavior in CI where PATH changes later.
            _ = Resolver(NativeLibraryName, typeof(FileSystem).Assembly, null);
        }
        catch
        {
            // Swallow - errors surfaced when the P/Invoke is actually invoked
        }
    }

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* windows_GetFileInformationByHandle(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* windows_DeviceIoControl_GetReparsePoint(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* windows_GetFinalPathNameByHandleW(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* win_lstat(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValueT* win_readlink(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
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

    /// <summary>
    ///     Gets file information using Windows GetFileInformationByHandle API.
    ///     Primary implementation - wraps windows_GetFileInformationByHandle native export.
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A JsonObject containing file attributes (size, timestamps, attributes, file ID, etc.).</returns>
    public static JsonNode WindowsGetFileInformationByHandle(string path)
    {
        return ToNode(
            windows_GetFileInformationByHandle(path),
            path,
            nameof(windows_GetFileInformationByHandle)
        );
    }

    /// <summary>
    ///     Reads reparse point data using Windows DeviceIoControl with FSCTL_GET_REPARSE_POINT.
    ///     Primary implementation - wraps windows_DeviceIoControl_GetReparsePoint native export.
    /// </summary>
    /// <param name="path">Path of the reparse point to read.</param>
    /// <returns>A JsonNode with reparse type and target information.</returns>
    public static JsonNode WindowsDeviceIoControlGetReparsePoint(string path)
    {
        return ToNode(
            windows_DeviceIoControl_GetReparsePoint(path),
            path,
            nameof(windows_DeviceIoControl_GetReparsePoint)
        );
    }

    /// <summary>
    ///     Gets canonical path using Windows GetFinalPathNameByHandleW API.
    ///     Primary implementation - wraps windows_GetFinalPathNameByHandleW native export.
    /// </summary>
    /// <param name="path">Original input path.</param>
    /// <returns>A JsonNode with a <c>path</c> field set to the canonical path.</returns>
    public static JsonNode WindowsGetFinalPathNameByHandleW(string path)
    {
        return ToNode(
            windows_GetFinalPathNameByHandleW(path),
            path,
            nameof(windows_GetFinalPathNameByHandleW)
        );
    }

    private static IntPtr Resolver(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath
    )
    {
        if (!string.Equals(libraryName, NativeLibraryName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;
        try
        {
            if (Logger.IsNativeDebugEnabled())
                Logger.ConWrite(
                    $"Resolver: loading {libraryName} for assembly {assembly?.GetName()?.Name} searchPath={searchPath}"
                );
            var full = FindNativeLibraryPath(NativeLibraryName);
            if (full is not null)
            {
                if (Logger.IsNativeDebugEnabled())
                    Logger.ConWrite($"Resolver: Found native path {full}");
                try
                {
                    return NativeLibrary.Load(full);
                }
                catch (Exception e)
                {
                    if (Logger.IsNativeDebugEnabled())
                        Logger.ConWrite(
                            $"Resolver: NativeLibrary.Load failed for {full}: {e.Message}"
                        );
                    return IntPtr.Zero;
                }
            }
        }
        catch (Exception e)
        {
            if (Logger.IsNativeDebugEnabled())
                Logger.ConWrite($"Resolver error: {e.Message}");
        }

        return IntPtr.Zero;
    }

    private static string? FindNativeLibraryPath(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        var colocated = Path.Combine(baseDir, fileName);
        if (File.Exists(colocated))
            return colocated;
        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidateDebug = Path.Combine(
                dir.FullName,
                "OsCallsWindowsShim",
                "bin",
                "Debug",
                "OsCallsWindowsShimNative.dll"
            );
            if (File.Exists(candidateDebug))
                return candidateDebug;
            var candidateRelease = Path.Combine(
                dir.FullName,
                "OsCallsWindowsShim",
                "bin",
                "Release",
                "OsCallsWindowsShimNative.dll"
            );
            if (File.Exists(candidateRelease))
                return candidateRelease;
            var candidateBuildDir = Path.Combine(
                dir.FullName,
                "OsCallsWindowsShim",
                "build-win-x64",
                "bin",
                "Debug",
                "OsCallsWindowsShimNative.dll"
            );
            if (File.Exists(candidateBuildDir))
                return candidateBuildDir;
            candidateBuildDir = Path.Combine(
                dir.FullName,
                "OsCallsWindowsShim",
                "build-win-x64",
                "bin",
                "Release",
                "OsCallsWindowsShimNative.dll"
            );
            if (File.Exists(candidateBuildDir))
                return candidateBuildDir;
            dir = dir.Parent;
        }

        return null;
    }

    // Additional Windows-specific methods will be added here:
    // - ListStreams (Alternate Data Streams)
    // - GetStream (read ADS content)
    // - ListHardlinks (enumerate hardlink paths)
}
