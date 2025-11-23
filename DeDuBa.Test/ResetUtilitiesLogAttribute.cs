using System.Reflection;
using Xunit;
using Xunit.Sdk;
using UtilitiesLibrary;

namespace DeDuBa.Test;

/// <summary>
/// Ensures Utilities.Log is closed and set to null before and after each test; also enforces testing mode.
/// </summary>
public sealed class ResetUtilitiesLogAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest)
    {
        Utilities.Testing = true;
        try
        {
            Utilities.Log?.Close();
        }
        catch { }
        Utilities.Log = null;
    }

    public override void After(MethodInfo methodUnderTest)
    {
        try
        {
            Utilities.Log?.Close();
        }
        catch { }
        Utilities.Log = null;
    }
}
