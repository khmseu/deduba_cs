using System;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.BZip2;
using Xunit;

namespace DeDuBa.Test;

public class ArchiveStoreTests : IDisposable
{
    private readonly string _tmpDir;
    private readonly BackupConfig _cfg;
    private readonly ArchiveStore _store;

    public ArchiveStoreTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
        _cfg = new BackupConfig(
            _tmpDir,
            chunkSize: 1024 * 16,
            testing: true,
            verbose: false,
            prefixSplitThreshold: 10
        );
        _store = new ArchiveStore(
            _cfg,
            s =>
            { /* no-op */
            }
        );
    }

    [Fact]
    public void BuildIndex_PopulatesArlistAndPreflist()
    {
        var dataPath = _cfg.DataPath;
        Directory.CreateDirectory(Path.Combine(dataPath, "ab"));
        var fileHash = "abcdef1234567890";
        var filePath = Path.Combine(dataPath, "ab", fileHash);
        File.WriteAllText(filePath, "dummy");

        _store.BuildIndex();

        Assert.Contains(fileHash, _store.Arlist.Keys);
        Assert.True(_store.Preflist.TryGetValue("ab", out var list));
        Assert.Contains(fileHash, list);
    }

    [Fact]
    public void SaveData_WritesBzip2File_And_StatsUpdated()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        var hash = _store.SaveData(data);

        Assert.NotNull(hash);
        var prefix = _store.Arlist[hash];
        var path = Path.Combine(_cfg.DataPath, prefix, hash);
        Assert.True(File.Exists(path));

        using var fs = File.OpenRead(path);
        using var bzip = new BZip2InputStream(fs);
        using var ms = new MemoryStream();
        bzip.CopyTo(ms);
        var decompressed = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal("Hello, World!", decompressed);

        Assert.True(_store.Stats.ContainsKey("saved_blocks"));
    }

    [Fact]
    public void SaveData_DuplicateDetection()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("duplicate-data");
        var hash1 = _store.SaveData(data);
        var hash2 = _store.SaveData(data);

        Assert.Equal(hash1, hash2);
        Assert.True(_store.Stats.ContainsKey("duplicate_blocks"));
    }

    [Fact]
    public void SaveStream_Chunking_And_Progress()
    {
        var size = 1024 * 32;
        var buffer = new byte[size];
        new Random(42).NextBytes(buffer);

        int callbackInvocations = 0;
        void OnProgress(long bytes) => callbackInvocations++;

        using var mem = new MemoryStream(buffer);

        var hashes = _store.SaveStream(mem, size, "test", bytes => OnProgress(bytes));
        Assert.True(hashes.Count >= 1);
        Assert.True(callbackInvocations > 0);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tmpDir, true);
        }
        catch { }
    }
}
