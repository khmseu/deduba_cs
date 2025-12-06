using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ArchiveStore;
using OsCallsCommon;

namespace OsCallsWindows;

/// <summary>
///     Windows implementation stub of IHighLevelOsApi.
///     TODO: Implement using Windows-specific APIs (security descriptors, alternate data streams).
/// </summary>
public class WindowsHighLevelOsApi : IHighLevelOsApi
{
    /// <summary>Whether the Windows implementation exposes ACL support.</summary>
    public bool HasAclSupport => false;

    /// <summary>Whether the Windows implementation exposes extended attribute support.</summary>
    public bool HasXattrSupport => false;

    /// <summary>Whether security descriptor support (Windows ACL objects) is available.</summary>
    public bool HasSecurityDescriptorSupport => true;

    /// <summary>Whether alternate data streams (ADS) are supported on this platform.</summary>
    public bool HasAlternateStreamSupport => true;

    /// <summary>
    ///     Get file status for the supplied path (like Win32 GetFileInformationByHandle),
    ///     without following symlinks/reparse points.
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A JsonNode containing file attributes (st_dev, st_ino, st_mode, timestamps, etc.).</returns>
    /// <exception cref="OsException">Thrown on permission denied, not found, or I/O errors</exception>
    public JsonNode LStat(string path)
    {
        return FileSystem.LStat(path);
    }

    /// <summary>
    ///     Create an <see cref="InodeData" /> for <paramref name="path" /> using
    ///     Windows-specific APIs and persist any auxiliary data via
    ///     <paramref name="archiveStore" />. Not implemented yet.
    /// </summary>
    /// <param name="path">Path to inspect.</param>
    /// <param name="archiveStore">Archive store used to save auxiliary streams.</param>
    /// <returns>Populated <see cref="InodeData" />.</returns>
    /// <exception cref="NotImplementedException">Always; Windows shim not yet implemented.</exception>
    public InodeData CreateInodeDataFromPath(string path, IArchiveStore archiveStore)
    {
        // Minimal Windows implementation mirroring Linux behavior where practical.
        // Uses the native Windows shim (FileSystem) to obtain stat-like fields
        // and Security to obtain an SDDL string when available. Errors are
        // mapped to OsException to preserve test expectations.
        JsonNode? statBuf;
        JsonObject? statObj = null;
        try
        {
            statBuf = FileSystem.LStat(path);
            if (statBuf is null)
                throw new OsException($"LStat returned null for {path}", ErrorKind.Unknown);
            statObj = statBuf as JsonObject;
        }
        catch (Exception ex)
        {
            throw new OsException($"Failed to stat {path}", ErrorKind.IOError, ex);
        }

        // Determine textual flags (reg/dir/lnk etc.) from S_IS* or S_TYPEIS* booleans
        var flags = new HashSet<string>();
        if (statObj is not null)
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

        // Basic numeric fields with safe fallbacks
        var groupId = statObj?["st_gid"]?.GetValue<long>() ?? 0;
        var userId = statObj?["st_uid"]?.GetValue<long>() ?? 0;
        var fileSize = statObj?["st_size"]?.GetValue<long>() ?? 0;

        // Best-effort names; Windows platform may not provide pw/gr info
        var userName = userId != 0 ? userId.ToString() : "0";
        var groupName = groupId != 0 ? groupId.ToString() : "0";

        var inodeData = new InodeData
        {
            FileId = JsonSerializer.Deserialize<JsonElement>(
                JsonSerializer.Serialize(
                    new List<object?>
                    {
                        statObj?["st_dev"]?.GetValue<long>() ?? 0,
                        statObj?["st_ino"]?.GetValue<long>() ?? 0
                    }
                )
            ),
            Mode = statObj?["st_mode"]?.GetValue<long>() ?? 0,
            Flags = flags,
            NLink = statObj?["st_nlink"]?.GetValue<long>() ?? 0,
            Uid = userId,
            UserName = userName,
            Gid = groupId,
            GroupName = groupName,
            RDev = statObj?["st_rdev"]?.GetValue<long>() ?? 0,
            Size = fileSize,
            MTime = statObj?["st_mtim"]?.GetValue<double>() ?? 0,
            CTime = statObj?["st_ctim"]?.GetValue<double>() ?? 0
        };

        // Try to capture security descriptor (SDDL) and save via archiveStore
        try
        {
            var sd = Security.GetSecurityDescriptor(path);
            if (sd is JsonObject sdObj && sdObj.ContainsKey("sddl"))
            {
                var sddl = sdObj["sddl"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(sddl))
                {
                    var aclBytes = Encoding.UTF8.GetBytes(sddl);
                    using var ms = new MemoryStream(aclBytes);
                    var aclHashes = archiveStore
                        .SaveStream(ms, aclBytes.Length, $"{path} $acl", _ => { })
                        .ToArray();
                    inodeData.Acl = aclHashes;
                }
            }
        }
        catch
        {
            // Non-fatal: continue without ACLs
        }

        // No extended attributes handling on Windows implementation for now.

        // Handle content/hash saving for regular files and symlinks
        var hashes = new List<string>();
        if (flags.Contains("reg"))
        {
            if (fileSize != 0)
                try
                {
                    using var fs = File.OpenRead(path);
                    hashes = archiveStore.SaveStream(fs, fileSize, path, _ => { }).ToList();
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
        else if (flags.Contains("lnk"))
        {
            try
            {
                var linkNode = FileSystem.ReadLink(path);
                var linkTarget = linkNode?["path"]?.GetValue<string>() ?? string.Empty;
                var linkBytes = Encoding.UTF8.GetBytes(linkTarget);
                using var lm = new MemoryStream(linkBytes);
                hashes = archiveStore
                    .SaveStream(lm, linkBytes.Length, $"{path} $data readlink", _ => { })
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new OsException($"Failed to read symlink {path}", ErrorKind.IOError, ex);
            }
        }

        inodeData.Hashes = hashes;
        return inodeData;
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
            return Directory
                .GetFileSystemEntries(path)
                .OrderBy(e => e, StringComparer.Ordinal)
                .ToArray();
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