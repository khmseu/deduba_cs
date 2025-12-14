using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;

namespace OsCallsWindows;

/// <summary>
///     Wrapper for native Windows security descriptor reading functions.
///     Provides methods to read security descriptors in SDDL (Security Descriptor Definition Language) format.
/// </summary>
public static unsafe partial class Security
{
    private const string NativeLibraryName = "OsCallsWindowsShimNative.dll";

    /// <summary>
    ///     Reads the security descriptor from the specified filesystem path.
    ///     Returns the descriptor in SDDL format (includes Owner, Group, DACL, and optionally SACL).
    /// </summary>
    /// <param name="path">Filesystem path to read security descriptor from.</param>
    /// <param name="includeSacl">Whether to include SACL (requires SeSecurityPrivilege).</param>
    /// <returns>JsonNode with "sddl" field containing the security descriptor string, or error.</returns>
    public static JsonNode GetSecurityDescriptor(string path, bool includeSacl = false)
    {
        return ValXfer.ToNode(win_get_sd(path, includeSacl), path, nameof(win_get_sd));
    }

    /// <summary>
    ///     Reads security descriptor using Windows GetNamedSecurityInfoW API.
    ///     Primary implementation - wraps windows_GetNamedSecurityInfoW native export.
    /// </summary>
    /// <param name="path">Filesystem path to read security descriptor from.</param>
    /// <param name="includeSacl">Whether to include SACL (requires SeSecurityPrivilege).</param>
    /// <returns>JsonNode with "sddl" field containing the security descriptor string, or error.</returns>
    public static JsonNode WindowsGetNamedSecurityInfoW(string path, bool includeSacl = false)
    {
        return ValXfer.ToNode(
            windows_GetNamedSecurityInfoW(path, includeSacl),
            path,
            nameof(windows_GetNamedSecurityInfoW)
        );
    }

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValXfer.ValueT* windows_GetNamedSecurityInfoW(
        string path,
        [MarshalAs(UnmanagedType.Bool)] bool includeSacl
    );

    [LibraryImport(NativeLibraryName, StringMarshalling = StringMarshalling.Utf16)]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static partial ValXfer.ValueT* win_get_sd(string path, [MarshalAs(UnmanagedType.Bool)] bool includeSacl);
}
