using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using static OsCalls.ValXfer;

namespace OsCalls;

/// <summary>
///     Access to system user/group databases via native libc calls (getpwuid/getgrgid).
///     Returned values are mapped into JSON using the <see cref="ValXfer" /> bridge.
/// </summary>
public static unsafe partial class UserGroupDatabase
{
    [LibraryImport("libOsCallsShim.so")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValueT* getpwuid(long uid);

    [LibraryImport("libOsCallsShim.so")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    private static partial ValueT* getgrgid(long gid);

    /// <summary>
    ///     Retrieves passwd database entry for a user id.
    /// </summary>
    /// <param name="uid">Numeric user id.</param>
    /// <returns>A JsonNode with typical fields like pw_name, pw_uid, pw_gid, etc.</returns>
    public static JsonNode GetPwUid(long uid)
    {
        return ToNode(getpwuid(uid), $"user {uid}", nameof(getpwuid));
    }

    /// <summary>
    ///     Retrieves group database entry for a group id.
    /// </summary>
    /// <param name="gid">Numeric group id.</param>
    /// <returns>A JsonNode with fields like gr_name, gr_gid, etc.</returns>
    public static JsonNode GetGrGid(long gid)
    {
        return ToNode(getgrgid(gid), $"group {gid}", nameof(getgrgid));
    }
}
