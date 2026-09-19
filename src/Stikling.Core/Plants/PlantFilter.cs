using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>Which plants to show in the list.</summary>
public enum StatusFilter
{
    Active,
    Gone,
    All
}

/// <summary>Search, filter and sort rules for the plant list.</summary>
public sealed record PlantFilter(string? Search = null, StatusFilter Status = StatusFilter.Active, string? Location = null)
{
    public IEnumerable<Plant> Apply(IEnumerable<Plant> plants) =>
        plants
            .Where(p => !p.IsDeleted)
            .Where(MatchesStatus)
            .Where(MatchesLocation)
            .Where(MatchesSearch)
            .OrderBy(p => p.DisplayName, StringComparer.CurrentCultureIgnoreCase);

    private bool MatchesStatus(Plant plant) => Status switch
    {
        StatusFilter.Active => plant.Status == PlantStatus.Active,
        StatusFilter.Gone => plant.Status != PlantStatus.Active,
        _ => true
    };

    private bool MatchesLocation(Plant plant) =>
        string.IsNullOrWhiteSpace(Location)
        || string.Equals(plant.Location?.Trim(), Location.Trim(), StringComparison.OrdinalIgnoreCase);

    // Every word typed must appear somewhere in the plant's names, so "thai monstera" finds
    // "Monstera deliciosa 'Thai Constellation'"
    private bool MatchesSearch(Plant plant)
    {
        if (string.IsNullOrWhiteSpace(Search))
            return true;

        var haystack = string.Join(' ', plant.Nickname, plant.Genus, plant.Species, plant.Cultivar);
        return Search
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => haystack.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }

    /// <summary>Distinct, sorted locations used by the given plants, for the location filter.</summary>
    public static IReadOnlyList<string> Locations(IEnumerable<Plant> plants) =>
        plants
            .Where(p => !p.IsDeleted && !string.IsNullOrWhiteSpace(p.Location))
            .Select(p => p.Location!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.CurrentCultureIgnoreCase)
            .ToList();
}
