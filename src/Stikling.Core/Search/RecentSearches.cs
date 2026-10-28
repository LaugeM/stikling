using System.Text.Json;

namespace Stikling.Core.Search;

/// <summary>The last few searches on this device, newest first. Kept on the device only, never synced.</summary>
public static class RecentSearches
{
    public const int Max = 5;

    /// <summary>Puts a search first. The same search typed in other capitals or with other accents moves up instead of repeating.</summary>
    public static IReadOnlyList<string> Add(IEnumerable<string> recent, string? query)
    {
        var text = string.Join(' ', (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (SearchText.Fold(text).Length == 0)
            return [.. recent.Take(Max)];

        var folded = SearchText.Fold(text);
        return [.. new[] { text }.Concat(recent.Where(r => SearchText.Fold(r) != folded)).Take(Max)];
    }

    /// <summary>The stored list, or an empty one when it isn't readable.</summary>
    public static IReadOnlyList<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return [.. (JsonSerializer.Deserialize<List<string?>>(json) ?? [])
                .OfType<string>().Where(s => s.Trim().Length > 0).Take(Max)];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string ToJson(IEnumerable<string> recent) => JsonSerializer.Serialize(recent.Take(Max));
}
