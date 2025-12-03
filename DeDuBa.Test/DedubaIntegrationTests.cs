using System.Text.RegularExpressions;
using UtilitiesLibrary;

namespace DeDuBa.Test;

[Collection("TestEnvironment")]
[ResetUtilitiesLog]
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
        catch
        {
        }
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
        // Canonicalize the paths to match logging output and canonicalization performed by Backup
        outsideFile = Path.GetFullPath(outsideFile);
        insideFile = Path.GetFullPath(insideFile);
        File.WriteAllText(outsideFile, "outside content");
        File.WriteAllText(insideFile, "inside content that must not be backed up");

        Environment.SetEnvironmentVariable("DEDU_ARCHIVE_ROOT", archiveRoot);

        // Debugging: print runtime environment info to help diagnose intermittent failures
        Console.WriteLine($"[DEBUG] Parent={parent}");
        Console.WriteLine($"[DEBUG] ArchiveRoot={archiveRoot}");
        Console.WriteLine($"[DEBUG] OutsideFile (canonical)={outsideFile}");
        Console.WriteLine($"[DEBUG] InsideFile (canonical)={insideFile}");
        Console.WriteLine($"[DEBUG] Environment.DEDU_ARCHIVE_ROOT={Environment.GetEnvironmentVariable("DEDU_ARCHIVE_ROOT")}");

        DedubaClass.Backup(new[] { parent });

        var config = BackupConfig.FromUtilities();
        // Basic: archive should exist and there should be a log file
        Assert.True(Directory.Exists(config.ArchiveRoot));
        var logs = Directory.GetFiles(config.ArchiveRoot, "log_*", SearchOption.TopDirectoryOnly);
        Assert.True(logs.Length > 0);
        var chosenLog = logs.OrderBy(x => x).Last();
        var log = File.ReadAllText(chosenLog);

        // Print the list of files in the created archive root for diagnostics
        Console.WriteLine("[DEBUG] ArchiveRoot contents:");
        foreach (var f in Directory.EnumerateFiles(config.ArchiveRoot, "*", SearchOption.AllDirectories))
        {
            Console.WriteLine("[DEBUG]  " + f);
        }

        Console.WriteLine("[DEBUG] Parent directory contents:");
        foreach (var f in Directory.EnumerateFiles(parent, "*", SearchOption.AllDirectories))
        {
            Console.WriteLine("[DEBUG]  " + f);
        }

        // Also output the log file being asserted against and its contents - this is critical
        var chosenLog = logs.OrderBy(x => x).Last();
        Console.WriteLine($"[DEBUG] Using log file: {chosenLog}");
        Console.WriteLine("[DEBUG] Log contents:\n" + log);
        // Persist the log to the test temp area for off-line inspection
        var logCopy = Path.Combine(_tmpDir, "debug-log.txt");
        File.WriteAllText(logCopy, log);
        Console.WriteLine($"[DEBUG] Saved full log to: {logCopy}");

        // Parse the log for any path-like entries and print them - useful when paths are encoded/hashed
        var lines = Regex.Split(log, "\r?\n");
        Console.WriteLine("[DEBUG] Parsed log path entries:");
        var pathRegex = new Regex(@"\] (?<path>/[^\s\[]+)", RegexOptions.Compiled);
        foreach (var line in lines)
        {
            var m = pathRegex.Match(line);
            if (m.Success)
            {
                Console.WriteLine("[DEBUG]  -> " + m.Groups["path"].Value);
            }
        }

        // Additional diagnostics for flaky test: check basename and relative matches in the log
        var outsideBasename = Path.GetFileName(outsideFile);
        var outsideRel = Path.GetRelativePath(parent, outsideFile);
        Console.WriteLine($"[DEBUG] Checking alternate forms: basename={outsideBasename}, rel={outsideRel}");
        Console.WriteLine($"[DEBUG] Contains basename: {log.Contains(outsideBasename)}");
        Console.WriteLine($"[DEBUG] Contains rel (parent): {log.Contains(outsideRel)}");

        // Log should mention the outside file but should not mention the archive-inside file path
        Assert.Contains(outsideFile, log);
        Assert.DoesNotContain(insideFile, log);
    }
}