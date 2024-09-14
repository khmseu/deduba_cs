namespace DeDuBa.Extensions;

internal static class StringExtensions
{
    public static string Repeat(this string text, uint n)
    {
        var textAsSpan = text.AsSpan();
        var span = new Span<char>(new char[textAsSpan.Length * (int)n]);
        for (var i = 0; i < n; i++) textAsSpan.CopyTo(span.Slice(i * textAsSpan.Length, textAsSpan.Length));

        return span.ToString();
    }

    public static string Repeat(this string text, int n)
    {
        var textAsSpan = text.AsSpan();
        var span = new Span<char>(new char[textAsSpan.Length * n]);
        for (var i = 0; i < n; i++) textAsSpan.CopyTo(span.Slice(i * textAsSpan.Length, textAsSpan.Length));

        return span.ToString();
    }
}