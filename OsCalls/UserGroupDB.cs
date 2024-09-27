using System.Runtime.InteropServices;
using static OsCalls.ValXfer;

namespace OsCalls;

public unsafe static class  UserGroupDB{
    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern TValue* getpwuid(UInt64 uid);
    
        [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]

    public static extern TValue* getgrgid(UInt64 gid);
}