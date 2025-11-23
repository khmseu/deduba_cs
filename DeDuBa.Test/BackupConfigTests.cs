namespace DeDuBa.Test;

[Collection("TestEnvironment")]
[ResetUtilitiesLog]
public class BackupConfigTests
{
    [Fact]
    public void FromUtilities_SetsPaths()
    {
        var cfg = new BackupConfig("/tmp/myarchive", testing: true, verbose: false);
        Assert.Equal("/tmp/myarchive", cfg.ArchiveRoot);
        Assert.Equal(Path.Combine("/tmp/myarchive", "DATA"), cfg.DataPath);
        Assert.Equal(1024L * 1024L * 1024L, cfg.ChunkSize);
        Assert.True(cfg.Testing);
        Assert.False(cfg.Verbose);
    }
}
