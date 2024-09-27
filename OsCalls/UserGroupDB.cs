using System.Runtime.InteropServices;
using static OsCalls.ValXfer;

namespace OsCalls;

public static unsafe class UserGroupDB
{
    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern TValue* getpwuid(ulong uid);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern TValue* getgrgid(ulong gid);
}