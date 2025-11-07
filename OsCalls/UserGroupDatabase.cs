using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using static OsCalls.ValXfer;

namespace OsCalls;

/// <summary>
///     Access to system user/group databases via native libc calls (getpwuid/getgrgid).
///     Returned values are mapped into JSON using the <see cref="ValXfer" /> bridge.
/// </summary>
public static unsafe class UserGroupDatabase
{
    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* getpwuid(ulong uid);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* getgrgid(ulong gid);

    /// <summary>
    ///     Retrieves passwd database entry for a user id.
    /// </summary>
    /// <param name="uid">Numeric user id.</param>
    /// <returns>A JsonNode with typical fields like pw_name, pw_uid, pw_gid, etc.</returns>
    public static JsonNode GetPwUid(ulong uid)
    {
        return ToNode(getpwuid(uid), $"user {uid}", nameof(getpwuid));
    }

    /// <summary>
    ///     Retrieves group database entry for a group id.
    /// </summary>
    /// <param name="gid">Numeric group id.</param>
    /// <returns>A JsonNode with fields like gr_name, gr_gid, etc.</returns>
    public static JsonNode GetGrGid(ulong gid)
    {
        return ToNode(getgrgid(gid), $"group {gid}", nameof(getgrgid));
    }
}
