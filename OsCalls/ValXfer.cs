using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace OsCalls;

public static unsafe class ValXfer
{
    public enum TypeT
    {
        IsOk = 0,
        IsError,
        IsNumber,
        IsString,
        IsComplex
    }

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool GetNextValue(ValueT* value);

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern void CreateHandle(ValueT* value, void* handler, void* data1, void* data2);

    public static JsonNode ToNode(ValueT* value, string op)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        var maybeError = (int)value->Number;
        var more = GetNextValue(value);
        if (value->Type == TypeT.IsError) throw new Win32Exception(maybeError, op);
        var name = Marshal.PtrToStringUTF8(value->Name) ??
                   throw new ArgumentNullException(nameof(value), $"{op} Feld {nameof(ValueT.Name)}");
        if (name == "[]")
        {
            var array = new JsonArray();
            while (more)
            {
                switch (value->Type)
                {
                    case TypeT.IsNumber:
                        array.Add(value->Number);
                        break;
                    case TypeT.IsString:
                        array.Add(Marshal.PtrToStringUTF8(value->String));
                        break;
                    case TypeT.IsComplex:
                        array.Add(ToNode(value->Complex, $"{op}[{value->Handle.index}]"));
                        break;
                    default:
                        throw new ArgumentException($"{op} Invalid ValueT type {value->Type:G}.{value->Handle.index}",
                            nameof(value));
                }

                more = GetNextValue(value);
            }

            return array;
        }

        var obj = new JsonObject();
        while (more)
        {
            switch (value->Type)
            {
                case TypeT.IsNumber:
                    obj[name] = value->Number;
                    break;
                case TypeT.IsString:
                    obj[name] = Marshal.PtrToStringUTF8(value->String);
                    break;
                case TypeT.IsComplex:
                    obj[name] = ToNode(value->Complex, $"{op}.{name}");
                    break;
                default:
                    throw new ArgumentException($"{op} Invalid ValueT type {value->Type:G}.{value->Handle.index}",
                        nameof(value));
            }

            more = GetNextValue(value);
            name = Marshal.PtrToStringUTF8(value->Name) ??
                   throw new ArgumentNullException(nameof(value), $"{op} Feld {nameof(ValueT.Name)}");
        }

        return obj;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HandleT
    {
        private readonly void* handler;
        private readonly void* data1;
        private readonly void* data2;
        public Int64 index;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ValueT
    {
        public readonly HandleT Handle;
        public readonly TypeT Type;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public readonly IntPtr Name;
        public readonly Int64 Number;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public readonly IntPtr String;
        public readonly ValueT* Complex;
    }
}