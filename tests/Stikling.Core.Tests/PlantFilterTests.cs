using Stikling.Core.Models;
using Stikling.Core.Plants;

namespace Stikling.Core.Tests;

public class PlantFilterTests
{
    private static readonly Plant Thai = new() { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation", Location = "Living room" };
    private static readonly Plant Monstera = new() { Nickname = "Big Monstera", Genus = "Monstera", Species = "deliciosa", Location = "living room " };
    private static readonly Plant Basil = new() { Nickname = "Kitchen basil", Genus = "Ocimum", Location = "Kitchen" };
    private static readonly Plant DeadColeus = new() { Nickname = "Coleus", Status = PlantStatus.Died };
    private static readonly Plant GivenAway = new() { Nickname = "Pothos", Status = PlantStatus.GivenAway };
    private static readonly Plant Deleted = new() { Nickname = "Deleted", DeletedAt = DateTimeOffset.UnixEpoch };

    private static readonly Plant[] All = [Thai, Monstera, Basil, DeadColeus, GivenAway, Deleted];

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
    public void Location_filter_ignores_case_and_whitespace()
    {
        var result = new PlantFilter(Location: "LIVING ROOM").Apply(All).ToList();

        Assert.Equal([Monstera, Thai], result);
    }

    [Fact]
    public void A_room_also_shows_what_sits_in_its_spots()
    {
        var pc = new Plant { Nickname = "Pilea", Location = "Living room / On top of the PC" };

        var result = new PlantFilter(Location: "Living room").Apply([.. All, pc]).ToList();

        Assert.Equal([Monstera, Thai, pc], result);
    }

    [Fact]
    public void A_spot_shows_only_what_sits_in_it()
    {
        var pc = new Plant { Nickname = "Pilea", Location = "Living room / On top of the PC" };

        var result = new PlantFilter(Location: "Living room / On top of the PC").Apply([.. All, pc]).ToList();

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
}
