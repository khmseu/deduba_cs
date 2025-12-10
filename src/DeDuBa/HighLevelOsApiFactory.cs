#if DEDUBA_LINUX
using OsCallsLinux;
#endif

#if DEDUBA_WINDOWS
using OsCallsWindows;
#endif

namespace OsCallsCommon;

/// <summary>
///     Factory class for creating platform-specific IHighLevelOsApi implementations.
/// </summary>
public static class HighLevelOsApiFactory
{
    /// <summary>
    ///     Get singleton instance of platform-specific IHighLevelOsApi implementation.
    /// </summary>
    /// <returns>Platform-specific IHighLevelOsApi instance.</returns>
    public static IHighLevelOsApi GetOsApi()
    {
#if DEDUBA_LINUX
        return LinuxHighLevelOsApi.Instance;
#endif
#if DEDUBA_WINDOWS
        return WindowsHighLevelOsApi.Instance;
#endif
    }
}