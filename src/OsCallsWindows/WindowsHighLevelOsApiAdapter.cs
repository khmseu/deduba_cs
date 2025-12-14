using System.Text.Json.Nodes;
using ArchiveDataHandler;
using OsCallsCommon;

namespace OsCallsWindows;

/// <summary>
///     Adapter that accepts a logger and forwards calls to the platform singleton.
///     Also wires the logger into native-call related modules (FileSystem/ValXfer).
/// </summary>
public class WindowsHighLevelOsApiAdapter : IHighLevelOsApi
{
    public static IHighLevelOsApi Instance => WindowsHighLevelOsApi.Instance;

    private readonly IHighLevelOsApi _inner = WindowsHighLevelOsApi.Instance;

    public WindowsHighLevelOsApiAdapter(UtilitiesLibrary.ILogging logger)
    {
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
