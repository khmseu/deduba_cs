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
        [CallerArgumentExpression(nameof(value))]
        string name = ""
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
        JsonSerializerOptions options = new()
        {
            IgnoreReadOnlyFields = false,
            IgnoreReadOnlyProperties = false,
            IncludeFields = true,
            // ReferenceHandler = ReferenceHandler.Preserve,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
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
    ///     When true, enables testing mode behaviors (e.g., using test archive path instead of production path).
    ///     Does not control diagnostic output - see <see cref="VerboseOutput" /> instead.
    /// </summary>
    public static bool Testing = true;

    /// <summary>
    ///     When true, writes verbose diagnostic output to the console in addition to the log file.
    ///     Controlled by --verbose/-v command-line option.
    /// </summary>
    public static bool VerboseOutput = false;

    /// <summary>
    ///     Checks whether native shim debug logging is enabled.
    ///     This consults <see cref="VerboseOutput" /> and the environment variable
    ///     DEDUBA_DEBUG_NATIVE. When the env var is set to '1', 'true', or any
    ///     non-empty value, we enable additional diagnostic output for native
    ///     runtime events (dll load/unload and resolver attempts) useful in CI.
    /// </summary>
    public static bool IsNativeDebugEnabled()
    {
        if (VerboseOutput)
            return true;
        try
        {
            var v = Environment.GetEnvironmentVariable("DEDUBA_DEBUG_NATIVE");
            if (string.IsNullOrEmpty(v))
                return false;
            v = v.Trim();
            if (
                v.Equals("1", StringComparison.OrdinalIgnoreCase)
                || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            )
                return true;
            // Any non-empty value other than explicitly disabled will enable debugging
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Log stream used by the backup process. When null, errors will be rethrown.
    /// </summary>
    public static StreamWriter? Log;

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
        // Always write errors to console in red, regardless of Testing
        try
        {
            const string red = "\u001b[31m";
            const string reset = "\u001b[0m";
            Console.Write($"\n{red}{msg}{reset}");
        }
        catch
        {
            // Fallback if ANSI not supported
            Console.Write($"\n{msg}");
        }

        if (Log != null)
            Log.Write(msg);
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

    // ############################################################################
    // Status line helpers
    /// <summary>
    ///     Formats a byte count using IEC units (KiB, MiB, GiB, TiB).
    /// </summary>
    public static string HumanizeBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB", "TiB", "PiB"]; // enough for backup sizes
        double value = bytes;
        var unit = 0;
        while (value >= 1024.0 && unit < units.Length - 1)
        {
            value /= 1024.0;
            unit++;
        }

        return unit == 0 ? $"{bytes} {units[unit]}" : $"{value:0.0} {units[unit]}";
    }

    /// <summary>
    ///     Writes a single updating status line with colorized segments.
    ///     Completed numbers are green, queued numbers are yellow.
    /// </summary>
    public static void Status(
        long filesDone,
        long dirsDone,
        long queued,
        long bytes,
        string currentPath,
        double percent
    )
    {
        // ANSI colors
        const string green = "\u001b[32m";
        const string yellow = "\u001b[33m";
        const string reset = "\u001b[0m";
        const string clearToEol = "\u001b[K";

        var bytesText = HumanizeBytes(bytes);
        var pctText =
            double.IsNaN(percent) || double.IsInfinity(percent) ? "-" : percent.ToString("0.0");
        // Keep the path reasonably short if extremely long
        var path = currentPath ?? string.Empty;
        if (path.Length > 140)
            path = "…" + path[^139..];

        var line =
            $"{green}{filesDone} files {dirsDone} dirs{reset} | {yellow}{queued} queued{reset} | {green}{bytesText}{reset} | {yellow}{pctText}%{reset} {path}";
        Console.Write($"\r{line}{clearToEol}");
    }
}