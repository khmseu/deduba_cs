using System;
using System.Collections.Generic;
using System.IO;

namespace DeDuBa;
#pragma warning disable CS1591

public interface IArchiveStore
{
    void BuildIndex();
    string? GetTargetPathForHash(string hexHash);
    string SaveData(ReadOnlySpan<byte> data);
    List<string> SaveStream(Stream stream, long size, string tag, Action<long>? progress = null);
    IReadOnlyDictionary<string, string> Arlist { get; }
    IReadOnlyDictionary<string, IReadOnlyCollection<string>> Preflist { get; }
    IReadOnlyDictionary<string, long> Stats { get; }
    long PackSum { get; }
    string DataPath { get; }
}
