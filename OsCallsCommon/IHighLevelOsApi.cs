namespace OsCallsCommon;

/// <summary>
///     High-level cross-platform OS API for backup source scanning and metadata collection.
///     Provides a single entry point (CreateInodeDataFromPath) that encapsulates all
///     platform-specific filesystem operations needed for backup.
///     All methods operate on backup source paths, NOT archive paths.
///     Archive management uses standard .NET File/Directory APIs.
/// </summary>
public interface IHighLevelOsApi
{
    // ========================================================================
    // Platform Capability Detection
    // ========================================================================

    /// <summary>
    ///     Check if this platform supports POSIX ACLs (Linux/macOS).
    ///     Used to determine whether ACL metadata should be collected.
    /// </summary>
    bool HasAclSupport { get; }

    /// <summary>
    ///     Check if this platform supports extended attributes (Linux/macOS).
    ///     Used to determine whether xattr metadata should be collected.
    /// </summary>
    bool HasXattrSupport { get; }

    /// <summary>
    ///     Check if this platform supports Windows security descriptors.
    ///     Used to determine whether security descriptor metadata should be collected.
    /// </summary>
    bool HasSecurityDescriptorSupport { get; }

    /// <summary>
    ///     Check if this platform supports Alternate Data Streams (Windows NTFS).
    ///     Used to determine whether ADS metadata should be collected.
    /// </summary>
    bool HasAlternateStreamSupport { get; }

    /// <summary>
    ///     Create complete InodeData from pathname with all metadata, ACLs, xattrs, and content hashes.
    ///     This is the main entry point that encapsulates:
    ///     - LStat (file metadata without following symlinks)
    ///     - User/group name lookups (UID→username, GID→groupname)
    ///     - ACL reading (if supported by platform)
    ///     - Extended attribute reading (if supported by platform)
    ///     - File content reading and hashing (via IArchiveStore)
    ///     - Symlink target reading
    ///     - Directory entry serialization
    /// </summary>
    /// <param name="path">Path to file/directory to process (backup source, not archive)</param>
    /// <param name="archiveStore">Archive store for content hashing and storage</param>
    /// <returns>Fully populated InodeData object with all collected metadata</returns>
    /// <exception cref="OsException">Thrown on permission denied, not found, or I/O errors</exception>
    InodeData CreateInodeDataFromPath(string path, IArchiveStore archiveStore);

    /// <summary>
    ///     List directory entries for breadth-first traversal.
    ///     Returns full paths, sorted, excluding "." and "..".
    ///     This is needed by Backup_worker's main loop to enumerate children
    ///     for directories encountered during traversal.
    /// </summary>
    /// <param name="path">Directory path to enumerate</param>
    /// <returns>Array of full paths to directory entries, sorted</returns>
    /// <exception cref="OsException">Thrown if directory cannot be read</exception>
    string[] ListDirectory(string path);
}