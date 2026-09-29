using Stikling.Core.Models;

namespace Stikling.Core.Plants;

/// <summary>Which plants to show in the list.</summary>
public enum StatusFilter
{
    Active,
    Gone,
    All
}

/// <summary>The order of the plant list.</summary>
public enum PlantSort
{
    Name,
    Newest,
    Room,
    LastActivity
}

/// <summary>Search, filter and sort rules for the plant list.</summary>
/// <param name="PlaceIds">The room or spot picked, as every id that counts as being in it
/// (see <see cref="Rooms.Places.IdsIn"/>). A room also shows what's in its spots.</param>
/// <param name="Tags">Picking several tags narrows the list: a plant has to carry all of them.</param>
/// <param name="Quarantine">Only the plants in quarantine.</param>
/// <param name="Dormant">Only the dormant plants.</param>
/// <param name="Sort">Name sorts A to Z. Room sorts by the room or spot's name, with the plants that
/// have no place last. Newest and last activity put the most recent first.</param>
/// <param name="PlaceName">The name of a plant's room or spot, for sorting by room.</param>
/// <param name="LastActivity">When each plant last had something on its timeline, by plant id.
/// A plant with nothing there counts from when it was added.</param>
public sealed record PlantFilter(
    string? Search = null,
    StatusFilter Status = StatusFilter.Active,
    IReadOnlySet<Guid>? PlaceIds = null,
    IReadOnlyCollection<string>? Tags = null,
    bool Quarantine = false,
    bool Dormant = false,
    PlantSort Sort = PlantSort.Name,
    Func<Guid?, string?>? PlaceName = null,
    IReadOnlyDictionary<Guid, DateTimeOffset>? LastActivity = null)
{
    public IEnumerable<Plant> Apply(IEnumerable<Plant> plants) =>
        Order(plants
            .Where(p => !p.IsDeleted)
            .Where(MatchesStatus)
            .Where(MatchesLocation)
            .Where(p => !Quarantine || p.InQuarantine)
            .Where(p => !Dormant || p.IsDormant)
            .Where(p => Tags is null || Tags.All(tag => PlantTags.Has(p, tag)))
            .Where(MatchesSearch));

    // Favourites come first whatever the sort. Every order falls back on the name, so plants that
    // tie keep a steady place
    private IEnumerable<Plant> Order(IEnumerable<Plant> all)
    {
        var plants = all.OrderBy(p => !p.Favourite);
        var byName = StringComparer.CurrentCultureIgnoreCase;
        return Sort switch
        {
            PlantSort.Newest => plants.ThenByDescending(p => p.CreatedAt).ThenBy(p => p.DisplayName, byName),
            PlantSort.Room => plants
                .ThenBy(p => RoomOf(p) is null)
                .ThenBy(RoomOf, byName)
                .ThenBy(p => p.DisplayName, byName),
            PlantSort.LastActivity => plants
                .ThenByDescending(p => LastActivity is not null && LastActivity.TryGetValue(p.Id, out var at) && at > p.CreatedAt ? at : p.CreatedAt)
                .ThenBy(p => p.DisplayName, byName),
            _ => plants.ThenBy(p => p.DisplayName, byName)
        };
    }

    private string? RoomOf(Plant plant) => PlaceName?.Invoke(plant.PlaceId);

    private bool MatchesStatus(Plant plant) => Status switch
    {
        StatusFilter.Active => plant.Status == PlantStatus.Active,
        StatusFilter.Gone => plant.Status != PlantStatus.Active,
        _ => true
    };

    private bool MatchesLocation(Plant plant) =>
        PlaceIds is null || plant.PlaceId is { } id && PlaceIds.Contains(id);

    // Every word typed must appear somewhere in the plant's names or tags, so "thai monstera"
    // finds "Monstera deliciosa 'Thai Constellation'"
    private bool MatchesSearch(Plant plant)
    {
        if (string.IsNullOrWhiteSpace(Search))
            return true;

        var haystack = string.Join(' ', [plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, .. plant.Tags]);
        return Search
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => haystack.Contains(word, StringComparison.CurrentCultureIgnoreCase));
    }
}
