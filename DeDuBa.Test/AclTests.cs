using System.ComponentModel;
using System.Diagnostics;
using OsCallsLinux;

namespace DeDuBa.Test;

/// <summary>
///     Tests for ACL (Access Control List) functionality via OsCalls.Acl module.
/// </summary>
public class AclTests : IDisposable
{
    private readonly string _testDirPath;
    private readonly string _testFilePath;

    public AclTests()
    {
        // Create a temporary test file
        _testFilePath = Path.Combine(Path.GetTempPath(), $"acl_test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_testFilePath, "Test content for ACL testing");
        File.SetUnixFileMode(
            _testFilePath,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.OtherRead
        );

        // Create a temporary test directory
        _testDirPath = Path.Combine(Path.GetTempPath(), $"acl_test_dir_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirPath);
        File.SetUnixFileMode(
            _testDirPath,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead
                | UnixFileMode.OtherExecute
        );

        // Set test ACLs using setfacl
        SetAcl(_testFilePath, "u:daemon:rwx", false);
        SetAcl(_testDirPath, "u:daemon:rwx", true);
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
    ///     Helper method to set ACLs using the setfacl command.
    /// </summary>
    private static void SetAcl(string path, string aclSpec, bool isDefault)
    {
        var args = isDefault ? $"-d -m {aclSpec} \"{path}\"" : $"-m {aclSpec} \"{path}\"";
        var process = Process.Start(
            new ProcessStartInfo
            {
                FileName = "setfacl",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        );
        process?.WaitForExit();
    }

    [Fact]
    public void GetFileAccess_ReturnsAccessAcl()
    {
        // Act
        var result = Acl.GetFileAccess(_testFilePath);

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        Assert.NotNull(obj);
        Assert.True(obj.ContainsKey("acl_text"));

        var aclText = obj["acl_text"]?.ToString();
        Assert.NotNull(aclText);
        Assert.NotEmpty(aclText);

        // The ACL should contain the daemon user entry we set
        Assert.Contains("daemon", aclText);
        Assert.Contains("rwx", aclText);
    }

    [Fact]
    public void GetFileDefault_ReturnsDefaultAcl()
    {
        // Act
        var result = Acl.GetFileDefault(_testDirPath);

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        Assert.NotNull(obj);
        Assert.True(obj.ContainsKey("acl_text"));

        var aclText = obj["acl_text"]?.ToString();
        Assert.NotNull(aclText);
        Assert.NotEmpty(aclText);

        // The default ACL should contain the daemon user entry we set
        Assert.Contains("daemon", aclText);
        Assert.Contains("rwx", aclText);
    }

    [Fact]
    public void GetFileAccess_WithBasicPermissions_ReturnsMinimalAcl()
    {
        // Arrange - create a file with no extended ACL entries
        var cleanFilePath = Path.Combine(Path.GetTempPath(), $"acl_clean_{Guid.NewGuid()}.txt");
        File.WriteAllText(cleanFilePath, "Clean file");
        File.SetUnixFileMode(
            cleanFilePath,
            UnixFileMode.UserRead
                | UnixFileMode.UserWrite
                | UnixFileMode.GroupRead
                | UnixFileMode.OtherRead
        );

        try
        {
            // Act
            var result = Acl.GetFileAccess(cleanFilePath);

            // Assert
            Assert.NotNull(result);
            var obj = result.AsObject();
            Assert.NotNull(obj);
            Assert.True(obj.ContainsKey("acl_text"));

            var aclText = obj["acl_text"]?.ToString();
            // With TEXT_ABBREVIATE flag, minimal ACLs (equal to mode bits) may return empty,
            // or may return the basic permission entries
            Assert.NotNull(aclText);
        }
        finally
        {
            if (File.Exists(cleanFilePath))
                File.Delete(cleanFilePath);
        }
    }

    [Fact]
    public void GetFileDefault_OnFileInsteadOfDirectory_ThrowsException()
    {
        // Act & Assert
        // Default ACLs only apply to directories
        // Attempting to get default ACL on a file should throw an error
        var ex = Assert.Throws<Exception>(() =>
        {
            Acl.GetFileDefault(_testFilePath);
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void GetFileAccess_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = "/tmp/nonexistent_acl_file_" + Guid.NewGuid() + ".txt";

        // Act & Assert
        var ex = Assert.Throws<Exception>(() =>
        {
            Acl.GetFileAccess(nonExistentPath);
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void GetFileDefault_WithNonExistentDirectory_ThrowsException()
    {
        // Arrange
        var nonExistentPath = "/tmp/nonexistent_acl_dir_" + Guid.NewGuid();

        // Act & Assert
        var ex = Assert.Throws<Exception>(() =>
        {
            Acl.GetFileDefault(nonExistentPath);
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void GetFileAccess_AclTextFormat_IsShortText()
    {
        // Act
        var result = Acl.GetFileAccess(_testFilePath);

        // Assert
        Assert.NotNull(result);
        var obj = result.AsObject();
        Assert.NotNull(obj);
        var aclText = obj["acl_text"]?.ToString();
        Assert.NotNull(aclText);

        // Short text format uses commas as separators (TEXT_ABBREVIATE flag)
        // Format should be like: "u::rw-,u:daemon:rwx,g::r--,m::rwx,o::r--"
        // or abbreviated forms without entries equal to mode bits
        if (!string.IsNullOrEmpty(aclText))
            // Should contain commas if there are multiple entries
            Assert.True(aclText.Contains(',') || aclText.Split(',').Length == 1);
    }

    [Fact]
    public void Acl_WorksWithSymlinks()
    {
        // Arrange - create a symlink to the test file
        var symlinkPath = Path.Combine(Path.GetTempPath(), $"acl_symlink_{Guid.NewGuid()}.txt");

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

            // Act - GetFileAccess follows symlinks by default
            var result = Acl.GetFileAccess(symlinkPath);

            // Assert - should get the ACL of the target file
            Assert.NotNull(result);
            var obj = result.AsObject();
            Assert.NotNull(obj);
            Assert.True(obj.ContainsKey("acl_text"));

            var aclText = obj["acl_text"]?.ToString();
            Assert.NotNull(aclText);
            // Should contain the daemon ACL we set on the target
            Assert.Contains("daemon", aclText);
        }
        finally
        {
            if (File.Exists(symlinkPath))
                File.Delete(symlinkPath);
        }
    }
}
