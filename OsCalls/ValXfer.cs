using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using UtilitiesLibrary;

namespace OsCalls;

/// <summary>
/// Bridge for transferring native values from the C++ shim into managed <see cref="JsonNode"/>s.
/// The native side exposes a cursor-like API where <see cref="GetNextValue"/> advances over a
/// sequence of values (arrays or key/value objects). This class interprets those sequences and
/// materializes them as JSON using <see cref="ToNode"/>.
/// </summary>
public static unsafe class ValXfer
{
    /// <summary>
    /// Value discriminator reported by the native layer for the current cursor position.
    /// </summary>
    public enum TypeT
    {
        // ReSharper disable UnusedMember.Global
        IsOk = 0,
        IsError,
        IsNumber,
        IsString,
        IsComplex,
        IsTimeSpec,
    }

    [DllImport("libOsCallsShim.so", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool GetNextValue(ValueT* value);

    /// <summary>
    /// Converts a native <see cref="ValueT"/> stream into a <see cref="JsonNode"/>.
    /// </summary>
    /// <param name="value">Pointer to a value cursor initialized by native code.</param>
    /// <param name="file">Logical file/resource for error reporting.</param>
    /// <param name="op">Operation name (native API) for error reporting.</param>
    /// <returns>A populated JsonArray or JsonObject depending on the native sequence.</returns>
    /// <exception cref="Win32Exception">If the native layer signaled an error.</exception>
    public static JsonNode ToNode(ValueT* value, string file, string op)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        ShowValue(value, "", op);
        var maybeError = (int)value->Number;
        var wasOk = value->Type;
        var more = GetNextValue(value);
        if (wasOk == TypeT.IsError)
        {
            var win32Exception = new Win32Exception(maybeError); //$"{nameof(ToNode)} found {op} caused error {maybeError}"
            Utilities.Error(file, op, win32Exception);
            throw win32Exception;
        }

        var name =
            Marshal.PtrToStringUTF8(value->Name)
            ?? throw new ArgumentNullException(nameof(value), $"{op} Feld {nameof(ValueT.Name)}");
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
                        array.Add(
                            value->TimeSpec.TvSec
                                + value->TimeSpec.TvNsec / (double)(1000 * 1000 * 1000)
                        );
                        break;
                    default:
                        throw new ArgumentException(
                            $"{op} Invalid ValueT type {value->Type:G}.{value->Handle.index}",
                            nameof(value)
                        );
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
                    obj[name] =
                        value->TimeSpec.TvSec
                        + value->TimeSpec.TvNsec / (double)(1000 * 1000 * 1000);
                    break;
                default:
                    throw new ArgumentException(
                        $"{op} Invalid ValueT type {value->Type:G}.{value->Handle.index}",
                        nameof(value)
                    );
            }

            more = GetNextValue(value);
            name =
                Marshal.PtrToStringUTF8(value->Name)
                ?? throw new ArgumentNullException(
                    nameof(value),
                    $"{op} Feld {nameof(ValueT.Name)}"
                );
        }

        return obj;

        static void ShowValue(ValueT* value, string name, string op)
        {
            if (false)
#pragma warning disable CS0162 // Unerreichbarer Code wurde entdeckt.
                Utilities.ConWrite(
                    Utilities.Dumper(
                        Utilities.D(op),
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
                        Utilities.D(value->TimeSpec)
                    )
                );
#pragma warning restore CS0162 // Unerreichbarer Code wurde entdeckt.
        }
    }

    /// <summary>
    /// Native iteration handle used by the shim to keep state across calls to <see cref="GetNextValue"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HandleT
    {
        private readonly void* handler;
        public readonly void* data1;
        public readonly void* data2;
        public Int64 index;
    }

    /// <summary>
    /// Native timespec representation (seconds + nanoseconds) passed through from POSIX APIs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TimeSpecT
    {
        public readonly long TvSec;
        public readonly long TvNsec;
    }

    /// <summary>
    /// Native value record describing the current node in the traversal.
    /// Depending on <see cref="Type"/>, either <see cref="Number"/>, <see cref="String"/>, <see cref="Complex"/>
    /// or <see cref="TimeSpec"/> is populated.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ValueT
    {
        public readonly HandleT Handle;
        public readonly TimeSpecT TimeSpec;
        public readonly Int64 Number;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public readonly IntPtr Name;

        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public readonly IntPtr String;
        public readonly ValueT* Complex;
        public readonly TypeT Type;
    }
}
