using UtilitiesLibrary;

namespace DeDuBa.Test;

public class DedubaIntegrationTests : IDisposable
{
    private readonly string _tmpDir;

    public DedubaIntegrationTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "deduba_integ_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tmpDir, true);
        }
        catch { }
    }

    [Fact]
    public void Backup_ProcessWritesArchiveDirectly()
    {
        var f = Path.Combine(_tmpDir, "testfile.txt");
        File.WriteAllText(f, "integration test content");

        Utilities.Testing = true;
        DedubaClass.Backup(new[] { f });

        var config = BackupConfig.FromUtilities();
        Assert.True(Directory.Exists(config.DataPath));

        var files = Directory.GetFiles(config.DataPath, "*", SearchOption.AllDirectories);
        Assert.True(files.Length > 0);
    }
}
