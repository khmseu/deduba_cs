using System.Runtime.CompilerServices;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using OsCallsCommon;
using static OsCallsCommon.ValXfer;

namespace OsCallsLinux;

/// <summary>
///     Access to system user/group databases via native libc calls (getpwuid/getgrgid).
///     Returned values are mapped into JSON using the <see cref="ValXfer" /> bridge.
/// </summary>
public static unsafe partial class UserGroupDatabase
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ShimPwUidDelegate(long uid);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate IntPtr ShimGrGidDelegate(long gid);
    private static ShimPwUidDelegate? _linux_getpwuid;
    private static ShimGrGidDelegate? _linux_getgrgid;
    [LibraryImport("libOsCallsLinuxShim.so")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValueT* getpwuid(long uid);

    [LibraryImport("libOsCallsLinuxShim.so")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValueT* getgrgid(long gid);

    static UserGroupDatabase()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var candidateDebug = Path.Combine(baseDir, "OsCallsLinuxShim", "bin", "Debug", "net8.0", "libOsCallsLinuxShim.so");
            var candidateRelease = Path.Combine(baseDir, "OsCallsLinuxShim", "bin", "Release", "net8.0", "libOsCallsLinuxShim.so");
            var full = File.Exists(candidateDebug) ? candidateDebug : (File.Exists(candidateRelease) ? candidateRelease : null);
            if (!string.IsNullOrWhiteSpace(full))
            {
                var handle = NativeLibrary.Load(full);
                if (NativeLibrary.TryGetExport(handle, "linux_getpwuid", out var ptr))
                    _linux_getpwuid = Marshal.GetDelegateForFunctionPointer<ShimPwUidDelegate>(ptr);
                if (NativeLibrary.TryGetExport(handle, "linux_getgrgid", out ptr))
                    _linux_getgrgid = Marshal.GetDelegateForFunctionPointer<ShimGrGidDelegate>(ptr);
            }
        }
        catch { }
    }

    /// <summary>
    ///     Retrieves passwd database entry for a user id.
    /// </summary>
    /// <param name="uid">Numeric user id.</param>
    /// <returns>A JsonNode with typical fields like pw_name, pw_uid, pw_gid, etc.</returns>
    public static JsonNode GetPwUid(long uid)
    {
           return LinuxGetPwUid(uid);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for GetPwUid.
    /// </summary>
        public static JsonNode LinuxGetPwUid(long uid)
        {
            if (_linux_getpwuid is not null)
            {
                var ptr = _linux_getpwuid(uid);
                return ToNode((ValueT*)ptr, $"user {uid}", "linux_getpwuid");
            }
            return ToNode(getpwuid(uid), $"user {uid}", nameof(getpwuid));
        }

    /// <summary>
    ///     Retrieves group database entry for a group id.
    /// </summary>
    /// <param name="gid">Numeric group id.</param>
    /// <returns>A JsonNode with fields like gr_name, gr_gid, etc.</returns>
    public static JsonNode GetGrGid(long gid)
    {
           return LinuxGetGrGid(gid);
    }

    /// <summary>
    ///     Platform-prefixed wrapper for GetGrGid.
    /// </summary>
        public static JsonNode LinuxGetGrGid(long gid)
        {
            if (_linux_getgrgid is not null)
            {
                var ptr = _linux_getgrgid(gid);
                return ToNode((ValueT*)ptr, $"group {gid}", "linux_getgrgid");
            }
            return ToNode(getgrgid(gid), $"group {gid}", nameof(getgrgid));
        }
}
