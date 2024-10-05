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


    public static JsonNode? LStat(string path) => ToNode(lstat(path), nameof(lstat));
    public static JsonNode? ReadLink(string path) => ToNode(readlink(path), nameof(readlink));
    public static JsonNode? Canonicalizefilename(string path) => ToNode(canonicalize_file_name(path), nameof(canonicalize_file_name));

    public static bool isDir(JsonNode? buf) => (buf?["st_mode"]?.GetValue<ulong>() & 0xF000) == 0x4000;
    public static bool isReg(JsonNode? buf) => (buf?["st_mode"]?.GetValue<ulong>() & 0xF000) == 0x8000;
    public static bool isLnk(JsonNode? buf) => (buf?["st_mode"]?.GetValue<ulong>() & 0xF000) == 0xA000;
}