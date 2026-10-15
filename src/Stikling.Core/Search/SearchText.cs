using System.Globalization;
using System.Text;

namespace Stikling.Core.Search;

/// <summary>Makes text comparable for search: no accents, no capitals.</summary>
public static class SearchText
{
    /// <summary>"Café" becomes "cafe" and "ALOCASIA" becomes "alocasia". Null becomes an empty string.</summary>
    public static string Fold(string? text) => FoldedText.From(text).Text;

    /// <summary>The words of a query, folded. Empty for an empty or blank query.</summary>
    public static IReadOnlyList<string> Words(string? query) =>
        [.. (query ?? "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Fold)
            .Where(w => w.Length > 0)];
}

/// <summary>
/// Text folded for matching, with where each folded character came from in the original, so a match
/// can be shown in the text as it was written.
/// </summary>
internal sealed class FoldedText
{
    public string Text { get; }

    /// <summary>For each folded character, where its original character starts.</summary>
    public int[] Starts { get; }

    /// <summary>For each folded character, where its original character ends (exclusive).</summary>
    public int[] Ends { get; }

    private FoldedText(string text, int[] starts, int[] ends)
    {
        Text = text;
        Starts = starts;
        Ends = ends;
    }

    public static FoldedText From(string? original)
    {
        if (string.IsNullOrEmpty(original))
            return new("", [], []);

        var text = new StringBuilder(original.Length);
        var starts = new List<int>(original.Length);
        var ends = new List<int>(original.Length);
        var at = 0;
        foreach (var rune in original.EnumerateRunes())
        {
            var length = rune.Utf16SequenceLength;
            // An accented letter splits into the letter and a mark, and the mark is dropped
            foreach (var c in rune.ToString().Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;
                text.Append(char.ToLowerInvariant(c));
                starts.Add(at);
                ends.Add(at + length);
            }
            at += length;
        }

        return new(text.ToString(), [.. starts], [.. ends]);
    }
}
