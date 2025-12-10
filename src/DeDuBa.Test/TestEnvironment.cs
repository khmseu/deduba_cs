using UtilitiesLibrary;

namespace DeDuBa.Test;

/// <summary>
///     Test environment fixture that ensures Utilities.Log is closed and Utilities.Testing is set.
///     Applied as a collection fixture across tests to avoid leaking test state between tests.
/// </summary>
public class TestEnvironment : IDisposable
{
    public TestEnvironment()
    {
        Utilities.Testing = true;
        try
        {
            Utilities.Log?.Close();
        }
        catch
        {
        }

        Utilities.Log = null;
        // Ensure BackupConfig singleton is initialized for tests that use BackupConfig.Instance.
        try
        {
            var tmpArchive = Path.Combine(
                Path.GetTempPath(),
                "deduba_test_archive_" + Guid.NewGuid().ToString("N")
            );
            Directory.CreateDirectory(tmpArchive);
            BackupConfig.SetInstance(new BackupConfig(tmpArchive, 1024 * 16, true));
        }
        catch
        {
            // If for any reason this fails, tests that rely on explicit construction
            // should still work because they construct their own BackupConfig instances.
        }
    }

    public void Dispose()
    {
        try
        {
            Utilities.Log?.Close();
        }
        catch
        {
        }

        Utilities.Log = null;
    }
}

[CollectionDefinition("TestEnvironment")]
public class TestEnvironmentCollection : ICollectionFixture<TestEnvironment>
{
    // Nothing to implement; this binds the TestEnvironment fixture to the "TestEnvironment" collection.
}