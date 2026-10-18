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

/// <summary>A need the plant list can be narrowed by. See <see cref="CareLikes"/>.</summary>
public enum LikesFilter
{
    HighHumidity,
    StaysMoist,
    DriesOut,
    RestsInWinter,
    RestsInSummer,
    DarkerThanItLikes
}

/// <summary>
/// One pick in the Fields filter. A null <paramref name="FieldId"/> is How it was raised, and
/// <paramref name="Value"/> a <see cref="RaisedAs"/> name matched against the effective value. Otherwise
/// it is a field of the person's own and the value is one of its choices. A null value is "Not set".
/// </summary>
public sealed record FieldPick(Guid? FieldId, string? Value);

/// <summary>Search, filter and sort rules for the plant list.</summary>
/// <param name="PlaceIds">The room or spot picked, as every id that counts as being in it
/// (see <see cref="Rooms.Places.IdsIn"/>). A room also shows what's in its spots.</param>
/// <param name="Tags">Picking several tags narrows the list: a plant has to carry all of them.</param>
/// <param name="Quarantine">Only the plants in quarantine.</param>
/// <param name="Dormant">Only the dormant plants.</param>
/// <param name="OnlyIds">Only these plants, like the ones with an open problem. Null for no limit.</param>
/// <param name="Sort">Name sorts A to Z. Room sorts by the room or spot's name, with the plants that
/// have no place last. Newest and last activity put the most recent first.</param>
/// <param name="PlaceName">The name of a plant's room or spot, for sorting by room.</param>
/// <param name="LastActivity">When each plant last had something on its timeline, by plant id.
/// A plant with nothing there counts from when it was added.</param>
/// <param name="GoneAs">Under Gone, only the plants that left this way (died, given away or sold). Ignored for the other statuses.</param>
/// <param name="Likes">Only the plants with this need: high humidity, stays moist, dries out, rests in
/// winter or summer, or stands darker than it likes. Null for no limit.</param>
/// <param name="Everyday">The everyday name shown for a plant, like "Snake plant", which the search looks through too.</param>
/// <param name="Picks">Picks on How it was raised and the choice fields. A plant has to match every one.</param>
/// <param name="FieldText">The values of the plant's own text and choice fields, which the search looks through too.</param>
public sealed record PlantFilter(
    string? Search = null,
    StatusFilter Status = StatusFilter.Active,
    IReadOnlySet<Guid>? PlaceIds = null,
    IReadOnlyCollection<string>? Tags = null,
    bool Quarantine = false,
    bool Dormant = false,
    PlantSort Sort = PlantSort.Name,
    Func<Guid?, string?>? PlaceName = null,
    IReadOnlyDictionary<Guid, DateTimeOffset>? LastActivity = null,
    Func<Plant, string?>? Everyday = null,
    PlantStatus? GoneAs = null,
    IReadOnlySet<Guid>? OnlyIds = null,
    LikesFilter? Likes = null,
    Func<Plant, IEnumerable<string>>? FieldText = null,
    IReadOnlyList<FieldPick>? Picks = null)
{
    public IEnumerable<Plant> Apply(IEnumerable<Plant> plants) =>
        Order(plants
            .Where(p => !p.IsDeleted)
            .Where(MatchesStatus)
            .Where(MatchesLocation)
            .Where(p => !Quarantine || p.InQuarantine)
            .Where(p => !Dormant || p.IsDormant)
            .Where(p => OnlyIds is null || OnlyIds.Contains(p.Id))
            .Where(MatchesLikes)
            .Where(MatchesPicks)
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
        StatusFilter.Gone => plant.Status != PlantStatus.Active && (GoneAs is null || plant.Status == GoneAs),
        _ => true
    };

    private bool MatchesLikes(Plant plant) => Likes switch
    {
        LikesFilter.HighHumidity => plant.LikesHumidity == HumidityNeed.High,
        LikesFilter.StaysMoist => plant.LikesWater == WaterNeed.EvenlyMoist,
        LikesFilter.DriesOut => plant.LikesWater == WaterNeed.DryOut,
        LikesFilter.RestsInWinter => plant.GrowingSeason == GrowingSeason.Summer,
        LikesFilter.RestsInSummer => plant.GrowingSeason == GrowingSeason.Winter,
        LikesFilter.DarkerThanItLikes => CareLikes.Fit(plant) == LightFit.Darker,
        _ => true
    };

    private bool MatchesPicks(Plant plant) => Picks is null || Picks.All(pick =>
    {
        var have = pick.FieldId is { } id
            ? plant.OwnFields.TryGetValue(id, out var text) && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null
            : plant.Raised?.ToString();
        return pick.Value is null
            ? have is null
            : have is not null && string.Equals(have, pick.Value.Trim(), StringComparison.CurrentCultureIgnoreCase);
    });

    private bool MatchesLocation(Plant plant) =>
        PlaceIds is null || plant.PlaceId is { } id && PlaceIds.Contains(id);

    // Every word typed must appear somewhere in the plant's names or tags, so "thai monstera"
    // finds "Monstera deliciosa 'Thai Constellation'"
    private bool MatchesSearch(Plant plant)
    {
        if (string.IsNullOrWhiteSpace(Search))
            return true;

        var haystack = string.Join(' ', [plant.Nickname, plant.Genus, plant.Species, plant.Cultivar, plant.NameNote, plant.BotanicalName, Everyday?.Invoke(plant), .. plant.Tags, .. FieldText?.Invoke(plant) ?? []]);
        return Search
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(word => haystack.Contains(word, StringComparison.CurrentCultureIgnoreCase)
                || CollectionNumbers.MatchesSearch(plant.CollectionNumber, word));
    }
}
