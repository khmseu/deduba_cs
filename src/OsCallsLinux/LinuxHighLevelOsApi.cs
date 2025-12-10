using System.Text;
using System.Text.Json.Nodes;
using ArchiveDataHandler;
using OsCallsCommon;

namespace OsCallsLinux;

/// <summary>
///     Linux implementation of IHighLevelOsApi using POSIX system calls.
///     Wraps existing FileSystem, Acl, Xattr, and UserGroupDatabase static methods.
/// </summary>
public class LinuxHighLevelOsApi : IHighLevelOsApi
{
    private static readonly Lazy<LinuxHighLevelOsApi> _instance = new(() =>
        new LinuxHighLevelOsApi()
    );

    /// <summary>
    /// Default singleton instance for the Linux high-level OS API.
    /// </summary>
    public static IHighLevelOsApi Instance => _instance.Value;

    /// <summary>
    ///     Creates a minimal InodeData object from a filesystem path containing only
    ///     stat information (no ACLs, xattrs, or content hashes).
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A minimal <see cref="InodeData" /> instance with stat information only.</returns>
    /// <exception cref="OsException">Thrown on permission denied, not found, or I/O errors</exception>
    public InodeData CreateMinimalInodeDataFromPath(string path)
    {
        var statBuf = FileSystem.LStat(path);
        var flags = new HashSet<string>();
        if (statBuf is JsonObject statObj)
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
            Device = statBuf["st_dev"]?.GetValue<long>() ?? 0,
            FileIndex = statBuf["st_ino"]?.GetValue<long>() ?? 0,
            Mode = statBuf["st_mode"]?.GetValue<long>() ?? 0,
            Flags = flags,
            NLink = statBuf["st_nlink"]?.GetValue<long>() ?? 0,
            Uid = statBuf["st_uid"]?.GetValue<long>() ?? 0,
            Gid = statBuf["st_gid"]?.GetValue<long>() ?? 0,
            RDev = statBuf["st_rdev"]?.GetValue<long>() ?? 0,
            Size = statBuf["st_size"]?.GetValue<long>() ?? 0,
            MTime = statBuf["st_mtim"]?.GetValue<double>() ?? 0,
            CTime = statBuf["st_ctim"]?.GetValue<double>() ?? 0,
            Acl = [],
            Xattr = [],
            Hashes = [],
        };
    }

    /// <summary>
    ///     Completes an existing <see cref="InodeData" /> instance with ACLs, xattrs, and content hashes for the specified
    ///     path.
    ///     Uses <paramref name="archiveStore" /> to persist auxiliary data streams.
    ///     Resolves user and group names based on UID/GID.
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

        // Resolve user/group names based on pre-initialized uid/gid
        data.UserName =
            UserGroupDatabase.GetPwUid(data.Uid)["pw_name"]?.ToString() ?? data.Uid.ToString();
        data.GroupName =
            UserGroupDatabase.GetGrGid(data.Gid)["gr_name"]?.ToString() ?? data.Gid.ToString();

        // Read ACLs
        string[] aclHashes = [];
        try
        {
            var aclAccessResult = Acl.GetFileAccess(path);
            if (aclAccessResult is JsonObject aclAccessObj && aclAccessObj.ContainsKey("acl_text"))
            {
                var aclText = aclAccessObj["acl_text"]?.ToString() ?? "";
                if (!string.IsNullOrEmpty(aclText))
                {
                    var aclBytes = Encoding.UTF8.GetBytes(aclText);
                    var aclMem = new MemoryStream(aclBytes);
                    aclHashes =
                    [
                        .. archiveStore.SaveStream(
                            aclMem,
                            aclBytes.Length,
                            $"{path} $acl",
                            _ => { }
                        ),
                    ];
                }
            }

            // For directories, also read default ACL
            if (data.Flags.Contains("dir"))
            {
                var aclDefaultResult = Acl.GetFileDefault(path);
                if (
                    aclDefaultResult is JsonObject aclDefaultObj
                    && aclDefaultObj.ContainsKey("acl_text")
                )
                {
                    var aclDefaultText = aclDefaultObj["acl_text"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(aclDefaultText))
                    {
                        var aclDefaultBytes = Encoding.UTF8.GetBytes(aclDefaultText);
                        var aclDefaultMem = new MemoryStream(aclDefaultBytes);
                        var defaultHashes = archiveStore
                            .SaveStream(
                                aclDefaultMem,
                                aclDefaultBytes.Length,
                                $"{path} $acl_default",
                                _ => { }
                            )
                            .ToArray();
                        aclHashes = [.. aclHashes, .. defaultHashes];
                    }
                }
            }
        }
        catch (Exception)
        {
            // ACL reading may fail - not fatal, continue with empty ACLs
        }

        // Read extended attributes
        Dictionary<string, IEnumerable<string>> xattrHashes = [];
        try
        {
            var xattrListResult = Xattr.ListXattr(path);
            if (xattrListResult is JsonArray xattrArray)
                foreach (var xattrNameNode in xattrArray)
                {
                    var xattrName = xattrNameNode?.ToString();
                    if (string.IsNullOrEmpty(xattrName))
                        continue;

                    try
                    {
                        var xattrValueResult = Xattr.GetXattr(path, xattrName);
                        if (
                            xattrValueResult is JsonObject xattrValueObj
                            && xattrValueObj.ContainsKey("value")
                        )
                        {
                            var xattrValue = xattrValueObj["value"]?.ToString() ?? "";
                            var xattrBytes = Encoding.UTF8.GetBytes(xattrValue);
                            var xattrMem = new MemoryStream(xattrBytes);
                            var xattrHashList = archiveStore
                                .SaveStream(
                                    xattrMem,
                                    xattrBytes.Length,
                                    $"{path} $xattr:{xattrName}",
                                    _ => { }
                                )
                                .ToArray();
                            xattrHashes[xattrName] = xattrHashList;
                        }
                    }
                    catch (Exception)
                    {
                        // Individual xattr reading may fail - continue
                    }
                }
        }
        catch (Exception)
        {
            // Xattr listing may fail - not fatal, continue with empty xattrs
        }

        data.Acl = aclHashes;
        data.Xattr = xattrHashes;

        // Handle file content based on type
        string[] hashes = [];
        if (data.Flags.Contains("reg"))
        {
            // Regular file - read and hash content
            if (data.Size != 0)
                try
                {
                    using var fileStream = File.OpenRead(path);
                    hashes = [.. archiveStore.SaveStream(fileStream, data.Size, path, _ => { })];
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
            // Symlink - read target
            try
            {
                var linkNode = FileSystem.ReadLink(path);
                var linkTarget = linkNode?["path"]?.GetValue<string>() ?? string.Empty;
                var linkBytes = Encoding.UTF8.GetBytes(linkTarget);
                var linkMem = new MemoryStream(linkBytes);
                hashes =
                [
                    .. archiveStore.SaveStream(
                        linkMem,
                        linkBytes.Length,
                        $"{path} $data readlink",
                        _ => { }
                    ),
                ];
            }
            catch (Exception ex)
            {
                throw new OsException($"Failed to read symlink {path}", ErrorKind.IOError, ex);
            }
        }
        else if (data.Flags.Contains("dir"))
        {
            // Directory content handled separately by Backup_worker
            // Just return empty hashes for now
            hashes = [];
        }

        data.Hashes = hashes;
        return data;
    }

    /// <summary>
    ///     List the directory entries for <paramref name="path" /> ordered by
    ///     ordinal string comparison. Wraps <see cref="Directory.GetFileSystemEntries(string)" />
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
                .. Directory.GetFileSystemEntries(path).OrderBy(e => e, StringComparer.Ordinal),
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
