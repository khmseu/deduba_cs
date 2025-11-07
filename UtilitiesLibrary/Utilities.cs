using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UtilitiesLibrary;

/// <summary>
///     Shared utility helpers for logging, diagnostics and pretty-printing structured data.
/// </summary>
public class Utilities
{
    private static readonly JsonSerializerOptions SerializerOptions = GenSerializerOptions();

    /// <summary>
    ///     Creates a name/value pair that preserves the original expression name for diagnostics.
    ///     Useful with <see cref="Dumper" /> to produce labeled debug output.
    /// </summary>
    /// <param name="value">The value to capture.</param>
    /// <param name="name">Caller-supplied expression name (auto-filled by compiler).</param>
    /// <returns>A labeled <see cref="KeyValuePair{TKey,TValue}" /> suitable for dumping.</returns>
    public static KeyValuePair<string, object?> D(
        object? value,
        [CallerArgumentExpression(nameof(value))] string name = ""
    )
    {
        return new KeyValuePair<string, object?>(name, value);
    }

    /// <summary>
    ///     Retrieves the version string from the calling assembly's InformationalVersion attribute.
    ///     This includes the semantic version and git commit hash when using MinVer.
    /// </summary>
    /// <returns>Version string (e.g., "0.1.0-alpha.5+sha.abc1234") or "unknown" if not available.</returns>
    public static string GetVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var version =
            asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";
        return version;
    }

    private static JsonSerializerOptions GenSerializerOptions()
    {
        JsonSerializerOptions Options = new()
        {
            IgnoreReadOnlyFields = false,
            IgnoreReadOnlyProperties = false,
            IncludeFields = true,
            // ReferenceHandler = ReferenceHandler.Preserve,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        Options.Converters.Add(new JsonStringEnumConverter());
        return Options;
    }

    /// <summary>
    ///     Serializes the provided labeled values into a multi-line, human-readable string using JSON.
    ///     Intended for debug logging and structured trace output.
    /// </summary>
    /// <param name="values">One or more labeled values produced by <see cref="D" />.</param>
    /// <returns>A concatenated string where each line is "name = json".</returns>
    public static string Dumper(params KeyValuePair<string, object?>[] values)
    {
        string[] ret = [];
        foreach (var kvp in values)
            try
            {
                var jsonOutput = JsonSerializer.Serialize(kvp.Value, SerializerOptions);
                ret = [.. ret, $"{kvp.Key} = {jsonOutput}\n"];
            }
            catch (Exception ex)
            {
                Error(kvp.Key, nameof(JsonSerializer.Serialize), ex);
                ret = [.. ret, $"{kvp.Key} = {ex.Message}\n"];
            }

        return string.Join("", ret);
    }

    // ############################################################################
    // errors
    // ReSharper disable ExplicitCallerInfoArgument
    /// <summary>
    ///     Logs a generic error using a synthetic exception and caller info.
    ///     Prefer the overload that accepts an <see cref="Exception" /> when available.
    /// </summary>
    /// <param name="file">Logical file or resource name related to the error.</param>
    /// <param name="op">Operation being performed when the error occurred.</param>
    /// <param name="filePath">Auto-supplied caller file path.</param>
    /// <param name="lineNumber">Auto-supplied caller line number.</param>
    /// <param name="callerMemberName">Auto-supplied caller member name.</param>
    public static void Error(
        string file,
        string op,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    )
    {
        Error(
            file,
            op,
            new Win32Exception("error in Error"),
            filePath,
            lineNumber,
            callerMemberName
        );
    }

    /// <summary>
    ///     When true, also writes diagnostic output to the console in addition to the log file.
    /// </summary>
    public static bool Testing = true;

    /// <summary>
    ///     Log stream used by the backup process. When null, errors will be rethrown.
    /// </summary>
    public static StreamWriter? _log;

    /// <summary>
    ///     Logs an error with full exception details, including inner exceptions, stack, and attached data.
    /// </summary>
    /// <param name="file">Logical file or resource name related to the error.</param>
    /// <param name="op">Operation being performed when the error occurred.</param>
    /// <param name="ex">The exception that was raised.</param>
    /// <param name="filePath">Auto-supplied caller file path.</param>
    /// <param name="lineNumber">Auto-supplied caller line number.</param>
    /// <param name="callerMemberName">Auto-supplied caller member name.</param>
    public static void Error(
        string file,
        string op,
        Exception ex,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    )
    {
        if (ex.InnerException != null)
            Error(file, op, ex.InnerException, filePath, lineNumber, callerMemberName);
        var msg = $"*** {file}: {op}: {ex.Message}\n{ex.StackTrace}\n{Dumper(D(ex.Data))}\n";
        if (Testing)
            ConWrite(msg, filePath, lineNumber, callerMemberName);
        if (_log != null)
            _log.Write(msg);
        else
            throw new Exception(msg, ex);
    }

    /// <summary>
    ///     Writes a warning message to the console with caller context.
    /// </summary>
    /// <param name="msg">Warning text.</param>
    /// <param name="filePath">Auto-supplied caller file path.</param>
    /// <param name="lineNumber">Auto-supplied caller line number.</param>
    /// <param name="callerMemberName">Auto-supplied caller member name.</param>
    public static void Warn(
        string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    )
    {
        ConWrite($"WARN: {msg}\n", filePath, lineNumber, callerMemberName);
    }

    /// <summary>
    ///     Writes a structured message to the console including precise caller context.
    /// </summary>
    /// <param name="msg">Text to write.</param>
    /// <param name="filePath">Auto-supplied caller file path.</param>
    /// <param name="lineNumber">Auto-supplied caller line number.</param>
    /// <param name="callerMemberName">Auto-supplied caller member name.</param>
    public static void ConWrite(
        string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    )
    {
        Console.Write(
            $"\n{lineNumber} {DateTime.Now} <{Path.GetFileName(filePath)}:{lineNumber} {callerMemberName}> {msg}"
        );
    }
    // ReSharper enable ExplicitCallerInfoArgument
}
