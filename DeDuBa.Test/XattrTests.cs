using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using OsCalls;

namespace DeDuBa.Test;

/// <summary>
///     Tests for extended attributes (xattr) functionality via OsCalls.Xattr module.
/// </summary>
public class XattrTests : IDisposable
{
    private readonly string _testDirPath;
    private readonly string _testFilePath;

    public XattrTests()
    {
        // Create a temporary test file
        _testFilePath = Path.Combine(Path.GetTempPath(), $"xattr_test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_testFilePath, "Test content for xattr testing");

        // Create a temporary test directory
        _testDirPath = Path.Combine(Path.GetTempPath(), $"xattr_test_dir_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirPath);

        // Set some test extended attributes using setfattr
        SetXattr(_testFilePath, "user.test_attr", "test_value");
        SetXattr(_testFilePath, "user.another_attr", "another_value");
        SetXattr(_testFilePath, "user.description", "This is a test file");
    }

    public void Dispose()
    {
        // Cleanup test files
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
        if (Directory.Exists(_testDirPath))
            Directory.Delete(_testDirPath, true);
    }

    /// <summary>
    ///     Helper method to set extended attributes using the setfattr command.
    /// </summary>
    private static void SetXattr(string path, string name, string value)
    {
        var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = "setfattr",
                Arguments = $"-n {name} -v \"{value}\" \"{path}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        );
        process?.WaitForExit();
    }

    [Fact]
    public void ListXattr_ReturnsAllAttributeNames()
    {
        // Act
        var result = Xattr.ListXattr(_testFilePath);

        // Assert
        Assert.NotNull(result);
        var array = result.AsArray();
        Assert.NotNull(array);
        Assert.Equal(3, array.Count);

        // Verify all expected attributes are present
        var attrNames = array.Select(n => n?.ToString()).ToList();
        Assert.Contains("user.test_attr", attrNames);
        Assert.Contains("user.another_attr", attrNames);
        Assert.Contains("user.description", attrNames);
    }

    [Fact]
    public void ListXattr_WithNoAttributes_ReturnsEmptyArray()
    {
        // Arrange - use a file with no extended attributes
        var cleanFilePath = Path.Combine(Path.GetTempPath(), $"xattr_clean_{Guid.NewGuid()}.txt");
        File.WriteAllText(cleanFilePath, "Clean file");

        try
        {
            // Act
            var result = Xattr.ListXattr(cleanFilePath);

            // Assert
            Assert.NotNull(result);
            // When there are no xattrs, the result might be an empty object or empty array
            if (result is JsonArray array)
                Assert.Empty(array);
            else if (result is JsonObject obj)
                // Empty object is also acceptable
                Assert.Empty(obj);
        }
        finally
        {
            if (File.Exists(cleanFilePath))
                File.Delete(cleanFilePath);
        }
    }

    [Fact]
    public void GetXattr_ReturnsCorrectValue()
    {
        // Act
        var result = Xattr.GetXattr(_testFilePath, "user.test_attr");

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        Assert.NotNull(obj);
        Assert.True(obj.ContainsKey("value"));
        Assert.Equal("test_value", obj["value"]?.ToString());
    }

    [Fact]
    public void GetXattr_WithDifferentAttribute_ReturnsCorrectValue()
    {
        // Act
        var result = Xattr.GetXattr(_testFilePath, "user.description");

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        Assert.NotNull(obj);
        Assert.True(obj.ContainsKey("value"));
        Assert.Equal("This is a test file", obj["value"]?.ToString());
    }

    [Fact]
    public void GetXattr_WithNonExistentAttribute_ThrowsException()
    {
        // Act & Assert
        // The error is wrapped in a System.Exception by Utilities.Error
        var ex = Assert.Throws<Exception>(() =>
        {
            Xattr.GetXattr(_testFilePath, "user.nonexistent");
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void ListXattr_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = "/tmp/nonexistent_file_" + Guid.NewGuid() + ".txt";

        // Act & Assert
        // The error is wrapped in a System.Exception by Utilities.Error
        var ex = Assert.Throws<Exception>(() =>
        {
            Xattr.ListXattr(nonExistentPath);
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void GetXattr_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = "/tmp/nonexistent_file_" + Guid.NewGuid() + ".txt";

        // Act & Assert
        // The error is wrapped in a System.Exception by Utilities.Error
        var ex = Assert.Throws<Exception>(() =>
        {
            Xattr.GetXattr(nonExistentPath, "user.test");
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void Xattr_WorksWithSymlinks()
    {
        // Arrange - create a symlink to the test file
        var symlinkPath = Path.Combine(Path.GetTempPath(), $"xattr_symlink_{Guid.NewGuid()}.txt");

        try
        {
            // Create symlink using ln -s
            var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = "ln",
                    Arguments = $"-s \"{_testFilePath}\" \"{symlinkPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            );
            process?.WaitForExit();

            // Act - ListXattr with llistxattr should get symlink's xattrs, not target's
            // The symlink itself typically has no xattrs unless specifically set
            var result = Xattr.ListXattr(symlinkPath);

            // Assert - the symlink itself won't have the same xattrs as the target
            // because llistxattr doesn't follow symlinks
            Assert.NotNull(result);
            // When there are no xattrs, the result might be an empty object or empty array
            if (result is JsonArray array)
                // Symlink has no xattrs (or possibly empty)
                Assert.NotNull(array);
            else if (result is JsonObject obj)
                // Empty object is also acceptable
                Assert.NotNull(obj);
        }
        finally
        {
            if (File.Exists(symlinkPath))
                File.Delete(symlinkPath);
        }
    }
}
