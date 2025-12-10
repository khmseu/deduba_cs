using System.Runtime.CompilerServices;

namespace UtilitiesLibrary;

/// <summary>
/// Instance-based logging interface for forwarding to static Utilities.
/// Designed to be injected where callers should not depend on static `Utilities`.
/// </summary>
public interface ILogging
{
    /// <summary>
    /// Dumps a value with its name as a key-value pair.
    /// </summary>
    /// <param name="value">The value to dump.</param>
    /// <param name="name">The name of the value, automatically captured from the argument expression.</param>
    /// <returns>A key-value pair containing the name and value.</returns>
    KeyValuePair<string, object?> D(
        object? value,
        [CallerArgumentExpression(nameof(value))] string name = ""
    );

    /// <summary>
    /// Formats multiple key-value pairs into a human-readable string.
    /// </summary>
    /// <param name="values">The key-value pairs to format.</param>
    /// <returns>A formatted string representation of the key-value pairs.</returns>
    string Dumper(params KeyValuePair<string, object?>[] values);

    /// <summary>
    /// Writes a message to the console with caller information.
    /// </summary>
    /// <param name="msg">The message to write.</param>
    /// <param name="filePath">The file path of the caller, automatically captured.</param>
    /// <param name="lineNumber">The line number of the caller, automatically captured.</param>
    /// <param name="callerMemberName">The member name of the caller, automatically captured.</param>
    void ConWrite(
        string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    );

    /// <summary>
    /// Logs an error message with caller information and optional exception details.
    /// </summary>
    /// <param name="file">The file being processed when the error occurred.</param>
    /// <param name="op">The operation that failed.</param>
    /// <param name="ex">Optional exception details to log.</param>
    /// <param name="filePath">The file path of the caller, automatically captured.</param>
    /// <param name="lineNumber">The line number of the caller, automatically captured.</param>
    /// <param name="callerMemberName">The member name of the caller, automatically captured.</param>
    void Error(
        string file,
        string op,
        Exception? ex = null,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    );

    /// <summary>
    /// Determines whether native debugging is enabled.
    /// </summary>
    /// <returns>True if native debugging is enabled; otherwise, false.</returns>
    bool IsNativeDebugEnabled();

    /// <summary>
    /// Reports processing status information.
    /// </summary>
    /// <param name="filesDone">The number of files processed.</param>
    /// <param name="dirsDone">The number of directories processed.</param>
    /// <param name="queued">The number of items queued for processing.</param>
    /// <param name="bytes">The total number of bytes processed.</param>
    /// <param name="currentPath">The current path being processed.</param>
    /// <param name="percent">The completion percentage.</param>
    void Status(
        long filesDone,
        long dirsDone,
        long queued,
        long bytes,
        string currentPath,
        double percent
    );

    /// <summary>
    /// Logs a warning message with caller information.
    /// </summary>
    /// <param name="msg">The warning message to log.</param>
    /// <param name="filePath">The file path of the caller, automatically captured.</param>
    /// <param name="lineNumber">The line number of the caller, automatically captured.</param>
    /// <param name="callerMemberName">The member name of the caller, automatically captured.</param>
    void Warn(
        string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string callerMemberName = ""
    );

    /// <summary>
    /// Gets the version information.
    /// </summary>
    /// <returns>A string representing the version.</returns>
    string GetVersion();

    /// <summary>
    /// Converts a byte count into a human-readable string format.
    /// </summary>
    /// <param name="bytes">The number of bytes to convert.</param>
    /// <returns>A human-readable string representation of the byte count.</returns>
    string HumanizeBytes(long bytes);

    /// <summary>
    /// Static singleton accessor for a default logger implementation.
    /// Implementations should provide a matching static property returning an `ILogging` singleton.
    /// </summary>
    static abstract ILogging Instance { get; }
}
