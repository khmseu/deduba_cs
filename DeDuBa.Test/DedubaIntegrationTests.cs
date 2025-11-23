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

    [Fact]
    public void Backup_RefusesToBackupArchiveRoot()
    {
        var f = Path.Combine(_tmpDir, "testfile.txt");
        File.WriteAllText(f, "integration test content");

        Utilities.Testing = true;
        var config = BackupConfig.FromUtilities();
        // Use a workspace-local archive path inside the temp dir
        var archiveRoot = Path.Combine(_tmpDir, "ARCHIVE4");
        Environment.SetEnvironmentVariable("DEDU_ARCHIVE_ROOT", archiveRoot);
        Directory.CreateDirectory(archiveRoot);

        // Calling backup with archive root should throw an InvalidOperationException
        Assert.Throws<InvalidOperationException>(() => DedubaClass.Backup(new[] { archiveRoot }));
    }

    [Fact]
    public void Backup_SkipsArchiveWhenParentBackup()
    {
        Utilities.Testing = true;
        var parent = Path.Combine(_tmpDir, "parent");
        var archiveRoot = Path.Combine(parent, "ARCHIVE4");
        Directory.CreateDirectory(archiveRoot);
        Directory.CreateDirectory(parent);

        var outsideFile = Path.Combine(parent, "outside.txt");
        var insideFile = Path.Combine(archiveRoot, "inside.txt");
        File.WriteAllText(outsideFile, "outside content");
        File.WriteAllText(insideFile, "inside content that must not be backed up");

        Environment.SetEnvironmentVariable("DEDU_ARCHIVE_ROOT", archiveRoot);

        DedubaClass.Backup(new[] { parent });

        var config = BackupConfig.FromUtilities();
        // Basic: archive should exist and there should be a log file
        Assert.True(Directory.Exists(config.ArchiveRoot));
        var logs = Directory.GetFiles(config.ArchiveRoot, "log_*", SearchOption.TopDirectoryOnly);
        Assert.True(logs.Length > 0);
        var log = File.ReadAllText(logs.OrderBy(x => x).Last());

        // Log should mention the outside file but should not mention the archive-inside file path
        Assert.Contains(outsideFile, log);
        Assert.DoesNotContain(insideFile, log);
    }
}
