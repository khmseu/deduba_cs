using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using static OsCalls.ValXfer;

namespace OsCalls;

/// <summary>
///     Thin P/Invoke wrapper around native POSIX filesystem calls exposed by libOsCallsShim.so.
///     Converts native iterator-style ValueT streams into JSON nodes via <see cref="ValXfer.ToNode" />.
/// </summary>
public static unsafe class FileSystem
{
    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* lstat(string path);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* readlink(string path);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* canonicalize_file_name(string path);

    /// <summary>
    ///     Gets file status for the supplied path (like POSIX lstat), without following symlinks.
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A JsonObject containing stat fields (st_dev, st_ino, st_mode, ...).</returns>
    public static JsonNode LStat(string path)
    {
        return ToNode(lstat(path), path, nameof(lstat));
    }

    /// <summary>
    ///     Reads the target of a symbolic link.
    /// </summary>
    /// <param name="path">Path of the symlink to read.</param>
    /// <returns>A JsonNode with a <c>path</c> field set to the symlink target string.</returns>
    public static JsonNode ReadLink(string path)
    {
        return ToNode(readlink(path), path, nameof(readlink));
    }

    /// <summary>
    ///     Resolves a path to its canonical absolute form (resolving symlinks and relative segments).
    /// </summary>
    /// <param name="path">Original input path.</param>
    /// <returns>A JsonNode with a <c>path</c> field set to the canonical path.</returns>
    public static JsonNode Canonicalizefilename(string path)
    {
        return ToNode(canonicalize_file_name(path), path, nameof(canonicalize_file_name));
    }

    // Inlined former convenience predicates (IsDir/IsReg/IsLnk) directly at call sites for minor perf/readability tweaks.
}
