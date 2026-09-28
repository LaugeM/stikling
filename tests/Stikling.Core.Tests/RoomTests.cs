using Stikling.Core.Models;
using Stikling.Core.Rooms;

namespace Stikling.Core.Tests;

public class RoomNameTests
{
    [Theory]
    [InlineData("  Living room  ", "Living room")]
    [InlineData("Living room/On top of the PC", "Living room / On top of the PC")]
    [InlineData("Living room /  On top of the PC ", "Living room / On top of the PC")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Places_are_tidied_the_same_way_however_they_were_typed(string? typed, string? expected) =>
        Assert.Equal(expected, RoomName.Clean(typed));

    [Fact]
    public void A_place_splits_into_the_room_and_the_spot_inside_it()
    {
        Assert.Equal(("Living room", "On top of the PC"), RoomName.Split("Living room / On top of the PC"));
        Assert.Equal(("Living room", null), RoomName.Split("Living room"));
    }
}

public class PlacesTests
{
    private readonly FakePlaceRepository repository = new();
    private readonly Place living;
    private readonly Place windowsill;
    private readonly Place kitchen;

    public PlacesTests()
    {
        living = repository.Add("Living room");
        windowsill = repository.Add("Windowsill", living);
        kitchen = repository.Add("Kitchen");
    }

    private Places Places => repository.Current;

    [Fact]
    public void A_spot_is_named_with_its_room()
    {
        Assert.Equal("Living room", Places.NameOf(living.Id));
        Assert.Equal("Living room / Windowsill", Places.NameOf(windowsill.Id));
        Assert.Null(Places.NameOf(null));
        Assert.Null(Places.NameOf(Guid.NewGuid()));
    }

    [Fact]
    public void A_room_holds_itself_and_its_spots_but_not_another_room()
    {
        Assert.True(Places.IsIn(living.Id, living.Id));
        Assert.True(Places.IsIn(windowsill.Id, living.Id));
        Assert.False(Places.IsIn(living.Id, windowsill.Id));
        Assert.False(Places.IsIn(kitchen.Id, living.Id));
        Assert.False(Places.IsIn(null, living.Id));
    }

    [Fact]
    public void A_merged_place_leads_to_the_one_it_went_into()
    {
        var stue = repository.Add("Stue");
        stue.MergedIntoId = living.Id;
        stue.DeletedAt = DateTimeOffset.UnixEpoch;

        Assert.Same(living, Places.Find(stue.Id));
        Assert.Equal("Living room", Places.NameOf(stue.Id));
        Assert.True(Places.IsIn(stue.Id, living.Id));
        Assert.Contains(stue.Id, Places.IdsIn(living.Id));
    }

    [Fact]
    public void A_place_deleted_without_a_merge_is_gone()
    {
        var shed = repository.Add("Shed");
        repository.DeleteAsync(shed.Id);

        Assert.Null(Places.Find(shed.Id));
    }

    [Fact]
    public void Two_places_merged_into_each_other_dont_loop_forever()
    {
        living.MergedIntoId = kitchen.Id;
        living.DeletedAt = DateTimeOffset.UnixEpoch;
        kitchen.MergedIntoId = living.Id;
        kitchen.DeletedAt = DateTimeOffset.UnixEpoch;

        Assert.Null(Places.Find(living.Id));
    }

    [Fact]
    public void The_ids_in_a_room_are_the_room_and_its_spots()
    {
        Assert.Equal([living.Id, windowsill.Id], Places.IdsIn(living.Id).Order());
        Assert.Equal([windowsill.Id], Places.IdsIn(windowsill.Id));
    }

    [Fact]
    public void A_place_is_found_by_its_name_whatever_the_case()
    {
        Assert.Same(windowsill, Places.Named("living room / WINDOWSILL"));
        Assert.Same(kitchen, Places.Named("Kitchen"));
        Assert.Null(Places.Named("Kitchen / Windowsill"));
        Assert.Null(Places.Named("Bathroom"));
    }

    [Fact]
    public void The_oldest_of_two_rooms_with_the_same_name_is_the_one_found()
    {
        // Two devices can each make a "Kitchen" before they sync
        repository.Add("kitchen");

        Assert.Same(kitchen, Places.RoomNamed("Kitchen"));
    }
}

public class RoomListTests
{
    private readonly FakePlaceRepository places = new();
    private readonly Plant[] plants;
    private readonly Propagation[] propagations;

    public RoomListTests()
    {
        var living = places.Add("Living room");
        var pc = places.Add("On top of the PC", living);
        var kitchen = places.Add("Kitchen");
        var bathroom = places.Add("Bathroom");
        var shed = places.Add("Shed");
        places.Add("Hallway"); // nothing in it

        plants =
        [
            new() { Nickname = "Thai", PlaceId = living.Id },
            new() { Nickname = "Monstera", PlaceId = living.Id },
            new() { Nickname = "Pilea", PlaceId = pc.Id },
            new() { Nickname = "Basil", PlaceId = kitchen.Id },
            new() { Nickname = "Homeless" },
            new() { Nickname = "Deleted", PlaceId = shed.Id, DeletedAt = DateTimeOffset.UnixEpoch }
        ];
        propagations =
        [
            new() { Genus = "Coleus", PlaceId = pc.Id },
            new() { Genus = "Pothos", PlaceId = bathroom.Id }
        ];
    }

    private IReadOnlyList<Room> List() => Room.List(places.Current, plants, propagations);

    [Fact]
    public void Rooms_are_sorted_and_only_the_ones_something_is_in()
    {
        Assert.Equal(["Bathroom", "Kitchen", "Living room"], List().Select(r => r.Name));
    }

    [Fact]
    public void A_spot_is_listed_under_its_room_not_beside_it()
    {
        var living = List().Single(r => r.Name == "Living room");

        Assert.Equal(["On top of the PC"], living.Spots.Select(s => s.Name));
        Assert.Equal("Living room / On top of the PC", living.Spots[0].Place);
    }

    [Fact]
    public void A_room_counts_what_sits_in_it_and_what_sits_in_its_spots()
    {
        var living = List().Single(r => r.Name == "Living room");

        Assert.Equal(2, living.Plants); // Thai and Monstera, not Pilea on the PC
        Assert.Equal(3, living.AllPlants);
        Assert.Equal(1, living.AllPropagations);
        Assert.Equal(4, living.Total);
    }

    [Fact]
    public void A_room_nothing_sits_in_directly_still_appears_for_its_spots()
    {
        var office = places.Add("Office");
        var shelf = places.Add("Shelf", office);

        var rooms = Room.List(places.Current, [new Plant { Nickname = "Pilea", PlaceId = shelf.Id }], []);

        Assert.Equal("Office", rooms.Single().Name);
        Assert.Equal(0, rooms.Single().Plants);
        Assert.Equal(1, rooms.Single().AllPlants);
    }
}
