using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using UtilitiesLibrary;

namespace OsCalls;

/// <summary>
///     Bridge for transferring native values from the C++ shim into managed <see cref="JsonNode" />s.
///     The native side exposes a cursor-like API where <see cref="GetNextValue" /> advances over a
///     sequence of values (arrays or key/value objects). This class interprets those sequences and
///     materializes them as JSON using <see cref="ToNode" />.
/// </summary>
public static unsafe partial class ValXfer
{
    /// <summary>
    ///     Value discriminator reported by the native layer for the current cursor position.
    /// </summary>
    public enum TypeT
    {
        // ReSharper disable UnusedMember.Global
        /// <summary>Operation completed successfully.</summary>
        IsOk = 0,

        /// <summary>Error occurred (errno set).</summary>
        IsError,

        /// <summary>Current value is a 64-bit integer.</summary>
        IsNumber,

        /// <summary>Current value is a UTF-8 string.</summary>
        IsString,

        /// <summary>Current value is a nested complex structure.</summary>
        IsComplex,

        /// <summary>Current value is a POSIX timespec.</summary>
        IsTimeSpec,

        /// <summary>Current value is a boolean.</summary>
        IsBoolean,
    }

    [LibraryImport("libOsCallsShim.so")]
    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNextValue(ValueT* value);

    /// <summary>
    ///     Converts a native <see cref="ValueT" /> stream into a <see cref="JsonNode" />.
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
                    case TypeT.IsBoolean:
                        array.Add(value->Boolean);
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
                case TypeT.IsBoolean:
                    obj[name] = value->Boolean;
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
                        Utilities.D(value->TimeSpec),
                        Utilities.D(value->Boolean)
                    )
                );
#pragma warning restore CS0162 // Unerreichbarer Code wurde entdeckt.
        }
    }

    /// <summary>
    ///     Managed snapshot of a single native <see cref="ValueT" /> structure.
    ///     This mirrors the layout of <see cref="ValueT" /> into safe managed fields and
    ///     decodes pointer-based strings into <see cref="string" />. For <see cref="TypeT.IsComplex" />,
    ///     the nested <see cref="ValueT" /> can optionally be captured recursively (depth-limited).
    /// </summary>
    public sealed class ValueObject
    {
        /// <summary>Iteration handle snapshot.</summary>
        public HandleObject Handle { get; init; } = new();

        /// <summary>Timespec components.</summary>
        public long TvSec { get; init; }

        /// <summary>Nanoseconds component of the timespec when <see cref="Type" /> is <see cref="TypeT.IsTimeSpec" />.</summary>
        public long TvNsec { get; init; }

        /// <summary>Numeric value when <see cref="Type" /> is <see cref="TypeT.IsNumber" />.</summary>
        public long Number { get; init; }

        /// <summary>Field/key name ("[]" for array items).</summary>
        public string? Name { get; init; }

        /// <summary>String value when <see cref="Type" /> is <see cref="TypeT.IsString" />.</summary>
        public string? String { get; init; }

        /// <summary>Boolean value when <see cref="Type" /> is <see cref="TypeT.IsBoolean" />.</summary>
        public bool Boolean { get; init; }

        /// <summary>Nested structure (optional, see <see cref="ToObject(ValueT*, int)" />).</summary>
        public ValueObject? Complex { get; set; }

        /// <summary>Discriminator indicating which field is valid.</summary>
        public TypeT Type { get; init; }

        /// <summary>
        ///     Returns a string representation of this value based on its <see cref="Type" />.
        /// </summary>
        public override string ToString()
        {
            return Type switch
            {
                TypeT.IsNumber => $"[{Handle}] {Name}: {Number}",
                TypeT.IsString => $"[{Handle}] {Name}: \"{String}\"",
                TypeT.IsBoolean => $"[{Handle}] {Name}: {Boolean}",
                TypeT.IsTimeSpec => $"[{Handle}] {Name}: {TvSec}.{TvNsec:D9}s",
                TypeT.IsComplex => $"[{Handle}] {Name}: [{Complex}]",
                TypeT.IsError => $"[{Handle}] {Name}: [Error {Number}]",
                TypeT.IsOk => $"[{Handle}] {Name}: [OK]",
                _ => $"[{Handle}] {Name}: [Unknown type {Type}]",
            };
        }

        /// <summary>
        ///     Managed snapshot of the native iteration handle embedded in <see cref="ValueT" />.
        /// </summary>
        public sealed class HandleObject
        {
            /// <summary>Current iteration index as reported by the native handle.</summary>
            public long Index { get; init; }

            /// <summary>First user data pointer associated with the native handle (as unsigned integer).</summary>
            public ulong Data1 { get; init; }

            /// <summary>Second user data pointer associated with the native handle (as unsigned integer).</summary>
            public ulong Data2 { get; init; }

            /// <summary>
            ///     Returns a compact string representation for diagnostics.
            /// </summary>
            public override string ToString()
            {
                return $"[{Index} d1=0x{Data1:x} d2=0x{Data2:x}]";
            }
        }
    }

    /// <summary>
    ///     Create a managed snapshot for a single native <see cref="ValueT" /> pointer.
    /// </summary>
    /// <param name="value">Pointer to the native value to convert.</param>
    /// <param name="maxDepth">Maximum recursion depth for <see cref="TypeT.IsComplex" /> (default: 1).</param>
    /// <returns>A managed <see cref="ValueObject" /> with fields corresponding to <see cref="ValueT" />.</returns>
    public static ValueObject ToObject(ValueT* value, int maxDepth = 1)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var vo = new ValueObject
        {
            Handle = new ValueObject.HandleObject
            {
                Index = value->Handle.index,
                Data1 = (ulong)value->Handle.data1,
                Data2 = (ulong)value->Handle.data2,
            },
            TvSec = value->TimeSpec.TvSec,
            TvNsec = value->TimeSpec.TvNsec,
            Number = value->Number,
            Name = value->Name != IntPtr.Zero ? Marshal.PtrToStringUTF8(value->Name) : null,
            String = value->String != IntPtr.Zero ? Marshal.PtrToStringUTF8(value->String) : null,
            Boolean = value->Boolean,
            Type = value->Type,
            Complex = null,
        };

        if (value->Type == TypeT.IsComplex && value->Complex != null && maxDepth > 0)
            vo.Complex = ToObject(value->Complex, maxDepth - 1);

        return vo;
    }

    /// <summary>
    ///     Native iteration handle used by the shim to keep state across calls to <see cref="GetNextValue" />.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct HandleT
    {
        private readonly void* handler;

        /// <summary>First user data pointer passed to the handler.</summary>
        public readonly void* data1;

        /// <summary>Second user data pointer passed to the handler.</summary>
        public readonly void* data2;

        /// <summary>Current iteration index (incremented by GetNextValue).</summary>
        public Int64 index;
    }

    /// <summary>
    ///     Native timespec representation (seconds + nanoseconds) passed through from POSIX APIs.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct TimeSpecT
    {
        /// <summary>Seconds since epoch.</summary>
        public readonly long TvSec;

        /// <summary>Nanoseconds component.</summary>
        public readonly long TvNsec;
    }

    /// <summary>
    ///     Native value record describing the current node in the traversal.
    ///     Depending on <see cref="Type" />, either <see cref="Number" />, <see cref="String" />, <see cref="Complex" />
    ///     or <see cref="TimeSpec" /> is populated.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ValueT
    {
        /// <summary>Iteration handle and state.</summary>
        public readonly HandleT Handle;

        /// <summary>Field/key name or "[]" for array items.</summary>
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public readonly IntPtr Name;

        /// <summary>Discriminator indicating which field is valid.</summary>
        public readonly TypeT Type;

        /// <summary>Timespec value when Type == IsTimeSpec.</summary>
        public readonly TimeSpecT TimeSpec;

        /// <summary>Integer value when Type == IsNumber.</summary>
        public readonly Int64 Number;

        /// <summary>String value pointer when Type == IsString.</summary>
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        public readonly IntPtr String;

        /// <summary>Nested structure pointer when Type == IsComplex.</summary>
        public readonly ValueT* Complex;

        /// <summary>Boolean value when Type == IsBoolean.</summary>
        [MarshalAs(UnmanagedType.I1)]
        public readonly bool Boolean;
    }
}
