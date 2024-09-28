using System.Runtime.InteropServices;

namespace OsCalls;

public static unsafe class ValXfer
{
    public enum TypeT
    {
        None = 0,
        IsNumber,
        IsString,
        IsComplex
    }

    [DllImport("libOsCalls.so", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool GetNextValue(ValueT* value);

    [DllImport("libOsCalls.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void CreateHandle(ValueT* value, void* handler, void* data);

    [StructLayout(LayoutKind.Sequential)]
    public struct HandleT
    {
        private readonly void* handler;
        private readonly void* data;
        public Int64 index;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ValueT
    {
        public readonly HandleT Handle;
        public readonly TypeT Type;
        public readonly Int64 Number;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public readonly IntPtr String;
        public readonly HandleT Complex;
    }
}