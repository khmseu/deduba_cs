using System.Text;
using System.Text.Json.Nodes;
using ArchiveDataHandler;
using OsCallsCommon;

namespace OsCallsWindows;

/// <summary>
///     Windows implementation stub of IHighLevelOsApi.
///     TODO: Implement using Windows-specific APIs (security descriptors, alternate data streams).
/// </summary>
public class WindowsHighLevelOsApi : IHighLevelOsApi
{
    private static readonly Lazy<WindowsHighLevelOsApi> _instance = new(() =>
        new WindowsHighLevelOsApi()
    );

    /// <summary>
    ///     Default singleton instance for the Windows high-level OS API.
    /// </summary>
    public static IHighLevelOsApi Instance => _instance.Value;

    /// <summary>
    ///     Creates a minimal InodeData object from a filesystem path containing only
    ///     stat information (no security descriptors, alternate data streams, or content hashes).
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A minimal <see cref="InodeData" /> instance with stat information only.</returns>
    /// <exception cref="OsException">Thrown on permission denied, not found, or I/O errors</exception>
    public InodeData CreateMinimalInodeDataFromPath(string path)
    {
        var statBuf = FileSystem.LStat(path);
        var statObj = statBuf as JsonObject;

        // Determine textual flags (reg/dir/lnk etc.) from S_IS* or S_TYPEIS* booleans
        var flags = new HashSet<string>();
        if (statObj is not null)
            foreach (var kvp in statObj)
            {
                var key = kvp.Key;
                if (!key.StartsWith("S_IS") && !key.StartsWith("S_TYPEIS"))
                    continue;

                if (kvp.Value?.GetValue<bool>() != true)
                    continue;

                var flagName = key.StartsWith("S_TYPEIS")
                    ? key[8..].ToLowerInvariant()
                    : key[4..].ToLowerInvariant();
                flags.Add(flagName);
            }

        return new InodeData
        {
            Device = statObj?["st_dev"]?.GetValue<long>() ?? 0,
            FileIndex = statObj?["st_ino"]?.GetValue<long>() ?? 0,
            Mode = statObj?["st_mode"]?.GetValue<long>() ?? 0,
            Flags = flags,
            NLink = statObj?["st_nlink"]?.GetValue<long>() ?? 0,
            Uid = statObj?["st_uid"]?.GetValue<long>() ?? 0,
            Gid = statObj?["st_gid"]?.GetValue<long>() ?? 0,
            RDev = statObj?["st_rdev"]?.GetValue<long>() ?? 0,
            Size = statObj?["st_size"]?.GetValue<long>() ?? 0,
            MTime = statObj?["st_mtim"]?.GetValue<double>() ?? 0,
            CTime = statObj?["st_ctim"]?.GetValue<double>() ?? 0,
            Acl = [],
            Xattr = [],
            Hashes = []
        };
    }

    /// <summary>
    ///     Completes an existing <see cref="InodeData" /> instance with security descriptors and content hashes for the
    ///     specified path.
    ///     Uses <paramref name="archiveStore" /> to persist auxiliary data streams.
    ///     Resolves user and group names (best-effort on Windows).
    /// </summary>
    /// <param name="path">Filesystem path to read metadata from.</param>
    /// <param name="data">Reference to an existing <see cref="InodeData" /> to complete.</param>
    /// <param name="archiveStore">Archive store used to save auxiliary data streams.</param>
    /// <returns>Completed <see cref="InodeData" /> instance.</returns>
    public InodeData CompleteInodeDataFromPath(
        string path,
        ref InodeData data,
        IArchiveStore archiveStore
    )
    {
        ArgumentNullException.ThrowIfNull(data);

        // Best-effort user/group name resolution on Windows
        // Windows platform may not provide pw/gr info, so use numeric IDs as fallback
        data.UserName = data.Uid != 0 ? data.Uid.ToString() : "0";
        data.GroupName = data.Gid != 0 ? data.Gid.ToString() : "0";

        // Try to capture security descriptor (SDDL) and save via archiveStore
        try
        {
            var sd = Security.GetSecurityDescriptor(path);
            if (sd is JsonObject sdObj && sdObj.ContainsKey("sddl"))
            {
                var sddl = sdObj["sddl"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(sddl))
                {
                    var sdBytes = Encoding.UTF8.GetBytes(sddl);
                    using var ms = new MemoryStream(sdBytes);
                    var sdHashes = archiveStore
                        .SaveStream(ms, sdBytes.Length, $"{path} $acl", _ => { })
                        .ToArray();
                    data.Acl = sdHashes;
                }
            }
        }
        catch
        {
            // Non-fatal: continue without security descriptor
        }

        // Alternate data streams and other Windows-specific metadata
        // would be handled here in a full implementation.

        // Handle file content based on type
        var hashes = new List<string>();
        if (data.Flags.Contains("reg"))
        {
            if (data.Size != 0)
                try
                {
                    using var fs = File.OpenRead(path);
                    hashes = [.. archiveStore.SaveStream(fs, data.Size, path, _ => { })];
                }
                catch (Exception ex)
                {
                    throw new OsException(
                        $"Failed to read file content {path}",
                        ErrorKind.IOError,
                        ex
                    );
                }
        }
        else if (data.Flags.Contains("lnk"))
        {
            try
            {
                var linkNode = FileSystem.ReadLink(path);
                var linkTarget = linkNode?["path"]?.GetValue<string>() ?? string.Empty;
                var linkBytes = Encoding.UTF8.GetBytes(linkTarget);
                using var lm = new MemoryStream(linkBytes);
                hashes =
                [
                    .. archiveStore.SaveStream(
                        lm,
                        linkBytes.Length,
                        $"{path} $data readlink",
                        _ => { }
                    )
                ];
            }
            catch (Exception ex)
            {
                throw new OsException($"Failed to read symlink {path}", ErrorKind.IOError, ex);
            }
        }

        data.Hashes = hashes;
        return data;
    }

    /// <summary>
    ///     List the directory entries for <paramref name="path" /> ordered by
    ///     ordinal string comparison. Wraps <see cref="System.IO.Directory.GetFileSystemEntries(System.String)" />
    ///     and maps system exceptions to <see cref="OsException" />.
    /// </summary>
    /// <param name="path">Directory to list.</param>
    /// <returns>Ordered array of filesystem entries (files and directories).</returns>
    public string[] ListDirectory(string path)
    {
        try
        {
            return
            [
                .. Directory.GetFileSystemEntries(path).OrderBy(e => e, StringComparer.Ordinal)
            ];
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new OsException(
                $"Permission denied listing directory {path}",
                ErrorKind.PermissionDenied,
                ex
            );
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new OsException($"Directory not found {path}", ErrorKind.NotFound, ex);
        }
        catch (Exception ex)
        {
            throw new OsException($"Failed to list directory {path}", ErrorKind.IOError, ex);
        }
    }

    /// <summary>
    ///     Canonicalizes a filesystem path by resolving symlinks and normalizing separators.
    ///     Delegates to the FileSystem module's platform-specific implementation.
    /// </summary>
    /// <param name="path">Path to canonicalize</param>
    /// <returns>A JsonNode containing the canonical path under the "path" key</returns>
    public JsonNode Canonicalizefilename(string path)
    {
        return FileSystem.Canonicalizefilename(path);
    }
}