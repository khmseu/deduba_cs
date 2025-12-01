using OsCallsCommon;

namespace OsCallsWindows;

/// <summary>
/// Windows implementation stub of IHighLevelOsApi.
/// TODO: Implement using Windows-specific APIs (security descriptors, alternate data streams).
/// </summary>
public class WindowsHighLevelOsApi : IHighLevelOsApi
{
    public bool HasAclSupport => false;
    public bool HasXattrSupport => false;
    public bool HasSecurityDescriptorSupport => true;
    public bool HasAlternateStreamSupport => true;

    public InodeData CreateInodeDataFromPath(string path, IArchiveStore archiveStore)
    {
        throw new NotImplementedException("Windows implementation not yet completed. Use Linux implementation as reference.");
    }

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
