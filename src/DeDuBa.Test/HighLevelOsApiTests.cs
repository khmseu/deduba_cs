using System.Diagnostics;
using ArchiveDataHandler;
using OsCallsCommon;
using UtilitiesLibrary;

namespace DeDuBa.Test;

/// <summary>
///     Tests for IHighLevelOsApi implementations using the high-level metadata collection APIs.
///     These tests validate CreateMinimalInodeDataFromPath and CompleteInodeDataFromPath.
/// </summary>
[Collection("TestEnvironment")]
[ResetUtilitiesLog]
public class HighLevelOsApiTests : IDisposable
{
    private readonly IArchiveStore _archiveStore;
    private readonly IHighLevelOsApi _osApi;
    private readonly string _testDirPath;
    private readonly string _testFilePath;
    private readonly string? _testSymlinkPath;
    private readonly string _tmpDir;

    public HighLevelOsApiTests()
    {
        _tmpDir = Path.Combine(Path.GetTempPath(), "deduba_hlapi_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpDir);

        _testFilePath = Path.Combine(_tmpDir, "testfile.txt");
        File.WriteAllText(_testFilePath, "test content for high-level API");

        _testDirPath = Path.Combine(_tmpDir, "testdir");
        Directory.CreateDirectory(_testDirPath);

        // Try to create a symlink (may fail on Windows without admin privileges)
        _testSymlinkPath = Path.Combine(_tmpDir, "testlink");
        try
        {
            if (OperatingSystem.IsWindows())
            {
                File.CreateSymbolicLink(_testSymlinkPath, _testFilePath);
            }
            else
            {
                var si = Process.Start(
                    new ProcessStartInfo("ln", $"-s {_testFilePath} {_testSymlinkPath}")
                    {
                        UseShellExecute = false,
                    }
                );
                si?.WaitForExit();
            }
        }
        catch
        {
            _testSymlinkPath = null;
        }

        _osApi = HighLevelOsApiFactory.GetOsApi(UtilitiesLogger.Instance);

        // Set up archive store for tests that need it
        Utilities.Testing = true;
        _archiveStore = new ArchiveStore(BackupConfig.Instance);
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
    public void CreateMinimalInodeDataFromPath_RegularFile_ReturnsBasicMetadata()
    {
        // Act
        var inodeData = _osApi.CreateMinimalInodeDataFromPath(_testFilePath);

        // Assert
        Assert.NotNull(inodeData);
        Assert.NotEqual(0, inodeData.Device);
        Assert.NotEqual(0, inodeData.FileIndex);
        Assert.NotEqual(0, inodeData.Mode);
        Assert.Contains("reg", inodeData.Flags);
        Assert.True(inodeData.Size > 0, $"Expected Size > 0, got {inodeData.Size}");
        Assert.True(inodeData.MTime > 0, $"Expected MTime > 0, got {inodeData.MTime}");

        // Minimal data should not have ACLs, xattrs, or hashes
        Assert.Empty(inodeData.Acl);
        Assert.Empty(inodeData.Xattr);
        Assert.Empty(inodeData.Hashes);
    }

    [Fact]
    public void CreateMinimalInodeDataFromPath_Directory_ReturnsDirectoryMetadata()
    {
        // Act
        var inodeData = _osApi.CreateMinimalInodeDataFromPath(_testDirPath);

        // Assert
        Assert.NotNull(inodeData);
        Assert.Contains("dir", inodeData.Flags);
        Assert.DoesNotContain("reg", inodeData.Flags);

        // Minimal data should not have extended metadata
        Assert.Empty(inodeData.Acl);
        Assert.Empty(inodeData.Xattr);
        Assert.Empty(inodeData.Hashes);
    }

    [Fact]
    public void CreateMinimalInodeDataFromPath_SymbolicLink_ReturnsLinkMetadata()
    {
        // Skip if symlink wasn't created
        if (string.IsNullOrEmpty(_testSymlinkPath))
            return;

        // Act
        var inodeData = _osApi.CreateMinimalInodeDataFromPath(_testSymlinkPath);

        // Assert
        Assert.NotNull(inodeData);
        Assert.Contains("lnk", inodeData.Flags);

        // Minimal data should not have extended metadata
        Assert.Empty(inodeData.Hashes);
    }

    [Fact]
    public void CompleteInodeDataFromPath_RegularFile_AddsContentHashes()
    {
        // Arrange - get minimal data first
        var minimalData = _osApi.CreateMinimalInodeDataFromPath(_testFilePath);

        // Act - complete the inode data
        var completeData = _osApi.CompleteInodeDataFromPath(
            _testFilePath,
            ref minimalData,
            _archiveStore
        );

        // Assert
        Assert.NotNull(completeData);
        Assert.NotEmpty(completeData.Hashes);
        Assert.NotEmpty(completeData.UserName);
        Assert.NotEmpty(completeData.GroupName);

        // Device and FileIndex should remain unchanged
        Assert.Equal(minimalData.Device, completeData.Device);
        Assert.Equal(minimalData.FileIndex, completeData.FileIndex);
    }

    [Fact]
    public void CompleteInodeDataFromPath_Directory_ResolvesUserGroupNames()
    {
        // Arrange
        var minimalData = _osApi.CreateMinimalInodeDataFromPath(_testDirPath);

        // Act
        var completeData = _osApi.CompleteInodeDataFromPath(
            _testDirPath,
            ref minimalData,
            _archiveStore
        );

        // Assert
        Assert.NotNull(completeData);
        Assert.NotEmpty(completeData.UserName);
        Assert.NotEmpty(completeData.GroupName);
        Assert.Contains("dir", completeData.Flags);

        // Directories don't have content hashes
        Assert.Empty(completeData.Hashes);
    }

    [Fact]
    public void CompleteInodeDataFromPath_SymbolicLink_AddsLinkTarget()
    {
        // Skip if symlink wasn't created
        if (string.IsNullOrEmpty(_testSymlinkPath))
            return;

        // Arrange
        var minimalData = _osApi.CreateMinimalInodeDataFromPath(_testSymlinkPath);

        // Act
        var completeData = _osApi.CompleteInodeDataFromPath(
            _testSymlinkPath,
            ref minimalData,
            _archiveStore
        );

        // Assert
        Assert.NotNull(completeData);
        Assert.Contains("lnk", completeData.Flags);
        // Symlink should have hashes for the target path
        Assert.NotEmpty(completeData.Hashes);
    }

    [Fact]
    public void CreateMinimalInodeDataFromPath_NonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tmpDir, "does_not_exist.txt");

        // Act & Assert
        // Note: Currently throws Exception (wrapped) rather than OsException directly
        Assert.Throws<Exception>(() => _osApi.CreateMinimalInodeDataFromPath(nonExistentPath));
    }

    [Fact]
    public void CreateMinimalInodeDataFromPath_ThenComplete_ProducesFullMetadata()
    {
        // Arrange
        var minimalData = _osApi.CreateMinimalInodeDataFromPath(_testFilePath);

        // Verify minimal data has expected properties
        Assert.NotEqual(0, minimalData.Device);
        Assert.NotEqual(0, minimalData.FileIndex);
        Assert.Empty(minimalData.Hashes);

        // Act - complete the data
        var completeData = _osApi.CompleteInodeDataFromPath(
            _testFilePath,
            ref minimalData,
            _archiveStore
        );

        // Assert - complete data has everything
        Assert.NotEqual(0, completeData.Device);
        Assert.NotEqual(0, completeData.FileIndex);
        Assert.NotEmpty(completeData.Hashes);
        Assert.NotEmpty(completeData.UserName);
        Assert.NotEmpty(completeData.GroupName);

        // ToString should produce a readable summary
        var summary = completeData.ToString();
        Assert.NotEmpty(summary);
        Assert.Contains("mode=", summary);
        Assert.Contains("nlink=", summary);
        Assert.Contains("size=", summary);
    }

    [Fact]
    public void ListDirectory_ReturnsOrderedEntries()
    {
        // Arrange - create some files in test directory
        var file1 = Path.Combine(_testDirPath, "aaa.txt");
        var file2 = Path.Combine(_testDirPath, "zzz.txt");
        var file3 = Path.Combine(_testDirPath, "mmm.txt");
        File.WriteAllText(file1, "a");
        File.WriteAllText(file2, "z");
        File.WriteAllText(file3, "m");

        // Act
        var entries = _osApi.ListDirectory(_testDirPath);

        // Assert
        Assert.NotNull(entries);
        Assert.Equal(3, entries.Length);

        // Entries should be sorted
        Assert.Equal(file1, entries[0]);
        Assert.Equal(file3, entries[1]);
        Assert.Equal(file2, entries[2]);
    }

    [Fact]
    public void Canonicalizefilename_ReturnsCanonicalPath()
    {
        // Act
        var result = _osApi.Canonicalizefilename(_testFilePath);

        // Assert
        Assert.NotNull(result);
        var path = result["path"]?.GetValue<string>();
        Assert.NotNull(path);
        Assert.True(Path.IsPathFullyQualified(path));
    }
}
