using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UtilitiesLibrary;

public class Utilities
{

    public static KeyValuePair<string, object?> D(object? value,
        [CallerArgumentExpression(nameof(value))]
        string name = "")
    {
        return new KeyValuePair<string, object?>(name, value);
    }
    private static JsonSerializerOptions GenSerializerOptions()
    {
        JsonSerializerOptions Options =
        new JsonSerializerOptions()
        {
            IgnoreReadOnlyFields = false,
            IgnoreReadOnlyProperties = false,
            IncludeFields = true,
            // ReferenceHandler = ReferenceHandler.Preserve,
            WriteIndented = true
        };
        Options.Converters.Add(new JsonStringEnumConverter());
        return Options;
    }
    private static readonly JsonSerializerOptions SerializerOptions = GenSerializerOptions();
    public static string Dumper(params KeyValuePair<string, object?>[] values)
    {
        string[] ret = [];
        foreach (var kvp in values)
            try
            {
                var jsonOutput = JsonSerializer.Serialize(kvp.Value, SerializerOptions);
                ret = ret.Append($"{kvp.Key} = {jsonOutput}\n")
                    .ToArray();
            }
            catch (Exception ex)
            {
                Error(kvp.Key, nameof(JsonSerializer.Serialize), ex);
                ret = ret.Append($"{kvp.Key} = {ex.Message}\n")
                    .ToArray();
            }

        return string.Join("", ret);
    }


    // ############################################################################
    // errors
    // ReSharper disable ExplicitCallerInfoArgument
    public static void Error(string file, string op, [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        Error(file, op, new Win32Exception("error in Error"), filePath, lineNumber, callerMemberName);
    }
    public static bool Testing = true;
    public static StreamWriter? _log;


    public static void Error(string file, string op, Exception ex, [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        if (ex.InnerException != null) Error(file, op, ex.InnerException, filePath, lineNumber, callerMemberName);
        var msg = $"*** {file}: {op}: {ex.Message}\n{ex.StackTrace}\n{Dumper(D(ex.Data))}\n";
        if (Testing) ConWrite(msg, filePath, lineNumber, callerMemberName);
        if (_log != null) _log.Write(msg);
        else
            throw new Exception(msg, ex);
    }

    public static void Warn(string msg, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        ConWrite($"WARN: {msg}\n", filePath, lineNumber, callerMemberName);
    }

    public static void ConWrite(string msg, [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = "")
    {
        Console.Write(
            $"\n{lineNumber} {DateTime.Now} <{Path.GetFileName(filePath)}:{lineNumber} {callerMemberName}> {msg}");
    }
    // ReSharper enable ExplicitCallerInfoArgument

}