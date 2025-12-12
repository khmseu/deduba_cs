using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;
using UtilitiesLibrary;
using static OsCallsCommon.ValXfer;

namespace OsCallsLinux;

/// <summary>
///     Thin P/Invoke wrapper around native POSIX filesystem calls exposed by libOsCallsLinuxShim.so.
///     Converts native iterator-style ValueT streams into JSON nodes via <see cref="ValXfer.ToNode" />.
/// </summary>
public static unsafe partial class FileSystem
{
    private const string NativeLibraryName = "libOsCallsLinuxShim.so";

    private static readonly ShimFnDelegate? _linux_lstat_fn;
    private static readonly ShimFnDelegate? _linux_readlink_fn;
    private static readonly ShimFnDelegate? _linux_cfn_fn;

    static FileSystem()
    {
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(FileSystem).Assembly, Resolver);
            if (Logger!.IsNativeDebugEnabled())
                Logger!.ConWrite(
                    $"OsCallsLinux.FileSystem resolver registered: searching for '{NativeLibraryName}'"
                );
            // Proactively load native library so first P/Invoke succeeds even if environment vars were set too late.
            _ = Resolver(NativeLibraryName, typeof(FileSystem).Assembly, null);
            // Best-effort: try to bind linux_* exports if present and create delegates.
            try
            {
                var full = FindNativeLibraryPath(NativeLibraryName);
                if (!string.IsNullOrWhiteSpace(full))
                {
                    var handle = NativeLibrary.Load(full);
                    if (NativeLibrary.TryGetExport(handle, "linux_lstat", out var ptr))
                        _linux_lstat_fn = Marshal.GetDelegateForFunctionPointer<ShimFnDelegate>(
                            ptr
                        );
                    if (NativeLibrary.TryGetExport(handle, "linux_readlink", out ptr))
                        _linux_readlink_fn = Marshal.GetDelegateForFunctionPointer<ShimFnDelegate>(
                            ptr
                        );
                    if (NativeLibrary.TryGetExport(handle, "linux_canonicalize_file_name", out ptr))
                        _linux_cfn_fn = Marshal.GetDelegateForFunctionPointer<ShimFnDelegate>(ptr);
                }
            }
            catch
            {
                // ignore — fallback to P/Invoke will be used
            }
        }
        catch
        {
            // Swallow; detailed errors surfaced when actual P/Invoke attempted.
        }
    }

    /// <summary>
    ///     Instance logger for this module. Replaceable for tests; must be injected by host.
    /// </summary>
    public static ILogging Logger { get; set; } = default!;

    /// <summary>
    ///     Injects a concrete logger instance for this module.
    /// </summary>
    /// <param name="logger">Non-null logger implementation.</param>
    public static void InjectLogger(ILogging logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static IntPtr Resolver(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath
    )
    {
        if (libraryName != NativeLibraryName)
            return IntPtr.Zero;
        try
        {
            if (Logger!.IsNativeDebugEnabled())
                Logger!.ConWrite(
                    $"Resolver: loading {libraryName} for assembly {assembly?.GetName()?.Name} searchPath={searchPath}"
                );
        }
        catch
        {
            // ignore
        }

        var full = FindNativeLibraryPath(NativeLibraryName);
        if (full is null)
            return IntPtr.Zero;
        try
        {
            // Preload dependency libOsCallsCommonShim.so first if present
            var libDir = new FileInfo(full).Directory; // Directory containing libOsCallsLinuxShim.so

            // Check for libOsCallsCommonShim.so in same directory first (test scenario)
            var colocated = Path.Combine(libDir!.FullName, "libOsCallsCommonShim.so");
            if (File.Exists(colocated))
                try
                {
                    NativeLibrary.Load(colocated);
                    return NativeLibrary.Load(full);
                }
                catch
                {
                    /* fallback to walking up */
                }

            // Otherwise, walk up to solution root and find it in build output
            var projectRoot = libDir?.Parent?.Parent?.Parent?.Parent; // ascend to solution root
            if (projectRoot is not null)
            {
                var commonShimDebug = Path.Combine(
                    projectRoot.FullName,
                    "OsCallsCommonShim",
                    "bin",
                    "Debug",
                    "net8.0",
                    "libOsCallsCommonShim.so"
                );
                var commonShimRelease = Path.Combine(
                    projectRoot.FullName,
                    "OsCallsCommonShim",
                    "bin",
                    "Release",
                    "net8.0",
                    "libOsCallsCommonShim.so"
                );
                foreach (var dep in new[] { commonShimDebug, commonShimRelease })
                    if (File.Exists(dep))
                        try
                        {
                            NativeLibrary.Load(dep);
                            break;
                        }
                        catch
                        {
                            /* ignore */
                        }

                // If still not resolved by dynamic loader, append common shim directory to LD_LIBRARY_PATH and retry.
                var commonDir =
                    File.Exists(commonShimDebug) ? Path.GetDirectoryName(commonShimDebug)!
                    : File.Exists(commonShimRelease) ? Path.GetDirectoryName(commonShimRelease)!
                    : null;
                if (commonDir is not null)
                {
                    var ld = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH") ?? string.Empty;
                    if (!ld.Split(':').Contains(commonDir))
                    {
                        var newLd = string.IsNullOrWhiteSpace(ld)
                            ? commonDir
                            : commonDir + ":" + ld;
                        Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", newLd);
                    }
                }
            }

            return NativeLibrary.Load(full);
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    private static string? FindNativeLibraryPath(string fileName)
    {
        // First check if library was copied to same directory (e.g., during tests)
        var baseDir = AppContext.BaseDirectory;
        var colocated = Path.Combine(baseDir, fileName);
        if (File.Exists(colocated))
            return colocated;

        // Start at base directory (bin/<config>/net8.0[/RID]) and walk up looking for project folder.
        var dir = new DirectoryInfo(baseDir);
        for (var i = 0; i < 8 && dir is not null; i++)
        {
            var candidateDebug = Path.Combine(
                dir.FullName,
                "OsCallsLinuxShim",
                "bin",
                "Debug",
                "net8.0",
                fileName
            );
            if (File.Exists(candidateDebug))
                return candidateDebug;
            var candidateRelease = Path.Combine(
                dir.FullName,
                "OsCallsLinuxShim",
                "bin",
                "Release",
                "net8.0",
                fileName
            );
            if (File.Exists(candidateRelease))
                return candidateRelease;
            dir = dir.Parent;
        }

        return null;
    }

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValueT* lstat(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValueT* readlink(string path);

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf8)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValueT* canonicalize_file_name(string path);

    /// <summary>
    ///     Gets file status for the supplied path (like POSIX lstat), without following symlinks.
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A JsonObject containing stat fields (st_dev, st_ino, st_mode, ...).</returns>
    public static JsonNode LStat(string path)
    {
        return LinuxLStat(path);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for calling the LStat functionality: preferred naming for OS-specific wrappers.
    ///     This mirrors <see cref="LStat(string)" /> and is intended for direct use by OS-prefixed API code.
    /// </summary>
    public static JsonNode LinuxLStat(string path)
    {
        if (_linux_lstat_fn is not null)
        {
            var ptr = _linux_lstat_fn(path);
            return ToNode((ValueT*)ptr, path, "linux_lstat");
        }

        return ToNode(lstat(path), path, nameof(lstat));
    }

    /// <summary>
    ///     Reads the target of a symbolic link.
    /// </summary>
    /// <param name="path">Path of the symlink to read.</param>
    /// <returns>A JsonNode with a <c>path</c> field set to the symlink target string.</returns>
    public static JsonNode ReadLink(string path)
    {
        return LinuxReadLink(path);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for ReadLink.
    /// </summary>
    public static JsonNode LinuxReadLink(string path)
    {
        if (_linux_readlink_fn is not null)
        {
            var ptr = _linux_readlink_fn(path);
            return ToNode((ValueT*)ptr, path, "linux_readlink");
        }

        return ToNode(readlink(path), path, nameof(readlink));
    }

    /// <summary>
    ///     Resolves a path to its canonical absolute form (resolving symlinks and relative segments).
    /// </summary>
    /// <param name="path">Original input path.</param>
    /// <returns>A JsonNode with a <c>path</c> field set to the canonical path.</returns>
    public static JsonNode Canonicalizefilename(string path)
    {
        return LinuxCanonicalizeFileName(path);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for canonicalize_file_name.
    /// </summary>
    public static JsonNode LinuxCanonicalizeFileName(string path)
    {
        if (_linux_cfn_fn is not null)
        {
            var ptr = _linux_cfn_fn(path);
            return ToNode((ValueT*)ptr, path, "linux_canonicalize_file_name");
        }

        return ToNode(canonicalize_file_name(path), path, nameof(canonicalize_file_name));
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ShimFnDelegate([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    // Inlined former convenience predicates (IsDir/IsReg/IsLnk) directly at call sites for minor perf/readability tweaks.
}
