using Stikling.Core.Models;
using Stikling.Core.Search;

namespace Stikling.Core.Pests;

/// <summary>The search box above the plants to pick on a pest case.</summary>
public static class PlantPickSearch
{
    /// <summary>
    /// Whether every word of the query is found somewhere in the plant's names, its place or its
    /// tags, ignoring case and accents. An empty query matches every plant.
    /// </summary>
    public static bool Matches(Plant plant, string? placeName, string? query)
    {
        var words = SearchText.Words(query);
        if (words.Count == 0)
            return true;

        string?[] parts = [plant.DisplayName, plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, placeName, .. plant.Tags];
        var haystack = SearchText.Fold(string.Join(' ', parts.Where(t => !string.IsNullOrWhiteSpace(t))));
        return words.All(w => haystack.Contains(w, StringComparison.Ordinal));
    }
}
