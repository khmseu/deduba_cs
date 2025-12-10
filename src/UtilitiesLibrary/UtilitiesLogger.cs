using System.Runtime.CompilerServices;

namespace UtilitiesLibrary;

/// <summary>
///     Adapter implementing <see cref="ILogging" /> by forwarding to static <see cref="Utilities" />.
///     This preserves existing runtime behavior while enabling instance injection.
/// </summary>
public sealed class UtilitiesLogger : ILogging
{
    private static readonly Lazy<UtilitiesLogger> _instance = new(() => new UtilitiesLogger());

    /// <summary>
    ///     Default singleton instance of the adapter.
    /// </summary>
    public static ILogging Instance => _instance.Value;

    /// <summary>
    ///     Captures and returns a key-value pair for the specified value.
    /// </summary>
    /// <param name="value">The value to capture.</param>
    /// <param name="name">The name of the value (automatically captured from the expression).</param>
    /// <returns>A key-value pair containing the name and value.</returns>
    public KeyValuePair<string, object?> D(
        object? value,
        [CallerArgumentExpression(nameof(value))]
        string name = ""
    )
    {
        return Utilities.D(value, name);
    }

    /// <summary>
    ///     Formats the provided key-value pairs into a human-readable dump string.
    /// </summary>
    /// <param name="values">The key-value pairs to format.</param>
    /// <returns>A formatted string representation of the values.</returns>
    public string Dumper(params KeyValuePair<string, object?>[] values)
    {
        return Utilities.Dumper(values);
    }

    /// <summary>
    ///     Writes a message to the console with caller information.
    /// </summary>
    /// <param name="msg">The message to write.</param>
    /// <param name="filePath">The source file path (automatically captured).</param>
    /// <param name="lineNumber">The source line number (automatically captured).</param>
    /// <param name="callerMemberName">The calling member name (automatically captured).</param>
    public void ConWrite(
        string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    )
    {
        Utilities.ConWrite(msg, filePath, lineNumber, callerMemberName);
    }

    /// <summary>
    ///     Logs an error with the specified file and operation details.
    /// </summary>
    /// <param name="file">The file identifier or name associated with the error.</param>
    /// <param name="op">The operation description.</param>
    /// <param name="ex">An optional exception object.</param>
    /// <param name="filePath">The source file path (automatically captured).</param>
    /// <param name="lineNumber">The source line number (automatically captured).</param>
    /// <param name="callerMemberName">The calling member name (automatically captured).</param>
    public void Error(
        string file,
        string op,
        Exception? ex = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    )
    {
        if (ex is null)
            Utilities.Error(file, op, filePath, lineNumber, callerMemberName);
        else
            Utilities.Error(file, op, ex, filePath, lineNumber, callerMemberName);
    }

    /// <summary>
    ///     Determines whether native debugging is enabled.
    /// </summary>
    /// <returns>True if native debugging is enabled; otherwise, false.</returns>
    public bool IsNativeDebugEnabled()
    {
        return Utilities.IsNativeDebugEnabled();
    }

    /// <summary>
    ///     Reports the current status of the operation.
    /// </summary>
    /// <param name="filesDone">The number of files processed.</param>
    /// <param name="dirsDone">The number of directories processed.</param>
    /// <param name="queued">The number of items queued for processing.</param>
    /// <param name="bytes">The number of bytes processed.</param>
    /// <param name="currentPath">The current file or directory path being processed.</param>
    /// <param name="percent">The completion percentage.</param>
    public void Status(
        long filesDone,
        long dirsDone,
        long queued,
        long bytes,
        string currentPath,
        double percent
    )
    {
        Utilities.Status(filesDone, dirsDone, queued, bytes, currentPath, percent);
    }

    /// <summary>
    ///     Writes a warning message to the console with caller information.
    /// </summary>
    /// <param name="msg">The warning message to write.</param>
    /// <param name="filePath">The source file path (automatically captured).</param>
    /// <param name="lineNumber">The source line number (automatically captured).</param>
    /// <param name="callerMemberName">The calling member name (automatically captured).</param>
    public void Warn(
        string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    )
    {
        Utilities.Warn(msg, filePath, lineNumber, callerMemberName);
    }

    /// <summary>
    ///     Gets the version string.
    /// </summary>
    /// <returns>The version string.</returns>
    public string GetVersion()
    {
        return Utilities.GetVersion();
    }

    /// <summary>
    ///     Converts the specified number of bytes to a human-readable format.
    /// </summary>
    /// <param name="bytes">The number of bytes to convert.</param>
    /// <returns>A human-readable string representation of the bytes.</returns>
    public string HumanizeBytes(long bytes)
    {
        return Utilities.HumanizeBytes(bytes);
    }
}