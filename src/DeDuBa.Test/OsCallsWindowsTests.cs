#if DEDUBA_WINDOWS
using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using OsCallsWindows;

namespace DeDuBa.Test;

/// <summary>
///     Tests for Windows-specific filesystem operations via OsCallsWindows module.
///     These tests validate win_lstat, win_readlink, win_canonicalize_file_name,
///     win_get_sd, and ADS functionality.
/// </summary>
[Collection("TestEnvironment")]
[ResetUtilitiesLog]
public class OsCallsWindowsTests : IDisposable
{
    private readonly string _testDirPath;
    private readonly string _testFilePath;
    private readonly string _testSymlinkPath;

    public OsCallsWindowsTests()
    {
        // Create a temporary test directory
        _testDirPath = Path.Combine(Path.GetTempPath(), $"windows_test_dir_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirPath);

        // Create a temporary test file
        _testFilePath = Path.Combine(_testDirPath, "test_file.txt");
        File.WriteAllText(_testFilePath, "Test content for Windows testing");

        // Create a symbolic link (requires admin privileges or developer mode)
        _testSymlinkPath = Path.Combine(_testDirPath, "test_symlink.txt");
        try
        {
            File.CreateSymbolicLink(_testSymlinkPath, _testFilePath);
        }
        catch (UnauthorizedAccessException)
        {
            // Skip symlink creation if no privileges
            _testSymlinkPath = string.Empty;
        }
    }

    public void Dispose()
    {
        // Cleanup test files and directory
        if (Directory.Exists(_testDirPath))
            Directory.Delete(_testDirPath, true);
    }

    [Fact]
    public void LStat_RegularFile_ReturnsFileMetadata()
    {
        // Act
        var result = FileSystem.LStat(_testFilePath);

        // Assert
        Assert.NotNull(result);
        var resultObj = result.AsObject();

        // Check that we got file metadata
        Assert.True(resultObj.ContainsKey("st_size"));
        Assert.True(resultObj.ContainsKey("st_mode"));
        Assert.True(resultObj.ContainsKey("S_ISREG"));
        Assert.True(resultObj["S_ISREG"]?.GetValue<bool>() == true);

        // File size should match
        var size = resultObj["st_size"]?.GetValue<long>();
        Assert.True(size > 0);

        // Should have timestamps
        Assert.True(resultObj.ContainsKey("st_mtim"));
        Assert.True(resultObj.ContainsKey("st_atim"));
    }

    [Fact]
    public void LStat_Directory_ReturnsDirectoryMetadata()
    {
        // Act
        var result = FileSystem.LStat(_testDirPath);

        // Assert
        Assert.NotNull(result);
        var resultObj = result.AsObject();

        // Check that it's identified as a directory
        Assert.True(resultObj.ContainsKey("S_ISDIR"));
        Assert.True(resultObj["S_ISDIR"]?.GetValue<bool>() == true);

        // Should have mode and timestamps
        Assert.True(resultObj.ContainsKey("st_mode"));
        Assert.True(resultObj.ContainsKey("st_mtim"));
    }

    [Fact]
    public void LStat_SymbolicLink_ReturnsLinkMetadata()
    {
        // Skip if symlink wasn't created (no privileges)
        if (string.IsNullOrEmpty(_testSymlinkPath))
        {
            return;
        }

        // Act
        var result = FileSystem.LStat(_testSymlinkPath);

        // Assert
        Assert.NotNull(result);
        var resultObj = result.AsObject();

        // Check that it's identified as a symbolic link
        Assert.True(resultObj.ContainsKey("S_ISLNK"));
        Assert.True(resultObj["S_ISLNK"]?.GetValue<bool>() == true);
    }

    [Fact]
    public void ReadLink_SymbolicLink_ReturnsTarget()
    {
        // Skip if symlink wasn't created
        if (string.IsNullOrEmpty(_testSymlinkPath))
        {
            return;
        }

        // Act
        var result = FileSystem.ReadLink(_testSymlinkPath);

        // Assert
        Assert.NotNull(result);
        var resultObj = result.AsObject();

        // Should have a path field with the target
        Assert.True(resultObj.ContainsKey("path"));
        var targetPath = resultObj["path"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(targetPath));
    }

    [Fact]
    public void Canonicalizefilename_RelativePath_ReturnsAbsolutePath()
    {
        // Arrange
        var currentDir = Directory.GetCurrentDirectory();
        var relativePath = Path.GetFileName(_testFilePath);

        // Change to test directory
        Directory.SetCurrentDirectory(_testDirPath);

        try
        {
            // Act
            var result = FileSystem.Canonicalizefilename(relativePath);

            // Assert
            Assert.NotNull(result);
            var resultObj = result.AsObject();

            Assert.True(resultObj.ContainsKey("path"));
            var canonicalPath = resultObj["path"]?.GetValue<string>();
            Assert.False(string.IsNullOrEmpty(canonicalPath));

            // Path should be absolute (no relative components)
            Assert.True(Path.IsPathRooted(canonicalPath));
        }
        finally
        {
            // Restore original directory
            Directory.SetCurrentDirectory(currentDir);
        }
    }

    [Fact]
    public void GetSecurityDescriptor_File_ReturnsSDDL()
    {
        // Act
        var result = Security.GetSecurityDescriptor(_testFilePath, includeSacl: false);

        // Assert
        Assert.NotNull(result);
        var resultObj = result.AsObject();

        // Should have an sddl field with security descriptor string
        Assert.True(resultObj.ContainsKey("sddl"));
        var sddl = resultObj["sddl"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(sddl));

        // SDDL should contain owner (O:), group (G:), and DACL (D:)
        Assert.Contains("O:", sddl);
        Assert.Contains("D:", sddl);
    }

    [Fact]
    public void ListStreams_FileWithADS_ReturnsStreamList()
    {
        // Arrange - Create a file with alternate data stream
        var testFileWithAds = Path.Combine(_testDirPath, "file_with_ads.txt");
        File.WriteAllText(testFileWithAds, "Main content");
        Console.WriteLine($"Created test file: {testFileWithAds}");
        Console.WriteLine($"File exists: {File.Exists(testFileWithAds)}");
        Console.WriteLine($"File size: {new FileInfo(testFileWithAds).Length} bytes");

        // Add an alternate data stream using cmd
        var streamPath = $"{testFileWithAds}:TestStream";
        try
        {
            File.WriteAllText(streamPath, "Stream content");
            Console.WriteLine($"Created ADS: {streamPath}");
            // Verify ADS was created by reading it back
            var adsContent = File.ReadAllText(streamPath);
            Console.WriteLine($"ADS content verified: {adsContent}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to create ADS: {ex.Message}");
            // Skip test if ADS not supported (e.g., not NTFS)
            return;
        }

        // Act
        Console.WriteLine($"Calling ListStreams for: {testFileWithAds}");
        var result = Streams.ListStreams(testFileWithAds);
        Console.WriteLine($"ListStreams returned: {result?.ToJsonString()}");

        // Assert
        Assert.NotNull(result);
        Console.WriteLine($"Result type: {result.GetType().Name}");

        // Result should be an array of stream objects
        if (result is JsonArray streamArray)
        {
            Console.WriteLine($"StreamArray count: {streamArray.Count}");
            for (int i = 0; i < streamArray.Count; i++)
            {
                Console.WriteLine($"Stream[{i}]: {streamArray[i]?.ToJsonString()}");
            }

            Assert.True(streamArray.Count > 0, $"Expected streams but got {streamArray.Count}");

            // At least one stream should be present
            var firstStream = streamArray[0]?.AsObject();
            Assert.NotNull(firstStream);
            Assert.True(firstStream.ContainsKey("name"));
            Assert.True(firstStream.ContainsKey("size"));
        }
        else
        {
            Console.WriteLine($"Result is not JsonArray: {result.ToJsonString()}");
            Assert.Fail("Expected JsonArray result");
        }
    }

    [Fact]
    public void ReadStream_AlternateDataStream_ReturnsContent()
    {
        // Arrange - Create a file with alternate data stream
        var testFileWithAds = Path.Combine(_testDirPath, "file_with_stream.txt");
        File.WriteAllText(testFileWithAds, "Main content");

        var streamName = "TestStream";
        var streamContent = "This is stream content";
        var streamPath = $"{testFileWithAds}:{streamName}";

        try
        {
            File.WriteAllText(streamPath, streamContent);
        }
        catch
        {
            // Skip test if ADS not supported
            return;
        }

        // Act
        var result = Streams.ReadStream(testFileWithAds, streamName);

        // Assert
        Assert.NotNull(result);
        var resultObj = result.AsObject();

        // Should have content field
        Assert.True(resultObj.ContainsKey("content"));
        var content = resultObj["content"]?.GetValue<string>();
        Assert.Equal(streamContent, content);
    }

    [Fact]
    public void LStat_NonExistentFile_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirPath, "does_not_exist.txt");

        // Act & Assert
        var ex = Assert.Throws<Exception>(() => FileSystem.LStat(nonExistentPath));

        // Should have Win32Exception as inner exception with ERROR_FILE_NOT_FOUND (2)
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
        var win32Ex = (Win32Exception)ex.InnerException;
        Assert.Equal(2, win32Ex.NativeErrorCode); // ERROR_FILE_NOT_FOUND
    }

    [Fact]
    public void LStat_LongPath_HandlesCorrectly()
    {
        // Arrange - Create a deeply nested directory structure
        var longPath = _testDirPath;
        for (int i = 0; i < 10; i++)
        {
            longPath = Path.Combine(longPath, $"subdir_{i}");
        }
        Directory.CreateDirectory(longPath);

        var longFilePath = Path.Combine(longPath, "test_file.txt");
        File.WriteAllText(longFilePath, "Test");

        // Act
        var result = FileSystem.LStat(longFilePath);

        // Assert
        Assert.NotNull(result);
        var resultObj = result.AsObject();

        // Should successfully get metadata for long path
        Assert.True(resultObj.ContainsKey("st_size"));
        Assert.True(resultObj.ContainsKey("S_ISREG"));
    }
}
#endif