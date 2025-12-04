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
    ///     Create an <see cref="InodeData"/> for <paramref name="path"/> using
    ///     Windows-specific APIs and persist any auxiliary data via
    ///     <paramref name="archiveStore"/>. Not implemented yet.
    /// </summary>
    /// <param name="path">Path to inspect.</param>
    /// <param name="archiveStore">Archive store used to save auxiliary streams.</param>
    /// <returns>Populated <see cref="InodeData"/>.</returns>
    /// <exception cref="NotImplementedException">Always; Windows shim not yet implemented.</exception>
    public InodeData CreateInodeDataFromPath(string path, IArchiveStore archiveStore)
    {
        throw new NotImplementedException(
            "Windows implementation not yet completed. Use Linux implementation as reference.");
    }

    /// <summary>
    ///     List the directory entries for <paramref name="path"/> ordered by
    ///     ordinal string comparison. Wraps <see cref="Directory.GetFileSystemEntries"/>
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