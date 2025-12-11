using System.Text.Json.Nodes;
using ArchiveDataHandler;

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
    /// <summary>
    ///     Static singleton accessor for a default platform `IHighLevelOsApi` implementation.
    ///     Implementations (Linux/Windows) must provide a matching static property returning
    ///     an `IHighLevelOsApi` singleton.
    /// </summary>
    static abstract IHighLevelOsApi Instance { get; }

    /// <summary>
    ///     Creates a minimal InodeData object from a filesystem path containing only
    ///     stat information (no ACLs, xattrs, or content hashes).
    /// </summary>
    /// <param name="path">Filesystem path to inspect.</param>
    /// <returns>A minimal <see cref="InodeData" /> instance with stat information only.</returns>
    /// <exception cref="T:OsCallsCommon.OsException">Thrown on permission denied, not found, or I/O errors</exception>
    InodeData CreateMinimalInodeDataFromPath(string path);

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
    InodeData CompleteInodeDataFromPath(
        string path,
        ref InodeData data,
        IArchiveStore archiveStore
    );

    /// <summary>
    ///     List directory entries for breadth-first traversal.
    ///     Returns full paths, sorted, excluding "." and "..".
    ///     This is needed by Backup_worker's main loop to enumerate children
    ///     for directories encountered during traversal.
    /// </summary>
    /// <param name="path">Directory path to enumerate</param>
    /// <returns>Array of full paths to directory entries, sorted</returns>
    /// <exception cref="T:OsCallsCommon.OsException">Thrown if directory cannot be read</exception>
    string[] ListDirectory(string path);

    /// <summary>
    ///     Canonicalizes a filesystem path by resolving symlinks and normalizing separators.
    ///     Returns a JsonNode containing the canonical path.
    /// </summary>
    /// <param name="path">Path to canonicalize</param>
    /// <returns>A JsonNode containing the canonical path under the "path" key</returns>
    /// <exception cref="T:OsCallsCommon.OsException">Thrown on permission denied, not found, or I/O errors</exception>
    JsonNode Canonicalizefilename(string path);
}
