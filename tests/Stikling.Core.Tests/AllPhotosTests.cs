using Stikling.Core.Models;
using Stikling.Core.Photos;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class AllPhotosTests
{
    private static readonly TimeProvider Time = new FixedTime(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

    private static Photo PhotoOf(Guid subject, DateTimeOffset taken, SubjectType kind = SubjectType.Plant, bool reference = false, bool deleted = false) =>
        new() { SubjectType = kind, SubjectId = subject, TakenAt = taken, IsReference = reference, DeletedAt = deleted ? taken : null };

    private static DateTimeOffset At(int year, int month, int day) => new(year, month, day, 12, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<PhotoTile> Tiles(IEnumerable<Photo> photos, IEnumerable<Plant> plants, IEnumerable<Propagation>? propagations = null,
        Places? places = null, AllPhotosFilter? filter = null) =>
        AllPhotos.Tiles(photos, plants, propagations ?? [], places ?? Places.None, filter);

    [Fact]
    public void Reference_deleted_and_other_kinds_of_photos_are_left_out()
    {
        var plant = new Plant { Nickname = "Monstera" };
        var photos = new[]
        {
            PhotoOf(plant.Id, At(2026, 3, 1)),
            PhotoOf(plant.Id, At(2026, 3, 2), reference: true),
            PhotoOf(plant.Id, At(2026, 3, 3), deleted: true),
            PhotoOf(plant.Id, At(2026, 3, 4), SubjectType.Place),
            PhotoOf(plant.Id, At(2026, 3, 5), SubjectType.Wish),
        };

        var tiles = Tiles(photos, [plant]);

        Assert.Single(tiles);
        Assert.Equal("Monstera", tiles[0].Name);
    }

    [Fact]
    public void Photos_of_a_missing_or_deleted_subject_are_left_out()
    {
        var gone = new Plant { Nickname = "Gone", DeletedAt = At(2026, 4, 1) };
        var goneCutting = new Propagation { Nickname = "Gone too", DeletedAt = At(2026, 4, 1) };
        var photos = new[]
        {
            PhotoOf(gone.Id, At(2026, 3, 1)),
            PhotoOf(goneCutting.Id, At(2026, 3, 1), SubjectType.Propagation),
            PhotoOf(Guid.NewGuid(), At(2026, 3, 1)),
        };

        Assert.Empty(Tiles(photos, [gone], [goneCutting]));
    }

    [Fact]
    public void Plants_and_cuttings_both_count_and_received_history_photos_stay()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var cutting = new Propagation { Nickname = "Hoya cutting" };
        var photos = new[]
        {
            new Photo { SubjectType = SubjectType.Plant, SubjectId = plant.Id, TakenAt = At(2026, 3, 1), ReceivedHistoryId = Guid.NewGuid() },
            PhotoOf(cutting.Id, At(2026, 3, 2), SubjectType.Propagation),
        };

        var tiles = Tiles(photos, [plant], [cutting]);

        Assert.Equal(2, tiles.Count);
        Assert.Contains(tiles, t => t.Kind == SubjectType.Propagation && t.SubjectId == cutting.Id);
    }

    [Fact]
    public void Months_are_newest_first_and_so_are_the_photos_in_them()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var photos = new[]
        {
            PhotoOf(plant.Id, At(2026, 3, 5)),
            PhotoOf(plant.Id, At(2025, 12, 31)),
            PhotoOf(plant.Id, At(2026, 3, 20)),
            PhotoOf(plant.Id, At(2026, 5, 1)),
        };

        var months = AllPhotos.Months(photos, [plant], [], Places.None, Time);

        Assert.Equal([(2026, 5), (2026, 3), (2025, 12)], months.Select(m => (m.Year, m.Month)));
        Assert.Equal([At(2026, 3, 20), At(2026, 3, 5)], months[1].Photos.Select(t => t.Photo.TakenAt));
    }

    [Fact]
    public void A_photo_belongs_to_the_month_in_local_time()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var photos = new[] { PhotoOf(plant.Id, new DateTimeOffset(2026, 3, 31, 23, 30, 0, TimeSpan.Zero)) };

        var months = AllPhotos.Months(photos, [plant], [], Places.None, new FixedTime(At(2026, 5, 1), TimeSpan.FromHours(2)));

        Assert.Equal((2026, 4), (months[0].Year, months[0].Month));
    }

    [Fact]
    public void A_plant_filter_shows_only_that_plants_own_photos()
    {
        var plant = new Plant { Nickname = "Hoya" };
        var other = new Plant { Nickname = "Monstera" };
        var cutting = new Propagation { Nickname = "Cutting", ParentPlantId = plant.Id };
        var photos = new[]
        {
            PhotoOf(plant.Id, At(2026, 3, 1)), PhotoOf(other.Id, At(2026, 3, 1)), PhotoOf(cutting.Id, At(2026, 3, 1), SubjectType.Propagation),
        };

        var tiles = Tiles(photos, [plant, other], [cutting], filter: new AllPhotosFilter(PlantId: plant.Id));

        Assert.Equal([plant.Id], tiles.Select(t => t.SubjectId));
    }

    [Fact]
    public void A_room_filter_includes_spots_and_cuttings_and_leaves_out_unplaced_ones()
    {
        var room = new Place { Name = "Living room" };
        var spot = new Place { Name = "Windowsill", RoomId = room.Id };
        var kitchen = new Place { Name = "Kitchen" };
        var places = new Places([room, spot, kitchen]);
        var inRoom = new Plant { Nickname = "A", PlaceId = room.Id };
        var onSpot = new Plant { Nickname = "B", PlaceId = spot.Id };
        var elsewhere = new Plant { Nickname = "C", PlaceId = kitchen.Id };
        var unplaced = new Plant { Nickname = "D" };
        var cutting = new Propagation { Nickname = "E", PlaceId = spot.Id };
        var cuttingUnplaced = new Propagation { Nickname = "F" };
        var photos = new[]
        {
            PhotoOf(inRoom.Id, At(2026, 3, 1)), PhotoOf(onSpot.Id, At(2026, 3, 1)), PhotoOf(elsewhere.Id, At(2026, 3, 1)),
            PhotoOf(unplaced.Id, At(2026, 3, 1)), PhotoOf(cutting.Id, At(2026, 3, 1), SubjectType.Propagation),
            PhotoOf(cuttingUnplaced.Id, At(2026, 3, 1), SubjectType.Propagation),
        };

        var tiles = Tiles(photos, [inRoom, onSpot, elsewhere, unplaced], [cutting, cuttingUnplaced], places, new AllPhotosFilter(RoomId: room.Id));

        Assert.Equal(["A", "B", "E"], tiles.Select(t => t.Name).Order());
    }

    [Fact]
    public void A_tag_filter_shows_plants_with_the_tag_and_no_cuttings()
    {
        var rare = new Plant { Nickname = "Rare one", Tags = ["Rare"] };
        var plain = new Plant { Nickname = "Plain" };
        var cutting = new Propagation { Nickname = "Cutting" };
        var photos = new[]
        {
            PhotoOf(rare.Id, At(2026, 3, 1)), PhotoOf(plain.Id, At(2026, 3, 1)), PhotoOf(cutting.Id, At(2026, 3, 1), SubjectType.Propagation),
        };

        var tiles = Tiles(photos, [rare, plain], [cutting], filter: new AllPhotosFilter(Tag: " rare "));

        Assert.Equal(["Rare one"], tiles.Select(t => t.Name));
    }
}
