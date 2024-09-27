using System.Runtime.InteropServices;

namespace OsCalls;

public unsafe class ValXfer
{
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct THandle
    {
        private readonly void* handler;
        private readonly void* data;
        public Int64 index;
    }
    public enum TType
    {
        None = 0,
        IsNumber,
        IsString,
        IsComplex,
    }
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TValue
    {
        public readonly THandle Handle;
        public readonly TType Type;
        public readonly Int64 Number;
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public readonly IntPtr String;
        public readonly THandle Complex;
    }
    [DllImport("libOsCalls.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool GetNextValue(TValue* value);
    [DllImport("libOsCalls.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void CreateHandle(TValue* value, void* handler, void* data);
        }