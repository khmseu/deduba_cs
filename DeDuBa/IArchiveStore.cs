namespace DeDuBa;

public interface IArchiveStore
{
    IReadOnlyDictionary<string, string> Arlist { get; }
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Preflist { get; }
    IReadOnlyDictionary<string, long> Stats { get; }
    long PackSum { get; }
    string DataPath { get; }
    void BuildIndex();
    string? GetTargetPathForHash(string hexHash);
    string SaveData(ReadOnlySpan<byte> data);
    List<string> SaveStream(Stream stream, long size, string tag, Action<long>? progress = null);
}
