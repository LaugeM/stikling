using Stikling.Core.Models;
using Stikling.Core.Sharing;

namespace Stikling.Core.Tests;

public class SwapTilesTests
{
    private static Propagation Cutting(string species, int spare, PropagationStage stage = PropagationStage.Rooted, Guid? cover = null) => new()
    {
        Genus = "Hoya", Species = species, InitialCount = spare, SpareCount = spare, Stage = stage,
        StartedOn = new DateOnly(2026, 10, 1), CoverPhotoId = cover
    };

    private static Plant Offered(string nickname, SpareMark mark, string? genus = null, string? species = null, string? number = null, Guid? cover = null) => new()
    {
        Nickname = nickname, Genus = genus, Species = species, Spare = mark, CollectionNumber = number, CoverPhotoId = cover
    };

    private static List<SwapTile> Tiles(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new SwapTile($"Plant {i}", "Plant", "plant", null, null))];

    [Fact]
    public void Tiles_follow_the_groups_and_order_of_the_text()
    {
        var tiles = SwapTiles.For(
            [Offered("Fern", SpareMark.UpForSwap), Offered("Zed", SpareMark.CuttingsToSpare, "Ficus", "lyrata")],
            [Cutting("pubicalyx", 2), Cutting("carnosa", 1, PropagationStage.Rooting), Cutting("carnosa", 3)],
            null, nicknames: false);

        Assert.Equal(["Hoya carnosa", "Hoya carnosa", "Hoya pubicalyx", "Ficus lyrata", "Fern"], tiles.Select(t => t.Name));
        Assert.Equal(["1 cutting, rooting", "3 cuttings, rooted", "2 cuttings, rooted", "Cuttings on request", "Plant"], tiles.Select(t => t.Detail));
        Assert.Equal(["propagation", "propagation", "propagation", "plant", "plant"], tiles.Select(t => t.Tone));
    }

    [Fact]
    public void The_same_name_and_stage_are_added_up_and_a_just_started_cutting_says_so()
    {
        var tiles = SwapTiles.For([], [Cutting("carnosa", 1), Cutting("carnosa", 1), Cutting("carnosa", 2, PropagationStage.Started)], null, false);

        Assert.Equal(["2 cuttings, just started", "2 cuttings, rooted"], tiles.Select(t => t.Detail));
    }

    [Fact]
    public void Plants_with_the_same_name_are_counted()
    {
        var tiles = SwapTiles.For([Offered("Fern", SpareMark.UpForSwap), Offered("Fern", SpareMark.UpForSwap)], [], null, false);

        var tile = Assert.Single(tiles);
        Assert.Equal("2 plants", tile.Detail);
    }

    [Fact]
    public void A_plant_with_a_number_is_on_its_own_with_the_number_first()
    {
        var tiles = SwapTiles.For(
            [Offered("Big", SpareMark.UpForSwap, "Monstera", "deliciosa", number: "117"),
             Offered("Small", SpareMark.UpForSwap, "Monstera", "deliciosa"),
             Offered("Small", SpareMark.UpForSwap, "Monstera", "deliciosa")],
            [], null, false);

        Assert.Equal(["Monstera deliciosa", "#117 Monstera deliciosa"], tiles.Select(t => t.Name).OrderBy(n => n.StartsWith('#')));
        Assert.Equal("Plant", tiles.Single(t => t.Name.StartsWith('#')).Detail);
        Assert.Equal("2 plants", tiles.Single(t => !t.Name.StartsWith('#')).Detail);
    }

    [Fact]
    public void Nicknames_are_added_in_quotes_only_when_switched_on()
    {
        Plant[] plants = [Offered("Big", SpareMark.UpForSwap, "Monstera", "deliciosa")];

        Assert.Equal("Monstera deliciosa", SwapTiles.For(plants, [], null, false).Single().Name);
        Assert.Equal("Monstera deliciosa \"Big\"", SwapTiles.For(plants, [], null, true).Single().Name);
    }

    [Fact]
    public void A_plant_without_a_botanical_name_uses_its_nickname()
    {
        Assert.Equal("Fern", SwapTiles.For([Offered("Fern", SpareMark.UpForSwap)], [], null, false).Single().Name);
    }

    [Fact]
    public void The_photo_is_the_cover_of_the_first_record_that_has_one_with_its_frame()
    {
        var photoId = Guid.NewGuid();
        var frame = new PhotoFrame(0.3, 0.6, 2);
        var first = Offered("Fern", SpareMark.UpForSwap);
        var second = Offered("Fern", SpareMark.UpForSwap, cover: photoId);
        var third = Offered("Fern", SpareMark.UpForSwap, cover: Guid.NewGuid());

        var tiles = SwapTiles.For([first, second, third], [], [new Photo { Id = photoId, Frame = frame }], false);

        var tile = Assert.Single(tiles);
        Assert.Equal(photoId, tile.PhotoId);
        Assert.Equal(frame, tile.Frame);
    }

    [Fact]
    public void A_cutting_uses_the_propagations_cover()
    {
        var photoId = Guid.NewGuid();

        var tile = SwapTiles.For([], [Cutting("carnosa", 1, cover: photoId)], [], false).Single();

        Assert.Equal(photoId, tile.PhotoId);
        Assert.Null(tile.Frame);
    }

    [Fact]
    public void Without_a_cover_the_photo_is_null()
    {
        var tile = SwapTiles.For([Offered("Fern", SpareMark.UpForSwap)], [], [], false).Single();

        Assert.Null(tile.PhotoId);
        Assert.Null(tile.Frame);
    }

    [Theory]
    [InlineData("square", 9, 1, 9)]
    [InlineData("square", 10, 2, 5)]
    [InlineData("portrait", 12, 1, 12)]
    [InlineData("story", 12, 1, 12)]
    [InlineData("portrait", 13, 2, 6)]
    [InlineData("story", 25, 3, 8)]
    public void Tiles_are_split_over_the_fewest_pictures(string format, int count, int pictures, int smallest)
    {
        var pages = SwapTiles.Split(Tiles(count), format);

        Assert.Equal(pictures, pages.Count);
        Assert.Equal(smallest, pages.Min(p => p.Count));
        Assert.Equal(count, pages.Sum(p => p.Count));
    }

    [Fact]
    public void Thirteen_at_twelve_make_seven_and_six_in_order()
    {
        var tiles = Tiles(13);

        var pages = SwapTiles.Split(tiles, "portrait");

        Assert.Equal([7, 6], pages.Select(p => p.Count));
        Assert.Equal(tiles, pages.SelectMany(p => p));
    }

    [Fact]
    public void No_tiles_make_no_pictures()
    {
        Assert.Empty(SwapTiles.Split([], "square"));
    }
}
