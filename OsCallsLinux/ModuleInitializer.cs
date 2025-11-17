using System.Runtime.CompilerServices;

namespace OsCallsLinux;

internal static class ModuleInit
{
    // Ensure the platform-specific ValXfer delegate is initialized as soon as this
    // assembly is loaded, regardless of which OsCallsLinux class is used first.
    #pragma warning disable CA2255 // The 'ModuleInitializer' attribute should not be used in libraries
    [ModuleInitializer]
    internal static void Initialize()
    {
        // Trigger static constructor of FileSystem which assigns ValXfer.PlatformGetNextValue
        RuntimeHelpers.RunClassConstructor(typeof(FileSystem).TypeHandle);
    }
    #pragma warning restore CA2255
}
