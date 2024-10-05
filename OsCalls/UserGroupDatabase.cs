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

    public static JsonNode? GetPwUid(ulong uid) => ToNode(getpwuid(uid), nameof(getpwuid));

    public static JsonNode? GetGrGid(ulong gid) => ToNode(getgrgid(gid), nameof(getgrgid));
}