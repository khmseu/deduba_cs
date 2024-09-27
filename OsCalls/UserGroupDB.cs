namespace OsCalls;

public unsafe class UserGroupDB{
    [LibraryImport("libOsCallsShim.so")]
public static extern    TValue*getpwuid(UInt64 uid);    
 [LibraryImport("libOsCallsShim.so")]
public static extern    TValue*getpwuid(UInt64 uid);}