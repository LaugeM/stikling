using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantFilterTests
{
    private static readonly FakePlaceRepository Places = new();

    private static readonly Plant Thai = new() { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation", PlaceId = Places.IdOf("Living room") };
    private static readonly Plant Monstera = new() { Nickname = "Big Monstera", Genus = "Monstera", Species = "deliciosa", PlaceId = Places.IdOf("living room ") };
    private static readonly Plant Basil = new() { Nickname = "Kitchen basil", Genus = "Ocimum", PlaceId = Places.IdOf("Kitchen") };
    private static readonly Plant DeadColeus = new() { Nickname = "Coleus", Status = PlantStatus.Died };
    private static readonly Plant GivenAway = new() { Nickname = "Pothos", Status = PlantStatus.GivenAway };
    private static readonly Plant Deleted = new() { Nickname = "Deleted", DeletedAt = DateTimeOffset.UnixEpoch };

    private static readonly Plant[] All = [Thai, Monstera, Basil, DeadColeus, GivenAway, Deleted];

    private static IReadOnlySet<Guid> In(string place) => Places.Current.IdsIn(Places.IdOf(place));

    [Fact]
    public void Default_shows_active_plants_sorted_by_name()
    {
        var result = new PlantFilter().Apply(All).ToList();

        Assert.Equal([Monstera, Basil, Thai], result);
    }

    [Fact]
    public void Gone_shows_plants_that_are_no_longer_in_the_collection()
    {
        var result = new PlantFilter(Status: StatusFilter.Gone).Apply(All).ToList();

        Assert.Equal([DeadColeus, GivenAway], result);
    }

    [Fact]
    public void All_never_includes_deleted_plants()
    {
        var result = new PlantFilter(Status: StatusFilter.All).Apply(All).ToList();

        Assert.Equal(5, result.Count);
        Assert.DoesNotContain(Deleted, result);
    }

    [Theory]
    [InlineData("thai monstera")]
    [InlineData("CONSTELLATION")]
    [InlineData("  thai  ")]
    public void Search_matches_every_word_in_any_name_field(string search)
    {
        var result = new PlantFilter(search).Apply(All).ToList();

        Assert.Equal([Thai], result);
    }

    [Fact]
    public void Search_finds_nicknames()
    {
        Assert.Equal([Basil], new PlantFilter("basil").Apply(All));
    }

    [Fact]
    public void Search_finds_the_everyday_name_shown()
    {
        var filter = new PlantFilter("swiss cheese", Everyday: p => p.Species == "deliciosa" ? "Swiss cheese plant" : null);

        Assert.Equal([Monstera, Thai], filter.Apply(All));
    }

    [Fact]
    public void A_room_shows_the_plants_in_it()
    {
        var result = new PlantFilter(PlaceIds: In("Living room")).Apply(All).ToList();

        Assert.Equal([Monstera, Thai], result);
    }

    [Fact]
    public void A_room_also_shows_what_sits_in_its_spots()
    {
        var pc = new Plant { Nickname = "Pilea", PlaceId = Places.IdOf("Living room / On top of the PC") };

        var result = new PlantFilter(PlaceIds: In("Living room")).Apply([.. All, pc]).ToList();

        Assert.Equal([Monstera, Thai, pc], result);
    }

    [Fact]
    public void A_spot_shows_only_what_sits_in_it()
    {
        var pc = new Plant { Nickname = "Pilea", PlaceId = Places.IdOf("Living room / On top of the PC") };

        var result = new PlantFilter(PlaceIds: In("Living room / On top of the PC")).Apply([.. All, pc]).ToList();

        Assert.Equal([pc], result);
    }

    [Fact]
    public void Picked_tags_must_all_be_on_the_plant()
    {
        var rare = new Plant { Nickname = "Rare one", Tags = ["Rare"] };
        var both = new Plant { Nickname = "Swap one", Tags = ["rare", "For swap"] };

        Assert.Equal([rare, both], new PlantFilter(Tags: ["RARE"]).Apply([.. All, rare, both]));
        Assert.Equal([both], new PlantFilter(Tags: ["Rare", "for swap"]).Apply([.. All, rare, both]));
    }

    [Fact]
    public void Quarantine_shows_only_plants_in_quarantine()
    {
        var isolated = new Plant { Nickname = "New alocasia", QuarantinedSince = new DateOnly(2026, 9, 1) };

        Assert.Equal([isolated], new PlantFilter(Quarantine: true).Apply([.. All, isolated]));
    }

    [Fact]
    public void Search_also_finds_tags()
    {
        var swap = new Plant { Nickname = "Hoya", Tags = ["For swap"] };

        Assert.Equal([swap], new PlantFilter("swap").Apply([.. All, swap]));
    }

    [Fact]
    public void Favourites_come_first_with_the_default_sort()
    {
        Plant zinnia = new() { Nickname = "Zinnia", Favourite = true };
        Plant aloe = new() { Nickname = "Aloe" };
        Plant yucca = new() { Nickname = "Yucca", Favourite = true };

        var result = new PlantFilter().Apply([aloe, zinnia, yucca]).ToList();

        Assert.Equal([yucca, zinnia, aloe], result);
    }

    [Fact]
    public void Favourites_come_first_and_the_chosen_sort_applies_within_each_group()
    {
        Plant oldFavourite = new() { Nickname = "A", Favourite = true, CreatedAt = DateTimeOffset.UnixEpoch };
        Plant newFavourite = new() { Nickname = "B", Favourite = true, CreatedAt = DateTimeOffset.UnixEpoch.AddDays(2) };
        Plant newest = new() { Nickname = "C", CreatedAt = DateTimeOffset.UnixEpoch.AddDays(9) };
        Plant older = new() { Nickname = "D", CreatedAt = DateTimeOffset.UnixEpoch.AddDays(5) };

        var result = new PlantFilter(Sort: PlantSort.Newest).Apply([older, oldFavourite, newest, newFavourite]).ToList();

        Assert.Equal([newFavourite, oldFavourite, newest, older], result);
    }

    [Fact]
    public void Newest_puts_the_latest_added_first()
    {
        Plant old = new() { Nickname = "A", CreatedAt = DateTimeOffset.UnixEpoch };
        Plant recent = new() { Nickname = "B", CreatedAt = DateTimeOffset.UnixEpoch.AddDays(5) };

        var result = new PlantFilter(Sort: PlantSort.Newest).Apply([old, recent]).ToList();

        Assert.Equal([recent, old], result);
    }

    [Fact]
    public void Room_sorts_by_place_name_with_plants_without_a_place_last()
    {
        Plant homeless = new() { Nickname = "Aloe" };

        var result = new PlantFilter(Sort: PlantSort.Room, PlaceName: Places.Current.NameOf)
            .Apply([Thai, homeless, Basil, Monstera]).ToList();

        Assert.Equal([Basil, Monstera, Thai, homeless], result);
    }

    [Fact]
    public void Last_activity_uses_the_latest_timeline_entry_or_else_when_the_plant_was_added()
    {
        Plant quiet = new() { Nickname = "A", CreatedAt = DateTimeOffset.UnixEpoch.AddDays(3) };
        Plant busy = new() { Nickname = "B", CreatedAt = DateTimeOffset.UnixEpoch };
        var activity = new Dictionary<Guid, DateTimeOffset> { [busy.Id] = DateTimeOffset.UnixEpoch.AddDays(10) };

        var result = new PlantFilter(Sort: PlantSort.LastActivity, LastActivity: activity).Apply([quiet, busy]).ToList();

        Assert.Equal([busy, quiet], result);
    }

    [Fact]
    public void Activity_takes_the_newest_of_timeline_and_care()
    {
        var plant = Guid.NewGuid();
        var entries = new Dictionary<Guid, TimelineEntry>
        {
            [plant] = new() { SubjectId = plant, OccurredAt = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero) }
        };
        CareLog[] care =
        [
            new() { PlantId = plant, OccurredOn = new DateOnly(2026, 5, 3) },
            new() { PlantId = plant, OccurredOn = new DateOnly(2026, 6, 1), DeletedAt = DateTimeOffset.UnixEpoch }
        ];

        var latest = PlantActivity.Latest(entries, care);

        Assert.Equal(new DateTimeOffset(2026, 5, 3, 0, 0, 0, TimeSpan.Zero), latest[plant]);
    }

    [Theory]
    [InlineData(PlantStatus.Died, "Coleus")]
    [InlineData(PlantStatus.GivenAway, "Pothos")]
    [InlineData(PlantStatus.Sold, "Sold fern")]
    public void Gone_can_be_narrowed_to_how_the_plants_left(PlantStatus how, string name)
    {
        Plant[] plants = [.. All, new() { Nickname = "Sold fern", Status = PlantStatus.Sold }];

        var result = new PlantFilter(Status: StatusFilter.Gone, GoneAs: how).Apply(plants).ToList();

        Assert.Equal([name], result.Select(p => p.DisplayName));
    }

    [Fact]
    public void How_they_left_is_ignored_unless_the_status_is_gone()
    {
        Assert.Equal(3, new PlantFilter(GoneAs: PlantStatus.Died).Apply(All).Count());
        Assert.Equal(5, new PlantFilter(Status: StatusFilter.All, GoneAs: PlantStatus.Died).Apply(All).Count());
    }
}
