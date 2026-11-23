using System.Text.RegularExpressions;
using Stikling.Core.Models;

namespace Stikling.Core.Photos;

/// <summary>Rules for the text a person writes on a photo.</summary>
public static partial class PhotoText
{
    /// <summary>
    /// The description as it is kept: trimmed, with any run of whitespace (newlines too) made one space, and cut to
    /// <see cref="Photo.DescriptionMaxLength"/>. Null when nothing is left.
    /// </summary>
    public static string? TidyDescription(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var tidy = Whitespace().Replace(text, " ").Trim();
        if (tidy.Length > Photo.DescriptionMaxLength)
            tidy = tidy[..Photo.DescriptionMaxLength].TrimEnd();
        return tidy.Length == 0 ? null : tidy;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
