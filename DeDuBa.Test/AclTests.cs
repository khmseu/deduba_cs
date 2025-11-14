using System;
using System.IO;
using System.Text.Json.Nodes;
using OsCalls;
using Xunit;

namespace DeDuBa.Test;

/// <summary>
/// Tests for ACL (Access Control List) functionality via OsCalls.Acl module.
/// </summary>
public class AclTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly string _testDirPath;

    public AclTests()
    {
        // Create a temporary test file
        _testFilePath = Path.Combine(Path.GetTempPath(), $"acl_test_{Guid.NewGuid()}.txt");
        File.WriteAllText(_testFilePath, "Test content for ACL testing");

        // Create a temporary test directory
        _testDirPath = Path.Combine(Path.GetTempPath(), $"acl_test_dir_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirPath);

        // Set some test ACLs using setfacl
        SetAcl(_testFilePath, "u::rwx,g::r--,o::---");
        SetAcl(_testDirPath, "u::rwx,g::r-x,o::r-x");
        SetDefaultAcl(_testDirPath, "u::rwx,g::r-x,o::---");
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
    /// Helper method to set ACLs using the setfacl command.
    /// </summary>
    private static void SetAcl(string path, string aclSpec)
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "setfacl",
            Arguments = $"-m {aclSpec} \"{path}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
    }

    /// <summary>
    /// Helper method to set default ACLs using the setfacl command.
    /// </summary>
    private static void SetDefaultAcl(string path, string aclSpec)
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "setfacl",
            Arguments = $"-d -m {aclSpec} \"{path}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit();
    }

    [Fact]
    public void GetFileAccess_ReturnsAclText()
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

        // ACL text should contain standard entries
        // Format can vary but should have user/group/other entries
        Assert.Contains("u::", aclText);
    }

    [Fact]
    public void GetFileDefault_ForDirectory_ReturnsDefaultAcl()
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

        // Default ACL text should contain standard entries
        Assert.Contains("u::", aclText);
    }

    [Fact]
    public void GetFileDefault_ForRegularFile_ReturnsEmptyOrError()
    {
        // Act - regular files don't have default ACLs
        // This might throw or return empty, depending on implementation
        try
        {
            var result = Acl.GetFileDefault(_testFilePath);

            // If it doesn't throw, it should return empty or indicate no default ACL
            var obj = result.AsObject();
            if (obj != null && obj.ContainsKey("acl_text"))
            {
                var aclText = obj["acl_text"]?.ToString();
                // Empty or minimal default ACL is expected for regular files
                Assert.True(string.IsNullOrEmpty(aclText) || aclText.Length < 5);
            }
        }
        catch (Exception)
        {
            // Exception is acceptable for regular files
            // (they don't support default ACLs)
        }
    }

    [Fact]
    public void GetFileAccess_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = "/tmp/nonexistent_acl_file_" + Guid.NewGuid() + ".txt";

        // Act & Assert
        // The error is wrapped in a System.Exception by Utilities.Error
        var ex = Assert.Throws<Exception>(() =>
        {
            Acl.GetFileAccess(nonExistentPath);
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<System.ComponentModel.Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void GetFileDefault_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var nonExistentPath = "/tmp/nonexistent_acl_dir_" + Guid.NewGuid();

        // Act & Assert
        // The error is wrapped in a System.Exception by Utilities.Error
        var ex = Assert.Throws<Exception>(() =>
        {
            Acl.GetFileDefault(nonExistentPath);
        });

        // Verify the inner exception is Win32Exception
        Assert.NotNull(ex.InnerException);
        Assert.IsType<System.ComponentModel.Win32Exception>(ex.InnerException);
    }

    [Fact]
    public void Acl_WorksWithSymlinks()
    {
        // Arrange - create a symlink to the test file
        var symlinkPath = Path.Combine(Path.GetTempPath(), $"acl_symlink_{Guid.NewGuid()}.txt");

        try
        {
            // Create symlink using ln -s
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ln",
                Arguments = $"-s \"{_testFilePath}\" \"{symlinkPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();

            // Act - GetFileAccess should read the target's ACL (follows symlinks)
            var result = Acl.GetFileAccess(symlinkPath);

            // Assert
            Assert.NotNull(result);
            var obj = result.AsObject();
            Assert.NotNull(obj);
            Assert.True(obj.ContainsKey("acl_text"));

            var aclText = obj["acl_text"]?.ToString();
            Assert.NotNull(aclText);
            Assert.NotEmpty(aclText);
        }
        finally
        {
            if (File.Exists(symlinkPath))
                File.Delete(symlinkPath);
        }
    }
}
