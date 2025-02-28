using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using static OsCalls.ValXfer;

namespace OsCalls;

public static unsafe class UserGroupDatabase
{
    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* getpwuid(ulong uid);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* getgrgid(ulong gid);

    public static JsonNode GetPwUid(ulong uid)
    {
        return ToNode(getpwuid(uid), $"user {uid}", nameof(getpwuid));
    }

    public static JsonNode GetGrGid(ulong gid)
    {
        return ToNode(getgrgid(gid), $"group {gid}", nameof(getgrgid));
    }
}