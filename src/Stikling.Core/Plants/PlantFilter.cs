using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Plants;

/// <summary>Which plants to show in the list.</summary>
public enum StatusFilter
{
    Active,
    Gone,
    All
}

/// <summary>Search, filter and sort rules for the plant list.</summary>
/// <param name="Location">A room, or a spot inside one. A room also shows what's in its spots.</param>
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
        string.IsNullOrWhiteSpace(Location) || RoomName.IsIn(plant.Location, Location);

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
}
