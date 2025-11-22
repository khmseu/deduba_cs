namespace DeDuBa;

/// <summary>
///     Extension methods for string manipulation.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    ///     Repeats the specified string <paramref name="n" /> times.
    /// </summary>
    /// <param name="text">The string to repeat.</param>
    /// <param name="n">The number of times to repeat the string.</param>
    /// <returns>A new string containing <paramref name="text" /> repeated <paramref name="n" /> times.</returns>
    public static string Repeat(this string text, int n)
    {
        var textAsSpan = text.AsSpan();
        var span = new Span<char>(new char[textAsSpan.Length * n]);
        for (var i = 0; i < n; i++)
            textAsSpan.CopyTo(span.Slice(i * textAsSpan.Length, textAsSpan.Length));

        return span.ToString();
    }
}
