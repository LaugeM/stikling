using Stikling.Core.Models;
using Stikling.Core.Rooms;
using Stikling.Core.Shelves;

namespace Stikling.Core.Tests;

public class ShelfTests
{
    private static readonly DateTimeOffset Day1 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Day2 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private sealed class Setup
    {
        public FakePhotoMarkRepository Marks { get; } = new();
        public FakePhotoRepository Photos { get; } = new();
        public FakePlantRepository Plants { get; } = new();
        public FakePropagationRepository Propagations { get; } = new();
        public FakePlaceRepository Places { get; } = new();
        public ShelfService Service { get; }

        public Setup() => Service = new ShelfService(Marks, Photos, Plants, Propagations, Places);

        public Plant AddPlant(string name, Guid? placeId = null, PlantStatus status = PlantStatus.Active)
        {
            var plant = new Plant { Nickname = name, PlaceId = placeId, Status = status };
            Plants.Plants[plant.Id] = plant;
            return plant;
        }

        public Propagation AddPropagation(string name, Guid? placeId = null, PropagationStage stage = PropagationStage.Started)
        {
            var propagation = new Propagation { Nickname = name, PlaceId = placeId, Stage = stage };
            Propagations.Propagations[propagation.Id] = propagation;
            return propagation;
        }

        public Photo AddPhoto(Guid placeId, DateTimeOffset takenAt)
        {
            var photo = new Photo { SubjectType = SubjectType.Place, SubjectId = placeId, TakenAt = takenAt, Width = 4000, Height = 3000 };
            Photos.Photos[photo.Id] = photo;
            return photo;
        }

        public Task<PhotoMark> MarkAsync(Photo photo, Entity subject, double x = 0.5, double y = 0.5) =>
            Service.MarkAsync(photo.Id, subject is Plant ? SubjectType.Plant : SubjectType.Propagation, subject.Id, x, y);
    }

    [Fact]
    public async Task A_mark_of_a_deleted_plant_is_hidden_and_shows_again_when_the_plant_comes_back()
    {
        var s = new Setup();
        var room = s.Places.Add("Living room");
        var photo = s.AddPhoto(room.Id, Day1);
        var fern = s.AddPlant("Fern");
        var ivy = s.AddPlant("Ivy");
        await s.MarkAsync(photo, fern);
        await s.MarkAsync(photo, ivy, 0.2, 0.3);

        await s.Plants.DeleteAsync(fern.Id);
        Assert.Equal("Ivy", Assert.Single((await s.Service.GetPhotoAsync(photo.Id))!.Marks).Name);
        Assert.Equal(1, (await s.Service.GetGroupsAsync()).Single().Tiles.Single().Marks);
        Assert.Equal(2, s.Marks.Marks.Count(m => !m.Value.IsDeleted));

        // A restore brings the plant back
        fern.DeletedAt = null;
        Assert.Equal(["Fern", "Ivy"], (await s.Service.GetPhotoAsync(photo.Id))!.Marks.Select(m => m.Name).Order());
    }

    [Fact]
    public async Task A_plant_that_is_gone_is_faded_and_a_plant_in_the_collection_is_not()
    {
        var s = new Setup();
        var photo = s.AddPhoto(s.Places.Add("Living room").Id, Day1);
        var kept = s.AddPlant("Kept");
        var died = s.AddPlant("Died", status: PlantStatus.Died);
        var sold = s.AddPlant("Sold", status: PlantStatus.Sold);
        var finished = s.AddPropagation("Done", stage: PropagationStage.Done);
        var going = s.AddPropagation("Going");
        foreach (var subject in new Entity[] { kept, died, sold, finished, going })
            await s.MarkAsync(photo, subject);

        var shown = (await s.Service.GetPhotoAsync(photo.Id))!.Marks.ToDictionary(m => m.Subject.Id, m => m.Faded);
        Assert.False(shown[kept.Id]);
        Assert.True(shown[died.Id]);
        Assert.True(shown[sold.Id]);
        Assert.True(shown[finished.Id]);
        Assert.False(shown[going.Id]);
    }

    [Fact]
    public void A_mark_with_no_plant_is_skipped_and_never_throws()
    {
        var plant = new Plant { Nickname = "Fern" };
        var marks = new[]
        {
            new PhotoMark { PhotoId = Guid.NewGuid(), SubjectType = SubjectType.Plant, SubjectId = Guid.NewGuid() },
            new PhotoMark { PhotoId = Guid.NewGuid(), SubjectType = SubjectType.Plant, SubjectId = plant.Id },
            // Pointing at a plant by the wrong kind
            new PhotoMark { PhotoId = Guid.NewGuid(), SubjectType = SubjectType.Propagation, SubjectId = plant.Id }
        };

        var shown = ShelfMarks.Visible(marks, [plant], []);

        Assert.Equal(plant.Id, Assert.Single(shown).Subject.Id);
    }

    [Fact]
    public void History_has_one_item_per_photo_even_when_two_devices_marked_the_plant_twice()
    {
        var plant = new Plant { Nickname = "Fern" };
        var place = Guid.NewGuid();
        var older = new Photo { SubjectType = SubjectType.Place, SubjectId = place, TakenAt = Day1 };
        var newer = new Photo { SubjectType = SubjectType.Place, SubjectId = place, TakenAt = Day2 };
        var marks = new[]
        {
            new PhotoMark { PhotoId = newer.Id, SubjectType = SubjectType.Plant, SubjectId = plant.Id, X = 0.9, Y = 0.9, CreatedAt = Day2.AddHours(2) },
            new PhotoMark { PhotoId = newer.Id, SubjectType = SubjectType.Plant, SubjectId = plant.Id, X = 0.1, Y = 0.2, CreatedAt = Day2.AddHours(1) },
            new PhotoMark { PhotoId = older.Id, SubjectType = SubjectType.Plant, SubjectId = plant.Id, X = 0.4, Y = 0.5, CreatedAt = Day1 },
            new PhotoMark { PhotoId = older.Id, SubjectType = SubjectType.Plant, SubjectId = Guid.NewGuid(), X = 0.7, Y = 0.7, CreatedAt = Day1 }
        };

        var items = ShelfMarks.ForHistory(plant.Id, marks, [older, newer]);

        Assert.Equal([(newer.Id, 0.1, 0.2), (older.Id, 0.4, 0.5)], items.Select(i => (i.Photo.Id, i.X, i.Y)));
    }

    [Fact]
    public void History_leaves_out_a_mark_whose_photo_is_not_there_or_is_deleted()
    {
        var plant = new Plant { Nickname = "Fern" };
        var gone = new Photo { SubjectType = SubjectType.Place, SubjectId = Guid.NewGuid(), TakenAt = Day1, DeletedAt = Day2 };
        var marks = new[]
        {
            new PhotoMark { PhotoId = Guid.NewGuid(), SubjectType = SubjectType.Plant, SubjectId = plant.Id },
            new PhotoMark { PhotoId = gone.Id, SubjectType = SubjectType.Plant, SubjectId = plant.Id }
        };

        Assert.Empty(ShelfMarks.ForHistory(plant.Id, marks, [gone]));
    }

    [Fact]
    public async Task History_names_the_place_and_is_empty_for_a_plant_that_was_never_marked()
    {
        var s = new Setup();
        var room = s.Places.Add("Living room");
        var spot = s.Places.Add("On top of the PC", room);
        var photo = s.AddPhoto(spot.Id, Day1);
        var fern = s.AddPlant("Fern");
        await s.MarkAsync(photo, fern, 0.3, 0.6);

        var item = Assert.Single(await s.Service.HistoryForAsync(fern.Id));
        Assert.Equal("Living room / On top of the PC", item.PlaceName);
        Assert.Equal((0.3, 0.6), (item.X, item.Y));
        Assert.Empty(await s.Service.HistoryForAsync(s.AddPlant("Other").Id));
    }

    [Fact]
    public async Task Groups_follow_a_merged_place_and_a_removed_place_stays_visible_last()
    {
        var s = new Setup();
        var kitchen = s.Places.Add("Kitchen");
        var living = s.Places.Add("Living room");
        var old = s.Places.Add("Lounge");
        var removed = s.Places.Add("Shed");
        var onKitchen = s.AddPhoto(kitchen.Id, Day1);
        var onOld = s.AddPhoto(old.Id, Day2);
        var onLiving = s.AddPhoto(living.Id, Day1);
        var onRemoved = s.AddPhoto(removed.Id, Day1);

        old.MergedIntoId = living.Id;
        await s.Places.SaveAsync(old);
        await s.Places.DeleteAsync(old.Id);
        s.Places.Places.Remove(removed.Id);

        var groups = await s.Service.GetGroupsAsync();

        Assert.Equal(["Kitchen", "Living room", null], groups.Select(g => g.Name));
        Assert.Equal([onOld.Id, onLiving.Id], groups[1].Tiles.Select(t => t.Photo.Id));
        Assert.Equal([onKitchen.Id], groups[0].Tiles.Select(t => t.Photo.Id));
        Assert.Null(groups[2].Place);
        Assert.Equal([onRemoved.Id], groups[2].Tiles.Select(t => t.Photo.Id));
    }

    [Fact]
    public async Task A_plant_photo_is_not_a_shelf_photo()
    {
        var s = new Setup();
        var plant = s.AddPlant("Fern");
        s.Photos.Photos[Guid.NewGuid()] = new Photo { SubjectType = SubjectType.Plant, SubjectId = plant.Id, TakenAt = Day1 };

        Assert.Empty(await s.Service.GetGroupsAsync());
        Assert.Equal(0, await s.Service.CountAsync());
    }

    [Fact]
    public async Task Pick_list_puts_the_place_first_then_the_last_photo_then_the_rest_by_name()
    {
        var s = new Setup();
        var room = s.Places.Add("Living room");
        var spot = s.Places.Add("Shelf", room);
        var bedroom = s.Places.Add("Bedroom");
        var inRoom = s.AddPlant("Zebra plant", room.Id);
        var inSpot = s.AddPlant("Aloe", spot.Id);
        var inBedroom = s.AddPlant("Cactus", bedroom.Id);
        var seenBefore = s.AddPlant("Yucca", bedroom.Id);
        var other = s.AddPlant("Begonia", bedroom.Id);
        var cutting = s.AddPropagation("Pothos cutting", room.Id);
        var finished = s.AddPropagation("Old cutting", room.Id, PropagationStage.Done);
        var died = s.AddPlant("Dead one", room.Id, PlantStatus.Died);

        var earlier = s.AddPhoto(spot.Id, Day1);
        await s.MarkAsync(earlier, seenBefore);
        var photo = s.AddPhoto(spot.Id, Day2);
        var view = (await s.Service.GetPhotoAsync(photo.Id))!;

        var names = (await s.Service.PickListAsync(view, null)).Select(i => i.Name).ToList();

        // The spot's own plant is in the place. The room's are not in the spot.
        Assert.Equal(["Aloe", "Yucca", "Begonia", "Cactus", "Pothos cutting", "Zebra plant"], names);
        Assert.DoesNotContain("Old cutting", names);
        Assert.DoesNotContain("Dead one", names);

        // A photo of the room itself counts the spots too
        var roomPhoto = s.AddPhoto(room.Id, Day2);
        var roomNames = (await s.Service.PickListAsync((await s.Service.GetPhotoAsync(roomPhoto.Id))!, null)).Select(i => i.Name);
        Assert.Equal(["Aloe", "Pothos cutting", "Zebra plant", "Begonia", "Cactus", "Yucca"], roomNames);
    }

    [Fact]
    public async Task Pick_list_leaves_out_what_is_marked_already_and_filters_on_every_name_field()
    {
        var s = new Setup();
        var photo = s.AddPhoto(s.Places.Add("Living room").Id, Day1);
        var thai = new Plant { Genus = "Monstera", Species = "deliciosa", Cultivar = "Thai Constellation" };
        var plain = new Plant { Nickname = "Window plant", Genus = "Monstera", Species = "adansonii" };
        s.Plants.Plants[thai.Id] = thai;
        s.Plants.Plants[plain.Id] = plain;
        var seed = new Propagation { Genus = "Monstera", Species = "deliciosa" };
        s.Propagations.Propagations[seed.Id] = seed;
        await s.MarkAsync(photo, plain);
        var view = (await s.Service.GetPhotoAsync(photo.Id))!;

        Assert.DoesNotContain(await s.Service.PickListAsync(view, null), i => i.Subject.Id == plain.Id);
        Assert.Equal(2, (await s.Service.PickListAsync(view, "monstera")).Count);
        Assert.Equal(thai.Id, Assert.Single(await s.Service.PickListAsync(view, "thai const")).Subject.Id);
        Assert.Equal(thai.Id, Assert.Single(await s.Service.PickListAsync(view, "deliciosa CONST")).Subject.Id);
        Assert.Empty(await s.Service.PickListAsync(view, "monstera nothing"));
    }

    [Fact]
    public async Task Marking_keeps_the_position_inside_the_photo_and_rounds_it()
    {
        var s = new Setup();
        var photo = s.AddPhoto(s.Places.Add("Living room").Id, Day1);
        var fern = s.AddPlant("Fern");

        var edge = await s.MarkAsync(photo, fern, -0.3, 1.7);
        var inside = await s.MarkAsync(photo, fern, 0.123456789, 0.5);
        var nowhere = await s.MarkAsync(photo, fern, double.NaN, double.PositiveInfinity);

        Assert.Equal((0.0, 1.0), (edge.X, edge.Y));
        Assert.Equal((0.1235, 0.5), (inside.X, inside.Y));
        Assert.Equal((0.5, 0.5), (nowhere.X, nowhere.Y));
    }

    [Fact]
    public async Task Marking_never_moves_the_plant_and_deleting_a_plant_leaves_its_marks()
    {
        var s = new Setup();
        var room = s.Places.Add("Living room");
        var elsewhere = s.Places.Add("Kitchen");
        var photo = s.AddPhoto(room.Id, Day1);
        var fern = s.AddPlant("Fern", elsewhere.Id);

        await s.MarkAsync(photo, fern);
        Assert.Equal(elsewhere.Id, fern.PlaceId);

        await s.Plants.DeleteAsync(fern.Id);
        Assert.Single(await s.Marks.GetForSubjectAsync(fern.Id));
    }

    [Fact]
    public async Task Marking_a_photo_or_a_plant_that_is_not_there_throws()
    {
        var s = new Setup();
        var photo = s.AddPhoto(s.Places.Add("Living room").Id, Day1);
        var fern = s.AddPlant("Fern");

        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.MarkAsync(Guid.NewGuid(), SubjectType.Plant, fern.Id, 0.5, 0.5));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.MarkAsync(photo.Id, SubjectType.Plant, Guid.NewGuid(), 0.5, 0.5));
        await Assert.ThrowsAsync<ArgumentException>(() => s.Service.MarkAsync(photo.Id, SubjectType.Pot, fern.Id, 0.5, 0.5));
        Assert.Empty(s.Marks.Marks);
    }

    [Fact]
    public async Task Removing_a_mark_and_removing_the_marks_of_a_photo_delete_softly_and_leave_the_plants()
    {
        var s = new Setup();
        var room = s.Places.Add("Living room");
        var one = s.AddPhoto(room.Id, Day1);
        var two = s.AddPhoto(room.Id, Day2);
        var fern = s.AddPlant("Fern");
        var ivy = s.AddPlant("Ivy");
        var first = await s.MarkAsync(one, fern);
        await s.MarkAsync(one, ivy);
        await s.MarkAsync(two, fern);

        await s.Service.RemoveMarkAsync(first.Id);
        Assert.Equal("Ivy", Assert.Single((await s.Service.GetPhotoAsync(one.Id))!.Marks).Name);
        Assert.True(s.Marks.Marks[first.Id].IsDeleted);

        await s.Service.RemoveMarksOfPhotoAsync(one.Id);
        Assert.Empty((await s.Service.GetPhotoAsync(one.Id))!.Marks);
        Assert.Single((await s.Service.GetPhotoAsync(two.Id))!.Marks);
        Assert.Equal(2, s.Plants.Plants.Values.Count(p => !p.IsDeleted));
    }

    [Fact]
    public async Task Counting_for_a_place_includes_its_spots_and_a_place_merged_into_it()
    {
        var s = new Setup();
        var room = s.Places.Add("Living room");
        var spot = s.Places.Add("Shelf", room);
        var other = s.Places.Add("Kitchen");
        var lounge = s.Places.Add("Lounge");
        s.AddPhoto(room.Id, Day1);
        s.AddPhoto(spot.Id, Day1);
        s.AddPhoto(lounge.Id, Day1);
        s.AddPhoto(other.Id, Day1);
        lounge.MergedIntoId = room.Id;
        await s.Places.DeleteAsync(lounge.Id);

        Assert.Equal(3, await s.Service.CountForPlaceAsync(room.Id));
        Assert.Equal(1, await s.Service.CountForPlaceAsync(spot.Id));
        Assert.Equal(1, await s.Service.CountForPlaceAsync(other.Id));
        Assert.Equal(4, await s.Service.CountAsync());
    }
}
