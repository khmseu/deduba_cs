using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DeDuBa;

/// <summary>
///     Structured data serialization/deserialization
///     Unpacked: nested arrays, strings, and numbers
///     Packed: with strings, unsigned numbers, and lists
/// </summary>
public static class StructuredData
{
    private const bool Testing = false; // Set to true for debug output

    /// <summary>
    ///     Packs structured data into a binary format
    /// </summary>
    /// <param name="value">The value to pack (can be string, number, or array)</param>
    /// <param name="name">Name for debugging purposes</param>
    /// <returns>Packed byte array</returns>
    public static byte[] Pack(object value, string name = "")
    {
        if (name == null)
            throw new ArgumentNullException(nameof(name));

        if (Testing && !string.IsNullOrEmpty(name))
            Console.WriteLine(
                $"\n{DateTime.Now}: {name}: Type={value?.GetType().Name}, Value={value}"
            );

        if (value == null)
            // undefined/null value
            return [(byte)'u'];

        if (value is string str)
            // string value
            return ConcatenateBytes([(byte)'s'], Encoding.ASCII.GetBytes(str));

        if (IsNumericReference(value, out var numValue))
        {
            // numeric reference (boxed number)
            if (numValue >= 0)
                return ConcatenateBytes([(byte)'n'], PackCompressedNumber((ulong)numValue));
            return ConcatenateBytes([(byte)'N'], PackCompressedNumber((ulong)-numValue));
        }

        if (value is IEnumerable enumerable)
        {
            // array/list value
            var list = new List<byte[]>();
            foreach (var item in enumerable)
                list.Add(Pack(item));

            // Pack as list: 'l' followed by compressed array of compressed byte arrays
            var listData = PackCompressedByteArrays(list);
            return ConcatenateBytes([(byte)'l'], listData);
        }

        throw new ArgumentException($"Unexpected type: {value.GetType().Name}");
    }

    /// <summary>
    ///     Unpacks binary data back into structured format
    /// </summary>
    /// <param name="data">Packed byte array</param>
    /// <returns>Unpacked object (string, numeric reference, array, or null)</returns>
    public static object Unpack(byte[] data)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Data cannot be null or empty");

        var typePrefix = (char)data[0];
        var content = data.Skip(1).ToArray();

        switch (typePrefix)
        {
            case 'u':
                // undefined/null
                return null;

            case 's':
                // string
                return Encoding.ASCII.GetString(content);

            case 'n':
                // positive number (as reference)
                return UnpackCompressedNumber(content);

            case 'N':
                // negative number (as reference)
                return -(long)UnpackCompressedNumber(content);

            case 'l':
                // list/array
                var items = UnpackCompressedByteArrays(content);
                return items.Select(Unpack).ToList();

            default:
                throw new ArgumentException($"Unexpected type prefix: {typePrefix}");
        }
    }

    /// <summary>
    ///     Checks if a value is a numeric reference (boxed number)
    /// </summary>
    private static bool IsNumericReference(object value, out long numValue)
    {
        numValue = 0;

        if (value == null)
            return false;

        var type = value.GetType();

        // Check if it's a boxed numeric type
        if (
            type == typeof(int)
            || type == typeof(long)
            || type == typeof(short)
            || type == typeof(byte)
            || type == typeof(uint)
            || type == typeof(ulong)
            || type == typeof(ushort)
            || type == typeof(sbyte)
        )
        {
            numValue = Convert.ToInt64(value);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Packs an unsigned number using BER compressed format (similar to Perl's 'w' pack)
    /// </summary>
    private static byte[] PackCompressedNumber(ulong value)
    {
        var bytes = new List<byte>();

        // Handle zero specially
        if (value == 0)
        {
            bytes.Add(0);
            return [.. bytes];
        }

        // BER (Basic Encoding Rules) compressed integer encoding
        // High bit set means more bytes follow
        var temp = new List<byte>();
        while (value > 0)
        {
            temp.Add((byte)(value & 0x7F));
            value >>= 7;
        }

        // Reverse and set high bit on all but last byte
        temp.Reverse();
        for (var i = 0; i < temp.Count - 1; i++)
            bytes.Add((byte)(temp[i] | 0x80));
        bytes.Add(temp[^1]);

        return [.. bytes];
    }

    /// <summary>
    ///     Unpacks a BER compressed number
    /// </summary>
    private static ulong UnpackCompressedNumber(byte[] data)
    {
        ulong result = 0;
        var i = 0;

        while (i < data.Length)
        {
            var b = data[i++];
            result = (result << 7) | (byte)(b & 0x7F);

            if ((b & 0x80) == 0)
                break;
        }

        return result;
    }

    /// <summary>
    ///     Packs an array of byte arrays with compressed lengths
    ///     Similar to Perl's 'w/(w/a)' pack format
    /// </summary>
    private static byte[] PackCompressedByteArrays(List<byte[]> arrays)
    {
        using var ms = new MemoryStream();
        // Write count of arrays
        var countBytes = PackCompressedNumber((ulong)arrays.Count);
        ms.Write(countBytes, 0, countBytes.Length);

        // Write each array with its length
        foreach (var array in arrays)
        {
            var lengthBytes = PackCompressedNumber((ulong)array.Length);
            ms.Write(lengthBytes, 0, lengthBytes.Length);
            ms.Write(array, 0, array.Length);
        }

        return ms.ToArray();
    }

    /// <summary>
    ///     Unpacks an array of byte arrays with compressed lengths
    ///     Similar to Perl's 'w/(w/a)' unpack format
    /// </summary>
    private static List<byte[]> UnpackCompressedByteArrays(byte[] data)
    {
        var result = new List<byte[]>();
        var offset = 0;

        // Read count of arrays
        var count = ReadCompressedNumber(data, ref offset);

        // Read each array
        for (ulong i = 0; i < count; i++)
        {
            var length = ReadCompressedNumber(data, ref offset);
            var array = new byte[length];
            Array.Copy(data, offset, array, 0, (int)length);
            offset += (int)length;
            result.Add(array);
        }

        return result;
    }

    /// <summary>
    ///     Reads a compressed number from a byte array at the given offset
    /// </summary>
    private static ulong ReadCompressedNumber(byte[] data, ref int offset)
    {
        ulong result = 0;

        while (offset < data.Length)
        {
            var b = data[offset++];
            result = (result << 7) | (byte)(b & 0x7F);

            if ((b & 0x80) == 0)
                break;
        }

        return result;
    }

    /// <summary>
    ///     Helper to concatenate byte arrays
    /// </summary>
    private static byte[] ConcatenateBytes(params byte[][] arrays)
    {
        var totalLength = arrays.Sum(a => a.Length);
        var result = new byte[totalLength];
        var offset = 0;

        foreach (var array in arrays)
        {
            Array.Copy(array, 0, result, offset, array.Length);
            offset += array.Length;
        }

        return result;
    }

    /// <summary>
    ///     String-based pack wrapper for compatibility with Perl-style byte-as-char string handling.
    ///     Matches the Sdpack signature used in Deduba.cs.
    /// </summary>
    public static string PackString(object? value, string name = "")
    {
        var bytes = Pack(value!, name);
        return new string(bytes.Select(b => (char)b).ToArray());
    }

    /// <summary>
    ///     String-based unpack wrapper for compatibility with Perl-style byte-as-char string handling.
    ///     Matches the Sdunpack signature used in Deduba.cs.
    /// </summary>
    public static object? UnpackString(string value)
    {
        var bytes = value.Select(c => (byte)c).ToArray();
        return Unpack(bytes);
    }
}
