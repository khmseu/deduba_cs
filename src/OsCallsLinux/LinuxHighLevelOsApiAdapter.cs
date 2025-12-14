using System.Text.Json.Nodes;
using ArchiveDataHandler;
using OsCallsCommon;
using UtilitiesLibrary;

namespace OsCallsLinux;

/// <summary>
///     Adapter that accepts a logger and forwards calls to the platform singleton.
///     Also wires the logger into native-call related modules (FileSystem/ValXfer).
/// </summary>
public class LinuxHighLevelOsApiAdapter : IHighLevelOsApi
{
    private readonly IHighLevelOsApi _inner = LinuxHighLevelOsApi.Instance;

    /// <inheritdoc />
    public LinuxHighLevelOsApiAdapter(ILogging logger)
    {
        // Forward logging to native wrappers so P/Invoke resolver and native errors are visible
        FileSystem.Logger = logger;
        ValXfer.Logger = logger;
    }

    /// <inheritdoc />
    public static IHighLevelOsApi Instance => LinuxHighLevelOsApi.Instance;

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
