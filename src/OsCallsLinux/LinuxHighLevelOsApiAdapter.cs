using System.Text.Json.Nodes;
using ArchiveDataHandler;
using OsCallsCommon;

namespace OsCallsLinux;

/// <summary>
///     Adapter that accepts a logger and forwards calls to the platform singleton.
///     Also wires the logger into native-call related modules (FileSystem/ValXfer).
/// </summary>
public class LinuxHighLevelOsApiAdapter : IHighLevelOsApi
{
    public static IHighLevelOsApi Instance => LinuxHighLevelOsApi.Instance;

    private readonly IHighLevelOsApi _inner = LinuxHighLevelOsApi.Instance;

    public LinuxHighLevelOsApiAdapter(UtilitiesLibrary.ILogging logger)
    {
        // Forward logging to native wrappers so P/Invoke resolver and native errors are visible
        FileSystem.Logger = logger;
        ValXfer.Logger = logger;
    }

    public InodeData CreateMinimalInodeDataFromPath(string path) =>
        _inner.CreateMinimalInodeDataFromPath(path);

    public InodeData CompleteInodeDataFromPath(
        string path,
        ref InodeData data,
        IArchiveStore archiveStore
    ) => _inner.CompleteInodeDataFromPath(path, ref data, archiveStore);

    public string[] ListDirectory(string path) => _inner.ListDirectory(path);

    public JsonNode Canonicalizefilename(string path) => _inner.Canonicalizefilename(path);
}
