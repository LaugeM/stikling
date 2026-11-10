using System.Globalization;

namespace Stikling.Core.Text;

/// <summary>
/// The text helpers every page and service shares: filling in resource texts in the current culture,
/// and trimming what someone typed.
/// </summary>
public static class Format
{
    /// <summary>Fills the placeholders of a text from the resource files, in the current culture.</summary>
    public static string F(string format, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, format, args);

    // Picks the one or other form by count, then fills in the rest
    public static string F(int count, string one, string other, params object?[] args) => F(count == 1 ? one : other, args);

    /// <summary>The text trimmed, or null when it is null, empty or only whitespace.</summary>
    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
