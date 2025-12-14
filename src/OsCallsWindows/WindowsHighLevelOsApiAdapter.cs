using System.Text.Json.Nodes;
using ArchiveDataHandler;
using OsCallsCommon;
using UtilitiesLibrary;

namespace OsCallsWindows;

/// <summary>
///     Adapter that accepts a logger and forwards calls to the platform singleton.
///     Also wires the logger into native-call related modules (FileSystem/ValXfer).
/// </summary>
public class WindowsHighLevelOsApiAdapter : IHighLevelOsApi
{
    private readonly IHighLevelOsApi _inner = WindowsHighLevelOsApi.Instance;

    /// <inheritdoc />
    public WindowsHighLevelOsApiAdapter(ILogging logger)
    {
        FileSystem.Logger = logger;
        ValXfer.Logger = logger;
    }

    /// <inheritdoc />
    public static IHighLevelOsApi Instance => WindowsHighLevelOsApi.Instance;

    /// <inheritdoc />
    public InodeData CreateMinimalInodeDataFromPath(string path)
    {
        return _inner.CreateMinimalInodeDataFromPath(path);
    }

    /// <inheritdoc />
    public InodeData CompleteInodeDataFromPath(string path, ref InodeData data, IArchiveStore archiveStore)
    {
        return _inner.CompleteInodeDataFromPath(path, ref data, archiveStore);
    }

    /// <inheritdoc />
    public string[] ListDirectory(string path)
    {
        return _inner.ListDirectory(path);
    }

    /// <inheritdoc />
    public JsonNode Canonicalizefilename(string path)
    {
        return _inner.Canonicalizefilename(path);
    }
}
