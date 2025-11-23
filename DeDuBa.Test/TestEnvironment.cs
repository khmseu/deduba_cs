using UtilitiesLibrary;
using Xunit;

namespace DeDuBa.Test;

/// <summary>
/// Test environment fixture that ensures Utilities.Log is closed and Utilities.Testing is set.
/// Applied as a collection fixture across tests to avoid leaking test state between tests.
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
        catch { }
        Utilities.Log = null;
    }

    public void Dispose()
    {
        try
        {
            Utilities.Log?.Close();
        }
        catch { }
        Utilities.Log = null;
    }
}

[CollectionDefinition("TestEnvironment")]
public class TestEnvironmentCollection : ICollectionFixture<TestEnvironment>
{
    // Nothing to implement; this binds the TestEnvironment fixture to the "TestEnvironment" collection.
}
