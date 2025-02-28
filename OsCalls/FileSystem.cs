using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using static OsCalls.ValXfer;

namespace OsCalls;

public static unsafe class FileSystem
{
    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* lstat(string path);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* readlink(string path);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern ValueT* canonicalize_file_name(string path);


    public static JsonNode LStat(string path)
    {
        return ToNode(lstat(path), path, nameof(lstat));
    }

    public static JsonNode ReadLink(string path)
    {
        return ToNode(readlink(path), path, nameof(readlink));
    }

    public static JsonNode Canonicalizefilename(string path)
    {
        return ToNode(canonicalize_file_name(path), path, nameof(canonicalize_file_name));
    }

    public static bool IsDir(JsonNode? buf)
    {
        return (buf?["st_mode"]?.GetValue<ulong>() & 0xF000) == 0x4000;
    }

    public static bool IsReg(JsonNode? buf)
    {
        return (buf?["st_mode"]?.GetValue<ulong>() & 0xF000) == 0x8000;
    }

    public static bool IsLnk(JsonNode? buf)
    {
        return (buf?["st_mode"]?.GetValue<ulong>() & 0xF000) == 0xA000;
    }
}