using System.Runtime.InteropServices;
using static OsCalls.ValXfer;

namespace OsCalls;

public static unsafe class UserGroupDatabase
{
    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern ValueT* getpwuid(ulong uid);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern ValueT* getgrgid(ulong gid);
}