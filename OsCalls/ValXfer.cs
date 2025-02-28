using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using UtilitiesLibrary;

namespace OsCalls;

public static unsafe class ValXfer
{
    public enum TypeT
    {
        // ReSharper disable UnusedMember.Global
        IsOk = 0,
        IsError,
        IsNumber,
        IsString,
        IsComplex,
        IsTimeSpec
    }

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool GetNextValue(ValueT* value);


    public static JsonNode ToNode(ValueT* value, string file, string op)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        ShowValue(value, "", op);
        var maybeError = (int)value->Number;
        var wasOk = value->Type;
        var more = GetNextValue(value);
        if (wasOk == TypeT.IsError)
        {
            var win32Exception =
                new Win32Exception(maybeError); //$"{nameof(ToNode)} found {op} caused error {maybeError}"
            Utilities.Error(file, op, win32Exception);
            throw win32Exception;
        }

        var name = Marshal.PtrToStringUTF8(value->Name) ??
                   throw new ArgumentNullException(nameof(value), $"{op} Feld {nameof(ValueT.Name)}");
        if (name == "[]")
        {
            var array = new JsonArray();
            while (more)
            {
                ShowValue(value, name, op);
                switch (value->Type)
                {
                    case TypeT.IsNumber:
                        array.Add(value->Number);
                        break;
                    case TypeT.IsString:
                        array.Add(Marshal.PtrToStringUTF8(value->String));
                        break;
                    case TypeT.IsComplex:
                        array.Add(ToNode(value->Complex, file, $"{op}[{value->Handle.index}]"));
                        break;
                    case TypeT.IsTimeSpec:
                        array.Add(value->TimeSpec.TvSec + value->TimeSpec.TvNsec / (double)(1000 * 1000 * 1000));
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
            ShowValue(value, name, op);
            switch (value->Type)
            {
                case TypeT.IsNumber:
                    obj[name] = value->Number;
                    break;
                case TypeT.IsString:
                    obj[name] = Marshal.PtrToStringUTF8(value->String);
                    break;
                case TypeT.IsComplex:
                    obj[name] = ToNode(value->Complex, file, $"{op}.{name}");
                    break;
                case TypeT.IsTimeSpec:
                    obj[name] = value->TimeSpec.TvSec + value->TimeSpec.TvNsec / (double)(1000 * 1000 * 1000);
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

        static void ShowValue(ValueT* value, string name, string op)
        {
            if (false)
#pragma warning disable CS0162 // Unerreichbarer Code wurde entdeckt.
                Utilities.ConWrite(
                    Utilities.Dumper(Utilities.D(op),
                        Utilities.D(name),
                        Utilities.D(value->Handle.index),
                        Utilities.D((ulong)value->Handle.data1),
                        Utilities.D((ulong)value->Handle.data2),
                        Utilities.D(value->Type),
                        Utilities.D((ulong)value->Name),
                        Utilities.D(Marshal.PtrToStringUTF8(value->Name)),
                        Utilities.D(value->Number),
                        Utilities.D((ulong)value->String),
                        Utilities.D((ulong)value->Complex),
                        Utilities.D(value->TimeSpec)));
#pragma warning restore CS0162 // Unerreichbarer Code wurde entdeckt.
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HandleT
    {
        private readonly void* handler;
        public readonly void* data1;
        public readonly void* data2;
        public Int64 index;
    }


    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TimeSpecT
    {
        public readonly long TvSec;
        public readonly long TvNsec;
    }


    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ValueT
    {
        public readonly HandleT Handle;
        public readonly TimeSpecT TimeSpec;
        public readonly Int64 Number;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public readonly IntPtr Name;
        [MarshalAs(UnmanagedType.LPUTF8Str)] public readonly IntPtr String;
        public readonly ValueT* Complex;
        public readonly TypeT Type;
    }
}