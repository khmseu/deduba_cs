using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OsCallsCommon;

namespace OsCallsLinux;

/// <summary>
///     Linux implementation of IHighLevelOsApi using POSIX system calls.
///     Wraps existing FileSystem, Acl, Xattr, and UserGroupDatabase static methods.
/// </summary>
public class LinuxHighLevelOsApi : IHighLevelOsApi
{
    /// <summary>Whether the current platform/filesystem exposes POSIX ACLs.</summary>
    public bool HasAclSupport => true;

    /// <summary>Whether the current platform/filesystem exposes extended attributes.</summary>
    public bool HasXattrSupport => true;

    /// <summary>Whether the platform supports security descriptor (Windows-style) objects.</summary>
    public bool HasSecurityDescriptorSupport => false;

    /// <summary>Whether alternate data streams are supported for this platform.</summary>
    public bool HasAlternateStreamSupport => false;

    /// <summary>
    ///     Collects full inode metadata for <paramref name="path"/>, including
    ///     stat, ACLs, xattrs and content hashes where applicable. Uses
    ///     <paramref name="archiveStore"/> to persist any associated auxiliary
    ///     streams (ACL text, xattr values, file content) and returns an
    ///     <see cref="InodeData"/> describing the file.
    /// </summary>
    /// <param name="path">Filesystem path to read metadata from.</param>
    /// <param name="archiveStore">Archive store used to save auxiliary data streams.</param>
    /// <returns>Populated <see cref="InodeData"/> instance.</returns>
    public InodeData CreateInodeDataFromPath(string path, IArchiveStore archiveStore)
    {
        // Get file stat (lstat - does not follow symlinks)
        JsonNode? statBuf;
        try
        {
            statBuf = FileSystem.LStat(path);
            if (statBuf == null)
                throw new OsException($"LStat returned null for {path}", ErrorKind.Unknown);
        }
        catch (Exception ex)
        {
            throw new OsException($"Failed to stat {path}", ErrorKind.IOError, ex);
        }

        // Extract file type flags from S_IS* boolean fields
        var flags = new HashSet<string>();
        if (statBuf is JsonObject statObj)
            foreach (var kvp in statObj)
            {
                var key = kvp.Key;
                if (key.StartsWith("S_IS") || key.StartsWith("S_TYPEIS"))
                    if (kvp.Value?.GetValue<bool>() ?? false)
                    {
                        var flagName = key.StartsWith("S_TYPEIS")
                            ? key[8..].ToLowerInvariant()
                            : key[4..].ToLowerInvariant();
                        flags.Add(flagName);
                    }
            }

        // Extract basic metadata
        var groupId = statBuf["st_gid"]?.GetValue<long>() ?? 0;
        var userId = statBuf["st_uid"]?.GetValue<long>() ?? 0;
        var fileSize = statBuf["st_size"]?.GetValue<long>() ?? 0;

        // Resolve user/group names
        var userName = UserGroupDatabase.GetPwUid(userId)["pw_name"]?.ToString()
                       ?? userId.ToString();
        var groupName = UserGroupDatabase.GetGrGid(groupId)["gr_name"]?.ToString()
                        ?? groupId.ToString();

        // Create base InodeData
        var inodeData = new InodeData
        {
            FileId = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(
                    new List<object?>
                        { statBuf["st_dev"]?.GetValue<long>() ?? 0, statBuf["st_ino"]?.GetValue<long>() ?? 0 }
                )
            ),
            Mode = statBuf["st_mode"]?.GetValue<long>() ?? 0,
            Flags = flags,
            NLink = statBuf["st_nlink"]?.GetValue<long>() ?? 0,
            Uid = userId,
            UserName = userName,
            Gid = groupId,
            GroupName = groupName,
            RDev = statBuf["st_rdev"]?.GetValue<long>() ?? 0,
            Size = fileSize,
            MTime = statBuf["st_mtim"]?.GetValue<double>() ?? 0,
            CTime = statBuf["st_ctim"]?.GetValue<double>() ?? 0
        };

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
                    aclHashes = archiveStore.SaveStream(aclMem, aclBytes.Length, $"{path} $acl", _ => { }).ToArray();
                }
            }

            // For directories, also read default ACL
            if (flags.Contains("dir"))
            {
                var aclDefaultResult = Acl.GetFileDefault(path);
                if (aclDefaultResult is JsonObject aclDefaultObj && aclDefaultObj.ContainsKey("acl_text"))
                {
                    var aclDefaultText = aclDefaultObj["acl_text"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(aclDefaultText))
                    {
                        var aclDefaultBytes = Encoding.UTF8.GetBytes(aclDefaultText);
                        var aclDefaultMem = new MemoryStream(aclDefaultBytes);
                        var defaultHashes = archiveStore.SaveStream(
                            aclDefaultMem,
                            aclDefaultBytes.Length,
                            $"{path} $acl_default",
                            _ => { }
                        ).ToArray();
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
                        if (xattrValueResult is JsonObject xattrValueObj && xattrValueObj.ContainsKey("value"))
                        {
                            var xattrValue = xattrValueObj["value"]?.ToString() ?? "";
                            var xattrBytes = Encoding.UTF8.GetBytes(xattrValue);
                            var xattrMem = new MemoryStream(xattrBytes);
                            var xattrHashList = archiveStore.SaveStream(
                                xattrMem,
                                xattrBytes.Length,
                                $"{path} $xattr:{xattrName}",
                                _ => { }
                            ).ToArray();
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

        inodeData.Acl = aclHashes;
        inodeData.Xattr = xattrHashes;

        // Handle file content based on type
        string[] hashes = [];
        if (flags.Contains("reg"))
        {
            // Regular file - read and hash content
            if (fileSize != 0)
                try
                {
                    using var fileStream = File.OpenRead(path);
                    hashes = archiveStore.SaveStream(fileStream, fileSize, path, _ => { }).ToArray();
                }
                catch (Exception ex)
                {
                    throw new OsException($"Failed to read file content {path}", ErrorKind.IOError, ex);
                }
        }
        else if (flags.Contains("lnk"))
        {
            // Symlink - read target
            try
            {
                var linkNode = FileSystem.ReadLink(path);
                var linkTarget = linkNode?["path"]?.GetValue<string>() ?? string.Empty;
                var linkBytes = Encoding.UTF8.GetBytes(linkTarget);
                var linkMem = new MemoryStream(linkBytes);
                hashes = archiveStore.SaveStream(linkMem, linkBytes.Length, $"{path} $data readlink", _ => { })
                    .ToArray();
            }
            catch (Exception ex)
            {
                throw new OsException($"Failed to read symlink {path}", ErrorKind.IOError, ex);
            }
        }
        else if (flags.Contains("dir"))
        {
            // Directory content handled separately by Backup_worker
            // Just return empty hashes for now
            hashes = [];
        }

        inodeData.Hashes = hashes;
        return inodeData;
    }

    /// <summary>
    ///     List the directory entries for <paramref name="path"/> ordered by
    ///     ordinal string comparison. Wraps <see cref="Directory.GetFileSystemEntries(string)"/>
    ///     and maps system exceptions to <see cref="OsException"/>.
    /// </summary>
    /// <param name="path">Directory to list.</param>
    /// <returns>Ordered array of filesystem entries (files and directories).</returns>
    public string[] ListDirectory(string path)
    {
        try
        {
            return Directory.GetFileSystemEntries(path)
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToArray();
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new OsException($"Permission denied listing directory {path}", ErrorKind.PermissionDenied, ex);
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
}
